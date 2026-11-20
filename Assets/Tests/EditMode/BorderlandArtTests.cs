using NUnit.Framework;
using UnityEngine;
using UnityEditor;

namespace AreArpg.EditMode.Tests
{
    public sealed class BorderlandArtTests
    {
        [TestCase("BloodcapBank")]
        [TestCase("AshPine")]
        [TestCase("StoneOutcrop")]
        [TestCase("TimberHouse")]
        [TestCase("MillWheel")]
        [TestCase("MillHouse")]
        [TestCase("GrainBay")]
        [TestCase("CryptArch")]
        [TestCase("CaveMouth")]
        [TestCase("StoneBridge")]
        [TestCase("WatchPost")]
        [TestCase("RibPillar")]
        [TestCase("Brazier")]
        [TestCase("SupplySack")]
        [TestCase("CoinStack")]
        [TestCase("BrokenCart")]
        [TestCase("Palisade")]
        [TestCase("Waystone")]
        [TestCase("RangedQuiver")]
        [TestCase("ShieldPlate")]
        [TestCase("SwarmSpines")]
        [TestCase("EliteCrest")]
        [TestCase("ShaleLedge")]
        [TestCase("DenLitter")]
        public void OriginalRegionalPrefabHasThreeTexturedMobileLods(string key)
        {
            var p = Resources.Load<GameObject>("ARE/Borderland/" + key);
            Assert.IsNotNull(p, key + " needs a published projection prefab");
            var lods = p.GetComponentInChildren<LODGroup>().GetLODs();
            Assert.AreEqual(3, lods.Length, key);
            int previous = int.MaxValue;
            Material palette = null;
            foreach (var lod in lods)
            {
                Assert.AreEqual(1, lod.renderers.Length, "one renderer per visible asset");
                var r = lod.renderers[0];
                var m = r.GetComponent<MeshFilter>().sharedMesh;
                Assert.IsNotNull(m);
                Assert.AreEqual(1, m.subMeshCount);
                int tris = (int)m.GetIndexCount(0) / 3;
                Assert.That(tris, Is.InRange(4, 3500));
                Assert.Less(tris, previous);
                previous = tris;
                Assert.IsTrue(m.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0));
                Assert.IsTrue(m.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Normal));
                Assert.That(r.bounds.min.y, Is.InRange(-0.025f, 0.025f), "authored ground pivot");
                Assert.AreEqual(1, r.sharedMaterials.Length);
                if (palette == null) palette = r.sharedMaterial;
                Assert.AreSame(palette, r.sharedMaterial);
                StringAssert.StartsWith("Universal Render Pipeline/", palette.shader.name);
                Assert.IsFalse(ShaderUtil.ShaderHasError(palette.shader));
                Assert.IsNotNull(palette.GetTexture("_BaseMap"));
            }
            Assert.IsEmpty(p.GetComponentsInChildren<Light>(true));
            var shape = p.GetComponent<BoxCollider>();
            Assert.IsNotNull(shape, "authoring bounds");
            Assert.IsFalse(shape.enabled, "Core owns movement");
        }
    }
}
