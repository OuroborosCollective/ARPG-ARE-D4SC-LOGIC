using System;
using System.Linq;
using System.Numerics;
using AreArpg.Core;
using AshenCrypt.MemoryForge;

/// <summary>Owner rule 2026-11-15: villagers calm down after a hostile act; the deed stays remembered; Mara's camp side task.</summary>
internal static class NpcCalmTests
{
    public static void Run(Action<bool,string> check)
    {
        var p=CharacterProgression.CreateEntryProfile();p.EnterVillage();p.Social.EnableForge();
        var world=new ArenaSimulation(91,p,BigInteger.One,LootCatalog.Version1);
        var ilyan=world.Npcs.First(n=>n.Id==0);
        ilyan.Xmm=world.PlayerXmm+1000;ilyan.Zmm=world.PlayerZmm;
        check(world.InteractNpc(0,NpcAction.Attack).Accepted,"calm-real-attack-accepted");
        var mind=p.Social.Forge.Archive.Get("ilyan");
        check(mind.RelationshipTo("player").Hostility==2500&&ilyan.Mood==NpcDisposition.Hostile,"calm-attack-makes-ilyan-hostile");
        for(int i=0;i<(int)ArenaSimulation.CalmIntervalTicks-20;i++)world.Step();
        check(mind.RelationshipTo("player").Hostility==2500,"calm-no-calming-before-interval");
        for(int i=0;i<40;i++)world.Step();
        check(mind.RelationshipTo("player").Hostility==1500&&mind.RelationshipTo("player").Fear==900,"calm-first-recorded-calming");
        check(ilyan.Mood!=NpcDisposition.Hostile&&mind.RelationshipTo("player").Trust==-2000&&p.Social.GetMemory(0).HostileActions==1,"calm-deed-stays-remembered");
        check(p.Social.Forge.Archive.Events.Count(e=>e.Context=="npc.calmed")==1,"calm-is-a-persisted-event");

        string saved=p.SaveString();
        check(CharacterProgression.TryLoad(saved,out var restored),"calm-save-loads");
        var loaded=new ArenaSimulation(91,restored,BigInteger.One,LootCatalog.Version1);
        check(world.ComputeWorldHash()==loaded.ComputeWorldHash(),"calm-world-readback");
        bool same=true;
        for(int i=0;i<(int)ArenaSimulation.CalmIntervalTicks+40;i++){world.Step();loaded.Step();same&=world.ComputeWorldHash()==loaded.ComputeWorldHash();}
        check(same,"calm-replay-after-load-includes-next-calming");
        for(int i=0;i<6000;i++)world.Step();
        var rel=mind.RelationshipTo("player");
        int calmEvents=p.Social.Forge.Archive.Events.Count(e=>e.Context=="npc.calmed");
        check(rel.Hostility==0&&rel.Fear==0&&calmEvents==3,"calm-terminates-at-zero-"+calmEvents);
        check(rel.Trust==-2000,"calm-never-restores-lost-trust");

        // Two attacks leave a legacy grudge that the save format keeps; calming sets it aside for contact and flight.
        var q=CharacterProgression.CreateEntryProfile();q.EnterVillage();q.Social.EnableForge();
        var twice=new ArenaSimulation(92,q,BigInteger.One,LootCatalog.Version1);
        var target=twice.Npcs.First(n=>n.Id==0);
        for(int hit=0;hit<2;hit++)
        {
            target.Xmm=twice.PlayerXmm+1000;target.Zmm=twice.PlayerZmm;target.InteractionCooldownTicks=0;
            for(int i=0;i<3;i++)twice.Step();
            target.Xmm=twice.PlayerXmm+1000;target.Zmm=twice.PlayerZmm;target.InteractionCooldownTicks=0;
            check(twice.InteractNpc(0,NpcAction.Attack).Accepted,"calm-second-profile-attack-"+hit);
        }
        check(q.Social.GetMemory(0).Disposition(target.Personality)==NpcDisposition.Hostile,"calm-two-attacks-legacy-grudge");
        target.Xmm=twice.PlayerXmm+1000;target.Zmm=twice.PlayerZmm;target.InteractionCooldownTicks=0;
        check(!twice.ActOnHunger(HungerAction.HearIlyan).Accepted,"calm-hostile-ilyan-refuses-story-contact");
        for(int i=0;i<(int)ArenaSimulation.CalmIntervalTicks*6;i++)twice.Step();
        check(q.Social.GetMemory(0).Disposition(target.Personality)==NpcDisposition.Hostile&&target.Mood==NpcDisposition.Wary,"calm-grudge-set-aside-not-erased");
        target.Xmm=twice.PlayerXmm+1000;target.Zmm=twice.PlayerZmm;target.InteractionCooldownTicks=0;
        check(twice.ActOnHunger(HungerAction.HearIlyan).Accepted&&q.HungerStage==1,"calm-main-quest-reachable-again");
        check(CharacterProgression.TryLoad(q.SaveString(),out var reloaded)&&new ArenaSimulation(92,reloaded,BigInteger.One,LootCatalog.Version1).Npcs.First(n=>n.Id==0).Mood==NpcDisposition.Wary,"calm-mood-survives-load");

        // A greeting eases a fresh grudge without erasing it.
        var g=CharacterProgression.CreateEntryProfile();g.EnterVillage();g.Social.EnableForge();
        var greet=new ArenaSimulation(93,g,BigInteger.One,LootCatalog.Version1);
        var near=greet.Npcs.First(n=>n.Id==0);
        near.Xmm=greet.PlayerXmm+1000;near.Zmm=greet.PlayerZmm;
        greet.InteractNpc(0,NpcAction.Attack);
        for(int i=0;i<12;i++)greet.Step();
        near.Xmm=greet.PlayerXmm+1000;near.Zmm=greet.PlayerZmm;near.InteractionCooldownTicks=0;
        check(greet.InteractNpc(0,NpcAction.Greet).Accepted&&g.Social.Forge.Archive.Get("ilyan").RelationshipTo("player").Hostility==2300,"calm-greeting-reconciles-slightly");

        // Mara's camp side task: exactly the missing rations, recorded once, then disabled.
        var c=CharacterProgression.CreateEntryProfile();c.EnterVillage();c.Social.EnableForge();
        var camp=new ArenaSimulation(94,c,BigInteger.One,LootCatalog.Version1);
        var mara=camp.Npcs.First(n=>n.Id==SocialLedger.MaraNpcId);
        int missing=SocialLedger.CampCapacity-c.Social.CampProvisions;
        check(missing==2&&camp.CanDonateCampRation&&!c.Social.CampRefilled,"camp-side-task-open");
        SocialResult last=null;
        for(int i=0;i<missing;i++)
        {
            mara.Xmm=camp.PlayerXmm+1000;mara.Zmm=camp.PlayerZmm;mara.InteractionCooldownTicks=0;
            last=camp.DonateCampRation();
            check(last.Accepted,"camp-donation-"+i);
        }
        check(c.Social.CampRefilled&&last.Text.Contains("Nebenaufgabe erledigt"),"camp-side-task-completed-with-thanks");
        mara.InteractionCooldownTicks=0;
        var provisions=c.Social.Provisions;
        check(!camp.CanDonateCampRation&&!camp.DonateCampRation().Accepted&&c.Social.Provisions==provisions,"camp-full-disables-and-consumes-nothing");
        check(c.Social.Forge.Archive.Events.Count(e=>e.Context=="quest.camp.refilled")==1,"camp-completion-recorded-once");
        check(CharacterProgression.TryLoad(c.SaveString(),out var campLoaded)&&campLoaded.Social.CampRefilled,"camp-completion-survives-load");
    }
}
