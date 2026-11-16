using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AreArpg.EditMode.Tests
{
    public sealed class VillagePalisadeRimEditModeTests
    {
        [Test]
        public void OwnedWoodenVillageWallForestAndRollingHorizonUseRealPassiveArt()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR")))
                Assert.Ignore("Requires native licensed Viking-art project");
            const string prefabPath = "Assets/Resources/ARE/Visuals/EntryVillageWorld.prefab";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(source, "Never replace the original private authored village");
            var copy = UnityEngine.Object.Instantiate(source);
            try
            {
                var hero = copy.transform.Find("VillageHeroPass1");
                var approach = copy.transform.Find("VillageApproachPass1");
                Assert.IsNotNull(hero);
                Assert.AreEqual(8, hero.childCount);
                Assert.IsNotNull(approach);
                Assert.AreEqual(1, approach.childCount);
                var wall = copy.transform.Find("VillagePalisadeRimPass1");
                Assert.IsNotNull(wall, "Missing ring of wooden perimeter walls");
                Assert.AreEqual(5, wall.childCount, "West/east/south and two north gate shoulders");
                string[] segments = { "WestWall", "EastWall", "SouthWall", "NorthWestWall", "NorthEastWall" };
                foreach (string id in segments)
                {
                    var side = wall.Find(id);
                    Assert.IsNotNull(side, "Missing original wood fence section " + id);
                    Assert.Greater(side.childCount, 0);
                    foreach (var mr in side.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        Assert.IsNotNull(mr.sharedMaterial);
                        Assert.IsNotNull(mr.sharedMaterial.mainTexture, "Use real Viking wood-fence albedo");
                        Assert.AreEqual(ShadowCastingMode.Off, mr.shadowCastingMode, "Distant wall casts no mobile shadow map");
                    }
                }
                Assert.IsEmpty(wall.GetComponentsInChildren<Collider>(true));
                Assert.IsEmpty(wall.GetComponentsInChildren<MonoBehaviour>(true));
                Assert.IsEmpty(wall.GetComponentsInChildren<Light>(true));

                var trees = copy.transform.Find("VillageForestBehindPalisadePass1");
                Assert.IsNotNull(trees, "No forest behind the palisade");
                Assert.AreEqual(22, trees.childCount, "Curated fixed layout, no unbounded scattering");
                Assert.IsEmpty(trees.GetComponentsInChildren<Collider>(true));
                Assert.IsEmpty(trees.GetComponentsInChildren<MonoBehaviour>(true));
                Assert.IsEmpty(trees.GetComponentsInChildren<Light>(true));
                Assert.IsEmpty(trees.GetComponentsInChildren<LODGroup>(true));
                int woodRenderers=0, treeRenderers=0, tris=0;
                foreach (var r in wall.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var f=r.GetComponent<MeshFilter>();
                    Assert.IsNotNull(f?.sharedMesh);
                    woodRenderers++;
                    tris += f.sharedMesh.triangles.Length/3;
                }
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (Transform t in trees)
                {
                    Assert.IsTrue(seen.Add(t.name));
                    var renders=t.GetComponentsInChildren<MeshRenderer>(true);
                    Assert.Greater(renders.Length, 0, "Each tree must be real imported mesh art");
                    foreach(var mr in renders)
                    {
                        var f=mr.GetComponent<MeshFilter>();
                        Assert.IsNotNull(f?.sharedMesh);
                        Assert.IsNotNull(mr.sharedMaterial);
                        Assert.AreEqual(ShadowCastingMode.Off, mr.shadowCastingMode);
                        treeRenderers++;
                        tris += f.sharedMesh.triangles.Length/3;
                    }
                }
                Assert.That(woodRenderers, Is.InRange(35, 90), "Bound wooden perimeter draw calls");
                Assert.That(treeRenderers, Is.InRange(22, 110), "Bound imported tree draw calls");
                Assert.LessOrEqual(tris, 650000, "Do not blow the Android geometry budget");

                var ridge=copy.transform.Find("VillageRollingHorizonPass1");
                Assert.IsNotNull(ridge, "Straight ground horizon not yet concealed");
                var ridgeFilters=ridge.GetComponentsInChildren<MeshFilter>(true);
                Assert.AreEqual(1, ridgeFilters.Length, "Single batched, non-colliding earth ridge");
                Assert.Greater(ridgeFilters[0].sharedMesh.vertexCount, 100);
                Assert.IsEmpty(ridge.GetComponentsInChildren<Collider>(true));
                Assert.IsEmpty(ridge.GetComponentsInChildren<MonoBehaviour>(true));
                var ridgeRenderer=ridge.GetComponentInChildren<MeshRenderer>(true);
                Assert.IsNotNull(ridgeRenderer);
                Assert.IsNotNull(ridgeRenderer.sharedMaterial.mainTexture, "Use existing real ground albedo");
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }
    }
}
