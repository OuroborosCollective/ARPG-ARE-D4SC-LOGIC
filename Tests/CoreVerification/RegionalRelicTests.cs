using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AreArpg.Core;

internal static class RegionalRelicTests
{
    private static ArenaSimulation Reload(ArenaSimulation world)
    {
        var save = world.Progression.SaveString();
        if (!CharacterProgression.TryLoad(save, JourneyPilotTests.Maps(), out var profile) || profile.SaveString() != save)
            throw new Exception("Regional gear failed to round-trip at act " + world.Progression.EntryAct);
        return new ArenaSimulation(123, profile, BigInteger.One, LootCatalog.Version1, JourneyPilotTests.Maps());
    }

    private static void Claim(ArenaSimulation world, RegionalRelic piece, Action<bool,string> require)
    {
        string label = "regional-" + piece + "-";
        if (!world.TryGetRegionalRelicSource(piece,out long x,out long z))
            throw new Exception("Unreachable source: " + piece + " in act " + world.Progression.EntryAct);
        HunterEncounterTests.Move(world,x,z);
        require(world.TryGetClaimableRegionalRelic(out var selected) && selected == piece &&
            world.ActOnHunger(HungerAction.ClaimRegionalRelic).Accepted && world.Progression.Equipment.OwnsRegionalRelic(piece),
            label + "normal-proximity-interaction");
        require(!world.TryGetRegionalRelicSource(piece,out _,out _) && !world.Progression.Equipment.IsRegionalRelicEquipped(piece),
            label + "one-time-pickup-no-auto-equip");
    }

    public static void Run(Action<bool,string> require)
    {
        var road = HunterEncounterTests.Road();
        string historical = road.Progression.SaveString();
        require(Reload(road).Progression.SaveString() == historical && !road.Progression.Equipment.Active &&
            !road.TryGetRegionalRelicSource(out _,out _,out _) && !road.TrySetRegionalRelicEquipped(RegionalRelic.Schlackengarde,true),
            "regional-existing-road-save-is-byte-identical-and-unvisited-finds-are-locked");
        HunterEncounterTests.Move(road,0,-2000);
        if (!road.ActOnHunger(HungerAction.InspectTrail).Accepted) throw new Exception("Trail fixture");
        string unlocked = road.Progression.SaveString();
        require(Reload(road).Progression.SaveString() == unlocked && !road.CanClaimRegionalRelic &&
            !road.ActOnHunger(HungerAction.ClaimRegionalRelic).Accepted && unlocked == road.Progression.SaveString(),
            "regional-discovery-and-load-alone-never-grant-regional-gear");

        var twin = Reload(road);
        Claim(road,RegionalRelic.Schlackengarde,require);
        Claim(twin,RegionalRelic.Schlackengarde,require);
        require(road.Progression.SaveString() == twin.Progression.SaveString() && !road.ActOnHunger(HungerAction.ClaimRegionalRelic).Accepted &&
            !road.Progression.Equipment.IsRegionalRelicEquipped(RegionalRelic.Schlackengarde), "regional-road-replay-and-no-second-grant");
        var gear = road.Progression.Equipment;
        require(road.TrySetRegionalRelicEquipped(RegionalRelic.Schlackengarde,true) &&
            !road.TrySetRegionalRelicEquipped(RegionalRelic.Schlackengarde,true) &&
            !road.TrySetRegionalRelicEquipped((RegionalRelic)100,true) &&
            Reload(road).Progression.Equipment.IsRegionalRelicEquipped(RegionalRelic.Schlackengarde),
            "regional-wear-in-inventory-deduplication-and-reload");

        Claim(road,RegionalRelic.G_Ofengabel,require);
        Claim(road,RegionalRelic.G_Zollmesser,require);
        Claim(road,RegionalRelic.G_Ziegelschild,require);
        Claim(road,RegionalRelic.G_Zunderpfanne,require);
        require(road.TrySetRegionalRelicEquipped(RegionalRelic.G_Ofengabel,true) &&
            gear.Weapon.Definition.Family == WeaponFamily.Spear && gear.Weapon.Definition.TwoHanded &&
            road.AttackRange(0) == 4400 && road.AttackCooldown(0)==4 && road.Progression.TrainingFamily == WeaponFamily.Staff &&
            !road.TrySetRegionalRelicEquipped(RegionalRelic.G_Ziegelschild,true) &&
            !road.TrySetRegionalRelicEquipped(RegionalRelic.G_Zunderpfanne,true) &&
            Reload(road).Progression.Equipment.Weapon.Definition.Id == "G_Ofengabel",
            "regional-fork-uses-real-combat-reach-tempo-training-and-both-hands");
        require(road.TrySetRegionalRelicEquipped(RegionalRelic.G_Zollmesser,true) &&
            road.TrySetRegionalRelicEquipped(RegionalRelic.G_Ziegelschild,true) && road.Progression.BlockChanceBp > 0 &&
            gear.WornRegionalRelic(RegionalRelicSlot.Shield)==RegionalRelic.G_Ziegelschild &&
            !road.TrySetRegionalRelicEquipped(RegionalRelic.G_Ofengabel,true) &&
            gear.Weapon.Definition.Family == WeaponFamily.Dagger,
            "regional-new-weapons-and-shields-obey-the-existing-owner-rules");
        road.TrySetRegionalRelicEquipped(RegionalRelic.G_Zunderpfanne,true);
        require(gear.Shield==null && road.Progression.BlockChanceBp==0 && gear.IsRegionalRelicEquipped(RegionalRelic.G_Zunderpfanne) &&
            !gear.IsRegionalRelicEquipped(RegionalRelic.G_Ziegelschild) && !road.TrySetRegionalRelicEquipped(RegionalRelic.G_Ofengabel,true) &&
            Reload(road).Progression.Equipment.RegionalWearMask == gear.RegionalWearMask, "regional-tool-occupies-offhand-and-blocks-big-weapon");
        require(road.TrySetRegionalRelicEquipped(RegionalRelic.G_Ziegelschild,true) &&
            !gear.IsRegionalRelicEquipped(RegionalRelic.G_Zunderpfanne) && road.TryUnequipWeapon(true) &&
            !gear.IsRegionalRelicEquipped(RegionalRelic.G_Ziegelschild) && road.TryUnequipWeapon() &&
            !gear.IsRegionalRelicEquipped(RegionalRelic.G_Zollmesser), "regional-legacy-slot-unequip-stays-consistent");

        HunterEncounterTests.Move(road,0,24000);
        var mill=road.TryTraversePassage();
        require(mill != null && mill.Progression.EntryAct==4 && !mill.TryGetRegionalRelicSource(RegionalRelic.Glockentraegerin,out _,out _) &&
            mill.Progression.Equipment.OwnsRegionalRelic(RegionalRelic.Schlackengarde),"regional-mill-arrival-keeps-road-item-and-gates-new-quest-gift");
        HunterEncounterTests.Move(mill,0,-2000); HunterEncounterTests.Move(mill,-4000,-2000);
        if (!mill.ActOnHunger(HungerAction.InspectShelter).Accepted) throw new Exception("Mill shelter");
        Claim(mill,RegionalRelic.Glockentraegerin,require);
        require(mill.TrySetRegionalRelicEquipped(RegionalRelic.Glockentraegerin,true) &&
            !mill.Progression.Equipment.IsRegionalRelicEquipped(RegionalRelic.Schlackengarde) &&
            mill.Progression.Equipment.OwnsRegionalRelic(RegionalRelic.Schlackengarde) &&
            Reload(mill).Progression.Equipment.IsRegionalRelicEquipped(RegionalRelic.Glockentraegerin),
            "regional-mill-outfit-replaces-only-the-worn-body");

        CheckCorruptStates(mill,require);
        CheckActualBoss(require);
        CheckSourcesAndEquipment(require);
        CheckOldPauldron(require);
    }

    private static void CheckCorruptStates(ArenaSimulation w,Action<bool,string> require)
    {
        var p=w.Progression; var gear=p.Equipment; string token=gear.Token();
        var parts=token.Split(',');long owned=long.Parse(parts[6]),wear=long.Parse(parts[7]);
        var guard=RegionalRelics.Bit(RegionalRelic.Schlackengarde);
        var lantern=RegionalRelics.Bit(RegionalRelic.Grubenlicht);
        bool Invalid(long o,long e)
        {
            var f=(string[])parts.Clone();f[6]=o.ToString();f[7]=e.ToString();
            return !WeaponEquipment.TryRead(string.Join(",",f),p,out _);
        }
        require(Invalid(owned|lantern,wear) && Invalid(owned,wear|guard) && Invalid(owned,wear|lantern) &&
            Invalid(owned|(1L<<40),wear) && Invalid(0,0),"regional-reject-unreached-boss-two-outfits-unowned-unknown-and-empty-save");
        var invalid=(string[])parts.Clone();invalid[4]="0";invalid[5]="1";
        require(!WeaponEquipment.TryRead(string.Join(",",invalid),p,out _) &&
            !WeaponEquipment.TryRead(token.Replace(","+parts[6]+",",",0"+parts[6]+","),p,out _),"regional-reject-unowned-pauldron-and-noncanonical-number");
        require(!p.Equipment.OwnsRegionalRelic(RegionalRelic.None) && !p.Equipment.IsRegionalRelicEquipped((RegionalRelic)(-1)),
            "regional-sentinel-and-invalid-id-never-index-a-bag-slot");
    }

    private static void CheckActualBoss(Action<bool,string> require)
    {
        var den=RattlerEncounterTests.Den(); var r=den.Progression.Rattler;
        require(!den.TryGetRegionalRelicSource(RegionalRelic.Grubenlicht,out _,out _),"regional-single-existing-rattler-must-be-defeated");
        for (int i=0;i<900&&!den.GameOver&&!r.Creature.IsDead;i++) RattlerEncounterTests.Fight(den,true);
        require(!den.GameOver && r.Creature.IsDead && !den.TryGetRegionalRelicSource(RegionalRelic.Grubenlicht,out _,out _),
            "regional-real-5200-health-boss-kill-does-not-skip-neck-mark");
        HunterEncounterTests.Move(den,r.Creature.Xmm,r.Creature.Zmm);
        require(den.ActOnHunger(HungerAction.InspectRoyalBrand).Accepted && !r.Reported &&
            den.TryGetRegionalRelicSource(RegionalRelic.Grubenlicht,out long x,out long z) && x==r.Creature.Xmm && z==r.Creature.Zmm,
            "regional-source-tracks-the-real-corpse-after-brand-without-reporting-to-Ilyan");
        Claim(den,RegionalRelic.Grubenlicht,require);
        require(den.TrySetRegionalRelicEquipped(RegionalRelic.Grubenlicht,true) &&
            Reload(den).Progression.Equipment.IsRegionalRelicEquipped(RegionalRelic.Grubenlicht) && !r.Reported &&
            !den.ActOnHunger(HungerAction.ClaimRegionalRelic).Accepted,"regional-single-boss-trophy-persists-without-changing-investigation");
    }

    // Late-story data is a labelled source-geometry fixture. Each retrieval and equip below enters real Core.
    private static void CheckSourcesAndEquipment(Action<bool,string> require)
    {
        var p=AschenzollTests.Station().Progression;
        var seen=new HashSet<RegionalRelic>();
        foreach (var id in RegionalRelics.All)
        {
            var src=RegionalRelics.Get(id);
            p.TravelHunger(src.Act,false);
            var world=new ArenaSimulation(123,p,1,LootCatalog.Version1);
            if (RegionalRelics.GrantedOnly(id))
            {
                // Handed over by a person, never lying in the world (Orsel's staff: HealingCourseTests).
                require(!world.TryGetRegionalRelicSource(id,out _,out _),"regional-granted-piece-has-no-world-source-"+id);
                continue;
            }
            if (src.Required == RegionalRelics.Unlock.Loot)
            {
                // Aschenwacht's gear turns up under loot (owner 2026-11-26); AschenwachtGearTests find and wear it.
                require(!world.TryGetRegionalRelicSource(id,out _,out _),"regional-loot-piece-has-no-world-source-"+id);
                continue;
            }
            if (src.Required == RegionalRelics.Unlock.Kiln)
            {
                // The Brennerhof's two weapons lie only in that yard (owner 2026-11-25); KilnYardTests take them on foot.
                require(!world.TryGetRegionalRelicSource(id,out _,out _),"regional-kiln-find-only-in-the-brennerhof-"+id);
                continue;
            }
            require(!seen.Contains(id) && world.TryGetRegionalRelicSource(id,out _,out _),"regional-walkable-authored-source-"+id);
            seen.Add(id);
            // Start at a close, walkable point for testing every individual source. Normal protagonist input must claim it.
            if (!world.TryGetRegionalRelicSource(id,out long x,out long z)) throw new Exception("Source missing: "+id);
            if (src.Act==4 || src.Act==5 || src.Act==6)
            {
                HunterEncounterTests.Move(world,0,z);
                HunterEncounterTests.Move(world,x,z);
            }
            else HunterEncounterTests.Move(world,x,z);
            Claim(world,id,require);
            var gear=world.Progression.Equipment;
            var previous=gear.WornRegionalRelic(src.Slot);
            if (previous!=RegionalRelic.None) world.TrySetRegionalRelicEquipped(previous,false);
            foreach (var hand in new[]{RegionalRelicSlot.Weapon,RegionalRelicSlot.Shield,RegionalRelicSlot.Utility})
            {
                var old=gear.WornRegionalRelic(hand);
                if(old!=RegionalRelic.None)world.TrySetRegionalRelicEquipped(old,false);
            }
            require(world.TrySetRegionalRelicEquipped(id,true) && gear.WornRegionalRelic(src.Slot)==id,"regional-usable-"+id);
            require(gear.Token()==Reload(world).Progression.Equipment.Token(),"regional-individual-save-"+id);
        }
        require(seen.Count==23 && seen.Select(RegionalRelics.Name).Distinct().Count()==23,
            "regional-every-reviewed-art-piece-has-a-unique-canonical-id-and-name");
        for (ulong seed=1;seed<=96;seed++)
        {
            var map=new CryptDungeonLayout(seed,2);
            foreach (var id in new[]{RegionalRelic.Brandreif,RegionalRelic.R_Splitterzahn})
            {
                var source=RegionalRelics.Get(id);
                require(map.CanStand(source.X,source.Z),"regional-north-hall-source-survives-floor-seed-"+id+"-"+seed);
            }
        }
    }

    private static void CheckOldPauldron(Action<bool,string> require)
    {
        var p=CharacterProgression.CreateEntryProfile();p.Equipment.Enable(p);
        var w=new ArenaSimulation(77,p,1,LootCatalog.Version1);
        var enemy=w.Monsters.Find(m=>m.Id==2);
        HunterEncounterTests.Move(w,enemy.Xmm,enemy.Zmm);
        for (int i=0;i<300&&!enemy.IsDead;i++){ w.ActivateSkill(0); w.ActivateSkill(3); w.ActivateSkill(1); w.Step(); }
        if (!enemy.IsDead) throw new Exception("Pauldron source alive");
        HunterEncounterTests.Move(w,enemy.Xmm,enemy.Zmm);
        for (int i=0;i<10&&!p.Equipment.FieldPauldronOwned;i++)w.TryCollectLoot();
        if(!p.Equipment.FieldPauldronOwned) throw new Exception("Pauldron source not collected");
        if(!w.TrySetFieldPauldronEquipped(true)) throw new Exception("Pauldron not worn");
        while(p.HungerStage<2) p.DiscoverHungerClue();
        p.TravelHunger(3,false);w=new ArenaSimulation(77,p,1,LootCatalog.Version1);
        Claim(w,RegionalRelic.Schlackengarde,require);
        require(p.Equipment.FieldPauldronOwned && p.Equipment.FieldPauldronEquipped && Reload(w).Progression.Equipment.FieldPauldronEquipped,
            "regional-new-token-preserves-preexisting-knight-kill-receipt");
        require(w.TrySetRegionalRelicEquipped(RegionalRelic.Schlackengarde,true) && !p.Equipment.FieldPauldronEquipped &&
            w.TrySetFieldPauldronEquipped(true) && !p.Equipment.IsRegionalRelicEquipped(RegionalRelic.Schlackengarde) &&
            Reload(w).Progression.Equipment.FieldPauldronEquipped, "regional-old-pauldron-and-full-outfit-mutually-exclusive-both-directions");
    }
}
