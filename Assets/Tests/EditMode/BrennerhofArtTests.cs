using System.Linq;
using AreArpg.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AreArpg.EditMode.Tests
{
    /// <summary>
    /// The Brennerhof's built pictures (owner decision 2026-11-25: the pack becomes Aschenwacht's side yard) stay passive
    /// (no collider, light, particle or script: the rules decide the walk, the runtime adds light, embers and sound), keep
    /// within the tablet's budget, and put the living parts where the rules expect them: eight vases with sixteen shards
    /// each, the gate of the kiln passage, the two weapons as regional finds. The kit is private art: without it the test
    /// is ignored.
    /// </summary>
    public sealed class BrennerhofArtTests
    {
        private const string Visuals = "Assets/Resources/ARE/Visuals/";
        private const string Kit = Visuals + "Brennerhof/";
        private const long TriangleBudget = 500000;

        [Test]
        public void TheYardAndItsLivingPartsArePassiveAndWhereTheRulesExpectThem()
        {
            var world = AssetDatabase.LoadAssetAtPath<GameObject>(Visuals + "EntryKilnWorld.prefab");
            if (world == null) Assert.Ignore("Requires the private Brennerhof pack");

            var pictures = new[] { world }.Concat(Enumerable.Range(0, KilnYard.Vases.Length).SelectMany(i => new[]
                { Load(Kit + "Vase_" + i + ".prefab"), Load(Kit + "Scherben_" + i + ".prefab") }))
                .Concat(new[] { Load(Kit + "Gittertor.prefab"), Load(Kit + "Seitentor.prefab"),
                    Load(Visuals + "Werkarsenal/B_Brennerspeer.prefab"), Load(Visuals + "Werkarsenal/B_Scherbenhellebarde.prefab") }).ToArray();
            foreach (var picture in pictures)
            {
                Assert.IsNotNull(picture, "every picture of the kit is built");
                var parts = picture.GetComponentsInChildren<Component>(true);
                Assert.IsTrue(parts.All(c => c is Transform || c is MeshFilter || c is MeshRenderer), picture.name + " holds only meshes");
                Assert.IsTrue(picture.GetComponentsInChildren<MeshRenderer>(true).All(r => r.sharedMaterials.Length > 0 && r.sharedMaterials.All(m => m != null)),
                    picture.name + " has every material");
            }

            long triangles = world.GetComponentsInChildren<MeshFilter>(true).Sum(f => (long)f.sharedMesh.triangles.Length / 3);
            Assert.LessOrEqual(triangles, TriangleBudget, "the yard stays within the tablet budget");
            Assert.Greater(world.GetComponentsInChildren<MeshRenderer>(true).Length, 300, "every still piece of the pack's scene is kept");
            Assert.IsTrue(world.GetComponentsInChildren<MeshRenderer>(true).Any(r => r.name.StartsWith("Aschenabtei")) &&
                world.GetComponentsInChildren<MeshRenderer>(true).Count(r => r.name.StartsWith("Bodennebel")) == 6, "horizon and ground fog stand in the yard");
            Assert.IsTrue(world.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.StartsWith("Fernkamm")).SelectMany(r => r.sharedMaterials)
                .All(m => m.GetFloat("_Metallic") == 0f && m.GetFloat("_Smoothness") <= .2f), "the ridges on the horizon are matte");

            foreach (KilnVase vase in KilnYard.Vases)
            {
                var single = Load(Kit + "Vase_" + vase.Id + ".prefab").GetComponentInChildren<MeshRenderer>(true).transform;
                Assert.AreEqual(vase.Xmm / 1000f, single.position.x, .25f, "vase " + vase.Id + " x");
                Assert.AreEqual(vase.Zmm / 1000f, single.position.z, .25f, "vase " + vase.Id + " z");
                var shards = Load(Kit + "Scherben_" + vase.Id + ".prefab").GetComponentsInChildren<MeshRenderer>(true);
                Assert.AreEqual(16, shards.Length, "sixteen shards of vase " + vase.Id);
                Assert.IsTrue(shards.All(s => (new Vector2(s.transform.position.x, s.transform.position.z) - new Vector2(single.position.x, single.position.z)).magnitude < .8f),
                    "the shards of vase " + vase.Id + " sit where the vase stands");
            }
            var gate = Load(Kit + "Gittertor.prefab").GetComponentInChildren<MeshRenderer>(true).transform;
            Assert.AreEqual(KilnYard.GateZmm / 1000f, gate.position.z, .1f, "the gate closes the kiln passage where the rules close it");
            foreach (string weapon in new[] { "B_Brennerspeer", "B_Scherbenhellebarde" })
            {
                var size = Load(Visuals + "Werkarsenal/" + weapon + ".prefab").GetComponentInChildren<MeshFilter>(true).sharedMesh.bounds.size;
                Assert.That(size.y, Is.InRange(2.2f, 3.0f), weapon + " is a long polearm along its up axis");
            }
            Assert.IsTrue(Enumerable.Range(1, 4).All(i => Resources.Load<AudioClip>("ARE/Visuals/Brennerhof/Tonbruch_" + i) != null) &&
                Resources.Load<AudioClip>("ARE/Visuals/Brennerhof/Zugwind_Loop") != null, "the four breaking sounds and the draught are there");
            Assert.IsNotNull(Resources.Load<Material>("ARE/Visuals/Brennerhof/Glutflug"), "the embers' material is there");
        }

        private static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }
}
