using System;
using System.IO;
using System.Numerics;
using AreArpg.Core;

internal static class VillageEconomyJourneyTests
{
    private const ulong Seed = 123;

    // This historical save starts in the village with Ilyan's first clue. Every later
    // quest transition, worker, stock change and trip below comes from ordinary game inputs.
    public static void Run(Action<bool, string> check)
    {
        string beforeQuest = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "road-before-meeting-NMF1.txt"));
        check(CharacterProgression.TryLoad(beforeQuest, out var progress), "economy-journey-loads-pre-economy-checkpoint");
        var village = new ArenaSimulation(Seed, progress, BigInteger.One, LootCatalog.Version1);
        check(progress.Village.MainQuestsCompleted == 0 && !progress.Economy.Started, "economy-journey-waits-for-actual-first-worker");
        var road = Cross(village, 24000);
        HunterEncounterTests.Move(road, 0, -2000);
        check(road.ActOnHunger(HungerAction.InspectTrail).Accepted, "economy-journey-finds-trail-in-person");
        var mill = Cross(road, 24000);
        HunterEncounterTests.Move(mill, 0, -2000);
        HunterEncounterTests.Move(mill, -4000, -2000);
        check(mill.ActOnHunger(HungerAction.InspectShelter).Accepted, "economy-journey-finds-mill-supplies");
        HunterEncounterTests.Move(mill, 0, -2000);
        village = Cross(Cross(mill, -24000), -24000);
        HunterEncounterTests.Move(village, 0, -6000);
        for (int attempt = 0; attempt < 10 && village.Progression.SupplyChoice == HungerChoice.Undecided; attempt++)
        {
            var mara = village.Npcs.Find(npc => npc.Id == SocialLedger.MaraNpcId);
            HunterEncounterTests.Move(village, mara.Xmm, mara.Zmm);
            village.ActOnHunger(HungerAction.Reveal);
        }
        check(village.Progression.SupplyChoice == HungerChoice.Reveal, "economy-journey-delivers-real-report-to-mara");
        HunterEncounterTests.Move(village, 0, village.PlayerZmm);
        mill = Cross(Cross(village, 24000), 24000);
        HunterEncounterTests.Move(mill, 0, 5000);
        HunterEncounterTests.Move(mill, -4000, 5000);
        check(mill.ActOnHunger(HungerAction.InspectBeds).Accepted && mill.Progression.Village.MainQuestsCompleted == 1,
            "economy-journey-unlocks-worker-via-mill-beds");
        check(!mill.ActOnHunger(HungerAction.InspectBeds).Accepted && mill.Progression.Village.MainQuestsCompleted == 1,
            "economy-journey-quest-cannot-duplicate-worker");
        HunterEncounterTests.Move(mill, 0, 5000);
        village = Cross(Cross(mill, -24000), -24000);
        HunterEncounterTests.Move(village, 0, -6500);
        HunterEncounterTests.Move(village, VillageEconomyLayout.StandXmm(VillageSpot.Board), VillageEconomyLayout.StandZmm(VillageSpot.Board));
        var economy = village.Progression.Economy;
        check(village.VillageBoardInReach && economy.Started && economy.Residents.Count == 1 && economy.Residents[0].Identity == "resident.village.3",
            "economy-journey-reaches-working-board-with-first-real-resident");

        string atBoard = village.Progression.SaveString();
        check(CharacterProgression.TryLoad(atBoard, out var restored), "economy-journey-reloads-at-board");
        var replay = new ArenaSimulation(Seed, restored, BigInteger.One, LootCatalog.Version1);
        bool equal = true;
        for (int day = 0; day < 12; day++)
        {
            for (int tick = 0; tick < (int)WorldSkyClock.TicksPerDay; tick++)
            {
                village.Step();
                replay.Step();
                if (tick % 600 == 0) equal &= village.ComputeWorldHash() == replay.ComputeWorldHash();
            }
            string dailySave = village.Progression.SaveString();
            equal &= dailySave == replay.Progression.SaveString() && CharacterProgression.TryLoad(dailySave, out var reread)
                && dailySave == reread.SaveString() && economy.Audit().Count == 0;
        }
        check(equal, "economy-journey-twelve-days-live-versus-save-replay-with-balanced-stock");
        check(economy.Made(VillageGood.Grain) > 0 && economy.Made(VillageGood.Flour) > 0 &&
              economy.Used(VillageGood.Flour) > 0 && economy.HandedOut(VillageGood.Ration) > 0,
            "economy-journey-first-quest-worker-harvests-mills-bakes-and-delivers-without-player-supplies");
    }

    private static ArenaSimulation Cross(ArenaSimulation area, long exitZ)
    {
        HunterEncounterTests.Move(area, 0, exitZ);
        return area.TryTraversePassage() ?? throw new InvalidOperationException("Unreachable economy quest passage in region " + area.Progression.EntryAct);
    }
}
