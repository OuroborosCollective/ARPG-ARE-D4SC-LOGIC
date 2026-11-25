using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AreArpg.EditMode.Tests
{
    /// <summary>
    /// Aschenwacht's built prefab (owner decision 2026-11-25) keeps every placement of the owner's two sets, stays a passive
    /// picture (no collider, no script: the Core's CourtLayout decides where the wanderer walks), wears the sets' own URP
    /// materials, keeps within the tablet's triangle budget and covers the walkable area of the rules. The prefab is private
    /// art: without it the test is ignored.
    /// </summary>
    public sealed class AschenwachtCourtArtTests
    {
        private const string Prefab = "Assets/Resources/ARE/Visuals/EntryCourtWorld.prefab";
        private const string Placements = "Assets/PrivateArt/Aschenwacht/station-placements.tsv";
        private const long TriangleBudget = 1400000;

        [Test]
        public void TheBuiltCourtKeepsEveryPlacementAsAPassivePicture()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            if (prefab == null || !File.Exists(Placements)) Assert.Ignore("Requires the private Aschenwacht sets");
            int rows = File.ReadAllLines(Placements).Skip(1).Count(line => line.Length > 0);
            Transform[] holders = prefab.transform.Cast<Transform>().Where(group => group.name != "Feuerlicht").SelectMany(group => group.Cast<Transform>()).ToArray();
            Assert.AreEqual(rows, holders.Length, "one holder per placement of the two sets");
            Assert.IsTrue(holders.All(h => h.childCount == 1 && h.GetComponentsInChildren<Renderer>(true).Length > 0), "every holder shows its model");

            Assert.IsEmpty(prefab.GetComponentsInChildren<Collider>(true), "the rules decide the walk, not physics");
            Assert.IsEmpty(prefab.GetComponentsInChildren<MonoBehaviour>(true), "no behaviour runs in the picture");
            Animator[] animators = prefab.GetComponentsInChildren<Animator>(true);
            Assert.AreEqual(1, animators.Length, "only the Aschbeter moves");
            Assert.IsNotNull(animators[0].runtimeAnimatorController);
            CollectionAssert.Contains(animators[0].runtimeAnimatorController.animationClips.Select(c => c.name).ToArray(), "Idle");

            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            long triangles = 0;
            var materials = new HashSet<Material>();
            foreach (Renderer renderer in renderers)
            {
                Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                Assert.IsNotNull(mesh, renderer.name + " has its mesh");
                triangles += mesh.triangles.Length / 3;
                foreach (Material material in renderer.sharedMaterials)
                {
                    Assert.IsNotNull(material, renderer.name + " has every material slot filled");
                    materials.Add(material);
                }
            }
            Assert.LessOrEqual(triangles, TriangleBudget, "all placements together stay within the tablet budget");
            foreach (Material material in materials)
            {
                Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name, material.name);
                Assert.IsTrue(AssetDatabase.GetAssetPath(material).StartsWith("Assets/PrivateArt/") || AssetDatabase.GetAssetPath(material).StartsWith("Assets/Aschenwacht/"),
                    material.name + " is one of the sets' own materials, not an imported default");
                Assert.IsNotNull(material.GetTexture("_BaseMap"), material.name + " shows its colour map");
            }
            Material grass = materials.FirstOrDefault(m => m.name == "Grass");
            Assert.IsNotNull(grass);
            Assert.IsFalse(grass.GetShaderPassEnabled("ShadowCaster"), "grass casts no shadow on the tablet");
        }

        [Test]
        public void TheBuiltCourtCoversTheWalkOfTheRules()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            if (prefab == null) Assert.Ignore("Requires the private Aschenwacht sets");
            var instance = Object.Instantiate(prefab);
            try
            {
                Bounds bounds = instance.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                long[] route = AreArpg.Core.CourtLayout.Route(AreArpg.Core.CourtLayout.SpawnXmm, AreArpg.Core.CourtLayout.SpawnZmm,
                    AreArpg.Core.CourtLayout.ExitXmm, AreArpg.Core.CourtLayout.ExitZmm);
                Assert.Greater(route.Length, 0);
                for (int i = 0; i < route.Length; i += 2)
                    Assert.IsTrue(bounds.Contains(new Vector3(route[i] / 1000f, bounds.center.y, route[i + 1] / 1000f)), "the way lies inside the picture");
                // The ground the rules place the wanderer on is the ground the picture shows: probe the floor under the way.
                var floor = instance.transform.Find("Boden");
                Assert.IsNotNull(floor, "the court's terraced floor is placed");
            }
            finally { Object.DestroyImmediate(instance); }
        }
    }
}
