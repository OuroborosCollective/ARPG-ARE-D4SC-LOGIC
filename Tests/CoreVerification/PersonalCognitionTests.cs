using System;
using System.Linq;
using System.Numerics;
using System.Text;
using AreArpg.Core;
using AshenCrypt.MemoryForge;

internal static class PersonalCognitionTests
{
    private static readonly NpcArchetypeControllerRegistry Registry = ForgeDefaults.CreateRegistry();

    public static void Run(Action<bool,string> check)
    {
        Wedding(check);
        CapacityAndProvenance(check);
        LiveNeedsAndDecisions(check);
        SocialLearning(check);
        UpgradeReport(check);
        CommittedHits(check);
        EncounterWindups(check);
        GameClockAndVisibleMemory(check);
        FutureResidents(check);
    }

    private static void Wedding(Action<bool,string> check)
    {
        var archive = new MemoryArchive();
        var guest = archive.Spawn("mara", "merchant", Registry);
        var listener = archive.Spawn("dorn", "guard", Registry);
        var absent = archive.Spawn("absent", "villager", Registry);
        const ulong start = 21, day = WorldSkyClock.TicksPerDay;
        var ev = archive.Append(MemoryKind.SemanticClaim, "king", "queen", start, 9500, true, "married", new RelationshipChange());
        var words = new EpisodeDetails("meal", "royal-court", "flowers-and-eagle-cutlet");
        check(archive.Perceive("mara", ev.Id, KnowledgeOrigin.Witnessed, start, words), "wedding-direct-witness");
        check(guest.Cognition.Recall(ev.Id, start).Details.Detail == words.Detail && absent.Cognition.Recall(ev.Id, start) == null, "wedding-details-only-for-guest");
        check(archive.Report("mara", "dorn", ev.Id, start + 10), "wedding-one-hop-report");
        var heard = listener.Cognition.Recall(ev.Id, start + 10);
        check(heard.Origin == KnowledgeOrigin.Reported && heard.Source == "mara" && heard.Stage == RecallStage.Gist && heard.Details.Place == "", "wedding-hearsay-keeps-source-without-sensory-access");
        var replay = Restore(archive);
        foreach (var a in new[]{archive, replay})
        {
            a.AdvancePersonalTime(start + day - 1);
            check(a.Get("mara").Cognition.Recall(ev.Id, start + day - 1).Stage == RecallStage.Episode, "wedding-day-one-minus-one");
            a.AdvancePersonalTime(start + day);
            var gist = a.Recollect("mara", ev.Id, start + day);
            check(gist.Stage == RecallStage.Gist && gist.Details.Category == "meal" && gist.Details.Detail == "" && gist.Details.Place == "", "wedding-day-one-only-food");
            string payload = Payload(a);
            check(!payload.Contains("royal-court") && !payload.Contains("flowers-and-eagle-cutlet") && payload.Contains("meal"), "wedding-discarded-details-absent-from-save");
            a.AdvancePersonalTime(start + 3*day - 1);
            check(a.Recollect("mara",ev.Id,start+3*day-1).Details.Category == "meal", "wedding-last-tick-gist");
            a.AdvancePersonalTime(start + 3*day);
            var fact = a.Get("mara").Cognition.Recall(ev.Id,start+3*day);
            check(fact.Stage == RecallStage.Fact && fact.Subject == "king" && fact.Predicate == "married" && fact.Object == "queen" && fact.Details == null, "wedding-day-three-only-marriage");
            check(a.Get("dorn").Cognition.Recall(ev.Id,start+3*day).Origin == KnowledgeOrigin.Reported && !Payload(a).Contains("meal"), "wedding-no-report-refresh-or-hearsay-promotion");
            check(Restore(a).Get("mara").Cognition.Recall(ev.Id,start+3*day).Details == null, "wedding-no-detail-resurrection-on-load");
        }
        check(archive.Save() == replay.Save(), "wedding-identical-replay-bytes");
        var repeated = new MemoryArchive();
        var m = repeated.Spawn("guest","villager",Registry);
        var e = repeated.Append(MemoryKind.SemanticClaim,"king","queen",0,9000,true,"married",new RelationshipChange());
        repeated.Perceive(m.Id,e.Id,KnowledgeOrigin.Witnessed,0,words);
        ulong hard = 3 * day;
        foreach (ulong t in new[] {day-1,2*day-2,hard-3})
            check(repeated.Recollect(m.Id,e.Id,t).Stage == RecallStage.Episode,"wedding-active-rehearsal-before-cutoff");
        repeated.AdvancePersonalTime(hard);
        check(m.Cognition.Recall(e.Id,hard).Stage == RecallStage.Fact && !Payload(repeated).Contains("eagle-cutlet"),"wedding-hard-cutoff-despite-rehearsal");
    }
    private static string Payload(MemoryArchive a) => Encoding.UTF8.GetString(Convert.FromBase64String(a.Save().Split('.')[1]));
    private static MemoryArchive Restore(MemoryArchive a)
    {
        if (!MemoryArchive.TryLoad(a.Save(),Registry,out var restored) || restored.Save()!=a.Save()) throw new Exception("Personal archive roundtrip failed");
        return restored;
    }
    private static void CapacityAndProvenance(Action<bool,string> check)
    {
        var a = new MemoryArchive();
        var mind = a.Spawn("guest","villager",Registry); a.Spawn("listener","villager",Registry);
        var greeting = a.Append(MemoryKind.Greeting,"guest","player",0,10,false,"encounter",new RelationshipChange());
        a.Learn("guest",greeting.Id,KnowledgeOrigin.Self,"guest",0);
        for(int i=0;i<40;i++)
        {
            var e=a.Append(MemoryKind.SemanticClaim,"house","person."+i,i==0?0UL:(ulong)i,8000,true,"joined",new RelationshipChange());
            a.Perceive("guest",e.Id,KnowledgeOrigin.Witnessed,(ulong)i,new EpisodeDetails("meal","hall","bread"));
        }
        check(mind.Cognition.FactCount==PersonalCognition.FactCapacity && mind.Cognition.EpisodeCount==PersonalCognition.EpisodeCapacity, "personal-capacities-under-40-events");
        check(!a.Report("guest","listener",greeting.Id,PersonalCognition.DetailLimit) && mind.Cognition.Recall(greeting.Id,PersonalCognition.DetailLimit)==null, "forgotten-trivia-cannot-be-relayed");
        var last=a.Events.Last();
        check(a.Report("guest","listener",last.Id,PersonalCognition.DetailLimit+40) && a.Learn("listener",last.Id,KnowledgeOrigin.Refuted,"listener",PersonalCognition.DetailLimit+41), "personal-hearsay-can-be-refuted");
        check(a.Get("listener").Cognition.Recall(last.Id,PersonalCognition.DetailLimit+42)==null && !a.Learn("guest",last.Id,KnowledgeOrigin.Refuted,"guest",PersonalCognition.DetailLimit+42), "refutation-removes-claim-and-cannot-overwrite-witness");
    }

    private static ArenaSimulation Village()
    {
        var p=CharacterProgression.CreateEntryProfile();p.EnterVillage();p.Social.EnableForge();
        var world = new ArenaSimulation(71,p,BigInteger.One,LootCatalog.Version1);
        if (!world.CompleteArrivalScene()) throw new InvalidOperationException("Personal cognition fixture must finish the fixed arrival");
        return world;
    }
    private static void LiveNeedsAndDecisions(Action<bool,string> check)
    {
        var world=Village();
        var p=world.Progression;var f=p.Social.Forge;
        var actor=world.Npcs.Single(n=>n.Id==0);
        var peer=f.BindResident("peer",actor.Mind.Controller.ArchetypeId);
        actor.Memory.Hunger=0;actor.Memory.Fatigue=900;
        int fatigue=actor.Memory.Fatigue;
        for(int i=0;i<12;i++)world.Step();
        check(actor.Memory.Fatigue<fatigue && actor.Mind.Cognition.Weight(ForgeAction.Rest,1)>0 && peer.Cognition.Weight(ForgeAction.Rest,1)==0, "real-rest-improves-needs-and-only-own-weight");
        var mara=world.Npcs.Single(n=>n.Id==1);mara.Memory.Hunger=960;mara.Xmm=ArenaSimulation.CampXmm;mara.Zmm=ArenaSimulation.CampZmm;
        int camp=p.Social.CampProvisions;
        for(int i=0;i<15;i++)world.Step();
        check(p.Social.CampProvisions<camp && mara.Memory.Hunger<700 && mara.Mind.Cognition.Weight(ForgeAction.Eat,0)>0, "actual-food-debit-and-nutrition-train-eat");
        var borderline=new ObservationFrame(ActionSet.Of(ForgeAction.Travel,ForgeAction.Rest),new[]{new Observation(Feature.Fatigue,624)});
        var learned=UtilityEvaluator.Decide(actor.Mind.Controller,borderline,actor.Mind.DecisionState,f.ProcessedTicks,true,actor.Mind.Cognition);
        var untrained=UtilityEvaluator.Decide(peer.Controller,borderline,peer.DecisionState,f.ProcessedTicks,true,peer.Cognition);
        check(learned.Action==ForgeAction.Rest && untrained.Action==ForgeAction.Travel && learned.CanonicalTrace.Contains("personal="), "two-travelers-same-options-different-actions-from-experience");
        var revision=f.Archive.Save();
        check(!actor.Mind.Cognition.RecordOutcome(ForgeAction.Eat,f.ProcessedTicks,8) && !actor.Mind.Cognition.RecordOutcome(ForgeAction.Rest,f.ProcessedTicks,0) && revision==f.Archive.Save(), "no-learning-from-illegal-or-ineffective-action");
        var onlyWait=new ObservationFrame(ActionSet.Of(ForgeAction.Wait),Array.Empty<Observation>());
        check(UtilityEvaluator.Decide(actor.Mind.Controller,onlyWait,actor.Mind.DecisionState,f.ProcessedTicks,true,actor.Mind.Cognition).Action==ForgeAction.Wait, "learned-weight-cannot-bypass-host-legality");
        string save=p.SaveString();
        check(CharacterProgression.TryLoad(save,out var after) && after.SaveString()==save && after.Social.Forge.Archive.Get("mara").Cognition.Weight(ForgeAction.Eat,0)==mara.Mind.Cognition.Weight(ForgeAction.Eat,0),"live-save-preserves-trained-matrix");
    }
    private static void UpgradeReport(Action<bool,string> check)
    {
        foreach (bool directlyPerceived in new[] { false, true })
        {
            var a=new MemoryArchive();
            a.Spawn("first","villager",Registry); var listener=a.Spawn("later","villager",Registry);
            var e=a.Append(MemoryKind.SemanticClaim,"king","queen",10,9500,true,"married",new RelationshipChange());
            a.Perceive("first",e.Id,KnowledgeOrigin.Witnessed,10,new EpisodeDetails("meal","court","flowers"));
            check(a.Report("first","later",e.Id,12),"upgrade-listener-hears-meal");
            bool upgraded=directlyPerceived ? a.Perceive("later",e.Id,KnowledgeOrigin.Witnessed,18,null) :
                a.Learn("later",e.Id,KnowledgeOrigin.Witnessed,"later",18);
            check(upgraded && MemoryArchive.TryLoad(a.Save(),Registry,out var restored) &&
                restored.Save()==a.Save(),"upgrade-direct-confirmation-without-new-senses-reloads-"+directlyPerceived);
            var recall=listener.Cognition.Recall(e.Id,18);
            check(recall.Origin==KnowledgeOrigin.Witnessed && recall.Details==null,"upgrade-heard-menu-never-becomes-witnessed-menu");
        }
    }
    private static void SocialLearning(Action<bool,string> check)
    {
        var a=new MemoryArchive();var recipient=a.Spawn("mara","merchant",Registry);var guard=a.Spawn("dorn","guard",Registry);var distant=a.Spawn("other","guard",Registry);
        var assault=a.Append(MemoryKind.Assault,"player","mara",2,9000,false,"encounter",new RelationshipChange(fear:1500,hostility:2500));
        var senses=new EpisodeDetails("harm","market","raised-fist");
        a.Perceive("mara",assault.Id,KnowledgeOrigin.Self,2,senses);
        a.Perceive("dorn",assault.Id,KnowledgeOrigin.Witnessed,2,senses,6000);
        int learned=guard.Cognition.Weight(ForgeAction.Attack,2);
        check(learned>0 && recipient.Cognition.Weight(ForgeAction.Flee,2)>0 && distant.Cognition.Weight(ForgeAction.Attack,2)==0,"real-attack-causes-distinct-role-and-witness-responses");
        check(!a.Perceive("dorn",assault.Id,KnowledgeOrigin.Witnessed,2,senses,6000) && learned==guard.Cognition.Weight(ForgeAction.Attack,2),"duplicate-contact-does-not-train-twice");
        check(a.Report("dorn","other",assault.Id,3) && distant.Cognition.Weight(ForgeAction.Attack,2)==0 && NpcSpeech.Personal(distant.Cognition.SocialRecall("player",3))==SpeechLine.HeardOfHarm,"hearsay-speaks-as-hearsay-without-sensory-reward");
        check(Restore(a).Get("dorn").Cognition.Weight(ForgeAction.Attack,2)==learned,"social-conditioning-reloads-once");
    }
    private static void GameClockAndVisibleMemory(Action<bool,string> check)
    {
        var live=Village(); var f=live.Progression.Social.Forge;
        var ilyan=live.Npcs.Single(n=>n.Id==0);
        ilyan.Xmm=live.PlayerXmm+900; ilyan.Zmm=live.PlayerZmm;
        check(live.InteractNpc(0,NpcAction.Help).Accepted,"clock-real-provision-start");
        var ev=f.Archive.Events.Last();
        for (int tick=0;tick<(int)PersonalCognition.DetailLimit-1;tick++) live.Step();
        var before=ilyan.Mind.Cognition.Recall(ev.Id,f.ProcessedTicks);
        check(before.Stage==RecallStage.Gist && before.Details.Category=="food","clock-43199-ticks-gist-survives-idle-npc");
        string save=live.Progression.SaveString();
        check(CharacterProgression.TryLoad(save,out var restored),"clock-checkpoint-before-last-tick");
        var replay=new ArenaSimulation(71,restored,BigInteger.One,LootCatalog.Version1);
        live.Step(); replay.Step();
        check(f.ProcessedTicks==ev.Tick+43200 && ilyan.Mind.Cognition.EpisodeCount==0 &&
            replay.Progression.SaveString()==live.Progression.SaveString() && replay.ComputeWorldHash()==live.ComputeWorldHash(),"clock-43200-detail-purged-with-identical-world-replay");
        ilyan.Xmm=live.PlayerXmm+900; ilyan.Zmm=live.PlayerZmm;
        var spoken=live.InteractNpc(0,NpcAction.Greet);
        check(spoken.Accepted && spoken.FollowupSpeech==SpeechLine.RememberedHelp && !spoken.Text.Contains("Proviant"),"clock-remembered-help-without-inventing-menu");
    }
    private static void CommittedHits(Action<bool,string> check)
    {
        foreach (bool pulse in new[]{false,true})
        foreach (bool dodge in new[]{false,true})
        {
            var p=new CharacterProgression();p.Social.EnableForge();
            var world=new ArenaSimulation(0xB055UL,p,new BigInteger(pulse?2:1));
            var enemy=pulse ? world.Monsters.Single(m=>m.Kind==3) :
                MonsterCatalog.Spawn("ork.warrior", 1,world.PlayerXmm+500,world.PlayerZmm,1);
            world.Monsters.Clear();world.Monsters.Add(enemy);
            enemy.Xmm=world.PlayerXmm+500; enemy.Zmm=world.PlayerZmm;
            for (int i=0;i<60 && enemy.BossWindupTicks==0;i++) world.Step();
            check(enemy.BossWindupTicks>0 && enemy.Mind.Cognition.Weight(ForgeAction.Attack,2)==0,"starting-warning-earns-no-credit-"+pulse);
            if(dodge) enemy.Xmm=world.PlayerXmm+6500;
            int health=world.PlayerHealth, delay=enemy.BossWindupTicks;
            ulong decisionTick=enemy.Mind.DecisionState.LastTick;
            for(int i=0;i<delay;i++)world.Step();
            check(enemy.Mind.DecisionState.LastTick==decisionTick,"windup-keeps-original-decision-"+pulse+"-"+dodge);
            int weight=enemy.Mind.Cognition.Weight(ForgeAction.Attack,2);
            check(enemy.BossWindupTicks==0 && (dodge ? world.PlayerHealth==health && weight==0 : world.PlayerHealth<health && weight>0),
                  "settled-hit-learns-once-and-dodged-hit-never-earns-credit-"+pulse+"-"+dodge);
            check(CharacterProgression.TryLoad(p.SaveString(),out var loaded) &&
                loaded.Social.Forge.Archive.Get(enemy.Mind.Id).Cognition.Weight(ForgeAction.Attack,2)==weight,"settled-outcome-reloads-"+pulse+"-"+dodge);
        }
    }
    private static void EncounterWindups(Action<bool,string> check)
    {
        var road=HunterEncounterTests.Road(123,true);
        var h=road.Progression.Road; h.Started=true; h.Age=31;
        h.Bear.Xmm=HunterEncounter.HunterX+500;h.Bear.Zmm=HunterEncounter.HunterZ;
        var p=RattlerEncounterTests.Den().Progression;p.Social.EnableForge();
        var den=new ArenaSimulation(123,p,1,LootCatalog.Version1);
        var r=p.Rattler.Creature;r.Xmm=den.PlayerXmm+500;r.Zmm=den.PlayerZmm;
        foreach (var world in new[]{road,den})
        {
            bool bear=world==road;
            var creature=bear?h.Bear:r;
            for(int i=0;i<65 && creature.BossWindupTicks==0;i++)world.Step();
            check(creature.BossWindupTicks>0,"encounter-real-windup-started-"+bear);
            ulong issued=creature.Mind.DecisionState.LastTick;
            int prior=bear?h.HunterHealth:den.PlayerHealth;
            int delay=creature.BossWindupTicks;
            for(int i=0;i<delay;i++)world.Step();
            check(creature.Mind.DecisionState.LastTick==issued && creature.Mind.Cognition.Weight(ForgeAction.Attack,2)>0 &&
                (bear?h.HunterHealth:den.PlayerHealth)<prior,"hunter-and-rattler-impact-credit-original-frame-"+bear);
        }
    }
    private static void FutureResidents(Action<bool,string> check)
    {
        var world=Village();world.CompleteMainQuest();
        var f=world.Progression.Social.Forge;
        var resident=world.VillageResident(3);
        var newcomer=f.BindResident("new.apprentice","unregistered-role");
        check(resident==world.VillageResident(3) && newcomer.Controller.ArchetypeId=="fallback" && newcomer.Cognition!=resident.Cognition && f.Archive.Minds.All(n=>n.Cognition!=null),"one-mind-per-born-or-future-resident-even-unknown-role");
        check(CharacterProgression.TryLoad(world.Progression.SaveString(),out var restored) && restored.Social.Forge.Archive.Get(newcomer.Id)!=null, "future-identities-survive-game-save");
    }
}
