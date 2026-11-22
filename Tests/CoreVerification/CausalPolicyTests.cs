using System;
using System.Linq;
using System.Numerics;
using AreArpg.Core;
using AshenCrypt.MemoryForge;

/// <summary>
/// Issue #4: exact, versioned Forge policy admission. Archive fixtures exercise
/// the canonical memory APIs; only ArenaSimulation owns gameplay effects.
/// </summary>
internal static class CausalPolicyTests
{
    public static void Run(Action<bool, string> check)
    {
        KnownVersusReported(check);
        IndirectThreat(check);
        DangerAndNeeds(check);
        ScoreDoesNotMintResources(check);
        ExactControllerRestore(check);
        LongCoreReplay(check);
    }

    private static ArenaSimulation Village(ulong seed)
    {
        var profile = CharacterProgression.CreateEntryProfile();
        profile.EnterVillage();
        profile.Social.EnableForge();
        var world = new ArenaSimulation(seed, profile, BigInteger.One, LootCatalog.Version1);
        if (world.ArrivalScenePending && !world.CompleteArrivalScene())
            throw new InvalidOperationException("Arrival scene was not accepted");
        return world;
    }

    private static void KnownVersusReported(Action<bool, string> check)
    {
        var registry = ForgeDefaults.CreateRegistry();
        var direct = new MemoryArchive();
        var directMind = direct.Spawn("traveler", "traveler", registry);
        var help = direct.Append(MemoryKind.Help, "player", "traveler", 1, 9000, false,
            "policy.accepted-help", new RelationshipChange(trust: 1000, gratitude: 1713));
        check(direct.Perceive("traveler", help.Id, KnowledgeOrigin.Self, 1, null, 4500),
            "policy-direct-help-provenance-recorded");

        var reported = new MemoryArchive();
        reported.Spawn("source", "traveler", registry);
        var reportedMind = reported.Spawn("traveler", "traveler", registry);
        var sameHelp = reported.Append(MemoryKind.Help, "player", "traveler", 1, 9000, false,
            "policy.accepted-help", new RelationshipChange(trust: 1000, gratitude: 1713));
        check(reported.Perceive("source", sameHelp.Id, KnowledgeOrigin.Witnessed, 1, null) &&
              reported.Report("source", "traveler", sameHelp.Id, 2, personal: 9000),
            "policy-reported-help-has-real-source-acquisition");

        var directRelation = directMind.RelationshipTo("player");
        var heardRelation = reportedMind.RelationshipTo("player");
        check(directRelation.Trust == heardRelation.Trust &&
              directRelation.Gratitude == heardRelation.Gratitude &&
              directRelation.Trust == 450 && directRelation.Gratitude == 771 &&
              directMind.Known.Single().IsFact && !reportedMind.Known.Single().IsFact,
            "policy-same-relationship-different-provenance");

        var frame = new ObservationFrame(ActionSet.Of(ForgeAction.Travel, ForgeAction.Observe),
            new[] { new Observation(Feature.Trust, directRelation.Trust),
                    new Observation(Feature.Gratitude, directRelation.Gratitude) });
        var known = UtilityEvaluator.Decide(directMind.Controller, frame,
            directMind.DecisionState, 4, true, directMind.Cognition);
        var heard = UtilityEvaluator.Decide(reportedMind.Controller, frame,
            reportedMind.DecisionState, 4, true, reportedMind.Cognition);
        check(known.Action == ForgeAction.Observe && heard.Action == ForgeAction.Travel &&
              known.CanonicalTrace.Contains("personal=") && !heard.CanonicalTrace.Contains("personal="),
            "policy-known-vs-unknown");
    }

    private static ArenaSimulation ThreatFixture(bool direct)
    {
        var world = Village(direct ? 404UL : 405UL);
        var forge = world.Progression.Social.Forge;
        var guard = world.Npcs.Single(n => n.Id == 2);
        guard.Xmm = world.PlayerXmm + 900;
        guard.Zmm = world.PlayerZmm;
        guard.AttackCooldownTicks = 0;
        var assault = forge.Archive.Append(MemoryKind.Assault, "player", "ilyan",
            forge.ProcessedTicks, 9500, false, "policy.assault",
            new RelationshipChange(hostility: 10000, fear: 1000));
        if (direct)
        {
            if (!forge.Archive.Perceive("dorn", assault.Id, KnowledgeOrigin.Witnessed,
                    forge.ProcessedTicks, null, 3000))
                throw new InvalidOperationException("Direct acquisition rejected");
        }
        else
        {
            if (!forge.Archive.Perceive("ilyan", assault.Id, KnowledgeOrigin.Self,
                    forge.ProcessedTicks, null) ||
                !forge.Archive.Report("ilyan", "dorn", assault.Id, forge.ProcessedTicks,
                    personal: 6000))
                throw new InvalidOperationException("Provenance-preserving report rejected");
        }
        return world;
    }

    private static void IndirectThreat(Action<bool, string> check)
    {
        // Isolated archive fixture: the host's real witness/damage validation is
        // independently tested in NpcForgeIntegrationTests, not invented here.
        var witnessed = ThreatFixture(true);
        var reported = ThreatFixture(false);
        var directGuard = witnessed.Npcs.Single(n => n.Id == 2);
        var heardGuard = reported.Npcs.Single(n => n.Id == 2);
        check(directGuard.Mind.RelationshipTo("player").Hostility ==
              heardGuard.Mind.RelationshipTo("player").Hostility &&
              directGuard.Mind.RelationshipTo("player").Hostility >= NpcAgent.FleeHostility &&
              directGuard.Mind.Known.Single().IsFact &&
              !heardGuard.Mind.Known.Single().IsFact,
            "policy-direct-and-indirect-same-hostility-balance");
        check(directGuard.Mood == NpcDisposition.Hostile &&
              heardGuard.Mood != NpcDisposition.Hostile,
            "policy-indirect-no-hostile-attack");
        int initialHealth = reported.PlayerHealth;
        reported.Step();
        check(reported.PlayerHealth == initialHealth &&
              heardGuard.Mind.DecisionState.Action != ForgeAction.Attack,
            "policy-rumor-cannot-enter-attack-legal-mask");
    }

    private static void DangerAndNeeds(Action<bool, string> check)
    {
        var controller = ForgeDefaults.CreateRegistry().BindNew("traveler");
        var legal = ActionSet.Of(ForgeAction.Wait, ForgeAction.Travel, ForgeAction.Observe,
            ForgeAction.Flee, ForgeAction.Eat);
        var danger = new ObservationFrame(legal,
            new[] { new Observation(Feature.Danger, 10000),
                    new Observation(Feature.Hunger, 10000), new Observation(Feature.Food, 10000) });
        var hungry = new ObservationFrame(legal,
            new[] { new Observation(Feature.Hunger, 10000), new Observation(Feature.Food, 10000) });
        var locked = new ObservationFrame(ActionSet.Of(ForgeAction.Wait),
            new[] { new Observation(Feature.Danger, 10000) });
        check(UtilityEvaluator.Evaluate(controller, danger).Action == ForgeAction.Flee &&
              UtilityEvaluator.Evaluate(controller, hungry).Action == ForgeAction.Eat &&
              UtilityEvaluator.Evaluate(controller, locked).Action == ForgeAction.Wait,
            "policy-danger-and-needs-precedence");
        var downed = Village(406);
        var npc = downed.Npcs.Single(n => n.Id == 2);
        npc.Memory.Health = 0;
        npc.Memory.RecoveryTicks = 10;
        int hp = downed.PlayerHealth, food = downed.Progression.Social.CampProvisions;
        downed.Step();
        check(npc.Behavior == NpcBehavior.Downed &&
              downed.PlayerHealth == hp && downed.Progression.Social.CampProvisions == food,
            "policy-downed-recovery-precedes-utility");
    }

    private static void ScoreDoesNotMintResources(Action<bool, string> check)
    {
        var world = Village(407);
        var profile = world.Progression;
        string before = profile.SaveString();
        ulong hash = world.ComputeWorldHash();
        var controller = ForgeDefaults.CreateRegistry().BindNew("guard");
        var input = new ObservationFrame(ActionSet.Of(ForgeAction.Wait, ForgeAction.Flee,
            ForgeAction.Attack), new[] { new Observation(Feature.Danger, 10000) });
        for (int i = 0; i < 30; i++) UtilityEvaluator.Evaluate(controller, input);
        check(profile.SaveString() == before && world.ComputeWorldHash() == hash,
            "policy-no-created-resources");
    }

    private static void ExactControllerRestore(Action<bool, string> check)
    {
        var registry = ForgeDefaults.CreateRegistry();
        var archive = new MemoryArchive();
        var mind = archive.Spawn("traveler", "traveler", registry);
        var frame = new ObservationFrame(ActionSet.Of(ForgeAction.Wait, ForgeAction.Travel),
            Array.Empty<Observation>());
        UtilityEvaluator.Decide(mind.Controller, frame, mind.DecisionState, 8);
        string snapshot = archive.Save();
        var v2 = new ControllerArtifact(mind.Controller.Archetype, mind.Controller.ControllerId,
            2, mind.Controller.Rules);
        var admission = ControllerAdmission.Run(v2, "policy.issue4-v2",
            new[] { new RegressionCase(new ObservationFrame(ActionSet.Of(ForgeAction.Wait),
                Array.Empty<Observation>()), ForgeAction.Wait) });
        registry.Register(v2, v2.Hash, admission);
        bool loaded = MemoryArchive.TryLoad(snapshot, registry, out var old);
        check(loaded && old.Save() == snapshot &&
              old.Get("traveler").Controller.Version == 1 &&
              old.Get("traveler").Controller.Hash == mind.Controller.Hash &&
              old.Get("traveler").DecisionState.Action == mind.DecisionState.Action &&
              registry.BindNew("traveler").Version == 2,
            "policy-controller-exact-version-load");
    }

    private static void LongCoreReplay(Action<bool, string> check)
    {
        var a = Village(408);
        var b = Village(408);
        bool equal = a.ComputeWorldHash() == b.ComputeWorldHash();
        int mismatch = -1;
        for (int tick = 1; equal && tick <= 10000; tick++)
        {
            a.Step();
            b.Step();
            if (a.ComputeWorldHash() == b.ComputeWorldHash() &&
                a.Npcs.All(n => b.Npcs.Single(other => other.Id == n.Id).Behavior == n.Behavior))
                continue;
            mismatch = tick;
            equal = false;
        }
        check(equal && mismatch == -1 &&
              a.Progression.SaveString() == b.Progression.SaveString(),
            "policy-10000tick-replay");
    }
}
