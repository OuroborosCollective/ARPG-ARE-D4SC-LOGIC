using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Fadenwerk.EditorTools
{
    [Serializable]
    public class ItemDto
    {
        public string id = "";
        public string displayName = "";
        public string category = "misc";
        public string rarity = "common";
        public string description = "";
        public string assetGuid = "";
    }

    /// <summary>Ein Anfrage-Körper für alle Befehle; jeder Befehl liest nur die Felder, die er braucht.</summary>
    [Serializable]
    public class FwReq
    {
        public string expectedRevision = "";
        public string id = "";
        public string kind = "";
        public float u;
        public float v;
        public string name = "";
        public string prefabGuid = "";
        public string goid = "";
        public string path = "";
        public string seed = "";
        public string folder = "";
        public string category = "";
        public string rarity = "";
        public string mode = "";
        public float minU, minV, maxU, maxV;
        public int size;
        public string worldSeed = "";

        public string npcId = "";
        public string displayName = "";
        public string role = "";
        public string loreText = "";
        public string goalText = "";
        public string goalMarkerId = "";
        public string dialogueId = "";
        public float interactRadius;

        public string markerId = "";
        public string label = "";
        public float radius;
        public bool autoReport;

        public QuestDef quest = new QuestDef();
        public DialogueDef dialogue = new DialogueDef();
        public RewardKitDef kit = new RewardKitDef();
        public ItemDto item = new ItemDto();
    }

    /// <summary>Alle /api-Befehle. Läuft ausschließlich im Unity-Hauptthread.</summary>
    internal static class FadenwerkApi
    {
        [Serializable] class RevisionOnly { public string rev; }
        static readonly Regex IdRe = new Regex("^[A-Za-z0-9_.:-]{1,80}$");
        static readonly Dictionary<string, byte[]> PreviewCache = new Dictionary<string, byte[]>();

        public static FwResponse Handle(FwRequest r)
        {
            string p = r.path;
            if (r.method == "GET")
            {
                switch (p)
                {
                    case "/api/state": return State(r);
                    case "/api/map/image": return MapImage();
                    case "/api/assets": return Assets(r);
                    case "/api/assets/preview": return Preview(r);
                    case "/api/folders": return Folders();
                    case "/api/candidates": return Candidates();
                }
                return FwResponse.Error("Unbekannter Befehl: " + p, 404);
            }

            FwReq q;
            try { q = string.IsNullOrEmpty(r.body) ? new FwReq() : JsonUtility.FromJson<FwReq>(r.body); }
            catch (Exception e) { return FwResponse.Error("Ungültige Daten: " + e.Message); }
            if (q == null) q = new FwReq();

            // Nur lesend, auch im Play-Modus erlaubt
            if (p == "/api/kit/roll") return KitRoll(q);
            if (p == "/api/entity/select") return Select(q);

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return FwResponse.Error("Unity ist im Play-Modus – Änderungen sind gesperrt, bis du ihn beendest.", 409);
            if (EditorApplication.isCompiling)
                return FwResponse.Error("Unity kompiliert gerade – gleich noch einmal versuchen.", 503);

            var snapshot = State(new FwRequest { method = "GET", path = "/api/state" });
            string revision = JsonUtility.FromJson<RevisionOnly>(System.Text.Encoding.UTF8.GetString(snapshot.body)).rev;
            if (string.IsNullOrEmpty(q.expectedRevision) || q.expectedRevision != revision)
                return FwResponse.Error("Der Unity-Stand hat sich geändert. Bitte aktualisieren und deine Eingaben vergleichen.", 409);
            if (p.StartsWith("/api/game/", StringComparison.Ordinal))
            {
                if (FadenwerkAuthoringBridge.Command == null) return FwResponse.Error("Keine Spielanbindung installiert.", 404);
                try { return FwResponse.Json(FadenwerkAuthoringBridge.Command(p.Substring("/api/game/".Length))); }
                catch (InvalidOperationException e) { return FwResponse.Error(e.Message, 422); }
            }

            switch (p)
            {
                case "/api/map/render": return MapRender(q);
                case "/api/npc/save": return NpcSave(q);
                case "/api/npc/create": return NpcCreate(q);
                case "/api/npc/convert": return NpcConvert(q);
                case "/api/marker/save": return MarkerSave(q);
                case "/api/marker/create": return MarkerCreate(q);
                case "/api/entity/move": return Move(q);
                case "/api/entity/delete": return EntityDelete(q);
                case "/api/quest/save": return QuestSave(q);
                case "/api/quest/delete": return Delete(q, "quest");
                case "/api/dialogue/save": return DialogueSave(q);
                case "/api/dialogue/delete": return Delete(q, "dialogue");
                case "/api/kit/save": return KitSave(q);
                case "/api/kit/delete": return Delete(q, "kit");
                case "/api/item/save": return ItemSave(q);
                case "/api/item/delete": return Delete(q, "item");
                case "/api/items/import": return ItemsImport(q);
                case "/api/seed/save": return SeedSave(q);
                case "/api/scene/save": return SceneSave();
                case "/api/scene/open": return SceneOpen(q);
                case "/api/db/create": FadenwerkEditorData.EnsureDatabase(); return Ok(null);
            }
            return FwResponse.Error("Unbekannter Befehl: " + p, 404);
        }

        static FwResponse Ok(string warning, string id = null)
        {
            var j = new FwJson().BeginObject().Prop("ok", true);
            if (!string.IsNullOrEmpty(warning)) j.Prop("warning", warning);
            if (!string.IsNullOrEmpty(id)) j.Prop("id", id);
            return FwResponse.Json(j.EndObject().ToString());
        }

        static FadenwerkDatabase Db(out FwResponse error)
        {
            FadenwerkDatabase db = FadenwerkEditorData.Database;
            error = db == null ? FwResponse.Error("Keine Fadenwerk-Datenbank im Projekt – im Unity-Fenster oder unter Einstellungen anlegen.", 409) : null;
            return db;
        }

        static string Clean(string s) { return s ?? ""; }

        // ================= Zustand =================

        static FwResponse State(FwRequest r)
        {
            FadenwerkDatabase db = FadenwerkEditorData.Database;
            List<FadenwerkNpc> npcs = FadenwerkEditorData.Npcs();
            List<FadenwerkMarker> markers = FadenwerkEditorData.Markers();
            var j = new FwJson();
            j.BeginObject();
            j.Prop("project", Path.GetFileName(Path.GetDirectoryName(Application.dataPath)));
            j.Prop("unity", Application.unityVersion);
            j.Prop("playing", EditorApplication.isPlayingOrWillChangePlaymode);
            j.Prop("plane", FadenwerkSettings.PlaneXY ? "XY" : "XZ");
            j.Prop("autoSave", FadenwerkSettings.AutoSave);
            if (FadenwerkAuthoringBridge.ReadState != null) j.Key("game").Raw(FadenwerkAuthoringBridge.ReadState());

            if (db != null)
                j.Key("db").BeginObject().Prop("path", AssetDatabase.GetAssetPath(db)).Prop("worldSeed", db.worldSeed).EndObject();
            else j.Key("db").Null();

            j.Key("scenes").BeginArray();
            Scene active = SceneManager.GetActiveScene();
            foreach (Scene s in FadenwerkEditorData.LoadedScenes())
                j.BeginObject().Prop("name", s.name).Prop("path", s.path).Prop("dirty", s.isDirty).Prop("active", s == active).EndObject();
            j.EndArray();

            j.Key("projectScenes").BeginArray();
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            for (int i = 0; i < sceneGuids.Length && i < 300; i++) j.Str(AssetDatabase.GUIDToAssetPath(sceneGuids[i]));
            j.EndArray();

            j.Key("npcs").BeginArray();
            foreach (FadenwerkNpc n in npcs)
            {
                Vector2 m = FadenwerkEditorData.ToMap(n.transform.position);
                j.BeginObject()
                    .Prop("id", n.npcId).Prop("name", n.Label).Prop("objectName", n.gameObject.name)
                    .Prop("role", n.role).Prop("lore", n.loreText).Prop("goal", n.goalText)
                    .Prop("goalMarkerId", n.goalMarkerId).Prop("dialogueId", n.dialogueId)
                    .Prop("radius", n.interactRadius).Prop("u", m.x).Prop("v", m.y)
                    .Prop("scene", n.gameObject.scene.name)
                    .Prop("path", FadenwerkEditorData.HierarchyPath(n.transform))
                    .Prop("active", n.gameObject.activeInHierarchy)
                    .EndObject();
            }
            j.EndArray();

            j.Key("markers").BeginArray();
            foreach (FadenwerkMarker mk in markers)
            {
                Vector2 m = FadenwerkEditorData.ToMap(mk.transform.position);
                j.BeginObject()
                    .Prop("id", mk.markerId).Prop("label", mk.Label).Prop("kind", mk.kind)
                    .Prop("radius", mk.radius).Prop("autoReport", mk.autoReport)
                    .Prop("u", m.x).Prop("v", m.y).Prop("scene", mk.gameObject.scene.name)
                    .Prop("path", FadenwerkEditorData.HierarchyPath(mk.transform))
                    .EndObject();
            }
            j.EndArray();

            j.Key("quests").BeginArray();
            if (db != null) foreach (QuestDef qd in db.quests) j.Raw(JsonUtility.ToJson(qd));
            j.EndArray();
            j.Key("dialogues").BeginArray();
            if (db != null) foreach (DialogueDef d in db.dialogues) j.Raw(JsonUtility.ToJson(d));
            j.EndArray();
            j.Key("kits").BeginArray();
            if (db != null) foreach (RewardKitDef k in db.kits) j.Raw(JsonUtility.ToJson(k));
            j.EndArray();

            j.Key("items").BeginArray();
            if (db != null)
            {
                foreach (ItemEntry it in db.items)
                {
                    string ap = it.asset != null ? AssetDatabase.GetAssetPath(it.asset) : "";
                    j.BeginObject()
                        .Prop("id", it.id).Prop("displayName", it.displayName).Prop("category", it.category)
                        .Prop("rarity", it.rarity).Prop("description", it.description)
                        .Prop("assetGuid", string.IsNullOrEmpty(ap) ? "" : AssetDatabase.AssetPathToGUID(ap))
                        .Prop("assetPath", ap)
                        .Prop("assetName", it.asset != null ? it.asset.name : "")
                        .Prop("assetType", it.asset != null ? it.asset.GetType().Name : "")
                        .EndObject();
                }
            }
            j.EndArray();

            j.Key("issues").BeginArray();
            foreach (FwIssue iss in FadenwerkValidator.Run(db, npcs, markers))
                j.BeginObject().Prop("level", iss.level).Prop("message", iss.message).Prop("kind", iss.kind).Prop("id", iss.id).EndObject();
            j.EndArray();

            byte[] img; float minU, minV, maxU, maxV; int mapRev;
            j.Key("map").BeginObject();
            if (FadenwerkMap.TryGet(out img, out minU, out minV, out maxU, out maxV, out mapRev))
                j.Prop("hasImage", true).Prop("rev", mapRev).Prop("minU", minU).Prop("minV", minV).Prop("maxU", maxU).Prop("maxV", maxV);
            else
            {
                Rect b = EntityBounds(npcs, markers);
                j.Prop("hasImage", false).Prop("rev", 0).Prop("minU", b.xMin).Prop("minV", b.yMin).Prop("maxU", b.xMax).Prop("maxV", b.yMax);
            }
            j.EndObject();
            j.EndObject();

            string payload = j.ToString();
            string rev = FwUtil.Hash64(payload);
            string since;
            if (r.query.TryGetValue("rev", out since) && since == rev)
                return FwResponse.Json("{\"ok\":true,\"same\":true,\"rev\":\"" + rev + "\"}");
            return FwResponse.Json("{\"ok\":true,\"rev\":\"" + rev + "\"," + payload.Substring(1));
        }

        static Rect EntityBounds(List<FadenwerkNpc> npcs, List<FadenwerkMarker> markers)
        {
            bool any = false;
            float a = 0, b = 0, c = 0, d = 0;
            var pts = new List<Vector2>();
            foreach (FadenwerkNpc n in npcs) pts.Add(FadenwerkEditorData.ToMap(n.transform.position));
            foreach (FadenwerkMarker m in markers) pts.Add(FadenwerkEditorData.ToMap(m.transform.position));
            foreach (Vector2 pt in pts)
            {
                if (!any) { a = c = pt.x; b = d = pt.y; any = true; continue; }
                a = Mathf.Min(a, pt.x); c = Mathf.Max(c, pt.x); b = Mathf.Min(b, pt.y); d = Mathf.Max(d, pt.y);
            }
            if (!any) return Rect.MinMaxRect(-25, -25, 25, 25);
            float pad = Mathf.Max(10f, Mathf.Max(c - a, d - b) * 0.15f);
            return Rect.MinMaxRect(a - pad, b - pad, c + pad, d + pad);
        }

        // ================= Karte =================

        static FwResponse MapImage()
        {
            byte[] img; float a, b, c, d; int rev;
            if (!FadenwerkMap.TryGet(out img, out a, out b, out c, out d, out rev)) return FwResponse.Error("Noch keine Karte gerendert.", 404);
            return FwResponse.Bytes(img, "image/jpeg");
        }

        static FwResponse MapRender(FwReq q)
        {
            Rect area = q.mode == "area" && q.maxU > q.minU && q.maxV > q.minV
                ? Rect.MinMaxRect(q.minU, q.minV, q.maxU, q.maxV)
                : FadenwerkMap.AutoBounds();
            string err = FadenwerkMap.Render(area, q.size > 0 ? q.size : 1536);
            return err != null ? FwResponse.Error(err, 500) : Ok(null);
        }

        // ================= NPCs und Marker =================

        static FwResponse NpcSave(FwReq q)
        {
            FadenwerkNpc n = FadenwerkEditorData.FindNpc(q.npcId);
            if (n == null) return FwResponse.Error("NPC \"" + q.npcId + "\" ist in keiner geladenen Szene.", 404);
            Undo.RecordObject(n, "Fadenwerk: NPC bearbeiten");
            n.displayName = Clean(q.displayName);
            n.role = Clean(q.role);
            n.loreText = Clean(q.loreText);
            n.goalText = Clean(q.goalText);
            n.goalMarkerId = Clean(q.goalMarkerId);
            n.dialogueId = Clean(q.dialogueId);
            if (q.interactRadius > 0.05f) n.interactRadius = q.interactRadius;
            return Ok(FadenwerkEditorData.CommitSceneChange(n));
        }

        static FwResponse NpcCreate(FwReq q)
        {
            string name = string.IsNullOrEmpty(q.name) ? "Neuer NPC" : q.name.Trim();
            GameObject go = null;
            bool placeholder = false;
            if (!string.IsNullOrEmpty(q.prefabGuid))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(q.prefabGuid));
                if (prefab == null) return FwResponse.Error("Prefab nicht gefunden.", 404);
                go = PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene()) as GameObject;
                if (go == null) return FwResponse.Error("Prefab konnte nicht platziert werden.", 500);
                go.name = name;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = name;
                placeholder = true;
            }
            Undo.RegisterCreatedObjectUndo(go, "Fadenwerk: NPC anlegen");
            bool grounded;
            Vector3 pos = FadenwerkEditorData.FromMap(go.transform.position, q.u, q.v, go.transform, out grounded);
            if (placeholder && !FadenwerkSettings.PlaneXY) pos.y = grounded ? pos.y + 1f : 1f; // Kapsel steht mit dem Fuß auf dem Boden
            go.transform.position = pos;
            FadenwerkNpc npc = go.GetComponent<FadenwerkNpc>();
            if (npc == null) npc = Undo.AddComponent<FadenwerkNpc>(go);
            npc.displayName = name;
            npc.EnsureId();
            return Ok(FadenwerkEditorData.CommitSceneChange(npc), npc.npcId);
        }

        static FwResponse NpcConvert(FwReq q)
        {
            GameObject go = FadenwerkEditorData.FromGlobalId(q.goid);
            if (go == null || !go.scene.IsValid()) return FwResponse.Error("Objekt nicht gefunden – wurde die Szene gewechselt?", 404);
            FadenwerkNpc npc = go.GetComponent<FadenwerkNpc>();
            if (npc == null) npc = Undo.AddComponent<FadenwerkNpc>(go);
            Undo.RecordObject(npc, "Fadenwerk: NPC anlegen");
            if (string.IsNullOrEmpty(npc.displayName)) npc.displayName = go.name;
            npc.EnsureId();
            return Ok(FadenwerkEditorData.CommitSceneChange(npc), npc.npcId);
        }

        static FwResponse MarkerSave(FwReq q)
        {
            FadenwerkMarker m = FadenwerkEditorData.FindMarker(q.markerId);
            if (m == null) return FwResponse.Error("Marker \"" + q.markerId + "\" ist in keiner geladenen Szene.", 404);
            Undo.RecordObject(m, "Fadenwerk: Marker bearbeiten");
            m.label = Clean(q.label);
            if (!string.IsNullOrEmpty(q.kind)) m.kind = q.kind;
            if (q.radius > 0.05f) m.radius = q.radius;
            m.autoReport = q.autoReport;
            return Ok(FadenwerkEditorData.CommitSceneChange(m));
        }

        static FwResponse MarkerCreate(FwReq q)
        {
            string label = string.IsNullOrEmpty(q.label) ? "Neuer Marker" : q.label.Trim();
            var go = new GameObject("Marker " + label);
            Undo.RegisterCreatedObjectUndo(go, "Fadenwerk: Marker anlegen");
            go.transform.position = FadenwerkEditorData.FromMap(Vector3.zero, q.u, q.v, go.transform);
            FadenwerkMarker m = Undo.AddComponent<FadenwerkMarker>(go);
            m.label = label;
            if (!string.IsNullOrEmpty(q.kind)) m.kind = q.kind;
            m.EnsureId();
            return Ok(FadenwerkEditorData.CommitSceneChange(m), m.markerId);
        }

        static Component FindEntity(string kind, string id)
        {
            if (kind == "npc") return FadenwerkEditorData.FindNpc(id);
            if (kind == "marker") return FadenwerkEditorData.FindMarker(id);
            return null;
        }

        static FwResponse Move(FwReq q)
        {
            Component c = FindEntity(q.kind, q.id);
            if (c == null) return FwResponse.Error("Objekt nicht gefunden.", 404);
            Transform t = c.transform;
            Undo.RecordObject(t, "Fadenwerk: verschieben");
            // Leere Hilfsobjekte über dem Boden lassen, Figuren auf den Boden stellen
            bool grounded;
            Vector3 target = FadenwerkEditorData.FromMap(t.position, q.u, q.v, t, out grounded);
            if (grounded && c is FadenwerkNpc && !FadenwerkSettings.PlaneXY && IsPlaceholderCapsule(c.gameObject)) target.y += 1f;
            t.position = target;
            return Ok(FadenwerkEditorData.CommitTransform(t));
        }

        static bool IsPlaceholderCapsule(GameObject go)
        {
            var mf = go.GetComponent<MeshFilter>();
            return mf != null && mf.sharedMesh != null && mf.sharedMesh.name == "Capsule" && go.transform.childCount == 0;
        }

        static FwResponse Select(FwReq q)
        {
            Component c = FindEntity(q.kind, q.id);
            if (c == null) return FwResponse.Error("Objekt nicht gefunden.", 404);
            Selection.activeGameObject = c.gameObject;
            EditorGUIUtility.PingObject(c.gameObject);
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
            return Ok(null);
        }

        static FwResponse EntityDelete(FwReq q)
        {
            Component c = FindEntity(q.kind, q.id);
            if (c == null) return FwResponse.Error("Objekt nicht gefunden.", 404);
            Scene s = c.gameObject.scene;
            GameObject go = c.gameObject;
            bool onlyMarker = c is FadenwerkMarker && go.transform.childCount == 0 && go.GetComponents<Component>().Length == 2;
            if (onlyMarker) Undo.DestroyObjectImmediate(go);
            else Undo.DestroyObjectImmediate(c); // Figur bleibt in der Szene, nur die Fadenwerk-Komponente geht
            if (s.IsValid()) EditorSceneManager.MarkSceneDirty(s);
            return Ok(FadenwerkSettings.AutoSave ? FadenwerkEditorData.SaveScene(s) : null);
        }

        static FwResponse Candidates()
        {
            var j = new FwJson().BeginObject().Prop("ok", true);
            j.Key("selected").BeginArray();
            foreach (GameObject go in Selection.gameObjects)
            {
                if (!go.scene.IsValid() || go.GetComponent<FadenwerkNpc>() != null) continue;
                Candidate(j, go);
            }
            j.EndArray();
            j.Key("objects").BeginArray();
            int count = 0;
            var seen = new HashSet<GameObject>();
#if FADENWERK_ANIMATION
            foreach (Animator a in FadenwerkEditorData.FindInScenes<Animator>()) count += TryCandidate(j, a.gameObject, seen, count);
#endif
            foreach (SkinnedMeshRenderer smr in FadenwerkEditorData.FindInScenes<SkinnedMeshRenderer>())
            {
                GameObject figure = smr.gameObject;
#if FADENWERK_ANIMATION
                // Figur = Objekt mit Animator darüber, sonst das Objekt selbst
                Animator owner = smr.GetComponentInParent<Animator>();
                if (owner != null) figure = owner.gameObject;
#endif
                count += TryCandidate(j, figure, seen, count);
            }
#if FADENWERK_PHYSICS
            foreach (CharacterController cc in FadenwerkEditorData.FindInScenes<CharacterController>()) count += TryCandidate(j, cc.gameObject, seen, count);
#endif
            j.EndArray();
            return FwResponse.Json(j.EndObject().ToString());
        }

        static int TryCandidate(FwJson j, GameObject go, HashSet<GameObject> seen, int count)
        {
            if (count >= 300 || !seen.Add(go) || go.GetComponentInParent<FadenwerkNpc>() != null) return 0;
            if (go.CompareTag("Player")) return 0;
            Candidate(j, go);
            return 1;
        }

        static void Candidate(FwJson j, GameObject go)
        {
            Vector2 m = FadenwerkEditorData.ToMap(go.transform.position);
            j.BeginObject().Prop("goid", FadenwerkEditorData.GlobalId(go)).Prop("name", go.name)
                .Prop("path", FadenwerkEditorData.HierarchyPath(go.transform)).Prop("scene", go.scene.name)
                .Prop("u", m.x).Prop("v", m.y).EndObject();
        }

        // ================= Datenbank =================

        static int IndexOf<T>(List<T> list, Func<T, string> getId, string id)
        {
            for (int i = 0; i < list.Count; i++) if (getId(list[i]) == id) return i;
            return -1;
        }

        static FwResponse QuestSave(FwReq q)
        {
            FwResponse err; FadenwerkDatabase db = Db(out err); if (db == null) return err;
            QuestDef quest = q.quest;
            if (quest == null || !IdRe.IsMatch(quest.id ?? "")) return FwResponse.Error("Ungültige Quest-ID.");
            if (quest.objectives == null) quest.objectives = new List<ObjectiveDef>();
            if (quest.prerequisiteQuestIds == null) quest.prerequisiteQuestIds = new List<string>();
            quest.prerequisiteQuestIds.RemoveAll(string.IsNullOrEmpty);
            foreach (ObjectiveDef o in quest.objectives) if (o.count < 1) o.count = 1;
            Undo.RecordObject(db, "Fadenwerk: Quest speichern");
            int i = IndexOf(db.quests, x => x.id, quest.id);
            if (i >= 0) db.quests[i] = quest; else db.quests.Add(quest);
            FadenwerkEditorData.CommitDatabase(db);
            return Ok(null, quest.id);
        }

        static FwResponse DialogueSave(FwReq q)
        {
            FwResponse err; FadenwerkDatabase db = Db(out err); if (db == null) return err;
            DialogueDef d = q.dialogue;
            if (d == null || !IdRe.IsMatch(d.id ?? "")) return FwResponse.Error("Ungültige Dialog-ID.");
            if (d.nodes == null) d.nodes = new List<DialogueNode>();
            foreach (DialogueNode n in d.nodes) if (n.choices == null) n.choices = new List<DialogueChoice>();
            Undo.RecordObject(db, "Fadenwerk: Dialog speichern");
            int i = IndexOf(db.dialogues, x => x.id, d.id);
            if (i >= 0) db.dialogues[i] = d; else db.dialogues.Add(d);
            FadenwerkEditorData.CommitDatabase(db);
            return Ok(null, d.id);
        }

        static FwResponse KitSave(FwReq q)
        {
            FwResponse err; FadenwerkDatabase db = Db(out err); if (db == null) return err;
            RewardKitDef k = q.kit;
            if (k == null || !IdRe.IsMatch(k.id ?? "")) return FwResponse.Error("Ungültige Kit-ID.");
            if (k.entries == null) k.entries = new List<KitEntry>();
            k.goldMin = Mathf.Max(0, k.goldMin); k.goldMax = Mathf.Max(k.goldMin, k.goldMax);
            k.xpMin = Mathf.Max(0, k.xpMin); k.xpMax = Mathf.Max(k.xpMin, k.xpMax);
            k.rolls = Mathf.Clamp(k.rolls, 0, 64);
            foreach (KitEntry e in k.entries)
            {
                e.minQuantity = Mathf.Max(0, e.minQuantity);
                e.maxQuantity = Mathf.Max(e.minQuantity, e.maxQuantity);
                e.weight = Mathf.Max(0, e.weight);
            }
            Undo.RecordObject(db, "Fadenwerk: Kit speichern");
            int i = IndexOf(db.kits, x => x.id, k.id);
            if (i >= 0) db.kits[i] = k; else db.kits.Add(k);
            FadenwerkEditorData.CommitDatabase(db);
            return Ok(null, k.id);
        }

        static FwResponse ItemSave(FwReq q)
        {
            FwResponse err; FadenwerkDatabase db = Db(out err); if (db == null) return err;
            ItemDto dto = q.item;
            if (dto == null || !IdRe.IsMatch(dto.id ?? "")) return FwResponse.Error("Ungültige Item-ID.");
            Object asset = null;
            if (!string.IsNullOrEmpty(dto.assetGuid))
            {
                string ap = AssetDatabase.GUIDToAssetPath(dto.assetGuid);
                asset = string.IsNullOrEmpty(ap) ? null : AssetDatabase.LoadMainAssetAtPath(ap);
                if (asset == null) return FwResponse.Error("Das gewählte Asset gibt es nicht mehr.", 404);
            }
            Undo.RecordObject(db, "Fadenwerk: Item speichern");
            int i = IndexOf(db.items, x => x.id, dto.id);
            ItemEntry it = i >= 0 ? db.items[i] : new ItemEntry();
            it.id = dto.id;
            it.displayName = Clean(dto.displayName);
            it.category = string.IsNullOrEmpty(dto.category) ? "misc" : dto.category;
            it.rarity = string.IsNullOrEmpty(dto.rarity) ? "common" : dto.rarity;
            it.description = Clean(dto.description);
            it.asset = asset;
            if (i < 0) db.items.Add(it);
            FadenwerkEditorData.CommitDatabase(db);
            return Ok(null, it.id);
        }

        static FwResponse Delete(FwReq q, string kind)
        {
            FwResponse err; FadenwerkDatabase db = Db(out err); if (db == null) return err;
            Undo.RecordObject(db, "Fadenwerk: löschen");
            int removed = 0;
            if (kind == "quest") removed = db.quests.RemoveAll(x => x.id == q.id);
            else if (kind == "dialogue") removed = db.dialogues.RemoveAll(x => x.id == q.id);
            else if (kind == "kit") removed = db.kits.RemoveAll(x => x.id == q.id);
            else if (kind == "item") removed = db.items.RemoveAll(x => x.id == q.id);
            if (removed == 0) return FwResponse.Error("Nicht gefunden: " + q.id, 404);
            FadenwerkEditorData.CommitDatabase(db);
            return Ok(null);
        }

        static FwResponse SeedSave(FwReq q)
        {
            FwResponse err; FadenwerkDatabase db = Db(out err); if (db == null) return err;
            Undo.RecordObject(db, "Fadenwerk: Welt-Seed");
            db.worldSeed = Clean(q.worldSeed);
            FadenwerkEditorData.CommitDatabase(db);
            return Ok(null);
        }

        static FwResponse KitRoll(FwReq q)
        {
            FwResponse err; FadenwerkDatabase db = Db(out err); if (db == null) return err;
            if (db.FindKit(q.id) == null) return FwResponse.Error("Kit nicht gefunden.", 404);
            RewardResult res = FadenwerkRewards.Roll(db, q.id, q.seed);
            var j = new FwJson().BeginObject().Prop("ok", true).Prop("gold", res.gold).Prop("xp", res.xp).Prop("seed", res.seed);
            j.Key("items").BeginArray();
            foreach (RewardItem ri in res.items)
                j.BeginObject().Prop("itemId", ri.itemId).Prop("displayName", ri.displayName).Prop("quantity", ri.quantity).EndObject();
            j.EndArray();
            return FwResponse.Json(j.EndObject().ToString());
        }

        static FwResponse ItemsImport(FwReq q)
        {
            FwResponse err; FadenwerkDatabase db = Db(out err); if (db == null) return err;
            string folder = (q.folder ?? "").TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(folder)) return FwResponse.Error("Ordner nicht gefunden: " + folder, 404);
            var linked = new HashSet<string>();
            foreach (ItemEntry it in db.items)
                if (it.asset != null) linked.Add(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(it.asset)));
            var ids = new HashSet<string>();
            foreach (ItemEntry it in db.items) ids.Add(it.id);

            var guids = new List<string>(AssetDatabase.FindAssets("t:Prefab", new[] { folder }));
            guids.AddRange(AssetDatabase.FindAssets("t:ScriptableObject", new[] { folder }));
            Undo.RecordObject(db, "Fadenwerk: Items importieren");
            int added = 0;
            var done = new HashSet<string>();
            foreach (string g in guids)
            {
                if (!done.Add(g) || linked.Contains(g)) continue;
                string ap = AssetDatabase.GUIDToAssetPath(g);
                Object asset = AssetDatabase.LoadMainAssetAtPath(ap);
                if (asset == null || asset is FadenwerkDatabase) continue;
                string baseId = FwUtil.Slug(asset.name);
                if (string.IsNullOrEmpty(baseId)) baseId = "item";
                string id = baseId;
                for (int n = 2; ids.Contains(id); n++) id = baseId + "_" + n;
                ids.Add(id);
                db.items.Add(new ItemEntry
                {
                    id = id,
                    displayName = asset.name,
                    category = string.IsNullOrEmpty(q.category) ? "misc" : q.category,
                    rarity = string.IsNullOrEmpty(q.rarity) ? "common" : q.rarity,
                    asset = asset
                });
                added++;
                if (added >= 1000) break;
            }
            if (added > 0) FadenwerkEditorData.CommitDatabase(db);
            return FwResponse.Json(new FwJson().BeginObject().Prop("ok", true).Prop("added", added).EndObject().ToString());
        }

        // ================= Szenen =================

        static FwResponse SceneSave()
        {
            string warn = FadenwerkEditorData.SaveAllScenes();
            FadenwerkDatabase db = FadenwerkEditorData.Database;
            if (db != null) { EditorUtility.SetDirty(db); AssetDatabase.SaveAssetIfDirty(db); }
            return warn != null ? FwResponse.Error(warn, 409) : Ok(null);
        }

        static FwResponse SceneOpen(FwReq q)
        {
            string path = q.path ?? "";
            if (!path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) || AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                return FwResponse.Error("Szene nicht gefunden: " + path, 404);
            foreach (Scene s in FadenwerkEditorData.LoadedScenes())
            {
                if (!s.isDirty) continue;
                if (!FadenwerkSettings.AutoSave)
                    return FwResponse.Error("Die Szene \"" + s.name + "\" hat ungespeicherte Änderungen – erst speichern.", 409);
                string w = FadenwerkEditorData.SaveScene(s);
                if (w != null) return FwResponse.Error(w, 409);
            }
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            return Ok(null);
        }

        // ================= Assets =================

        static FwResponse Assets(FwRequest r)
        {
            string text, type;
            r.query.TryGetValue("q", out text);
            r.query.TryGetValue("type", out type);
            text = (text ?? "").Replace("t:", "").Trim();
            string filter = text;
            if (type == "prefab") filter += " t:Prefab";
            else if (type == "so") filter += " t:ScriptableObject";
            else if (type == "model") filter += " t:Model";
            else if (string.IsNullOrEmpty(text)) filter = "t:Prefab";
            string[] guids = AssetDatabase.FindAssets(filter.Trim(), new[] { "Assets" });
            var j = new FwJson().BeginObject().Prop("ok", true).Prop("total", guids.Length);
            j.Key("assets").BeginArray();
            int n = 0;
            foreach (string g in guids)
            {
                string ap = AssetDatabase.GUIDToAssetPath(g);
                if (AssetDatabase.IsValidFolder(ap)) continue;
                Type t = AssetDatabase.GetMainAssetTypeAtPath(ap);
                if (t == typeof(FadenwerkDatabase)) continue;
                j.BeginObject().Prop("guid", g).Prop("path", ap).Prop("name", Path.GetFileNameWithoutExtension(ap))
                    .Prop("type", t != null ? t.Name : "").EndObject();
                if (++n >= 120) break;
            }
            j.EndArray();
            return FwResponse.Json(j.EndObject().ToString());
        }

        static FwResponse Preview(FwRequest r)
        {
            string guid;
            if (!r.query.TryGetValue("guid", out guid) || string.IsNullOrEmpty(guid)) return FwResponse.Error("guid fehlt");
            byte[] cached;
            if (PreviewCache.TryGetValue(guid, out cached)) return FwResponse.Bytes(cached, "image/png");
            string ap = AssetDatabase.GUIDToAssetPath(guid);
            Object asset = string.IsNullOrEmpty(ap) ? null : AssetDatabase.LoadMainAssetAtPath(ap);
            if (asset == null) return FwResponse.Error("Asset nicht gefunden", 404);
            Texture2D tex = AssetPreview.GetAssetPreview(asset);
            if (tex == null)
            {
                if (AssetPreview.IsLoadingAssetPreviews()) return FwResponse.Error("Vorschau wird erzeugt", 202);
                tex = AssetPreview.GetMiniThumbnail(asset);
            }
            if (tex == null) return FwResponse.Error("Keine Vorschau", 404);
            byte[] png = ToPng(tex);
            if (PreviewCache.Count > 400) PreviewCache.Clear();
            PreviewCache[guid] = png;
            return FwResponse.Bytes(png, "image/png");
        }

        static byte[] ToPng(Texture tex)
        {
            int w = Mathf.Clamp(tex.width, 1, 256), h = Mathf.Clamp(tex.height, 1, 256);
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            RenderTexture prev = RenderTexture.active;
            Texture2D copy = null;
            try
            {
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                copy.Apply(false);
                return copy.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                if (copy != null) Object.DestroyImmediate(copy);
            }
        }

        static FwResponse Folders()
        {
            var j = new FwJson().BeginObject().Prop("ok", true);
            j.Key("folders").BeginArray();
            var stack = new Stack<string>();
            stack.Push("Assets");
            int n = 0;
            var list = new List<string>();
            while (stack.Count > 0 && n < 600)
            {
                string f = stack.Pop();
                list.Add(f);
                n++;
                string[] subs = AssetDatabase.GetSubFolders(f);
                for (int i = subs.Length - 1; i >= 0; i--) stack.Push(subs[i]);
            }
            list.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string f in list) j.Str(f);
            j.EndArray();
            return FwResponse.Json(j.EndObject().ToString());
        }
    }
}
