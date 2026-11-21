using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AreArpg.Core
{
    /// <summary>One anonymous resident of the village: needs, whereabouts, the work in hand and what is carried.</summary>
    public sealed class VillageResidentState
    {
        /// <summary>Position in the village's population count; the stable identity is "resident.village.N".</summary>
        public int Ordinal { get; internal set; }
        public int Hunger { get; internal set; }
        public int Fatigue { get; internal set; }
        public int Health { get; internal set; } = VillageEconomyRules.MaxHealth;
        /// <summary>Where the resident is, or the place just left while walking.</summary>
        public VillageSite Site { get; internal set; }
        public VillageSite Destination { get; internal set; }
        /// <summary>Above zero: on the way to the destination.</summary>
        public int TravelTicks { get; internal set; }
        public VillageWork Work { get; internal set; }
        public int WorkTicks { get; internal set; }
        /// <summary>Uses left of the axe in hand; zero without a working axe.</summary>
        public int AxeUses { get; internal set; }
        public VillageActivity Activity { get; internal set; }
        /// <summary>Game day of the last delivery that entered the personal memories; one memory a day is enough.</summary>
        public long DeliveryDay { get; internal set; } = -1;
        /// <summary>Game day Orsel last took this resident into her care; one memory per day of care.</summary>
        public long CareDay { get; internal set; } = -1;
        internal readonly int[] Carry = new int[VillageEconomyRules.GoodCount];
        /// <summary>Inputs taken from the commons for the work in hand. Returned if the work is given up.</summary>
        internal readonly int[] Reserved = new int[VillageEconomyRules.GoodCount];

        public int Carried(VillageGood good) { return Carry[(int)good]; }
        public int Held(VillageGood good) { return Reserved[(int)good]; }
        public bool Walking { get { return TravelTicks > 0; } }
        public bool CanWork { get { return Health >= VillageEconomyRules.WorkHealth; } }
        public string Identity { get { return "resident.village." + Ordinal.ToString(CultureInfo.InvariantCulture); } }

        /// <summary>Goods in hand without the tools.</summary>
        internal int Load
        {
            get
            {
                int load = 0;
                for (int g = 0; g < Carry.Length; g++)
                    if (g != (int)VillageGood.Axe && g != (int)VillageGood.Bow) load += Carry[g];
                return load;
            }
        }
    }

    /// <summary>
    /// Stocks, natural sources, residents and the ledger of the village economy. Owned by the character save as its
    /// own AC22 section "economy". Every unit of a good lies in exactly one place: the commons, the nursery at the
    /// forest edge, a resident's hands or a resident's work in progress. Mara's rations stay with the social ledger.
    /// </summary>
    public sealed partial class VillageEconomyState
    {
        public bool Started { get; private set; }
        public ulong StartedTick { get; private set; }
        /// <summary>The last tick the economy was evaluated; a second call for the same tick changes nothing.</summary>
        public ulong LastTick { get; private set; }
        private readonly int[] _commons = new int[VillageEconomyRules.GoodCount];
        private readonly int[] _sources = new int[VillageEconomyRules.SourceCount];
        private int _nursery;
        private int _millHerbs;
        private readonly List<KeyValuePair<ulong, int>> _saplings = new List<KeyValuePair<ulong, int>>();
        private readonly List<VillageResidentState> _residents = new List<VillageResidentState>();
        // Ledger per good: start + made + received - used - handed out = present.
        private readonly long[] _start = new long[VillageEconomyRules.GoodCount];
        private readonly long[] _made = new long[VillageEconomyRules.GoodCount];
        private readonly long[] _used = new long[VillageEconomyRules.GoodCount];
        private readonly long[] _received = new long[VillageEconomyRules.GoodCount];
        private readonly long[] _handedOut = new long[VillageEconomyRules.GoodCount];

        public int Commons(VillageGood good) { return _commons[(int)good]; }
        public int Source(VillageSource source) { return _sources[(int)source]; }
        /// <summary>Seedlings standing in the nursery at the forest edge.</summary>
        public int NurserySeedlings { get { return _nursery; } }
        /// <summary>Healing herbs on Orsel's shelf at the old mill.</summary>
        public int MillHerbs { get { return _millHerbs; } }
        public IReadOnlyList<VillageResidentState> Residents { get { return _residents; } }
        public int SaplingCount { get { int n = 0; foreach (var s in _saplings) n += s.Value; return n; } }
        /// <summary>The tick the next planted seedling becomes a tree; zero without saplings.</summary>
        public ulong NextSaplingTick { get { return _saplings.Count == 0 ? 0UL : _saplings[0].Key; } }
        public long Made(VillageGood good) { return _made[(int)good]; }
        public long Used(VillageGood good) { return _used[(int)good]; }
        public long Received(VillageGood good) { return _received[(int)good]; }
        public long HandedOut(VillageGood good) { return _handedOut[(int)good]; }
        public long StartStock(VillageGood good) { return _start[(int)good]; }

        /// <summary>Every unit of a good that exists right now, wherever it lies.</summary>
        public long Present(VillageGood good)
        {
            int g = (int)good;
            long total = _commons[g];
            if (good == VillageGood.Seedling) total += _nursery;
            if (good == VillageGood.Herb) total += _millHerbs;
            foreach (var r in _residents) total += r.Carry[g] + r.Reserved[g];
            return total;
        }

        /// <summary>
        /// The ledger equation for every good: start + made + received - used - handed out = present.
        /// Returns the goods that do not balance; an empty list is the proof that nothing was doubled or lost.
        /// </summary>
        public List<VillageGood> Audit()
        {
            var wrong = new List<VillageGood>();
            for (int g = 0; g < VillageEconomyRules.GoodCount; g++)
                if (_start[g] + _made[g] + _received[g] - _used[g] - _handedOut[g] != Present((VillageGood)g) ||
                    _commons[g] < 0 || _made[g] < 0 || _used[g] < 0 || _received[g] < 0 || _handedOut[g] < 0)
                    wrong.Add((VillageGood)g);
            return wrong;
        }

        /// <summary>The stated start: stocks, tools and a living forest. Called once, when the first resident moves in.</summary>
        internal void Begin(ulong tick)
        {
            if (Started) return;
            Started = true; StartedTick = tick; LastTick = tick;
            for (int g = 0; g < VillageEconomyRules.GoodCount; g++)
            {
                int amount = VillageEconomyRules.StartStock((VillageGood)g);
                _start[g] = amount;
                if (g == (int)VillageGood.Seedling) _nursery = amount; else _commons[g] = amount;
            }
            for (int s = 0; s < VillageEconomyRules.SourceCount; s++) _sources[s] = VillageEconomyRules.StartSource((VillageSource)s);
        }

        /// <summary>FNV-1a over the canonical text; mixed into the world hash once the economy has started.</summary>
        public ulong StableHash
        {
            get
            {
                ulong h = 14695981039346656037UL;
                unchecked { foreach (char c in Token()) { h ^= c; h *= 1099511628211UL; } }
                return h;
            }
        }

        private static string N(long value) { return value.ToString(CultureInfo.InvariantCulture); }
        private static string N(ulong value) { return value.ToString(CultureInfo.InvariantCulture); }

        private static string Row(int[] values)
        {
            var b = new StringBuilder();
            for (int i = 0; i < values.Length; i++) { if (i > 0) b.Append(','); b.Append(N(values[i])); }
            return b.ToString();
        }

        private static string Row(long[] values)
        {
            var b = new StringBuilder();
            for (int i = 0; i < values.Length; i++) { if (i > 0) b.Append(','); b.Append(N(values[i])); }
            return b.ToString();
        }

        /// <summary>Canonical text of the whole state. Fields are separated by ';', lists by '/', numbers by ','.</summary>
        internal string Token()
        {
            var b = new StringBuilder("1;").Append(N(VillageEconomyRules.Revision)).Append(';').Append(N(StartedTick)).Append(';').Append(N(LastTick));
            b.Append(';').Append(Row(_commons)).Append(';').Append(Row(_sources)).Append(';').Append(N(_nursery)).Append(',').Append(N(_millHerbs)).Append(';');
            for (int i = 0; i < _saplings.Count; i++)
            { if (i > 0) b.Append('/'); b.Append(N(_saplings[i].Key)).Append(',').Append(N(_saplings[i].Value)); }
            if (_saplings.Count == 0) b.Append('x');
            b.Append(';').Append(Row(_start)).Append(';').Append(Row(_made)).Append(';').Append(Row(_used)).Append(';').Append(Row(_received)).Append(';').Append(Row(_handedOut)).Append(';');
            for (int i = 0; i < _residents.Count; i++)
            {
                var r = _residents[i];
                if (i > 0) b.Append('/');
                b.Append(N(r.Ordinal)).Append(',').Append(N(r.Hunger)).Append(',').Append(N(r.Fatigue)).Append(',').Append(N(r.Health))
                    .Append(',').Append(N((int)r.Site)).Append(',').Append(N((int)r.Destination)).Append(',').Append(N(r.TravelTicks))
                    .Append(',').Append(N((int)r.Work)).Append(',').Append(N(r.WorkTicks)).Append(',').Append(N(r.AxeUses))
                    .Append(',').Append(N((int)r.Activity)).Append(',').Append(N(r.DeliveryDay + 1)).Append(',').Append(N(r.CareDay + 1))
                    .Append(',').Append(Row(r.Carry)).Append(',').Append(Row(r.Reserved));
            }
            if (_residents.Count == 0) b.Append('x');
            return b.ToString();
        }

        private static bool Number(string text, long maximum, out long value)
        {
            value = 0;
            if (string.IsNullOrEmpty(text) || text.Length > 18 || (text.Length > 1 && text[0] == '0')) return false;
            foreach (char c in text) if (c < '0' || c > '9') return false;
            return long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value <= maximum;
        }

        private static bool Numbers(string text, int count, long maximum, long[] into)
        {
            string[] parts = text.Split(',');
            if (parts.Length != count) return false;
            for (int i = 0; i < count; i++) if (!Number(parts[i], maximum, out into[i])) return false;
            return true;
        }

        /// <summary>
        /// Strict reader: canonical numbers only, every value inside its range, the work in hand consistent with the
        /// rule table, and the ledger must balance. A text that fails any check is rejected as a whole.
        /// </summary>
        internal static bool TryRead(string token, out VillageEconomyState state)
        {
            state = null;
            if (string.IsNullOrEmpty(token)) return false;
            string[] f = token.Split(';');
            const long Big = 1000000000000L;
            if (f.Length != 14 || f[0] != "1" || f[1] != VillageEconomyRules.Revision.ToString(CultureInfo.InvariantCulture)) return false;
            var s = new VillageEconomyState { Started = true };
            if (!Number(f[2], long.MaxValue / 4, out long started) || !Number(f[3], long.MaxValue / 4, out long last) || last < started) return false;
            s.StartedTick = (ulong)started; s.LastTick = (ulong)last;
            var goods = new long[VillageEconomyRules.GoodCount];
            if (!Numbers(f[4], goods.Length, int.MaxValue / 4, goods)) return false;
            for (int g = 0; g < goods.Length; g++) s._commons[g] = (int)goods[g];
            if (s._commons[(int)VillageGood.Seedling] != 0) return false;       // seedlings stand in the nursery
            var sources = new long[VillageEconomyRules.SourceCount];
            if (!Numbers(f[5], sources.Length, 100000, sources)) return false;
            for (int i = 0; i < sources.Length; i++) s._sources[i] = (int)sources[i];
            if (s._sources[(int)VillageSource.Wildlife] > VillageEconomyRules.WildlifeCap || s._sources[(int)VillageSource.Berries] > VillageEconomyRules.BerryCap ||
                s._sources[(int)VillageSource.Well] > VillageEconomyRules.WellCap || s._sources[(int)VillageSource.Herbs] > VillageEconomyRules.HerbCap) return false;
            var beds = new long[2];
            if (!Numbers(f[6], 2, int.MaxValue / 4, beds) || beds[1] > VillageEconomyRules.MillHerbCap) return false;
            s._nursery = (int)beds[0]; s._millHerbs = (int)beds[1];
            if (f[7] != "x")
            {
                ulong before = 0;
                foreach (string entry in f[7].Split('/'))
                {
                    string[] pair = entry.Split(',');
                    if (pair.Length != 2 || !Number(pair[0], long.MaxValue / 4, out long due) || !Number(pair[1], 100000, out long count) ||
                        count == 0 || (ulong)due <= before || (ulong)due <= s.LastTick || (ulong)due > s.LastTick + VillageEconomyRules.SaplingGrowthTicks) return false;
                    before = (ulong)due;
                    s._saplings.Add(new KeyValuePair<ulong, int>((ulong)due, (int)count));
                }
            }
            if (!Numbers(f[8], goods.Length, Big, s._start) || !Numbers(f[9], goods.Length, Big, s._made) || !Numbers(f[10], goods.Length, Big, s._used) ||
                !Numbers(f[11], goods.Length, Big, s._received) || !Numbers(f[12], goods.Length, Big, s._handedOut)) return false;
            for (int g = 0; g < goods.Length; g++) if (s._start[g] != VillageEconomyRules.StartStock((VillageGood)g)) return false;
            if (f[13] != "x")
            {
                int expected = VillageServicesState.BasePopulation;
                foreach (string entry in f[13].Split('/'))
                {
                    const int Head = 13;
                    var n = new long[Head + 2 * VillageEconomyRules.GoodCount];
                    if (!Numbers(entry, n.Length, int.MaxValue / 4, n)) return false;
                    var r = new VillageResidentState
                    {
                        Ordinal = (int)n[0], Hunger = (int)n[1], Fatigue = (int)n[2], Health = (int)n[3], Site = (VillageSite)n[4], Destination = (VillageSite)n[5],
                        TravelTicks = (int)n[6], Work = (VillageWork)n[7], WorkTicks = (int)n[8], AxeUses = (int)n[9], Activity = (VillageActivity)n[10], DeliveryDay = n[11] - 1, CareDay = n[12] - 1
                    };
                    if (r.Ordinal != expected++ || r.Hunger > VillageEconomyRules.MaxNeed || r.Fatigue > VillageEconomyRules.MaxNeed ||
                        r.Health < 1 || r.Health > VillageEconomyRules.MaxHealth ||
                        n[4] > 2 || n[5] > 2 || r.TravelTicks > VillageEconomyRules.TravelTicks(r.Site, r.Destination) || r.TravelTicks % VillageEconomyRules.Stride != 0 ||
                        (r.TravelTicks > 0) == (n[4] == n[5]) || r.TravelTicks > 0 && n[7] != 0 ||
                        // Forest and mill are reached through the village: no walk leads from one to the other.
                        r.TravelTicks > 0 && r.Site != VillageSite.Village && r.Destination != VillageSite.Village ||
                        n[7] >= VillageEconomyRules.WorkCount || n[10] > (long)VillageActivity.Recovering || r.AxeUses > VillageEconomyRules.AxeUses) return false;
                    var rule = VillageEconomyRules.Rule(r.Work);
                    // A work step is in hand exactly while it has time left; walking home is a walk, not a step.
                    if (r.WorkTicks > rule.Ticks || r.WorkTicks % VillageEconomyRules.Stride != 0 || r.Work == VillageWork.GoHome || r.Work == VillageWork.SeekCare ||
                        (r.Work == VillageWork.None) != (r.WorkTicks == 0)) return false;
                    for (int g = 0; g < VillageEconomyRules.GoodCount; g++)
                    {
                        r.Carry[g] = (int)n[Head + g];
                        r.Reserved[g] = (int)n[Head + VillageEconomyRules.GoodCount + g];
                    }
                    if (r.Load > VillageEconomyRules.CarryCapacity || r.Carry[(int)VillageGood.Axe] > 1 || r.Carry[(int)VillageGood.Bow] > 1 ||
                        (r.Carry[(int)VillageGood.Axe] == 1) != (r.AxeUses > 0)) return false;
                    // What is held back must be exactly the inputs of the work in hand, or nothing before it started.
                    var held = new int[VillageEconomyRules.GoodCount];
                    if (r.WorkTicks > 0) foreach (var input in rule.Inputs) held[(int)input.Good] += input.Count;
                    for (int g = 0; g < held.Length; g++) if (r.Reserved[g] != held[g]) return false;
                    s._residents.Add(r);
                }
            }
            if (s.Audit().Count != 0) return false;
            state = s;
            return true;
        }
    }

    public sealed partial class CharacterProgression
    {
        private VillageEconomyState _economy = new VillageEconomyState();
        /// <summary>The village economy: stocks, forest, residents at work. Empty until the first resident moves in.</summary>
        public VillageEconomyState Economy { get { return _economy; } }
        private const string EconomySectionName = "economy";

        private void SyncEconomySection()
        {
            if (_economy.Started) SetSection(EconomySectionName, _economy.Token());
            else _sections.Remove(EconomySectionName);
        }

        /// <summary>A save without this section is a village whose economy has not begun: nothing is made up for it.</summary>
        private void RestoreEconomySection()
        {
            string token;
            if (_sections.TryGetValue(EconomySectionName, out token) && VillageEconomyState.TryRead(token, out var state))
                _economy = state;
        }
    }
}
