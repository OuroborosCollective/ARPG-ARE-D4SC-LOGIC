using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AreArpg.Core
{
    /// <summary>
    /// Region network of the borderland (Weg A): regions are nodes with stable story ids, transitions and their
    /// own state (unlocked, visited, cleared, events, enemy return). The story progress unlocks regions; the
    /// village is the hub. This is Core authority and is stored as its own AC22 save section. No region section is
    /// written while a character has no region state, so old saves and their tests stay byte-identical.
    /// </summary>
    public sealed class RegionNetwork
    {
        public const string Aschengrenze = "region.aschengrenze.v1";
        public const string Aschenstrasse = "region.aschenstrasse.v1";
        public const string AlteMuehle = "region.alte-muehle.v1";
        public const string AlterSpeicher = "region.alter-speicher.v1";
        public const string VerborgeneKrypta = "region.verborgene-krypta.v1";
        public const string Rattlernest = "region.rattlernest.v1";
        public const string Waldweg = "region.waldweg.v1";
        public const string Baerenhoehle = "region.baerenhoehle.v1";
        public const string Aschenzoll = "region.aschenzoll.v1";
        public const string Reise = "region.reise-jenseits-aschenzoll.v1";

        private sealed class Node
        {
            public bool Visited, Cleared, ReturnPending;
            public int ReturnRun, Events;
            public bool HasState { get { return Visited || Cleared || ReturnRun != 0 || Events != 0; } }
        }

        private static readonly string[] Ordered =
        {
            Aschengrenze, Aschenstrasse, AlteMuehle, AlterSpeicher, VerborgeneKrypta,
            Rattlernest, Waldweg, Baerenhoehle, Aschenzoll, Reise
        };

        // Story act that first unlocks a region. Reise is special: it unlocks only once the onward travel is active.
        private static readonly Dictionary<string, int> UnlockAct = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { Aschengrenze, 0 }, { Aschenstrasse, 3 }, { AlteMuehle, 4 }, { AlterSpeicher, 5 },
            { VerborgeneKrypta, 6 }, { Rattlernest, 7 }, { Waldweg, 8 }, { Baerenhoehle, 9 }, { Aschenzoll, 10 }
        };

        // Undirected chain of neighbouring regions; the hub reaches every unlocked region without an edge.
        private static readonly (string A, string B)[] Edges =
        {
            (Aschengrenze, Aschenstrasse), (Aschenstrasse, AlteMuehle), (AlteMuehle, AlterSpeicher),
            (AlterSpeicher, VerborgeneKrypta), (VerborgeneKrypta, Rattlernest), (AlteMuehle, Waldweg),
            (Waldweg, Baerenhoehle), (Waldweg, Aschenzoll), (Aschenzoll, Reise)
        };

        private readonly SortedDictionary<string, Node> _nodes = new SortedDictionary<string, Node>(StringComparer.Ordinal);

        /// <summary>Set by the simulation: true only while the player is actually in the onward journey.</summary>
        public bool JourneyActive { get; set; }

        public static IReadOnlyList<string> AllRegions { get { return Ordered; } }

        public static string RegionForAct(int act)
        {
            if (act <= 2) return Aschengrenze;
            switch (act)
            {
                case 3: return Aschenstrasse;
                case 4: return AlteMuehle;
                case 5: return AlterSpeicher;
                case 6: return VerborgeneKrypta;
                case 7: return Rattlernest;
                case 8: return Waldweg;
                case 9: return Baerenhoehle;
                case 10: return Aschenzoll;
                default: return Aschengrenze;
            }
        }

        public bool IsUnlocked(string region, int act)
        {
            if (region == Reise) return JourneyActive;
            int threshold;
            return UnlockAct.TryGetValue(region, out threshold) && act >= threshold;
        }

        public IEnumerable<string> UnlockedRegions(int act) { return Ordered.Where(r => IsUnlocked(r, act)); }

        /// <summary>The region the story act currently places the player in; the journey overrides act 10.</summary>
        public string CurrentRegion(int act) { return JourneyActive ? Reise : RegionForAct(act); }

        private static bool IsEdge(string a, string b)
        {
            foreach (var e in Edges) if ((e.A == a && e.B == b) || (e.A == b && e.B == a)) return true;
            return false;
        }

        /// <summary>Adjacent unlocked regions and the hub are reachable; locked or non-adjacent targets are refused.</summary>
        public bool CanTransition(string from, string to, int act)
        {
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to) || from == to) return false;
            if (!IsUnlocked(from, act) || !IsUnlocked(to, act)) return false;
            if (from == Aschengrenze || to == Aschengrenze) return true;
            return IsEdge(from, to);
        }

        /// <summary>Enter an unlocked region; a cleared region returns its enemies on the next entry.</summary>
        public bool Enter(string region, int act)
        {
            if (!IsUnlocked(region, act)) return false;
            var node = Get(region);
            if (node.ReturnPending) { node.ReturnRun++; node.ReturnPending = false; }
            node.Visited = true;
            return true;
        }

        public bool Clear(string region)
        {
            var node = Read(region);
            if (node == null || !node.Visited) return false;
            node.Cleared = true;
            node.ReturnPending = true;
            return true;
        }

        /// <summary>True while a cleared region still owes a fresh enemy group to the next entry.</summary>
        public bool EnemiesReturn(string region) { var n = Read(region); return n != null && n.ReturnPending; }

        public bool IsVisited(string region) { var n = Read(region); return n != null && n.Visited; }
        public bool IsCleared(string region) { var n = Read(region); return n != null && n.Cleared; }
        public int ReturnRun(string region) { var n = Read(region); return n == null ? 0 : n.ReturnRun; }
        public int Events(string region) { var n = Read(region); return n == null ? 0 : n.Events; }
        /// <summary>Only known regions accept an event, so the API cannot create a phantom node.</summary>
        public bool SetEvent(string region, int value)
        {
            if (value < 0 || Array.IndexOf(Ordered, region) < 0) return false;
            Get(region).Events = value;
            return true;
        }

        public bool HasState
        {
            get { if (JourneyActive) return true; foreach (var n in _nodes.Values) if (n.HasState) return true; return false; }
        }

        private Node Read(string region) { Node n; return region != null && _nodes.TryGetValue(region, out n) ? n : null; }
        private Node Get(string region)
        {
            if (string.IsNullOrEmpty(region)) throw new ArgumentException("Region id is required.", nameof(region));
            Node n;
            if (!_nodes.TryGetValue(region, out n)) { n = new Node(); _nodes[region] = n; }
            return n;
        }

        /// <summary>Canonical, deterministic state string stored as the "regions" save section.</summary>
        public string Token()
        {
            var parts = new List<string>();
            foreach (var pair in _nodes)
            {
                if (!pair.Value.HasState) continue;
                var n = pair.Value;
                string flags = (n.Visited ? "1" : "0") + (n.Cleared ? "1" : "0") + (n.ReturnPending ? "1" : "0");
                parts.Add(pair.Key + "," + flags + "," + n.ReturnRun.ToString(CultureInfo.InvariantCulture) + "," + n.Events.ToString(CultureInfo.InvariantCulture));
            }
            return "1|" + (JourneyActive ? "1" : "0") + "|" + string.Join(";", parts);
        }

        public static bool TryRead(string token, out RegionNetwork network)
        {
            network = null;
            if (string.IsNullOrEmpty(token)) return false;
            string[] top = token.Split('|');
            if (top.Length != 3 || top[0] != "1" || (top[1] != "0" && top[1] != "1")) return false;
            var net = new RegionNetwork { JourneyActive = top[1] == "1" };
            if (top[2].Length != 0)
            {
                foreach (string entry in top[2].Split(';'))
                {
                    string[] f = entry.Split(',');
                    if (f.Length != 4 || Array.IndexOf(Ordered, f[0]) < 0 || net._nodes.ContainsKey(f[0])) return false;
                    if (f[1].Length != 3 || (f[1][0] != '0' && f[1][0] != '1') || (f[1][1] != '0' && f[1][1] != '1') || (f[1][2] != '0' && f[1][2] != '1')) return false;
                    int run, events;
                    if (!CanonicalInt(f[2], out run) || !CanonicalInt(f[3], out events)) return false;
                    if (f[1][1] == '1' && f[1][0] != '1') return false; // a clear can only follow a visit
                    if (f[1][2] == '1' && f[1][1] != '1') return false; // a return can only follow a clear
                    net._nodes[f[0]] = new Node { Visited = f[1][0] == '1', Cleared = f[1][1] == '1', ReturnPending = f[1][2] == '1', ReturnRun = run, Events = events };
                }
            }
            network = net;
            return true;
        }

        private static bool CanonicalInt(string text, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < text.Length; i++) if (text[i] < '0' || text[i] > '9') return false;
            if (text.Length > 1 && text[0] == '0') return false;
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= 0;
        }
    }

    public sealed partial class CharacterProgression
    {
        /// <summary>Region network state; contributes its own save section only once it has real state.</summary>
        public RegionNetwork Regions { get; private set; } = new RegionNetwork();

        private const string RegionSectionName = "regions";

        private void SyncRegionSection()
        {
            if (Regions.HasState) SetSection(RegionSectionName, Regions.Token());
            else if (_sections.ContainsKey(RegionSectionName)) _sections.Remove(RegionSectionName);
        }

        private void RestoreRegionSection()
        {
            string token;
            if (_sections.TryGetValue(RegionSectionName, out token) && RegionNetwork.TryRead(token, out var network)) Regions = network;
        }
    }
}
