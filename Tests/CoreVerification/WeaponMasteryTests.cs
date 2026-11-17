using System;
using System.Linq;
using System.Numerics;
using AreArpg.Core;

/// <summary>Owner order 2026-11-17: RuneScape-style learn-by-doing skill per droppable weapon type.</summary>
internal static class WeaponMasteryTests
{
    private static LootItem Give(CharacterProgression p, WeaponFamily family, bool? two = null)
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
        require(trained.SequenceEqual(new[] { WeaponFamily.Sword, WeaponFamily.Dagger, WeaponFamily.Axe, WeaponFamily.Staff, WeaponFamily.Bow }) &&
                !WeaponMasteryRules.Trains(WeaponFamily.Shield) && !WeaponMasteryRules.Trains(WeaponFamily.Mace) &&
                !WeaponMasteryRules.Trains(WeaponFamily.Spear), "wm-every-droppable-attacking-family-has-its-own-skill");
        require(Enumerable.Range(0, WeaponCatalog.Count).Select(i => WeaponCatalog.Get(i).Family)
                .Where(f => f != WeaponFamily.Shield).Distinct().All(WeaponMasteryRules.Trains), "wm-catalog-families-covered");

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
}
