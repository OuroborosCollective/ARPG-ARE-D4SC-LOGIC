using System;
using System.Numerics;
using AreArpg.Core;

internal static class MasteryTests
{
    public static void Run(Action<bool, string> require)
    {
        var fresh = new CharacterProgression();
        require(!fresh.TrySpendMastery(MasteryPath.Might), "mastery-cannot-spend-before-first-levelup");
        require(fresh.UnspentMasteryPoints == BigInteger.Zero, "mastery-new-character-no-points");
        fresh.EarnExperience(new BigInteger(960)); // Level 4: thresholds are 150, 450, 900.
        require(fresh.Level == new BigInteger(4) && fresh.UnspentMasteryPoints == new BigInteger(3),
            "mastery-three-points-at-level-four");
        require(fresh.TrySpendMastery(MasteryPath.Might), "mastery-might-spend-accepted");
        require(fresh.TrySpendMastery(MasteryPath.Vitality), "mastery-vitality-spend-accepted");
        require(fresh.TrySpendMastery(MasteryPath.Resolve), "mastery-resolve-spend-accepted");
        require(fresh.MightRank == BigInteger.One && fresh.VitalityRank == BigInteger.One &&
            fresh.ResolveRank == BigInteger.One, "mastery-three-branches-persist-in-memory");
        require(fresh.UnspentMasteryPoints == BigInteger.Zero && fresh.SpentMasteryPoints == new BigInteger(3),
            "mastery-budget-conserved");
        ulong beforeDenied = fresh.StableHash;
        require(!fresh.TrySpendMastery(MasteryPath.Might) && !fresh.TrySpendMastery((MasteryPath)91),
            "mastery-overspend-and-unknown-path-rejected");
        require(fresh.StableHash == beforeDenied, "mastery-rejection-does-not-mutate-character");

        string ac3 = fresh.SaveString();
        CharacterProgression loaded = null;
        require(ac3.StartsWith("AC3|", StringComparison.Ordinal) &&
            CharacterProgression.TryLoad(ac3, out loaded), "mastery-ac3-save-validated");
        require(loaded.MightRank == BigInteger.One && loaded.VitalityRank == BigInteger.One &&
            loaded.ResolveRank == BigInteger.One && loaded.UnspentMasteryPoints == BigInteger.Zero,
            "mastery-ac2-roundtrip-preserves-spent-points");
        require(loaded.StableHash == fresh.StableHash, "mastery-ac2-stable-hash-roundtrip");
        require(!CharacterProgression.TryLoad("AC2|0|0|0|0|1|0|0|202F94BB20628FC6", out loaded),
            "mastery-valid-checksum-but-invalid-budget-rejected");

        const string legacyAc1 = "AC1|160|8|1|1|83E7CB814DDECFE9";
        require(CharacterProgression.TryLoad(legacyAc1, out loaded), "mastery-existing-ac1-character-migrates");
        require(loaded.Level == new BigInteger(2) && loaded.WeaponBonus == 8 &&
            loaded.LootCollected == BigInteger.One && loaded.LifetimeKills == BigInteger.One &&
            loaded.MightRank == BigInteger.Zero && loaded.UnspentMasteryPoints == BigInteger.One,
            "mastery-ac1-migration-keeps-existing-character-and-points");
        require(loaded.TrySpendMastery(MasteryPath.Vitality), "mastery-legacy-character-can-allocate");
        require(CharacterProgression.TryLoad(loaded.SaveString(), out var migrated) &&
            migrated.VitalityRank == BigInteger.One, "mastery-migrated-ac2-save-validated");

        var unranked = new ArenaSimulation(0xCAFEUL, new CharacterProgression());
        unranked.Monsters[0].Xmm = 1000;
        unranked.Monsters[0].Zmm = 0;
        unranked.Monsters[0].Health = 1000;
        var rankedProgress = new CharacterProgression();
        rankedProgress.EarnExperience(160);
        require(rankedProgress.TrySpendMastery(MasteryPath.Might), "mastery-ranked-fighter-created");
        var ranked = new ArenaSimulation(0xCAFEUL, rankedProgress);
        ranked.Monsters[0].Xmm = 1000;
        ranked.Monsters[0].Zmm = 0;
        ranked.Monsters[0].Health = 1000;
        int baselineDamage = unranked.ActivateSkill(0).TotalDamage;
        int boostedDamage = ranked.ActivateSkill(0).TotalDamage;
        require(boostedDamage > baselineDamage,
            "mastery-might-increases-real-simulation-damage-with-same-seed");
        require(ranked.ComputeWorldHash() != unranked.ComputeWorldHash(),
            "mastery-ranks-affect-authoritative-replay-hash");

        var vitalityProgress = new CharacterProgression();
        vitalityProgress.EarnExperience(160);
        require(vitalityProgress.TrySpendMastery(MasteryPath.Vitality), "mastery-vitality-fighter-created");
        var sturdy = new ArenaSimulation(0x100UL, vitalityProgress);
        require(sturdy.MaxPlayerHealth > unranked.MaxPlayerHealth &&
            sturdy.PlayerHealth == sturdy.MaxPlayerHealth, "mastery-vitality-increases-max-and-spawn-hp");

        var resolveProgress = new CharacterProgression();
        resolveProgress.EarnExperience(160);
        require(resolveProgress.TrySpendMastery(MasteryPath.Resolve), "mastery-resolve-fighter-created");
        var healer = new ArenaSimulation(0x200UL, resolveProgress);
        var control = new ArenaSimulation(0x200UL);
        foreach (ArenaSimulation world in new[] { healer, control })
        {
            world.Monsters[0].Xmm = 23000;
            world.Monsters[0].Zmm = 23000;
            world.Monsters[1].Xmm = 1000;
            world.Monsters[1].Zmm = 0;
            world.Monsters[2].Xmm = 1000;
            world.Monsters[2].Zmm = 0;
        }
        for (int tick = 0; tick < 20; tick++) { healer.Step(); control.Step(); }
        require(healer.PlayerHealth == control.PlayerHealth && healer.PlayerHealth < 900 &&
            !healer.GameOver, "mastery-guard-comparison-starts-with-real-monster-damage");
        int healed = healer.PlayerHealth;
        int plain = control.PlayerHealth;
        require(healer.ActivateSkill(2).Activated && control.ActivateSkill(2).Activated &&
            healer.PlayerHealth - healed > control.PlayerHealth - plain,
            "mastery-resolve-increases-guard-heal-with-real-simulation");

        var huge = new CharacterProgression();
        huge.EarnExperience(BigInteger.Pow(new BigInteger(10), 85));
        require(huge.Level > new BigInteger(ulong.MaxValue), "mastery-levels-beyond-64-bit-remain-valid");
        require(huge.TrySpendMastery(MasteryPath.Might), "mastery-huge-level-can-spend");
        require(CharacterProgression.TryLoad(huge.SaveString(), out loaded) &&
            loaded.Level == huge.Level && loaded.MightRank == huge.MightRank,
            "mastery-huge-level-preserves-spent-rank-on-save");
    }
}
