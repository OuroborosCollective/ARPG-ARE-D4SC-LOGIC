using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AreArpg.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AreArpg.EditorTools
{
    /// <summary>
    /// Owner decision 2026-11-25: the pack "Der verschleierte Brennerhof" becomes Aschenwacht's side yard. The pack lies
    /// privately in the editor under Assets/Aschenwacht_Brennerhof and is never part of the repository. This builds the
    /// game's passive pictures from the pack's own scene, in the yard's frame (KilnLayout), every piece where the pack puts it:
    /// the yard with its horizon and ground fog (EntryKilnWorld), the living parts the runtime moves (the eight vases and
    /// their sixteen shards each, the gate of the kiln passage), the arch of the side gate in Aschenwacht's east wall, the
    /// two weapons as regional finds, and the embers' materials and the sounds. Pictures stay passive: no light, no
    /// collider, no script; the rules decide the walk, the runtime adds light, particles and sound.
    /// </summary>
    public static class BrennerhofBuilder
    {
        public const string Pack = "Assets/Aschenwacht_Brennerhof";
        public const string ScenePath = Pack + "/Szenen/Der_verschleierte_Brennerhof.unity";
        public const string Visuals = "Assets/Resources/ARE/Visuals";
        public const string Kit = Visuals + "/Brennerhof";
        public const string Arsenal = Visuals + "/Werkarsenal";
        public const string WorldPrefab = Visuals + "/EntryKilnWorld.prefab";

        /// <summary>Pieces the runtime shows on its own instead of in the still picture.</summary>
        private static readonly string[] Living = { "Bauchamphore", "Glasururn", "Gittertor", "Hellebarde", "Speer" };

        /// <summary>The side gate's arch in Aschenwacht's east wall: the pack's narrower inner arch, turned along the wall.</summary>
        private static readonly Vector3 SideArchPosition = new Vector3(40.8f, 0f, KilnYard.SideGateZmm / 1000f);
        private static readonly Vector3 SideArchScale = new Vector3(.82f, .88f, .85f);

        [MenuItem("ARE ARPG/Art/Build Brennerhof")]
        public static void Build()
        {
            if (!TryBuild()) throw new InvalidOperationException("The Brennerhof pack is not installed (" + ScenePath + ").");
        }

        /// <summary>Build when the private pack is installed; false (and nothing written) without it.</summary>
        public static bool TryBuild()
        {
            if (!File.Exists(ScenePath)) return false;
            Folder(Visuals, "Brennerhof"); Folder(Visuals, "Werkarsenal");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var renderers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(false))
                    .Where(r => r.enabled && r.GetComponent<MeshFilter>() != null && r.GetComponent<MeshFilter>().sharedMesh != null).ToList();

                var world = new GameObject("EntryKilnWorld");
                int still = 0;
                foreach (var r in renderers)
                {
                    if (Living.Contains(Family(r.name))) continue;
                    Copy(r, world.transform, r.transform.position, r.transform.rotation, r.transform.lossyScale);
                    still++;
                }
                Save(world, WorldPrefab);

                var vases = renderers.Where(r => Family(r.name) == "Bauchamphore" || Family(r.name) == "Glasururn").ToList();
                if (vases.Count != KilnYard.Vases.Length) throw new InvalidOperationException("Brennerhof: expected 8 vases, found " + vases.Count);
                var taken = new HashSet<int>();
                foreach (var r in vases)
                {
                    KilnVase vase = KilnYard.Vases.OrderBy(v => (new Vector2(v.Xmm / 1000f, v.Zmm / 1000f) - new Vector2(r.transform.position.x, r.transform.position.z)).sqrMagnitude).First();
                    float miss = (new Vector2(vase.Xmm / 1000f, vase.Zmm / 1000f) - new Vector2(r.transform.position.x, r.transform.position.z)).magnitude;
                    if (miss > .25f || !taken.Add(vase.Id) || vase.Glazed != (Family(r.name) == "Glasururn"))
                        throw new InvalidOperationException("Brennerhof: vase at " + r.transform.position + " does not match the rules' vases");
                    var single = new GameObject("Vase_" + vase.Id);
                    Copy(r, single.transform, r.transform.position, r.transform.rotation, r.transform.lossyScale);
                    Save(single, Kit + "/Vase_" + vase.Id + ".prefab");
                    SaveShards(r, vase.Id);
                }

                var gate = renderers.Single(r => Family(r.name) == "Gittertor");
                if (Mathf.Abs(gate.transform.position.x - KilnYard.GateXmm / 1000f) > .1f || Mathf.Abs(gate.transform.position.z - KilnYard.GateZmm / 1000f) > .1f)
                    throw new InvalidOperationException("Brennerhof: the gate stands elsewhere than the rules' gate: " + gate.transform.position);
                var gateRoot = new GameObject("Gittertor");
                Copy(gate, gateRoot.transform, gate.transform.position, gate.transform.rotation, gate.transform.lossyScale);
                Save(gateRoot, Kit + "/Gittertor.prefab");

                var arch = renderers.First(r => Family(r.name) == "Torbogensegment");
                var side = new GameObject("Seitentor");
                Copy(arch, side.transform, SideArchPosition, Quaternion.Euler(0f, 90f, 0f), SideArchScale);
                Save(side, Kit + "/Seitentor.prefab");

                SaveWeapon(renderers.Single(r => Family(r.name) == "Speer"), RegionalRelic.B_Brennerspeer);
                SaveWeapon(renderers.Single(r => Family(r.name) == "Hellebarde"), RegionalRelic.B_Scherbenhellebarde);

                foreach (string material in new[] { "Glutflug", "Ascheflug" }) CopyAsset(Pack + "/Materialien/" + material + ".mat", Kit + "/" + material + ".mat");
                for (int i = 1; i <= 4; i++) CopyAsset(Pack + "/Audio/Tonbruch_" + i + ".wav", Kit + "/Tonbruch_" + i + ".wav");
                CopyAsset(Pack + "/Audio/Zugwind_Loop.wav", Kit + "/Zugwind_Loop.wav");
                AssetDatabase.SaveAssets();
                Debug.Log("ARE_BRENNERHOF_BUILT still=" + still + " vases=" + vases.Count + " renderers=" + renderers.Count);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
            return true;
        }

        /// <summary>The pack names every piece after its prefab; copies may carry a " (n)" suffix.</summary>
        private static string Family(string name)
        {
            int cut = name.IndexOf(" (", StringComparison.Ordinal);
            return cut < 0 ? name : name.Substring(0, cut);
        }

        private static void Folder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        private static GameObject Copy(MeshRenderer source, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var go = new GameObject(source.name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = source.sharedMaterials;
            renderer.shadowCastingMode = source.shadowCastingMode;
            renderer.receiveShadows = source.receiveShadows;
            return go;
        }

        /// <summary>
        /// The sixteen pieces the pack cut along each vase's own wall, where they sit in the whole vase: the pack's breaking
        /// script holds them (mesh, materials, offset in the vase's frame); it is read as serialized data, never run.
        /// </summary>
        private static void SaveShards(MeshRenderer vase, int id)
        {
            var breaker = vase.GetComponents<MonoBehaviour>().FirstOrDefault(m => m != null && m.GetType().Name == "Scherbenkrug");
            if (breaker == null) throw new InvalidOperationException("Brennerhof: vase without its pieces: " + vase.name);
            var pieces = new SerializedObject(breaker).FindProperty("stuecke");
            if (pieces == null || pieces.arraySize != 16) throw new InvalidOperationException("Brennerhof: a vase needs sixteen pieces: " + vase.name);
            var root = new GameObject("Scherben_" + id);
            for (int i = 0; i < pieces.arraySize; i++)
            {
                var piece = pieces.GetArrayElementAtIndex(i);
                var mesh = piece.FindPropertyRelative("mesh").objectReferenceValue as Mesh;
                var materialList = piece.FindPropertyRelative("materials");
                var materials = new Material[materialList.arraySize];
                for (int k = 0; k < materials.Length; k++) materials[k] = materialList.GetArrayElementAtIndex(k).objectReferenceValue as Material;
                if (mesh == null || materials.Length == 0 || materials.Any(m => m == null)) throw new InvalidOperationException("Brennerhof: incomplete piece " + i + " of " + vase.name);
                var go = new GameObject("Scherbe_" + i.ToString("00"));
                go.transform.SetParent(root.transform, false);
                go.transform.SetPositionAndRotation(vase.transform.TransformPoint(piece.FindPropertyRelative("offset").vector3Value), vase.transform.rotation);
                go.transform.localScale = vase.transform.lossyScale;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Save(root, Kit + "/Scherben_" + id + ".prefab");
        }

        /// <summary>
        /// A weapon as a regional find, in the frame the hand and the ground display use for every piece of the arsenal: the
        /// shaft along its own up axis, the grip at the origin (the pack models them that way).
        /// </summary>
        private static void SaveWeapon(MeshRenderer source, RegionalRelic id)
        {
            var root = new GameObject(id.ToString());
            var art = Copy(source, root.transform, Vector3.zero, Quaternion.identity, Vector3.one);
            art.name = id + "_Art";
            Save(root, Arsenal + "/" + id + ".prefab");
        }

        private static void Save(GameObject root, string path)
        {
            try
            {
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
                if (!ok) throw new InvalidOperationException("Brennerhof: saving failed: " + path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void CopyAsset(string from, string to)
        {
            if (AssetDatabase.LoadMainAssetAtPath(from) == null) throw new InvalidOperationException("Brennerhof: missing " + from);
            if (AssetDatabase.LoadMainAssetAtPath(to) != null) AssetDatabase.DeleteAsset(to);
            if (!AssetDatabase.CopyAsset(from, to)) throw new InvalidOperationException("Brennerhof: copy failed " + from);
        }
    }
}
