using System;
using System.Numerics;
using AreArpg.Core;

internal static class HunterEncounterTests
{
    internal static ArenaSimulation Road(ulong seed = 123)
    {
        // Explicit authored-region fixture. All movement, damage, saves and interactions below are production paths.
        var p = CharacterProgression.CreateEntryProfile(); p.DiscoverHungerClue(); p.TravelHunger(3, false);
        return new ArenaSimulation(seed, p, BigInteger.One, LootCatalog.Version1);
    }
    internal static void Move(ArenaSimulation w, long x, long z, int limit = 200)
    {
        for (int t = 0; t < limit; t++)
        {
            long dx = x - w.PlayerXmm, dz = z - w.PlayerZmm;
            if (Math.Abs(dx) < 600 && Math.Abs(dz) < 600) { w.SetMoveInput(0, 0); return; }
            w.SetMoveInput(Math.Abs(dx) < 500 ? 0 : Math.Sign(dx)*10000, Math.Abs(dz) < 500 ? 0 : Math.Sign(dz)*10000); w.Step();
        }
        throw new Exception("Hunter test movement failed");
    }
    private static CharacterProgression Load(ArenaSimulation w)
    { if (!CharacterProgression.TryLoad(w.Progression.SaveString(), out var p)) throw new Exception("Invalid hunter save: " + w.Progression.SaveString()); return p; }
    internal static void FightTick(ArenaSimulation w, bool dodge)
    {
        var h = w.Progression.Road; var b = h.Bear;
        if (dodge && b.BossWindupTicks > 0 && h.StrikePlayer)
        {
            // Sidestep away from the fixed telegraph, then return to effective attack range.
            w.SetMoveInput(w.PlayerXmm >= h.StrikeX ? 10000 : -10000, 0);
        }
        else
        {
            long dx = b.Xmm-w.PlayerXmm, dz=b.Zmm-w.PlayerZmm;
            if (dx*dx+dz*dz > 2700L*2700) w.SetMoveInput(Math.Sign(dx)*10000,Math.Sign(dz)*10000);
            else w.SetMoveInput(0,0);
        }
        w.ActivateSkill(0); w.ActivateSkill(1); w.ActivateSkill(3);
        if (dodge && w.PlayerHealth < 1000) w.ActivateSkill(2);
        w.Step();
    }
    public static void Run(Action<bool,string> check)
    {
        var idle=Road(); check(!idle.Progression.Road.Started,"hunter-not-triggered-at-region-load");
        for(int i=0;i<400;i++) idle.Step();
        check(idle.Progression.Road.HunterHealth==700,"hunter-does-not-die-before-approach");
        Move(idle,0,0); check(idle.Progression.Road.Started,"hunter-real-approach-trigger");
        for(int i=0;i<300;i++) idle.Step();
        var h=idle.Progression.Road;
        check(h.HunterHealth==0 && h.Bear.Health==HunterEncounter.BearMaximumHealth,"hunter-inaction-lethal-no-free-arrow-kill");
        check(idle.Progression.Social.RoadEventId(RoadFact.Death)>0 && idle.Progression.Social.RoadEventId(RoadFact.RefusedHelp)>0,"hunter-separate-observed-death-and-neglect");
        check(idle.Progression.Social.RoadRumor(1)=="","hunter-no-telepathic-village-rumor");
        idle=new ArenaSimulation(123,Load(idle),BigInteger.One,LootCatalog.Version1);
        for(int i=0;i<400;i++) idle.Step();
        check(idle.Progression.Road.HunterHealth==0,"hunter-death-survives-reload-and-old-revival-window");
        Move(idle,2000,9000); var coins=idle.Progression.Social.Coins;
        check(idle.InteractHunter(true).Accepted && idle.Progression.Social.Coins==coins+12,"hunter-corpse-transfers-real-purse");
        string once=idle.Progression.SaveString();
        check(!idle.InteractHunter(true).Accepted && once==idle.Progression.SaveString(),"hunter-no-double-corpse-loot");
        check(CharacterProgression.TryLoad(once,out _),"hunter-dead-looted-roundtrip");

        var unaware = Road(); unaware.Progression.Road.TravelerX = 25000; // Explicit distant witness fixture.
        Move(unaware, 0, 0); for (int i=0;i<300;i++) unaware.Step();
        check(unaware.Progression.Social.RoadEventId(RoadFact.Death)>0 && unaware.Progression.Social.RoadEventId(RoadFact.RefusedHelp)==0,
            "hunter-unobserved-player-not-accused");
        Move(unaware,2000,9000); check(unaware.InteractHunter(true).Accepted,"hunter-unseen-theft-still-transfers-purse");
        check(unaware.Progression.Social.RoadKnowledge(4,RoadFact.CorpseTheft)==0,"hunter-unseen-theft-not-telepathic");
        Load(unaware);
        var fled=Road(); Move(fled,0,0); fled.Progression.TravelHunger(2,true);
        fled=new ArenaSimulation(123,Load(fled),BigInteger.One,LootCatalog.Version1);
        for(int i=0;i<300;i++) fled.Step();
        check(fled.Progression.Road.HunterHealth==0,"hunter-fight-continues-offscreen-in-authoritative-ticks");
        Load(fled);
        var a=Road(); var b=Road(); Move(a,0,3000); Move(b,0,3000);
        bool arrows=false, aggro=false; int hits=0;
        for(int i=0;i<700 && !a.GameOver && !a.Progression.Road.Bear.IsDead;i++)
        {
            FightTick(a,true); FightTick(b,true);
            arrows |= a.Progression.Road.ArrowFlash>0; aggro |= a.Progression.Road.AggroTicks>0;
            check(a.ComputeWorldHash()==b.ComputeWorldHash(),"hunter-deterministic-fight-"+i);
            if(i%20==0) { var restored=Load(a); check(restored.SaveString()==a.Progression.SaveString(),"hunter-midfight-canonical-save-"+i); hits++; }
        }
        check(arrows && aggro && hits>0,"hunter-real-attacks-release-arrow-support");
        check(!a.GameOver && a.Progression.Road.Rescued,"hunter-tactical-win-with-starter-profile");
        var saved=Load(a); check(saved.Road.Rescued && saved.Social.RoadKnowledge(3,RoadFact.Rescue)==1,"hunter-survivor-remembers-actual-rescue");
        var xp=a.Progression.TotalExperience; var loot=a.Progression.LootCollected;
        for(int i=0;i<30;i++) FightTick(a,true);
        check(a.Progression.TotalExperience==xp && a.Progression.LootCollected==loot,"hunter-bear-rewards-once");
        Move(a,2000,9000); coins=a.Progression.Social.Coins;
        check(a.InteractHunter(false).Accepted && a.Progression.Social.Coins==coins+12,"hunter-gratitude-transfers-real-purse");
        check(!a.InteractHunter(false).Accepted && !a.InteractHunter(true).Accepted,"hunter-no-repeat-favor-or-living-corpse-theft");
        Load(a);
        var continuous=Road(); Move(continuous,0,3000);
        for(int i=0;i<20;i++) FightTick(continuous,true);
        var resumed=new ArenaSimulation(123,Load(continuous),BigInteger.One,LootCatalog.Version1);
        check(resumed.PlayerHealth==continuous.PlayerHealth && resumed.PlayerXmm==continuous.PlayerXmm && resumed.PlayerZmm==continuous.PlayerZmm,
            "hunter-reload-restores-player-health-position-not-free-heal");
        for(int i=0;i<30;i++)
        {
            FightTick(continuous,true); FightTick(resumed,true);
            check(continuous.Progression.SaveString()==resumed.Progression.SaveString(),"hunter-midfight-resume-same-future-"+i);
        }
        var healed = Road(); Move(healed, 0, 3000);
        check(healed.ActivateSkill(3).Activated, "hunter-heal-fixture-draws-aggro");
        for (int i = 0; i < 100 && healed.PlayerHealth == healed.MaxPlayerHealth; i++) healed.Step();
        check(healed.PlayerHealth > 0 && healed.PlayerHealth < healed.MaxPlayerHealth, "hunter-heal-fixture-takes-real-damage");
        check(healed.ActivateSkill(2).Activated, "hunter-self-heal-activates");
        var afterHeal = new ArenaSimulation(123, Load(healed), BigInteger.One, LootCatalog.Version1);
        check(afterHeal.PlayerHealth == healed.PlayerHealth && afterHeal.Cooldowns[2] == healed.Cooldowns[2],
            "hunter-immediate-heal-save-preserves-health-and-cooldown-before-next-tick");

        var tank=Road(); Move(tank,0,3000);
        for(int i=0;i<700 && !tank.GameOver && !tank.Progression.Road.Bear.IsDead;i++) FightTick(tank,false);
        check(tank.GameOver || tank.PlayerHealth < a.PlayerHealth,"hunter-facetanking-costlier-than-tactics");

        // A real road witness walks home and reports only when actually meeting an awake NPC in sight.
        a.Progression.TravelHunger(2,true); var village=new ArenaSimulation(123,Load(a),BigInteger.One,LootCatalog.Version1);
        for(int i=0;i<700;i++) village.Step();
        check(village.Progression.Road.TravelerRegion==2 && village.Progression.Social.RoadKnowledge(1,RoadFact.Rescue)==6,"hunter-witness-walks-and-reports-to-mara");
        check(village.Progression.Social.GetMemory(1).PositivePoints==0,"hunter-reports-do-not-fabricate-direct-help");
        Load(village);
    }
}
