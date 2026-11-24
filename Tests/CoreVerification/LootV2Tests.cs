using System;
using System.Linq;
using System.Numerics;
using AreArpg.Core;

/// <summary>
/// Loot revision 2 (issue #48; owner decisions of 2026-11-15, 2026-11-17 and 2026-11-23): not every enemy drops a
/// weapon, four tiers that improve with depth and bosses, legendary only from bosses and deep champions, and supply
/// bags with coins and rations as a drop of their own. Revision-1 receipts stay readable and a revision-1 world drops
/// as before.
/// </summary>
internal static class LootV2Tests
{
    public static void Run(Action<bool, string> check)
    {
        DropRatesFollowTheOwnersTable(check);
        TiersImproveWithDepthAndBosses(check);
        TheSameKillGivesTheSameDrop(check);
        ReceiptsOfBothRevisionsLoadAndForgeriesDoNot(check);
        AKillWithoutADropStillCounts(check);
        WeaponsCarryAffixesByTier(check);
        OnlyBagsBringRations(check);
        BagsFollowTheOwnersTable(check);
        ABagIsTakenOnce(check);
        BagsSurviveTheSaveAndRejectTampering(check);
        BagsKeepThePlayerInTheArea(check);
        TheKnightWithThePauldronAlwaysDrops(check);
        GeneratedPlacesKeepTheirDropsInTheSave(check);
    }

    private static ArenaSimulation World(ulong seed = 123, CharacterProgression p = null, LootCatalog catalog = null)
    {
        return new ArenaSimulation(seed, p, BigInteger.One, catalog ?? LootCatalog.Version2);
    }

    /// <summary>Arrange a close, weak enemy (optionally of a given kind), then require a real canonical kill.</summary>
    private static MonsterState Kill(ArenaSimulation w, int kind = -1)
    {
        var m = w.Monsters.First(x => !x.IsDead);
        if (kind >= 0) m.Kind = kind;
        m.Xmm = w.PlayerXmm + 1000; m.Zmm = w.PlayerZmm; m.Health = 1;
        w.Cooldowns[0] = 0;
        if (w.ActivateSkill(0).Kills != 1) throw new Exception("fixture must use a canonical kill");
        return m;
    }

    /// <summary>Share of each tier among the weapons that fell, in whole percent: white, blue, yellow, orange.</summary>
    private static int[] Share(int kind, int depth, int flags = 0)
    {
        var count = new int[4]; int total = 0;
        for (int i = 1; i <= 6000; i++)
        {
            var item = LootCatalog.Version2.GenerateFlagged(91, depth, (ulong)i, flags != 0 ? LootCatalog.RattlerEnemyId : 7, kind, 5000 + i, flags);
            if (item == null) continue;
            count[(int)item.Rarity]++; total++;
        }
        return count.Select(n => (int)Math.Round(n * 100.0 / total)).ToArray();
    }

    private static bool Near(int[] share, params int[] expected)
    {
        return share.Length == expected.Length && share.Zip(expected, (a, b) => Math.Abs(a - b) <= 4).All(ok => ok);
    }

    private static void DropRatesFollowTheOwnersTable(Action<bool, string> check)
    {
        var c = LootCatalog.Version2;
        int[] expected = { 25, 45, 75, 100 };
        for (int kind = 0; kind < 4; kind++)
        {
            int drops = 0;
            for (int i = 1; i <= 4000; i++) if (c.Generate(77, 1, (ulong)i, 5, kind, 1000 + i) != null) drops++;
            check(Math.Abs(drops / 40.0 - expected[kind]) < 3.0, "loot2-drop-rate-kind-" + kind);
        }
        check(Enumerable.Range(1, 500).All(i => c.GenerateFlagged(77, 1, 1, 5, 1, i, LootCatalog.GuaranteedFlag) != null), "loot2-guaranteed-drop-always-falls");
        check(Enumerable.Range(1, 500).All(i => c.GenerateFlagged(77, 3, 1, LootCatalog.RattlerEnemyId, 2, i, LootCatalog.BossFlag) != null), "loot2-rattler-always-drops");
    }

    private static void TiersImproveWithDepthAndBosses(Action<bool, string> check)
    {
        check(Near(Share(0, 1), 65, 28, 7, 0), "loot2-surface-tiers");
        check(Near(Share(0, 2), 53, 34, 13, 0), "loot2-second-depth-tiers");
        check(Near(Share(1, 4), 30, 45, 25, 0) && Near(Share(1, 9), 30, 45, 25, 0), "loot2-deepest-tiers-cap-at-four");
        check(Share(0, 4)[3] == 0 && Share(1, 4)[3] == 0 && Share(2, 1)[3] == 0 && Share(2, 2)[3] == 0, "loot2-no-legendary-from-common-or-shallow-enemies");
        int[] deep = Share(2, 3);
        check(deep[3] >= 1 && deep[3] <= 4 && Near(deep, 42, 39, 17, 2), "loot2-deep-champions-rarely-drop-legendary");
        int[] boss = Share(3, 1);
        check(boss[0] == 0 && Near(boss, 0, 35, 50, 15), "loot2-boss-tiers");
        int[] rattler = Share(2, 3, LootCatalog.BossFlag);
        check(rattler[0] == 0 && Near(rattler, 0, 35, 50, 15), "loot2-rattler-drops-like-a-boss");
    }

    private static void TheSameKillGivesTheSameDrop(Action<bool, string> check)
    {
        var c = LootCatalog.Version2;
        bool same = true;
        for (int i = 1; i <= 300; i++)
        {
            var a = c.Generate(5, 2, 40, 9, 1, i); var b = c.Generate(5, 2, 40, 9, 1, i);
            same &= (a == null) == (b == null) && (a == null || (a.Token() == b.Token() && a.InstanceId == b.InstanceId));
            var x = c.Supply(5, 2, 40, 9, 1, i); var y = c.Supply(5, 2, 40, 9, 1, i);
            same &= (x == null) == (y == null) && (x == null || x.Token() == y.Token());
        }
        check(same, "loot2-same-kill-same-drop");
        check(c.Revision == 2 && LootCatalog.Version1.Revision == 1 && c.StableHash != LootCatalog.Version1.StableHash, "loot2-own-catalog-revision-and-hash");
    }

    private static void ReceiptsOfBothRevisionsLoadAndForgeriesDoNot(Action<bool, string> check)
    {
        LootItem item = null; int ordinal = 0;
        while (item == null || item.Rarity == ItemRarity.Legendary) item = LootCatalog.Version2.Generate(5, 1, 40, 9, 0, ++ordinal);
        check(item.CatalogRevision == 2 && LootItem.TryRead(item.Token(), out var read) && read.Token() == item.Token() &&
            read.Rarity == item.Rarity && read.StableHash == item.StableHash, "loot2-receipt-roundtrip");
        var old = LootCatalog.Version1.Generate(5, 1, 40, 9, 0, 1);
        check(old.CatalogRevision == 1 && old.Token().Split(',').Length == 11 && LootItem.TryRead(old.Token(), out var oldRead) &&
            oldRead.Token() == old.Token(), "loot2-revision-one-receipt-still-loads");
        string[] p = item.Token().Split(',');
        Func<int, string, string> with = (index, value) => string.Join(",", p.Select((v, i) => i == index ? value : v));
        check(p.Length == 12 && !LootItem.TryRead(with(8, "3"), out _), "loot2-reject-raised-tier");
        int none = 1;
        while (LootCatalog.Version2.Generate(5, 1, 40, 9, 0, none) != null) none++;
        check(!LootItem.TryRead(with(6, none.ToString()), out _), "loot2-reject-receipt-of-a-kill-without-drop");
        check(!LootItem.TryRead(with(11, "2"), out _) && !LootItem.TryRead(with(11, "1"), out _) && !LootItem.TryRead(with(11, "4"), out _),
            "loot2-reject-flags-that-do-not-fit-the-enemy");
        check(!LootItem.TryRead(with(0, "3"), out _) && !LootItem.TryRead(item.Token() + ",0", out _), "loot2-reject-unknown-revision-or-shape");
        // Review on #134: a negative flag is refused, not thrown at; the guaranteed drop only fits a knight who may carry
        // the field pauldron (an elite at depth 1: the entry crypt's knight or a creature of the wood).
        check(!LootItem.TryRead(with(11, "-1"), out _) && !SupplyBag.TryRead("2,0000000000000005,1,40,9,0,1,-1,5,1", out _), "loot2-reject-negative-flags-without-throwing");
        check(LootCatalog.FlagsFit(1, 1, 2, 1) && LootCatalog.FlagsFit(1, 1, 200, 1) && LootCatalog.FlagsFit(1, 1, 215, 1) && LootCatalog.FlagsFit(2, 2, 42, 3) &&
            !LootCatalog.FlagsFit(1, 1, 9, 1) && !LootCatalog.FlagsFit(1, 1, 2, 2) && !LootCatalog.FlagsFit(1, 0, 2, 1) && !LootCatalog.FlagsFit(1, 1, 216, 1) &&
            !LootCatalog.FlagsFit(2, 2, 41, 3) && !LootCatalog.FlagsFit(3, 1, 2, 1) && !LootCatalog.FlagsFit(-1, 1, 2, 1), "loot2-flags-fit-only-their-two-sources");
        var elite = LootCatalog.Version2.GenerateFlagged(5, 1, 40, 9, 1, 77, LootCatalog.GuaranteedFlag);
        check(elite != null && !LootItem.TryRead(elite.Token(), out _), "loot2-reject-a-guaranteed-drop-for-an-ordinary-elite");
        var sourceKnight = LootCatalog.Version2.GenerateFlagged(5, 1, 40, 2, 1, 77, LootCatalog.GuaranteedFlag);
        check(sourceKnight != null && LootItem.TryRead(sourceKnight.Token(), out _), "loot2-accept-the-guaranteed-drop-of-the-crypt-knight");
    }

    private static void AKillWithoutADropStillCounts(Action<bool, string> check)
    {
        ArenaSimulation w = null; ulong seed;
        for (seed = 1; seed <= 200; seed++) { w = World(seed); Kill(w, 0); if (w.GroundLoot.Count == 0) break; }
        check(seed <= 200, "loot2-a-kill-may-drop-no-weapon");
        check(w.Kills == 1 && w.Progression.LifetimeKills == 1 && w.Progression.LootOrdinal == 1 && w.Progression.Items.Count == 0,
            "loot2-kill-without-drop-counts");
        var twin = World(seed); Kill(twin, 0);
        check(twin.ComputeWorldHash() == w.ComputeWorldHash(), "loot2-replay-hash");
        var legacy = World(seed, null, LootCatalog.Version1); Kill(legacy, 0);
        check(legacy.GroundLoot.Count == 1 && legacy.GroundLoot[0].Item.CatalogRevision == 1 && legacy.GroundSupplies.Count == 0,
            "loot2-revision-one-world-drops-as-before");
    }

    private static void WeaponsCarryAffixesByTier(Action<bool, string> check)
    {
        int[] low = { 200, 500, 200, 100 }, high = { 800, 1500, 600, 400 };
        var seen = new bool[4];
        for (int i = 1; i <= 4000 && !seen.All(x => x); i++)
        {
            var receipt = LootCatalog.Version2.Generate(3, 4, (ulong)i, 7, i % 2 == 0 ? 3 : 0, i);
            if (receipt == null || seen[(int)receipt.Rarity]) continue;
            seen[(int)receipt.Rarity] = true;
            var weapon = new WeaponItem(receipt);
            int a = (int)weapon.Affix, b = (int)weapon.SecondAffix;
            bool ok;
            switch (receipt.Rarity)
            {
                case ItemRarity.Normal:
                    ok = weapon.Value == 0 && weapon.SecondValue == 0 && weapon.DisplayName == weapon.Definition.Name && weapon.AffixDescription.Length == 0; break;
                case ItemRarity.Magic:
                    ok = weapon.Value >= low[a] && weapon.Value <= high[a] && weapon.SecondValue == 0; break;
                case ItemRarity.Rare:
                    ok = weapon.Value >= low[a] && weapon.Value <= high[a] && weapon.SecondValue >= low[b] && weapon.SecondValue <= high[b]; break;
                default:
                    ok = weapon.Value >= low[a] * 3 / 2 && weapon.Value <= high[a] * 3 / 2 && weapon.SecondValue >= low[b] * 3 / 2 && weapon.SecondValue <= high[b] * 3 / 2; break;
            }
            check(ok && weapon.Rarity == receipt.Rarity, "loot2-weapon-affixes-" + receipt.Rarity);
        }
        check(seen.All(x => x), "loot2-all-four-tiers-become-weapons");
        var first = new WeaponItem(LootCatalog.Version1.Generate(3, 1, 1, 7, 0, 1));
        check(first.Value > 0 && (first.Rarity == ItemRarity.Rare) == (first.SecondValue > 0) && first.DisplayName != first.Definition.Name,
            "loot2-revision-one-weapon-keeps-its-affixes");
    }

    private static void OnlyBagsBringRations(Action<bool, string> check)
    {
        ArenaSimulation w = null;
        for (ulong seed = 1; seed <= 300; seed++) { w = World(seed); Kill(w, 0); if (w.GroundLoot.Count == 1) break; }
        check(w.GroundLoot.Count == 1 && w.GroundSupplies.Count == 1 && w.LootOnGround, "loot2-weapon-and-bag-from-one-kill");
        BigInteger rations = w.Progression.Social.Provisions;
        check(w.TryCollectLoot() && w.Progression.Items.Count == 1 && w.LastSupply == null, "loot2-weapon-is-taken-first");
        check(w.Progression.Social.Provisions == rations, "loot2-weapon-brings-no-ration");
        check(w.TryCollectLoot() && w.LastSupply != null && w.Progression.Social.Provisions == rations + 1 && !w.LootOnGround, "loot2-first-bag-brings-a-ration");
        var old = World(5, null, LootCatalog.Version1); Kill(old, 0);
        BigInteger before = old.Progression.Social.Provisions, purse = old.Progression.Social.Coins;
        check(old.TryCollectLoot() && old.Progression.Social.Provisions == before + 1 && old.Progression.Social.Coins == purse, "loot2-revision-one-pickup-keeps-its-ration");
    }

    private static bool CoinRange(int kind, int low, int high)
    {
        var coins = Enumerable.Range(1, 6000).Select(i => LootCatalog.Version2.Supply(13, 1, (ulong)i, 7, kind, 100 + i))
            .Where(bag => bag != null).Select(bag => bag.Coins).ToList();
        return coins.Count > 0 && coins.Min() == low && coins.Max() == high;
    }

    private static void BagsFollowTheOwnersTable(Action<bool, string> check)
    {
        var c = LootCatalog.Version2; int bags = 0, rations = 0;
        for (int i = 1; i <= 6000; i++)
        {
            var bag = c.Supply(11, 1, (ulong)i, 7, 0, 100 + i);
            if (bag == null) continue;
            bags++; rations += bag.Rations;
        }
        check(Math.Abs(bags / 60.0 - 25) < 2.5, "loot2-bag-from-every-fourth-enemy");
        check(Math.Abs(rations * 100.0 / bags - 33.3) < 5, "loot2-every-third-bag-holds-a-ration");
        check(CoinRange(0, 3, 8) && CoinRange(1, 8, 20) && CoinRange(2, 20, 50), "loot2-bag-coins-by-enemy-class");
        check(Enumerable.Range(1, 300).All(i => { var b = c.Supply(11, 1, 1, 7, 3, 100 + i); return b != null && b.Coins == 100 && b.Rations == 1; }),
            "loot2-boss-bag-always-a-silver-and-a-ration");
        check(Enumerable.Range(1, 3).All(i => Enumerable.Range(1, 60).All(s => { var b = c.Supply((ulong)s, 1, 1, 7, 0, i); return b != null && b.Rations == 1; })),
            "loot2-first-three-kills-always-bring-a-ration");
        check(LootCatalog.Version1.Supply(11, 1, 1, 7, 0, 1) == null, "loot2-revision-one-has-no-bags");
    }

    private static void ABagIsTakenOnce(Action<bool, string> check)
    {
        var w = World(); Kill(w, 0);
        while (w.GroundLoot.Count != 0) w.TryCollectLoot();
        check(w.GroundSupplies.Count == 1, "loot2-first-kill-leaves-a-bag");
        var drop = w.GroundSupplies[0]; var bag = drop.Bag; long at = drop.Xmm;
        drop.Xmm = at + 20000;
        check(!w.TryCollectLoot() && w.GroundSupplies.Count == 1, "loot2-bag-out-of-reach-stays");
        drop.Xmm = at;
        BigInteger coins = w.Progression.Social.Coins;
        ulong before = w.ComputeWorldHash();
        check(w.TryCollectLoot() && ReferenceEquals(w.LastSupply, bag) && w.Progression.Social.Coins == coins + bag.Coins && w.GroundSupplies.Count == 0,
            "loot2-bag-credited");
        check(w.ComputeWorldHash() != before, "loot2-bag-on-the-ground-is-part-of-the-world-hash");
        check(!w.TryCollectLoot() && w.LastSupply == null && w.Progression.Social.Coins == coins + bag.Coins, "loot2-bag-taken-once");
    }

    private static void BagsSurviveTheSaveAndRejectTampering(Action<bool, string> check)
    {
        check(!World().Progression.SaveString().Contains("supplies="), "loot2-no-section-without-a-bag");
        var w = World(); Kill(w, 0);
        while (w.GroundLoot.Count != 0) w.TryCollectLoot();
        string save = w.Progression.SaveString();
        bool read = CharacterProgression.TryLoad(save, out var loaded);
        check(save.Contains("supplies=") && read && loaded.SaveString() == save && loaded.Supplies.Ground.Count == 1, "loot2-bag-save-roundtrip");
        var resumed = World(123, loaded);
        BigInteger coins = resumed.Progression.Social.Coins; int worth = resumed.GroundSupplies[0].Bag.Coins;
        check(resumed.GroundSupplies.Count == 1 && resumed.TryCollectLoot() && resumed.Progression.Social.Coins == coins + worth &&
            !resumed.Progression.SaveString().Contains("supplies="), "loot2-bag-taken-after-loading");

        string token = w.Progression.Supplies.Token(); BigInteger kills = w.Progression.LootOrdinal;
        check(SupplyDrops.TryRead(token, kills, out var same) && same.Token() == token, "loot2-bag-token-roundtrip");
        string[] f = token.Split(',');
        f[f.Length - 2] = "99";
        check(!SupplyDrops.TryRead(string.Join(",", f), kills, out _), "loot2-reject-a-fuller-bag");
        check(!SupplyDrops.TryRead(token, kills - 1, out _), "loot2-reject-the-bag-of-a-kill-that-never-happened");
        string[] section = token.Split(';');
        string twice = section[0] + ";" + section[1] + ";" + section[2] + "/2" + section[2].Substring(section[2].IndexOf(','));
        check(section.Length == 3 && !SupplyDrops.TryRead(twice, kills, out _), "loot2-reject-the-same-bag-twice");
        check(!SupplyDrops.TryRead("2;" + section[1] + ";" + section[2], kills, out _) && !SupplyDrops.TryRead(section[0] + ";bad area;" + section[2], kills, out _),
            "loot2-reject-unknown-version-or-area");
        loaded.Supplies.EnterArea("9.a");
        check(loaded.Supplies.Ground.Count == 0, "loot2-bags-stay-in-their-area");
    }

    private static void BagsKeepThePlayerInTheArea(Action<bool, string> check)
    {
        var w = World(7);
        while (w.Monsters.Any(m => !m.IsDead)) Kill(w);
        while (w.GroundLoot.Count != 0) check(w.TryCollectLoot(), "loot2-weapons-are-taken-before-bags");
        check(w.CryptCleared && w.GroundSupplies.Count >= 1 && w.TryAdvanceCrypt() == null, "loot2-a-bag-on-the-ground-keeps-the-player");
        while (w.GroundSupplies.Count != 0) check(w.TryCollectLoot(), "loot2-bags-are-taken");
        check(w.TryAdvanceCrypt() != null, "loot2-the-way-opens-once-everything-is-taken");
    }

    /// <summary>Kill everybody by arrangement and report whether the save with the drops on the ground loads unchanged.</summary>
    private static bool DropsReload(ArenaSimulation w, JourneyMapCatalog maps, out int weapons, out int bags)
    {
        while (w.Monsters.Any(m => !m.IsDead)) Kill(w);
        weapons = w.GroundLoot.Count(l => l.Item != null && l.Item.CatalogRevision == 2); bags = w.GroundSupplies.Count;
        string save = w.Progression.SaveString();
        return CharacterProgression.TryLoad(save, maps, out var loaded) && loaded.SaveString() == save && loaded.Supplies.Ground.Count == bags;
    }

    /// <summary>Crypt floors, the wood and the journey keep revision-2 weapons and bags in their saves; crypt floors count as depth.</summary>
    private static void GeneratedPlacesKeepTheirDropsInTheSave(Action<bool, string> check)
    {
        var crypt = new ArenaSimulation(123, CryptDungeonTests.Enter().Progression, 1, LootCatalog.Version2);
        check(DropsReload(crypt, null, out int weapons, out int bags) && weapons + bags > 0, "loot2-crypt-floor-drops-reload");
        check(crypt.GroundLoot.Where(l => l.Item != null).All(l => l.Item.Token().Split(',')[2] == "1"), "loot2-first-crypt-floor-is-depth-one");
        while (crypt.LootOnGround) if (!crypt.TryCollectLoot()) throw new Exception("crypt loot out of reach");
        CryptDungeonTests.WalkFight(crypt, 0, 24000);
        crypt = crypt.TryTraversePassage();
        check(crypt != null && crypt.Progression.Dungeon.Depth == 2, "loot2-crypt-descends-once-everything-is-taken");
        check(DropsReload(crypt, null, out weapons, out bags) && weapons > 0, "loot2-second-crypt-floor-drops-reload");
        check(crypt.GroundLoot.Where(l => l.Item != null).All(l => l.Item.Token().Split(',')[2] == "2"), "loot2-crypt-floors-count-as-depth");

        var wood = new ArenaSimulation(71, WoodlandTests.Enter().Progression, 1, LootCatalog.Version2);
        check(DropsReload(wood, null, out weapons, out bags) && weapons + bags > 0, "loot2-wood-drops-reload");

        var maps = JourneyPilotTests.Maps();
        var journey = new ArenaSimulation(71, JourneyPilotTests.Enter(maps).Progression, 1, LootCatalog.Version2, maps);
        check(DropsReload(journey, maps, out weapons, out bags) && weapons + bags > 0, "loot2-journey-drops-reload");
    }

    private static void TheKnightWithThePauldronAlwaysDrops(Action<bool, string> check)
    {
        bool always = true, loads = true;
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var hero = CharacterProgression.CreateEntryProfile();
            hero.Equipment.Enable(hero);
            var w = new ArenaSimulation(seed, hero, 1, LootCatalog.Version2);
            var knight = w.Monsters.Single(m => m.Id == 2 && m.Kind == 1);
            knight.Xmm = w.PlayerXmm + 1000; knight.Zmm = w.PlayerZmm; knight.Health = 1;
            if (w.ActivateSkill(0).Kills != 1) throw new Exception("fixture must use a canonical kill");
            var drop = w.GroundLoot.SingleOrDefault(l => l.HasFieldPauldron);
            always &= drop != null && drop.HasFieldPauldron && drop.Item != null && drop.Item.CatalogRevision == 2;
            loads &= drop != null && LootItem.TryRead(drop.Item.Token(), out var read) && read.Token() == drop.Item.Token();
        }
        check(always, "loot2-pauldron-knight-always-drops");
        check(loads, "loot2-guaranteed-receipt-loads");
    }
}
