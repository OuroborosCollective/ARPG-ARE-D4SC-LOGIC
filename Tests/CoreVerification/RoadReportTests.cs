using System;
using System.Linq;
using System.Numerics;
using AreArpg.Core;
using AshenCrypt.MemoryForge;

internal static class RoadReportTests
{
    private const ulong Seed = 123;

    public static void Run(Action<bool, string> check)
    {
        var village = VillageBeforeMeeting(check);
        string checkpoint = village.Progression.SaveString();
        CheckEncounterBounds(check, checkpoint);
        CheckNoRelay(check, checkpoint);
        CheckUnwitnessedDeath(check);
        CheckLegacyWitness(check);
        CheckReplay(check, Restore(checkpoint), "NMF1");

        // Exercise an actual NMF2 save with an admitted non-default guard controller.
        var registry = NpcForgeState.Registry;
        var guard = registry.BindNew("guard");
        var next = new ControllerArtifact(guard.Archetype, "tests.road-report.guard", 1, guard.Rules);
        if (!registry.TryRestore("guard", next.ControllerId, next.Version, next.Hash, out _))
        {
            var wait = new ObservationFrame(ActionSet.Of(ForgeAction.Wait), Array.Empty<Observation>());
            var receipt = ControllerAdmission.Run(next, "tests.road-report", new[] { new RegressionCase(wait, ForgeAction.Wait) });
            registry.Register(next, next.Hash, receipt, false);
        }
        check(village.TryMigrateNpcController("dorn", next.ControllerId, next.Version, next.Hash),
            "report-nmf2-admitted-migration-before-meeting");
        CheckReplay(check, village, "NMF2");
    }

    private static ArenaSimulation VillageBeforeMeeting(Action<bool, string> check)
    {
        var road = HunterEncounterTests.Road(Seed, enableForge: true);
        HunterEncounterTests.Move(road, 0, 3000);
        for (int tick = 0; tick < 700 && !road.Progression.Road.Rescued && !road.GameOver; tick++)
            HunterEncounterTests.FightTick(road, true);
        check(road.Progression.Road.Rescued, "report-real-hunter-rescue");
        var ledger = road.Progression.Social;
        var archive = ledger.Forge.Archive;
        var rescue = archive.Events.Single(e => e.Kind == MemoryKind.Rescue);
        var witness = archive.Get("road.traveler").Knowledge(rescue.Id);
        check(witness != null && witness.Origin == KnowledgeOrigin.Witnessed &&
            archive.Get("hunter").Knowledge(rescue.Id)?.Origin == KnowledgeOrigin.Self,
            "report-an-eyewitness-and-a-rescued-hunter-know-the-original-event");
        check(rescue.Id != (ulong)ledger.RoadEventId(RoadFact.Rescue),
            "report-fixture-distinguishes-forge-id-from-road-ordinal");

        HunterEncounterTests.Move(road, 0, ArenaSimulation.ReturnPassageZmm);
        var traveler = road.Progression.Road;
        for (int tick = 0; tick < 1500; tick++)
        {
            if (traveler.TravelerRegion == 2 && Math.Abs(traveler.TravelerX + 5500) < 200 &&
                Math.Abs(traveler.TravelerZ + 6000) < 200 && traveler.Age % 100 == 99)
            {
                check(road.CurrentAreaId == 3 && !archive.Acquisitions.Any(k => k.Origin == KnowledgeOrigin.Reported) &&
                    Enumerable.Range(0, 3).All(id => ledger.RoadKnowledge(id, RoadFact.Rescue) == 0),
                    "report-no-offscreen-village-delivery-while-player-on-road");
                return road.TryTraversePassage() ?? throw new Exception("Road return passage rejected");
            }
            road.Step();
        }
        throw new Exception("The eyewitness never reached the village");
    }

    private static ArenaSimulation Restore(string checkpoint)
    {
        if (!CharacterProgression.TryLoad(checkpoint, out var progress))
            throw new Exception("Road-report save rejected");
        return new ArenaSimulation(Seed, progress, BigInteger.One, LootCatalog.Version1);
    }

    private static void CheckEncounterBounds(Action<bool, string> check, string checkpoint)
    {
        foreach (string condition in new[] { "visible", "distance", "wall", "other-region", "downed", "before-meeting" })
        {
            var world = Restore(checkpoint);
            var traveler = world.Progression.Road;
            var mara = world.Npcs.Single(n => n.Id == SocialLedger.MaraNpcId);
            traveler.TravelerX = -5500;
            traveler.TravelerZ = -6000;
            mara.Xmm = traveler.TravelerX + (condition == "distance" ? 2401 : 2400);
            mara.Zmm = traveler.TravelerZ;
            switch (condition)
            {
                case "wall":
                    // Walkable points straddling the north-east corner of the supply hall.
                    traveler.TravelerX = -10800; traveler.TravelerZ = -5650;
                    mara.Xmm = -9250; mara.Zmm = -7200;
                    break;
                case "other-region": traveler.TravelerRegion = 3; break;
                case "downed": mara.Memory.Health = 0; mara.Memory.RecoveryTicks = 25; break;
                case "before-meeting": traveler.Age--; break;
            }
            world.Step();
            if (condition == "wall")
            {
                long dx = mara.Xmm - traveler.TravelerX, dz = mara.Zmm - traveler.TravelerZ;
                check(EntryWorldLayout.CanStand(traveler.TravelerX, traveler.TravelerZ) &&
                    EntryWorldLayout.CanStand(mara.Xmm, mara.Zmm) && dx * dx + dz * dz <= 2400L * 2400 &&
                    !EntryWorldLayout.HasSight(traveler.TravelerX, traveler.TravelerZ, mara.Xmm, mara.Zmm),
                    "report-wall-alone-blocks-sight-in-speaking-range");
            }
            var ledger = world.Progression.Social;
            ulong id = ledger.Forge.Archive.Events.Single(e => e.Kind == MemoryKind.Rescue).Id;
            var known = mara.Mind.Knowledge(id);
            check((condition == "visible" ? known?.Origin == KnowledgeOrigin.Reported : known == null) &&
                ledger.RoadKnowledge(mara.Id, RoadFact.Rescue) == (condition == "visible" ? 6 : 0),
                "report-real-meeting-only-" + condition);
        }
    }

    private static void CheckNoRelay(Action<bool, string> check, string checkpoint)
    {
        var world = Restore(checkpoint);
        var ledger = world.Progression.Social;
        var traveler = world.Progression.Road;
        var mara = world.Npcs.Single(n => n.Id == SocialLedger.MaraNpcId);
        var dorn = world.Npcs.Single(n => n.Id == 2);
        mara.Xmm = traveler.TravelerX; mara.Zmm = traveler.TravelerZ;
        dorn.Xmm = 0; dorn.Zmm = 14000;
        world.Step();
        var archive = ledger.Forge.Archive;
        ulong id = archive.Events.Single(e => e.Kind == MemoryKind.Rescue).Id;
        var report = mara.Mind.Knowledge(id);
        check(report != null && !report.IsFact && report.Origin == KnowledgeOrigin.Reported &&
            dorn.Mind.Knowledge(id) == null && ledger.RoadRumor(dorn.Id) == "",
            "report-no-omniscience-for-absent-guard");
        check(!archive.Report("mara", "dorn", id, ledger.Forge.ProcessedTicks) &&
            dorn.Mind.Knowledge(id) == null && mara.Mind.Knowledge(id) == report,
            "report-no-relay-laundering");
    }

    private static void CheckUnwitnessedDeath(Action<bool, string> check)
    {
        var road = HunterEncounterTests.Road(Seed, enableForge: true);
        road.Progression.Road.TravelerX = 25000;
        HunterEncounterTests.Move(road, 0, 0);
        for (int tick = 0; tick < 400 && road.Progression.Road.HunterHealth > 0; tick++) road.Step();
        var death = road.Progression.Social.Forge.Archive.Events.Single(e => e.Kind == MemoryKind.Death);
        HunterEncounterTests.Move(road, 0, ArenaSimulation.ReturnPassageZmm);
        var village = road.TryTraversePassage() ?? throw new Exception("Unwitnessed road return rejected");
        var traveler = village.Progression.Road;
        var mara = village.Npcs.Single(n => n.Id == SocialLedger.MaraNpcId);
        bool met = false;
        for (int tick = 0; tick < 1000 && !met; tick++)
        {
            village.Step();
            long dx = mara.Xmm - traveler.TravelerX, dz = mara.Zmm - traveler.TravelerZ;
            met = traveler.Age % 100 == 0 && traveler.TravelerRegion == 2 && dx * dx + dz * dz <= 2400L * 2400 &&
                EntryWorldLayout.HasSight(traveler.TravelerX, traveler.TravelerZ, mara.Xmm, mara.Zmm);
        }
        var ledger = village.Progression.Social;
        check(met && ledger.Forge.Archive.Get("road.traveler").Knowledge(death.Id) == null &&
            mara.Mind.Knowledge(death.Id) == null && ledger.RoadKnowledge(mara.Id, RoadFact.Death) == 0,
            "report-a-real-meeting-cannot-convey-an-unwitnessed-death");
    }

    private static void CheckLegacyWitness(Action<bool, string> check)
    {
        var road = HunterEncounterTests.Road(Seed);
        HunterEncounterTests.Move(road, 0, 0);
        for (int tick = 0; tick < 400 && road.Progression.Road.HunterHealth > 0; tick++) road.Step();
        HunterEncounterTests.Move(road, 0, ArenaSimulation.ReturnPassageZmm);
        var oldVillage = road.TryTraversePassage() ?? throw new Exception("Legacy road return rejected");
        var oldLedger = oldVillage.Progression.Social;
        var oldDorn = oldVillage.Npcs.Single(n => n.Id == 2);
        oldDorn.Xmm = 0; oldDorn.Zmm = 14000;
        for (int tick = 0; tick < 1000 && oldLedger.RoadKnowledge(1, RoadFact.Death) == 0; tick++)
            oldVillage.Step();
        string heard = oldLedger.RoadRumor(1);
        string oldSave = oldVillage.Progression.SaveString();
        check(heard.Length > 0 && oldLedger.RoadKnowledge(4, RoadFact.Death) == 1 &&
            oldLedger.RoadRumor(2) == "" && oldLedger.Forge == null,
            "report-legacy-witness-and-already-informed-merchant-before-migration");
        if (!CharacterProgression.TryLoad(oldSave, out var progress)) throw new Exception("Legacy road save rejected");
        check(progress.SaveString() == oldSave, "report-legacy-save-roundtrips-before-opt-in");
        progress.Social.EnableForge();
        var village = new ArenaSimulation(Seed, progress, BigInteger.One, LootCatalog.Version1);
        var traveler = progress.Road;
        var dorn = village.Npcs.Single(n => n.Id == 2);
        dorn.Xmm = traveler.TravelerX; dorn.Zmm = traveler.TravelerZ;
        do { village.Step(); } while (traveler.Age % 100 != 0);
        check(progress.Social.RoadRumor(1) == heard &&
            !progress.Social.Forge.Archive.Events.Any(e => e.Context.StartsWith("road.fact.", StringComparison.Ordinal)),
            "report-migration-preserves-existing-rumor-without-inventing-history");
        dorn.Xmm = village.PlayerXmm + 1000; dorn.Zmm = village.PlayerZmm;
        var greeting = village.InteractNpc(dorn.Id, NpcAction.Greet);
        check(greeting.Accepted && greeting.FollowupSpeech == SpeechLine.None && progress.Social.RoadRumor(dorn.Id) == "" &&
            !dorn.Mind.Known.Any(k => k.Origin == KnowledgeOrigin.Reported),
            "report-migrated-witness-cannot-start-an-untraceable-rumor");
    }

    private static void CheckReplay(Action<bool, string> check, ArenaSimulation world, string format)
    {
        var ledger = world.Progression.Social;
        var archive = ledger.Forge.Archive;
        var traveler = world.Progression.Road;
        foreach (var npc in world.Npcs)
        {
            npc.Xmm = traveler.TravelerX + 700;
            npc.Zmm = traveler.TravelerZ;
        }
        string before = world.Progression.SaveString();
        var replay = Restore(before);
        check(archive.Save().StartsWith(format + ".", StringComparison.Ordinal) &&
            replay.Progression.SaveString() == before && replay.ComputeWorldHash() == world.ComputeWorldHash(),
            "report-before-meeting-full-save-replay-" + format);
        var rescue = archive.Events.Single(e => e.Kind == MemoryKind.Rescue);
        var source = archive.Get("road.traveler").Knowledge(rescue.Id);
        int eventCount = archive.Events.Count, roadCount = ledger.RoadEventCount;
        // Both hosts tick the same encounter, one with the actors enumerated in reverse.
        replay.Npcs.Reverse();
        world.Step();
        replay.Step();
        replay.Npcs.Reverse();
        var reports = replay.Progression.Social.Forge.Archive.Acquisitions.Where(k => k.Origin == KnowledgeOrigin.Reported).ToArray();
        check(reports.Length == 3 && reports.Select(k => k.Knower).SequenceEqual(new[] { "ilyan", "mara", "dorn" }),
            "report-simultaneous-encounters-have-stable-npc-id-order-" + format);
        check(reports.All(k => k.Event.Id == rescue.Id && k.Source == source.Knower && k.SourceSequence == source.Sequence &&
            k.LearnedTick == ledger.Forge.ProcessedTicks && !k.IsFact) &&
            archive.Events.Count == eventCount && ledger.RoadEventCount == roadCount,
            "report-same-id-same-source-without-new-event-" + format);
        string after = world.Progression.SaveString();
        check(replay.Progression.SaveString() == after && replay.ComputeWorldHash() == world.ComputeWorldHash(),
            "report-permuted-encounters-have-the-same-world-hash-and-bytes-" + format);
        replay = Restore(after);
        check(replay.Progression.SaveString() == after && replay.ComputeWorldHash() == world.ComputeWorldHash(),
            "report-after-meeting-full-save-replay-" + format);
        var account = (world.Progression.TotalExperience, ledger.Coins, ledger.Provisions, ledger.CampProvisions,
            world.PlayerHealth, traveler.HunterHealth);
        var axes = Enum.GetValues(typeof(RelationshipAxis)).Cast<RelationshipAxis>().ToArray();
        var relations = world.Npcs.SelectMany(n => axes.Select(a => n.Mind.RelationshipTo("player")[a])).ToArray();
        int acquisitions = archive.Acquisitions.Count;
        for (int tick = 0; tick < 99; tick++)
        {
            world.Step();
            replay.Step();
            check(replay.ComputeWorldHash() == world.ComputeWorldHash(), "report-tick-replay-" + format + "-" + tick);
        }
        // Ilyan naturally heals while resting. Compare his health against the same tick without a meeting.
        var withoutMeeting = Restore(world.Progression.SaveString());
        withoutMeeting.Progression.Road.TravelerX = 20000;
        withoutMeeting.Progression.Road.TravelerZ = 15000;
        withoutMeeting.Step();
        world.Step();
        replay.Step();
        var mara = world.Npcs.Single(n => n.Id == SocialLedger.MaraNpcId);
        long dx = mara.Xmm - traveler.TravelerX, dz = mara.Zmm - traveler.TravelerZ;
        check(traveler.Age % 100 == 0 && dx * dx + dz * dz <= 2400L * 2400 &&
            EntryWorldLayout.HasSight(traveler.TravelerX, traveler.TravelerZ, mara.Xmm, mara.Zmm) &&
            replay.ComputeWorldHash() == world.ComputeWorldHash(), "report-replayed-second-meeting-" + format);
        check(archive.Acquisitions.Count == acquisitions && archive.Events.Count == eventCount && ledger.RoadEventCount == roadCount &&
            relations.SequenceEqual(world.Npcs.SelectMany(n => axes.Select(a => n.Mind.RelationshipTo("player")[a]))) &&
            withoutMeeting.Npcs.Select(n => n.Memory.Health).SequenceEqual(world.Npcs.Select(n => n.Memory.Health)) &&
            account == (world.Progression.TotalExperience, ledger.Coins, ledger.Provisions, ledger.CampProvisions,
                world.PlayerHealth, traveler.HunterHealth),
            "report-no-double-impact-at-next-meeting-" + format);
    }
}
