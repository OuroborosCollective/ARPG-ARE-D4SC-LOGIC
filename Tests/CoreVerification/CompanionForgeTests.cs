using System;
using System.Numerics;
using System.Linq;
using AreArpg.Core;

internal static class CompanionForgeTests
{
    public static void Run(Action<bool,string> check)
    {
        var world=RescueFixture();var p=world.Progression;
        check(p.Road.Rescued&&world.RecruitHunter().Accepted,"companion-real-survivor-can-join-near-player");
        check(p.Companion.Status==CompanionStatus.Following&&p.Companion.Level==BigInteger.One,"companion-new-member-has-own-starting-level");
        check(p.Social.Forge.Archive.Get("hunter").Controller.ArchetypeId=="companion","companion-uses-same-admitted-controller-contract");
        check(p.Social.Forge.Archive.Get("hunter").Known.Count==1&&p.Social.Forge.Archive.Get("hunter").RelationshipTo("player").Gratitude==10000,"companion-remembers-the-actual-life-saving-event");
        p.EarnExperience(BigInteger.Pow(10,40));world.Step();
        check(p.Companion.Level==BigInteger.One&&p.Companion.TotalExperience.IsZero,"companion-never-copies-player-level");
        var playerXp=p.TotalExperience;var playerKills=p.LifetimeKills;var runKills=world.Kills;
        var enemy=new MonsterState{Id=91,Kind=0,Xmm=p.Companion.Xmm+1400,Zmm=p.Companion.Zmm,Health=1,MaxHealth=1000};
        world.Monsters.Add(enemy); // Explicit live combat target; real controller/combat/training paths execute below.
        for(int i=0;i<40&&p.Companion.TotalExperience.IsZero;i++)world.Step();
        check(enemy.Health<1000&&p.Companion.PracticeExperience(0)>0&&p.Companion.TotalExperience>0,"companion-learns-only-from-real-effective-hit");
        check(enemy.IsDead&&p.TotalExperience==playerXp&&p.LifetimeKills==playerKills&&world.Kills==runKills,"companion-solo-kill-does-not-credit-player-xp-or-kills");
        HunterEncounterTests.Move(world,enemy.Xmm,enemy.Zmm);
        check(world.TryCollectLoot()&&p.Items.Count==1,"companion-solo-kill-still-drops-collectible-item");
        var experience=p.Companion.TotalExperience;var practice=p.Companion.PracticeExperience(0);
        string token=p.SaveString();check(token.StartsWith("AC18|")&&CharacterProgression.TryLoad(token,out _),"companion-ac18-preserves-checked-ac17-and-progress");
        CharacterProgression.TryLoad(token,out var restored);var resumed=new ArenaSimulation(73,restored,BigInteger.One,LootCatalog.Version1);
        for(int i=0;i<20;i++)resumed.Step();
        check(restored.Companion.TotalExperience==experience&&restored.Companion.PracticeExperience(0)==practice,"companion-load-without-new-effective-action-cannot-duplicate-xp");
        check(restored.TotalExperience==playerXp&&restored.LifetimeKills==playerKills&&restored.Items.Count==1,"companion-kill-attribution-and-loot-survive-load");
        var waiting=RescueFixture();check(waiting.RecruitHunter().Accepted&&waiting.DismissCompanion().Accepted,"companion-can-wait-at-authored-safe-road-anchor");
        var level=waiting.Progression.Companion.Level;
        waiting.Progression.EarnExperience(1000000);for(int i=0;i<300;i++)waiting.Step();
        check(waiting.Progression.Companion.Status==CompanionStatus.Waiting&&waiting.Progression.Companion.Level==level,"companion-waiting-member-never-autolevels");
        check(CharacterProgression.TryLoad(waiting.Progression.SaveString(),out var savedWaiting),"companion-waiting-state-roundtrips");
        var rejoined=new ArenaSimulation(73,savedWaiting,BigInteger.One,LootCatalog.Version1);
        check(rejoined.RecruitHunter().Accepted&&savedWaiting.Companion.Level==level,"companion-rejoining-keeps-individual-level");
        var mortal=RescueFixture();mortal.RecruitHunter();var wounded=mortal.Progression.Companion;
        // Wounded, stationary companion fixture; a real nearby monster must choose and damage this actor.
        wounded.Status=CompanionStatus.Waiting;wounded.Health=40;wounded.Xmm=wounded.PreviousXmm=5000;wounded.Zmm=wounded.PreviousZmm=9000;
        mortal.Monsters.Add(new MonsterState{Id=92,Kind=0,Xmm=6000,Zmm=9000,Health=10000,MaxHealth=10000});mortal.Step();
        check(wounded.Status==CompanionStatus.Dead&&wounded.Health==0,"companion-actual-enemy-hit-can-kill-companion");
        check(CharacterProgression.TryLoad(mortal.Progression.SaveString(),out var deadSave),"companion-death-and-earlier-rescue-both-persist");
        var deadWorld=new ArenaSimulation(73,deadSave,BigInteger.One,LootCatalog.Version1);
        for(int i=0;i<350;i++)deadWorld.Step();
        check(!deadWorld.RecruitHunter().Accepted&&deadSave.Companion.Status==CompanionStatus.Dead&&deadSave.Companion.Health==0,"companion-death-cannot-use-village-recovery-or-rejoin-to-revive");
        var gear=RescueFixture();gear.RecruitHunter();gear.Progression.Equipment.Enable(gear.Progression);
        LootItem owned=null;
        for(ulong seed=0;seed<1000;seed++)
        {
            var receipt=LootCatalog.Version1.Generate(seed,BigInteger.One,1,1,0,gear.Progression.LifetimeKills+1);
            if(gear.Progression.Equipment.ForItem(receipt).Definition.Family==WeaponFamily.Sword){owned=receipt;break;}
        }
        check(owned!=null,"companion-owned-sword-fixture-found");
        gear.Progression.RecordKill();gear.Progression.CollectItem(owned); // Explicit owned-inventory fixture.
        check(gear.TryEquipWeapon(owned.KillOrdinal)&&gear.TryEquipCompanionWeapon(owned.KillOrdinal),"companion-can-receive-existing-equipped-weapon");
        check(gear.Progression.Companion.WeaponOrdinal==owned.KillOrdinal&&gear.Progression.Equipment.Weapon==null&&!gear.TryEquipWeapon(owned.KillOrdinal),"companion-weapon-has-one-equipped-owner");
        check(!gear.TryEquipCompanionWeapon(999999),"companion-cannot-equip-unowned-receipt");
        check(CharacterProgression.TryLoad(gear.Progression.SaveString(),out var gearedSave)&&gearedSave.Companion.WeaponOrdinal==owned.KillOrdinal,"companion-equipment-survives-save-load");
        check(gear.TryUnequipCompanionWeapon()&&gear.TryEquipWeapon(owned.KillOrdinal),"companion-returned-weapon-is-not-duplicated-or-lost");
        var care=RescueFixture();care.RecruitHunter();var patient=care.Progression.Companion;
        patient.Health=500;patient.Hunger=900;patient.Fatigue=500;
        var food=care.Progression.Social.Provisions;var remembered=care.Progression.Social.Forge.Archive.Events.Count;
        check(care.FeedCompanion().Accepted&&care.Progression.Social.Provisions==food-1&&patient.Health==620&&patient.Hunger==500,"companion-care-consumes-one-real-provision");
        check(care.Progression.Social.Forge.Archive.Events.Count==remembered+1&&patient.TotalExperience.IsZero,"companion-care-creates-personal-memory-not-free-experience");
        patient.Health=CompanionState.MaximumHealth;patient.Hunger=0;patient.Fatigue=0;
        check(!care.FeedCompanion().Accepted&&care.Progression.Social.Provisions==food-1,"companion-care-rejects-unneeded-resource-spend");
        check(CharacterProgression.TryLoad(care.Progression.SaveString(),out _),"companion-care-memory-roundtrip");
        var pose=RescueFixture();pose.RecruitHunter();var standing=pose.Progression.Companion;
        standing.PreviousXmm=standing.Xmm-100;pose.DismissCompanion();pose.Step();
        check(standing.PreviousXmm==standing.Xmm&&standing.PreviousZmm==standing.Zmm,"companion-waiting-has-no-stale-moving-pose");
        var areas=RescueFixture();areas.RecruitHunter();var traveler=areas.Progression.Companion;
        areas.Progression.Dungeon.Begin(100);areas.Progression.TravelHunger(6,false);
        var first=new ArenaSimulation(73,areas.Progression,1,LootCatalog.Version1);traveler.Xmm=5000;
        // These distinct (seed, depth) pairs previously both collapsed to 101.
        areas.Progression.Dungeon.Begin(99);areas.Progression.Dungeon.Depth=2;
        var second=new ArenaSimulation(73,areas.Progression,1,LootCatalog.Version1);
        check(traveler.Xmm!=5000,"companion-area-identity-keeps-run-seed-and-floor-separate");
        var formation=RescueFixture();formation.RecruitHunter();var follower=formation.Progression.Companion;
        for(int i=0;i<8;i++)formation.Step();
        check(FixedMath.Square(follower.Xmm-formation.PlayerXmm)+FixedMath.Square(follower.Zmm-formation.PlayerZmm)>=FixedMath.Square(700),"companion-maintains-room-around-player-after-recruitment");
        var walking=RescueFixture();walking.RecruitHunter();var walker=walking.Progression.Companion;
        long walkingStart=walking.PlayerZmm;
        walking.SetMoveInput(0,10000);
        for(int i=0;i<15;i++)walking.Step();
        check(walking.PlayerZmm-walkingStart==15*ArenaSimulation.PlayerSpeedMmPerTick,"companion-follow-test-uses-unblocked-player-walk");
        check(FixedMath.Square(walker.Xmm-walking.PlayerXmm)+FixedMath.Square(walker.Zmm-walking.PlayerZmm)<=FixedMath.Square(2400),"companion-maintains-formation-with-moving-player");
        var boss=RescueFixture();boss.RecruitHunter();var bp=boss.Progression;
        for(int i=bp.HungerStage;i<4;i++)bp.DiscoverHungerClue();bp.ChooseHungerSupply(HungerChoice.Protect);bp.TravelHunger(7,false);
        bp.Investigation.Active=true;bp.Investigation.Clues=7;bp.Investigation.Approach=SealApproach.Investigated;bp.Investigation.TraceFound=true;bp.Rattler.Active=true;
        boss=new ArenaSimulation(73,bp,1,LootCatalog.Version1);
        var creature=bp.Rattler.Creature;creature.Xmm=bp.Companion.Xmm+1000;creature.Zmm=bp.Companion.Zmm;creature.Health=creature.MaxHealth=5200;
        int original=creature.Health;for(int i=0;i<15&&creature.Health==original;i++)boss.Step();
        check(creature.Health<original&&bp.Companion.TotalExperience>0,"companion-can-use-real-weapon-against-rattler");
        bp.Companion.Status=CompanionStatus.Waiting;bp.Companion.Health=100;bp.Rattler.StrikeX=bp.Companion.Xmm;bp.Rattler.StrikeZ=bp.Companion.Zmm;creature.BossWindupTicks=1;
        boss.Step();check(bp.Companion.Status==CompanionStatus.Dead,"companion-is-subject-to-rattler-telegraphed-strike");
        var journey=RescueFixture();journey.RecruitHunter();var jp=journey.Progression;
        for(int i=jp.HungerStage;i<4;i++)jp.DiscoverHungerClue();jp.ChooseHungerSupply(HungerChoice.Protect);
        jp.Investigation.Active=true;jp.Investigation.Clues=7;jp.Investigation.Approach=SealApproach.Investigated;jp.Investigation.TraceFound=true;jp.Investigation.Reported=true;
        jp.Rattler.Active=true;jp.Rattler.Creature.Health=0;jp.Rattler.Creature.LootGranted=true;jp.Rattler.BrandExamined=true;jp.Rattler.Reported=true;
        jp.Woodland.Accept(71);jp.Woodland.Enter();jp.TravelHunger(9,false);
        var cave=new ArenaSimulation(73,jp,1,LootCatalog.Version1);HunterEncounterTests.Move(cave,0,-18000);
        var returned=cave.TryTraversePassage();check(returned!=null&&returned.InForest,"companion-cave-return-uses-real-passage");
        var returning=returned.Progression.Companion;
        check(FixedMath.Square(returning.Xmm-returned.PlayerXmm)+FixedMath.Square(returning.Zmm-returned.PlayerZmm)<=FixedMath.Square(1500),"companion-arrives-at-final-western-entrance-not-default-southern-spawn");
        check(returned.CanStand(returning.Xmm,returning.Zmm)&&returning.PreviousXmm==returning.Xmm&&returning.PreviousZmm==returning.Zmm,"companion-passage-position-valid-and-stable");
        var forestXp=returned.Progression.TotalExperience;var forestKills=returned.Progression.LifetimeKills;
        var victim=returned.Monsters[0];victim.Xmm=returning.Xmm;victim.Zmm=returning.Zmm+400;victim.Health=1;
        check(returned.CanStand(victim.Xmm,victim.Zmm),"companion-forest-target-fixture-on-walkable-floor");
        for(int i=0;i<20&&!victim.IsDead;i++)returned.Step();
        check(victim.IsDead&&returned.Progression.Companion.LifetimeKills==1&&returned.Progression.TotalExperience==forestXp&&returned.Progression.LifetimeKills==forestKills,"companion-forest-defeat-has-independent-credit");
        string forestSave=returned.Progression.SaveString();
        check(CharacterProgression.TryLoad(forestSave,out var forestLoaded)&&forestLoaded.SaveString()==forestSave,"companion-forest-ground-loot-and-attribution-roundtrip-exactly");
        var forestResume=new ArenaSimulation(73,forestLoaded,1,LootCatalog.Version1);
        forestResume.Step();check(forestLoaded.Companion.LifetimeKills==1&&forestLoaded.LifetimeKills==forestKills,"companion-loaded-defeat-cannot-reaward-credit");
        var playerTarget=forestResume.Monsters[1];playerTarget.Xmm=forestResume.PlayerXmm;playerTarget.Zmm=forestResume.PlayerZmm+400;playerTarget.Health=1;
        check(forestResume.ActivateSkill(0).Kills==1&&forestLoaded.LifetimeKills==forestKills+1&&forestLoaded.TotalExperience>forestXp,"next-player-defeat-still-credits-player");
        check(forestResume.GroundLoot.Count==2&&forestResume.GroundLoot.Select(l=>l.Item.KillOrdinal).Distinct().Count()==2&&CharacterProgression.TryLoad(forestLoaded.SaveString(),out _),"mixed-actor-defeats-have-unique-loadable-loot-ordinals");
    }
    private static ArenaSimulation RescueFixture()
    {
        var p=CharacterProgression.CreateEntryProfile();p.EnterVillage();p.DiscoverHungerClue();p.TravelHunger(3,false);p.Social.EnableForge();
        var world=new ArenaSimulation(73,p,BigInteger.One,LootCatalog.Version1);
        p.Road.Started=true;p.Road.Bear.Xmm=world.PlayerXmm+1000;p.Road.Bear.Zmm=world.PlayerZmm;p.Road.Bear.Health=1;
        if(world.ActivateSkill(0).Hits==0)throw new Exception("Rescue fixture strike failed");
        for(int i=0;i<200;i++)
        {
            long dx=HunterEncounter.HunterX-world.PlayerXmm,dz=HunterEncounter.HunterZ-world.PlayerZmm;
            if(Math.Abs(dx)<500&&Math.Abs(dz)<500){world.SetMoveInput(0,0);return world;}
            world.SetMoveInput(Math.Abs(dx)<500?0:Math.Sign(dx)*10000,Math.Abs(dz)<500?0:Math.Sign(dz)*10000);world.Step();
        }
        throw new Exception("Rescue fixture approach failed");
    }
}
