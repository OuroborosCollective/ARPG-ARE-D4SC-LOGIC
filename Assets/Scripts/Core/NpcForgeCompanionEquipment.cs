using System;
using System.Linq;
using System.Numerics;

namespace AreArpg.Core
{
    public sealed partial class ArenaSimulation
    {
        public WeaponItem CompanionWeapon => CompanionItem(Progression.Companion.WeaponOrdinal);
        public WeaponItem CompanionShield => CompanionItem(Progression.Companion.ShieldOrdinal);
        private WeaponItem CompanionItem(BigInteger ordinal) => ordinal<=0?null:Progression.Equipment.ForItem(Progression.Items.FirstOrDefault(i=>i.KillOrdinal==ordinal));
        private static readonly WeaponDefinition HunterBow=Enumerable.Range(0,WeaponCatalog.Count).Select(WeaponCatalog.Get).First(w=>w.Id=="bow_newbie.1");
        public WeaponDefinition CompanionWeaponDefinition => CompanionWeapon?.Definition??HunterBow;
        public int CompanionBonus(WeaponAffix affix) => (CompanionWeapon?.Bonus(affix)??0)+(CompanionShield?.Bonus(affix)??0);
        public bool CanManageCompanionEquipment => !GameOver&&CompanionPresent&&DistanceSquared(PlayerXmm,PlayerZmm,Progression.Companion.Xmm,Progression.Companion.Zmm)<=FixedMath.Square(3500);
        public bool TryEquipCompanionWeapon(BigInteger ordinal)
        {
            var c=Progression.Companion;var item=CompanionItem(ordinal);
            if(!CanManageCompanionEquipment||c.Status==CompanionStatus.Dead||item==null)return false;
            if(item.Definition.Family==WeaponFamily.Shield)
            {if(CompanionWeaponDefinition.TwoHanded)return false;c.ShieldOrdinal=ordinal;}
            else {c.WeaponOrdinal=ordinal;if(item.Definition.TwoHanded)c.ShieldOrdinal=0;}
            if(Progression.Equipment.Weapon?.Ordinal==ordinal)Progression.Equipment.Unequip(false);
            if(Progression.Equipment.Shield?.Ordinal==ordinal)Progression.Equipment.Unequip(true);
            return true;
        }
        public bool TryUnequipCompanionWeapon(bool shield=false)
        {
            if(!CanManageCompanionEquipment)return false;var c=Progression.Companion;
            if(shield){if(c.ShieldOrdinal.IsZero)return false;c.ShieldOrdinal=0;}
            else {if(c.WeaponOrdinal.IsZero)return false;c.WeaponOrdinal=0;c.ShieldOrdinal=0;}
            return true;
        }
    }
}
