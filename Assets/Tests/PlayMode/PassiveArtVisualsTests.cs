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
    }
}
