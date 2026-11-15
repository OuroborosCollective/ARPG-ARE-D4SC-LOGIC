using System;
using System.Linq;
using System.Numerics;
using AreArpg.Core;

/// <summary>Owner decision 2026-11-15: the arrival scene plays once, is recorded by the Core and never blocks advanced saves.</summary>
internal static class ArrivalSceneTests
{
    public static void Run(Action<bool,string> check)
    {
        check(ArrivalScene.Lines.Length>=5&&ArrivalScene.Lines.Any(l=>l.SpeakerId==0)&&ArrivalScene.Lines.Any(l=>l.SpeakerId==1)&&ArrivalScene.Lines.Any(l=>l.SpeakerId==2)&&
            ArrivalScene.Lines.All(l=>l.Text.Length>0&&l.Text.Length<=220),"arrival-scene-authored-lines-for-all-three");
        var p=CharacterProgression.CreateEntryProfile();p.EnterVillage();p.Social.EnableForge();
        var world=new ArenaSimulation(81,p,BigInteger.One,LootCatalog.Version1);
        check(world.ArrivalScenePending,"arrival-scene-pending-on-first-village-visit");
        check(world.CompleteArrivalScene()&&!world.ArrivalScenePending&&!world.CompleteArrivalScene(),"arrival-scene-completes-once");
        check(p.Social.Forge.Archive.Events.Count(e=>e.Context=="story.arrival.aschengrenze")==1&&
            new[]{"ilyan","mara","dorn"}.All(id=>p.Social.Forge.Archive.Get(id).Known.Count==0),"arrival-scene-recorded-without-touching-npc-memories");
        check(CharacterProgression.TryLoad(p.SaveString(),out var loaded)&&!new ArenaSimulation(81,loaded,BigInteger.One,LootCatalog.Version1).ArrivalScenePending,"arrival-scene-survives-load");

        // An older save that heard Ilyan's secret before the scene existed never gets the scene injected afterwards.
        var q=CharacterProgression.CreateEntryProfile();q.EnterVillage();
        var advanced=new ArenaSimulation(82,q,BigInteger.One,LootCatalog.Version1);
        var ilyan=advanced.Npcs.First(n=>n.Id==0);ilyan.Xmm=advanced.PlayerXmm+1000;ilyan.Zmm=advanced.PlayerZmm;
        check(advanced.ActOnHunger(HungerAction.HearIlyan).Accepted,"arrival-scene-older-save-heard-ilyan");
        q.Social.EnableForge();
        check(!new ArenaSimulation(82,q,BigInteger.One,LootCatalog.Version1).ArrivalScenePending,"arrival-scene-not-for-advanced-saves");

        // Without the memory owner there is nowhere to record it: never shown, so it can never loop.
        var r=CharacterProgression.CreateEntryProfile();r.EnterVillage();
        check(!new ArenaSimulation(83,r,BigInteger.One,LootCatalog.Version1).ArrivalScenePending,"arrival-scene-needs-recording-owner");
        // The crypt start is not the village.
        check(!new ArenaSimulation(84,CharacterProgression.CreateEntryProfile(),BigInteger.One,LootCatalog.Version1).ArrivalScenePending,"arrival-scene-not-in-crypt");
    }
}
