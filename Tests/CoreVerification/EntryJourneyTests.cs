using System;
using System.Numerics;
using AreArpg.Core;

internal static class EntryJourneyTests
{
    /// <summary>Opt-in entry checkpoints preserve legacy profiles and bind both acts to one Core.</summary>
    public static void Run(Action<bool, string> check)
    {
        var legacy = new CharacterProgression();
        string oldSave = legacy.SaveString();
        var profile = CharacterProgression.CreateEntryProfile();
        check(profile.EntryAct == 1 && profile.SaveString().StartsWith("AC8|"), "entry-explicit-new-profile");
        check(legacy.EntryAct == 0 && legacy.SaveString() == oldSave, "entry-legacy-save-byte-stable");
        check(CharacterProgression.TryLoad(profile.SaveString(), out var restored) && restored.SaveString() == profile.SaveString(), "entry-empty-receipts-save-roundtrip");
        var a = new ArenaSimulation(123, profile, BigInteger.One, LootCatalog.Version1);
        var b = new ArenaSimulation(123, restored, BigInteger.One, LootCatalog.Version1);
        check(a.Npcs.Count == 0 && a.Monsters.Count == 3 && !a.IsVillage, "entry-crypt-before-village");
        check(a.TryAdvanceCrypt() == null, "entry-gate-rejects-uncleared-crypt");
        // Explicit combat fixtures exercise accepted damage/loot paths; they are not visual runtime evidence.
        foreach (var world in new[] { a, b })
        {
            foreach (var enemy in world.Monsters) { enemy.Xmm = 1000; enemy.Zmm = 0; enemy.Health = 1; }
            world.ActivateSkill(3);
        }
        check(a.CryptCleared && a.GroundLoot.Count == 3 && a.TryAdvanceCrypt() == null, "entry-exit-requires-collected-loot");
        check(a.TryCollectLoot() && b.TryCollectLoot(), "entry-collects-real-affixed-loot");
        for (int item = 1; item < 3; item++)
            check(a.TryCollectLoot() && b.TryCollectLoot(), "entry-collects-remaining-item-" + item);
        check(a.ComputeWorldHash() == b.ComputeWorldHash(), "entry-crypt-deterministic-hash");
        var village = a.TryAdvanceCrypt();
        var replay = b.TryAdvanceCrypt();
        check(village != null && village.IsVillage && village.Progression.EntryAct == 2, "entry-canonical-act-transition");
        check(a.Progression.EntryAct == 1, "entry-transition-does-not-mutate-old-checkpoint");
        check(village.Monsters.Count == 0 && village.Npcs.Count == 3 && village.CryptDepth == 1, "entry-existing-social-owner-in-village");
        check(village.PlayerZmm == -23000 && village.TryAdvanceCrypt() == null, "entry-arrival-road-and-agreed-stop");
        check(village.Progression.Items.Count == 3 && village.Progression.LifetimeKills == 3, "entry-keeps-loot-xp-and-kills");
        check(CharacterProgression.TryLoad(village.Progression.SaveString(), out var checkpoint) && checkpoint.EntryAct == 2 && checkpoint.StableHash == village.Progression.StableHash, "entry-village-save-reload");
        var restart = new ArenaSimulation(123, checkpoint, checkpoint.UnlockedCryptDepth, LootCatalog.Version1);
        check(restart.Monsters.Count == 0 && restart.PlayerZmm == -23000, "entry-load-does-not-respawn-crypt");
        for (int tick = 0; tick < 50; tick++)
        {
            village.SetMoveInput(0, 10000); replay.SetMoveInput(0, 10000);
            village.Step(); replay.Step();
            check(village.ComputeWorldHash() == replay.ComputeWorldHash(), "entry-village-replay-" + tick);
        }
        // Move through real Core input to the supplier and retain the established donation transaction.
        for (int tick = 0; tick < 80; tick++)
        {
            var mara = village.Npcs[1];
            long dx = mara.Xmm - village.PlayerXmm, dz = mara.Zmm - village.PlayerZmm;
            village.SetMoveInput(Math.Sign(dx) * (Math.Abs(dx) > 500 ? 10000 : 0), Math.Sign(dz) * (Math.Abs(dz) > 500 ? 10000 : 0));
            village.Step();
            if (village.NearestNpc()?.Id == 1) break;
        }
        int stock = village.Progression.Social.CampProvisions;
        check(village.DonateCampRation().Accepted && village.Progression.Social.CampProvisions == stock + 1, "entry-real-supply-choice");
        check(!village.TakeCampRation().Accepted, "entry-theft-shares-interaction-cooldown");
        village.SetMoveInput(0, 0);
        for (int i = 0; i < 10; i++) village.Step();
        var provisions = village.Progression.Social.Provisions;
        var guardMemory = village.Progression.Social.GetMemory(2);
        var observed = guardMemory.HostileActions;
        check(village.TakeCampRation().Accepted, "entry-real-food-theft-accepted");
        check(village.Progression.Social.CampProvisions == stock && village.Progression.Social.Provisions == provisions + 1, "entry-food-transfer-conserves-rations");
        check(guardMemory.HostileActions == observed + 1, "entry-nearby-conscious-guard-witnesses-theft");
        check(!village.TakeCampRation().Accepted, "entry-theft-not-double-applied");
        check(CharacterProgression.TryLoad(village.Progression.SaveString(), out var consequences) && consequences.Social.GetMemory(2).HostileActions == guardMemory.HostileActions, "entry-witness-consequence-persists");
        check(!new ArenaSimulation(123).TakeCampRation().Accepted, "entry-theft-is-not-silently-enabled-in-legacy-world");
        foreach (string token in new[] { "2,1", "1,0", "1,3", "01,1", "1,01", "" })
        {
            var fields = profile.SaveString().Split('|'); fields[15] = token;
            check(!CharacterProgression.TryLoad(Sign(fields), out _), "entry-invalid-revision-act-rejected-" + token);
        }
        var wrongDepth = profile.SaveString().Split('|'); wrongDepth[8] = "2";
        check(!CharacterProgression.TryLoad(Sign(wrongDepth), out _), "entry-checkpoint-cannot-skip-to-legacy-depth");
        check(!EntryWorldLayout.CanStand(-12500, -8700) && EntryWorldLayout.CanStand(0, -8700), "entry-building-footprints-block-movement");
        check(EntryWorldLayout.BuildingCount == 10 && !EntryWorldLayout.CanStand(12500, -15500) &&
            EntryWorldLayout.CanStand(0, -23000), "entry-dressed-village-houses-block-core-and-preserve-approach");
        check(!EntryWorldLayout.HasSight(-9000, -8700, -16000, -8700), "entry-building-occludes-guard-sight");
        check(EntryWorldLayout.HasSight(-9000, -16000, -16000, -16000), "entry-open-road-keeps-sight");
        check(!EntryWorldLayout.HasSight(-16000, -8700, -9000, -8700), "entry-occlusion-symmetric");
        foreach (int scenario in new[] { 0, 1, 2 })
        {
            CharacterProgression.TryLoad(checkpoint.SaveString(), out var p);
            var w = new ArenaSimulation(123, p, BigInteger.One, LootCatalog.Version1);
            w.Npcs[1].Xmm = w.PlayerXmm + 1000; w.Npcs[1].Zmm = w.PlayerZmm;
            var guard = w.Npcs[2];
            guard.Xmm = w.PlayerXmm + (scenario == 0 ? 8001 : 2000); guard.Zmm = w.PlayerZmm;
            if (scenario == 1) { guard.Memory.Health = 0; guard.Memory.RecoveryTicks = 300; }
            var beforeGuard = guard.Memory.HostileActions;
            ulong before = w.ComputeWorldHash();
            if (scenario == 2) w.Npcs[1].Xmm = w.PlayerXmm + 3000;
            if (scenario == 2)
            {
                before = w.ComputeWorldHash();
                check(!w.TakeCampRation().Accepted && before == w.ComputeWorldHash(), "entry-out-of-reach-theft-does-not-mutate");
            }
            else check(w.TakeCampRation().Accepted, "entry-unobserved-theft-accepted-" + scenario);
            check(guard.Memory.HostileActions == beforeGuard, "entry-no-omniscient-guard-memory-" + scenario);
        }
    }

    /// <summary>Checksum-valid malformed fixtures test semantic validation rather than checksum rejection.</summary>
    private static string Sign(string[] fields)
    {
        string payload = string.Join("|", fields, 0, fields.Length - 1);
        ulong hash = 14695981039346656037UL;
        unchecked { foreach (byte value in System.Text.Encoding.UTF8.GetBytes(payload)) { hash ^= value; hash *= 1099511628211UL; } }
        return payload + "|" + hash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
    }
}
