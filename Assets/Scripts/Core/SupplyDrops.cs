using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace AreArpg.Core
{
    /// <summary>
    /// The supply bag of one kill under loot revision 2 (owner decisions of 2026-11-17 and 2026-11-23): some coins and
    /// sometimes a ration. Immutable; like a weapon receipt it can be regenerated from the identity of its kill.
    /// </summary>
    public sealed class SupplyBag
    {
        private readonly string _context;
        private readonly int _flags;
        public int Coins { get; }
        public int Rations { get; }
        public BigInteger KillOrdinal { get; }

        internal SupplyBag(string context, int flags, int coins, int rations, BigInteger killOrdinal)
        { _context = context; _flags = flags; Coins = coins; Rations = rations; KillOrdinal = killOrdinal; }

        /// <summary>The kill identity, the flags and the content, without culture-sensitive formatting.</summary>
        internal string Token()
        {
            return _context + "," + _flags.ToString(CultureInfo.InvariantCulture) + "," + Coins.ToString(CultureInfo.InvariantCulture) + "," +
                Rations.ToString(CultureInfo.InvariantCulture);
        }

        public ulong StableHash { get { return LootCatalog.HashText("supply;" + Token()); } }

        /// <summary>Regenerate the bag of the kill and reject anything else: a fuller bag, a bag no kill dropped.</summary>
        internal static bool TryRead(string token, out SupplyBag bag)
        {
            bag = null;
            if (string.IsNullOrEmpty(token)) return false;
            string[] p = token.Split(',');
            if (p.Length != 10 || p[0] != "2" ||
                !ulong.TryParse(p[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var seed) ||
                !BigInteger.TryParse(p[2], NumberStyles.None, CultureInfo.InvariantCulture, out var depth) || depth < 1 ||
                !ulong.TryParse(p[3], NumberStyles.None, CultureInfo.InvariantCulture, out var tick) ||
                !int.TryParse(p[4], NumberStyles.None, CultureInfo.InvariantCulture, out var enemyId) || enemyId <= 0 ||
                !int.TryParse(p[5], NumberStyles.None, CultureInfo.InvariantCulture, out var kind) || kind < 0 || kind > 3 ||
                !BigInteger.TryParse(p[6], NumberStyles.None, CultureInfo.InvariantCulture, out var ordinal) || ordinal < 1 ||
                !int.TryParse(p[7], NumberStyles.None, CultureInfo.InvariantCulture, out var flags) ||
                !LootCatalog.FlagsFit(flags, kind, enemyId, depth)) return false;
            var generated = LootCatalog.Version2.Supply(seed, depth, tick, enemyId, kind, ordinal, flags);
            if (generated == null || generated.Token() != token) return false;
            bag = generated;
            return true;
        }
    }

    /// <summary>A supply bag lying in the world.</summary>
    public sealed class SupplyDrop
    {
        public SupplyBag Bag { get; internal set; }
        public int Id;
        public long Xmm;
        public long Zmm;
    }

    /// <summary>
    /// The supply bags on the ground of the area the character is in. They live with the character, not with the
    /// running world: a save keeps them, a restart of the same area finds them again, and another area starts empty.
    /// </summary>
    public sealed class SupplyDrops
    {
        private const int MaximumBags = 512;
        private const long PositionLimit = 200000;
        private readonly List<SupplyDrop> _ground = new List<SupplyDrop>();
        public IReadOnlyList<SupplyDrop> Ground { get { return _ground; } }
        /// <summary>The area and layout the bags lie in; empty until a world was entered.</summary>
        public string Area { get; private set; } = "";
        public bool HasProgress { get { return _ground.Count != 0; } }

        /// <summary>A world starts: bags of another area or another layout are gone.</summary>
        internal void EnterArea(string area)
        {
            if (!ValidArea(area)) throw new ArgumentException("Invalid supply area", nameof(area));
            if (Area == area) return;
            _ground.Clear();
            Area = area;
        }

        internal void Add(SupplyBag bag, long x, long z)
        {
            if (_ground.Count >= MaximumBags) return; // far beyond what one area can hold; the bag is not dropped
            int id = _ground.Count == 0 ? 1 : _ground[_ground.Count - 1].Id + 1;
            _ground.Add(new SupplyDrop { Bag = bag, Id = id, Xmm = x, Zmm = z });
        }

        internal SupplyBag TakeAt(int index)
        {
            SupplyBag bag = _ground[index].Bag;
            _ground.RemoveAt(index);
            return bag;
        }

        private static bool ValidArea(string area)
        {
            if (string.IsNullOrEmpty(area) || area.Length > 64) return false;
            foreach (char c in area) if (!(c >= '0' && c <= '9') && !(c >= 'A' && c <= 'Z') && !(c >= 'a' && c <= 'z') && c != '.') return false;
            return true;
        }

        /// <summary>Version, area and every bag with its place, in the order the bags fell.</summary>
        internal string Token()
        {
            var b = new StringBuilder("1;").Append(Area).Append(';');
            for (int i = 0; i < _ground.Count; i++)
            {
                SupplyDrop d = _ground[i];
                if (i != 0) b.Append('/');
                b.Append(d.Id.ToString(CultureInfo.InvariantCulture)).Append(',').Append(d.Xmm.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(d.Zmm.ToString(CultureInfo.InvariantCulture)).Append(',').Append(d.Bag.Token());
            }
            return b.ToString();
        }

        public ulong StableHash { get { return LootCatalog.HashText("supplies;" + Token()); } }

        /// <summary>
        /// Read the section of a save. Every bag must be the bag its kill really dropped, lie within the world, belong to
        /// a kill the character made, and appear once; a section without bags is never written.
        /// </summary>
        internal static bool TryRead(string token, BigInteger kills, out SupplyDrops state)
        {
            state = null;
            if (string.IsNullOrEmpty(token)) return false;
            string[] f = token.Split(';');
            if (f.Length != 3 || f[0] != "1" || !ValidArea(f[1]) || f[2].Length == 0) return false;
            var s = new SupplyDrops { Area = f[1] };
            var ordinals = new HashSet<BigInteger>();
            int previousId = 0;
            foreach (string text in f[2].Split('/'))
            {
                string[] v = text.Split(',');
                if (v.Length != 13 ||
                    !int.TryParse(v[0], NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= previousId ||
                    !long.TryParse(v[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var x) || x < -PositionLimit || x > PositionLimit ||
                    !long.TryParse(v[2], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var z) || z < -PositionLimit || z > PositionLimit ||
                    !SupplyBag.TryRead(string.Join(",", v, 3, 10), out var bag) || bag.KillOrdinal > kills || !ordinals.Add(bag.KillOrdinal) ||
                    s._ground.Count >= MaximumBags) return false;
                previousId = id;
                s._ground.Add(new SupplyDrop { Bag = bag, Id = id, Xmm = x, Zmm = z });
            }
            if (s.Token() != token) return false; // canonical numbers only
            state = s;
            return true;
        }
    }

    public sealed partial class CharacterProgression
    {
        private SupplyDrops _supplies = new SupplyDrops();
        public SupplyDrops Supplies { get { return _supplies; } }
        private const string SuppliesSectionName = "supplies";

        private void SyncSuppliesSection()
        {
            if (_supplies.HasProgress) SetSection(SuppliesSectionName, _supplies.Token());
            else _sections.Remove(SuppliesSectionName);
        }

        private bool RestoreSuppliesSection()
        {
            var state = new SupplyDrops();
            if (_sections.TryGetValue(SuppliesSectionName, out string token) && !SupplyDrops.TryRead(token, LootOrdinal, out state)) return false;
            _supplies = state;
            return true;
        }
    }

    public sealed partial class ArenaSimulation
    {
        /// <summary>The supply bags lying in this area (loot revision 2).</summary>
        public IReadOnlyList<SupplyDrop> GroundSupplies { get { return Progression.Supplies.Ground; } }

        /// <summary>Weapons or bags still lie on the ground: the ways out of the area stay closed until they are taken.</summary>
        public bool LootOnGround { get { return GroundLoot.Count != 0 || Progression.Supplies.Ground.Count != 0; } }

        /// <summary>The bag the last <see cref="TryCollectLoot"/> took, or null; presentation only.</summary>
        public SupplyBag LastSupply { get; private set; }

        /// <summary>The weapon receipt the last <see cref="TryCollectLoot"/> took, or null; presentation only.</summary>
        public LootItem LastLoot { get; private set; }

        /// <summary>The revision of the catalog this world rolls loot with; 0 for a world without a catalog.</summary>
        public int LootRevision { get { return _lootCatalog == null ? 0 : _lootCatalog.Revision; } }

        private bool LootRevisionTwo { get { return LootRevision >= 2; } }

        /// <summary>
        /// The area with the identity of its layout. Generated places carry their seed, so bags never survive into a
        /// rebuilt cave, crypt or wood where their place might lie inside a wall.
        /// </summary>
        private string SupplyArea
        {
            get
            {
                string place = InJourney ? (InJourneyCave ? "c" + Progression.Journey.Depth.ToString(CultureInfo.InvariantCulture) : "r") + "." + JourneySeed.ToString("X16", CultureInfo.InvariantCulture)
                    : InDungeon ? "d" + Progression.Dungeon.Depth.ToString(CultureInfo.InvariantCulture) + "." + Progression.Dungeon.Seed.ToString("X16", CultureInfo.InvariantCulture)
                    : InForest ? "w." + Progression.Woodland.Seed.ToString("X16", CultureInfo.InvariantCulture) : "a";
                return CurrentAreaId.ToString(CultureInfo.InvariantCulture) + "." + place;
            }
        }

        /// <summary>The kill's supply bag, if it has one, falls where the enemy fell.</summary>
        private void DropSupply(ulong seed, BigInteger depth, int enemyId, int kind, int flags, long x, long z)
        {
            SupplyBag bag = _lootCatalog.Supply(seed, depth, CurrentTick, enemyId, kind, Progression.LootOrdinal, flags);
            if (bag != null) Progression.Supplies.Add(bag, x, z);
        }

        /// <summary>Take the first bag within reach: its coins and rations reach the character exactly once.</summary>
        private bool TryCollectSupply()
        {
            IReadOnlyList<SupplyDrop> ground = Progression.Supplies.Ground;
            for (int i = 0; i < ground.Count; i++)
            {
                if (DistanceSquared(PlayerXmm, PlayerZmm, ground[i].Xmm, ground[i].Zmm) > FixedMath.Square(PickupRadiusMm)) continue;
                LastSupply = Progression.Supplies.TakeAt(i);
                Progression.Social.TakeSupplies(LastSupply.Coins, LastSupply.Rations);
                SaveWoodlandPlayer();
                return true;
            }
            return false;
        }

        private void MixSupplies(ref ulong h)
        {
            if (Progression.Supplies.HasProgress) { Mix(ref h, 0x535550504C494553UL); Mix(ref h, Progression.Supplies.StableHash); } // "SUPPLIES"
        }
    }
}
