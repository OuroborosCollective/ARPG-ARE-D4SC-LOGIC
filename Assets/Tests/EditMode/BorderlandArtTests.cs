using NUnit.Framework;
using UnityEngine;
using UnityEditor;

namespace AreArpg.EditMode.Tests
{
    public sealed class BorderlandArtTests
    {
        /// <summary>A detail part of a regional asset: one small draw call with a working material.</summary>
        private static void AssertRegionalDetail(Renderer detail, string key)
        {
            Assert.IsNotNull(detail, key + " detail renderer");
            var filter = detail.GetComponent<MeshFilter>();
            Assert.IsNotNull(filter, key + " detail mesh filter");
            var mesh = filter.sharedMesh;
            Assert.IsNotNull(mesh, key + " detail mesh");
            Assert.AreEqual(1, mesh.subMeshCount, key + " detail is one draw call");
            Assert.That((int)mesh.GetIndexCount(0) / 3, Is.InRange(1, DetailTriangleLimit), key + " detail triangles (" + detail.name + ")");
            Assert.AreEqual(1, detail.sharedMaterials.Length, key + " detail material count");
            Assert.IsNotNull(detail.sharedMaterial, key + " detail material");
            Assert.IsFalse(ShaderUtil.ShaderHasError(detail.sharedMaterial.shader), key + " detail shader");
        }

        private const int DetailTriangleLimit = 1500;

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
        [TestCase("EmberSnag")]
        [TestCase("EmberSedge")]
        public void OriginalRegionalPrefabHasThreeTexturedMobileLods(string key)
        {
            var p = Resources.Load<GameObject>("ARE/Borderland/" + key);
            Assert.IsNotNull(p, key + " needs a published projection prefab");
            var lods = p.GetComponentInChildren<LODGroup>().GetLODs();
            Assert.AreEqual(3, lods.Length, key);
            int previous = int.MaxValue;
            Material palette = null;
            for (int level = 0; level < lods.Length; level++)
            {
                var lod = lods[level];
                // The asset itself is one renderer. Near the camera it may carry up to three detail parts of the
                // region (flames, shale relief, a cooled ember channel); the farthest level is the asset alone.
                Assert.That(lod.renderers.Length, Is.InRange(1, level == lods.Length - 1 ? 1 : 4),
                    "one renderer per visible asset, detail parts only near the camera");
                for (int extra = 1; extra < lod.renderers.Length; extra++) AssertRegionalDetail(lod.renderers[extra], key);
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
                Assert.IsFalse(ShaderUtil.ShaderHasError(palette.shader));
                if (key == "StoneOutcrop" || key == "ShaleLedge" || key == "CryptArch" || key == "Waystone")
                {
                    Assert.AreEqual("UniversalPipeline", palette.GetTag("RenderPipeline", false));
                    Assert.IsTrue(palette.IsKeywordEnabled("ARE_CLEFT_STONE"), "Exposed stone needs vertical grain and ash on upper faces.");
                    Assert.AreEqual(1024, palette.GetTexture("_Mask").width);
                }
                else
                {
                    StringAssert.StartsWith("Universal Render Pipeline/", palette.shader.name);
                    Assert.IsNotNull(palette.GetTexture("_BaseMap"));
                }
            }
            Assert.IsEmpty(p.GetComponentsInChildren<Light>(true));
            var shape = p.GetComponent<BoxCollider>();
            Assert.IsNotNull(shape, "authoring bounds");
            Assert.IsFalse(shape.enabled, "Core owns movement");
        }
    }
}
