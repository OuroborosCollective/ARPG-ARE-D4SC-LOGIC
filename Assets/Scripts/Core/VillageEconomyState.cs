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
        public int Thirst { get; internal set; }
        public int Fatigue { get; internal set; }
        public int Health { get; internal set; } = VillageEconomyRules.MaxHealth;
        /// <summary>Where the resident is, or the place just left while walking.</summary>
        public VillageSite Site { get; internal set; }
        public VillageSite Destination { get; internal set; }
        /// <summary>Above zero: on the way to the destination.</summary>
        public int TravelTicks { get; internal set; }
        public VillageWork Work { get; internal set; }
        /// <summary>Work accepted in the village for a particular outlying destination.</summary>
        public VillageWork Assignment { get; internal set; }
        internal int SiteSample;
        internal ulong SiteSampleTick;
        // V1 could put a newly broken equipped axe into six occupied cargo slots. The axe stays
        // in its original tool loop on that historical return journey until the actual handover.
        internal bool ReturningBrokenTool;
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
                return ReturningBrokenTool ? load - 1 : load;
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
        // Revision 1 was shipped with these array dimensions and IDs. Never derive its wire shape from today's catalog.
        private const int OriginalGoods = 13, OriginalSources = 6, OriginalWorks = 16;
        private const int OriginalAxeOverload = 7;
        private bool _originalRules;
        public bool Started { get; private set; }
        public ulong StartedTick { get; private set; }
        /// <summary>The last tick the economy was evaluated; a second call for the same tick changes nothing.</summary>
        public ulong LastTick { get; private set; }
        /// <summary>Latest forest conditions actually brought back to the village, including their observation tick.</summary>
        public int ForestReport { get; internal set; }
        public ulong ForestReportTick { get; internal set; }
        public int ForestReporterOrdinal { get; internal set; }
        /// <summary>Number of herbs last reported by a returning witness. Negative until the first report.</summary>
        public int ReportedMillHerbs { get; internal set; } = -1;
        public ulong MillReportTick { get; internal set; }
        public int MillReporterOrdinal { get; internal set; }
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

        /// <summary>Open only the newly declared stocks when an existing village next acts. No previous production,
        /// deliveries or memories are inferred. Reading or saving a dormant historical village keeps its old hash.</summary>
        private void UpgradeRules()
        {
            if (!_originalRules) return;
            for (int g = OriginalGoods; g < _commons.Length; g++)
            {
                _commons[g] = VillageEconomyRules.StartStock((VillageGood)g);
                _start[g] = _commons[g];
            }
            for (int i = OriginalSources; i < _sources.Length; i++)
                _sources[i] = VillageEconomyRules.StartSource((VillageSource)i);
            _originalRules = false;
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

        private static string Row(int[] values, int count)
        {
            var b = new StringBuilder();
            for (int i = 0; i < count; i++) { if (i > 0) b.Append(','); b.Append(N(values[i])); }
            return b.ToString();
        }

        private static string Row(long[] values, int count)
        {
            var b = new StringBuilder();
            for (int i = 0; i < count; i++) { if (i > 0) b.Append(','); b.Append(N(values[i])); }
            return b.ToString();
        }

        /// <summary>Canonical text of the whole state. Fields are separated by ';', lists by '/', numbers by ','.</summary>
        internal string Token()
        {
            int goods = _originalRules ? OriginalGoods : VillageEconomyRules.GoodCount;
            int sources = _originalRules ? OriginalSources : VillageEconomyRules.SourceCount;
            var b = new StringBuilder("1;").Append(N(_originalRules ? 1 : VillageEconomyRules.Revision)).Append(';').Append(N(StartedTick)).Append(';').Append(N(LastTick));
            b.Append(';').Append(Row(_commons, goods)).Append(';').Append(Row(_sources, sources)).Append(';').Append(N(_nursery)).Append(',').Append(N(_millHerbs)).Append(';');
            for (int i = 0; i < _saplings.Count; i++)
            { if (i > 0) b.Append('/'); b.Append(N(_saplings[i].Key)).Append(',').Append(N(_saplings[i].Value)); }
            if (_saplings.Count == 0) b.Append('x');
            b.Append(';').Append(Row(_start, goods)).Append(';').Append(Row(_made, goods)).Append(';').Append(Row(_used, goods)).Append(';').Append(Row(_received, goods)).Append(';').Append(Row(_handedOut, goods)).Append(';');
            for (int i = 0; i < _residents.Count; i++)
            {
                var r = _residents[i];
                if (i > 0) b.Append('/');
                b.Append(N(r.Ordinal)).Append(',').Append(N(r.Hunger)).Append(',').Append(N(r.Fatigue)).Append(',').Append(N(r.Health))
                    .Append(',').Append(N((int)r.Site)).Append(',').Append(N((int)r.Destination)).Append(',').Append(N(r.TravelTicks))
                    .Append(',').Append(N((int)r.Work)).Append(',').Append(N(r.WorkTicks)).Append(',').Append(N(r.AxeUses))
                    .Append(',').Append(N((int)r.Activity)).Append(',').Append(N(r.DeliveryDay + 1)).Append(',').Append(N(r.CareDay + 1))
                    .Append(',').Append(Row(r.Carry, goods)).Append(',').Append(Row(r.Reserved, goods));
                if (!_originalRules && (r.Thirst != 0 || r.Assignment != VillageWork.None || r.SiteSample != 0))
                    b.Append(',').Append(N(r.Thirst)).Append(',').Append(N((int)r.Assignment));
                if (!_originalRules && r.SiteSample != 0)
                    b.Append(',').Append(N(r.SiteSample)).Append(',').Append(N(r.SiteSampleTick));
                if (!_originalRules && r.ReturningBrokenTool) b.Append(",1");
            }
            if (_residents.Count == 0) b.Append('x');
            if (!_originalRules && ForestReport != 0)
                b.Append(';').Append(N(ForestReport)).Append(',').Append(N(ForestReportTick)).Append(',').Append(N(ForestReporterOrdinal));
            if (!_originalRules && ReportedMillHerbs >= 0)
                b.Append(";m,").Append(N(ReportedMillHerbs)).Append(',').Append(N(MillReportTick)).Append(',').Append(N(MillReporterOrdinal));
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

        private bool ReadObservationTick(string text, out ulong tick)
        {
            tick = 0;
            if (!Number(text, (long)LastTick, out long when) || when <= 0 || (ulong)when < StartedTick) return false;
            tick = (ulong)when;
            return true;
        }

        private bool ReadSiteSample(VillageSite site, string[] parts, int at, out int report, out ulong tick)
        {
            report = 0;
            tick = 0;
            if (!Number(parts[at], ushort.MaxValue, out long value) || value == 0 || !ReadObservationTick(parts[at + 1], out tick))
                return false;
            if (site == VillageSite.Forest ? !IsForestReport((int)value) : site != VillageSite.Mill || value > VillageEconomyRules.MillHerbCap + 1)
                return false;
            report = (int)value;
            return true;
        }

        private bool ReadVillageReports(string[] fields)
        {
            int next = 14;
            if (next < fields.Length && !fields[next].StartsWith("m,", StringComparison.Ordinal))
            {
                string[] report = fields[next++].Split(',');
                if (report.Length != 3 || !ReadSiteSample(VillageSite.Forest, report, 0, out int value, out ulong when) ||
                    !Number(report[2], int.MaxValue, out long ordinal) || ordinal < VillageServicesState.BasePopulation) return false;
                ForestReport = value;
                ForestReportTick = when;
                ForestReporterOrdinal = (int)ordinal;
            }
            if (next < fields.Length)
            {
                string[] report = fields[next++].Split(',');
                if (report.Length != 4 || report[0] != "m" || !Number(report[1], VillageEconomyRules.MillHerbCap, out long herbs) ||
                    !ReadObservationTick(report[2], out ulong when) || !Number(report[3], int.MaxValue, out long ordinal) ||
                    ordinal < VillageServicesState.BasePopulation) return false;
                ReportedMillHerbs = (int)herbs;
                MillReportTick = when;
                MillReporterOrdinal = (int)ordinal;
            }
            return next == fields.Length;
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
            if (f.Length < 14 || f.Length > 16 || f[0] != "1") return false;
            bool original = f[1] == "1";
            if (original ? f.Length != 14 : f[1] != N(VillageEconomyRules.Revision)) return false;
            int goodsCount = original ? OriginalGoods : VillageEconomyRules.GoodCount;
            int sourcesCount = original ? OriginalSources : VillageEconomyRules.SourceCount;
            int worksCount = original ? OriginalWorks : VillageEconomyRules.WorkCount;
            var s = new VillageEconomyState { Started = true, _originalRules = original };
            if (!Number(f[2], long.MaxValue / 4, out long started) || !Number(f[3], long.MaxValue / 4, out long last) || last < started) return false;
            s.StartedTick = (ulong)started; s.LastTick = (ulong)last;
            if (!s.ReadVillageReports(f)) return false;
            var goods = new long[goodsCount];
            if (!Numbers(f[4], goods.Length, StockLimit, goods)) return false;
            for (int g = 0; g < goods.Length; g++) s._commons[g] = (int)goods[g];
            if (s._commons[(int)VillageGood.Seedling] != 0) return false;       // seedlings stand in the nursery
            var sources = new long[sourcesCount];
            if (!Numbers(f[5], sources.Length, 100000, sources)) return false;
            for (int i = 0; i < sources.Length; i++) s._sources[i] = (int)sources[i];
            if (s._sources[(int)VillageSource.Wildlife] > VillageEconomyRules.WildlifeCap || s._sources[(int)VillageSource.Berries] > VillageEconomyRules.BerryCap ||
                s._sources[(int)VillageSource.Well] > VillageEconomyRules.WellCap || s._sources[(int)VillageSource.Herbs] > VillageEconomyRules.HerbCap ||
                !original && !VillageEconomyRules.ValidProductionSources(s._sources)) return false;
            var beds = new long[2];
            if (!Numbers(f[6], 2, StockLimit, beds) || beds[1] > VillageEconomyRules.MillHerbCap) return false;
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
            if (!Numbers(f[8], goods.Length, LedgerLimit, s._start) || !Numbers(f[9], goods.Length, LedgerLimit, s._made) || !Numbers(f[10], goods.Length, LedgerLimit, s._used) ||
                !Numbers(f[11], goods.Length, LedgerLimit, s._received) || !Numbers(f[12], goods.Length, LedgerLimit, s._handedOut)) return false;
            for (int g = 0; g < goods.Length; g++) if (s._start[g] != VillageEconomyRules.StartStock((VillageGood)g)) return false;
            if (f[13] != "x")
            {
                int expected = VillageServicesState.BasePopulation;
                foreach (string entry in f[13].Split('/'))
                {
                    const int Head = 13;
                    int basicLength = Head + 2 * goodsCount;
                    string[] fields = entry.Split(',');
                    int extras = fields.Length - basicLength;
                    bool returningTool = !original && extras > 0 && (extras & 1) != 0 && fields[fields.Length - 1] == "1";
                    if (returningTool) extras--;
                    bool sample = !original && extras == 4;
                    bool extra = sample || !original && extras == 2;
                    if (!extra && extras != 0) return false;
                    var n = new long[extra ? basicLength + 2 : basicLength];
                    for (int i = 0; i < n.Length; i++) if (!Number(fields[i], StockLimit, out n[i])) return false;
                    var r = new VillageResidentState
                    {
                        Ordinal = (int)n[0], Hunger = (int)n[1], Fatigue = (int)n[2], Health = (int)n[3], Site = (VillageSite)n[4], Destination = (VillageSite)n[5],
                        TravelTicks = (int)n[6], Work = (VillageWork)n[7], WorkTicks = (int)n[8], AxeUses = (int)n[9], Activity = (VillageActivity)n[10], DeliveryDay = n[11] - 1, CareDay = n[12] - 1
                    };
                    if (extra)
                    {
                        if (n[basicLength] > VillageEconomyRules.MaxNeed || n[basicLength + 1] >= worksCount) return false;
                        r.Thirst = (int)n[basicLength];
                        r.Assignment = (VillageWork)n[basicLength + 1];
                        if (!sample && r.Assignment == VillageWork.None && r.Thirst == 0 ||
                            r.Assignment != VillageWork.None && !IsExternalAssignment(r.Assignment)) return false;
                        if (sample && (!r.Walking || r.Destination != VillageSite.Village ||
                            !s.ReadSiteSample(r.Site, fields, basicLength + 2, out r.SiteSample, out r.SiteSampleTick))) return false;
                    }
                    if (r.Ordinal != expected++ || r.Hunger > VillageEconomyRules.MaxNeed || r.Fatigue > VillageEconomyRules.MaxNeed ||
                        r.Health < 1 || r.Health > VillageEconomyRules.MaxHealth ||
                        n[4] > 2 || n[5] > 2 || r.TravelTicks > VillageEconomyRules.TravelTicks(r.Site, r.Destination) || r.TravelTicks % VillageEconomyRules.Stride != 0 ||
                        (r.TravelTicks > 0) == (n[4] == n[5]) || r.TravelTicks > 0 && n[7] != 0 ||
                        // Forest and mill are reached through the village: no walk leads from one to the other.
                        r.TravelTicks > 0 && r.Site != VillageSite.Village && r.Destination != VillageSite.Village ||
                        n[7] >= worksCount || n[10] > (long)(original ? VillageActivity.Recovering : VillageActivity.Drinking) || r.AxeUses > VillageEconomyRules.AxeUses) return false;
                    var rule = VillageEconomyRules.Rule(r.Work);
                    // A work step is in hand exactly while it has time left; walking home is a walk, not a step.
                    if (r.WorkTicks > rule.Ticks || r.WorkTicks % VillageEconomyRules.Stride != 0 || r.Work == VillageWork.GoHome || r.Work == VillageWork.SeekCare ||
                        (r.Work == VillageWork.None) != (r.WorkTicks == 0)) return false;
                    for (int g = 0; g < goodsCount; g++)
                    {
                        r.Carry[g] = (int)n[Head + g];
                        r.Reserved[g] = (int)n[Head + goodsCount + g];
                        if (r.Carry[g] > VillageEconomyRules.CarryCapacity) return false;
                    }
                    if (returningTool || original && r.Load > VillageEconomyRules.CarryCapacity)
                    {
                        if (r.Site != VillageSite.Forest || r.Work != VillageWork.None || r.AxeUses != 0 || r.Carry[(int)VillageGood.Axe] != 0 ||
                            r.Load != OriginalAxeOverload || r.Carry[(int)VillageGood.Wood] != 6 || r.Carry[(int)VillageGood.BrokenAxe] != 1 ||
                            s.Used(VillageGood.Axe) < 1 || s.Made(VillageGood.BrokenAxe) < 1 || s.Made(VillageGood.Wood) < 36)
                            return false;
                        r.ReturningBrokenTool = true;
                    }
                    if (r.Load > VillageEconomyRules.CarryCapacity || r.Carry[(int)VillageGood.Axe] > 1 || r.Carry[(int)VillageGood.Bow] > 1 ||
                        (r.Carry[(int)VillageGood.Axe] == 1) != (r.AxeUses > 0)) return false;
                    if (r.Work != VillageWork.None && (r.Site != rule.Site || rule.Tool >= 0 && r.Carry[rule.Tool] != 1)) return false;
                    // What is held back must be exactly the inputs of the work in hand, or nothing before it started.
                    // Rations on their way to Mara are a load of one up to a full delivery.
                    var held = new int[VillageEconomyRules.GoodCount];
                    if (r.WorkTicks > 0) foreach (var input in rule.Inputs) held[(int)input.Good] += input.Count;
                    for (int g = 0; g < held.Length; g++)
                    {
                        bool load = r.Work == VillageWork.DeliverRations && g == (int)VillageGood.Ration;
                        if (load ? r.Reserved[g] < 1 || r.Reserved[g] > VillageEconomyRules.DeliveryLoad : r.Reserved[g] != held[g]) return false;
                    }
                    s._residents.Add(r);
                }
            }
            if (s.ForestReport != 0 && s.ForestReporterOrdinal - VillageServicesState.BasePopulation >= s._residents.Count) return false;
            if (s.ReportedMillHerbs >= 0 && s.MillReporterOrdinal - VillageServicesState.BasePopulation >= s._residents.Count) return false;
            if (s.Audit().Count != 0 || !original && !s.ProductionAccountsAgree()) return false;
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
        private bool RestoreEconomySection()
        {
            if (!_sections.TryGetValue(EconomySectionName, out var token)) return true;
            if (!VillageEconomyState.TryRead(token, out var state) || Social.Forge == null ||
                state.LastTick > Social.Forge.ProcessedTicks || state.Residents.Count > Village.Population - VillageServicesState.BasePopulation ||
                state.ReportedMillHerbs >= 0 && !Village.OrselTeachesUnlocked)
                return false;
            _economy = state;
            return true;
        }
    }
}
