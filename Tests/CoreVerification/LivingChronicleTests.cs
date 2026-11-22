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
        check(entries.Any(e => e.EventId == theft.Id && e.Origin == ChronicleOrigin.Performed) &&
              entries.Any(e => e.EventId == decision.Id && e.Origin == ChronicleOrigin.Performed),
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
        check(world.InteractNpc(0, NpcAction.Greet).Accepted, "chronicle-player-really-greeted-informed-npc");
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
        check(archive.Learn("mara", claim.Id, KnowledgeOrigin.Refuted, "mara", 0),
              "chronicle-refuted-claim-in-actual-forge");
        Near(world, SocialLedger.MaraNpcId);
        check(world.InteractNpc(SocialLedger.MaraNpcId, NpcAction.Greet).Accepted &&
              LivingChronicle.VisibleToPlayer(p).All(e => e.EventId != claim.Id),
              "chronicle-refuted-or-suspected-is-never-presented-as-player-fact");
    }
}
