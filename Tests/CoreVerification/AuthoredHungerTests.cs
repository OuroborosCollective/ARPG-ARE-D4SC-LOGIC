using System;
using System.Numerics;
using AreArpg.Core;

internal static class AuthoredHungerTests
{
    public static void Run(Action<bool,string> check)
    {
        var p=CharacterProgression.CreateEntryProfile();p.EnterVillage();p.Social.EnableForge();
        var w=new ArenaSimulation(71,p,BigInteger.One,LootCatalog.Version1);
        string before=p.SaveString();
        foreach(var id in new[]{null,"","0","HearIlyan","giveKit","setFlag","unknown"})
            check(!AuthoredHungerBinding.Execute(w,id).Accepted && p.SaveString()==before,"fadenwerk-denies-unbound-"+id);
        check(!AuthoredHungerBinding.Execute(w,"hear_ilyan").Accepted&&p.SaveString()==before,"fadenwerk-no-remote-story-action");
        var npc=w.Npcs.Find(n=>n.Id==0);npc.Xmm=w.PlayerXmm+1000;npc.Zmm=w.PlayerZmm;
        check(AuthoredHungerBinding.Execute(w,"hear_ilyan").Accepted&&p.HungerStage==1,"fadenwerk-real-local-story-action");
        string saved=p.SaveString();
        check(!AuthoredHungerBinding.Execute(w,"hear_ilyan").Accepted&&p.SaveString()==saved,"fadenwerk-no-duplicate-effect");
        check(CharacterProgression.TryLoad(saved,out var loaded)&&loaded.SaveString()==saved,"fadenwerk-existing-save-roundtrip");
        check(AuthoredHungerBinding.Status(loaded)=="active"&&AuthoredHungerBinding.TextStage(loaded)==1,"fadenwerk-progress-derived-after-load");
        var restored=new ArenaSimulation(71,loaded,BigInteger.One,LootCatalog.Version1);
        check(!AuthoredHungerBinding.Execute(restored,"hear_ilyan").Accepted&&loaded.SaveString()==saved,"fadenwerk-reload-no-second-grant");
        for(int i=0;i<40;i++){w.Step();restored.Step();check(w.ComputeWorldHash()==restored.ComputeWorldHash(),"fadenwerk-replay-"+i);}
    }
}
