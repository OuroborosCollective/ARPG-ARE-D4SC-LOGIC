using System;
using System.Linq;
using AreArpg.Core;
using AshenCrypt.MemoryForge;

internal static class ForgePopulationTests
{
    public static void Run(Action<bool,string> check)
    {
        var p=CharacterProgression.CreateEntryProfile();p.Social.EnableForge();
        var world=new ArenaSimulation(77,p,1,LootCatalog.Version1);
        check(world.Monsters.All(m=>m.Mind!=null&&m.Mind.Controller.ArchetypeId=="monster"),"forge-all-canonical-entry-spawns-bind-admitted-minds");
        var enemy=world.Monsters[0];enemy.Xmm=world.PlayerXmm+1000;enemy.Zmm=world.PlayerZmm;enemy.Health=enemy.MaxHealth=1000;
        check(world.ActivateSkill(0).Hits>0&&enemy.Mind.Known.Any(k=>k.Event.Kind==MemoryKind.Assault&&k.Origin==KnowledgeOrigin.Self),"forge-surviving-enemy-remembers-actual-hit");
        world.Step();check(enemy.Mind.DecisionState.LastDecision!=null,"forge-monster-uses-same-real-decision-engine");
        var copy=p.SaveString();check(CharacterProgression.TryLoad(copy,out var restored)&&restored.Social.Forge.Archive.Get(enemy.Mind.Id).Knowledge(enemy.Mind.Known[0].Event.Id)!=null,"forge-creature-identity-controller-and-threat-memory-restore");
        var unknown=new MonsterState{Id=999,Kind=99,Xmm=world.PlayerXmm+1000,Zmm=world.PlayerZmm,Health=100,MaxHealth=100};world.Monsters.Add(unknown);
        int hp=world.PlayerHealth;foreach(var other in world.Monsters.Where(m=>m!=unknown))other.Health=0;
        world.Step();check(unknown.Mind.Controller.ArchetypeId=="fallback"&&world.PlayerHealth==hp,"forge-unknown-creature-role-cannot-use-unguarded-attack");
        var village=CharacterProgression.CreateEntryProfile();village.EnterVillage();village.Social.EnableForge();var v=new ArenaSimulation(78,village,1,LootCatalog.Version1);
        var ilyan=v.Npcs[0];ilyan.Xmm=v.PlayerXmm+1000;ilyan.Zmm=v.PlayerZmm;
        check(v.ActOnHunger(HungerAction.HearIlyan).Accepted&&ilyan.Mind.Known.Any(k=>k.Event.Context=="story.hearilyan"&&k.Event.Essential),"forge-actual-secret-conversation-is-persistent-personal-history");
        int count=village.Social.Forge.Archive.Events.Count;check(!v.ActOnHunger(HungerAction.HearIlyan).Accepted&&village.Social.Forge.Archive.Events.Count==count,"forge-story-replay-cannot-duplicate-canonical-episode");
        check(village.Social.Forge.Archive.Get("mara").Known.Count==0&&village.Social.Forge.Archive.Get("dorn").Known.Count==0,"forge-private-story-conversation-is-not-world-knowledge");
        var animal=NpcForgeState.Registry.BindNew("animal");
        var calm=new ControllerArtifact(animal.Archetype,"test.calm-animal",1,new[]{new UtilityRule(ForgeAction.Wait,0,0,new int[(int)Feature.Count])});
        var input=new ObservationFrame(ActionSet.Of(ForgeAction.Wait,ForgeAction.Attack),new[]{new Observation(Feature.Danger,10000)});
        NpcForgeState.Registry.Register(calm,calm.Hash,ControllerAdmission.Run(calm,"test.calm",new[]{new RegressionCase(input,ForgeAction.Wait)}),false);
        var den=RattlerEncounterTests.Den();den.Progression.Social.EnableForge();den=new ArenaSimulation(123,den.Progression,1,LootCatalog.Version1);
        var rattler=den.Progression.Rattler.Creature;rattler.Xmm=den.PlayerXmm+1000;rattler.Zmm=den.PlayerZmm;rattler.BossWindupTicks=0;
        check(den.TryMigrateNpcController(rattler.Mind.Id,calm.ControllerId,calm.Version,calm.Hash),"forge-rattler-explicit-calm-policy-admitted");
        int beforeHealth=den.PlayerHealth;for(int i=0;i<20;i++)den.Step();
        check(rattler.Mind.DecisionState.Action==ForgeAction.Wait&&rattler.BossWindupTicks==0&&den.PlayerHealth==beforeHealth,"forge-rattler-must-execute-wait-decision-not-hidden-attack");
        var roadProfile=CharacterProgression.CreateEntryProfile();roadProfile.EnterVillage();roadProfile.DiscoverHungerClue();roadProfile.TravelHunger(3,false);roadProfile.Social.EnableForge();
        var road=new ArenaSimulation(123,roadProfile,1,LootCatalog.Version1);var encounter=roadProfile.Road;encounter.Started=true;encounter.Age=31;
        encounter.Bear.Xmm=HunterEncounter.HunterX+1000;encounter.Bear.Zmm=HunterEncounter.HunterZ;string bearId=encounter.Bear.Mind.Id;
        check(road.TryMigrateNpcController(bearId,calm.ControllerId,calm.Version,calm.Hash),"forge-road-bear-explicit-calm-policy-admitted");
        int hunterHealth=encounter.HunterHealth;for(int i=0;i<20;i++)road.Step();
        check(encounter.Bear.Mind.DecisionState.Action==ForgeAction.Wait&&encounter.HunterHealth==hunterHealth&&encounter.Bear.BossWindupTicks==0,"forge-road-bear-must-execute-its-admitted-decision");
        check(CharacterProgression.TryLoad(roadProfile.SaveString(),out var awayProfile),"forge-road-animal-policy-and-encounter-restore");
        awayProfile.TravelHunger(2,true);var away=new ArenaSimulation(123,awayProfile,1,LootCatalog.Version1);away.Step();
        check(awayProfile.Road.Bear.Mind?.Id==bearId&&awayProfile.Road.Bear.Mind.Controller.Hash==calm.Hash,"forge-offscreen-road-encounter-keeps-same-individual-controller");
    }
}
