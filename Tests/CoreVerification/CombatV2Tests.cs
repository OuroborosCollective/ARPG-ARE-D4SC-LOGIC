using System;
using AreArpg.Core;

/// <summary>
/// Issue #127, phase 1 for the player, with the owner's decisions of 2026-11-23: actions with windup, impact and
/// recovery, stamina, dodge, a held guard, potions that work on their effect tick, no free heal on slot 2, and armor
/// with protection 0/15/30/45 % and healing 100/75/50/25 % weighted by slot. Legacy worlds keep instant combat.
/// </summary>
public static class CombatV2Tests
{
    public static void Run(Action<bool, string> check)
    {
        StandingChainInputs(check);
        LegacyWorldsKeepInstantCombat(check);
        StrikeLandsOnItsImpactTickOnce(check);
        ImpactsDoNotRepeatAfterDefeat(check);
        WeaponArchetypesSetTheTiming(check);
        HeavyStrikeAndRuinCostFocusAndStamina(check);
        StaminaRefillsAfterFiveRestingTicks(check);
        DodgeMovesAndAvoidsOnlyDuringItsUntouchableTicks(check);
        GuardBlocksFromTheFrontForStamina(check);
        GuardBreakLetsHalfThrough(check);
        PotionWorksOnlyOnItsEffectTick(check);
        SlotTwoNoLongerHealsForFree(check);
        ArmorProtectsAndHindersHealingBySlot(check);
        CombatAndArmorSurviveTheSave(check);
        EnemiesWindUpBeforeTheyHit(check);
        SameInputsGiveTheSameWorld(check);
    }

    private static void StandingChainInputs(Action<bool, string> check)
    {
        var w = V2World(891, out var p);
        var chain = new AreArpg.UnityClient.StandingMeleeChain();
        for (int swing = 0; swing < 5; swing++)
        {
            chain.Observe(w, false);
            check(w.ActivateSkill(0).Activated, "standing-chain-real-attack-" + swing);
            check(chain.Accept(w, 0, false) == swing % 4, "standing-chain-stage-" + swing);
            int duration = w.CurrentActionTiming.Total;
            while (w.ActionTick < duration - 3) w.Step();
            check(!chain.Buffer(w, 0), "standing-chain-too-early-" + swing);
            w.Step();
            check(chain.Buffer(w, 0) && chain.Buffer(w, 0), "standing-chain-window-" + swing);
            w.Step(); check(chain.TakeReady(w) < 0, "standing-chain-no-early-hit-" + swing);
            w.Step(); check(chain.TakeReady(w) == 0 && chain.TakeReady(w) < 0, "standing-chain-only-one-" + swing);
        }
        chain.Observe(w, true);
        check(w.ActivateSkill(0).Activated && chain.Accept(w, 0, false) == 0, "standing-chain-move-resets");
        while (w.ActionTick < w.CurrentActionTiming.Total - 1) w.Step();
        check(chain.Buffer(w, 0), "standing-chain-last-tick");
        chain.Observe(w, true);
        w.Step();
        check(chain.TakeReady(w) < 0, "standing-chain-move-clears-buffer");
    }

    private static ArenaSimulation V2World(ulong seed, out CharacterProgression p)
    {
        p = new CharacterProgression();
        p.UseCombatV2();
        var w = new ArenaSimulation(seed, p);
        foreach (var m in w.Monsters) { m.Xmm = 20000; m.Zmm = 20000; m.AttackCooldownTicks = 100000; }
        return w;
    }

    private static MonsterState Target(ArenaSimulation w, long dx)
    {
        var m = w.Monsters[0];
        m.Xmm = w.PlayerXmm + dx; m.Zmm = w.PlayerZmm; m.MaxHealth = 50000; m.Health = 50000; m.AttackCooldownTicks = 100000;
        return m;
    }

    private static LootItem Give(CharacterProgression p, WeaponFamily family, bool? twoHanded = null)
    {
        if (!p.Equipment.Active) p.Equipment.Enable(p);
        for (ulong seed = 0; seed < 10000; seed++)
        {
            var item = LootCatalog.Version1.Generate(seed, 1, 1, 1, 0, p.LifetimeKills + 1);
            var gear = new WeaponItem(item);
            if (gear.Definition.Family != family || (twoHanded.HasValue && gear.Definition.TwoHanded != twoHanded.Value)) continue;
            p.RecordKill();
            if (!p.CollectItem(item)) throw new Exception("Equipment fixture receipt rejected");
            return item;
        }
        throw new Exception("Unreachable weapon family");
    }

    private static void LegacyWorldsKeepInstantCombat(Action<bool, string> check)
    {
        check(new ArenaSimulation(123).ComputeWorldHash() == 778990259245021324UL, "combat-v2-legacy-golden-hash-unchanged");
        var legacy = new ArenaSimulation(41);
        check(!legacy.CombatV2 && legacy.Progression.CombatVersion == 1, "combat-v2-is-opt-in");
        foreach (var m in legacy.Monsters) { m.Xmm = 20000; m.Zmm = 20000; }
        var target = Target(legacy, 1500);
        var hit = legacy.ActivateSkill(0);
        check(hit.Activated && hit.TotalDamage > 0 && target.Health < 50000, "combat-v2-legacy-strike-stays-instant");
    }

    private static void StrikeLandsOnItsImpactTickOnce(Action<bool, string> check)
    {
        var w = V2World(7, out _);
        var target = Target(w, 1500);
        var start = w.ActivateSkill(0);
        check(start.Activated && w.ActionKind == CombatActionKind.Strike && target.Health == 50000 && w.Stamina == 88,
            "combat-v2-strike-starts-a-windup-without-damage");
        w.Step(); w.Step();
        check(target.Health == 50000 && w.ActionPhase == CombatPhase.Windup, "combat-v2-two-windup-ticks-unarmed");
        w.Step();
        int afterImpact = target.Health;
        check(afterImpact < 50000 && w.Impacts.Count == 1 && w.Impacts[0].TotalDamage == 50000 - afterImpact,
            "combat-v2-hit-lands-on-the-third-tick");
        check(!w.ActivateSkill(0).Activated, "combat-v2-no-new-strike-during-recovery");
        w.Step(); w.Step(); w.Step();
        check(target.Health == afterImpact && w.ActionKind == CombatActionKind.None && w.Impacts.Count == 0,
            "combat-v2-exactly-one-impact-then-idle-after-six-ticks");
        check(w.ActivateSkill(0).Activated, "combat-v2-next-strike-after-recovery");
    }

    /// <summary>A tick after defeat reports no landed strike again: the presentation reads Impacts after every tick (PR #130 review).</summary>
    private static void ImpactsDoNotRepeatAfterDefeat(Action<bool, string> check)
    {
        var w = V2World(7, out _);
        Target(w, 1500);
        w.ActivateSkill(0);
        w.Step(); w.Step(); w.Step();
        check(w.Impacts.Count == 1, "combat-v2-impact-reported-on-its-tick");
        w.ReceiveBlowFrom(1000000, w.PlayerXmm + 1000, w.PlayerZmm);
        check(w.GameOver, "combat-v2-defeat-fixture");
        w.Step();
        check(w.Impacts.Count == 0, "combat-v2-no-impact-repeated-after-defeat");
    }

    private static void WeaponArchetypesSetTheTiming(Action<bool, string> check)
    {
        bool all = true;
        for (int i = 0; i < WeaponCatalog.Count; i++)
        {
            var t = CombatV2Rules.Primary(WeaponCatalog.Get(i));
            all &= t.Startup >= 2 && t.Active == 1 && t.Recovery >= 2 && t.Stamina > 0;
        }
        check(all, "combat-v2-every-catalog-weapon-has-readable-timing");
        Func<string, string> timing = id =>
        {
            for (int i = 0; i < WeaponCatalog.Count; i++)
                if (WeaponCatalog.Get(i).Id == id) { var t = CombatV2Rules.Primary(WeaponCatalog.Get(i)); return t.Startup + "/" + t.Active + "/" + t.Recovery + "/" + t.Stamina; }
            return "missing";
        };
        check(timing("dagger_1h_newbie.1") == "2/1/2/12" && timing("sword_1h_newbie.1") == "2/1/3/17" &&
              timing("sword_2h_newbie.1") == "4/1/5/27" && timing("axe_1h_newbie.1") == "3/1/4/20" &&
              timing("axe_2hl_newbie.1") == "5/1/6/34" && timing("bow_newbie.1") == "4/1/5/20" && timing("staff.fire") == "3/1/5/24",
            "combat-v2-issue-127-archetype-table");
        var unarmed = CombatV2Rules.Primary(null);
        var heavy = CombatV2Rules.Heavy(null);
        check(unarmed.Total == 6 && heavy.Startup == 4 && heavy.Recovery == 5 && heavy.Stamina == 18, "combat-v2-unarmed-and-heavy-timing");
    }

    private static void HeavyStrikeAndRuinCostFocusAndStamina(Action<bool, string> check)
    {
        var w = V2World(8, out _);
        var target = Target(w, 1500);
        check(w.ActivateSkill(1).Activated && w.Resource == 75 && w.Stamina == 82, "combat-v2-heavy-costs-focus-and-stamina");
        for (int i = 0; i < 4; i++) w.Step();
        check(target.Health == 50000, "combat-v2-heavy-four-windup-ticks");
        w.Step();
        check(target.Health < 50000, "combat-v2-heavy-lands-on-the-fifth-tick");
        for (int i = 0; i < 5; i++) w.Step();
        int before = target.Health;
        check(w.ActionKind == CombatActionKind.None && w.ActivateSkill(3).Activated && w.Cooldowns[3] > 0, "combat-v2-ruin-starts-with-its-cooldown");
        for (int i = 0; i < 6; i++) w.Step();
        check(target.Health == before, "combat-v2-ruin-six-tick-cast");
        w.Step();
        check(target.Health < before, "combat-v2-ruin-lands-on-the-seventh-tick");
    }

    private static void StaminaRefillsAfterFiveRestingTicks(Action<bool, string> check)
    {
        var w = V2World(9, out _);
        w.SetGuard(true);
        int guard = 0;
        while (w.Stamina > 0 && guard++ < 200) w.Step();
        check(w.Stamina == 0, "combat-v2-held-guard-drains-stamina");
        w.SetGuard(false);
        int ticks = 0;
        while (w.Stamina < CombatV2Rules.StaminaMax && ticks++ < 100) w.Step();
        check(ticks == 22, "combat-v2-empty-to-full-in-22-ticks (5 resting + 17 refilling), measured " + ticks);
    }

    private static void DodgeMovesAndAvoidsOnlyDuringItsUntouchableTicks(Action<bool, string> check)
    {
        var w = V2World(10, out var p);
        long x = w.PlayerXmm, z = w.PlayerZmm;
        check(w.StartDodge(10000, 0) && w.Stamina == 75, "combat-v2-dodge-costs-25-stamina");
        check(!w.StartDodge(10000, 0), "combat-v2-no-dodge-while-dodging");
        int hp = w.PlayerHealth;
        w.Step(); // tick 1: windup
        w.ReceiveBlowFrom(60, w.PlayerXmm, w.PlayerZmm + 1500);
        check(w.PlayerHealth == hp - 60 && w.DodgedAttacks == 0, "combat-v2-dodge-windup-is-not-safe");
        hp = w.PlayerHealth;
        w.Step(); // tick 2: untouchable
        w.ReceiveBlowFrom(60, w.PlayerXmm, w.PlayerZmm + 1500);
        w.Step(); w.Step(); // ticks 3 and 4: still untouchable
        w.ReceiveBlowFrom(60, w.PlayerXmm, w.PlayerZmm + 1500);
        check(w.PlayerHealth == hp && w.DodgedAttacks == 2 && w.Invulnerable, "combat-v2-dodge-ticks-2-to-4-are-untouchable");
        w.Step(); // tick 5: recovery
        w.ReceiveBlowFrom(60, w.PlayerXmm, w.PlayerZmm + 1500);
        check(w.PlayerHealth == hp - 60, "combat-v2-dodge-recovery-is-not-safe");
        w.Step(); w.Step(); w.Step();
        check(w.PlayerXmm == x + 2800 && w.PlayerZmm == z && w.ActionKind == CombatActionKind.None, "combat-v2-dodge-travels-2-8-m-in-8-ticks");
        check(p.CombatVersion == 2, "combat-v2-progression-keeps-the-version");
    }

    private static void GuardBlocksFromTheFrontForStamina(Action<bool, string> check)
    {
        var w = V2World(11, out var p);
        w.SetGuard(true);
        w.Step();
        check(w.ActionKind == CombatActionKind.Guard && !w.GuardRaised, "combat-v2-guard-raises-after-one-tick");
        w.Step();
        int stamina = w.Stamina, hp = w.PlayerHealth;
        check(w.GuardRaised && stamina == 97, "combat-v2-raised-guard-drains-three-per-tick");
        w.ReceiveBlowFrom(100, w.PlayerXmm, w.PlayerZmm + 2000); // facing north: from the front
        check(w.PlayerHealth == hp - 30 && w.Stamina == stamina - 38 && w.BlockedAttacks == 1,
            "combat-v2-weapon-parry-costs-more-and-lets-30-percent-through");
        hp = w.PlayerHealth;
        w.ReceiveBlowFrom(100, w.PlayerXmm, w.PlayerZmm - 2000); // from behind
        check(w.PlayerHealth == hp - 100, "combat-v2-guard-does-not-cover-the-back");

        var ps = new CharacterProgression();
        ps.UseCombatV2();
        var sword = Give(ps, WeaponFamily.Sword, false);
        var shield = Give(ps, WeaponFamily.Shield);
        var s = new ArenaSimulation(12, ps, 1, LootCatalog.Version1);
        foreach (var m in s.Monsters) { m.Xmm = 20000; m.Zmm = 20000; m.AttackCooldownTicks = 100000; }
        check(s.TryEquipWeapon(sword.KillOrdinal) && s.TryEquipWeapon(shield.KillOrdinal) && ps.Equipment.Shield != null, "combat-v2-shield-fixture");
        var practice = ps.WeaponMastery.Experience(WeaponFamily.Shield);
        s.SetGuard(true); s.Step(); s.Step();
        int sStamina = s.Stamina, sHp = s.PlayerHealth;
        s.ReceiveBlowFrom(100, s.PlayerXmm + 1000, s.PlayerZmm + 1500); // 34 degrees off the front: inside 120 degrees
        check(s.PlayerHealth == sHp && s.Stamina == sStamina - CombatV2Rules.GuardCost(100, true, ps.WeaponMastery.Level(WeaponFamily.Shield)),
            "combat-v2-shield-blocks-fully-for-stamina");
        check(ps.WeaponMastery.Experience(WeaponFamily.Shield) > practice, "combat-v2-shield-trains-on-real-blocked-contact");
        check(CombatV2Rules.GuardCost(100, true, 99) < CombatV2Rules.GuardCost(100, true, 1), "combat-v2-shield-level-eases-the-guard");
        var u = V2World(13, out var pu);
        Give(pu, WeaponFamily.Shield);
        var unguarded = pu.WeaponMastery.Experience(WeaponFamily.Shield);
        u.ReceiveBlowFrom(100, u.PlayerXmm, u.PlayerZmm + 1500);
        check(pu.WeaponMastery.Experience(WeaponFamily.Shield) == unguarded, "combat-v2-no-shield-training-without-blocking");
    }

    private static void GuardBreakLetsHalfThrough(Action<bool, string> check)
    {
        var w = V2World(14, out _);
        w.SetGuard(true);
        int guard = 0;
        while (w.Stamina >= 38 && guard++ < 100) w.Step();
        int hp = w.PlayerHealth;
        w.ReceiveBlowFrom(100, w.PlayerXmm, w.PlayerZmm + 2000);
        check(w.PlayerHealth == hp - 50 && w.Stamina == 0 && !w.GuardRaised, "combat-v2-guard-break-lets-half-through");
        bool down = true;
        for (int i = 0; i < 6; i++) { w.Step(); down &= !w.GuardRaised; }
        check(down, "combat-v2-broken-guard-stays-down-for-six-ticks");
    }

    private static void PotionWorksOnlyOnItsEffectTick(Action<bool, string> check)
    {
        var w = V2World(15, out var p);
        p.Crafting.AddPotion(PotionType.Heal, PotionTier.Small);
        p.Crafting.AddPotion(PotionType.Heal, PotionTier.Small);
        w.ApplyEnvironmentalDamage(500);
        int hp = w.PlayerHealth;
        check(w.DrinkPotion(PotionType.Heal) && p.Crafting.PotionCount(PotionType.Heal) == 2 && w.PlayerHealth == hp,
            "combat-v2-drinking-starts-without-effect");
        check(!w.DrinkPotion(PotionType.Heal), "combat-v2-no-second-drink-intent");
        for (int i = 0; i < 5; i++) w.Step();
        check(w.PlayerHealth == hp && p.Crafting.PotionCount(PotionType.Heal) == 2, "combat-v2-five-lifting-ticks");
        w.Step();
        check(w.PlayerHealth == hp + 200 && p.Crafting.PotionCount(PotionType.Heal) == 1, "combat-v2-potion-works-on-the-sixth-tick-once");
        w.Step(); w.Step(); w.Step();
        check(w.ActionKind == CombatActionKind.None, "combat-v2-drink-recovery-three-ticks");
        hp = w.PlayerHealth;
        check(w.DrinkPotion(PotionType.Heal), "combat-v2-second-drink-starts");
        w.Step(); w.Step();
        w.ReceiveBlowFrom(50, w.PlayerXmm, w.PlayerZmm + 1500);
        for (int i = 0; i < 6; i++) w.Step();
        check(w.PlayerHealth == hp - 50 && p.Crafting.PotionCount(PotionType.Heal) == 1, "combat-v2-a-blow-spills-the-potion-unused");
    }

    private static void SlotTwoNoLongerHealsForFree(Action<bool, string> check)
    {
        var w = V2World(16, out var p);
        w.ApplyEnvironmentalDamage(400);
        int hp = w.PlayerHealth;
        var level = p.PracticeLevel(2);
        var result = w.ActivateSkill(2);
        check(!result.Activated && w.PlayerHealth == hp && p.PracticeLevel(2) == level, "combat-v2-slot-two-no-free-heal-practice-kept");
        var legacy = new ArenaSimulation(16);
        legacy.ApplyEnvironmentalDamage(400);
        int legacyHp = legacy.PlayerHealth;
        check(legacy.ActivateSkill(2).Activated && legacy.PlayerHealth > legacyHp, "combat-v2-legacy-slot-two-still-heals");
    }

    private static void ArmorProtectsAndHindersHealingBySlot(Action<bool, string> check)
    {
        var a = new ArmorState();
        check(a.ProtectionBp == 0 && a.HealingEfficiencyBp == 10000, "armor-nothing-worn-neither-protects-nor-hinders");
        foreach (string id in new[] { "armor.plate.helm", "armor.plate.cuirass", "armor.plate.gauntlets", "armor.plate.greaves", "armor.plate.sabatons" })
        { var piece = ArmorCatalog.Find(id); a.Grant(piece); a.Wear(piece); }
        check(a.ProtectionBp == 4500 && a.HealingEfficiencyBp == 2500, "armor-full-plate-45-percent-protection-25-percent-healing");
        var mixed = new ArmorState();
        var cuirass = ArmorCatalog.Find("armor.plate.cuirass");
        mixed.Grant(cuirass); mixed.Wear(cuirass);
        foreach (string id in new[] { "armor.cloth.hood", "armor.cloth.wraps", "armor.cloth.trousers", "armor.cloth.shoes" })
        { var piece = ArmorCatalog.Find(id); mixed.Grant(piece); mixed.Wear(piece); }
        check(mixed.ProtectionBp == 1800 && mixed.HealingEfficiencyBp == 7000, "armor-plate-chest-over-cloth-gives-70-percent-healing");
        int leather = 0, chain = 0;
        foreach (var slot in new[] { ArmorSlot.Head, ArmorSlot.Chest, ArmorSlot.Hands, ArmorSlot.Legs, ArmorSlot.Feet })
        {
            leather += ArmorRules.SlotWeight(slot) * ArmorRules.MaterialProtection(ArmorMaterial.Leather) / 10000;
            chain += ArmorRules.SlotWeight(slot) * ArmorRules.MaterialProtection(ArmorMaterial.Chain) / 10000;
        }
        check(leather == 1500 && chain == 3000, "armor-owner-values-15-and-30-percent");
        check(!a.Grant(cuirass) && !new ArmorState().Wear(cuirass), "armor-owned-once-worn-only-when-owned");
        var w = V2World(17, out var p);
        foreach (string id in new[] { "armor.plate.helm", "armor.plate.cuirass", "armor.plate.gauntlets", "armor.plate.greaves", "armor.plate.sabatons" })
        { var piece = ArmorCatalog.Find(id); p.Armor.Grant(piece); p.Armor.Wear(piece); }
        int hp = w.PlayerHealth;
        w.ApplyEnvironmentalDamage(1000);
        check(w.PlayerHealth == hp - 550, "armor-full-plate-takes-45-percent-off-a-blow");
    }

    private static void CombatAndArmorSurviveTheSave(Action<bool, string> check)
    {
        var p = new CharacterProgression();
        p.UseCombatV2();
        var robe = ArmorCatalog.Find("armor.cloth.robe.healer");
        var cuirass = ArmorCatalog.Find("armor.plate.cuirass");
        p.Armor.Grant(robe); p.Armor.Grant(cuirass); p.Armor.Wear(cuirass);
        string saved = p.SaveString();
        check(CharacterProgression.TryLoad(saved, out var q) && q.CombatVersion == 2 && q.Armor.Owned.Count == 2 &&
              q.Armor.Worn[(int)ArmorSlot.Chest] == cuirass && q.SaveString() == saved, "combat-v2-and-armor-survive-save-load");
        var legacy = new CharacterProgression();
        legacy.SaveString();
        check(!legacy.TryGetSection("combat", out _) && !legacy.TryGetSection("armor", out _), "combat-v2-legacy-save-gets-no-new-sections");
        check(!ArmorState.TryRead("1;armor.unknown;-,-,-,-,-", out _) && !ArmorState.TryRead("1;armor.plate.cuirass;armor.plate.cuirass,-,-,-,-", out _) &&
              !ArmorState.TryRead("1;armor.plate.cuirass,armor.plate.cuirass;-,-,-,-,-", out _) && !ArmorState.TryRead("2;armor.plate.cuirass;-,-,-,-,-", out _) &&
              ArmorState.TryRead("1;armor.plate.cuirass;-,armor.plate.cuirass,-,-,-", out _), "armor-section-fails-closed");
        var once = new CharacterProgression();
        check(once.UseCombatV2() && !once.UseCombatV2(), "combat-v2-switch-is-one-way");
    }

    private static void EnemiesWindUpBeforeTheyHit(Action<bool, string> check)
    {
        var w = V2World(18, out _);
        var ghoul = w.Monsters[0];
        ghoul.Kind = 0; ghoul.Xmm = w.PlayerXmm + 1000; ghoul.Zmm = w.PlayerZmm; ghoul.AttackCooldownTicks = 0; ghoul.Health = ghoul.MaxHealth = 5000;
        int hp = w.PlayerHealth;
        w.Step();
        check(w.PlayerHealth == hp && ghoul.BossWindupTicks == 5, "combat-v2-ghoul-announces-its-swing");
        for (int i = 0; i < 4; i++) w.Step();
        check(w.PlayerHealth == hp, "combat-v2-ghoul-five-windup-ticks");
        w.Step();
        check(w.PlayerHealth == hp - 40 && ghoul.AttackCooldownTicks > 0, "combat-v2-ghoul-hits-on-its-impact-tick");
        var s = V2World(19, out _);
        var slow = s.Monsters[0];
        slow.Kind = 1; slow.Xmm = s.PlayerXmm + 1000; slow.Zmm = s.PlayerZmm; slow.AttackCooldownTicks = 0; slow.Health = slow.MaxHealth = 5000;
        int shp = s.PlayerHealth;
        s.Step();
        check(slow.BossWindupTicks == 6, "combat-v2-knight-winds-up-six-ticks");
        s.SetMoveInput(-10000, 0);
        for (int i = 0; i < 8; i++) s.Step();
        check(s.PlayerHealth == shp, "combat-v2-stepping-out-of-reach-avoids-the-announced-blow");
        var legacy = new ArenaSimulation(18);
        foreach (var m in legacy.Monsters) { m.Xmm = 20000; m.Zmm = 20000; m.AttackCooldownTicks = 100000; }
        var old = legacy.Monsters[0];
        old.Kind = 0; old.Xmm = legacy.PlayerXmm + 1000; old.Zmm = legacy.PlayerZmm; old.AttackCooldownTicks = 0;
        int lhp = legacy.PlayerHealth;
        legacy.Step();
        check(legacy.PlayerHealth < lhp, "combat-v2-legacy-ghoul-still-hits-at-once");
    }

    private static void SameInputsGiveTheSameWorld(Action<bool, string> check)
    {
        var a = V2World(21, out _); var b = V2World(21, out _);
        Target(a, 1500); Target(b, 1500);
        bool same = true;
        for (int tick = 0; tick < 200; tick++)
        {
            foreach (var w in new[] { a, b })
            {
                w.SetMoveInput(tick % 40 < 20 ? 10000 : -10000, tick % 7 == 0 ? 10000 : 0);
                if (tick % 9 == 0) w.ActivateSkill(tick % 18 == 0 ? 0 : 1);
                if (tick % 31 == 5) w.StartDodge(0, -10000);
                w.SetGuard(tick % 50 > 40);
                w.Step();
            }
            same &= a.ComputeWorldHash() == b.ComputeWorldHash();
        }
        check(same, "combat-v2-same-inputs-same-world-for-200-ticks");
    }
}
