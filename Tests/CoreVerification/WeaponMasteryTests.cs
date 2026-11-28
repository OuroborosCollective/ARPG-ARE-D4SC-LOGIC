using System;
using System.Linq;
using System.Numerics;
using AreArpg.Core;

/// <summary>Owner order 2026-11-17: RuneScape-style learn-by-doing skill per droppable weapon type.</summary>
internal static class WeaponMasteryTests
{
    internal static LootItem Give(CharacterProgression p, WeaponFamily family, bool? two = null)
    {
        for (ulong seed = 0; seed < 10000; seed++)
        {
            var item = LootCatalog.Version1.Generate(seed, 1, 1, 1, 0, p.LifetimeKills + 1); var gear = new WeaponItem(item);
            if (gear.Definition.Family != family || (two.HasValue && gear.Definition.TwoHanded != two.Value)) continue;
            p.RecordKill(); if (!p.CollectItem(item)) throw new Exception("Weapon mastery fixture receipt rejected"); return item;
        }
        throw new Exception("Unreachable weapon family");
    }

    private static ArenaSimulation Duel(CharacterProgression p, out MonsterState target)
    {
        var w = new ArenaSimulation(8, p, 1, LootCatalog.Version1);
        target = w.Monsters[0]; target.Xmm = 1000; target.Zmm = 0; target.Health = target.MaxHealth = 1000000;
        foreach (var other in w.Monsters) if (other != target) { other.Health = 0; other.LootGranted = true; }
        return w;
    }

    private static int Hit(ArenaSimulation w, MonsterState target)
    {
        for (int i = 0; i < 40 && w.Cooldowns[0] > 0; i++) w.Step();
        target.Xmm = 1000; target.Zmm = 0;
        int before = target.Health;
        if (!w.ActivateSkill(0).Activated) throw new Exception("Weapon mastery fixture hit was rejected");
        return before - target.Health;
    }

    private static string Forge(string inner, string token)
    {
        string previous = inner + "|" + LootCatalog.HashText(inner).ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
        string body = "AC21|" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(previous)) + "|" + token;
        return body + "|" + LootCatalog.HashText(body).ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The checkpoint inside an AC21 weapon-skill wrapper, or the save itself when it has none.</summary>
    internal static string Unwrapped(string saved)
    {
        if (saved == null || !saved.StartsWith("AC21|", StringComparison.Ordinal)) return saved;
        return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(saved.Split('|')[1]));
    }

    /// <summary>Wrap a (possibly mutated) inner checkpoint in the same AC21 token with a valid checksum.</summary>
    internal static string Rewrap(string outer, string inner)
    {
        if (outer == null || !outer.StartsWith("AC21|", StringComparison.Ordinal)) return inner;
        string body = "AC21|" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(inner)) + "|" + outer.Split('|')[2];
        return body + "|" + LootCatalog.HashText(body).ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void SetHealth(ArenaSimulation w, int value) =>
        typeof(ArenaSimulation).GetProperty("PlayerHealth").GetSetMethod(true).Invoke(w, new object[] { value });

    /// <summary>Earn one weapon point, put it into Vitality (level points go elsewhere) and heal to the raised maximum.
    /// Equipping the sword runs the real checkpoint capture of road, woodland and journey.</summary>
    private static void FundVitalityByWeapon(ArenaSimulation w, Action<bool, string> require, string key)
    {
        var p = w.Progression; p.Equipment.Enable(p);
        var sword = Give(p, WeaponFamily.Sword, false);
        p.WeaponMastery.Record(WeaponFamily.Sword, WeaponMasteryRules.ExperienceForLevel(5));
        while (p.UnspentLevelPoints > BigInteger.Zero) p.TrySpendMastery(MasteryPath.Might);
        int before = w.MaxPlayerHealth;
        require(p.TrySpendMastery(MasteryPath.Vitality) && p.WeaponMastery.BonusRank(MasteryPath.Vitality) == 1 &&
                w.MaxPlayerHealth == before + 20, "wm-weapon-vitality-raises-maximum-" + key);
        SetHealth(w, w.MaxPlayerHealth);
        require(w.TryEquipWeapon(sword.KillOrdinal) && w.PlayerHealth == w.MaxPlayerHealth, "wm-healed-to-weapon-maximum-" + key);
    }

    private static void HealedWeaponVitalityRoundTrips(Action<bool, string> require)
    {
        var woods = WoodlandTests.Enter();
        FundVitalityByWeapon(woods, require, "woodland");
        string saved = woods.Progression.SaveString();
        require(woods.Progression.Woodland.SavedHealth == woods.MaxPlayerHealth && saved.StartsWith("AC21|", StringComparison.Ordinal) &&
                CharacterProgression.TryLoad(saved, out var back) && back.SaveString() == saved &&
                CombatRules.MaximumHealth(back) == woods.MaxPlayerHealth, "wm-healed-weapon-vitality-woodland-roundtrip");
        string woodsSaved = saved;

        var road = HunterEncounterTests.Road(); HunterEncounterTests.Move(road, 0, 0);
        require(road.Progression.Road.Started, "wm-road-fixture-started");
        FundVitalityByWeapon(road, require, "road");
        saved = road.Progression.SaveString();
        require(road.Progression.Road.SavedHealth == road.MaxPlayerHealth && saved.StartsWith("AC21|", StringComparison.Ordinal),
                "wm-road-checkpoint-holds-weapon-raised-health");
        require(CharacterProgression.TryLoad(saved, out back) && back.SaveString() == saved, "wm-healed-weapon-vitality-road-roundtrip");

        var maps = JourneyPilotTests.Maps();
        var journey = JourneyPilotTests.Enter(maps);
        FundVitalityByWeapon(journey, require, "journey");
        saved = journey.Progression.SaveString();
        require(saved.StartsWith("AC21|", StringComparison.Ordinal) && CharacterProgression.TryLoad(saved, maps, out back) &&
                back.SaveString() == saved && CombatRules.MaximumHealth(back) == journey.MaxPlayerHealth,
                "wm-healed-weapon-vitality-journey-roundtrip");

        // Review PR #59: the AC21 wrapper keeps the journey classification that decides which backup may replace a primary.
        require(CharacterProgression.MayHoldJourneyProgress(saved) && CharacterProgression.MayHoldJourneyProgress(Unwrapped(saved)) &&
                CharacterProgression.MayHoldJourneyProgress(saved.Substring(0, saved.Length - 24)) &&
                !CharacterProgression.MayHoldJourneyProgress(woodsSaved) && !CharacterProgression.MayHoldJourneyProgress(Unwrapped(woodsSaved)) &&
                !CharacterProgression.MayHoldJourneyProgress(null) && !CharacterProgression.MayHoldJourneyProgress(""),
                "wm-journey-classification-survives-the-weapon-wrapper");
        require(CharacterProgression.MayHoldJourneyProgress("AC21|@@@@@@@@|x|y") && CharacterProgression.MayHoldJourneyProgress("AC21|QU"),
                "wm-unreadable-weapon-wrapper-counts-as-journey-progress");
        // Review PR #59 (CodeRabbit): a damaged first character must not turn a journey checkpoint into "no journey".
        string damagedJourney = "AC21|" + (saved[5] == 'Q' ? "R" : "Q") + saved.Substring(6);
        string damagedWoods = "AC21|" + (woodsSaved[5] == 'Q' ? "R" : "Q") + woodsSaved.Substring(6);
        require(CharacterProgression.MayHoldJourneyProgress(damagedJourney) && CharacterProgression.MayHoldJourneyProgress(damagedWoods),
                "wm-damaged-discriminator-counts-as-journey-progress");
        require(!CharacterProgression.MayHoldJourneyProgress(woodsSaved.Substring(0, woodsSaved.Length - 24)),
                "wm-truncated-pre-journey-wrapper-stays-recoverable");
    }

    public static void Run(Action<bool, string> require)
    {
        require(WeaponMasteryRules.ExperienceForLevel(1) == 0 && WeaponMasteryRules.ExperienceForLevel(2) == 83 &&
                WeaponMasteryRules.ExperienceForLevel(5) == 388 && WeaponMasteryRules.ExperienceForLevel(10) == 1154 &&
                WeaponMasteryRules.ExperienceForLevel(50) == 101333 && WeaponMasteryRules.ExperienceForLevel(92) == 6517253 &&
                WeaponMasteryRules.ExperienceForLevel(99) == 13034431, "wm-runescape-experience-table");
        bool rising = true;
        for (int level = 2; level <= WeaponMasteryRules.MaxLevel; level++)
            rising &= WeaponMasteryRules.ExperienceForLevel(level) > WeaponMasteryRules.ExperienceForLevel(level - 1);
        require(rising, "wm-experience-table-strictly-rising");
        require(WeaponMasteryRules.LevelForExperience(0) == 1 && WeaponMasteryRules.LevelForExperience(82) == 1 &&
                WeaponMasteryRules.LevelForExperience(83) == 2 && WeaponMasteryRules.LevelForExperience(13034430) == 98 &&
                WeaponMasteryRules.LevelForExperience(13034431) == 99 &&
                WeaponMasteryRules.LevelForExperience(WeaponMasteryRules.MaxExperience) == 99, "wm-level-lookup-boundaries");
        require(WeaponMasteryRules.StatPoints(4) == 0 && WeaponMasteryRules.StatPoints(5) == 1 && WeaponMasteryRules.StatPoints(9) == 1 &&
                WeaponMasteryRules.StatPoints(10) == 2 && WeaponMasteryRules.StatPoints(99) == 19, "wm-one-free-stat-point-per-five-levels");
        require(WeaponMasteryRules.DamageBonusBp(4) == 0 && WeaponMasteryRules.DamageBonusBp(5) == 250 &&
                WeaponMasteryRules.DamageBonusBp(10) == 500 && WeaponMasteryRules.DamageBonusBp(99) == 4750, "wm-two-and-a-half-percent-per-five-levels");
        var trained = WeaponMasteryRules.TrainedFamilies.ToArray();
        // Owner decision 2026-11-17: the shield also has a skill (block chance instead of damage).
        require(trained.SequenceEqual(new[] { WeaponFamily.Sword, WeaponFamily.Dagger, WeaponFamily.Axe, WeaponFamily.Staff, WeaponFamily.Bow, WeaponFamily.Shield }) &&
                WeaponMasteryRules.Trains(WeaponFamily.Shield) && !WeaponMasteryRules.Trains(WeaponFamily.Mace) &&
                !WeaponMasteryRules.Trains(WeaponFamily.Spear), "wm-every-droppable-weapon-type-has-its-own-skill");
        require(Enumerable.Range(0, WeaponCatalog.Count).Select(i => WeaponCatalog.Get(i).Family).Distinct().All(WeaponMasteryRules.Trains),
                "wm-catalog-families-covered");
        require(WeaponMasteryRules.DamageBonusBp(WeaponFamily.Shield, 99) == 0 && WeaponMasteryRules.DamageBonusBp(WeaponFamily.Sword, 99) == 4750,
                "wm-shield-skill-gives-no-damage-bonus");

        RunBoundaries(require);

        // Learn by doing through the real combat path.
        var p = new CharacterProgression(); p.Equipment.Enable(p);
        var sword = Give(p, WeaponFamily.Sword, false); var bow = Give(p, WeaponFamily.Bow);
        var w = Duel(p, out var target);
        require(w.TryEquipWeapon(sword.KillOrdinal), "wm-fixture-equips-sword");
        int applied = Hit(w, target);
        require(applied > 0 && p.WeaponMastery.Experience(WeaponFamily.Sword) == (long)PracticeRules.ExperienceFromEffectiveAmount(applied) &&
                p.WeaponMastery.Experience(WeaponFamily.Bow) == 0, "wm-effective-hit-trains-only-the-equipped-family");
        require(w.TryUnequipWeapon(), "wm-fixture-unequips");
        long swordXp = p.WeaponMastery.Experience(WeaponFamily.Sword);
        Hit(w, target);
        require(p.WeaponMastery.Experience(WeaponFamily.Sword) == swordXp && p.WeaponMastery.HasProgress,
                "wm-unarmed-hits-train-no-weapon-skill");
        foreach (var m in w.Monsters) { m.Health = 0; m.LootGranted = true; }
        for (int i = 0; i < 40 && w.Cooldowns[0] > 0; i++) w.Step();
        require(w.TryEquipWeapon(sword.KillOrdinal) && !w.ActivateSkill(0).Activated &&
                p.WeaponMastery.Experience(WeaponFamily.Sword) == swordXp, "wm-swing-without-target-earns-nothing");

        // Every fifth level: one free stat point and +2.5 % damage for that weapon type only.
        long baseUnspent = (long)p.UnspentMasteryPoints;
        int bonusBefore = new CombatStats(p, 0).AdditiveDamageBp;
        require(p.WeaponMastery.Record(WeaponFamily.Sword, WeaponMasteryRules.ExperienceForLevel(5) - swordXp) == 4 &&
                p.WeaponMastery.Level(WeaponFamily.Sword) == 5, "wm-level-up-reports-gained-levels");
        require((long)p.UnspentMasteryPoints == baseUnspent + 1, "wm-level-five-grants-one-free-stat-point");
        require(new CombatStats(p, 0).AdditiveDamageBp == bonusBefore + 250 && p.WeaponMasteryDamageBonusBp == 250,
                "wm-level-five-adds-two-and-a-half-percent-sword-damage");
        require(w.TryEquipWeapon(bow.KillOrdinal) && p.WeaponMasteryDamageBonusBp == 0, "wm-bonus-only-for-the-trained-weapon-type");
        int mightBefore = p.MightDamageBonusBp;
        require(p.TrySpendMastery(MasteryPath.Might) && p.MightRank == 0 && p.TotalMightRank == 1 &&
                p.MightDamageBonusBp == mightBefore + 100 && p.UnspentMasteryPoints == baseUnspent, "wm-weapon-point-is-freely-spendable");
        require(!p.TrySpendMastery(MasteryPath.Vitality) || baseUnspent > 0, "wm-no-spending-beyond-earned-points");

        // Persistence: canonical AC21 wrapper, legacy saves unchanged, forged values rejected.
        string saved = p.SaveString();
        require(saved.StartsWith("AC21|", StringComparison.Ordinal) && CharacterProgression.TryLoad(saved, out var loaded) &&
                loaded.SaveString() == saved && loaded.WeaponMastery.Level(WeaponFamily.Sword) == 5 && loaded.TotalMightRank == 1,
                "wm-ac21-roundtrip-is-canonical");
        var fresh = new CharacterProgression();
        require(!fresh.SaveString().StartsWith("AC21|", StringComparison.Ordinal), "wm-no-wrapper-without-weapon-progress");
        string inner = saved.Split('|')[1];
        string innerText = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(inner));
        string innerPayload = innerText.Substring(0, innerText.LastIndexOf('|'));
        require(CharacterProgression.TryLoad(Forge(innerPayload, "1,388,0,0,0,0,0,0,0,0,0,0"), out _), "wm-forge-helper-accepts-honest-token");
        require(!CharacterProgression.TryLoad(Forge(innerPayload, "1,388,0,0,0,0,0,0,0,2,0,0"), out _), "wm-rejects-more-spent-than-earned");
        require(!CharacterProgression.TryLoad(Forge(innerPayload, "1,200000001,0,0,0,0,0,0,0,0,0,0"), out _), "wm-rejects-experience-above-cap");
        require(!CharacterProgression.TryLoad(Forge(innerPayload, "1,0,0,0,0,0,0,0,0,0,0,0"), out _), "wm-rejects-empty-wrapper");
        require(!CharacterProgression.TryLoad(Forge(innerPayload, "1,388,0,0,0,0,0,0,0,0,0"), out _), "wm-rejects-short-token");
        require(!CharacterProgression.TryLoad(Forge(innerPayload, "1,0388,0,0,0,0,0,0,0,0,0,0"), out _), "wm-rejects-noncanonical-number");
        require(!CharacterProgression.TryLoad(Forge(innerPayload, "1,0,0,0,388,0,0,0,0,0,0,0"), out _), "wm-rejects-progress-on-untrained-family");
        require(!CharacterProgression.TryLoad(Forge(innerPayload, "1,-388,0,0,0,0,0,0,0,0,0,0"), out _), "wm-rejects-negative-experience");
        string honest = Forge(innerPayload, "1,388,0,0,0,0,0,0,0,0,0,0");
        require(!CharacterProgression.TryLoad(Forge(honest.Substring(0, honest.LastIndexOf('|')), "1,388,0,0,0,0,0,0,0,0,0,0"), out _),
                "wm-rejects-nested-weapon-wrapper");
        require(!CharacterProgression.TryLoad(honest.Replace("|1,388,", "|1,389,"), out _), "wm-rejects-token-changed-without-checksum");
        // Review PR #59: two huge ranks plus three must not wrap around to "one point spent".
        require(!CharacterProgression.TryLoad(Forge(innerPayload, "1,388,0,0,0,0,0,0,0,2147483647,2147483647,3"), out _),
                "wm-rejects-rank-sum-that-wraps-around");

        // Review PR #59: a weapon point in Vitality raises the maximum before any saved health is validated.
        HealedWeaponVitalityRoundTrips(require);

        // Determinism and damage preview used by the menu.
        var a = new CharacterProgression(); a.Equipment.Enable(a); var sa = Give(a, WeaponFamily.Sword, false);
        var b = new CharacterProgression(); b.Equipment.Enable(b); var sb = Give(b, WeaponFamily.Sword, false);
        var wa = Duel(a, out var ta); var wb = Duel(b, out var tb); wa.TryEquipWeapon(sa.KillOrdinal); wb.TryEquipWeapon(sb.KillOrdinal);
        for (int i = 0; i < 6; i++) { Hit(wa, ta); Hit(wb, tb); }
        require(a.SaveString() == b.SaveString() && a.WeaponMastery.Experience(WeaponFamily.Sword) > 0, "wm-replay-identical");
        var stats = new CombatStats(p, 0);
        bool critSeen = false, plainSeen = false, previewMatches = true;
        for (ulong seed = 1; seed < 200 && previewMatches; seed++)
        {
            long dealt = CombatRules.Damage(stats, 10000, 1, new DeterministicRng(seed), out bool critical);
            previewMatches &= dealt == CombatRules.PreviewDamage(stats, 10000, 1, critical);
            critSeen |= critical; plainSeen |= !critical;
        }
        require(previewMatches && critSeen && plainSeen, "wm-menu-preview-equals-real-damage-for-crit-and-normal-hits");
        require(CombatRules.CriticalChanceBp == 1900 && CombatRules.CriticalMultiplierBp == 15000, "wm-critical-constants-match-combat");
    }

    private static void OutOfRange(Action action, Action<bool, string> require, string name)
    {
        bool rejected = false;
        try { action(); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        require(rejected, name);
    }

    private static void RunBoundaries(Action<bool, string> require)
    {
        // Each threshold must be reached exactly; checking both sides catches lookup off-by-one errors.
        for (int level = 2; level <= WeaponMasteryRules.MaxLevel; level++)
        {
            long threshold = WeaponMasteryRules.ExperienceForLevel(level);
            require(WeaponMasteryRules.LevelForExperience(threshold - 1) == level - 1 &&
                    WeaponMasteryRules.LevelForExperience(threshold) == level,
                    "wm-exact-threshold-" + level);
        }
        foreach (int level in new[] { int.MinValue, 0, 100, int.MaxValue })
        {
            OutOfRange(() => WeaponMasteryRules.ExperienceForLevel(level), require, "wm-invalid-xp-level-" + level);
            OutOfRange(() => WeaponMasteryRules.StatPoints(level), require, "wm-invalid-point-level-" + level);
            OutOfRange(() => WeaponMasteryRules.DamageBonusBp(level), require, "wm-invalid-bonus-level-" + level);
        }
        OutOfRange(() => WeaponMasteryRules.LevelForExperience(-1), require, "wm-negative-level-lookup");
        require(WeaponMasteryRules.LevelForExperience(long.MaxValue) == 99, "wm-level-lookup-saturates");

        var state = new WeaponMasteryState();
        foreach (var family in WeaponMasteryRules.TrainedFamilies)
        {
            require(state.Experience(family) == 0 && state.Level(family) == 1 && state.ExperienceToNextLevel(family) == 83,
                    "wm-fresh-family-" + family);
            require(state.Record(family, 82) == 0 && state.ExperienceToNextLevel(family) == 1,
                    "wm-one-xp-before-first-level-" + family);
            require(state.Record(family, 1) == 1 && state.Level(family) == 2 && state.ExperienceToNextLevel(family) == 91,
                    "wm-first-level-exact-" + family);
            // A huge award must saturate before addition can overflow, and award each crossed level once.
            require(state.Record(family, long.MaxValue) == 97 && state.Experience(family) == 200000000 &&
                    state.Level(family) == 99 && state.ExperienceToNextLevel(family) == 0,
                    "wm-overflow-safe-xp-cap-" + family);
            require(state.Record(family, long.MaxValue) == 0 && state.Record(family, 0) == 0 &&
                    state.Experience(family) == 200000000, "wm-cap-idempotent-" + family);
        }
        // Owner decision 2026-11-17: the shield is a sixth trained skill with its own stat-point budget (6 x 19).
        require(state.StatPointsEarned == 114 && state.StatPointsUnspent == 114, "wm-six-independent-point-budgets");
        string before = state.Token();
        OutOfRange(() => state.Record(WeaponFamily.Sword, -1), require, "wm-negative-award-rejected");
        foreach (var family in new[] { WeaponFamily.Mace, WeaponFamily.Spear, (WeaponFamily)(-1), (WeaponFamily)8 })
        {
            require(state.Record(family, 1000) == 0, "wm-unsupported-award-ignored-" + family);
            OutOfRange(() => WeaponMasteryRules.SkillName(family), require, "wm-unsupported-name-" + family);
        }
        require(!state.TrySpend((MasteryPath)(-1)) && !state.TrySpend((MasteryPath)3) && state.Token() == before,
                "wm-invalid-actions-preserve-state");
        for (int i = 0; i < 114; i++)
            require(state.TrySpend((MasteryPath)(i % 3)), "wm-spend-earned-point-" + i);
        require(state.StatPointsUnspent == 0 && state.StatPointsSpent == 114 &&
                state.BonusRank(MasteryPath.Might) == 38 && state.BonusRank(MasteryPath.Vitality) == 38 &&
                state.BonusRank(MasteryPath.Resolve) == 38 && !state.TrySpend(MasteryPath.Might), "wm-budget-exhausted");
        require(WeaponMasteryState.TryRead(state.Token(), out var restored) && restored.Token() == state.Token() &&
                restored.StatPointsSpent == 114, "wm-all-families-and-ranks-token-roundtrip");

        // Mutate one field at a time so parser rejection cannot be explained by another invalid field.
        string[] honest = "1,388,0,0,0,0,0,0,0,1,0,0".Split(',');
        require(WeaponMasteryState.TryRead(string.Join(",", honest), out _), "wm-parser-control-valid");
        foreach (int field in new[] { 1, 9 })
        foreach (string invalid in new[] { "", "-1", "+1", " 1", "1 ", "01", "1.0", "1e2", "١", "9999999999999", "9223372036854775808" })
        {
            var fields = (string[])honest.Clone(); fields[field] = invalid;
            require(!WeaponMasteryState.TryRead(string.Join(",", fields), out var rejected) && rejected == null,
                    "wm-parser-rejects-noncanonical-field-" + field + "-" + invalid);
        }
        foreach (string invalid in new[] { null, "", "2,388,0,0,0,0,0,0,0,1,0,0", string.Join(",", honest) + ",0",
                    "1,388,0,0,0,0,0,0,0,2147483648,0,0", "1,388,0,0,0,0,0,0,0,2147483647,2147483647,2147483647" })
            require(!WeaponMasteryState.TryRead(invalid, out var rejected) && rejected == null, "wm-invalid-token-" + invalid);

        // Level-funded ranks must remain separate from weapon-funded ranks for legacy saves.
        var p = new CharacterProgression();
        p.EarnExperience(150);
        p.WeaponMastery.Record(WeaponFamily.Dagger, 1154); // Two weapon points, plus one character point.
        require(p.UnspentLevelPoints == 1 && p.UnspentMasteryPoints == 3, "wm-combined-budget");
        require(p.TrySpendMastery(MasteryPath.Vitality) && p.VitalityRank == 1 &&
                p.WeaponMastery.StatPointsSpent == 0 && p.UnspentLevelPoints == 0, "wm-level-budget-spent-first");
        require(p.TrySpendMastery(MasteryPath.Vitality) && p.TrySpendMastery(MasteryPath.Resolve) &&
                p.TotalVitalityRank == 2 && p.TotalResolveRank == 1 && p.VitalityRank == 1 && p.ResolveRank == 0 &&
                p.VitalityHealthBonus == 40 && p.ResolveHealBonus == 10 && p.SpentMasteryPoints == 1,
                "wm-weapon-vitality-and-resolve-affect-stats-without-legacy-ranks");
        var stats = new CombatStats(p, 2);
        require(stats.MaximumHealth == CombatRules.MaximumHealth(p) && stats.HealingBonus == 10 &&
                !p.TrySpendMastery(MasteryPath.Resolve), "wm-combat-stats-consume-weapon-ranks");
        string saved = p.SaveString();
        require(CharacterProgression.TryLoad(saved, out var loaded) && loaded.SaveString() == saved &&
                loaded.StableHash == p.StableHash && loaded.VitalityRank == 1 && loaded.TotalVitalityRank == 2 &&
                loaded.ResolveHealBonus == 10 && loaded.UnspentMasteryPoints == 0, "wm-mixed-point-sources-save-roundtrip");

        // A killing blow trains only the health removed, even when the reported hit is larger.
        var fighter = new CharacterProgression(); fighter.Equipment.Enable(fighter);
        var weapon = Give(fighter, WeaponFamily.Sword, false);
        var duel = Duel(fighter, out var victim);
        require(duel.TryEquipWeapon(weapon.KillOrdinal), "wm-overkill-fixture-equipped");
        fighter.WeaponMastery.Record(WeaponFamily.Sword, 82);
        victim.Health = 1;
        var result = duel.ActivateSkill(0);
        require(result.Activated && result.TotalDamage > 1 && victim.Health == 0 &&
                fighter.WeaponMastery.Experience(WeaponFamily.Sword) == 83,
                "wm-overkill-trains-effective-health-only");
        require(result.WeaponLevelUps == 1 && result.TrainedFamily == WeaponFamily.Sword &&
                result.Message.Contains("Schwertkunst Stufe 2"), "wm-levelup-feedback-identifies-trained-family");

        // Recompute the outer checksum so malformed payloads reach decoding and inner validation.
        string[] wrapper = saved.Split('|');
        foreach (string encoded in new[] { "!", wrapper[1] + "\n", Convert.ToBase64String(new byte[] { 0xff }),
                    Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("not-a-checkpoint")) })
        {
            string body = "AC21|" + encoded + "|" + wrapper[2];
            string malformed = body + "|" + LootCatalog.HashText(body).ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
            require(!CharacterProgression.TryLoad(malformed, out var rejected) && rejected == null,
                    "wm-rejects-malformed-inner-checkpoint-" + encoded);
        }

        // Independent known answers protect against matching errors in preview and actual-hit code.
        var freshStats = new CombatStats(new CharacterProgression(), 0);
        int[] normal = { 122, 110, 96, 96, 96, 96 };
        int[] critical = { 183, 165, 144, 144, 144, 144 };
        for (int kind = 0; kind <= 5; kind++)
        {
            require(CombatRules.PreviewDamage(freshStats, 10000, kind, false) == normal[kind] &&
                    CombatRules.PreviewDamage(freshStats, 10000, kind, true) == critical[kind], "wm-preview-known-damage-" + kind);
            foreach (int power in new[] { 0, 1, 3333, 10000, 17777 })
            {
                for (ulong seed = 1; seed <= 20; seed++)
                {
                    long actual = CombatRules.Damage(freshStats, power, kind, new DeterministicRng(seed), out bool crit);
                    require(actual == CombatRules.PreviewDamage(freshStats, power, kind, crit),
                            "wm-preview-hit-equality-" + kind + "-" + power + "-" + seed);
                }
            }
            require(CombatRules.PreviewDamage(freshStats, 0, kind, true) == 1, "wm-zero-power-minimum-" + kind);
        }
        OutOfRange(() => CombatRules.PreviewDamage(freshStats, -1, 0, false), require, "wm-preview-negative-power");
        foreach (int kind in new[] { -1, 6 })
            OutOfRange(() => CombatRules.PreviewDamage(freshStats, 10000, kind, false), require, "wm-preview-invalid-enemy-" + kind);
    }

}
