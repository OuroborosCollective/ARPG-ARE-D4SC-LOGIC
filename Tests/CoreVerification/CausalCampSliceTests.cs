using System;
using System.IO;
using System.Linq;
using System.Numerics;
using AreArpg.Core;
using AshenCrypt.MemoryForge;

/// <summary>
/// Issue #5: a vertical slice through the actual existing village Core, the
/// authoritative SocialLedger and the canonical Forge archive. No new quest owner.
/// </summary>
internal static class CausalCampSliceTests
{
    public static void Run(Action<bool, string> check)
    {
        IntroInputGate(check);
        RefillFinalOnly(check);
        RobExistingWitness(check);
        GateAfterDecision(check);
        LegacyNoRetroactiveParent(check);
    }

    private static ArenaSimulation Village(ulong seed)
    {
        var progress = CharacterProgression.CreateEntryProfile();
        progress.EnterVillage();
        progress.Social.EnableForge();
        return new ArenaSimulation(seed, progress, BigInteger.One, LootCatalog.Version1);
    }

    private static void Near(ArenaSimulation game, int npcId)
    {
        var npc = game.Npcs.Single(n => n.Id == npcId);
        npc.Xmm = game.PlayerXmm + 1000;
        npc.Zmm = game.PlayerZmm;
        npc.InteractionCooldownTicks = 0;
    }

    private static void IntroInputGate(Action<bool, string> check)
    {
        // A direct gameplay API is an untrusted input too; the Unity scene
        // currently drops queued controls, but Core must still reject bypasses.
        foreach (var action in new[] { NpcAction.Greet, NpcAction.Help, NpcAction.Attack, NpcAction.Rob })
        {
            var world = Village(500UL + (ulong)action);
            Near(world, 0);
            var baseline = world.Progression.SaveString();
            var hash = world.ComputeWorldHash();
            var events = world.Progression.Social.Forge.Archive.Events.Count;
            var result = world.InteractNpc(0, action);
            check(world.ArrivalScenePending && !result.Accepted &&
                  world.Progression.SaveString() == baseline &&
                  world.ComputeWorldHash() == hash &&
                  world.Progression.Social.Forge.Archive.Events.Count == events,
                "slice-intro-input-gate-" + action);
        }

        var story = Village(505);
        Near(story, 0);
        Near(story, SocialLedger.MaraNpcId);
        string before = story.Progression.SaveString();
        var heard = story.ActOnHunger(HungerAction.HearIlyan);
        var donated = story.DonateCampRation();
        var robbed = story.TakeCampRation();
        check(!heard.Accepted && !donated.Accepted && !robbed.Accepted &&
              story.Progression.SaveString() == before && story.ArrivalScenePending,
            "slice-intro-input-gate-story-and-camp");
        check(story.CompleteArrivalScene() && !story.CompleteArrivalScene() &&
              story.Progression.Social.Forge.Archive.Events.Count(e => e.Context == ArrivalScene.Context) == 1 &&
              !story.ArrivalScenePending,
            "slice-intro-event-is-one-root");
        Near(story, 0);
        check(story.ActOnHunger(HungerAction.HearIlyan).Accepted &&
              story.Progression.Social.Forge.Archive.Events.Single(e => e.Context == "story.hearilyan").CauseEventId == null,
            "slice-intro-real-ilyan-root-without-invented-cause");
    }

    private static void RefillFinalOnly(Action<bool, string> check)
    {
        var world = Village(510);
        var progress = world.Progression;
        check(world.CompleteArrivalScene(), "slice-refill-intro-accepted");
        Near(world, SocialLedger.MaraNpcId);
        int stock = progress.Social.CampProvisions;
        BigInteger player = progress.Social.Provisions;
        var mara = progress.Social.Forge.Archive.Get("mara");
        var first = world.DonateCampRation();
        var firstReceipt = progress.Social.Forge.Archive.Events.Single(e => e.Context == "camp.donation");
        check(first.Accepted && stock + 1 == progress.Social.CampProvisions &&
              progress.Social.Provisions == player - 1 &&
              progress.Social.Forge.Archive.Events.All(e => e.Context != "camp.decision.replaced") &&
              progress.CampDecisionMade == false && world.CampPanelVisible &&
              mara.Known.Any(k => k.Event.Id == firstReceipt.Id),
            "slice-refill-final-only-partial");
        Near(world, SocialLedger.MaraNpcId);
        var second = world.DonateCampRation();
        var events = progress.Social.Forge.Archive.Events;
        var donations = events.Where(e => e.Context == "camp.donation").ToArray();
        var final = events.Single(e => e.Context == "camp.decision.replaced");
        check(second.Accepted && donations.Length == 2 &&
              donations[0].Id == firstReceipt.Id &&
              final.CauseEventId == donations[1].Id &&
              donations.All(e => e.CauseEventId == null) &&
              final.Id > donations[1].Id &&
              progress.Social.CampProvisions == stock + 2 &&
              progress.Social.Provisions == player - 2 && !world.CampPanelVisible,
            "slice-refill-final-only");
        string saved = progress.SaveString();
        check(CharacterProgression.TryLoad(saved, out var loaded) && loaded.SaveString() == saved &&
              loaded.Social.Forge.Archive.Events.Single(e => e.Id == final.Id).CauseEventId == donations[1].Id,
            "slice-refill-final-save-reload-parent");
        Near(world, SocialLedger.MaraNpcId);
        string postSetup = progress.SaveString();
        check(!world.DonateCampRation().Accepted && !world.TakeCampRation().Accepted &&
              progress.SaveString() == postSetup && !world.CampDecisionOpen,
            "slice-refill-after-decision-resource-and-event-immutable");
    }

    private static void Move(ArenaSimulation game, long x, long z)
    {
        for (int step = 0; step < 200; step++)
        {
            long dx = x - game.PlayerXmm, dz = z - game.PlayerZmm;
            if (Math.Abs(dx) < 500 && Math.Abs(dz) < 500)
            {
                game.SetMoveInput(0, 0);
                return;
            }
            game.SetMoveInput(Math.Abs(dx) < 500 ? 0 : Math.Sign(dx) * 10000,
                              Math.Abs(dz) < 500 ? 0 : Math.Sign(dz) * 10000);
            game.Step();
        }
        throw new InvalidOperationException("The existing walkable witness route could not be traversed");
    }

    private static void RobExistingWitness(Action<bool, string> check)
    {
        foreach (bool occluded in new[] { false, true })
        {
            var world = Village(520);
            check(world.CompleteArrivalScene(), "slice-rob-intro-" + occluded);
            Move(world, 0, -8700);
            Move(world, -9000, -8700);
            var mara = world.Npcs.Single(n => n.Id == 1);
            var guard = world.Npcs.Single(n => n.Id == 2);
            mara.Xmm = -8000; mara.Zmm = -8700; mara.InteractionCooldownTicks = 0;
            guard.Xmm = occluded ? -16000 : -7000; guard.Zmm = -8700;
            var stock = world.Progression.Social.CampProvisions;
            var player = world.Progression.Social.Provisions;
            var maraHostile = mara.Memory.HostileActions;
            var guardHostile = guard.Memory.HostileActions;
            var result = world.TakeCampRation();
            var archive = world.Progression.Social.Forge.Archive;
            var theft = archive.Events.Single(e => e.Context == "camp.theft");
            var decision = archive.Events.Single(e => e.Context == "camp.decision.stolen");
            check(result.Accepted && world.Progression.Social.CampProvisions == stock - 1 &&
                  world.Progression.Social.Provisions == player + 1 &&
                  mara.Memory.HostileActions == maraHostile + 1 &&
                  guard.Memory.HostileActions == guardHostile + (occluded ? 0 : 1) &&
                  decision.CauseEventId == theft.Id && theft.CauseEventId == null &&
                  archive.Events.Count(e => e.Context == "camp.theft") == 1 &&
                  archive.Events.Count(e => e.Context == "camp.decision.stolen") == 1 &&
                  mara.Mind.Known.Any(k => k.Event.Id == theft.Id && k.Origin == KnowledgeOrigin.Self) &&
                  guard.Mind.Known.Any(k => k.Event.Id == theft.Id && k.Origin == KnowledgeOrigin.Witnessed) == !occluded,
                "slice-rob-existing-witness-" + occluded);
            string saved = world.Progression.SaveString();
            check(CharacterProgression.TryLoad(saved, out var loaded) && loaded.SaveString() == saved &&
                  loaded.Social.Forge.Archive.Events.Single(e => e.Id == decision.Id).CauseEventId == theft.Id &&
                  loaded.Social.Forge.Archive.Get("dorn").Known.Any(k => k.Event.Id == theft.Id) == !occluded,
                "slice-rob-knowledge-and-parent-survive-load-" + occluded);
            check(!world.TakeCampRation().Accepted && !world.DonateCampRation().Accepted &&
                  world.Progression.SaveString() == saved && !world.CampPanelVisible,
                "slice-gate-after-decision-" + occluded);
        }
    }

    private static void GateAfterDecision(Action<bool, string> check)
    {
        var world = Village(530);
        check(world.CompleteArrivalScene(), "slice-ilyan-intro");
        Near(world, 0);
        check(world.ActOnHunger(HungerAction.HearIlyan).Accepted &&
              world.Progression.HungerStage == 1 && world.Progression.CampDecisionMade,
            "slice-ilyan-story-choice");
        var story = world.Progression.Social.Forge.Archive.Events.Single(e => e.Context == "story.hearilyan");
        check(story.CauseEventId == null, "slice-ilyan-choice-is-root-event");
        Near(world, SocialLedger.MaraNpcId);
        var saved = world.Progression.SaveString();
        check(!world.DonateCampRation().Accepted && !world.TakeCampRation().Accepted &&
              !world.ActOnHunger(HungerAction.HearIlyan).Accepted &&
              world.Progression.SaveString() == saved &&
              !world.CampPanelVisible && !world.CampDecisionOpen,
            "slice-gate-after-decision-ilyan");
    }

    private static void LegacyNoRetroactiveParent(Action<bool, string> check)
    {
        var names = new[] { "AC5", "AC17", "AC18", "AC19", "AC20" };
        var tokens = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "causal-legacy-saves.txt"));
        check(tokens.Length == names.Length, "slice-legacy-fixture-count");
        for (int i = 0; i < names.Length; i++)
        {
            var maps = names[i] == "AC20" ? JourneyPilotTests.Maps() : null;
            var token = tokens[i];
            bool loaded = token.StartsWith(names[i] + "|", StringComparison.Ordinal) &&
                          CharacterProgression.TryLoad(token, maps, out var p) &&
                          p.SaveString() == token &&
                          (p.Social.Forge == null || p.Social.Forge.Archive.Events.All(e => e.CauseEventId == null));
            check(loaded, "slice-legacy-no-retroactive-parent-" + names[i]);
        }
    }
}
