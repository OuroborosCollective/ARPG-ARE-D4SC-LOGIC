using System;
using System.Collections.Generic;
using System.Linq;
using AreArpg.Core;
internal static class WeaponEquipmentTests
{
    private static LootItem Give(CharacterProgression p,WeaponFamily family,bool? two=null)
    {
        for(ulong seed=0;seed<10000;seed++)
        {
            var item=LootCatalog.Version1.Generate(seed,1,1,1,0,p.LifetimeKills+1);var gear=new WeaponItem(item);
            if(gear.Definition.Family!=family||(two.HasValue&&gear.Definition.TwoHanded!=two.Value))continue;
            p.RecordKill();if(!p.CollectItem(item))throw new Exception("Equipment fixture receipt rejected");return item;
        }
        throw new Exception("Unreachable weapon family");
    }
    public static void Run(Action<bool,string> require)
    {
        require(WeaponCatalog.Count==36,"all-36-reviewed-model-definitions");
        require(Enumerable.Range(0,WeaponCatalog.Count).Select(i=>WeaponCatalog.Get(i).ModelStem).Distinct().Count()==WeaponCatalog.Count,"one-definition-per-distinct-model");
        var seen=new HashSet<string>();bool rareTwo=false,magicOne=false;
        for(ulong seed=0;seed<4000;seed++)
        {
            var receipt=LootCatalog.Version1.Generate(seed,1,10,1,0,1);var a=new WeaponItem(receipt);var b=new WeaponItem(receipt);
            if(a.Definition.Id!=b.Definition.Id||a.Affix!=b.Affix||a.Value!=b.Value||a.SecondValue!=b.SecondValue)throw new Exception("Gear rerolled");
            seen.Add(a.Definition.Id);rareTwo|=a.SecondValue>0&&a.Affix!=a.SecondAffix;magicOne|=a.Rarity==ItemRarity.Magic&&a.SecondValue==0;
        }
        require(seen.Count==WeaponCatalog.Count&&rareTwo&&magicOne,"every-model-can-drop-with-persistent-varied-affixes");
        var old=new CharacterProgression();var historical=Give(old,WeaponFamily.Sword);string legacy=old.SaveString();
        require(CharacterProgression.TryLoad(legacy,out var p)&&p.SaveString()==legacy&&!p.Equipment.Active,"legacy-receipts-remain-byte-stable");
        p.Equipment.Enable(p);require(p.Equipment.ForItem(historical)==null,"old-spent-shards-not-retroactively-new-equipment");
        var bow=Give(p,WeaponFamily.Bow);var shield=Give(p,WeaponFamily.Shield);var sword=Give(p,WeaponFamily.Sword,false);
        var w=new ArenaSimulation(8,p,1,LootCatalog.Version1);
        require(!w.TryEquipWeapon(100000),"cannot-equip-unowned-gear");
        require(w.TryEquipWeapon(bow.KillOrdinal)&&w.AttackRange(0)==12000&&w.AttackCooldown(0)>2,"bow-uses-real-range-and-tempo");
        require(!w.TryEquipWeapon(shield.KillOrdinal),"two-handed-bow-blocks-shield");
        var target=w.Monsters[0];target.Xmm=0;target.Zmm=8000;target.Health=target.MaxHealth=10000;
        int hp=target.Health;require(w.ActivateSkill(0).Activated&&target.Health<hp,"equipped-bow-actually-hits-at-eight-metres");
        require(w.TryEquipWeapon(sword.KillOrdinal)&&w.TryEquipWeapon(shield.KillOrdinal),"one-handed-weapon-plus-shield");
        string saved=p.SaveString();require(saved.StartsWith("AC16|")&&CharacterProgression.TryLoad(saved,out var loaded)&&loaded.SaveString()==saved&&loaded.Equipment.Weapon.Ordinal==sword.KillOrdinal&&loaded.Equipment.Shield.Ordinal==shield.KillOrdinal,"equipped-models-and-affixes-survive-save-load");
        require(w.TryEquipWeapon(bow.KillOrdinal)&&p.Equipment.Shield==null,"equipping-two-hand-clears-offhand-without-losing-item");
        require(w.TryUnequipWeapon()&&p.Items.Count==4&&new CombatStats(p,0).WeaponPoints==95L+p.WeaponBonus,"unequip-restores-baseline-and-retains-owned-items");
        var forest=WoodlandTests.Enter();forest.Progression.Equipment.Enable(forest.Progression);string nested=forest.Progression.SaveString();
        require(CharacterProgression.TryLoad(nested,out var forestLoaded)&&forestLoaded.EntryAct==8&&forestLoaded.Woodland.Seed==forest.Progression.Woodland.Seed&&forestLoaded.SaveString()==nested,"AC16-preserves-AC15-forest-checkpoint");
        var lootWorld=new ArenaSimulation(77,new CharacterProgression(),1,LootCatalog.Version1);lootWorld.Progression.Equipment.Enable(lootWorld.Progression);
        var enemy=lootWorld.Monsters[0];enemy.Xmm=1000;enemy.Zmm=0;enemy.Health=1;
        require(lootWorld.ActivateSkill(0).Kills==1&&!lootWorld.TryEquipWeapon(1),"ground-drop-is-not-owned-until-picked-up");
        require(lootWorld.TryCollectLoot()&&lootWorld.Progression.Equipment.ForItem(lootWorld.Progression.Items[0])!=null&&lootWorld.TryEquipWeapon(1),"actual-kill-pickup-equip-path");
    }
}
