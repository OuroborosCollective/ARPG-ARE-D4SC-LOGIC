using System.Collections;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    /// <summary>
    /// Owner decision 2026-11-25: the side gate in Aschenwacht's east wall leads into the Brennerhof. The runtime shows the
    /// yard (the private kit where installed, a plain floor otherwise), stands the hero on its ground, breaks a vase into its
    /// shards with a breaking sound when the rules say so, and leads back out through the south gate.
    /// </summary>
    public sealed partial class EntryJourneyRuntimeTests
    {
        [UnityTest, Timeout(600000)]
        public IEnumerator TheBrennerhofOpensBesideAschenwachtAndItsVasesBreak()
        {
            _runtime.enabled = false;
            ArrangeCryptKill(); CollectCryptLoot(); MoveStory(0, 24000);
            typeof(ArenaRuntime).GetMethod("AdvanceCrypt", Flags).Invoke(_runtime, null);
            Assert.IsTrue(Game.InCourt, "the cleared crypt opens into Aschenwacht");
            yield return null;
            bool kit = Resources.Load<GameObject>("ARE/Visuals/EntryKilnWorld") != null;
            if (kit) Assert.IsNotNull(GameObject.Find("Aschenwacht_Seitentor"), "the side gate's arch stands in the east wall");
            Assert.IsNotNull(GameObject.Find("ARE_Kiln_Passage") ?? GameObject.Find("Aschenwacht_FallbackFloor"), "the side gate glows");

            Assert.IsTrue(WalkCourt(39375, KilnYard.SideGateZmm, 650, 400), "the side gate is reachable on foot");
            Assert.IsTrue(Game.AtKilnSideGate && Game.AtWorldPassage);
            typeof(ArenaRuntime).GetMethod("AdvanceCrypt", Flags).Invoke(_runtime, null);
            Assert.IsTrue(Game.InKilnYard, "the side gate leads into the Brennerhof");
            yield return null;
            Transform hero = Find("Wanderer").transform;
            Assert.AreEqual(1.05f + KilnLayout.HeightMm(Game.PlayerXmm, Game.PlayerZmm) / 1000f, hero.position.y, .06f, "the hero stands on the yard's ground");
            Assert.IsNotNull(GameObject.Find("AshenCrypt_KilnYard") ?? GameObject.Find("Brennerhof_FallbackFloor"), "the yard is shown");
            Assert.AreEqual(KilnLayout.ExitZmm / 1000f, Find("ARE_World_Passage").transform.position.z, .01f, "the way back waits at the south gate");
            StringAssert.Contains("Funde: 0/2", ArenaRuntime.ObjectiveText(Game));
            StringAssert.Contains("Brennerhof", Game.ChapterName);

            // The first vase of the Topfhof: the story action breaks it, the picture bursts into its shards with a sound.
            KilnVase vase = KilnYard.Vases[0];
            Assert.IsTrue(WalkKiln(vase.Xmm + 1100, vase.Zmm - 200, 300, 500), "the first vase is reachable");
            Assert.IsTrue(Game.NearKilnVase.HasValue && Game.NearKilnVase.Value.Id == 0);
            if (kit) Assert.IsTrue(Find("Brennerhof_Vase_0").activeSelf, "the vase stands whole");
            yield return StoryAction(HungerAction.BreakKilnVase);
            Assert.IsTrue(Game.Progression.KilnVaseBroken(0), "the rules know the vase is broken");
            _runtime.enabled = true; // the presentation follows the rules frame by frame
            yield return null; yield return null;
            if (kit)
            {
                Assert.IsFalse(Find("Brennerhof_Vase_0").activeSelf, "the whole vase is gone");
                var shards = Find("Brennerhof_Scherben_0").GetComponentsInChildren<MeshRenderer>();
                Assert.AreEqual(16, shards.Length, "sixteen shards fly");
                Assert.IsNotNull(GameObject.Find("Tonbruch_0"), "the vase is heard breaking");
                for (int i = 0; i < 180; i++) yield return null;
                foreach (var shard in shards)
                    Assert.GreaterOrEqual(shard.transform.position.y, KilnLayout.HeightMm((long)(shard.transform.position.x * 1000), (long)(shard.transform.position.z * 1000)) / 1000f - .05f,
                        "no shard falls through the ground");
            }
            _runtime.enabled = false;

            // Back through the south gate into the forecourt, beside the side gate.
            Assert.IsTrue(WalkKiln(KilnLayout.ExitXmm, KilnLayout.ExitZmm, 400, 600), "the south gate is reachable");
            typeof(ArenaRuntime).GetMethod("AdvanceCrypt", Flags).Invoke(_runtime, null);
            Assert.IsTrue(Game.InCourt && !Game.InKilnYard, "the south gate leads back into Aschenwacht");
            Assert.AreEqual(KilnYard.CourtReturnXmm, Game.PlayerXmm);
            Assert.IsTrue(Game.Progression.KilnVaseBroken(0), "the yard remembers its broken vase");
        }

        /// <summary>Walk the Brennerhof's own route with the stick, one Core tick at a time, as WalkCourt does in Aschenwacht.</summary>
        private bool WalkKiln(long x, long z, long stopMm, int limit)
        {
            long[] route = KilnLayout.Route(Game.PlayerXmm, Game.PlayerZmm, x, z);
            if (route.Length == 0) return false;
            int points = route.Length / 2, index = 0, stuck = 0;
            long lastX = Game.PlayerXmm, lastZ = Game.PlayerZmm;
            for (int tick = 0; tick < limit; tick++)
            {
                long ex = x - Game.PlayerXmm, ez = z - Game.PlayerZmm;
                if (ex * ex + ez * ez <= stopMm * stopMm) { Game.SetMoveInput(0, 0); return true; }
                while (index < points - 1)
                {
                    long px = route[2 * index] - Game.PlayerXmm, pz = route[2 * index + 1] - Game.PlayerZmm;
                    if (px * px + pz * pz >= 700L * 700L) break;
                    index++;
                }
                int aim = System.Math.Min(points - 1, index + (stuck > 2 ? 0 : 2));
                long dx = route[2 * aim] - Game.PlayerXmm, dz = route[2 * aim + 1] - Game.PlayerZmm;
                long scale = System.Math.Max(1, System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dz)));
                Game.SetMoveInput(dx * 10000 / scale, dz * 10000 / scale);
                Game.Step();
                stuck = Game.PlayerXmm == lastX && Game.PlayerZmm == lastZ ? stuck + 1 : 0;
                lastX = Game.PlayerXmm; lastZ = Game.PlayerZmm;
            }
            Game.SetMoveInput(0, 0);
            return false;
        }
    }
}
