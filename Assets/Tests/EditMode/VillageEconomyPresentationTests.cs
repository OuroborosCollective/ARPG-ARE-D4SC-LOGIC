using System;
using System.Collections.Generic;
using System.Linq;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AreArpg.EditMode.Tests
{
    /// <summary>
    /// The village economy's presentation rules that need no running game: which workplace shows which good, how
    /// figures find their way, and - when the private models are installed - that each model carries exactly the
    /// parts the game shows. Without the private models those checks are reported as ignored, never as passed.
    /// </summary>
    public sealed class VillageEconomyPresentationTests
    {
        const string NotInstalled = "Requires the private workplace models with built visuals (never in the repository)";

        [Test]
        public void EveryShownGoodHasOneWorkplace()
        {
            var shown = new HashSet<VillageGood>();
            var spots = new HashSet<VillageSpot>();
            int fires = 0;
            foreach (ArenaRuntime.VillageWorkplace workplace in ArenaRuntime.VillageWorkplaces)
            {
                Assert.IsTrue(spots.Add(workplace.Spot), workplace.Key + " shares its place with another workplace");
                Assert.AreEqual(workplace.Goods.Length, workplace.Units.Length);
                for (int g = 0; g < workplace.Goods.Length; g++)
                {
                    Assert.IsTrue(Enum.IsDefined(typeof(VillageGood), workplace.Goods[g]), workplace.Key + " shows a good the rules do not know");
                    Assert.Greater(workplace.Units[g], 0);
                    Assert.IsTrue(shown.Add(workplace.Goods[g]), workplace.Goods[g] + " would be shown at two places");
                }
                if (workplace.Fire) fires++;
                Assert.Greater(workplace.Size.x, 0f); Assert.Greater(workplace.Size.y, 0f); Assert.Greater(workplace.Height, 0f);
            }
            Assert.AreEqual(1, fires, "One cooking fire");
            // Goods kept in the village are all seen somewhere. Seedlings stand at the forest edge and herbs go to the mill.
            foreach (VillageGood good in Enum.GetValues(typeof(VillageGood)))
                if (good != VillageGood.Seedling && good != VillageGood.Herb) Assert.IsTrue(shown.Contains(good), good + " has no place to be seen at");
            foreach (VillageSpot own in new[] { VillageSpot.Store, VillageSpot.Home, VillageSpot.Gate })
                Assert.IsFalse(spots.Contains(own), own + " is an existing place and gets no second model");
        }

        [Test]
        public void TheWorkerStandsInFrontOfTheWorkplaceNotInIt()
        {
            foreach (ArenaRuntime.VillageWorkplace workplace in ArenaRuntime.VillageWorkplaces)
            {
                var place = new Vector2(VillageEconomyLayout.PlaceXmm(workplace.Spot), VillageEconomyLayout.PlaceZmm(workplace.Spot)) / 1000f;
                var stand = new Vector2(VillageEconomyLayout.StandXmm(workplace.Spot), VillageEconomyLayout.StandZmm(workplace.Spot)) / 1000f;
                // The model's front is +Z and it is turned to face the stand point: the worker is straight in front of it.
                float distance = (stand - place).magnitude;
                float frontEdge = workplace.Centre.y + workplace.Size.y * 0.5f;
                Assert.Greater(distance, frontEdge + 0.25f, workplace.Key + ": the worker would stand inside the workplace");
                Assert.Less(distance, frontEdge + 1.6f, workplace.Key + ": the worker would stand too far away to be at work");
            }
        }

        static bool SegmentIsFree(VillageWalkGrid grid, Vector2 a, Vector2 b, Vector2 start, Vector2 goal)
        {
            int samples = Mathf.CeilToInt((b - a).magnitude / 0.25f) + 1;
            for (int i = 0; i <= samples; i++)
            {
                Vector2 point = Vector2.Lerp(a, b, i / (float)samples);
                // Around the two ends a figure may stand on covered ground (a worker stands close to the workplace).
                if ((point - start).magnitude < 0.75f || (point - goal).magnitude < 0.75f) continue;
                if (grid.Blocked(point)) return false;
            }
            return true;
        }

        [Test]
        public void FiguresWalkAroundBuildingsAndThings()
        {
            var grid = new VillageWalkGrid();
            grid.Reset();
            var path = new List<Vector2>();
            // Straight through the supply hall is not a way: from west of it to the market square.
            var from = new Vector2(-17f, -8.7f); var to = new Vector2(-7f, -8.7f);
            Assert.IsTrue(grid.Blocked(new Vector2(-12.5f, -8.7f)), "The supply hall is covered ground");
            Assert.IsTrue(grid.Find(from, to, path));
            Assert.Greater(path.Count, 1, "The way has corners");
            Assert.AreEqual(to, path[path.Count - 1], "The way ends at the target itself");
            Vector2 at = from; float length = 0f;
            foreach (Vector2 corner in path)
            {
                Assert.IsTrue(SegmentIsFree(grid, at, corner, from, to), "A leg of the way crosses covered ground: " + at + " -> " + corner);
                length += (corner - at).magnitude; at = corner;
            }
            Assert.Less(length, 20f, "The way round the hall is a short detour, not a tour of the village");

            // In clear view the way is one straight leg.
            Assert.IsTrue(grid.Find(new Vector2(0f, -20f), new Vector2(0f, 10f), path));
            Assert.AreEqual(1, path.Count);

            // A thing in the way is walked around; a worker's place close to a thing can still be reached.
            grid.BlockDisc(new Vector2(0f, -5f), 1.5f);
            Assert.IsTrue(grid.Find(new Vector2(0f, -20f), new Vector2(0f, 10f), path));
            Assert.Greater(path.Count, 1);
            at = new Vector2(0f, -20f);
            foreach (Vector2 corner in path) { Assert.IsTrue(SegmentIsFree(grid, at, corner, new Vector2(0f, -20f), new Vector2(0f, 10f))); at = corner; }
            Assert.IsTrue(grid.Find(new Vector2(0f, -20f), new Vector2(0f, -4.2f), path), "A target on covered ground is still reached");
            Assert.AreEqual(new Vector2(0f, -4.2f), path[path.Count - 1]);

            // The middle of a covered block cannot be reached: no way is found, and the figure is sent straight
            // to its place rather than left standing.
            grid.Reset();
            grid.BlockBox(new Vector2(10f, 5f), Vector2.right, 3f, 3f);
            Assert.IsFalse(grid.Find(new Vector2(-10f, 5f), new Vector2(10f, 5f), path));
            Assert.AreEqual(1, path.Count);
            Assert.AreEqual(new Vector2(10f, 5f), path[0]);
        }

        [Test]
        public void TurnedBoxesCoverTheGroundTheyStandOn()
        {
            var grid = new VillageWalkGrid();
            grid.Reset();
            // A 4 x 1 m box turned by 45 degrees around the middle of the east side.
            Vector2 side = new Vector2(1f, 1f).normalized;
            grid.BlockBox(new Vector2(6f, 6f), side, 2f, 0.5f);
            Assert.IsTrue(grid.Blocked(new Vector2(6f, 6f)));
            Assert.IsTrue(grid.Blocked(new Vector2(7f, 7f)), "Along the box");
            Assert.IsFalse(grid.Blocked(new Vector2(8f, 4f)), "Across the box, well outside its depth");
            Assert.IsFalse(grid.Blocked(new Vector2(4f, 8f)));
        }

        [Test]
        public void TheBoardFitsTheSmallestScreenAndItsButtonsDoNotOverlap()
        {
            foreach (Vector2 screen in new[] { new Vector2(960f, 540f), new Vector2(2400f, 1080f), new Vector2(1280f, 800f) })
            {
                var layout = new ArenaHudLayout(screen.x, screen.y, new Rect(0f, 0f, screen.x, screen.y));
                Rect panel = ArenaRuntime.VillageBoardPanel(layout), close = ArenaRuntime.VillageBoardCloseRect(layout);
                Assert.GreaterOrEqual(panel.xMin, 0f); Assert.LessOrEqual(panel.xMax, layout.Width);
                Assert.GreaterOrEqual(panel.yMin, 0f); Assert.LessOrEqual(panel.yMax, layout.Height, "The board fits on " + screen);
                var buttons = new List<Rect> { close };
                for (int i = 0; i < VillageBoard.GiftGoods.Count; i++) buttons.Add(ArenaRuntime.VillageGiftRect(layout, i));
                for (int a = 0; a < buttons.Count; a++)
                {
                    Assert.IsTrue(panel.Contains(buttons[a].min) && panel.Contains(buttons[a].max), "Button " + a + " lies on the board");
                    Assert.GreaterOrEqual(buttons[a].height, 30f, "A finger can hit the button");
                    for (int b = a + 1; b < buttons.Count; b++) Assert.IsFalse(buttons[a].Overlaps(buttons[b]), "Buttons " + a + " and " + b + " overlap");
                }
                // The button that opens the board is the shared context button below the middle of the screen.
                Assert.AreEqual(layout.ContextActionRect, ArenaRuntime.VillageBoardToggleRect(layout));
            }
        }

        static GameObject Visual(ArenaRuntime.VillageWorkplace workplace)
        {
            var prefab = Resources.Load<GameObject>("ARE/Visuals/" + workplace.Key);
            if (prefab == null) Assert.Ignore(NotInstalled);
            return prefab;
        }

        [Test]
        public void PrivateModelsCarryExactlyThePartsTheGameShows()
        {
            foreach (ArenaRuntime.VillageWorkplace workplace in ArenaRuntime.VillageWorkplaces)
            {
                GameObject prefab = Visual(workplace);
                Transform[] parts = prefab.GetComponentsInChildren<Transform>(true);
                // A model of one part arrives as that part itself; otherwise the furniture is the part named Base.
                Assert.IsTrue(prefab.GetComponent<MeshRenderer>() != null || parts.Any(p => p.name == "Base"), workplace.Key + " has no furniture part");
                var expected = new HashSet<string> { prefab.name, "Base" };
                for (int g = 0; g < workplace.Goods.Length; g++)
                {
                    string prefix = ArenaRuntime.StockPrefix + workplace.Goods[g] + "_";
                    Assert.AreEqual(workplace.Units[g], parts.Count(p => p.name.StartsWith(prefix, StringComparison.Ordinal)), workplace.Key + " units of " + workplace.Goods[g]);
                    foreach (Transform part in parts) if (part.name.StartsWith(prefix, StringComparison.Ordinal)) expected.Add(part.name);
                }
                if (workplace.Fire) { Assert.IsTrue(parts.Any(p => p.name == ArenaRuntime.FireName), "The kitchen has its fire"); expected.Add(ArenaRuntime.FireName); }
                foreach (Transform part in parts) Assert.IsTrue(expected.Contains(part.name), workplace.Key + " carries a part the game does not know: " + part.name);
            }
        }

        [Test]
        public void PrivateModelsArePassiveAndCheap()
        {
            Material shared = null;
            foreach (ArenaRuntime.VillageWorkplace workplace in ArenaRuntime.VillageWorkplaces)
            {
                GameObject prefab = Visual(workplace);
                Assert.IsEmpty(prefab.GetComponentsInChildren<Collider>(true), workplace.Key + " must not bring physics");
                Assert.IsEmpty(prefab.GetComponentsInChildren<MonoBehaviour>(true), workplace.Key + " must not bring scripts");
                Assert.IsEmpty(prefab.GetComponentsInChildren<Animator>(true));
                int triangles = 0;
                Bounds bounds = new Bounds();
                bool first = true;
                foreach (MeshRenderer renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Assert.AreEqual(1, renderer.sharedMaterials.Length);
                    if (shared == null) shared = renderer.sharedMaterial;
                    Assert.AreSame(shared, renderer.sharedMaterial, "All workplaces draw with one material");
                    Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    Assert.IsNotNull(mesh);
                    triangles += (int)(mesh.GetIndexCount(0) / 3);
                    Assert.IsFalse(mesh.isReadable, "No second copy of the mesh for scripts");
                    if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds);
                }
                Assert.Less(triangles, 3500, workplace.Key + " is over its triangle budget");
                // The model stands on the ground and fits the ground the game reserves for it.
                Assert.AreEqual(0f, bounds.min.y, 0.06f, workplace.Key + " does not stand on the ground");
                Assert.LessOrEqual(bounds.size.x, workplace.Size.x + 0.15f, workplace.Key + " is wider than its reserved ground");
                Assert.LessOrEqual(bounds.size.z, workplace.Size.y + 0.15f, workplace.Key + " is deeper than its reserved ground");
                Assert.AreEqual(workplace.Centre.x, bounds.center.x, 0.2f, workplace.Key + " is not where its reserved ground is");
                Assert.AreEqual(workplace.Centre.y, bounds.center.z, 0.2f);
            }
            Assert.IsNotNull(shared);
            Assert.AreEqual("Universal Render Pipeline/Lit", shared.shader.name);
            var palette = shared.GetTexture("_BaseMap") as Texture2D;
            Assert.IsNotNull(palette);
            Assert.AreEqual(FilterMode.Point, palette.filterMode, "Flat palette colours need exact pixels");
            Assert.AreEqual(1, palette.mipmapCount);
            Assert.IsTrue(shared.IsKeywordEnabled("_EMISSION"), "The fire glows");
            Assert.IsTrue(shared.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF"), "Wood and hide do not shine");
            StringAssert.StartsWith("Assets/PrivateArt/", AssetDatabase.GetAssetPath(shared), "Private art stays out of the repository");
        }
    }
}
