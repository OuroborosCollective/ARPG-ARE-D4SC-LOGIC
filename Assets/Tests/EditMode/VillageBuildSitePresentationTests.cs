using System;
using System.Collections.Generic;
using System.Linq;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;

namespace AreArpg.EditMode.Tests
{
    /// <summary>
    /// The lots of the village houses as the game shows them, without a running game: every need has a pile, every
    /// asker a figure, the panel fits the screen, and - when the private model is installed - the lot model carries
    /// the piles and fits the ground of a house. Without the model that check is reported as ignored.
    /// </summary>
    public sealed class VillageBuildSitePresentationTests
    {
        static readonly VillagePlace[] Places = { VillagePlace.SickRoom, VillagePlace.Smithy, VillagePlace.Barn };

        [Test]
        public void EveryNeedOfEveryHouseHasItsPileAndEveryAskerAFigure()
        {
            ArenaRuntime.VillageWorkplace model = ArenaRuntime.VillageSiteModel;
            Assert.AreEqual(VillagePlaceLayout.PlaceCount, ArenaRuntime.SiteAskerKeys.Length);
            foreach (VillagePlace place in Places)
            {
                Assert.IsFalse(string.IsNullOrEmpty(ArenaRuntime.SiteAskerKeys[(int)place - 1]), place + " has a figure for its asker");
                for (int need = 0; need < VillageBuildRules.NeedCount; need++)
                {
                    VillageBuildRules.Need n = VillageBuildRules.NeedAt(place, need);
                    int pile = Array.IndexOf(model.Goods, n.Good);
                    Assert.GreaterOrEqual(pile, 0, "The lot has a pile for " + n.Good + " (" + place + ")");
                    Assert.GreaterOrEqual(model.Units[pile], n.Count, "The pile of " + n.Good + " can show everything " + place + " needs");
                }
            }
            // The model is as large as the ground of a house: the three houses stand on footprints of one size.
            foreach (VillagePlace place in Places)
            {
                EntryWorldLayout.Footprint f = default; bool found = false;
                for (int i = 0; i < EntryWorldLayout.BuildingCount; i++)
                    if (EntryWorldLayout.Building(i).Id == VillagePlaceLayout.House(place)) { f = EntryWorldLayout.Building(i); found = true; }
                Assert.IsTrue(found);
                Assert.AreEqual(model.Size.x, f.HalfWidth / 500f, 0.01f, place + " width");
                Assert.AreEqual(model.Size.y, f.HalfDepth / 500f, 0.01f, place + " depth");
            }
        }

        [Test]
        public void TheLotPanelFitsTheSmallestScreenAndItsButtonsDoNotOverlap()
        {
            foreach (Vector2 screen in new[] { new Vector2(960f, 540f), new Vector2(2400f, 1080f), new Vector2(1280f, 800f) })
            {
                var layout = new ArenaHudLayout(screen.x, screen.y, new Rect(0f, 0f, screen.x, screen.y));
                Rect panel = ArenaRuntime.VillageSitePanel(layout), close = ArenaRuntime.VillageSiteCloseRect(layout), text = ArenaRuntime.VillageSiteTextRect(layout);
                Assert.GreaterOrEqual(panel.xMin, 0f); Assert.LessOrEqual(panel.xMax, layout.Width);
                Assert.GreaterOrEqual(panel.yMin, 0f); Assert.LessOrEqual(panel.yMax, layout.Height, "The panel fits on " + screen);
                var buttons = new List<Rect> { close };
                for (int need = 0; need < VillageBuildRules.NeedCount; need++) buttons.Add(ArenaRuntime.VillageSiteGiftRect(layout, need));
                for (int a = 0; a < buttons.Count; a++)
                {
                    Assert.IsTrue(panel.Contains(buttons[a].min) && panel.Contains(buttons[a].max), "Button " + a + " lies on the panel");
                    Assert.GreaterOrEqual(buttons[a].height, 30f, "A finger can hit the button");
                    Assert.IsFalse(buttons[a].Overlaps(text), "Button " + a + " does not cover the words");
                    for (int b = a + 1; b < buttons.Count; b++) Assert.IsFalse(buttons[a].Overlaps(buttons[b]), "Buttons " + a + " and " + b + " overlap");
                }
                Assert.Greater(text.height, 150f, "There is room for the asker's words and the two needs");
                Assert.AreEqual(layout.ContextActionRect, ArenaRuntime.VillageSiteToggleRect(layout));
            }
        }

        [Test]
        public void PrivateLotModelCarriesThePilesAndFitsTheGroundOfAHouse()
        {
            ArenaRuntime.VillageWorkplace model = ArenaRuntime.VillageSiteModel;
            var prefab = Resources.Load<GameObject>("ARE/Visuals/" + model.Key);
            if (prefab == null) Assert.Ignore("Requires the private lot model with its built visual (never in the repository)");
            Transform[] parts = prefab.GetComponentsInChildren<Transform>(true);
            var expected = new HashSet<string> { prefab.name, "Base" };
            for (int g = 0; g < model.Goods.Length; g++)
            {
                string prefix = ArenaRuntime.StockPrefix + model.Goods[g] + "_";
                Assert.AreEqual(model.Units[g], parts.Count(p => p.name.StartsWith(prefix, StringComparison.Ordinal)), "Pieces of " + model.Goods[g]);
                foreach (Transform part in parts) if (part.name.StartsWith(prefix, StringComparison.Ordinal)) expected.Add(part.name);
            }
            foreach (Transform part in parts) Assert.IsTrue(expected.Contains(part.name), "The lot carries a part the game does not know: " + part.name);
            Assert.IsEmpty(prefab.GetComponentsInChildren<Collider>(true), "The lot must not bring physics");
            Assert.IsEmpty(prefab.GetComponentsInChildren<MonoBehaviour>(true), "The lot must not bring scripts");
            int triangles = 0; Bounds bounds = new Bounds(); bool first = true;
            // Put up as the game puts it up: with half a turn, the piles on the side the door will be on (north).
            Quaternion turn = Quaternion.LookRotation(Vector3.back);
            foreach (MeshRenderer renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                Assert.AreEqual(1, renderer.sharedMaterials.Length);
                Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                triangles += (int)(mesh.GetIndexCount(0) / 3);
                Assert.IsFalse(mesh.isReadable, "No second copy of the mesh for scripts");
                if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds);
                if (renderer.name.StartsWith(ArenaRuntime.StockPrefix, StringComparison.Ordinal))
                    Assert.Greater((turn * renderer.bounds.center).z, 1.2f, renderer.name + " lies near the side of the door");
            }
            Assert.Less(triangles, 3500, "The lot is over its triangle budget");
            Assert.AreEqual(0f, bounds.min.y, 0.06f, "The lot does not stand on the ground");
            Assert.LessOrEqual(bounds.size.x, model.Size.x + 0.15f, "The lot is wider than the ground of a house");
            Assert.LessOrEqual(bounds.size.z, model.Size.y + 0.15f, "The lot is deeper than the ground of a house");
            Assert.AreEqual(0f, bounds.center.x, 0.25f); Assert.AreEqual(0f, bounds.center.z, 0.25f);
        }
    }
}
