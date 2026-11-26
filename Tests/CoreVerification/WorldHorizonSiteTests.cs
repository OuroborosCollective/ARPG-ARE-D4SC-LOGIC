using System;
using System.Collections.Generic;
using AreArpg.Core;
using AreArpg.UnityClient;

internal static class WorldHorizonSiteTests
{
    public static void Run(Action<bool, string> require)
    {
        var sites = new HashSet<string>();
        void At(ArenaSimulation game, string expected)
        {
            if (game == null) throw new InvalidOperationException("No simulation at " + expected);
            ulong before = game.ComputeWorldHash();
            string actual = WorldHorizonSite.For(game);
            string label = expected ?? "interior-" + game.InsidePlace;
            require(actual == expected, "horizon-selects-" + label);
            require(game.ComputeWorldHash() == before, "horizon-preserves-world-hash-" + label);
            if (actual != null) sites.Add(actual);
        }

        var oldCrypt = new ArenaSimulation(93, CharacterProgression.CreateEntryProfile(), 1, LootCatalog.Version1);
        At(oldCrypt, "old_crypt");
        var court = KilnYardTests.Court(93);
        At(court, "forecourt");
        var sideGate = KilnYardTests.SideGateStand;
        require(CourtTests.WalkTo(court, sideGate[0], sideGate[1], 900), "horizon-reaches-east-side-gate");
        At(court.TryTraversePassage(), "kiln");
        require(CourtTests.WalkTo(court, CourtLayout.ExitXmm, CourtLayout.ExitZmm, 900, 1500), "horizon-reaches-bell-tower");
        At(court, "bellcourt");
        At(court.TryTraversePassage(), "village");

        foreach (var room in new[] { VillagePlace.SickRoom, VillagePlace.Smithy, VillagePlace.Barn })
        {
            var village = VillagePlaceTests.Village(11);
            VillagePlaceTests.Walk(village, VillagePlaceLayout.LeaveXmm(room), VillagePlaceLayout.LeaveZmm(room));
            village.SetMoveInput(-10000 * VillagePlaceLayout.DoorOutX(room), -10000 * VillagePlaceLayout.DoorOutZ(room));
            for (int tick = 0; tick < 12 && village.WalkingIntoHouse == VillagePlace.None; ++tick) village.Step();
            require(village.TryEnterPlace(), "horizon-enters-" + room);
            At(village, null);
        }

        At(HunterEncounterTests.Road(), "ash_road");
        var mill = WoodlandTests.Accepted();
        At(mill, "mill");
        var forest = mill.TryTraversePassage();
        At(forest, "west_forest");
        WoodlandTests.Walk(forest, -36000, 0);
        WoodlandTests.Collect(forest);
        WoodlandTests.Walk(forest, -36000, 0);
        At(forest.TryTraversePassage(), "bear");
        At(AschenzollTests.Station(), "freight");
        At(RattlerEncounterTests.Door(), "hidden_crypt");
        At(RattlerEncounterTests.Den(), "nest");

        var dungeon = CryptDungeonTests.Enter();
        for (int depth = 1; depth <= 3; ++depth)
        {
            At(dungeon, "under_" + depth);
            if (depth == 3) break;
            CryptDungeonTests.ClearFloor(dungeon);
            if (depth == 2)
            {
                CryptDungeonTests.WalkFight(dungeon, 0, 15000);
                require(dungeon.ActOnHunger(HungerAction.InspectCryptTrace).Accepted &&
                        dungeon.ActOnHunger(HungerAction.OpenInnerDoor).Accepted, "horizon-reaches-inner-crypt-door");
            }
            CryptDungeonTests.WalkFight(dungeon, 0, 24000);
            dungeon = dungeon.TryTraversePassage();
        }
        var underMillEntrance = CryptDungeonTests.Enter();
        CryptDungeonTests.WalkFight(underMillEntrance, 0, -24000);
        At(underMillEntrance.TryTraversePassage(), "granary");

        var journey = JourneyPilotTests.Enter(JourneyPilotTests.Maps());
        At(journey, "forest_road");
        JourneyPilotTests.ClearAndCollect(journey);
        var route = (JourneyRouteLayout)journey.JourneyGeometry;
        JourneyPilotTests.Walk(journey, route.CaveX, route.CaveZ);
        journey = journey.TryTraversePassage();
        for (int depth = 1; depth <= 4; ++depth)
        {
            At(journey, "side_" + depth);
            if (depth == 4) break;
            JourneyPilotTests.ClearAndCollect(journey);
            JourneyPilotTests.Walk(journey, journey.JourneyGeometry.ForwardX, journey.JourneyGeometry.ForwardZ);
            journey = journey.TryTraversePassage();
        }
        require(sites.Count == 21, "horizon-covers-sixteen-sites-and-all-seven-underground-floors");
    }
}
