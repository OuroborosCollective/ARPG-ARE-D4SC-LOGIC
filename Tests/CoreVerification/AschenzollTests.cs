using System;
using AreArpg.Core;

internal static class AschenzollTests
{
    internal static ArenaSimulation Station()
    {
        var forest=WoodlandTests.Enter();
        var p=forest.Progression;
        // Authored station fixture; subsequent movement, interactions and saves use production paths.
        p.Woodland.ReachedAschenzoll=true;p.Woodland.NeedsNewRun=true;
        p.TravelHunger(10,false);p.Social.EnableForge();
        return new ArenaSimulation(71,p,1,LootCatalog.Version1);
    }
    public static void Run(Action<bool,string> check)
    {
        var w=Station();HunterEncounterTests.Move(w,-5000,-12000);
        check(w.ActOnHunger((HungerAction)34).Accepted,"aschentoll-read-real-station-notice");
    }
}
