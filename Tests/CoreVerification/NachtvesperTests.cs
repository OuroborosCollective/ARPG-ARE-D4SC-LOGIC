using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AreArpg.Core;

/// <summary>
/// Owner request 2026-11-26: the Nachtvesper harness is rare random loot (2 percent under any loot, 25 percent under a
/// knight's), its eight pieces each add one percent to maximum health, and all eight add a quarter to stamina. The plate
/// pieces protect like plate; the set is kept in the save, and health above the plain maximum survives a reload.
/// </summary>
internal static class NachtvesperTests
{
    public static void Run(Action<bool, string> check)
    {
        Rolls(check);
        Finds(check);
        Bonus(check);
        Places(check);
        Saves(check);
    }

    private static List<LootItem> Loot(int count, int kind)
    {
        var items = new List<LootItem>();
        for (int i = 0; items.Count < count && i < count * 4; i++)
        {
            var item = LootCatalog.Version1.GenerateFlagged(91UL + (ulong)(i / 50), BigInteger.One, (ulong)(i * 11), i % 50 + 1, kind, new BigInteger(i + 1), 0);
            if (item != null) items.Add(item);
        }
        return items;
    }

    private static void Rolls(Action<bool, string> check)
    {
        var none = CharacterProgression.CreateEntryProfile().Equipment;
        foreach (int kind in new[] { 0, 1, 2 })
        {
            var items = Loot(kind == 1 ? 4000 : 20000, kind);
            check(items.Count > 0 && items.All(i => i.EnemyKind == kind), "nachtvesper-loot-knows-its-enemy-" + kind);
            double share = items.Count(i => NachtvesperSet.Roll(i, none) != RegionalRelic.None) / (double)items.Count;
            double want = (kind == NachtvesperSet.KnightKind ? NachtvesperSet.KnightChanceBp : NachtvesperSet.LootChanceBp) / 10000.0;
            check(Math.Abs(share - want) <= Math.Max(.004, want * .15), "nachtvesper-chance-kind-" + kind + "-" + share.ToString("0.000"));
        }
        var knights = Loot(4000, 1);
        check(knights.All(i => NachtvesperSet.Roll(i, none) == NachtvesperSet.Roll(i, none)), "nachtvesper-roll-deterministic");
        var finds = knights.Select(i => NachtvesperSet.Roll(i, none)).Where(id => id != RegionalRelic.None).ToList();
        check(NachtvesperSet.Pieces.All(finds.Contains), "nachtvesper-every-piece-can-be-found");

        var p = CharacterProgression.CreateEntryProfile();
        foreach (var id in NachtvesperSet.Pieces.Take(7)) p.Equipment.ClaimRegionalRelic(p, id);
        check(knights.Select(i => NachtvesperSet.Roll(i, p.Equipment)).Where(id => id != RegionalRelic.None).All(id => id == RegionalRelic.N_Panzerschuhe),
            "nachtvesper-only-missing-pieces");
        p.Equipment.ClaimRegionalRelic(p, RegionalRelic.N_Panzerschuhe);
        check(knights.All(i => NachtvesperSet.Roll(i, p.Equipment) == RegionalRelic.None), "nachtvesper-nothing-after-all-eight");
        check(NachtvesperSet.Pieces.All(id => RegionalRelics.Location(id) == "Seltene Beute · oft bei Rittern" &&
            !new ArenaSimulation(5, CharacterProgression.CreateEntryProfile(), BigInteger.One, LootCatalog.Version1).TryGetRegionalRelicSource(id, out _, out _)),
            "nachtvesper-never-lies-in-the-world");
        check(NachtvesperSet.Pieces.Select(RegionalRelics.Slot).Distinct().Count() == 8, "nachtvesper-eight-places");
    }

    private static void Finds(Action<bool, string> check)
    {
        // Unlike Aschenwacht's gear, the harness turns up from the first crypt on: knights are there from the start.
        var crypt = Crypt(101, true);
        var loot = Lucky(crypt, NachtvesperSet.KnightKind, out var piece);
        check(Pick(crypt, loot) && crypt.LastGearFind == piece && crypt.Progression.Equipment.OwnsRegionalRelic(piece) &&
            NachtvesperSet.IsPiece(piece), "nachtvesper-found-under-a-knights-loot");
        check(!Pick(crypt, loot) || crypt.LastGearFind == RegionalRelic.None, "nachtvesper-same-loot-not-twice");

        var plain = Crypt(102, false);
        var plainLoot = Lucky(plain, NachtvesperSet.KnightKind, out _);
        check(!Pick(plain, plainLoot) && plain.LastLoot == plainLoot && !plain.Progression.Equipment.Active, "nachtvesper-needs-equipment-on");
    }

    /// <summary>A loot receipt for the world's next kill, from an enemy of <paramref name="kind"/>, that carries a piece.</summary>
    private static LootItem Lucky(ArenaSimulation w, int kind, out RegionalRelic piece)
    {
        w.Progression.RecordKill();
        var gear = w.Progression.Equipment.Active ? w.Progression.Equipment : CharacterProgression.CreateEntryProfile().Equipment;
        for (ulong seed = 1; seed < 20000; seed++)
        {
            var item = LootCatalog.Version1.GenerateFlagged(seed, BigInteger.One, seed, 1, kind, w.Progression.LootOrdinal, 0);
            if (item == null || AschenwachtGear.Roll(item, gear) != RegionalRelic.None) continue;
            piece = NachtvesperSet.Roll(item, gear);
            if (piece != RegionalRelic.None) return item;
        }
        throw new Exception("no lucky loot found");
    }

    private static CharacterProgression Armoured(int pieces, bool combatV2)
    {
        var p = CharacterProgression.CreateEntryProfile();
        p.Equipment.Enable(p);
        if (combatV2) p.UseCombatV2();
        foreach (var id in NachtvesperSet.Pieces) p.Equipment.ClaimRegionalRelic(p, id);
        foreach (var id in NachtvesperSet.Pieces.Take(pieces)) p.Equipment.SetRegionalRelicEquipped(id, true);
        return p;
    }

    private static void Bonus(Action<bool, string> check)
    {
        var bare = CharacterProgression.CreateEntryProfile();
        int plain = CombatRules.MaximumHealth(bare);
        check(CombatRules.HealthCeiling(bare) == plain + plain * 8 / 100, "nachtvesper-health-ceiling");
        for (int worn = 0; worn <= 8; worn++)
        {
            var p = Armoured(worn, false);
            check(p.NachtvesperPiecesWorn == worn && CombatRules.MaximumHealth(p) == plain + plain * worn / 100, "nachtvesper-health-" + worn + "-pieces");
        }
        var seven = new ArenaSimulation(103, Armoured(7, true), BigInteger.One, LootCatalog.Version1);
        var all = new ArenaSimulation(104, Armoured(8, true), BigInteger.One, LootCatalog.Version1);
        check(seven.MaxStamina == CombatV2Rules.StaminaMax && all.MaxStamina == CombatV2Rules.StaminaMax * 125 / 100, "nachtvesper-full-set-stamina");
        check(all.PlayerHealth == plain + plain * 8 / 100, "nachtvesper-starts-with-the-larger-health");
        check(all.Progression.WornProtectionBp == 4500 && all.Progression.WornHealingEfficiencyBp == 2500, "nachtvesper-plate-protection");

        // Taking a piece off shrinks the health pool; what is left stays within it.
        check(all.TrySetRegionalRelicEquipped(RegionalRelic.N_Kuerass, false) && all.MaxPlayerHealth == plain + plain * 7 / 100 &&
            all.PlayerHealth == all.MaxPlayerHealth && all.MaxStamina == 100, "nachtvesper-taking-off-shrinks-health");
        check(all.Progression.WornProtectionBp == 4500 - 1800, "nachtvesper-cuirass-off-loses-chest-plate");

        // The stamina pool: rested, stamina fills the larger pool, and shrinks back with a piece taken off.
        var rested = new ArenaSimulation(106, Armoured(8, true), BigInteger.One, LootCatalog.Version1);
        for (int i = 0; i < 12; i++) rested.Step();
        check(rested.Stamina == 125, "nachtvesper-stamina-fills-the-larger-pool-" + rested.Stamina);
        check(rested.TrySetRegionalRelicEquipped(RegionalRelic.N_Guertung, false) && rested.MaxStamina == 100 && rested.Stamina == 100,
            "nachtvesper-taking-off-shrinks-stamina");
        check(rested.TrySetRegionalRelicEquipped(RegionalRelic.N_Guertung, true) && rested.MaxStamina == 125 && rested.Stamina == 100, "nachtvesper-back-on");
    }

    private static void Places(Action<bool, string> check)
    {
        var p = Armoured(0, false);
        p.Equipment.ClaimRegionalRelic(p, RegionalRelic.A_Gliedpanzer);
        var g = p.Equipment;
        check(g.SetRegionalRelicEquipped(RegionalRelic.N_Armschienen, true) && g.SetRegionalRelicEquipped(RegionalRelic.N_Beinzeug, true), "nachtvesper-arms-and-legs");
        check(g.SetRegionalRelicEquipped(RegionalRelic.A_Gliedpanzer, true) && !g.IsRegionalRelicEquipped(RegionalRelic.N_Armschienen) &&
            !g.IsRegionalRelicEquipped(RegionalRelic.N_Beinzeug), "nachtvesper-limb-armour-replaces-arms-and-legs");
        check(g.SetRegionalRelicEquipped(RegionalRelic.N_Beinzeug, true) && !g.IsRegionalRelicEquipped(RegionalRelic.A_Gliedpanzer), "nachtvesper-legs-replace-limb-armour");
        check(g.SetRegionalRelicEquipped(RegionalRelic.N_Schaller, true) && p.Equipment.WornRegionalRelic(RegionalRelicSlot.Head) == RegionalRelic.N_Schaller,
            "nachtvesper-helm-on-the-head");
        p.Equipment.ClaimRegionalRelic(p, RegionalRelic.A_Visierhelm);
        check(g.SetRegionalRelicEquipped(RegionalRelic.A_Visierhelm, true) && !g.IsRegionalRelicEquipped(RegionalRelic.N_Schaller), "nachtvesper-one-helm-at-a-time");
    }

    private static void Saves(Action<bool, string> check)
    {
        var p = Armoured(8, true);
        check(CharacterProgression.TryLoad(p.SaveString(), out var loaded) && loaded.NachtvesperComplete &&
            NachtvesperSet.Pieces.All(loaded.Equipment.OwnsRegionalRelic) && CombatRules.MaximumHealth(loaded) == CombatRules.HealthCeiling(loaded),
            "nachtvesper-kept-in-the-save");
        check(RegionalRelics.Bit(RegionalRelic.N_Panzerschuhe) == 1L << 39, "nachtvesper-mask-bits");

        // A forest checkpoint taken with the larger health is not refused on reload, and resumes with it.
        var w = WoodlandTests.Accepted();
        var gear = w.Progression.Equipment;
        gear.Enable(w.Progression);
        foreach (var id in NachtvesperSet.Pieces) { gear.ClaimRegionalRelic(w.Progression, id); w.TrySetRegionalRelicEquipped(id, true); }
        w.ActivateSkill(2);
        int health = w.PlayerHealth;
        var forest = w.TryTraversePassage() ?? throw new Exception("Forest entry refused");
        check(health > CombatRules.MaximumHealth(CharacterProgression.CreateEntryProfile()) && forest.PlayerHealth == health, "nachtvesper-forest-entered-with-larger-health");
        var again = WoodlandTests.Reload(forest);
        check(again.PlayerHealth == health && again.Progression.NachtvesperComplete, "nachtvesper-forest-reload-keeps-health");
    }

    private static bool Pick(ArenaSimulation w, LootItem item)
    {
        w.GroundLoot.Add(new LootState { Id = 950 + w.GroundLoot.Count, Xmm = w.PlayerXmm, Zmm = w.PlayerZmm, Item = item });
        bool picked = w.TryCollectLoot();
        return picked && w.LastGearFind != RegionalRelic.None;
    }

    private static ArenaSimulation Crypt(ulong seed, bool equipment)
    {
        var profile = CharacterProgression.CreateEntryProfile();
        if (equipment) profile.Equipment.Enable(profile);
        var crypt = new ArenaSimulation(seed, profile, BigInteger.One, LootCatalog.Version1);
        foreach (var enemy in crypt.Monsters) { enemy.Xmm = 1000; enemy.Zmm = 0; enemy.Health = 1; }
        crypt.ActivateSkill(3);
        while (crypt.LootOnGround) if (!crypt.TryCollectLoot()) throw new Exception("crypt loot out of reach");
        return crypt;
    }
}
