using System;
using System.Linq;
using System.Numerics;
using AreArpg.Core;
using AshenCrypt.MemoryForge;

internal static class NpcForgeIntegrationTests
{
    public static void Run(Action<bool,string> check)
    {
        var p=CharacterProgression.CreateEntryProfile();p.EnterVillage();p.Social.EnableForge();
        var world=new ArenaSimulation(71,p,BigInteger.One,LootCatalog.Version1);
        var ilyan=world.Npcs.First(n=>n.Id==0);
        // Explicit contact fixture; the real accepted transaction and simulation are under test.
        ilyan.Xmm=world.PlayerXmm+1000;ilyan.Zmm=world.PlayerZmm;
        check(world.InteractNpc(0,NpcAction.Help).Accepted,"forge-real-ilyan-provisions-help");
        var mind=p.Social.Forge.Archive.Get("ilyan");
        check(mind.Known.Count==1&&mind.Known[0].Event.Kind==MemoryKind.Help&&mind.RelationshipTo("player").Gratitude==2000,"forge-real-help-observation-memory-relationship");
        check(p.Social.GetMemory(0).HelpfulActions==1&&p.Social.GetMemory(0).PositivePoints==8&&p.Social.Provisions==2,"forge-real-help-not-double-rewarded");
        world.Step();
        check(mind.DecisionState.Action==ForgeAction.Observe,"forge-gratitude-changes-later-core-action");
        string saved=p.SaveString();
        check(saved.StartsWith("AC17|")&&CharacterProgression.TryLoad(saved,out var restored),"forge-ac17-verified-save");
        CharacterProgression.TryLoad(saved,out restored);
        var loaded=new ArenaSimulation(71,restored,BigInteger.One,LootCatalog.Version1);
        check(world.ComputeWorldHash()==loaded.ComputeWorldHash(),"forge-village-full-world-readback");
        for(int i=0;i<40;i++)
        {
            world.Step();loaded.Step();
            check(world.ComputeWorldHash()==loaded.ComputeWorldHash(),"forge-replay-after-load-"+i);
        }
        var greeting=loaded.InteractNpc(0,NpcAction.Greet);
        check(greeting.Accepted&&greeting.Text.Contains("Proviant"),"forge-later-greeting-remembers-specific-help");
        check(restored.Social.Forge.Archive.Get("mara").Known.Count==0,"forge-actual-help-not-globally-known");
        foreach(bool hidden in new[]{false,true})
        {
            var crimeProfile=CharacterProgression.CreateEntryProfile();crimeProfile.EnterVillage();crimeProfile.Social.EnableForge();
            var crime=new ArenaSimulation(72,crimeProfile,BigInteger.One,LootCatalog.Version1);
            // Legal positions on either side of a real village building; only the Core sight query decides witnessing.
            Move(crime,0,-8700);Move(crime,-9000,-8700);
            var merchant=crime.Npcs[1];merchant.Xmm=-8000;merchant.Zmm=-8700;
            var guardNpc=crime.Npcs[2];guardNpc.Xmm=hidden?-16000:-7000;guardNpc.Zmm=-8700;
            check(crime.TakeCampRation().Accepted,"forge-local-theft-transaction-"+hidden);
            check(merchant.Mind.Known.Count==1&&merchant.Mind.Known[0].Origin==KnowledgeOrigin.Self,"forge-victim-remembers-actual-theft-"+hidden);
            check(guardNpc.Mind.Known.Count==(hidden?0:1),"forge-building-occlusion-gates-personal-knowledge-"+hidden);
            if(!hidden)check(guardNpc.Mind.Known[0].Origin==KnowledgeOrigin.Witnessed,"forge-guard-witness-provenance");
            var token=crimeProfile.SaveString();
            check(!crime.TakeCampRation().Accepted&&crimeProfile.SaveString()==token,"forge-rejected-repeat-produces-no-memory-"+hidden);
            check(CharacterProgression.TryLoad(token,out var afterCrime)&&afterCrime.Social.Forge.Archive.Get("dorn").Known.Count==(hidden?0:1),"forge-witness-versus-occluded-state-persists-"+hidden);
            for(int i=0;i<15;i++)crime.Step();
            merchant.Xmm=crime.PlayerXmm+1000;merchant.Zmm=crime.PlayerZmm;guardNpc.Xmm=hidden?-16000:-7000;guardNpc.Zmm=-8700;
            int prior=guardNpc.Mind.Known.Count;
            check(crime.InteractNpc(1,NpcAction.Rob).Accepted,"forge-personal-robbery-accepted-"+hidden);
            check(guardNpc.Mind.Known.Count==prior+(hidden?0:1),"forge-personal-robbery-respects-actual-building-sight-"+hidden);
        }
    }
    private static void Move(ArenaSimulation world,long x,long z)
    {
        for(int i=0;i<200;i++)
        {
            long dx=x-world.PlayerXmm,dz=z-world.PlayerZmm;
            if(Math.Abs(dx)<500&&Math.Abs(dz)<500){world.SetMoveInput(0,0);return;}
            world.SetMoveInput(Math.Abs(dx)<500?0:Math.Sign(dx)*10000,Math.Abs(dz)<500?0:Math.Sign(dz)*10000);world.Step();
        }
        throw new Exception("Witness test route failed");
    }
}
