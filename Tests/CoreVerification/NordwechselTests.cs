using System;
using System.Linq;
using System.Text;
using AreArpg.Core;

internal static class NordwechselTests
{
    private const ulong Seed = 71;

    // A station checkpoint with the permanent facts earned separately in the Rattler and freight scenarios.
    private static ArenaSimulation ReportedStation()
    {
        var p = AschenzollTests.Station().Progression;
        p.Freight.Clues = 7;
        p.Freight.Choice = FreightChoice.Secured;
        p.Freight.OriginalCollected = true;
        p.Freight.Reported = true;
        return new ArenaSimulation(Seed, p, 1, LootCatalog.Version2);
    }

    private static ArenaSimulation Reload(string saved)
    {
        if (!CharacterProgression.TryLoad(saved, out var p)) throw new Exception("Nordwechsel checkpoint refused");
        return new ArenaSimulation(Seed, p, p.UnlockedCryptDepth, LootCatalog.Version2);
    }

    private static void WalkToArch(ArenaSimulation w) =>
        HunterEncounterTests.Move(w, ArenaSimulation.NordwechselXmm, ArenaSimulation.NordwechselZmm);

    public static void Run(Action<bool,string> check)
    {
        var station = ReportedStation();
        check(station.NordwechselOpen && !station.AtNordwechsel && station.TryTraversePassage() == null,
            "nordwechsel-requires-physical-western-arch");
        WalkToArch(station);
        check(station.AtAnyPassage && station.AtNordwechsel && !station.AtJourneyPassage && !station.AtWoodlandPassage,
            "nordwechsel-three-station-routes-have-distinct-triggers");
        var p = station.Progression;
        p.Freight.Reported = false;
        check(p.Rattler.Creature.IsDead && !station.NordwechselOpen && station.TryTraversePassage() == null,
            "nordwechsel-victory-and-evidence-still-require-personal-freight-report");
        p.Freight.Reported = true;
        p.Rattler.Reported = p.Rattler.BrandExamined = false;
        p.Rattler.Creature.Health = p.Rattler.Creature.MaxHealth;
        check(station.CryptCleared && !station.NordwechselOpen && station.TryTraversePassage() == null,
            "nordwechsel-empty-station-and-living-unreported-rattler-refused");
        p.Rattler.Creature.Health = 0;
        p.Rattler.BrandExamined = true;
        check(!station.NordwechselOpen && station.TryTraversePassage() == null, "nordwechsel-unreported-rattler-victory-refused");
        p.Rattler.Reported = true;

        // Individual ground fixtures isolate the two actual pickup paths, including a canonical v2 supply receipt.
        station.GroundLoot.Add(new LootState { Id = 1, Xmm = station.PlayerXmm, Zmm = station.PlayerZmm, WeaponBonus = 1 });
        check(!station.NordwechselOpen && station.TryTraversePassage() == null && station.TryCollectLoot(),
            "nordwechsel-last-gear-drop-requires-real-pickup");
        p.RecordKill();
        p.Supplies.Add(LootCatalog.Version2.Supply(Seed, 1, station.CurrentTick, 3, 3, p.LootOrdinal),
            station.PlayerXmm, station.PlayerZmm);
        check(!station.NordwechselOpen && station.TryTraversePassage() == null &&
            station.TryCollectLoot() && station.NordwechselOpen, "nordwechsel-last-supply-bag-requires-real-pickup");

        p.Regions.SetEvent(RegionNetwork.Aschenzoll, 7);
        p.AddWeaponUpgrade(8000); // Combat power fixture only; the Warden and his guards keep their actual full health.
        station.Step();
        string source = p.SaveString();
        check(source.StartsWith("AC22|", StringComparison.Ordinal) && Reload(source).Progression.SaveString() == source,
            "nordwechsel-previous-ac22-station-reads-back-byte-for-byte");
        var fallen = Reload(source).Progression;
        fallen.Woodland.SavedHealth = 0; // Defeated station checkpoint.
        var dead = Reload(fallen.SaveString());
        check(dead.GameOver && dead.AtNordwechsel && !dead.NordwechselOpen && dead.TryTraversePassage() == null,
            "nordwechsel-defeated-player-remains-at-station");
        var veteran = Reload(source).Progression;
        veteran.Dungeon.Begin(43);
        veteran.Dungeon.NextRun();
        veteran.Rattler.RestartDungeonRun();
        var returned = Reload(veteran.SaveString());
        check(!returned.Progression.Rattler.Creature.IsDead && returned.Progression.Rattler.Reported &&
            returned.TryTraversePassage()?.ChapterId == 2, "nordwechsel-optional-refreshed-dungeon-keeps-historical-victory");

        var first = Reload(source);
        var replay = Reload(source);
        ulong oldHash = first.ComputeWorldHash();
        var rift = first.TryTraversePassage();
        var repeated = replay.TryTraversePassage();
        check(rift != null && repeated != null && rift.InNordwechselRift && rift.ChapterId == 2 &&
            rift.CurrentAreaId == CharacterProgression.NordwechselAct && !rift.InAschenzoll && !rift.AtAnyPassage &&
            rift.PlayerZmm == -23000 && rift.CryptDepth == 2, "nordwechsel-real-rift-location-and-safe-southern-arrival");
        var regions = rift.Progression.Regions;
        check(regions.CurrentRegion(rift.Progression.EntryAct) == RegionNetwork.Glutklamm &&
            regions.IsVisited(RegionNetwork.Glutklamm) && regions.Events(RegionNetwork.Aschenzoll) == 7 &&
            regions.CanTransition(RegionNetwork.Aschenzoll, RegionNetwork.Glutklamm, rift.Progression.EntryAct) &&
            !regions.CanTransition(RegionNetwork.Aschengrenze, RegionNetwork.Glutklamm, rift.Progression.EntryAct),
            "nordwechsel-existing-region-section-keeps-prior-event-and-station-edge");
        check(rift.ComputeWorldHash() == repeated.ComputeWorldHash() && rift.ComputeWorldHash() != oldHash &&
            replay.ComputeWorldHash() == oldHash && first.ComputeWorldHash() == oldHash &&
            first.Progression.SaveString() == source && first.Progression.UnlockedCryptDepth.IsOne &&
            rift.Progression.StableHash != first.Progression.StableHash, "nordwechsel-two-identical-destinations-and-sealed-source");
        Console.WriteLine("NORDWECHSEL_HASH seed=" + Seed + " source=" + oldHash.ToString("X16") +
            " replaySource=" + replay.ComputeWorldHash().ToString("X16") + " target=" + rift.ComputeWorldHash().ToString("X16") +
            " replayTarget=" + repeated.ComputeWorldHash().ToString("X16"));
        string destination = rift.Progression.SaveString();
        var cold = Reload(destination);
        check(cold.Progression.SaveString() == destination && cold.ComputeWorldHash() == rift.ComputeWorldHash() &&
            CharacterProgression.TryLoad(destination, out var defaultProfile) &&
            new ArenaSimulation(Seed, defaultProfile).CryptDepth == 2 && cold.Progression.Freight.Reported &&
            cold.Progression.Rattler.Reported && cold.Progression.Woodland.ReachedAschenzoll, "nordwechsel-freight-and-history-resume-at-depth-two");
        check(!cold.CanStand(7000,7000) && cold.CanStand(0,7000) && cold.CurrentAreaId == 13 &&
            cold.PresentationLocation == 130 && cold.Progression.Supplies.Area == "13.a" &&
            cold.HungerObjective.Contains("Warden") && !cold.AtReturnPassage, "nordwechsel-native-collision-area-and-boss-objective");
        check(!CharacterProgression.TryLoad(Encoding.UTF8.GetString(Convert.FromBase64String(destination.Split('|')[2])), out _),
            "nordwechsel-stripped-region-envelope-refused");
        var noFreight = Reload(destination).Progression;
        noFreight.Freight.Reported = false;
        check(!CharacterProgression.TryLoad(noFreight.SaveString(), out _), "nordwechsel-rechecksummed-rift-without-report-refused");
        var skipped = Reload(source).Progression;
        skipped.TryUnlockNextCryptDepth(1);
        check(!CharacterProgression.TryLoad(skipped.SaveString(), out _), "nordwechsel-rechecksummed-prologue-depth-skip-refused");
        bool wrongDepth = false;
        try { new ArenaSimulation(Seed, cold.Progression, 1); } catch (ArgumentException) { wrongDepth = true; }
        check(wrongDepth, "nordwechsel-inconsistent-explicit-start-depth-refused");

        BossPulse(Reload(destination), false, check);
        BossPulse(Reload(destination), true, check);
        LeaveAggroDuringPulse(Reload(destination), check);
        ClearAndAdvance(rift, check);
        OptionalJourneyAndHunter(check);
    }

    private static void BossPulse(ArenaSimulation w, bool evade, Action<bool,string> check)
    {
        var boss = w.Monsters.Single(m => m.Kind == 3);
        // Place only the Warden in range, keeping his real AI, attacks and the player's health.
        boss.Xmm = w.PlayerXmm;
        boss.Zmm = w.PlayerZmm + 2500;
        for (int t = 0; t < 50 && boss.BossWindupTicks == 0; t++) w.Step();
        int health = w.PlayerHealth;
        check(boss.BossWindupTicks == 8, "nordwechsel-native-warden-eight-tick-warning-" + evade);
        if (evade) w.SetMoveInput(10000, 0);
        for (int i = 1; i < 8; i++)
        {
            w.Step();
            check(boss.BossWindupTicks == 8 - i && w.PlayerHealth == health,
                "nordwechsel-no-pulse-before-eighth-tick-" + evade + "-" + i);
        }
        w.Step();
        check(boss.BossWindupTicks == 0 && (evade ? w.PlayerHealth == health : w.PlayerHealth < health),
            "nordwechsel-eighth-tick-pulse-respects-position-" + evade);
    }

    private static void LeaveAggroDuringPulse(ArenaSimulation w, Action<bool,string> check)
    {
        var boss = w.Monsters.Single(m => m.Kind == 3);
        boss.Xmm = w.PlayerXmm - 10000;
        boss.Zmm = w.PlayerZmm;
        for (int t = 0; t < 50 && boss.BossWindupTicks == 0; t++) w.Step();
        check(boss.BossWindupTicks == 8, "nordwechsel-warden-commits-pulse-at-outer-trigger");
        int health = w.PlayerHealth;
        w.SetMoveInput(10000, 0);
        for (int t = 0; t < 8; t++) w.Step();
        check(w.PlayerXmm - boss.Xmm > 14000 && boss.BossWindupTicks == 0 && w.PlayerHealth == health,
            "nordwechsel-warden-pulse-expires-after-eight-ticks-outside-aggro");
    }

    private static void ClearAndAdvance(ArenaSimulation w, Action<bool,string> check)
    {
        HunterEncounterTests.Move(w, ArenaSimulation.PassageXmm, ArenaSimulation.PassageZmm);
        check(w.AtWorldPassage && !w.HungerForwardOpen && w.TryTraversePassage() == null,
            "nordwechsel-guarded-depth-two-exit-locked");
        foreach (var enemy in w.Monsters) { enemy.Xmm = w.PlayerXmm + 800; enemy.Zmm = w.PlayerZmm; }
        check(w.ActivateSkill(3).Kills == 3 && w.CryptCleared && w.GroundSupplies.Count > 0 &&
            w.TryTraversePassage() == null, "nordwechsel-full-health-warden-and-guards-defeated-yield-real-ground-loot");
        while (w.GroundLoot.Count > 0) check(w.TryCollectLoot(), "nordwechsel-collect-chapter-two-equipment");
        check(w.GroundSupplies.Count > 0 && w.TryTraversePassage() == null, "nordwechsel-boss-bag-alone-blocks-next-depth");
        while (w.GroundSupplies.Count > 0) check(w.TryCollectLoot(), "nordwechsel-collect-chapter-two-supplies");
        for (int t = 0; t < 10; t++) w.Step();
        check(w.Monsters.All(m => m.IsDead) && w.HungerForwardOpen && w.CryptDepth == 2,
            "nordwechsel-no-automatic-respawn-or-depth-change-after-clear");
        HunterEncounterTests.Move(w, 0, 19000);
        check(!w.AtWorldPassage && w.TryAdvanceCrypt() == null, "nordwechsel-cleared-rift-still-requires-physical-exit");
        HunterEncounterTests.Move(w, ArenaSimulation.PassageXmm, ArenaSimulation.PassageZmm);
        var third = w.TryTraversePassage();
        check(third != null && third.CryptDepth == 3 && third.Monsters.Count == 3 &&
            third.Monsters.Single(m => m.Kind == 3).MaxHealth > w.Monsters.Single(m => m.Kind == 3).MaxHealth &&
            third.Progression.Regions.ReturnRun(RegionNetwork.Glutklamm) == 1 && third.Progression.Freight.Reported &&
            Reload(third.Progression.SaveString()).CryptDepth == 3, "nordwechsel-next-depth-keeps-station-story-and-stronger-warden-on-cold-load");
    }

    private static void OptionalJourneyAndHunter(Action<bool,string> check)
    {
        var maps = JourneyPilotTests.Maps();
        var route = JourneyPilotTests.Enter(maps, CompanionStatus.Following);
        JourneyPilotTests.Walk(route, route.JourneyGeometry.ReturnX, route.JourneyGeometry.ReturnZ, false);
        var station = route.TryTraversePassage();
        check(station != null && station.InAschenzoll && station.Progression.Journey.Active && !station.InJourney,
            "nordwechsel-optional-woodland-remains-separate-and-returnable");
        station.Progression.WeaponMastery.Record(WeaponFamily.Bow, 17);
        WalkToArch(station);
        var rift = station.TryTraversePassage();
        check(rift != null && rift.CompanionPresent && rift.Progression.Companion.Area == CharacterProgression.NordwechselAct,
            "nordwechsel-following-hunter-enters-new-region");
        string saved = rift.Progression.SaveString();
        check(!CharacterProgression.TryLoad(saved, out _) && CharacterProgression.TryLoad(saved, maps, out var restored) &&
            restored.SaveString() == saved && restored.WeaponMastery.Experience(WeaponFamily.Bow) == 17 &&
            restored.Journey.Location == 0 && restored.Journey.MapHash == route.Progression.Journey.MapHash &&
            new ArenaSimulation(Seed, restored, restored.UnlockedCryptDepth, LootCatalog.Version1, maps).CompanionPresent,
            "nordwechsel-retains-optional-journey-map-history-and-hunter-on-cold-load");
    }
}
