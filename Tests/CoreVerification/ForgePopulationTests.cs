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
    }
}
