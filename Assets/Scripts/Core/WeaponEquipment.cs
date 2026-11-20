using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace AreArpg.Core
{
    public enum WeaponFamily { Sword, Dagger, Axe, Mace, Staff, Bow, Spear, Shield }
    public enum WeaponAffix { Power, Haste, Ward, Siphon }
    public sealed class WeaponDefinition
    {
        public readonly string Id,Name,ModelStem;
        public readonly WeaponFamily Family, TrainingFamily;
        private readonly string _visualFolder;
        public readonly int BasePower,StrikeCooldown;
        public readonly long RangeMm;
        public readonly bool TwoHanded;
        public string FamilyName => new[]{"Schwert","Dolch","Axt","Keule","Stab","Bogen","Speer","Schild"}[(int)Family];
        public string VisualKey => _visualFolder + Id;
        internal WeaponDefinition(string id,string name,string model,WeaponFamily family,int power,int cooldown,long range,bool twoHanded=false,
            WeaponFamily? trainingFamily=null, string visualFolder="Weapons/")
        {
            Id=id; Name=name; ModelStem=model; Family=family; BasePower=power; StrikeCooldown=cooldown;
            RangeMm=range; TwoHanded=twoHanded; TrainingFamily=trainingFamily??family; _visualFolder=visualFolder;
        }
    }
    /// <summary>Immutable equipment projection of a verified kill receipt. Catalog revision is pinned by AC16.</summary>
    public sealed class WeaponItem
    {
        public readonly WeaponDefinition Definition;
        public readonly BigInteger Ordinal;
        public readonly ItemRarity Rarity;
        public readonly WeaponAffix Affix,SecondAffix;
        public readonly int Value,SecondValue;
        // Authored regional gear has a fixed source and no random affixes or kill ordinal.
        internal WeaponItem(WeaponDefinition regional)
        { Definition = regional; }
        internal WeaponItem(LootItem receipt)
        {
            var rng=new DeterministicRng(receipt.StableHash ^ 0xE0111UL);
            Definition=WeaponCatalog.Get((int)(rng.Next64()%(ulong)WeaponCatalog.Count));Ordinal=receipt.KillOrdinal;Rarity=receipt.Rarity;
            Affix=(WeaponAffix)(rng.Next64()%4);Value=Roll(rng,Affix);
            SecondAffix=(WeaponAffix)(((int)Affix+1+(int)(rng.Next64()%3))%4);SecondValue=Rarity==ItemRarity.Rare?Roll(rng,SecondAffix):0;
        }
        private static int Roll(DeterministicRng rng,WeaponAffix affix) => affix==WeaponAffix.Power?200+(int)(rng.Next64()%601):affix==WeaponAffix.Haste?500+(int)(rng.Next64()%1001):affix==WeaponAffix.Ward?200+(int)(rng.Next64()%401):100+(int)(rng.Next64()%301);
        public int Bonus(WeaponAffix affix) => (Affix==affix?Value:0)+(SecondAffix==affix?SecondValue:0);
        public string DisplayName => Ordinal.IsZero ? Definition.Name : Definition.Name+" "+new[]{"der Kraft","der Eile","der Wacht","des Zehrens"}[(int)Affix];
        public string AffixDescription => Ordinal.IsZero ? string.Empty : Describe(Affix,Value)+(SecondValue>0?"\n"+Describe(SecondAffix,SecondValue):"");
        private static string Describe(WeaponAffix a,int value) => new[]{"Schaden +","Abklingzeit −","Schadensminderung +","Lebensraub "}[(int)a]+(value/100m).ToString("0.##",CultureInfo.InvariantCulture)+" %";
    }
    public sealed partial class WeaponEquipment
    {
        public bool Active { get; private set; }
        public BigInteger FirstOrdinal { get; private set; }
        public WeaponItem Weapon { get; private set; }
        public WeaponItem Shield { get; private set; }
        public BigInteger FieldPauldronOrdinal { get; private set; }
        public bool FieldPauldronOwned => FieldPauldronOrdinal > 0;
        public bool FieldPauldronEquipped { get; private set; }

        internal void CollectFieldPauldron(LootItem receipt)
        {
            if (Active && !FieldPauldronOwned && receipt.KillOrdinal >= FirstOrdinal)
                FieldPauldronOrdinal = receipt.KillOrdinal;
        }

        internal bool SetFieldPauldronEquipped(bool equipped)
        {
            if (!FieldPauldronOwned || equipped == FieldPauldronEquipped) return false;
            if (equipped) ClearRegionalSlot(RegionalRelicSlot.Body);
            FieldPauldronEquipped = equipped;
            return true;
        }
        public void Enable(CharacterProgression p) { if(!Active) { Active=true;FirstOrdinal=p.LootOrdinal+1; } }
        public WeaponItem ForItem(LootItem item) => Active&&item!=null&&item.KillOrdinal>=FirstOrdinal?new WeaponItem(item):null;
        public int Bonus(WeaponAffix affix) => (Weapon?.Bonus(affix)??0)+(Shield?.Bonus(affix)??0);
        internal bool Equip(CharacterProgression p,BigInteger ordinal)
        {
            var receipt=p.Items.FirstOrDefault(i=>i.KillOrdinal==ordinal);var item=ForItem(receipt);if(item==null)return false;
            if(item.Definition.Family==WeaponFamily.Shield)
            {
                if(Weapon!=null&&Weapon.Definition.TwoHanded)return false;
                ClearRegionalSlot(RegionalRelicSlot.Shield);
                ClearRegionalSlot(RegionalRelicSlot.Utility);
                Shield=item;
            }
            else
            {
                // Owner decision 2026-11-17: while a shield is carried, only one-handed weapons can be equipped.
                if(item.Definition.TwoHanded&&(Shield!=null || WornRegionalRelic(RegionalRelicSlot.Utility)!=RegionalRelic.None))return false;
                ClearRegionalSlot(RegionalRelicSlot.Weapon);
                Weapon=item;
            }
            return true;
        }
        internal bool Unequip(bool shield)
        {
            if ((shield ? Shield : Weapon) == null) return false;
            ClearRegionalSlot(shield ? RegionalRelicSlot.Shield : RegionalRelicSlot.Weapon);
            if (shield) Shield = null; else Weapon = null;
            return true;
        }
        internal string Token()
        {
            var culture = CultureInfo.InvariantCulture;
            string slots = FirstOrdinal.ToString(culture) + "," + (Weapon?.Ordinal ?? BigInteger.Zero).ToString(culture) +
                "," + (Shield?.Ordinal ?? BigInteger.Zero).ToString(culture);
            if (!FieldPauldronOwned && _ownedRelics == 0) return "1," + slots;
            slots += "," + FieldPauldronOrdinal.ToString(culture) + "," + (FieldPauldronEquipped ? "1" : "0");
            return _ownedRelics == 0 ? "2," + slots :
                "3," + slots + "," + _ownedRelics.ToString(culture) + "," + _wornRelics.ToString(culture);
        }
        internal static bool TryRead(string text,CharacterProgression p,out WeaponEquipment equipment)
        {
            equipment=null;var f=text.Split(',');
            bool regional = f.Length == 8 && f[0] == "3";
            bool shoulder = f.Length == 6 && f[0] == "2";
            if (!regional && !shoulder && (f.Length != 4 || f[0] != "1")) return false;
            bool hasShoulder = shoulder || (regional && f[4] != "0");
            if (regional && !hasShoulder && f[5] != "0") return false;
            var n=new BigInteger[3];
            for(int i=0;i<3;i++)if(!BigInteger.TryParse(f[i+1],NumberStyles.None,CultureInfo.InvariantCulture,out n[i])||n[i]<0||n[i].ToString(CultureInfo.InvariantCulture)!=f[i+1])return false;
            if(n[0]<1||n[0]>p.LootOrdinal+1)return false;
            var e=new WeaponEquipment { Active=true,FirstOrdinal=n[0] };
            if(n[1]>0&&(!e.Equip(p,n[1])||e.Weapon==null))return false;
            if(n[2]>0&&(!e.Equip(p,n[2])||e.Shield==null))return false;
            if((e.Weapon?.Ordinal??BigInteger.Zero)!=n[1]||(e.Shield?.Ordinal??BigInteger.Zero)!=n[2])return false;
            var pending = p.Woodland.Loot.Find(l => l.HasFieldPauldron);
            if (pending != null && (hasShoulder || pending.Item.KillOrdinal < e.FirstOrdinal)) return false;
            if (hasShoulder)
            {
                if (!BigInteger.TryParse(f[4], NumberStyles.None, CultureInfo.InvariantCulture, out var origin) ||
                    origin.ToString(CultureInfo.InvariantCulture) != f[4] || origin < e.FirstOrdinal ||
                    (f[5] != "0" && f[5] != "1")) return false;
                var receipt = p.Items.FirstOrDefault(i => i.KillOrdinal == origin);
                if (receipt == null || !receipt.IsFieldPauldronSource(p)) return false;
                e.FieldPauldronOrdinal = origin;
                e.FieldPauldronEquipped = f[5] == "1";
            }
            if (regional && !e.RestoreRegionalSlots(p,f[6],f[7])) return false;
            equipment=e;return true;
        }
    }
    public sealed partial class CharacterProgression
    {
        public WeaponEquipment Equipment { get; private set; }=new WeaponEquipment();
        private string EquipmentPayload()
        {
            string body=BasePayload();if(!Equipment.Active)return body;
            string legacy=body+"|"+HashText(body).ToString("X16",CultureInfo.InvariantCulture);
            return "AC16|"+Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(legacy))+"|"+Equipment.Token();
        }
        private static bool TryLoadEquipment(string saved,WeaponMasteryState restoring,out CharacterProgression progress)
        {
            progress=null;var f=saved.Split('|');if(f.Length!=4||f[0]!="AC16"||f[3]!=HashText(string.Join("|",f,0,3)).ToString("X16",CultureInfo.InvariantCulture))return false;
            string inner;
            try { var bytes=Convert.FromBase64String(f[1]);if(Convert.ToBase64String(bytes)!=f[1])return false;inner=new System.Text.UTF8Encoding(false,true).GetString(bytes); }
            catch(FormatException){return false;}catch(System.Text.DecoderFallbackException){return false;}
            if(inner.StartsWith("AC16|",StringComparison.Ordinal)||inner.StartsWith("AC17|",StringComparison.Ordinal)||inner.StartsWith("AC18|",StringComparison.Ordinal)||inner.StartsWith("AC19|",StringComparison.Ordinal)||!TryLoadRestoring(inner,restoring,out var p,insideEquipment: true)||!WeaponEquipment.TryRead(f[2],p,out var equipment))return false;
            p.Equipment=equipment;progress=p;return true;
        }
    }
    public sealed partial class ArenaSimulation
    {
        public bool TrySetFieldPauldronEquipped(bool equipped) =>
            !GameOver && Progression.Equipment.SetFieldPauldronEquipped(equipped);

        public bool TryEquipWeapon(BigInteger ordinal)
        {
            if(ordinal>0&&(Progression.Companion.WeaponOrdinal==ordinal||Progression.Companion.ShieldOrdinal==ordinal))return false;
            if(GameOver||!Progression.Equipment.Equip(Progression,ordinal))return false;
            SaveRoadPlayer();SaveInvestigationPlayer();SaveWoodlandPlayer();return true;
        }
        public bool TryUnequipWeapon(bool shield=false) => !GameOver&&Progression.Equipment.Unequip(shield);
        public long AttackRange(int slot) => slot<=1&&Progression.Equipment.Weapon!=null?Math.Max(slot==0?0:SkillCatalog.Get(slot).RangeMm,Progression.Equipment.Weapon.Definition.RangeMm):SkillCatalog.Get(slot).RangeMm;
        public int AttackCooldown(int slot)
        {
            var e=Progression.Equipment;int cooldown=slot==0&&e.Weapon!=null?e.Weapon.Definition.StrikeCooldown:SkillCatalog.Get(slot).CooldownTicks;
            return Math.Max(1,(cooldown*(10000-e.Bonus(WeaponAffix.Haste))+9999)/10000);
        }
    }
}
