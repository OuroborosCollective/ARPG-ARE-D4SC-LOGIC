using System;
using System.IO;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AreArpg.EditMode.Tests
{
    /// <summary>Checks the real privately composed mesh prefab, not a geometry mock.</summary>
    public sealed class VillageHeroEditModeTests
    {
        [Serializable] private sealed class Plan { public string schema; public Placement[] placements; }
        [Serializable] private sealed class Placement
        {
            public string id;
            public int xMm, zMm, maxWidthMm, maxHeightMm, maxDepthMm;
        }

        [Test]
        public void PrivateVillageHeroPrefabHasEightGroundedPassiveRenderGroups()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR")))
                Assert.Ignore("Requires installed licensed private village art in the native editor");
            var plan = JsonUtility.FromJson<Plan>(File.ReadAllText("docs/visual-pass-1/village-hero-layout.json"));
            Assert.IsNotNull(plan);
            Assert.AreEqual("ashen-crypt.visual-pass-1-village-hero.v1", plan.schema);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/ARE/Visuals/EntryVillageWorld.prefab");
            Assert.IsNotNull(prefab, "Real world art must be installed in this isolated workspace");
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var hero = instance.transform.Find("VillageHeroPass1");
                Assert.IsNotNull(hero, "The saved native village must contain the new pass");
                Assert.AreEqual(plan.placements.Length, hero.childCount);
                int totalRenderers = 0;
                long totalVertices = 0;
                long totalTriangles = 0;
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (Placement placement in plan.placements)
                {
                    Transform piece = hero.Find(placement.id);
                    Assert.IsNotNull(piece, "Missing hero asset: " + placement.id);
                    Assert.IsTrue(ids.Add(placement.id), "Duplicate hero art id");
                    foreach (Component component in piece.GetComponentsInChildren<Component>(true))
                        Assert.IsTrue(component is Transform || component is MeshFilter ||
                                      component is MeshRenderer, "Vendor physics or script leaked: " + placement.id);
                    var renderers = piece.GetComponentsInChildren<MeshRenderer>(true);
                    Assert.IsNotEmpty(renderers, "Hero art needs real imported meshes");
                    bool initialized = false;
                    Bounds extent = new Bounds();
                    foreach (var renderer in renderers)
                    {
                        MeshFilter filter = renderer.GetComponent<MeshFilter>();
                        Assert.IsNotNull(filter);
                        Assert.IsNotNull(filter.sharedMesh, "Missing imported mesh in " + placement.id);
                        // One mesh renderer per authentic Viking source is valid. Prove origin
                        // and actual mesh complexity rather than guessing the submesh count.
                        string importedMeshPath = AssetDatabase.GetAssetPath(filter.sharedMesh);
                        Assert.That(importedMeshPath,
                            Does.StartWith("Assets/PrivateArt/VisualPass1/VikingVillage/Models/"),
                            "The visible art must use real imported Viking FBX geometry: " + placement.id);
                        totalVertices += filter.sharedMesh.vertexCount;
                        for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++)
                            totalTriangles += (long)filter.sharedMesh.GetIndexCount(submesh) / 3;
                        Assert.IsNotEmpty(renderer.sharedMaterials);
                        foreach (Material material in renderer.sharedMaterials)
                        {
                            Assert.IsNotNull(material, "Missing material in " + placement.id);
                            Assert.IsNotNull(material.shader);
                            Assert.AreNotEqual("Hidden/InternalErrorShader", material.shader.name);
                        }
                        if (!initialized) extent = renderer.bounds;
                        else extent.Encapsulate(renderer.bounds);
                        initialized = true;
                    }
                    totalRenderers += renderers.Length;
                    Assert.That(extent.min.y, Is.EqualTo(0f).Within(.055f), "Ground contact: " + placement.id);
                    Assert.That(extent.center.x, Is.EqualTo(placement.xMm / 1000f).Within(.055f),
                        "X center drift: " + placement.id);
                    Assert.That(extent.center.z, Is.EqualTo(placement.zMm / 1000f).Within(.055f),
                        "Z center drift: " + placement.id);
                    Assert.LessOrEqual(extent.size.x * 1000f, placement.maxWidthMm + 55, "Width: " + placement.id);
                    Assert.LessOrEqual(extent.size.y * 1000f, placement.maxHeightMm + 55, "Height: " + placement.id);
                    Assert.LessOrEqual(extent.size.z * 1000f, placement.maxDepthMm + 55, "Depth: " + placement.id);
                }
                Assert.GreaterOrEqual(totalRenderers, plan.placements.Length,
                    "Every selected piece needs at least one real imported renderer");
                Assert.Greater(totalVertices, 192,
                    "Total authored vertices must exceed eight primitive cube equivalents");
                Assert.Greater(totalTriangles, 512,
                    "Actual source geometry must exceed simple placeholder boxes");
                Debug.Log("ARE_VILLAGE_HERO_EDIT_PASS renderers=" + totalRenderers +
                    " vertices=" + totalVertices + " triangles=" + totalTriangles);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
    }
}
