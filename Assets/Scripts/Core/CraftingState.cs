using System;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace AreArpg.Core
{
    /// <summary>
    /// Persisted gathering/crafting state: skill experience, materials, one potion item per type (the tier is the
    /// affix), bandages, the once-only revive cloth, carpentry outputs, affix runes and the running buffs. It owns no
    /// clock: the simulation advances the buff countdown once per existing 100-ms step.
    /// </summary>
    public sealed class CraftingState
    {
        private readonly long[] _experience = new long[CraftingRules.SkillCount];
        private readonly int[] _materials = new int[CraftingRules.MaterialCount];
        private readonly int[] _potions = new int[CraftingRules.PotionTypeCount * CraftingRules.TierCount];
        private readonly int[] _bandages = new int[CraftingRules.TierCount];
        public int ReviveClothCount { get; private set; }
        public int WoodenFrameCount { get; private set; }
        public int BowCount { get; private set; }
        public int AffixRuneCount { get; private set; }
        public int ActiveWardTicks { get; private set; }
        public int ActiveMightTicks { get; private set; }
        public int LastHealAmount { get; private set; }
        private readonly WeaponAffix[] _runes = new WeaponAffix[8];
        public int RuneSlots { get { return _runes.Length; } }
        /// <summary>Owner set by CharacterProgression so a recipe can raise the persisted weapon power.</summary>
        internal CharacterProgression Owner { get; set; }

        public int Level(CraftSkill skill) { return CraftingRules.LevelForExperience(_experience[(int)skill]); }
        public long Experience(CraftSkill skill) { return _experience[(int)skill]; }
        public int MaterialCount(MaterialKind kind) { return _materials[(int)kind]; }

        internal void RecordExperience(CraftSkill skill, BigInteger amount)
        {
            if (amount <= BigInteger.Zero) return;
            int i = (int)skill;
            long add = amount > new BigInteger(WeaponMasteryRules.MaxExperience) ? WeaponMasteryRules.MaxExperience : (long)amount;
            _experience[i] = add > WeaponMasteryRules.MaxExperience - _experience[i] ? WeaponMasteryRules.MaxExperience : _experience[i] + add;
        }

        public void AddMaterial(MaterialKind kind, int count)
        {
            if (count < 0 || (int)kind < 0 || (int)kind >= CraftingRules.MaterialCount) throw new ArgumentOutOfRangeException(nameof(kind));
            _materials[(int)kind] = Math.Min(int.MaxValue - count, _materials[(int)kind]) + count;
        }

        private bool Has(MaterialKind kind, int count) { return _materials[(int)kind] >= count; }
        private void Take(MaterialKind kind, int count) { _materials[(int)kind] -= count; }

        /// <summary>Gather at a real spot: add reproducible yields with the same saturation as AddMaterial, and train
        /// only on the amount actually stored (a full stock earns nothing).</summary>
        public bool GatherAt(CraftSkill skill, ulong seed, out int[] yields)
        {
            yields = CraftingRules.Gather(skill, Level(skill), seed);
            int stored = 0;
            for (int i = 0; i < yields.Length; i++)
            {
                if (yields[i] <= 0) continue;
                int free = int.MaxValue - _materials[i];
                int take = Math.Min(yields[i], free);
                _materials[i] += take;
                stored += take;
            }
            if (stored > 0) RecordExperience(skill, CraftingRules.ExperienceFromEffectiveAmount(stored));
            return stored > 0;
        }

        public int PotionCount(PotionType type, PotionTier tier) { return _potions[(int)type * CraftingRules.TierCount + (int)tier]; }
        public int PotionCount(PotionType type) { int n = 0; for (int t = 0; t < CraftingRules.TierCount; t++) n += PotionCount(type, (PotionTier)t); return n; }
        public void AddPotion(PotionType type, PotionTier tier) { _potions[(int)type * CraftingRules.TierCount + (int)tier]++; }

        /// <summary>Brew one potion of the type; the tier is a level-based random affix, never a chosen item.</summary>
        public bool BrewPotion(PotionType type, ulong seed)
        {
            MaterialKind a, b; int ca, cb;
            switch (type)
            {
                case PotionType.Heal: a = MaterialKind.Ashenflieder; ca = 2; b = MaterialKind.Glutklee; cb = 1; break;
                case PotionType.Ward: a = MaterialKind.Grauwurz; ca = 1; b = MaterialKind.Dornbeere; cb = 1; break;
                default: a = MaterialKind.Glutklee; ca = 1; b = MaterialKind.Nachtbluete; cb = 1; break;
            }
            if (!Has(a, ca) || !Has(b, cb)) return false;
            Take(a, ca); Take(b, cb);
            AddPotion(type, CraftingRules.RollTier(Level(CraftSkill.Alchemy), seed));
            RecordExperience(CraftSkill.Alchemy, CraftingRules.ExperienceFromEffectiveAmount(ca + cb));
            return true;
        }

        /// <summary>Drink the highest available tier of a potion type; applies its effect and starts the buff.</summary>
        public bool DrinkPotion(PotionType type)
        {
            for (int t = CraftingRules.TierCount - 1; t >= 0; t--)
            {
                var tier = (PotionTier)t;
                if (PotionCount(type, tier) == 0) continue;
                _potions[(int)type * CraftingRules.TierCount + t]--;
                int ticks = CraftingRules.DurationTicks(tier);
                if (type == PotionType.Heal) LastHealAmount = CraftingRules.HealAmount(tier);
                else if (type == PotionType.Ward) ActiveWardTicks = Math.Max(ActiveWardTicks, ticks);
                else ActiveMightTicks = Math.Max(ActiveMightTicks, ticks);
                return true;
            }
            return false;
        }

        public int DefenseBonusBp { get { return ActiveWardTicks > 0 ? CraftingRules.BuffBonusBp : 0; } }
        public int AttackBonusBp { get { return ActiveMightTicks > 0 ? CraftingRules.BuffBonusBp : 0; } }

        public int BandageCount(BandageTier tier) { return _bandages[(int)tier]; }

        public bool CraftBandage(BandageTier tier)
        {
            MaterialKind a, b; int ca, cb;
            switch (tier)
            {
                case BandageTier.Medium: a = MaterialKind.Ashenflieder; ca = 2; b = MaterialKind.Grauwurz; cb = 1; break;
                case BandageTier.Mighty: a = MaterialKind.Nachtbluete; ca = 2; b = MaterialKind.Grauwurz; cb = 2; break;
                default: a = MaterialKind.Ashenflieder; ca = 1; b = a; cb = 0; break;
            }
            if (!Has(a, ca) || (cb > 0 && !Has(b, cb))) return false;
            Take(a, ca); if (cb > 0) Take(b, cb);
            _bandages[(int)tier]++;
            RecordExperience(CraftSkill.FirstAid, CraftingRules.ExperienceFromEffectiveAmount(ca + cb));
            return true;
        }

        public bool CraftReviveCloth()
        {
            if (!Has(MaterialKind.Nachtbluete, 1) || !Has(MaterialKind.Glutstein, 1)) return false;
            Take(MaterialKind.Nachtbluete, 1); Take(MaterialKind.Glutstein, 1);
            ReviveClothCount++;
            RecordExperience(CraftSkill.FirstAid, CraftingRules.ExperienceFromEffectiveAmount(20));
            return true;
        }

        /// <summary>First-Aid level required to even attempt the holy revive cloth.</summary>
        public const int HolyReviveClothLevel = 85;
        /// <summary>Attempt cost: extreme, rare resources; consumed whether or not the attempt succeeds.</summary>
        public const int HolyRunenerz = 8;
        public const int HolyGlutstein = 6;
        public const int HolyNachtbluete = 6;
        public const int HolyDornbeere = 4;

        /// <summary>Attempt the holy revive cloth: needs First-Aid 85+, huge rare resources and a level-based roll.
        /// The materials are spent either way; only a successful attempt yields the cloth.</summary>
        public bool CraftHolyReviveCloth(ulong seed)
        {
            if (Level(CraftSkill.FirstAid) < HolyReviveClothLevel) return false;
            if (!Has(MaterialKind.Runenerz, HolyRunenerz) || !Has(MaterialKind.Glutstein, HolyGlutstein) ||
                !Has(MaterialKind.Nachtbluete, HolyNachtbluete) || !Has(MaterialKind.Dornbeere, HolyDornbeere)) return false;
            Take(MaterialKind.Runenerz, HolyRunenerz); Take(MaterialKind.Glutstein, HolyGlutstein);
            Take(MaterialKind.Nachtbluete, HolyNachtbluete); Take(MaterialKind.Dornbeere, HolyDornbeere);
            RecordExperience(CraftSkill.FirstAid, CraftingRules.ExperienceFromEffectiveAmount(30));
            if (!CraftingRules.HolyReviveSucceeds(Level(CraftSkill.FirstAid), seed)) return false;
            ReviveClothCount++;
            return true;
        }

        /// <summary>Use the revive cloth once; afterwards it is gone and cannot revive again.</summary>
        public bool ConsumeReviveCloth()
        {
            if (ReviveClothCount <= 0) return false;
            ReviveClothCount--;
            return true;
        }

        public bool CraftWoodenFrame()
        {
            if (!Has(MaterialKind.Holz, 2)) return false;
            Take(MaterialKind.Holz, 2); WoodenFrameCount++;
            RecordExperience(CraftSkill.Carpentry, CraftingRules.ExperienceFromEffectiveAmount(4));
            return true;
        }

        public bool CraftBow()
        {
            if (WoodenFrameCount < 1 || !Has(MaterialKind.Dornbeere, 1)) return false;
            WoodenFrameCount--; Take(MaterialKind.Dornbeere, 1); BowCount++;
            RecordExperience(CraftSkill.Carpentry, CraftingRules.ExperienceFromEffectiveAmount(10));
            return true;
        }

        public bool ImproveWeapon()
        {
            if (Owner == null || !Has(MaterialKind.Ascheisen, 2)) return false;
            Take(MaterialKind.Ascheisen, 2);
            Owner.AddCraftedWeaponPower(17);
            RecordExperience(CraftSkill.Smithing, CraftingRules.ExperienceFromEffectiveAmount(17));
            return true;
        }

        public bool ReinforceWeapon()
        {
            if (Owner == null || !Has(MaterialKind.Grausilber, 1) || !Has(MaterialKind.Glutstein, 1)) return false;
            Take(MaterialKind.Grausilber, 1); Take(MaterialKind.Glutstein, 1);
            Owner.AddCraftedWeaponPower(34);
            RecordExperience(CraftSkill.Smithing, CraftingRules.ExperienceFromEffectiveAmount(34));
            return true;
        }

        public bool CraftAffixRune(WeaponAffix affix)
        {
            if (AffixRuneCount >= _runes.Length || !Has(MaterialKind.Runenerz, 2) || !Has(MaterialKind.Grausilber, 1)) return false;
            Take(MaterialKind.Runenerz, 2); Take(MaterialKind.Grausilber, 1);
            _runes[AffixRuneCount] = affix;
            AffixRuneCount++;
            RecordExperience(CraftSkill.Smithing, CraftingRules.ExperienceFromEffectiveAmount(15));
            return true;
        }

        public WeaponAffix RuneAt(int index) { return index >= 0 && index < AffixRuneCount && index < _runes.Length ? _runes[index] : WeaponAffix.Power; }

        /// <summary>Advance running buffs one 100-ms tick; called exactly once per simulation step.</summary>
        internal void AdvanceTick()
        {
            if (ActiveWardTicks > 0) ActiveWardTicks--;
            if (ActiveMightTicks > 0) ActiveMightTicks--;
        }
        public bool HasProgress
        {
            get
            {
                foreach (long xp in _experience) if (xp > 0) return true;
                foreach (int m in _materials) if (m > 0) return true;
                foreach (int p in _potions) if (p > 0) return true;
                foreach (int b in _bandages) if (b > 0) return true;
                return ReviveClothCount > 0 || WoodenFrameCount > 0 || BowCount > 0 || AffixRuneCount > 0 ||
                    ActiveWardTicks > 0 || ActiveMightTicks > 0;
            }
        }

        internal string Token()
        {
            var b = new StringBuilder("1");
            foreach (long xp in _experience) b.Append(',').Append(xp.ToString(CultureInfo.InvariantCulture));
            foreach (int m in _materials) b.Append(',').Append(m.ToString(CultureInfo.InvariantCulture));
            foreach (int p in _potions) b.Append(',').Append(p.ToString(CultureInfo.InvariantCulture));
            foreach (int x in _bandages) b.Append(',').Append(x.ToString(CultureInfo.InvariantCulture));
            b.Append(',').Append(ReviveClothCount.ToString(CultureInfo.InvariantCulture));
            b.Append(',').Append(WoodenFrameCount.ToString(CultureInfo.InvariantCulture));
            b.Append(',').Append(BowCount.ToString(CultureInfo.InvariantCulture));
            b.Append(',').Append(AffixRuneCount.ToString(CultureInfo.InvariantCulture));
            b.Append(',').Append(ActiveWardTicks.ToString(CultureInfo.InvariantCulture));
            b.Append(',').Append(ActiveMightTicks.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < _runes.Length; i++) b.Append(',').Append(((int)_runes[i]).ToString(CultureInfo.InvariantCulture));
            return b.ToString();
        }

        private static bool Canonical(string text, long max, out long value)
        {
            value = 0;
            if (string.IsNullOrEmpty(text) || text.Length > 12 || (text.Length > 1 && text[0] == '0')) return false;
            foreach (char c in text) if (c < '0' || c > '9') return false;
            return long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value <= max;
        }

        internal static bool TryRead(string token, out CraftingState state)
        {
            state = null;
            if (string.IsNullOrEmpty(token)) return false;
            string[] f = token.Split(',');
            int expected = 1 + CraftingRules.SkillCount + CraftingRules.MaterialCount + CraftingRules.PotionTypeCount * CraftingRules.TierCount +
                CraftingRules.TierCount + 6 + 8;
            if (f.Length != expected || f[0] != "1") return false;
            var s = new CraftingState();
            int i = 1;
            for (int k = 0; k < CraftingRules.SkillCount; k++, i++) if (!Canonical(f[i], WeaponMasteryRules.MaxExperience, out s._experience[k])) return false;
            for (int k = 0; k < CraftingRules.MaterialCount; k++, i++) if (!Canonical(f[i], int.MaxValue, out long m)) return false; else s._materials[k] = (int)m;
            for (int k = 0; k < s._potions.Length; k++, i++) if (!Canonical(f[i], int.MaxValue, out long p)) return false; else s._potions[k] = (int)p;
            for (int k = 0; k < CraftingRules.TierCount; k++, i++) if (!Canonical(f[i], int.MaxValue, out long bn)) return false; else s._bandages[k] = (int)bn;
            if (!Canonical(f[i++], int.MaxValue, out long rv)) return false; s.ReviveClothCount = (int)rv;
            if (!Canonical(f[i++], int.MaxValue, out long wf)) return false; s.WoodenFrameCount = (int)wf;
            if (!Canonical(f[i++], int.MaxValue, out long bw)) return false; s.BowCount = (int)bw;
            if (!Canonical(f[i++], int.MaxValue, out long ar)) return false; s.AffixRuneCount = (int)ar;
            if (!Canonical(f[i++], int.MaxValue, out long wd)) return false; s.ActiveWardTicks = (int)wd;
            if (!Canonical(f[i++], int.MaxValue, out long mt)) return false; s.ActiveMightTicks = (int)mt;
            for (int k = 0; k < 8; k++, i++) if (!Canonical(f[i], 3, out long a)) return false; else s._runes[k] = (WeaponAffix)(int)a;
            state = s;
            return true;
        }
    }

    public sealed partial class CharacterProgression
    {
        private CraftingState _crafting = new CraftingState();
        /// <summary>Gathering/crafting state; the owner is bound lazily so smithing can raise weapon power.</summary>
        public CraftingState Crafting { get { _crafting.Owner = this; return _crafting; } }
        private const string CraftSectionName = "crafting";

        /// <summary>Raise the persisted weapon power from a smithing recipe without minting a loot receipt.</summary>
        internal void AddCraftedWeaponPower(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            WeaponBonus = amount > int.MaxValue - WeaponBonus ? int.MaxValue : WeaponBonus + amount;
        }

        private void SyncCraftSection()
        {
            if (Crafting.HasProgress) SetSection(CraftSectionName, Crafting.Token());
            else _sections.Remove(CraftSectionName);
        }

        private void RestoreCraftSection()
        {
            string token;
            if (_sections.TryGetValue(CraftSectionName, out token) && CraftingState.TryRead(token, out var state))
            {
                state.Owner = this;
                _crafting = state;
            }
        }
    }
}
