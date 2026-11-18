using System;
using AreArpg.Core;

/// <summary>Orsel's mill teachings, Orkel's side quest, the story-NPC death rule and per-village population growth.</summary>
internal static class VillageServicesTests
{
    private static CharacterProgression AtMill()
    {
        var p = CharacterProgression.CreateEntryProfile();
        for (int i = 0; i < 3; i++) p.DiscoverHungerClue();
        p.ChooseHungerSupply(HungerChoice.Protect);
        p.TravelHunger(4, false);
        return p;
    }

    public static void Run(Action<bool, string> require)
    {
        // Orsel teaches only after the mill quest (act 4, stage 4).
        var before = AtMill();
        require(!VillageServicesState.MillQuestCompleted(before), "mill-quest-open-before-beds");
        require(!before.Village.UnlockOrselTeaches(before), "orsel-locked-before-mill-quest");
        before.DiscoverHungerClue();
        require(VillageServicesState.MillQuestCompleted(before), "mill-quest-complete-after-beds");
        require(before.Village.UnlockOrselTeaches(before) && !before.Village.UnlockOrselTeaches(before), "orsel-unlocked-once-after-mill-quest");

        // End to end: the real mill-beds action at the old mill unlocks Orsel's teachings.
        var mill = AtMill();
        mill.Social.EnableForge();
        var millWorld = new ArenaSimulation(77UL, mill, System.Numerics.BigInteger.One, LootCatalog.Version1);
        HunterEncounterTests.Move(millWorld, -4000, 5000);
        require(millWorld.ActOnHunger(HungerAction.InspectBeds).Accepted && mill.Village.OrselTeachesUnlocked,
            "orsel-unlocked-by-real-mill-beds-action");
        require(mill.Village.Population == VillageServicesState.BasePopulation + 1,
            "population-grows-by-real-mill-beds-action");

        // The three lessons unlock once and only once.
        var p = AtMill();
        p.DiscoverHungerClue(); p.Village.UnlockOrselTeaches(p);
        require(p.Village.LearnFirstAid() && !p.Village.LearnFirstAid(), "orsel-first-aid-once");
        require(p.Village.LearnHealSpell() && p.Village.LearnBuffet(), "orsel-heal-and-buffet");
        require(!p.Village.LearnHealSpell(), "orsel-heal-once");

        // The heal spell heals the player and starts the cooldown.
        var world = new ArenaSimulation(31UL, p, System.Numerics.BigInteger.One);
        world.ApplyEnvironmentalDamage(600);
        int hurt = world.PlayerHealth;
        require(world.CastHealSpell() && world.PlayerHealth == hurt + VillageServicesState.HealSpellAmount, "orsel-heal-spell-heals");
        require(!world.CastHealSpell(), "orsel-heal-spell-cooldown");
        for (int i = 0; i < VillageServicesState.HealSpellCooldown; i++) world.Step();
        require(world.CastHealSpell(), "orsel-heal-spell-ready-again");

        // The buffet is a short attack-and-defence buff that expires.
        require(world.EatBuffet() && world.Progression.Village.BuffetAttackBp > 0 && world.Progression.Village.BuffetDefenseBp > 0, "orsel-buffet-applies");
        require(!world.EatBuffet(), "orsel-buffet-no-double");
        for (int i = 0; i < VillageServicesState.BuffetDurationTicks; i++) world.Step();
        require(world.Progression.Village.BuffetAttackBp == 0, "orsel-buffet-expires");

        // Orkel's side quest: offer, cave, find (fleeing or honey), return, then hire.
        var quest = AtMill();
        quest.DiscoverHungerClue(); quest.Village.UnlockOrselTeaches(quest);
        require(quest.Village.OfferOrkelQuest() && quest.Village.OrkelQuestStage == 1, "orkel-quest-offered");
        require(quest.Village.EnterOrkelCave() && quest.Village.OrkelQuestStage == 2, "orkel-in-cave");
        require(quest.Village.ResolveOrkelFind(true) && quest.Village.OrkelQuestStage == 4, "orkel-found-with-honey");
        require(quest.Village.ReturnOrkelToOrsel() && quest.Village.OrkelQuestStage == 5, "orkel-returned");
        require(quest.Village.RecruitOrkel() && !quest.Village.RecruitOrkel(), "orkel-hired-once");
        var fleeing = AtMill(); fleeing.DiscoverHungerClue(); fleeing.Village.UnlockOrselTeaches(fleeing);
        fleeing.Village.OfferOrkelQuest(); fleeing.Village.EnterOrkelCave();
        require(fleeing.Village.ResolveOrkelFind(false) && fleeing.Village.OrkelQuestStage == 3, "orkel-found-fleeing");

        // Story-NPCs cannot die while their quest is open; they may die once it is complete.
        var open = AtMill();
        require(!ArenaSimulation.StoryNpcCanDie(StoryNpc.Mara, open), "story-npc-immortal-while-quest-open");
        open.DiscoverHungerClue();
        require(ArenaSimulation.StoryNpcCanDie(StoryNpc.Mara, open), "story-npc-mortal-after-quest");

        // Population grows by one per completed main quest, per village.
        var village = AtMill();
        require(village.Village.Population == VillageServicesState.BasePopulation, "population-starts-at-base");
        village.Village.RecordMainQuestCompleted();
        require(village.Village.Population == VillageServicesState.BasePopulation + 1, "population-grows-per-quest");

        // A new material (honey) and the regeneration potion are usable.
        var honey = new CharacterProgression();
        honey.Crafting.AddMaterial(MaterialKind.Honig, 1); honey.Crafting.AddMaterial(MaterialKind.Nachtbluete, 1);
        require(honey.Crafting.BrewPotion(PotionType.Regen, 5UL) && honey.Crafting.PotionCount(PotionType.Regen) == 1, "regen-potion-brewed");
        honey.Crafting.DrinkPotion(PotionType.Regen);
        require(honey.Crafting.ActiveRegenTicks > 0 && honey.Crafting.RegenPerTick > 0, "regen-potion-active");

        // Village state persists as its own AC22 section; a fresh character writes none.
        require(!new CharacterProgression().SaveString().StartsWith("AC22|"), "village-fresh-save-stays-legacy");
        var carrier = AtMill(); carrier.DiscoverHungerClue(); carrier.Village.UnlockOrselTeaches(carrier);
        carrier.Village.RecordMainQuestCompleted(); carrier.Village.LearnFirstAid();
        string saved = carrier.SaveString();
        require(saved.StartsWith("AC22|"), "village-writes-section");
        require(CharacterProgression.TryLoad(saved, out var loaded) && loaded.Village.OrselTeachesUnlocked &&
            loaded.Village.LearnedFirstAid && loaded.Village.Population == VillageServicesState.BasePopulation + 1,
            "village-section-roundtrips");
        require(loaded.SaveString() == saved, "village-section-canonical");

        // Review 2026-11-18 (gitar-bot): the regen potion must really heal on the real simulation tick.
        var regen = new CharacterProgression();
        regen.Crafting.AddMaterial(MaterialKind.Honig, 1); regen.Crafting.AddMaterial(MaterialKind.Nachtbluete, 1);
        regen.Crafting.BrewPotion(PotionType.Regen, 5UL);
        var regenWorld = new ArenaSimulation(41UL, regen, System.Numerics.BigInteger.One);
        regenWorld.ApplyEnvironmentalDamage(500);
        int regenBefore = regenWorld.PlayerHealth;
        regenWorld.DrinkPotion(PotionType.Regen);
        regenWorld.Step();
        require(regenWorld.PlayerHealth == regenBefore + regen.Crafting.RegenPerTick, "regen-potion-heals-on-real-tick");
        // A save during the buff keeps the regen timer.
        string regenSave = regen.SaveString();
        require(CharacterProgression.TryLoad(regenSave, out var regenLoaded) &&
            regenLoaded.Crafting.ActiveRegenTicks == regen.Crafting.ActiveRegenTicks &&
            regenLoaded.Crafting.RegenPerTick == regen.Crafting.RegenPerTick, "regen-buff-persists");

        // Review 2026-11-18 (gitar-bot): a version-1 crafting token still round-trips byte-for-byte.
        var v1 = new CharacterProgression();
        v1.Crafting.AddMaterial(MaterialKind.Ascheisen, 3);
        string v1Save = v1.SaveString();
        require(CharacterProgression.TryLoad(v1Save, out var v1Loaded) &&
            v1Loaded.TryGetSection("crafting", out var v1Token) && v1Token.StartsWith("1,", StringComparison.Ordinal),
            "crafting-v1-token-written-without-v2-data");
        require(v1Loaded.SaveString() == v1Save, "crafting-v1-token-roundtrips");

        // Review 2026-11-18 (gitar-bot): the mill-healer, Orkel quest and population are reachable through the
        // simulation, not only through direct state calls.
        var wiring = AtMill();
        wiring.Social.EnableForge();
        var wiringWorld = new ArenaSimulation(88UL, wiring, System.Numerics.BigInteger.One, LootCatalog.Version1);
        wiring.DiscoverHungerClue();
        wiring.Village.UnlockOrselTeaches(wiring);
        require(wiring.Village.OrselTeachesUnlocked, "orsel-teaches-unlocked-for-wiring");
        require(wiringWorld.OfferOrkelQuest().Accepted, "orkel-offer-via-simulation");
        require(wiringWorld.EnterOrkelCave() && wiringWorld.ResolveOrkelFind(true) && wiringWorld.ReturnOrkelToOrsel().Accepted,
            "orkel-quest-via-simulation");
        require(wiringWorld.RecruitOrkel().Accepted, "orkel-recruit-via-simulation");
        int popBefore = wiring.Village.Population;
        wiringWorld.CompleteMainQuest();
        require(wiring.Village.Population == popBefore + 1, "population-grows-via-simulation");
    }
}
