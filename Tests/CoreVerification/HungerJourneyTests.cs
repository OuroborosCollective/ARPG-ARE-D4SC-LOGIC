using System;
using System.Numerics;
using AreArpg.Core;

internal static class HungerJourneyTests
{
    /// <summary>Exercise actual travel, resource transactions and reloads; NPC positioning is a labelled contact fixture.</summary>
    public static void Run(Action<bool, string> check)
    {
        foreach (long x in new long[] { -10500, -2500 })
            foreach (long z in new long[] { 2900, 7200 })
            {
                check(!ArenaSimulation.CanStandInHungerRegion(4, x, z), "mill-refuge-post-is-solid");
                check(ArenaSimulation.CanStandInHungerRegion(3, x, z), "mill-refuge-post-only-at-mill");
                check(!ArenaSimulation.CanStandInHungerRegion(4, x + 120 + ArenaSimulation.PlayerRadiusMm, z), "mill-refuge-post-includes-player-radius");
                check(ArenaSimulation.CanStandInHungerRegion(4, x + 121 + ArenaSimulation.PlayerRadiusMm, z), "mill-refuge-post-edge-is-bounded");
            }
        foreach (long z in new long[] { -24000, -2000, 2900, 5000, 7200, 24000 })
            check(ArenaSimulation.CanStandInHungerRegion(4, 0, z), "mill-refuge-central-path-stays-open");
        check(ArenaSimulation.CanStandInHungerRegion(4, -4000, -2000) && ArenaSimulation.CanStandInHungerRegion(4, -4000, 5000),
            "mill-refuge-existing-clues-stay-reachable");
        foreach (HungerAction decision in new[] { HungerAction.Reveal, HungerAction.Protect, HungerAction.Replace })
        {
            var w = Village();
            string untouched = w.Progression.SaveString();
            check(untouched.StartsWith("AC8|"), "hunger-untouched-ac8-stays-byte-compatible");
            check(!w.ActOnHunger(HungerAction.HearIlyan).Accepted && w.Progression.SaveString() == untouched, "hunger-no-remote-conversation");
            Contact(w, 0);
            check(w.ActOnHunger(HungerAction.HearIlyan).Accepted, "hunger-local-ilyan-clue");
            check(w.Progression.SaveString().StartsWith("AC9|"), "hunger-explicit-save-version");
            string learned = w.Progression.SaveString();
            check(!w.ActOnHunger(HungerAction.HearIlyan).Accepted && w.Progression.SaveString() == learned, "hunger-clue-idempotent");
            Move(w, 0, 24000);
            var road = w.TryTraversePassage();
            check(road != null && road.Progression.EntryAct == 3 && !road.AtAnyPassage, "hunger-road-without-immediate-bounce");
            check(w.Progression.EntryAct == 2, "hunger-old-checkpoint-immutable");
            check(!road.ActOnHunger(HungerAction.InspectTrail).Accepted, "hunger-clue-needs-proximity");
            Move(road, 0, 24000);
            check(road.TryTraversePassage() == null, "hunger-road-forward-gated-by-clue");
            Move(road, 0, -2000);
            check(road.ActOnHunger(HungerAction.InspectTrail).Accepted, "hunger-trail-found");
            Move(road, 0, 24000);
            var mill = road.TryTraversePassage();
            check(mill != null && mill.Progression.EntryAct == 4 && mill.Monsters.Count == 0 && mill.Npcs.Count == 0, "hunger-no-duplicate-crypt-or-npc-at-mill");
            check(mill.HungerShelterPatients == 3, "hunger-three-patients-on-first-visit");
            check(!mill.CanStand(10500, 5000) && mill.CanStand(0, 5000), "hunger-mill-footprint-core");
            Move(mill, 0, -2000); Move(mill, -4000, -2000);
            check(mill.ActOnHunger(HungerAction.InspectShelter).Accepted, "hunger-shelter-evidence");
            Move(mill, -4000, 5000);
            check(!mill.ActOnHunger(HungerAction.InspectBeds).Accepted, "hunger-missing-patients-after-supply-choice");
            Move(mill, 0, 5000); Move(mill, 0, -24000);
            road = mill.TryTraversePassage();
            check(road != null && road.Progression.EntryAct == 3 && road.PlayerZmm == 21000 && !road.AtAnyPassage, "hunger-return-arrival-outside-trigger");
            check(CharacterProgression.TryLoad(road.Progression.SaveString(), out var loaded) && loaded.EntryFromNorth &&
                loaded.SaveString() == road.Progression.SaveString(), "hunger-return-save-roundtrip");
            Move(road, 0, -24000); w = road.TryTraversePassage();
            check(w != null && w.Progression.EntryAct == 2 && w.Progression.HungerStage == 3, "hunger-return-to-real-village");
            Contact(w, decision == HungerAction.Protect ? 0 : 1);
            var before = w.Progression.Social.Provisions;
            int missing = SocialLedger.CampCapacity - w.Progression.Social.CampProvisions;
            string dorn = w.Progression.Social.GetMemory(2).Token();
            check(w.ActOnHunger(decision).Accepted, "hunger-choice-" + decision);
            check(w.Progression.Social.GetMemory(2).Token() == dorn, "hunger-no-telepathic-guard-" + decision);
            check(w.Progression.Social.Provisions == before - (decision == HungerAction.Replace ? missing : 0), "hunger-exact-supply-conservation-" + decision);
            string chosen = w.Progression.SaveString();
            foreach (var other in new[] { HungerAction.Reveal, HungerAction.Protect, HungerAction.Replace })
                check(!w.ActOnHunger(other).Accepted && w.Progression.SaveString() == chosen, "hunger-no-repeated-choice-" + other);
            check(CharacterProgression.TryLoad(chosen, out var choiceLoaded) && choiceLoaded.SaveString() == chosen, "hunger-choice-roundtrip");
            w = new ArenaSimulation(123, choiceLoaded, BigInteger.One, LootCatalog.Version1);
            var replay = new ArenaSimulation(123, Load(chosen), BigInteger.One, LootCatalog.Version1);
            for (int tick = 0; tick < 25; tick++)
            {
                w.SetMoveInput(0, 10000); replay.SetMoveInput(0, 10000); w.Step(); replay.Step();
                check(w.ComputeWorldHash() == replay.ComputeWorldHash(), "hunger-replay-" + decision + "-" + tick);
            }
            Move(w, 0, 24000); road = w.TryTraversePassage();
            Move(road, 0, 24000); mill = road.TryTraversePassage();
            Move(mill, 0, 5000); Move(mill, -4000, 5000);
            check(mill.HungerShelterPatients == 2, "hunger-one-missing-on-return-" + decision);
            check(mill.ActOnHunger(HungerAction.InspectBeds).Accepted && mill.Progression.HungerStage == 4, "hunger-missing-patients-discovered-" + decision);
            check(CharacterProgression.TryLoad(mill.Progression.SaveString(), out _), "hunger-final-checkpoint-loadable");
            Move(mill, 0, 5000); Move(mill, 0, 24000);
            check(mill.TryTraversePassage()?.Progression.EntryAct == 5, "hunger-missing-patients-open-store-passage");
            foreach (string invalid in new[] { "1,5,0,4,1", "1,4,1,3,0", "1,4,0,1,0", "1,3,0,2,2", "1,2,0,4,0", "1,02,0,3,0", "2,2,0,3,0" })
            {
                var f = chosen.Split('|'); f[15] = invalid;
                check(!CharacterProgression.TryLoad(Sign(f), out _), "hunger-reject-invalid-state-" + invalid);
            }
        }
        var p = Village(); Contact(p, 0); p.ActOnHunger(HungerAction.HearIlyan);
        p.Progression.DiscoverHungerClue(); p.Progression.DiscoverHungerClue(); // Explicit late-quest fixture.
        Contact(p, 1);
        while (p.Progression.Social.Provisions > 0) { p.Progression.Social.GetMemory(0).Hunger = 800; p.Progression.Social.Help(0); }
        string empty = p.Progression.SaveString();
        check(!p.ActOnHunger(HungerAction.Replace).Accepted && empty == p.Progression.SaveString(), "hunger-insufficient-rations-atomic");
    }

    /// <summary>Establish a village through the real kill, pickup and entry transition, using explicit enemy fixtures.</summary>
    private static ArenaSimulation Village()
    {
        var w = new ArenaSimulation(123, CharacterProgression.CreateEntryProfile(), BigInteger.One, LootCatalog.Version1);
        foreach (var enemy in w.Monsters) { enemy.Xmm = 1000; enemy.Zmm = 0; enemy.Health = 1; }
        w.ActivateSkill(3);
        for (int i = 0; i < 3; i++) w.TryCollectLoot();
        return w.TryAdvanceCrypt();
    }

    /// <summary>Arrange only an NPC contact; actions, distance validation and resources use actual production code.</summary>
    private static void Contact(ArenaSimulation w, int id)
    { var npc = w.Npcs.Find(n => n.Id == id); npc.Xmm = w.PlayerXmm + 1000; npc.Zmm = w.PlayerZmm; npc.InteractionCooldownTicks = 0; }

    /// <summary>Travel via actual fixed-tick movement rather than teleporting the player.</summary>
    private static void Move(ArenaSimulation w, long x, long z)
    {
        for (int tick = 0; tick < 150; tick++)
        {
            long dx = x - w.PlayerXmm, dz = z - w.PlayerZmm;
            if (Math.Abs(dx) < 600 && Math.Abs(dz) < 600) { w.SetMoveInput(0, 0); return; }
            w.SetMoveInput(Math.Abs(dx) < 500 ? 0 : Math.Sign(dx) * 10000, Math.Abs(dz) < 500 ? 0 : Math.Sign(dz) * 10000); w.Step();
        }
        throw new InvalidOperationException("Movement did not reach " + x + "," + z);
    }

    /// <summary>Independently reload a serialized progression for replay comparisons.</summary>
    private static CharacterProgression Load(string save)
    { if (!CharacterProgression.TryLoad(save, out var p)) throw new InvalidOperationException("Invalid test checkpoint"); return p; }

    /// <summary>Re-sign malformed semantic fixtures to exercise more than checksum detection.</summary>
    private static string Sign(string[] fields)
    {
        string payload = string.Join("|", fields, 0, fields.Length - 1); ulong hash = 14695981039346656037UL;
        unchecked { foreach (byte value in System.Text.Encoding.UTF8.GetBytes(payload)) { hash ^= value; hash *= 1099511628211UL; } }
        return payload + "|" + hash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
    }
}
