using System;
using System.Numerics;
using AreArpg.Core;

internal static class CompanionForgeTests
{
    public static void Run(Action<bool,string> check)
    {
        var world=RescueFixture();var p=world.Progression;
        check(p.Road.Rescued&&world.RecruitHunter().Accepted,"companion-real-survivor-can-join-near-player");
        check(p.Companion.Status==CompanionStatus.Following&&p.Companion.Level==BigInteger.One,"companion-new-member-has-own-starting-level");
        check(p.Social.Forge.Archive.Get("hunter").Controller.ArchetypeId=="companion","companion-uses-same-admitted-controller-contract");
        p.EarnExperience(BigInteger.Pow(10,40));world.Step();
        check(p.Companion.Level==BigInteger.One&&p.Companion.TotalExperience.IsZero,"companion-never-copies-player-level");
        var enemy=new MonsterState{Id=91,Kind=0,Xmm=p.Companion.Xmm+1400,Zmm=p.Companion.Zmm,Health=1000,MaxHealth=1000};
        world.Monsters.Add(enemy); // Explicit live combat target; real controller/combat/training paths execute below.
        for(int i=0;i<40&&p.Companion.TotalExperience.IsZero;i++)world.Step();
        check(enemy.Health<1000&&p.Companion.PracticeExperience(0)>0&&p.Companion.TotalExperience>0,"companion-learns-only-from-real-effective-hit");
        var experience=p.Companion.TotalExperience;var practice=p.Companion.PracticeExperience(0);
        string token=p.SaveString();check(token.StartsWith("AC18|")&&CharacterProgression.TryLoad(token,out _),"companion-ac18-preserves-checked-ac17-and-progress");
        CharacterProgression.TryLoad(token,out var restored);var resumed=new ArenaSimulation(73,restored,BigInteger.One,LootCatalog.Version1);
        for(int i=0;i<20;i++)resumed.Step();
        check(restored.Companion.TotalExperience==experience&&restored.Companion.PracticeExperience(0)==practice,"companion-load-without-new-effective-action-cannot-duplicate-xp");
        var waiting=RescueFixture();check(waiting.RecruitHunter().Accepted&&waiting.DismissCompanion().Accepted,"companion-can-wait-at-authored-safe-road-anchor");
        var level=waiting.Progression.Companion.Level;
        waiting.Progression.EarnExperience(1000000);for(int i=0;i<300;i++)waiting.Step();
        check(waiting.Progression.Companion.Status==CompanionStatus.Waiting&&waiting.Progression.Companion.Level==level,"companion-waiting-member-never-autolevels");
        check(CharacterProgression.TryLoad(waiting.Progression.SaveString(),out var savedWaiting),"companion-waiting-state-roundtrips");
        var rejoined=new ArenaSimulation(73,savedWaiting,BigInteger.One,LootCatalog.Version1);
        check(rejoined.RecruitHunter().Accepted&&savedWaiting.Companion.Level==level,"companion-rejoining-keeps-individual-level");
    }
    private static ArenaSimulation RescueFixture()
    {
        var p=CharacterProgression.CreateEntryProfile();p.EnterVillage();p.DiscoverHungerClue();p.TravelHunger(3,false);p.Social.EnableForge();
        var world=new ArenaSimulation(73,p,BigInteger.One,LootCatalog.Version1);
        p.Road.Started=true;p.Road.Bear.Xmm=world.PlayerXmm+1000;p.Road.Bear.Zmm=world.PlayerZmm;p.Road.Bear.Health=1;
        if(!world.ActivateSkill(0).Accepted)throw new Exception("Rescue fixture strike failed");
        for(int i=0;i<200;i++)
        {
            long dx=HunterEncounter.HunterX-world.PlayerXmm,dz=HunterEncounter.HunterZ-world.PlayerZmm;
            if(Math.Abs(dx)<500&&Math.Abs(dz)<500){world.SetMoveInput(0,0);return world;}
            world.SetMoveInput(Math.Abs(dx)<500?0:Math.Sign(dx)*10000,Math.Abs(dz)<500?0:Math.Sign(dz)*10000);world.Step();
        }
        throw new Exception("Rescue fixture approach failed");
    }
}
