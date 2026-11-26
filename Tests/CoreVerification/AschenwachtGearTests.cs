using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AreArpg.Core;

/// <summary>
/// Owner decision 2026-11-26 (answer A): the Glockenwacht's armour and the Aschbeter's two claw staffs are rare loot, fitted
/// to the hero. Once the wanderer has been in Aschenwacht, picked-up loot carries a piece he does not own yet by its rarity's
/// chance (1, 2, 4, 10 percent); the armour protects and costs healing like plate; the staffs are a one- and a two-handed
/// weapon; everything is kept in the save.
/// </summary>
internal static class AschenwachtGearTests
{
    public static void Run(Action<bool, string> check)
    {
        Rolls(check);
        Finds(check);
        Wearing(check);
    }

    private static List<LootItem> Loot(int count)
    {
        var items = new List<LootItem>();
        for (int i = 0; items.Count < count && i < count * 4; i++)
        {
            var item = LootCatalog.Version1.GenerateFlagged(77UL + (ulong)(i / 50), BigInteger.One, (ulong)(i * 7), i % 50 + 1, i % 3, new BigInteger(i + 1), 0);
            if (item != null) items.Add(item);
        }
        return items;
    }

    private static void Rolls(Action<bool, string> check)
    {
        var none = CharacterProgression.CreateEntryProfile().Equipment;
        var items = Loot(20000);
        var byRarity = items.GroupBy(i => i.Rarity).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var pair in byRarity.Where(p => p.Value.Count >= 400))
        {
            double share = pair.Value.Count(i => AschenwachtGear.Roll(i, none) != RegionalRelic.None) / (double)pair.Value.Count;
            double want = AschenwachtGear.ChanceBpFor(pair.Key) / 10000.0;
            check(Math.Abs(share - want) <= Math.Max(.006, want * .35), "aschenwacht-gear-chance-" + pair.Key + "-" + share.ToString("0.000"));
        }
        check(items.All(i => AschenwachtGear.Roll(i, none) == AschenwachtGear.Roll(i, none)), "aschenwacht-gear-roll-deterministic");
        var finds = items.Select(i => AschenwachtGear.Roll(i, none)).Where(id => id != RegionalRelic.None).ToList();
        check(AschenwachtGear.Pieces.All(id => finds.Contains(id)), "aschenwacht-gear-every-piece-can-be-found");

        // A piece once owned never comes again; with all six owned nothing more is found.
        var p = CharacterProgression.CreateEntryProfile();
        foreach (var id in AschenwachtGear.Pieces.Take(5)) p.Equipment.ClaimRegionalRelic(p, id);
        check(items.Select(i => AschenwachtGear.Roll(i, p.Equipment)).Where(id => id != RegionalRelic.None).All(id => id == RegionalRelic.A_KlauenstabZweihand),
            "aschenwacht-gear-only-missing-pieces");
        p.Equipment.ClaimRegionalRelic(p, RegionalRelic.A_KlauenstabZweihand);
        check(items.All(i => AschenwachtGear.Roll(i, p.Equipment) == RegionalRelic.None), "aschenwacht-gear-nothing-after-all-six");
        check(AschenwachtGear.Pieces.All(id => RegionalRelics.Location(id) == "Seltene Beute" &&
            !new ArenaSimulation(5, CharacterProgression.CreateEntryProfile(), BigInteger.One, LootCatalog.Version1).TryGetRegionalRelicSource(id, out _, out _)),
            "aschenwacht-gear-never-lies-in-the-world");
    }

    private static void Finds(Action<bool, string> check)
    {
        // In the old crypt, before Aschenwacht, loot that would carry a piece carries nothing.
        var crypt = Crypt(81, true);
        var cryptLoot = Lucky(crypt, out var cryptPiece);
        check(!Pick(crypt, cryptLoot) && crypt.LastLoot == cryptLoot && crypt.LastGearFind == RegionalRelic.None &&
            !crypt.Progression.Equipment.OwnsRegionalRelic(cryptPiece), "aschenwacht-gear-only-after-aschenwacht");

        // In Aschenwacht it does, and the find is a regional piece like every other one.
        var court = Court(82, true);
        var courtLoot = Lucky(court, out var piece);
        check(Pick(court, courtLoot) && court.LastGearFind == piece && court.Progression.Equipment.OwnsRegionalRelic(piece), "aschenwacht-gear-found-under-loot");
        check(!Pick(court, courtLoot) || court.LastGearFind == RegionalRelic.None, "aschenwacht-gear-same-loot-not-twice");

        // A character whose equipment is not switched on (older test fixtures, never a started game) finds nothing.
        var plain = Court(83, false);
        var plainLoot = Lucky(plain, out _);
        check(!Pick(plain, plainLoot) && plain.LastLoot == plainLoot && plain.LastGearFind == RegionalRelic.None && !plain.Progression.Equipment.Active,
            "aschenwacht-gear-needs-equipment-on");
    }

    /// <summary>A loot receipt for the world's next kill that carries a piece the wanderer lacks (searched among seeds).</summary>
    private static LootItem Lucky(ArenaSimulation w, out RegionalRelic piece)
    {
        w.Progression.RecordKill();
        var gear = w.Progression.Equipment.Active ? w.Progression.Equipment : CharacterProgression.CreateEntryProfile().Equipment;
        for (ulong seed = 1; seed < 20000; seed++)
        {
            var item = LootCatalog.Version1.GenerateFlagged(seed, BigInteger.One, seed, 1, 0, w.Progression.LootOrdinal, 0);
            if (item == null) continue;
            piece = AschenwachtGear.Roll(item, gear);
            if (piece != RegionalRelic.None) return item;
        }
        throw new Exception("no lucky loot found");
    }

    private static void Wearing(Action<bool, string> check)
    {
        var court = Court(84, true);
        var p = court.Progression;
        foreach (var id in AschenwachtGear.Pieces) p.Equipment.ClaimRegionalRelic(p, id);
        check(p.WornProtectionBp == 0 && p.WornHealingEfficiencyBp == 10000, "aschenwacht-gear-owned-protects-nothing");
        check(court.TrySetRegionalRelicEquipped(RegionalRelic.A_Visierhelm, true) && p.WornProtectionBp == 675, "aschenwacht-gear-helm-is-plate-on-the-head");
        check(court.TrySetRegionalRelicEquipped(RegionalRelic.A_Kuerass, true) && court.TrySetRegionalRelicEquipped(RegionalRelic.A_Schulterplatten, true) &&
            court.TrySetRegionalRelicEquipped(RegionalRelic.A_Gliedpanzer, true), "aschenwacht-gear-four-armour-slots");
        check(p.WornProtectionBp == 4050 && p.WornHealingEfficiencyBp == 3250, "aschenwacht-gear-full-set-protection-and-healing");
        int health = court.PlayerHealth;
        court.ReceiveBlowFrom(1000, court.PlayerXmm + 1000, court.PlayerZmm);
        check(health - court.PlayerHealth == 595, "aschenwacht-gear-softens-a-blow");

        // The staffs: the short one leaves a hand free for a shield, the long one takes both hands.
        var shortStaff = RegionalRelics.HandDefinition(RegionalRelic.A_KlauenstabEinhand);
        var longStaff = RegionalRelics.HandDefinition(RegionalRelic.A_KlauenstabZweihand);
        check(shortStaff != null && !shortStaff.TwoHanded && longStaff != null && longStaff.TwoHanded && longStaff.RangeMm > shortStaff.RangeMm,
            "aschenwacht-gear-two-claw-staffs");
        check(court.TrySetRegionalRelicEquipped(RegionalRelic.A_KlauenstabZweihand, true) && p.Equipment.Weapon.Definition.Name == "Langer Klauenstab",
            "aschenwacht-gear-long-staff-wielded");
        check(court.TrySetRegionalRelicEquipped(RegionalRelic.A_KlauenstabEinhand, true) && p.Equipment.Weapon.Definition.Name == "Klauenstab" &&
            !p.Equipment.IsRegionalRelicEquipped(RegionalRelic.A_KlauenstabZweihand), "aschenwacht-gear-one-weapon-at-a-time");

        // Everything is kept in the save; the owned mask now needs more than 31 bits.
        string save = p.SaveString();
        check(CharacterProgression.TryLoad(save, out var loaded) && AschenwachtGear.Pieces.All(loaded.Equipment.OwnsRegionalRelic) &&
            loaded.Equipment.IsRegionalRelicEquipped(RegionalRelic.A_Kuerass) && loaded.Equipment.IsRegionalRelicEquipped(RegionalRelic.A_KlauenstabEinhand) &&
            loaded.WornProtectionBp == 4050, "aschenwacht-gear-kept-in-the-save");
        check(RegionalRelics.Bit(RegionalRelic.A_KlauenstabZweihand) > int.MaxValue, "aschenwacht-gear-mask-beyond-31-bits");
        check(court.TrySetRegionalRelicEquipped(RegionalRelic.A_Kuerass, false) && p.WornProtectionBp == 4050 - 1800, "aschenwacht-gear-taken-off");
    }

    private static bool Pick(ArenaSimulation w, LootItem item)
    {
        w.GroundLoot.Add(new LootState { Id = 900 + w.GroundLoot.Count, Xmm = w.PlayerXmm, Zmm = w.PlayerZmm, Item = item });
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

    private static ArenaSimulation Court(ulong seed, bool equipment)
    {
        var crypt = Crypt(seed, equipment);
        HunterEncounterTests.Move(crypt, 0, 24000);
        return crypt.TryTraversePassage() ?? throw new Exception("crypt did not open into Aschenwacht");
    }
}
