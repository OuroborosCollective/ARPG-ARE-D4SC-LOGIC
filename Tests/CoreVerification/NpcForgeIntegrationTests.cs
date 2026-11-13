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
    }
}
