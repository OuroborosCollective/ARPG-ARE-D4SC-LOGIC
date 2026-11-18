using System;
using System.Linq;
using AreArpg.Core;

/// <summary>Region network: stable ids, story-driven unlock, transitions, per-region state and enemy return.</summary>
internal static class RegionNetworkTests
{
    private static CharacterProgression At(int act)
    {
        var p = CharacterProgression.CreateEntryProfile();
        if (act >= 3) p.TravelHunger(act, false);
        return p;
    }

    public static void Run(Action<bool, string> require)
    {
        // Stable ids follow the story arc and the world bible.
        require(RegionNetwork.Aschengrenze == "region.aschengrenze.v1", "region-id-village");
        require(RegionNetwork.Aschenstrasse == "region.aschenstrasse.v1", "region-id-road");
        require(RegionNetwork.AlteMuehle == "region.alte-muehle.v1", "region-id-mill");
        require(RegionNetwork.AlterSpeicher == "region.alter-speicher.v1", "region-id-storehouse");
        require(RegionNetwork.VerborgeneKrypta == "region.verborgene-krypta.v1", "region-id-crypt");
        require(RegionNetwork.Rattlernest == "region.rattlernest.v1", "region-id-rattler");
        require(RegionNetwork.Waldweg == "region.waldweg.v1", "region-id-forest");
        require(RegionNetwork.Baerenhoehle == "region.baerenhoehle.v1", "region-id-bear");
        require(RegionNetwork.Aschenzoll == "region.aschenzoll.v1", "region-id-customs");
        require(RegionNetwork.Reise == "region.reise-jenseits-aschenzoll.v1", "region-id-journey");

        // Every story act maps to exactly one current region.
        require(RegionNetwork.RegionForAct(0) == RegionNetwork.Aschengrenze, "region-act0-village");
        require(RegionNetwork.RegionForAct(1) == RegionNetwork.Aschengrenze, "region-act1-village");
        require(RegionNetwork.RegionForAct(2) == RegionNetwork.Aschengrenze, "region-act2-village");
        require(RegionNetwork.RegionForAct(3) == RegionNetwork.Aschenstrasse, "region-act3-road");
        require(RegionNetwork.RegionForAct(4) == RegionNetwork.AlteMuehle, "region-act4-mill");
        require(RegionNetwork.RegionForAct(5) == RegionNetwork.AlterSpeicher, "region-act5-storehouse");
        require(RegionNetwork.RegionForAct(6) == RegionNetwork.VerborgeneKrypta, "region-act6-crypt");
        require(RegionNetwork.RegionForAct(7) == RegionNetwork.Rattlernest, "region-act7-rattler");
        require(RegionNetwork.RegionForAct(8) == RegionNetwork.Waldweg, "region-act8-forest");
        require(RegionNetwork.RegionForAct(9) == RegionNetwork.Baerenhoehle, "region-act9-bear");
        require(RegionNetwork.RegionForAct(10) == RegionNetwork.Aschenzoll, "region-act10-customs");

        // Unlock follows story progress; the village stays unlocked and every earlier region stays reachable.
        var fresh = new CharacterProgression();
        require(fresh.Regions.UnlockedRegions(0).SequenceEqual(new[] { RegionNetwork.Aschengrenze }),
            "region-fresh-only-village");
        var forest = fresh.Regions.UnlockedRegions(8).ToArray();
        require(forest.Contains(RegionNetwork.Waldweg) && forest.Contains(RegionNetwork.Aschengrenze) &&
            !forest.Contains(RegionNetwork.Baerenhoehle), "region-act8-unlocks-up-to-forest");
        require(fresh.Regions.UnlockedRegions(10).Count() == 9, "region-act10-unlocks-nine");
        require(!fresh.Regions.UnlockedRegions(10).Contains(RegionNetwork.Reise), "region-journey-locked-without-travel");

        // Transitions: hub reachable from anywhere, forward/backward along the chain, never into a locked region.
        var net = fresh.Regions;
        require(net.CanTransition(RegionNetwork.Aschengrenze, RegionNetwork.Waldweg, 8), "region-hub-to-unlocked");
        require(net.CanTransition(RegionNetwork.Waldweg, RegionNetwork.Aschengrenze, 8), "region-return-to-hub");
        require(net.CanTransition(RegionNetwork.AlteMuehle, RegionNetwork.Waldweg, 8), "region-chain-forward");
        require(net.CanTransition(RegionNetwork.Waldweg, RegionNetwork.AlteMuehle, 8), "region-chain-backward");
        require(!net.CanTransition(RegionNetwork.Waldweg, RegionNetwork.Baerenhoehle, 8), "region-locked-target-refused");
        require(!net.CanTransition(RegionNetwork.Waldweg, RegionNetwork.Reise, 8), "region-non-adjacent-refused");
        require(!net.CanTransition(RegionNetwork.Aschenzoll, RegionNetwork.Aschenzoll, 10), "region-self-transition-refused");

        // Enter marks visited; a cleared region returns its enemies on the next entry, reproducibly.
        require(net.Enter(RegionNetwork.Waldweg, 8), "region-enter-unlocked");
        require(!net.Enter(RegionNetwork.Baerenhoehle, 8), "region-enter-locked-refused");
        require(net.IsVisited(RegionNetwork.Waldweg), "region-visited-recorded");
        require(!net.EnemiesReturn(RegionNetwork.Waldweg), "region-enemies-stay-down-first-visit");
        require(net.Clear(RegionNetwork.Waldweg), "region-clear-recorded");
        require(net.EnemiesReturn(RegionNetwork.Waldweg), "region-enemies-return-flagged-after-clear");
        require(net.ReturnRun(RegionNetwork.Waldweg) == 0, "region-return-run-zero-before-reentry");
        require(net.Enter(RegionNetwork.Waldweg, 8), "region-reentry-allowed");
        require(net.ReturnRun(RegionNetwork.Waldweg) == 1, "region-return-run-counted");
        require(!net.EnemiesReturn(RegionNetwork.Waldweg), "region-enemies-active-after-return");
        net.Clear(RegionNetwork.Waldweg);
        net.Enter(RegionNetwork.Waldweg, 8);
        require(net.ReturnRun(RegionNetwork.Waldweg) == 2, "region-second-return-counted");
        var clone = new CharacterProgression().Regions;
        clone.Enter(RegionNetwork.Waldweg, 8); clone.Clear(RegionNetwork.Waldweg); clone.Enter(RegionNetwork.Waldweg, 8);
        clone.Clear(RegionNetwork.Waldweg); clone.Enter(RegionNetwork.Waldweg, 8);
        require(clone.Token() == net.Token(), "region-state-reproducible");
        require(net.SetEvent(RegionNetwork.Waldweg, 2) && net.Events(RegionNetwork.Waldweg) == 2, "region-event-recorded");

        // A fresh character writes no region section, so old saves and their tests stay byte-stable.
        require(!new CharacterProgression().SaveString().StartsWith("AC22|"), "region-fresh-save-stays-legacy");
        var traveller = new CharacterProgression();
        require(traveller.SaveString().StartsWith("AC5|"), "region-fresh-save-ac5");
        traveller.Regions.Enter(RegionNetwork.Aschenstrasse, 3);
        string saved = traveller.SaveString();
        require(saved.StartsWith("AC22|"), "region-progress-writes-section");
        require(CharacterProgression.TryLoad(saved, out var loaded) && loaded.Regions.IsVisited(RegionNetwork.Aschenstrasse),
            "region-section-roundtrips");
        require(loaded.SaveString() == saved, "region-section-canonical");

        // Old saves derive the right current region and unlocks without a stored section.
        var mill = At(4);
        require(mill.Regions.CurrentRegion(4) == RegionNetwork.AlteMuehle, "region-old-save-current-mill");
        require(mill.Regions.IsUnlocked(RegionNetwork.AlteMuehle, 4) &&
            !mill.Regions.IsUnlocked(RegionNetwork.AlterSpeicher, 4) &&
            !mill.Regions.IsUnlocked(RegionNetwork.Waldweg, 4), "region-unlock-follows-act");
        require(mill.Regions.CurrentRegion(10) == RegionNetwork.Aschenzoll, "region-act10-current-customs");
        mill.Regions.JourneyActive = true;
        require(mill.Regions.CurrentRegion(10) == RegionNetwork.Reise, "region-journey-overrides-act10");

        // Journey region unlocks only when the onward travel is actually active.
        var journey = At(10);
        require(!journey.Regions.IsUnlocked(RegionNetwork.Reise, 10), "region-journey-locked-by-default");
        journey.Regions.JourneyActive = true;
        require(journey.Regions.Enter(RegionNetwork.Reise, 10), "region-journey-entered-when-active");
        require(journey.Regions.IsVisited(RegionNetwork.Reise), "region-journey-visited");
    }
}
