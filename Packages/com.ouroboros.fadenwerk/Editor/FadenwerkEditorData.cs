using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fadenwerk.EditorTools
{
    /// <summary>Zugriff auf Datenbank und die NPCs/Marker der geöffneten Szenen – nur im Editor, nur im Hauptthread.</summary>
    internal static class FadenwerkEditorData
    {
        public const string DefaultDatabasePath = "Assets/FadenwerkData/FadenwerkDatabase.asset";

        static FadenwerkDatabase _cached;

        /// <summary>Die aktive Datenbank: zuerst die eingestellte, sonst die erste im Projekt.</summary>
        public static FadenwerkDatabase Database
        {
            get
            {
                if (_cached != null) return _cached;
                string guid = FadenwerkSettings.DatabaseGuid;
                if (!string.IsNullOrEmpty(guid))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(path)) _cached = AssetDatabase.LoadAssetAtPath<FadenwerkDatabase>(path);
                }
                if (_cached == null)
                {
                    string[] found = AssetDatabase.FindAssets("t:FadenwerkDatabase");
                    if (found.Length > 0)
                    {
                        _cached = AssetDatabase.LoadAssetAtPath<FadenwerkDatabase>(AssetDatabase.GUIDToAssetPath(found[0]));
                        if (_cached != null) FadenwerkSettings.DatabaseGuid = found[0];
                    }
                }
                if (_cached != null) _cached.Sanitize();
                return _cached;
            }
        }

        public static void SetDatabase(FadenwerkDatabase db)
        {
            _cached = db;
            if (db == null) { FadenwerkSettings.DatabaseGuid = ""; return; }
            string path = AssetDatabase.GetAssetPath(db);
            FadenwerkSettings.DatabaseGuid = string.IsNullOrEmpty(path) ? "" : AssetDatabase.AssetPathToGUID(path);
        }

        public static FadenwerkDatabase CreateDatabase()
        {
            string dir = Path.GetDirectoryName(DefaultDatabasePath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets", "FadenwerkData");
            string path = AssetDatabase.GenerateUniqueAssetPath(DefaultDatabasePath);
            var db = ScriptableObject.CreateInstance<FadenwerkDatabase>();
            db.worldSeed = "fadenwerk-" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            AssetDatabase.CreateAsset(db, path);
            AssetDatabase.SaveAssets();
            SetDatabase(db);
            return db;
        }

        public static FadenwerkDatabase EnsureDatabase()
        {
            return Database != null ? Database : CreateDatabase();
        }

        // ---------- Szene ----------

        /// <summary>Alle geladenen Szenen mit gültigem Inhalt (keine Prefab-Ansicht).</summary>
        public static List<Scene> LoadedScenes()
        {
            var list = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s.isLoaded) list.Add(s);
            }
            return list;
        }

        public static List<T> FindInScenes<T>() where T : Component
        {
            var result = new List<T>();
            var roots = new List<GameObject>();
            foreach (Scene s in LoadedScenes())
            {
                roots.Clear();
                s.GetRootGameObjects(roots);
                for (int i = 0; i < roots.Count; i++)
                    result.AddRange(roots[i].GetComponentsInChildren<T>(true));
            }
            return result;
        }

        public static List<FadenwerkNpc> Npcs() { return FindInScenes<FadenwerkNpc>(); }
        public static List<FadenwerkMarker> Markers() { return FindInScenes<FadenwerkMarker>(); }

        public static FadenwerkNpc FindNpc(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (FadenwerkNpc n in Npcs()) if (n.npcId == id) return n;
            return null;
        }

        public static FadenwerkMarker FindMarker(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (FadenwerkMarker m in Markers()) if (m.markerId == id) return m;
            return null;
        }

        public static string HierarchyPath(Transform t)
        {
            var parts = new List<string>();
            while (t != null) { parts.Add(t.name); t = t.parent; }
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        public static string GlobalId(Object o)
        {
            return o == null ? "" : GlobalObjectId.GetGlobalObjectIdSlow(o).ToString();
        }

        public static GameObject FromGlobalId(string id)
        {
            GlobalObjectId gid;
            if (string.IsNullOrEmpty(id) || !GlobalObjectId.TryParse(id, out gid)) return null;
            Object o = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
            var go = o as GameObject;
            if (go != null) return go;
            var c = o as Component;
            return c != null ? c.gameObject : null;
        }

        // ---------- Kartenebene ----------

        /// <summary>Weltposition → Kartenkoordinaten (u nach rechts, v nach oben).</summary>
        public static Vector2 ToMap(Vector3 p)
        {
            return FadenwerkSettings.PlaneXY ? new Vector2(p.x, p.y) : new Vector2(p.x, p.z);
        }

        /// <summary>Setzt die Kartenkoordinaten einer Position; die Höhe bleibt, oder wird auf den Boden gesetzt.</summary>
        public static Vector3 FromMap(Vector3 current, float u, float v, Transform ignore)
        {
            bool grounded;
            return FromMap(current, u, v, ignore, out grounded);
        }

        /// <summary>grounded = true, wenn die Höhe auf Terrain oder einen Collider gesetzt wurde.</summary>
        public static Vector3 FromMap(Vector3 current, float u, float v, Transform ignore, out bool grounded)
        {
            grounded = false;
            if (FadenwerkSettings.PlaneXY) return new Vector3(u, v, current.z);
            var p = new Vector3(u, current.y, v);
            if (FadenwerkSettings.SnapToGround)
            {
                float y;
                if (GroundHeight(u, v, ignore, out y)) { p.y = y; grounded = true; }
            }
            return p;
        }

        static bool GroundHeight(float x, float z, Transform ignore, out float y)
        {
#if FADENWERK_TERRAIN
            Terrain[] terrains = Terrain.activeTerrains;
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain t = terrains[i];
                if (t == null || t.terrainData == null) continue;
                Vector3 tp = t.transform.position;
                Vector3 size = t.terrainData.size;
                if (x >= tp.x && x <= tp.x + size.x && z >= tp.z && z <= tp.z + size.z)
                {
                    y = t.SampleHeight(new Vector3(x, 0f, z)) + tp.y;
                    return true;
                }
            }
#endif
#if FADENWERK_PHYSICS
            Physics.SyncTransforms();
            RaycastHit[] hits = Physics.RaycastAll(new Vector3(x, 10000f, z), Vector3.down, 20000f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                if (ignore != null && hits[i].transform.IsChildOf(ignore)) continue;
                if (hits[i].point.y > best) { best = hits[i].point.y; found = true; }
            }
            y = found ? best : 0f;
            return found;
#else
            y = 0f;
            return false;
#endif
        }

        // ---------- Speichern ----------

        /// <summary>Markiert ein geändertes Szenenobjekt und speichert die Szene, wenn "automatisch speichern" an ist.</summary>
        public static string CommitSceneChange(Component c)
        {
            if (c == null) return null;
            EditorUtility.SetDirty(c);
            if (PrefabUtility.IsPartOfPrefabInstance(c)) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
            Scene s = c.gameObject.scene;
            if (s.IsValid()) EditorSceneManager.MarkSceneDirty(s);
            return FadenwerkSettings.AutoSave ? SaveScene(s) : null;
        }

        public static string CommitTransform(Transform t)
        {
            if (t == null) return null;
            if (PrefabUtility.IsPartOfPrefabInstance(t)) PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            Scene s = t.gameObject.scene;
            if (s.IsValid()) EditorSceneManager.MarkSceneDirty(s);
            return FadenwerkSettings.AutoSave ? SaveScene(s) : null;
        }

        /// <summary>Speichert eine Szene. Gibt eine Warnung zurück, wenn das nicht ohne Dialog geht.</summary>
        public static string SaveScene(Scene s)
        {
            if (!s.IsValid() || !s.isDirty) return null;
            if (string.IsNullOrEmpty(s.path))
                return "Die Szene \"" + s.name + "\" wurde noch nie gespeichert – bitte einmal in Unity speichern (Strg+S), danach geht es automatisch.";
            return EditorSceneManager.SaveScene(s) ? null : "Szene \"" + s.name + "\" konnte nicht gespeichert werden.";
        }

        public static string SaveAllScenes()
        {
            string warn = null;
            foreach (Scene s in LoadedScenes())
            {
                string w = SaveScene(s);
                if (w != null) warn = w;
            }
            return warn;
        }

        public static void CommitDatabase(FadenwerkDatabase db)
        {
            if (db == null) return;
            EditorUtility.SetDirty(db);
            if (FadenwerkSettings.AutoSave) AssetDatabase.SaveAssetIfDirty(db);
        }
    }
}
