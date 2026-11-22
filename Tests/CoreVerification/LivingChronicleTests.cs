using System;
using System.Linq;
using System.Numerics;
using AreArpg.Core;
using AshenCrypt.MemoryForge;

/// <summary>Issue #99: only accepted Core acts and actually heard claims may appear to the player.</summary>
internal static class LivingChronicleTests
{
    public static void Run(Action<bool, string> check)
    {
        RejectedAndAcceptedCampActions(check);
        RealMeetingAndPlayerKnowledge(check);
        NoOmniscientOrLaunderedRumors(check);
        NoRumorWithoutSafeMeetingOrDirectWitness(check);
        TenThousandTickIndependentReplayWithTwoColdLoads(check);
        RejectCorruptedChronicleReferences(check);
    }

    private static CharacterProgression Village(ulong seed, out ArenaSimulation world)
    {
        var progress = CharacterProgression.CreateEntryProfile();
        progress.EnterVillage();
        progress.Social.EnableForge();
        world = new ArenaSimulation(seed, progress, BigInteger.One, LootCatalog.Version1);
        return progress;
    }

    private static void Near(ArenaSimulation world, int npcId)
    {
        var npc = world.Npcs.Single(n => n.Id == npcId);
        npc.Xmm = world.PlayerXmm + 1000;
        npc.Zmm = world.PlayerZmm;
        npc.InteractionCooldownTicks = 0;
    }

    private static void RejectedAndAcceptedCampActions(Action<bool, string> check)
    {
        var p = Village(9901, out var world);
        Near(world, SocialLedger.MaraNpcId);
        string before = p.SaveString();
        ulong hash = world.ComputeWorldHash();
        check(!world.TakeCampRation().Accepted &&
              LivingChronicle.VisibleToPlayer(p).Count == 0 &&
              p.SaveString() == before && world.ComputeWorldHash() == hash,
              "chronicle-rejected-action-no-event-or-state");

        world.CompleteArrivalScene();
        Near(world, SocialLedger.MaraNpcId);
        check(world.TakeCampRation().Accepted, "chronicle-real-camp-theft-accepted");
        var theft = p.Social.Forge.Archive.Events.Single(e => e.Context == "camp.theft");
        var decision = p.Social.Forge.Archive.Events.Single(e => e.Context == "camp.decision.stolen");
        var entries = LivingChronicle.VisibleToPlayer(p);
        check(entries.Any(e => e.EventId == theft.Id && e.Origin == ChronicleOrigin.Performed &&
                  e.RegionId == RegionNetwork.Aschengrenze && e.AcceptedTick == theft.Tick &&
                  e.ConfirmedByPlayer) &&
              entries.Any(e => e.EventId == decision.Id && e.Origin == ChronicleOrigin.Performed &&
                  e.RegionId == RegionNetwork.Aschengrenze),
              "chronicle-accepted-action-and-causal-consequence-visible");
        check(entries.All(e => e.SourceNpcId == "" &&
              p.Social.Forge.Archive.Events.Any(source => source.Id == e.EventId &&
                  source.Actor == "player")),
              "chronicle-does-not-surface-unrelated-archive-events");
        string saved = p.SaveString();
        ulong worldHash = world.ComputeWorldHash();
        check(!world.TakeCampRation().Accepted && p.SaveString() == saved &&
              world.ComputeWorldHash() == worldHash &&
              LivingChronicle.VisibleToPlayer(p).Select(e => e.EventId).SequenceEqual(entries.Select(e => e.EventId)),
              "chronicle-replayed-camp-theft-is-idempotent");
        check(CharacterProgression.TryLoad(saved, out var restored) && restored.SaveString() == saved &&
              LivingChronicle.VisibleToPlayer(restored).Select(e => e.TextKey)
                  .SequenceEqual(entries.Select(e => e.TextKey)),
              "chronicle-canonical-save-reload-identical");
    }

    private static void RealMeetingAndPlayerKnowledge(Action<bool, string> check)
    {
        var p = Village(9902, out var world);
        world.CompleteArrivalScene();
        Near(world, SocialLedger.MaraNpcId);
        check(world.DonateCampRation().Accepted, "chronicle-partial-donation-accepted");
        var donation = p.Social.Forge.Archive.Events.Last(e => e.Context == "camp.donation");
        var mara = p.Social.Forge.Archive.Get("mara");
        var listener = p.Social.Forge.Archive.Get("ilyan");
        check(mara.Knowledge(donation.Id) != null && listener.Knowledge(donation.Id) == null,
              "chronicle-initially-only-present-mara-knows");
        // The actual 100-ms Core and its existing contact/LOS gate must create
        // this report. There is no direct Archive.Report call in this test.
        var namedMara = world.Npcs.Single(n => n.Id == SocialLedger.MaraNpcId);
        var ilyan = world.Npcs.Single(n => n.Id == 0);
        for (int step = 0; step < 130 && listener.Knowledge(donation.Id) == null; step++)
        {
            ilyan.Xmm = namedMara.Xmm + 900; ilyan.Zmm = namedMara.Zmm;
            ilyan.Memory.Health = Math.Max(1, ilyan.Memory.Health);
            world.Step();
        }
        var heard = listener.Knowledge(donation.Id);
        check(heard != null && heard.Origin == KnowledgeOrigin.Reported &&
              heard.Source == "mara" && heard.SourceSequence == mara.Knowledge(donation.Id).Sequence,
              "chronicle-real-core-meeting-creates-one-hop-reported-knowledge");
        var beforeGreeting = LivingChronicle.VisibleToPlayer(p);
        check(beforeGreeting.All(e => e.Origin != ChronicleOrigin.Heard),
              "chronicle-npc-report-not-automatically-player-knowledge");
        Near(world, 0);
        var spoken = world.InteractNpc(0, NpcAction.Greet);
        check(spoken.Accepted && spoken.ChronicleTextKey == "chronicle.camp.donation" &&
              spoken.ChronicleSpeakerId == "ilyan" &&
              spoken.ChronicleKnowledgeOrigin == KnowledgeOrigin.Reported &&
              spoken.ChronicleEventId == donation.Id,
              "chronicle-player-really-greeted-informed-npc");
        var visible = LivingChronicle.VisibleToPlayer(p);
        check(visible.Any(e => e.EventId == donation.Id && e.Origin == ChronicleOrigin.Heard &&
              e.SourceNpcId == "ilyan" && e.KnowledgeOrigin == KnowledgeOrigin.Reported),
              "chronicle-hearsay-has-speaker-and-reported-origin");
        string saved = p.SaveString();
        check(CharacterProgression.TryLoad(saved, out var restored) && restored.SaveString() == saved &&
              LivingChronicle.VisibleToPlayer(restored).Any(e => e.EventId == donation.Id &&
                  e.Origin == ChronicleOrigin.Heard && e.SourceNpcId == "ilyan"),
              "chronicle-heard-reference-survives-checked-save");
    }

    /// <summary>Two genuine independently stepped Core instances, no test doubles or clock overrides.</summary>
    private static void TenThousandTickIndependentReplayWithTwoColdLoads(Action<bool, string> check)
    {
        var first = Village(9988, out var a);
        var second = Village(9988, out var b);
        a.CompleteArrivalScene(); b.CompleteArrivalScene();
        Near(a, SocialLedger.MaraNpcId);
        Near(b, SocialLedger.MaraNpcId);
        check(a.DonateCampRation().Accepted && b.DonateCampRation().Accepted,
              "chronicle-replay-initial-real-action");
        for (int tick = 0; tick < 10000; tick++)
        {
            a.Step();
            b.Step();
            if (tick % 97 == 0)
                check(a.ComputeWorldHash() == b.ComputeWorldHash() &&
                      first.SaveString() == second.SaveString(),
                      "chronicle-replay-10000-ticks-" + tick);
            if (tick == 2999 || tick == 6999)
            {
                string saveA = first.SaveString(), saveB = second.SaveString();
                CharacterProgression restoredA, restoredB;
                bool loadedA = CharacterProgression.TryLoad(saveA, out restoredA);
                bool loadedB = CharacterProgression.TryLoad(saveB, out restoredB);
                check(saveA == saveB && loadedA && loadedB &&
                      restoredA.SaveString() == saveA && restoredB.SaveString() == saveB,
                      "chronicle-real-cold-reload-checkpoint-" + tick);
                check(LivingChronicle.VisibleToPlayer(restoredA).Select(e => e.TextKey).SequenceEqual(
                      LivingChronicle.VisibleToPlayer(restoredB).Select(e => e.TextKey)),
                      "chronicle-real-cold-reload-knowledge-" + tick);
                first = restoredA;
                second = restoredB;
                a = new ArenaSimulation(9988, first, BigInteger.One, LootCatalog.Version1);
                b = new ArenaSimulation(9988, second, BigInteger.One, LootCatalog.Version1);
            }
        }
        check(first.SaveString() == second.SaveString() && a.ComputeWorldHash() == b.ComputeWorldHash(),
              "chronicle-two-actual-replay-worlds-remain-identical");
    }

    private static void RejectCorruptedChronicleReferences(Action<bool, string> check)
    {
        var p = Village(9989, out var world);
        p.SetSection("chronicle.v1", "1|999,1,mara");
        string inconsistent = p.SaveString();
        check(!CharacterProgression.TryLoad(inconsistent, out _),
              "chronicle-checksummed-forged-reference-fails-closed");
    }

    private static void NoRumorWithoutSafeMeetingOrDirectWitness(Action<bool, string> check)
    {
        foreach (int scenario in new[] { 0, 1 })
        {
            var p = Village((ulong)(9910 + scenario), out var world);
            world.CompleteArrivalScene();
            Near(world, SocialLedger.MaraNpcId);
            check(world.DonateCampRation().Accepted, "chronicle-negative-fixture-donation-" + scenario);
            ulong eventId = p.Social.Forge.Archive.Events.Last(e => e.Context == "camp.donation").Id;
            var mara = world.Npcs.Single(n => n.Id == SocialLedger.MaraNpcId);
            var other = world.Npcs.Single(n => n.Id == 0);
            if (scenario == 1)
            {
                other.Memory.Health = 0;
                other.Memory.RecoveryTicks = 300;
            }
            for (int tick = 0; tick < 115; tick++)
            {
                other.Xmm = mara.Xmm + (scenario == 0 ? 12000 : 900);
                other.Zmm = mara.Zmm;
                world.Step();
            }
            check(p.Social.Forge.Archive.Get("ilyan").Knowledge(eventId) == null,
                  "chronicle-no-report-without-range-and-consciousness-" + scenario);
        }

        var confirmed = Village(9920, out var w);
        w.CompleteArrivalScene();
        Near(w, SocialLedger.MaraNpcId);
        check(w.DonateCampRation().Accepted, "chronicle-relay-fixture-donation");
        var source = confirmed.Social.Forge.Archive;
        ulong receiptId = source.Events.Last(e => e.Context == "camp.donation").Id;
        var maraNpc = w.Npcs.Single(n => n.Id == SocialLedger.MaraNpcId);
        var ilyanNpc = w.Npcs.Single(n => n.Id == 0);
        var dornNpc = w.Npcs.Single(n => n.Id == 2);
        for (int tick = 0; tick < 120 && source.Get("ilyan").Knowledge(receiptId) == null; tick++)
        {
            ilyanNpc.Xmm = maraNpc.Xmm + 850; ilyanNpc.Zmm = maraNpc.Zmm;
            dornNpc.Xmm = maraNpc.Xmm + 12000; dornNpc.Zmm = maraNpc.Zmm;
            w.Step();
        }
        check(source.Get("ilyan").Knowledge(receiptId)?.Origin == KnowledgeOrigin.Reported &&
              source.Get("dorn").Knowledge(receiptId) == null,
              "chronicle-only-one-hop-firsthand-source");
        ulong learnedSequence = source.Get("ilyan").Knowledge(receiptId).Sequence;
        for (int tick = 0; tick < 110; tick++)
        {
            maraNpc.Xmm = w.PlayerXmm + 12000; maraNpc.Zmm = w.PlayerZmm;
            ilyanNpc.Xmm = w.PlayerXmm + 1500; ilyanNpc.Zmm = w.PlayerZmm;
            dornNpc.Xmm = ilyanNpc.Xmm + 850; dornNpc.Zmm = ilyanNpc.Zmm;
            w.Step();
        }
        check(source.Get("dorn").Knowledge(receiptId) == null &&
              source.Get("ilyan").Knowledge(receiptId).Sequence == learnedSequence,
              "chronicle-reported-knowledge-is-not-reported-again");
    }

    private static void NoOmniscientOrLaunderedRumors(Action<bool, string> check)
    {
        var p = Village(9903, out var world);
        var archive = p.Social.Forge.Archive;
        var hidden = archive.Append(MemoryKind.SemanticClaim, "dorn", "mara", 0, 2000, false,
                                    "npc.secret", new RelationshipChange());
        check(LivingChronicle.VisibleToPlayer(p).All(e => e.EventId != hidden.Id),
              "chronicle-unseen-npc-archive-is-hidden");
        var claim = archive.Append(MemoryKind.SemanticClaim, "dorn", "mara", 0, 2000, false,
                                   "camp.allegation", new RelationshipChange());
        check(archive.Learn("mara", claim.Id, KnowledgeOrigin.Suspected, "mara", 0),
              "chronicle-suspected-claim-in-actual-forge");
        Near(world, SocialLedger.MaraNpcId);
        var suspectedGreeting = world.InteractNpc(SocialLedger.MaraNpcId, NpcAction.Greet);
        check(suspectedGreeting.Accepted && suspectedGreeting.ChronicleTextKey == "" &&
              LivingChronicle.VisibleToPlayer(p).All(e => e.EventId != claim.Id),
              "chronicle-suspected-only-is-never-player-fact");
        check(archive.Learn("mara", claim.Id, KnowledgeOrigin.Refuted, "mara", 0),
              "chronicle-refuted-claim-in-actual-forge");
        Near(world, SocialLedger.MaraNpcId);
        var refutedGreeting = world.InteractNpc(SocialLedger.MaraNpcId, NpcAction.Greet);
        check(refutedGreeting.Accepted && refutedGreeting.ChronicleTextKey == "" &&
              LivingChronicle.VisibleToPlayer(p).All(e => e.EventId != claim.Id),
              "chronicle-refuted-or-suspected-is-never-presented-as-player-fact");
    }
}
