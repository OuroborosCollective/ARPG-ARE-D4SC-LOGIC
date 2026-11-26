using System;
using System.Collections.Generic;
using System.Globalization;

namespace AreArpg.Core
{
    // Values are persisted as bit positions. Append new pieces instead of reordering the existing ones.
    public enum RegionalRelic
    {
        None, Schlackengarde, Glockentraegerin, Grubenlicht, Rattlerkluft, Brandreif,
        R_Gratkeule, R_Splitterzahn, R_Scharrbeil, R_Rippenschild, R_Grabschild,
        G_Ofengabel, G_Zollmesser, G_Hammerbeil, G_Zollbuckler, G_Zunderpfanne,
        M_Wehrsaebel, M_Lotklinge, M_Jochbeil, M_Wehrschild, M_Glockenring,
        G_Ziegelschild, M_Handglocke, G_Schuerhaken, M_Heilerstab,
        B_Brennerspeer, B_Scherbenhellebarde,
        A_Visierhelm, A_Kuerass, A_Schulterplatten, A_Gliedpanzer, A_KlauenstabEinhand, A_KlauenstabZweihand,
        N_Schaller, N_Kuerass, N_Schulterstuecke, N_Armschienen, N_Handschuhe, N_Guertung, N_Beinzeug, N_Panzerschuhe
    }

    /// <summary>
    /// One worn piece per place. Since the Nachtvesper (2026-11-26) arms, hands, waist, legs and feet have places of their
    /// own; Aschenwacht's limb armour covers arms and legs at once, so it and an arm or leg piece take each other's place.
    /// </summary>
    public enum RegionalRelicSlot { Body, Belt, Weapon, Shield, Utility, Head, Shoulders, Limbs, Arms, Hands, Waist, Legs, Feet }

    /// <summary>Fixed sources for the regional gear. Version-one random weapon rolls have their own frozen catalog.</summary>
    public static class RegionalRelics
    {
        internal enum Unlock { Trail, Shelter, Beds, Trace, Brand, Toll, Kiln, Loot }
        internal sealed class Source
        {
            public readonly RegionalRelic Id;
            public readonly string Name, Location;
            public readonly RegionalRelicSlot Slot;
            public readonly int Act;
            public readonly long X, Z;
            public readonly Unlock Required;
            public readonly WeaponDefinition Hand;
            /// <summary>Handed over by a person (Orsel's staff after her lesson); it never lies in the world to be picked up.</summary>
            public readonly bool Granted;

            public Source(RegionalRelic id, string name, RegionalRelicSlot slot, int act, long x, long z,
                Unlock required, string location, WeaponFamily? family = null, int power = 0,
                int cooldown = 0, long reach = 0, bool heavy = false, WeaponFamily? training = null,
                bool granted = false, string visualKey = null)
            {
                Id = id; Name = name; Slot = slot; Act = act; X = x; Z = z;
                Required = required; Location = location; Granted = granted;
                if (family.HasValue)
                    Hand = new WeaponDefinition(id.ToString(), name, id.ToString(), family.Value, power, cooldown,
                        reach, heavy, training, "Werkarsenal/", visualKey);
            }
            public bool HistoricalSource(CharacterProgression p)
            {
                switch (Required)
                {
                    case Unlock.Trail: return p.HungerStage >= 2;
                    case Unlock.Shelter: return p.HungerStage >= 3;
                    case Unlock.Beds: return p.HungerStage >= 4;
                    case Unlock.Trace: return p.Investigation.TraceFound;
                    case Unlock.Brand: return p.Rattler.BrandExamined;
                    case Unlock.Toll: return p.Woodland.ReachedAschenzoll;
                    // The Brennerhof lies in Aschenwacht, which the wanderer passes once on his way to the village.
                    case Unlock.Kiln: return p.EntryAct >= 2 || p.InCourt;
                    // Aschenwacht's gear and the Nachtvesper are found under ordinary loot (AschenwachtGear, NachtvesperSet):
                    // they never lie anywhere by themselves.
                    case Unlock.Loot: return true;
                    default: return false;
                }
            }
        }

        private static readonly Source[] Sources =
        {
            null,
            new Source(RegionalRelic.Schlackengarde, "Montur der Schlackengarde", RegionalRelicSlot.Body, 3, 3400,-1400, Unlock.Trail, "Aschestraße · bei der Schlackengarde"),
            new Source(RegionalRelic.Glockentraegerin, "Montur der Glockenträgerin", RegionalRelicSlot.Body, 4,-6800,2400, Unlock.Shelter, "Alte Mühle · bei der Glockenträgerin"),
            new Source(RegionalRelic.Grubenlicht, "Grubenlicht", RegionalRelicSlot.Belt, 7,0,0, Unlock.Brand, "Rattlernest · am Halsband des gefallenen Tiefscharrers"),
            new Source(RegionalRelic.Rattlerkluft, "Kluft der Scharrgräber", RegionalRelicSlot.Body, 5,4200,10500, Unlock.Brand, "Alter Speicher · Gestell am Nordgang"),
            new Source(RegionalRelic.Brandreif, "Brandreif", RegionalRelicSlot.Head, 6,2300,15800, Unlock.Trace, "Verborgene Krypta · Nordsaal beim alten Riegel"),
            new Source(RegionalRelic.R_Gratkeule, "Gratkeule", RegionalRelicSlot.Weapon, 5,-2700,11400, Unlock.Brand, "Alter Speicher · westliches Frachtregal", WeaponFamily.Mace,182,5,3750,true,WeaponFamily.Axe),
            new Source(RegionalRelic.R_Splitterzahn, "Splitterzahn", RegionalRelicSlot.Weapon, 6,-2800,15800, Unlock.Brand, "Verborgene Krypta · westliche Nische des Nordsaals", WeaponFamily.Dagger,95,2,2300),
            new Source(RegionalRelic.R_Scharrbeil, "Scharrbeil", RegionalRelicSlot.Weapon, 5,4100,-8900, Unlock.Brand, "Alter Speicher · Kerbe im Ladeboden", WeaponFamily.Axe,202,5,3900,true),
            new Source(RegionalRelic.R_Rippenschild, "Rippenschild", RegionalRelicSlot.Shield, 5,-4300,-3200, Unlock.Brand, "Alter Speicher · unter den Bindegurten", WeaponFamily.Shield,740,3,3100),
            new Source(RegionalRelic.R_Grabschild, "Grabschild", RegionalRelicSlot.Shield, 10,12500,-4000, Unlock.Toll, "Aschenzoll · neben dem Frachtkäfig", WeaponFamily.Shield,620,3,3100),
            new Source(RegionalRelic.G_Ofengabel, "Ofengabel", RegionalRelicSlot.Weapon, 3,-5100,-12600, Unlock.Trail, "Aschestraße · Stellplatz des Kohlenkarrens", WeaponFamily.Spear,147,4,4400,true,WeaponFamily.Staff),
            new Source(RegionalRelic.G_Zollmesser, "Zollmesser", RegionalRelicSlot.Weapon, 3,5700,-9700, Unlock.Trail, "Aschestraße · alte Zahlstelle", WeaponFamily.Dagger,86,2,2300),
            new Source(RegionalRelic.G_Hammerbeil, "Hammerbeil", RegionalRelicSlot.Weapon, 3,6700,7200, Unlock.Trail, "Aschestraße · Schutt des Ofenplatzes", WeaponFamily.Axe,193,5,3750,true),
            new Source(RegionalRelic.G_Zollbuckler, "Zollbuckler", RegionalRelicSlot.Shield, 10,-2200,-12000, Unlock.Toll, "Aschenzoll · beim Frachtanschlag", WeaponFamily.Shield,680,3,3100),
            new Source(RegionalRelic.G_Zunderpfanne, "Zunderpfanne", RegionalRelicSlot.Utility, 3,-6100,6200, Unlock.Trail, "Aschestraße · Feuerstelle am Westrand"),
            new Source(RegionalRelic.M_Wehrsaebel, "Wehrsäbel", RegionalRelicSlot.Weapon, 4,-8200,-11800, Unlock.Shelter, "Alte Mühle · südlicher Wehrzugang", WeaponFamily.Sword,169,4,4050,true),
            new Source(RegionalRelic.M_Lotklinge, "Lotklinge", RegionalRelicSlot.Weapon, 4,-5600,-6400, Unlock.Shelter, "Alte Mühle · Lot am Mehlsteg", WeaponFamily.Sword,112,3,3000),
            new Source(RegionalRelic.M_Jochbeil, "Jochbeil", RegionalRelicSlot.Weapon, 5,-4500,5300, Unlock.Beds, "Alter Speicher · zerbrochenes Tragejoch", WeaponFamily.Axe,189,5,3800,true),
            new Source(RegionalRelic.M_Wehrschild, "Wehrschild", RegionalRelicSlot.Shield, 4,4800,7400, Unlock.Shelter, "Alte Mühle · vor der Wehrmauer", WeaponFamily.Shield,765,3,3100),
            new Source(RegionalRelic.M_Glockenring, "Glockenring", RegionalRelicSlot.Shield, 4,-7500,12000, Unlock.Shelter, "Alte Mühle · alter Glockenplatz", WeaponFamily.Shield,630,3,3100),
            new Source(RegionalRelic.G_Ziegelschild, "Ziegelschild", RegionalRelicSlot.Shield, 3,9800,-1000, Unlock.Trail, "Aschestraße · Ziegelvorrat neben der Hut", WeaponFamily.Shield,810,3,3100),
            new Source(RegionalRelic.M_Handglocke, "Handglocke vom Mühlwehr", RegionalRelicSlot.Utility, 4,1500,13400, Unlock.Shelter, "Alte Mühle · Meldeplatz am Nordweg"),
            new Source(RegionalRelic.G_Schuerhaken, "Schürhaken", RegionalRelicSlot.Weapon, 3,-8100,-2800, Unlock.Trail, "Aschestraße · seitliche Ofenklappe", WeaponFamily.Axe,126,3,2950),
            // Orsel's own one-handed staff, handed over with her lesson (owner 2026-11-23): one hand stays free for a
            // shield, Wundschluss works at 80 %. It borrows the wanderer's staff model until it has its own.
            new Source(RegionalRelic.M_Heilerstab, "Orsels Heilerstab", RegionalRelicSlot.Weapon, 4, HealingRules.OrselMillXmm, HealingRules.OrselMillZmm,
                Unlock.Shelter, "Alte Mühle · von Orsel nach ihrer Unterweisung", WeaponFamily.Staff, 150, 7, 8000, false,
                granted: true, visualKey: "Weapons/staff_newbie.1"),
            // Owner decision 2026-11-25: the Brennerhof's two weapons are fixed finds. Its yard frame is KilnLayout's.
            new Source(RegionalRelic.B_Brennerspeer, "Brennerspeer", RegionalRelicSlot.Weapon, 1, KilnYard.SpearXmm, KilnYard.SpearZmm,
                Unlock.Kiln, "Brennerhof · an der Waffenbank", WeaponFamily.Spear, 140, 4, 4600, true, WeaponFamily.Staff),
            new Source(RegionalRelic.B_Scherbenhellebarde, "Scherbenhellebarde", RegionalRelicSlot.Weapon, 1, KilnYard.HalberdXmm, KilnYard.HalberdZmm,
                Unlock.Kiln, "Brennerhof · im Scherbenschlupf", WeaponFamily.Spear, 175, 5, 4800, true, WeaponFamily.Axe),
            // Owner decision 2026-11-26: the Glockenwacht's armour and the Aschbeter's two claw staffs are rare loot, fitted to the hero.
            new Source(RegionalRelic.A_Visierhelm, "Visierhelm der Glockenwacht", RegionalRelicSlot.Head, 0, 0, 0, Unlock.Loot, "Seltene Beute"),
            new Source(RegionalRelic.A_Kuerass, "Kürass der Glockenwacht", RegionalRelicSlot.Body, 0, 0, 0, Unlock.Loot, "Seltene Beute"),
            new Source(RegionalRelic.A_Schulterplatten, "Schulterplatten der Glockenwacht", RegionalRelicSlot.Shoulders, 0, 0, 0, Unlock.Loot, "Seltene Beute"),
            new Source(RegionalRelic.A_Gliedpanzer, "Gliedpanzer der Glockenwacht", RegionalRelicSlot.Limbs, 0, 0, 0, Unlock.Loot, "Seltene Beute"),
            new Source(RegionalRelic.A_KlauenstabEinhand, "Klauenstab", RegionalRelicSlot.Weapon, 0, 0, 0, Unlock.Loot, "Seltene Beute",
                WeaponFamily.Mace, 150, 4, 2600, false),
            new Source(RegionalRelic.A_KlauenstabZweihand, "Langer Klauenstab", RegionalRelicSlot.Weapon, 0, 0, 0, Unlock.Loot, "Seltene Beute",
                WeaponFamily.Spear, 185, 5, 3300, true),
            new Source(RegionalRelic.N_Schaller, "Schaller der Nachtvesper", RegionalRelicSlot.Head, 0, 0, 0, Unlock.Loot, NachtvesperLocation),
            new Source(RegionalRelic.N_Kuerass, "Kürass der Nachtvesper", RegionalRelicSlot.Body, 0, 0, 0, Unlock.Loot, NachtvesperLocation),
            new Source(RegionalRelic.N_Schulterstuecke, "Schulterstücke der Nachtvesper", RegionalRelicSlot.Shoulders, 0, 0, 0, Unlock.Loot, NachtvesperLocation),
            new Source(RegionalRelic.N_Armschienen, "Armschienen der Nachtvesper", RegionalRelicSlot.Arms, 0, 0, 0, Unlock.Loot, NachtvesperLocation),
            new Source(RegionalRelic.N_Handschuhe, "Panzerhandschuhe der Nachtvesper", RegionalRelicSlot.Hands, 0, 0, 0, Unlock.Loot, NachtvesperLocation),
            new Source(RegionalRelic.N_Guertung, "Gürtung der Nachtvesper", RegionalRelicSlot.Waist, 0, 0, 0, Unlock.Loot, NachtvesperLocation),
            new Source(RegionalRelic.N_Beinzeug, "Beinzeug der Nachtvesper", RegionalRelicSlot.Legs, 0, 0, 0, Unlock.Loot, NachtvesperLocation),
            new Source(RegionalRelic.N_Panzerschuhe, "Panzerschuhe der Nachtvesper", RegionalRelicSlot.Feet, 0, 0, 0, Unlock.Loot, NachtvesperLocation)
        };
        private const string NachtvesperLocation = "Seltene Beute · oft bei Rittern";

        public static readonly IReadOnlyList<RegionalRelic> All = Array.AsReadOnly(Array.ConvertAll(
            Array.FindAll(Sources, source => source != null), source => source.Id));
        // Owned and worn pieces are bit masks in the save. Since the Aschenwacht gear (2026-11-26) there are more than 31 pieces:
        // the masks are 64-bit; older saves wrote smaller numbers, which read the same.
        internal static long AllMask => (1L << (Sources.Length - 1)) - 1;
        internal static bool Valid(RegionalRelic id) => (int)id > 0 && (int)id < Sources.Length;
        internal static long Bit(RegionalRelic id) => 1L << ((int)id - 1);
        internal static Source Get(RegionalRelic id) => Valid(id) ? Sources[(int)id] : throw new ArgumentOutOfRangeException(nameof(id));
        public static string Name(RegionalRelic id) => Get(id).Name;
        public static string Location(RegionalRelic id) => Get(id).Location;
        public static RegionalRelicSlot Slot(RegionalRelic id) => Get(id).Slot;
        public static WeaponDefinition HandDefinition(RegionalRelic id) => Get(id).Hand;
        /// <summary>A piece handed over by a person, never found in the world.</summary>
        public static bool GrantedOnly(RegionalRelic id) => Get(id).Granted;
    }

    public sealed partial class WeaponEquipment
    {
        private long _ownedRelics, _wornRelics;
        public long RegionalWearMask => _wornRelics;
        public bool OwnsRegionalRelic(RegionalRelic id) =>
            RegionalRelics.Valid(id) && (_ownedRelics & RegionalRelics.Bit(id)) != 0;
        public bool IsRegionalRelicEquipped(RegionalRelic id) =>
            RegionalRelics.Valid(id) && (_wornRelics & RegionalRelics.Bit(id)) != 0;
        public RegionalRelic WornRegionalRelic(RegionalRelicSlot slot)
        {
            foreach (var id in RegionalRelics.All)
                if (IsRegionalRelicEquipped(id) && RegionalRelics.Slot(id) == slot) return id;
            return RegionalRelic.None;
        }

        internal bool ClaimRegionalRelic(CharacterProgression p, RegionalRelic id)
        {
            if (!RegionalRelics.Valid(id) || OwnsRegionalRelic(id)) return false;
            Enable(p);
            _ownedRelics |= RegionalRelics.Bit(id);
            return true;
        }
        private void ClearRegionalSlot(RegionalRelicSlot slot)
        {
            var previous = WornRegionalRelic(slot);
            if (previous == RegionalRelic.None) return;
            _wornRelics &= ~RegionalRelics.Bit(previous);
            if (slot == RegionalRelicSlot.Weapon) Weapon = null;
            if (slot == RegionalRelicSlot.Shield) Shield = null;
        }

        internal bool SetRegionalRelicEquipped(RegionalRelic id, bool equip)
        {
            if (!OwnsRegionalRelic(id) || equip == IsRegionalRelicEquipped(id)) return false;
            var source = RegionalRelics.Get(id);
            if (equip)
            {
                if ((source.Slot == RegionalRelicSlot.Weapon && source.Hand.TwoHanded &&
                        (Shield != null || WornRegionalRelic(RegionalRelicSlot.Utility) != RegionalRelic.None)) ||
                    ((source.Slot == RegionalRelicSlot.Shield || source.Slot == RegionalRelicSlot.Utility) &&
                        Weapon != null && Weapon.Definition.TwoHanded)) return false;
                if (source.Slot == RegionalRelicSlot.Body || source.Slot == RegionalRelicSlot.Shoulders) FieldPauldronEquipped = false;
                if (source.Slot == RegionalRelicSlot.Limbs) { ClearRegionalSlot(RegionalRelicSlot.Arms); ClearRegionalSlot(RegionalRelicSlot.Legs); }
                if (source.Slot == RegionalRelicSlot.Arms || source.Slot == RegionalRelicSlot.Legs) ClearRegionalSlot(RegionalRelicSlot.Limbs);
                if (source.Slot == RegionalRelicSlot.Shield) ClearRegionalSlot(RegionalRelicSlot.Utility);
                if (source.Slot == RegionalRelicSlot.Utility)
                {
                    ClearRegionalSlot(RegionalRelicSlot.Shield);
                    Shield = null;
                }
            }
            ClearRegionalSlot(source.Slot);
            if (equip)
            {
                _wornRelics |= RegionalRelics.Bit(id);
                if (source.Slot == RegionalRelicSlot.Weapon) Weapon = new WeaponItem(source.Hand);
                if (source.Slot == RegionalRelicSlot.Shield) Shield = new WeaponItem(source.Hand);
            }
            return true;
        }

        private bool RestoreRegionalSlots(CharacterProgression p, string owned, string worn)
        {
            if (!long.TryParse(owned, NumberStyles.None, CultureInfo.InvariantCulture, out long o) ||
                !long.TryParse(worn, NumberStyles.None, CultureInfo.InvariantCulture, out long w) ||
                o.ToString(CultureInfo.InvariantCulture) != owned || w.ToString(CultureInfo.InvariantCulture) != worn ||
                o == 0 || (o & ~RegionalRelics.AllMask) != 0 || (w & ~o) != 0) return false;
            _ownedRelics = o;
            int occupied = 0;
            foreach (var id in RegionalRelics.All)
            {
                if (!OwnsRegionalRelic(id)) continue;
                var source = RegionalRelics.Get(id);
                if (!source.HistoricalSource(p)) return false;
                if ((w & RegionalRelics.Bit(id)) == 0) continue;
                var slot = source.Slot;
                int slotBit = 1 << (int)slot;
                if ((occupied & slotBit) != 0 || ((slot == RegionalRelicSlot.Body || slot == RegionalRelicSlot.Shoulders) && FieldPauldronEquipped) ||
                    (slot == RegionalRelicSlot.Weapon && Weapon != null) ||
                    ((slot == RegionalRelicSlot.Shield || slot == RegionalRelicSlot.Utility) && Shield != null))
                    return false;
                occupied |= slotBit;
                if (!SetRegionalRelicEquipped(id, true)) return false;
            }
            return _wornRelics == w;
        }
    }

    public sealed partial class ArenaSimulation
    {
        public bool TryGetRegionalRelicSource(RegionalRelic id, out long x, out long z)
        {
            x = z = 0;
            if (GameOver || InJourney || !RegionalRelics.Valid(id) || Progression.Equipment.OwnsRegionalRelic(id)) return false;
            var source = RegionalRelics.Get(id);
            if (source.Granted || source.Act != Progression.EntryAct || !source.HistoricalSource(Progression)) return false;
            if ((source.Required == RegionalRelics.Unlock.Kiln) != InKilnYard) return false; // the yard's finds lie only there
            if (source.Required == RegionalRelics.Unlock.Loot) return false; // found under loot, never lying in the world
            if (id == RegionalRelic.Grubenlicht)
            {
                var rattler = Progression.Rattler;
                if (!rattler.Active || !rattler.Creature.IsDead) return false;
                x = rattler.Creature.Xmm; z = rattler.Creature.Zmm;
            }
            else
            {
                if (source.Act == 6 && InDungeon && Progression.Dungeon.Depth != 2) return false;
                x = source.X; z = source.Z;
            }
            return CanStand(x,z);
        }

        private bool NearestRegionalRelic(bool withinReach, out RegionalRelic id, out long x, out long z)
        {
            id = RegionalRelic.None; x = z = 0;
            long best = withinReach ? FixedMath.Square(NpcInteractionRadiusMm) : long.MaxValue;
            foreach (var candidate in RegionalRelics.All)
            {
                if (!TryGetRegionalRelicSource(candidate, out long cx, out long cz)) continue;
                long distance = DistanceSquared(PlayerXmm, PlayerZmm, cx, cz);
                if (distance > best || (id != RegionalRelic.None && distance == best) ||
                    (withinReach && !CombatSight(PlayerXmm, PlayerZmm, cx, cz))) continue;
                id = candidate; x = cx; z = cz; best = distance;
            }
            return id != RegionalRelic.None;
        }
        public bool TryGetRegionalRelicSource(out RegionalRelic id, out long x, out long z) =>
            NearestRegionalRelic(false, out id, out x, out z);
        public bool TryGetClaimableRegionalRelic(out RegionalRelic id) => NearestRegionalRelic(true, out id, out _, out _);
        public bool CanClaimRegionalRelic => TryGetClaimableRegionalRelic(out _);
        private SocialResult ClaimRegionalRelic()
        {
            if (!NearestRegionalRelic(true, out var id, out _, out _) || !Progression.Equipment.ClaimRegionalRelic(Progression,id))
                return new SocialResult { Text = "Hier ist kein Fundstück in Reichweite." };
            return new SocialResult { Accepted = true, Text = RegionalRelics.Name(id) + " erhalten." };
        }
        public bool TrySetRegionalRelicEquipped(RegionalRelic id, bool equipped)
        {
            if (GameOver || !Progression.Equipment.SetRegionalRelicEquipped(id, equipped)) return false;
            KeepWithinPools();
            return true;
        }
    }
}
