using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;

namespace AreArpg.PlayMode.Tests
{
    /// <summary>Visual-only prefab replacement must never change authoritative gameplay or colliders.</summary>
    public sealed class PassiveArtVisualsTests
    {
        [Test]
        public void MissingAssetKeepsPrimitiveFallbackVisible()
        {
            GameObject host = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                Assert.IsFalse(PassiveArtVisuals.TryBind(host.transform, "DefinitelyMissingVisual"));
                Assert.IsTrue(host.GetComponent<Renderer>().enabled);
                Assert.AreEqual(0, host.transform.childCount);
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void PassiveMeshReplacesPrimitiveRendererWithoutChangingHostTransform()
        {
            GameObject host = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject asset = new GameObject("ApprovedHeroVisual");
            asset.AddComponent<MeshFilter>();
            asset.AddComponent<MeshRenderer>();
            host.transform.position = new Vector3(2f, 3f, -4f);
            try
            {
                Assert.IsTrue(PassiveArtVisuals.TryBindPrefab(host.transform, asset));
                Assert.IsFalse(host.GetComponent<Renderer>().enabled);
                Assert.AreEqual(new Vector3(2f, 3f, -4f), host.transform.position);
                Assert.AreEqual(1, host.transform.childCount);
                Assert.IsNull(host.transform.GetChild(0).GetComponent<Collider>());
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(asset); }
        }

        [Test]
        public void PrefabWithColliderIsRejectedBeforeInstantiation()
        {
            GameObject host = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject unsafePrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                Assert.IsFalse(PassiveArtVisuals.TryBindPrefab(host.transform, unsafePrefab));
                Assert.AreEqual(0, host.transform.childCount);
                Assert.IsTrue(host.GetComponent<Renderer>().enabled);
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(unsafePrefab); }
        }

        [Test]
        public void ForeignPipelineMaterialIsAdaptedWithoutTouchingTheSourceMaterial()
        {
            // Built-in "Standard" materials render magenta under the project URP pipeline.
            Assert.IsNotNull(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline, "project URP pipeline must be active");
            Shader builtIn = Shader.Find("Standard");
            Assert.IsNotNull(builtIn);
            var texture = new Texture2D(2, 2);
            var source = new Material(builtIn) { name = "VendorSkin", mainTexture = texture, color = new Color(0.5f, 0.25f, 0.125f, 1f) };
            GameObject host = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject asset = new GameObject("ApprovedEnemyVisual");
            asset.AddComponent<MeshFilter>();
            asset.AddComponent<MeshRenderer>().sharedMaterial = source;
            try
            {
                Assert.IsTrue(PassiveArtVisuals.TryBindPrefab(host.transform, asset));
                Material bound = host.transform.GetChild(0).GetComponent<MeshRenderer>().sharedMaterial;
                Assert.AreNotSame(source, bound);
                Assert.AreEqual("UniversalPipeline", bound.GetTag("RenderPipeline", false, ""));
                Assert.AreSame(texture, bound.mainTexture);
                Assert.IsTrue(source.color == bound.color, "tint differs: " + source.color + " vs " + bound.color); // Unity approximate colour equality
                Assert.AreSame(builtIn, source.shader, "vendor material must stay untouched");
                Assert.AreSame(source, asset.GetComponent<MeshRenderer>().sharedMaterial);

                // The same source material maps to one shared adapter (no per-instance material leak).
                GameObject secondHost = GameObject.CreatePrimitive(PrimitiveType.Cube);
                try
                {
                    Assert.IsTrue(PassiveArtVisuals.TryBindPrefab(secondHost.transform, asset));
                    Assert.AreSame(bound, secondHost.transform.GetChild(0).GetComponent<MeshRenderer>().sharedMaterial);
                }
                finally { Object.DestroyImmediate(secondHost); }
            }
            finally
            {
                Object.DestroyImmediate(host); Object.DestroyImmediate(asset);
                Object.DestroyImmediate(source); Object.DestroyImmediate(texture);
            }
        }
    }
}
