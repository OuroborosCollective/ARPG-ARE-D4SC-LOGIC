using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AreArpg.EditMode.Tests
{
    /// <summary>Checks the actual saved private road-to-earth meshes after the editor pass.</summary>
    public sealed class VillageApproachEditModeTests
    {
        [Test]
        public void ArrivalRoadHasFadedPassiveEarthShouldersAndRetainsHeroArt()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR")))
                Assert.Ignore("Requires the licensed native village overlay");
            const string path = "Assets/Resources/ARE/Visuals/EntryVillageWorld.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, "Original village prefab must already exist");
            var sceneCopy = UnityEngine.Object.Instantiate(prefab);
            try
            {
                Transform hero = sceneCopy.transform.Find("VillageHeroPass1");
                Assert.IsNotNull(hero, "Original village hero pass lost");
                Assert.AreEqual(8, hero.childCount, "No old Viking group may be removed");

                Transform shoulders = sceneCopy.transform.Find("VillageApproachPass1");
                Assert.IsNotNull(shoulders, "Missing new earth-to-stone visual transition");
                Assert.AreEqual(1, shoulders.childCount, "A single batched mesh avoids excess mobile draw calls");
                Assert.IsEmpty(shoulders.GetComponentsInChildren<Collider>(true));
                Assert.IsEmpty(shoulders.GetComponentsInChildren<MonoBehaviour>(true));
                Assert.IsEmpty(shoulders.GetComponentsInChildren<Light>(true));

                var meshFilters = shoulders.GetComponentsInChildren<MeshFilter>(true);
                var renderers = shoulders.GetComponentsInChildren<MeshRenderer>(true);
                Assert.AreEqual(1, meshFilters.Length);
                Assert.AreEqual(1, renderers.Length);
                var mesh = meshFilters[0].sharedMesh;
                Assert.IsNotNull(mesh, "Road shoulder must be a real saved mesh");
                Assert.GreaterOrEqual(mesh.vertexCount, 48);
                Assert.GreaterOrEqual(mesh.triangles.Length / 3, 32);
                var alphas = mesh.colors32;
                Assert.AreEqual(mesh.vertexCount, alphas.Length);
                bool transparent = false, opaqueEnough = false;
                foreach (Color32 col in alphas)
                {
                    if (col.a == 0) transparent = true;
                    if (col.a >= 175) opaqueEnough = true;
                }
                Assert.IsTrue(transparent && opaqueEnough, "Shoulder must feather to earth, not create another hard border");
                var material = renderers[0].sharedMaterial;
                Assert.IsNotNull(material);
                Assert.IsNotNull(material.mainTexture, "Use the existing selected ground material, not a placeholder texture");
                Assert.IsNotNull(material.shader);
                Assert.AreEqual("ARE/VisualOnly/VillageEarthFeather", material.shader.name);
                Assert.IsTrue(material.shader.isSupported, "URP mobile-compatible vertex alpha shader must import");
                Assert.IsTrue(renderers[0].enabled);
            }
            finally { UnityEngine.Object.DestroyImmediate(sceneCopy); }
        }
    }
}
