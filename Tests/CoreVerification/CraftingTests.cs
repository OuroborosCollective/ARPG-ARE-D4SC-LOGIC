using System;
using System.Linq;
using AreArpg.Core;

/// <summary>Gathering and crafting (#75): seven level 1-99 skills, real gathering spots with reproducible
/// yields, alchemy/first-aid/carpentry/smithing recipes and potions whose tier is a level-based random affix.</summary>
internal static class CraftingTests
{
    public static void Run(Action<bool, string> require)
    {
        // Seven skills, each a classic level 1-99 curve; no XP for ineffective actions.
        require(CraftingRules.SkillCount == 7, "craft-seven-skills");
        require(CraftingRules.LevelForExperience(0) == 1 && CraftingRules.LevelForExperience(83) == 2 &&
            CraftingRules.LevelForExperience(13034431) == 99, "craft-classic-level-curve");
        require(CraftingRules.ExperienceFromEffectiveAmount(0) == 0 &&
            CraftingRules.ExperienceFromEffectiveAmount(25) == 2, "craft-xp-only-for-effective");

        // Gathering produces reproducible yields; a higher skill yields at least as much.
        var low = CraftingRules.Gather(CraftSkill.Herbalism, 1, 4242UL);
        var low2 = CraftingRules.Gather(CraftSkill.Herbalism, 1, 4242UL);
        var high = CraftingRules.Gather(CraftSkill.Herbalism, 80, 4242UL);
        require(low.Length == high.Length && low.Zip(high, (a, b) => b >= a).All(x => x), "craft-gather-higher-level-at-least-as-much");
        require(low.SequenceEqual(low2), "craft-gather-reproducible");
        require(low.Sum() > 0, "craft-gather-yields-something");

        // Mining yields ores, woodcutting yields wood; the spot kind decides the material family.
        var ore = CraftingRules.Gather(CraftSkill.Mining, 40, 7UL);
        require(ore[(int)MaterialKind.Ascheisen] + ore[(int)MaterialKind.Schlackenkupfer] +
            ore[(int)MaterialKind.Grausilber] + ore[(int)MaterialKind.Glutstein] + ore[(int)MaterialKind.Runenerz] > 0,
            "craft-mining-yields-ore");
        var wood = CraftingRules.Gather(CraftSkill.Woodcutting, 40, 7UL);
        require(wood[(int)MaterialKind.Holz] > 0, "craft-woodcutting-yields-wood");

        // Alchemy: brewing consumes materials and produces exactly one potion of the chosen type.
        var p = new CharacterProgression();
        p.Crafting.AddMaterial(MaterialKind.Ashenflieder, 2);
        p.Crafting.AddMaterial(MaterialKind.Glutklee, 1);
        require(p.Crafting.BrewPotion(PotionType.Heal, 1UL), "craft-brew-heal-potion");
        require(p.Crafting.PotionCount(PotionType.Heal) == 1, "craft-brew-adds-one-potion");
        require(p.Crafting.MaterialCount(MaterialKind.Ashenflieder) == 0 &&
            p.Crafting.MaterialCount(MaterialKind.Glutklee) == 0, "craft-brew-consumes-materials");
        require(!p.Crafting.BrewPotion(PotionType.Heal, 2UL), "craft-brew-refused-without-materials");

        // The potion tier is a level-based random affix: a low skill yields small, a high skill yields large.
        var lowRolls = Enumerable.Range(0, 200).Select(i => CraftingRules.RollTier(1, (ulong)i)).ToArray();
        require(lowRolls.All(t => t == PotionTier.Small || t == PotionTier.Medium), "craft-low-level-no-large");
        var highRolls = Enumerable.Range(0, 200).Select(i => CraftingRules.RollTier(99, (ulong)i)).ToArray();
        require(highRolls.Contains(PotionTier.Large) && highRolls.Contains(PotionTier.Medium) && highRolls.Contains(PotionTier.Small),
            "craft-high-level-any-tier");
        require(CraftingRules.RollTier(50, 12345UL) == CraftingRules.RollTier(50, 12345UL), "craft-tier-roll-reproducible");

        // Duration is fixed per tier: small 3 min, medium 6 min, large 9 min (100 ms ticks).
        require(CraftingRules.DurationTicks(PotionTier.Small) == 1800, "craft-duration-small-3min");
        require(CraftingRules.DurationTicks(PotionTier.Medium) == 3600, "craft-duration-medium-6min");
        require(CraftingRules.DurationTicks(PotionTier.Large) == 5400, "craft-duration-large-9min");

        // One potion item per type; the tier is the affix. Drinking applies the effect and consumes it.
        var drink = new CharacterProgression();
        drink.Crafting.AddPotion(PotionType.Ward, PotionTier.Medium);
        require(drink.Crafting.DrinkPotion(PotionType.Ward), "craft-drink-potion");
        require(drink.Crafting.PotionCount(PotionType.Ward) == 0, "craft-drink-consumes-potion");
        require(drink.Crafting.DefenseBonusBp == 2500 && drink.Crafting.ActiveWardTicks == 3600, "craft-ward-25pct-and-duration");
        var might = new CharacterProgression();
        might.Crafting.AddPotion(PotionType.Might, PotionTier.Small);
        require(might.Crafting.DrinkPotion(PotionType.Might) && might.Crafting.AttackBonusBp == 2500 &&
            might.Crafting.ActiveMightTicks == 1800, "craft-might-25pct-and-duration");
        var heal = new CharacterProgression();
        heal.Crafting.AddPotion(PotionType.Heal, PotionTier.Large);
        require(heal.Crafting.DrinkPotion(PotionType.Heal) && heal.Crafting.LastHealAmount == CraftingRules.HealAmount(PotionTier.Large),
            "craft-heal-potion-heals-by-tier");
        // Review 2026-11-18 (gitar-bot): the heal potion must actually raise player health in the simulation.
        var healOwner = new CharacterProgression();
        healOwner.Crafting.AddPotion(PotionType.Heal, PotionTier.Small);
        var healSim = new ArenaSimulation(12UL, healOwner, System.Numerics.BigInteger.One);
        healSim.ApplyEnvironmentalDamage(1000);
        int beforeHeal = healSim.PlayerHealth;
        require(beforeHeal < 1200 && healSim.DrinkPotion(PotionType.Heal) &&
            healSim.PlayerHealth == beforeHeal + CraftingRules.HealAmount(PotionTier.Small),
            "craft-heal-potion-raises-player-health");

        // First aid: bandages and the once-only revive cloth.
        var aid = new CharacterProgression();
        aid.Crafting.AddMaterial(MaterialKind.Ashenflieder, 2); aid.Crafting.AddMaterial(MaterialKind.Grauwurz, 1);
        require(aid.Crafting.CraftBandage(BandageTier.Medium), "craft-bandage-medium");
        require(aid.Crafting.BandageCount(BandageTier.Medium) == 1, "craft-bandage-added");
        var revive = new CharacterProgression();
        revive.Crafting.AddMaterial(MaterialKind.Nachtbluete, 1); revive.Crafting.AddMaterial(MaterialKind.Glutstein, 1);
        require(revive.Crafting.CraftReviveCloth(), "craft-revive-cloth");
        require(revive.Crafting.ReviveClothCount == 1 && revive.Crafting.ConsumeReviveCloth(), "craft-revive-cloth-consumable-once");
        require(revive.Crafting.ReviveClothCount == 0 && !revive.Crafting.ConsumeReviveCloth(), "craft-revive-cloth-single-use");

        // Carpentry: wooden frame and a real bow; smithing: improve and reinforce a weapon, plus affix runes.
        var carp = new CharacterProgression();
        carp.Crafting.AddMaterial(MaterialKind.Holz, 4);
        carp.Crafting.AddMaterial(MaterialKind.Dornbeere, 1);
        require(carp.Crafting.CraftWoodenFrame(), "craft-wooden-frame");
        require(carp.Crafting.CraftBow(), "craft-bow");
        require(carp.Crafting.WoodenFrameCount == 0 && carp.Crafting.BowCount == 1, "craft-bow-consumes-frame");

        var smith = new CharacterProgression();
        smith.Crafting.AddMaterial(MaterialKind.Ascheisen, 2);
        int before = smith.WeaponBonus;
        require(smith.Crafting.ImproveWeapon(), "craft-improve-weapon");
        require(smith.WeaponBonus > before, "craft-improve-raises-weapon-bonus");
        smith.Crafting.AddMaterial(MaterialKind.Grausilber, 1); smith.Crafting.AddMaterial(MaterialKind.Glutstein, 1);
        int improved = smith.WeaponBonus;
        require(smith.Crafting.ReinforceWeapon(), "craft-reinforce-weapon");
        require(smith.WeaponBonus > improved, "craft-reinforce-raises-more");
        smith.Crafting.AddMaterial(MaterialKind.Runenerz, 2); smith.Crafting.AddMaterial(MaterialKind.Grausilber, 1);
        require(smith.Crafting.CraftAffixRune(WeaponAffix.Power), "craft-affix-rune");
        require(smith.Crafting.AffixRuneCount == 1, "craft-affix-rune-added");

        // State persists as its own AC22 section; a fresh character writes none, so old saves stay byte-stable.
        require(!new CharacterProgression().SaveString().StartsWith("AC22|"), "craft-fresh-save-stays-legacy");
        var carrier = new CharacterProgression();
        carrier.Crafting.AddMaterial(MaterialKind.Runenerz, 3);
        carrier.Crafting.RecordExperience(CraftSkill.Smithing, 500);
        carrier.Crafting.AddPotion(PotionType.Heal, PotionTier.Medium);
        string saved = carrier.SaveString();
        require(saved.StartsWith("AC22|"), "craft-writes-section");
        require(CharacterProgression.TryLoad(saved, out var loaded) &&
            loaded.Crafting.MaterialCount(MaterialKind.Runenerz) == 3 &&
            loaded.Crafting.PotionCount(PotionType.Heal) == 1 &&
            loaded.Crafting.Level(CraftSkill.Smithing) == carrier.Crafting.Level(CraftSkill.Smithing),
            "craft-section-roundtrips");
        require(loaded.SaveString() == saved, "craft-section-canonical");

        // Review 2026-11-18 (gitar-bot): a running buff alone is progress and survives save/reload.
        var buffCarrier = new CharacterProgression();
        buffCarrier.Crafting.AddPotion(PotionType.Ward, PotionTier.Large);
        buffCarrier.Crafting.DrinkPotion(PotionType.Ward);
        string buffSaved = buffCarrier.SaveString();
        require(buffSaved.StartsWith("AC22|") && CharacterProgression.TryLoad(buffSaved, out var buffLoaded) &&
            buffLoaded.Crafting.ActiveWardTicks == buffCarrier.Crafting.ActiveWardTicks, "craft-running-buff-persists");

        // Review 2026-11-18 (gitar-bot): a full rune stock refuses further crafting instead of losing the affix.
        var full = new CharacterProgression();
        for (int i = 0; i < 8; i++)
        {
            full.Crafting.AddMaterial(MaterialKind.Runenerz, 2); full.Crafting.AddMaterial(MaterialKind.Grausilber, 1);
            require(full.Crafting.CraftAffixRune((WeaponAffix)(i % 4)), "craft-rune-" + i);
        }
        full.Crafting.AddMaterial(MaterialKind.Runenerz, 2); full.Crafting.AddMaterial(MaterialKind.Grausilber, 1);
        require(!full.Crafting.CraftAffixRune(WeaponAffix.Ward), "craft-rune-refused-when-full");
        require(full.Crafting.MaterialCount(MaterialKind.Runenerz) == 2, "craft-rune-refusal-keeps-materials");
        // A buff countdown advances on the real step and expires.
        var tick = new CharacterProgression();
        tick.Crafting.AddPotion(PotionType.Might, PotionTier.Small);
        tick.Crafting.DrinkPotion(PotionType.Might);
        var world = new ArenaSimulation(9UL, tick, System.Numerics.BigInteger.One);
        for (int i = 0; i < 1800; i++) world.Step();
        require(tick.Crafting.ActiveMightTicks == 0 && tick.Crafting.AttackBonusBp == 0, "craft-buff-expires-on-ticks");
    }
}
