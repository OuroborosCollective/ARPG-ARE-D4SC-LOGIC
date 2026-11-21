using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    /// <summary>
    /// The village economy seen in the running game. The Core owns every stock and every person's whereabouts;
    /// workplaces, sacks, fire and figures only show them. These tests look at both sides together: what is shown
    /// equals what the Core holds, and touching what is shown never changes the Core.
    /// </summary>
    public sealed partial class EntryJourneyRuntimeTests
    {
        private VillageEconomyState Economy { get { return Game.Progression.Economy; } }

        private void UpdateVillageWork(float seconds)
        { typeof(ArenaRuntime).GetMethod("UpdateVillageEconomy", Flags).Invoke(_runtime, new object[] { seconds }); }

        /// <summary>Two residents move in (two finished main quests) and the Core starts the village's work.</summary>
        private void StartVillageWork()
        {
            EnterVillageAfterScene();
            Game.CompleteMainQuest(); Game.CompleteMainQuest();
            // The Core looks at the village every tenth tick: by then both residents are counted.
            for (int tick = 0; tick < 12 && Economy.Residents.Count < 2; tick++) Game.Step();
            Assert.IsTrue(Economy.Started, "The economy starts with the first resident");
            Assert.AreEqual(2, Economy.Residents.Count);
        }

        private static List<Transform> Units(GameObject host, VillageGood good)
        {
            string prefix = ArenaRuntime.StockPrefix + good + "_";
            var units = host.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith(prefix, StringComparison.Ordinal)).ToList();
            units.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return units;
        }

        /// <summary>Every workplace shows exactly the Core's stock, filled in order and capped by its room.</summary>
        private void AssertShownStockIsTheCoresStock(string when)
        {
            foreach (ArenaRuntime.VillageWorkplace workplace in ArenaRuntime.VillageWorkplaces)
            {
                GameObject host = Find(ArenaRuntime.WorkplacePrefix + workplace.Spot);
                Assert.IsNotNull(host, workplace.Key + " is missing " + when);
                for (int g = 0; g < workplace.Goods.Length; g++)
                {
                    List<Transform> units = Units(host, workplace.Goods[g]);
                    Assert.AreEqual(workplace.Units[g], units.Count, workplace.Key + " room for " + workplace.Goods[g]);
                    int expected = Mathf.Min(Economy.Commons(workplace.Goods[g]), units.Count);
                    for (int i = 0; i < units.Count; i++)
                        Assert.AreEqual(i < expected, units[i].gameObject.activeSelf, workplace.Goods[g] + " unit " + i + " " + when + " (Core has " + Economy.Commons(workplace.Goods[g]) + ")");
                }
            }
            bool cooking = Economy.Residents.Any(r => !r.Walking && r.Site == VillageSite.Village && r.Activity == VillageActivity.Working &&
                VillageEconomyLayout.SpotFor(r.Work) == VillageSpot.Kitchen);
            Assert.AreEqual(cooking, Find(ArenaRuntime.FireName).activeSelf, "The fire burns exactly while somebody cooks " + when);
        }

        private GameObject Figure(VillageResidentState resident) { return Find(ArenaRuntime.ResidentPrefix + resident.Ordinal); }

        private void WalkToBoard()
        {
            long x = VillageEconomyLayout.StandXmm(VillageSpot.Board), z = VillageEconomyLayout.StandZmm(VillageSpot.Board);
            for (int tick = 0; tick < 600 && !Game.VillageBoardInReach; tick++)
            {
                long dx = x - Game.PlayerXmm, dz = z - Game.PlayerZmm, reach = Math.Max(Math.Abs(dx), Math.Abs(dz));
                if (reach < 300) break;
                Game.SetMoveInput(dx * 10000 / reach, dz * 10000 / reach);
                Game.Step();
            }
            Game.SetMoveInput(0, 0);
            Assert.IsTrue(Game.VillageBoardInReach, "The player reaches the village board at " + Game.PlayerXmm + "," + Game.PlayerZmm);
        }

        [UnityTest]
        public IEnumerator FirstVillageVisitShowsTheActualStoreAndItsUnstaffedWork()
        {
            EnterVillageAfterScene();
            yield return RunFrames(1);
            Assert.IsNotNull(Find(ArenaRuntime.WorkplacePrefix + VillageSpot.Board), "The first visitor can find the board");
            Assert.IsNull(Find(ArenaRuntime.WorkplacePrefix + VillageSpot.Workbench), "Workshops appear with their first worker");
            WalkToBoard();
            string board = (string)typeof(ArenaRuntime).GetMethod("VillageBoardText", Flags).Invoke(_runtime, null);
            StringAssert.Contains("8/10", board);
            StringAssert.Contains("Arbeitskräfte", board);
            Game.Progression.Crafting.AddMaterial(MaterialKind.Holz, 1);
            Assert.IsFalse(Game.GiveToVillage(VillageGood.Wood, 1).Accepted);
            Assert.AreEqual(1, Game.Progression.Crafting.MaterialCount(MaterialKind.Holz));
            Assert.IsFalse(Economy.Started);
        }

        [UnityTest]
        public IEnumerator ScrollingTheBoardReachesTheLastLineWithoutDonating()
        {
            StartVillageWork();
            for (int i = 0; i < 16; i++) Game.CompleteMainQuest();
            for (int i = 0; i < VillageEconomyRules.Stride; i++) Game.Step();
            Game.Progression.Crafting.AddMaterial(MaterialKind.Holz, 1);
            yield return RunFrames(1); // Complete the actual region change and its scenery rebuild before opening the board.
            Assert.IsNotNull(Find(ArenaRuntime.WorkplacePrefix + VillageSpot.Board));
            WalkToBoard();
            var layout = new ArenaHudLayout(Screen.width, Screen.height, Screen.safeArea);
            var input = typeof(ArenaRuntime).GetMethod("VillageBoardTouch", Flags);
            void TouchBoard(TouchPhase phase, Vector2 point)
            { input.Invoke(_runtime, new object[] { layout, 7, phase, point }); }
            TouchBoard(TouchPhase.Began, ArenaRuntime.VillageBoardToggleRect(layout).center);
            TouchBoard(TouchPhase.Ended, ArenaRuntime.VillageBoardToggleRect(layout).center);
            yield return RunFrames(2); // The real IMGUI layout measures the wrapped text and the available viewport.
            Assert.IsTrue((bool)typeof(ArenaRuntime).GetField("_villageBoardOpen", Flags).GetValue(_runtime), "The board remains open in the current village");
            float limit = (float)typeof(ArenaRuntime).GetField("_villageScrollLimit", Flags).GetValue(_runtime);
            Assert.Greater(limit, 0f, "The complete village list actually overflows this viewport");
            Rect viewport = ArenaRuntime.VillageBoardTextRect(layout, 1);
            int gifts = Game.Progression.Crafting.MaterialCount(MaterialKind.Holz);
            ulong hash = Game.ComputeWorldHash();
            TouchBoard(TouchPhase.Began, viewport.center);
            TouchBoard(TouchPhase.Moved, viewport.center - Vector2.up * (limit + 100f));
            Assert.AreEqual(limit, (float)typeof(ArenaRuntime).GetField("_villageScrollY", Flags).GetValue(_runtime));
            TouchBoard(TouchPhase.Ended, ArenaRuntime.VillageGiftRect(layout, 0).center);
            typeof(ArenaRuntime).GetMethod("ProcessVillageGift", Flags).Invoke(_runtime, null);
            Assert.AreEqual(gifts, Game.Progression.Crafting.MaterialCount(MaterialKind.Holz));
            Assert.AreEqual(hash, Game.ComputeWorldHash(), "A reading gesture never becomes a hand-over");
        }

        [UnityTest]
        public IEnumerator VillageWorkShowsTheCoresStockAndNeverChangesIt()
        {
            EnterVillageAfterScene();
            yield return RunFrames(1);
            Assert.IsNotNull(Find(ArenaRuntime.WorkplacePrefix + VillageSpot.Board));
            Assert.IsNull(Find(ArenaRuntime.WorkplacePrefix + VillageSpot.Woodyard), "Production waits for a resident");

            Game.CompleteMainQuest(); Game.CompleteMainQuest();
            for (int tick = 0; tick < 12 && Economy.Residents.Count < 2; tick++) Game.Step();
            Assert.IsTrue(Economy.Started, "The economy starts with the first resident");
            Assert.AreEqual(2, Economy.Residents.Count, "Both residents are counted");
            yield return RunFrames(1);
            Assert.IsNotNull(Find(ArenaRuntime.VillageWorkRoot), "The workplaces appear with the first resident");
            AssertShownStockIsTheCoresStock("at the start");
            // The stated start stock is what lies there: wood on the pile, metal on the bench, water at the kitchen.
            Assert.AreEqual(Mathf.Min(Economy.Commons(VillageGood.Wood), 12), Units(Find(ArenaRuntime.WorkplacePrefix + VillageSpot.Woodyard), VillageGood.Wood).Count(u => u.gameObject.activeSelf));
            Assert.Greater(Economy.Commons(VillageGood.Wood), 0);
            Assert.AreEqual(2, Units(Find(ArenaRuntime.WorkplacePrefix + VillageSpot.Workbench), VillageGood.Metal).Count(u => u.gameObject.activeSelf));
            foreach (VillageResidentState resident in Economy.Residents) Assert.IsNotNull(Figure(resident), "Every resident has a figure");

            // 1. What is shown is not a stock: show everything, hide everybody, light the fire - the Core stays as it is.
            ulong token = Economy.StableHash; ulong world = Game.ComputeWorldHash(); string save = Game.Progression.SaveString();
            foreach (ArenaRuntime.VillageWorkplace workplace in ArenaRuntime.VillageWorkplaces)
                foreach (VillageGood good in workplace.Goods)
                    foreach (Transform unit in Units(Find(ArenaRuntime.WorkplacePrefix + workplace.Spot), good)) unit.gameObject.SetActive(true);
            Find(ArenaRuntime.FireName).SetActive(true);
            foreach (VillageResidentState resident in Economy.Residents) Figure(resident).SetActive(false);
            UnityEngine.Object.DestroyImmediate(Units(Find(ArenaRuntime.WorkplacePrefix + VillageSpot.GameRack), VillageGood.Game)[3].gameObject);
            Assert.AreEqual(token, Economy.StableHash, "Shown sacks, logs and fire are not stock");
            Assert.AreEqual(world, Game.ComputeWorldHash());
            Assert.AreEqual(save, Game.Progression.SaveString(), "Nothing shown reaches the save");
            // The next picture is the Core's truth again, and drawing it changes nothing either.
            UpdateVillageWork(0f); UpdateVillageWork(30f); UpdateVillageWork(600f);
            Assert.AreEqual(token, Economy.StableHash, "Drawing the village, however long, moves no stock");
            Assert.AreEqual(world, Game.ComputeWorldHash());
            Assert.AreEqual(save, Game.Progression.SaveString());
            foreach (ArenaRuntime.VillageWorkplace workplace in ArenaRuntime.VillageWorkplaces)
            {
                GameObject host = Find(ArenaRuntime.WorkplacePrefix + workplace.Spot);
                for (int g = 0; g < workplace.Goods.Length; g++)
                {
                    List<Transform> units = Units(host, workplace.Goods[g]);
                    int expected = Mathf.Min(Economy.Commons(workplace.Goods[g]), units.Count);
                    for (int i = 0; i < units.Count; i++) Assert.AreEqual(i < expected, units[i].gameObject.activeSelf, "The picture follows the Core again: " + workplace.Goods[g] + " " + i);
                }
            }

            // 2. The other way round it works: the Core changes, the picture follows.
            Game.Progression.Crafting.AddMaterial(MaterialKind.Holz, 3);
            WalkToBoard();
            int wood = Economy.Commons(VillageGood.Wood);
            var layout = new ArenaHudLayout(Screen.width, Screen.height, Screen.safeArea);
            var touch = typeof(ArenaRuntime).GetMethod("VillageBoardTouch", Flags);
            touch.Invoke(_runtime, new object[] {layout, 2, TouchPhase.Began, ArenaRuntime.VillageBoardToggleRect(layout).center});
            touch.Invoke(_runtime, new object[] {layout, 2, TouchPhase.Ended, ArenaRuntime.VillageBoardToggleRect(layout).center});
            touch.Invoke(_runtime, new object[] {layout, 2, TouchPhase.Began, ArenaRuntime.VillageGiftRect(layout, 0).center});
            yield return RunFrames(3);
            Assert.GreaterOrEqual(Economy.Commons(VillageGood.Wood), wood + 1, "The hand-over at the board reached the commons");
            Assert.AreEqual(1, Economy.Received(VillageGood.Wood), "Exactly one unit was handed over");
            Assert.AreEqual(2, Game.Progression.Crafting.MaterialCount(MaterialKind.Holz), "The pack lost exactly that unit");
            StringAssert.Contains("Dorflager", Message());
            Assert.IsTrue(CharacterProgression.TryLoad(PlayerPrefs.GetString(_keys[0]), NativeJourneyMaps, out var saved));
            Assert.IsTrue(saved.Economy.Started && saved.Economy.Received(VillageGood.Wood) == 1, "An accepted hand-over is saved with the village's ledger");
            UpdateVillageWork(0f);
            Assert.AreEqual(Mathf.Min(Economy.Commons(VillageGood.Wood), 12), Units(Find(ArenaRuntime.WorkplacePrefix + VillageSpot.Woodyard), VillageGood.Wood).Count(u => u.gameObject.activeSelf),
                "The pile shows the wood the Core has now");
            Assert.AreEqual(0, Economy.Audit().Count, "The ledger still balances");

            // 3. A workplace without its private model still shows the stock: one plain cube per unit.
            var plain = new ArenaRuntime.VillageWorkplace(VillageSpot.Woodyard, "DefinitelyMissingWorkplace", 0f, 0f, 2f, 1f, 1f, false, (int)VillageGood.Wood, 12);
            int hosts = Find(ArenaRuntime.VillageWorkRoot).transform.childCount;
            typeof(ArenaRuntime).GetMethod("BuildWorkplace", Flags).Invoke(_runtime, new object[] { plain });
            UpdateVillageWork(0f);
            Transform fallback = Find(ArenaRuntime.VillageWorkRoot).transform.GetChild(hosts);
            Assert.IsNotNull(fallback.Find("Platzhalter_DefinitelyMissingWorkplace"), "A missing model leaves a plain block");
            List<Transform> cubes = Units(fallback.gameObject, VillageGood.Wood);
            Assert.AreEqual(12, cubes.Count);
            Assert.AreEqual(Mathf.Min(Economy.Commons(VillageGood.Wood), 12), cubes.Count(c => c.gameObject.activeSelf), "A missing model cannot hide real stock");
        }

        /// <summary>
        /// Everything else that stands in the village, as a disc on the ground: props, plants, crates, posts. Ground,
        /// paving and other flat or very large pieces are no obstacle, and neither are people, who move.
        /// </summary>
        private List<KeyValuePair<string, Vector3>> StandingThings()
        {
            var things = new List<KeyValuePair<string, Vector3>>();
            Transform work = Find(ArenaRuntime.VillageWorkRoot).transform, people = Find(ArenaRuntime.VillageResidentsRoot).transform;
            foreach (GameObject root in _scene.GetRootGameObjects())
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(false))
                {
                    if (!renderer.enabled || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
                    Transform t = renderer.transform;
                    if (t.IsChildOf(work) || t.IsChildOf(people)) continue;
                    bool person = false;
                    for (Transform up = t; up != null && !person; up = up.parent) person = up.name == "Wanderer" || up.name.StartsWith("ARE_NPC_", StringComparison.Ordinal);
                    if (person) continue;
                    Bounds b = renderer.bounds;
                    if (b.size.x > 12f || b.size.z > 12f || b.max.y < 0.3f || b.min.y > 2.5f) continue;
                    // x, z: centre on the ground; y: the radius of the disc.
                    things.Add(new KeyValuePair<string, Vector3>(renderer.name + " in " + root.name, new Vector3(b.center.x, Mathf.Min(b.extents.x, b.extents.z), b.center.z)));
                }
            return things;
        }

        /// <summary>The workplaces were put where the village has room: nothing else stands in them or where their worker stands.</summary>
        [UnityTest]
        public IEnumerator WorkplacesStandOnFreeGround()
        {
            StartVillageWork();
            yield return RunFrames(1);
            List<KeyValuePair<string, Vector3>> things = StandingThings();
            Assert.Greater(things.Count, 5, "The village's own scenery was looked at");
            var clashes = new List<string>();
            foreach (ArenaRuntime.VillageWorkplace workplace in ArenaRuntime.VillageWorkplaces)
            {
                Transform host = Find(ArenaRuntime.WorkplacePrefix + workplace.Spot).transform;
                var stand = new Vector2(VillageEconomyLayout.StandXmm(workplace.Spot), VillageEconomyLayout.StandZmm(workplace.Spot)) / 1000f;
                foreach (KeyValuePair<string, Vector3> thing in things)
                {
                    float radius = thing.Value.y * 0.8f;
                    Vector3 local = host.InverseTransformPoint(new Vector3(thing.Value.x, host.position.y, thing.Value.z));
                    float dx = Mathf.Max(0f, Mathf.Abs(local.x - workplace.Centre.x) - workplace.Size.x * 0.5f), dz = Mathf.Max(0f, Mathf.Abs(local.z - workplace.Centre.y) - workplace.Size.y * 0.5f);
                    if (Mathf.Sqrt(dx * dx + dz * dz) < radius)
                        clashes.Add(workplace.Key + " stands in " + thing.Key + " at " + thing.Value.x.ToString("F1") + "," + thing.Value.z.ToString("F1") + " r=" + thing.Value.y.ToString("F1"));
                    if ((new Vector2(thing.Value.x, thing.Value.z) - stand).magnitude < radius + 0.35f)
                        clashes.Add("The worker at " + workplace.Key + " stands in " + thing.Key + " at " + thing.Value.x.ToString("F1") + "," + thing.Value.z.ToString("F1") + " r=" + thing.Value.y.ToString("F1"));
                }
            }
            string survey = Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR");
            if (!string.IsNullOrEmpty(survey))
                System.IO.File.WriteAllLines(System.IO.Path.Combine(survey, "village-standing-things.tsv"),
                    things.Select(t => t.Value.x.ToString("F2") + "\t" + t.Value.z.ToString("F2") + "\t" + t.Value.y.ToString("F2") + "\t" + t.Key));
            Assert.IsEmpty(clashes, string.Join("\n", clashes));
        }

        [UnityTest]
        public IEnumerator ResidentsAreSeenWhereTheCoreHasThemAndHiddenFiguresChangeNothing()
        {
            StartVillageWork();
            yield return RunFrames(1);
            // Somebody leaves for the forest. The Core runs alone; nothing is drawn meanwhile.
            VillageResidentState away = null;
            for (int tick = 0; tick < 4000 && away == null; tick++)
            {
                Game.Step();
                away = Economy.Residents.FirstOrDefault(r => !r.Walking && r.Site == VillageSite.Forest);
            }
            Assert.IsNotNull(away, "A resident reaches the forest");
            Assert.IsFalse(VillageEconomyLayout.Seen(away, out _));
            ulong token = Economy.StableHash; ulong world = Game.ComputeWorldHash();
            UpdateVillageWork(600f); UpdateVillageWork(600f);
            Assert.IsFalse(Figure(away).activeSelf, "A resident in the forest is not standing in the village");
            Assert.AreEqual(token, Economy.StableHash, "A figure that leaves the picture takes nothing with it");
            Assert.AreEqual(world, Game.ComputeWorldHash());
            // Showing the figure by hand does not bring the resident home.
            Figure(away).SetActive(true);
            Figure(away).transform.position = new Vector3(8.2f, 1f, -3.8f);
            Assert.AreEqual(token, Economy.StableHash);
            Assert.AreEqual(VillageSite.Forest, away.Site);
            Figure(away).SetActive(false);

            // The same resident comes home: seen again from the gate on, then at the place of the next step.
            bool atGate = false;
            for (int tick = 0; tick < 12000 && !(away.Site == VillageSite.Village && !away.Walking && VillageEconomyLayout.Seen(away, out _)); tick++)
            {
                Game.Step();
                if (!atGate && away.Walking && away.Destination == VillageSite.Village && VillageEconomyLayout.Seen(away, out _))
                {
                    UpdateVillageWork(0f);
                    Assert.IsTrue(Figure(away).activeSelf, "Coming home, the figure is seen again");
                    Assert.Less(Vector3.Distance(Figure(away).transform.position, new Vector3(0f, Figure(away).transform.position.y, VillageEconomyLayout.StandZmm(VillageSpot.Gate) / 1000f)), 2.5f,
                        "The figure comes back in through the north gate");
                    atGate = true;
                }
            }
            Assert.IsTrue(atGate, "The way home was seen");
            Assert.IsTrue(VillageEconomyLayout.Seen(away, out VillageSpot spot), "Home again, the resident is seen at a place: " + VillageBoard.ResidentLine(away));
            token = Economy.StableHash;
            UpdateVillageWork(600f);
            Assert.IsTrue(Figure(away).activeSelf);
            var stand = new Vector3(VillageEconomyLayout.StandXmm(spot) / 1000f, Figure(away).transform.position.y, VillageEconomyLayout.StandZmm(spot) / 1000f);
            Assert.Less(Vector3.Distance(Figure(away).transform.position, stand), 2.8f, "The figure stands at the place the Core names (" + spot + "), beside others at most");
            Assert.AreEqual(token, Economy.StableHash);

            // The board says what the Core says, line by line.
            string board = (string)typeof(ArenaRuntime).GetMethod("VillageBoardText", Flags).Invoke(_runtime, null);
            foreach (string line in VillageBoard.StockLines(Game.Progression)) StringAssert.Contains(line, board);
            foreach (VillageResidentState resident in Economy.Residents) StringAssert.Contains(VillageBoard.ResidentNotice(resident), board);
            Assert.AreEqual(token, Economy.StableHash, "Reading the board changes nothing");
        }

        /// <summary>
        /// Pictures for a look at the village at work, taken only on request (ARE_ENTRY_EVIDENCE_DIR). The Core runs
        /// for a good day first, so what is seen is real stock, not a staged scene.
        /// </summary>
        [UnityTest]
        public IEnumerator VillageEconomyPicturesOnRequest()
        {
            string directory = Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR");
            if (string.IsNullOrEmpty(directory)) Assert.Ignore("Pictures are taken only when an evidence folder is named");
            StartVillageWork();
            // Nearly two days pass: people eat from Mara's store, so the village hunts and cooks as well as cutting
            // wood. The pictures are taken around noon (the sky clock starts a day at noon).
            int ticks = 0;
            for (; ticks < 45000; ticks++)
            {
                Game.Step();
                ulong phase = WorldSkyClock.CurrentPhaseTicks(Game);
                bool day = phase >= 13000 || phase <= 1500;
                if (ticks > 21000 && day && Economy.Residents.Any(r => !r.Walking && r.Site == VillageSite.Village && r.Activity == VillageActivity.Working)) break;
            }
            yield return RunFrames(2);
            UpdateVillageWork(600f);
            Debug.Log("VILLAGE_PICTURES after " + ticks + " ticks: " + string.Join(" | ", VillageBoard.StockLines(Game.Progression)) + " || " +
                string.Join(" ", Economy.Residents.Select(VillageBoard.ResidentLine)));
            yield return Capture("village-economy.png", overridePosition: new Vector3(0f, 17f, -22f), overrideFocus: new Vector3(0f, 0f, -4f));
            foreach (ArenaRuntime.VillageWorkplace workplace in ArenaRuntime.VillageWorkplaces)
            {
                Transform host = Find(ArenaRuntime.WorkplacePrefix + workplace.Spot).transform;
                Vector3 front = host.forward, focus = host.TransformPoint(new Vector3(workplace.Centre.x, 0.7f, workplace.Centre.y));
                yield return Capture("work-" + workplace.Spot.ToString().ToLowerInvariant() + ".png",
                    overridePosition: focus + front * 4.6f + host.right * 1.6f + Vector3.up * 3.0f, overrideFocus: focus);
            }
            // The board as the player reads it: walk there, open it, and take the screen as shown.
            WalkToBoard();
            yield return RunFrames(2);
            yield return Capture("board-reach.png");
            var layout = new ArenaHudLayout(Screen.width, Screen.height, Screen.safeArea);
            typeof(ArenaRuntime).GetMethod("VillageBoardTouch", Flags).Invoke(_runtime,
                new object[] {layout, 1, TouchPhase.Began, ArenaRuntime.VillageBoardToggleRect(layout).center});
            if (Application.isBatchMode)
            {
                // A batch player has a renderable scene camera but no presented game-view framebuffer with IMGUI.
                // Keep the actual panel contents and the actual scene-camera image separately.
                var board = Find(ArenaRuntime.WorkplacePrefix + VillageSpot.Board).transform;
                Vector3 focus = board.position + Vector3.up;
                yield return Capture("board-workplace.png", overridePosition: focus + board.forward * 4f + Vector3.up, overrideFocus: focus);
                string text = (string)typeof(ArenaRuntime).GetMethod("VillageBoardText", Flags).Invoke(_runtime, null);
                System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "board-readback.txt"), text);
                yield break;
            }
            _runtime.enabled = true;
            for (int i = 0; i < 6; i++) yield return null;
            yield return new WaitForEndOfFrame();
            Texture2D screen = ScreenCapture.CaptureScreenshotAsTexture();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, "board-panel.png"), screen.EncodeToPNG());
            UnityEngine.Object.Destroy(screen);
            _runtime.enabled = false;
        }
    }
}
