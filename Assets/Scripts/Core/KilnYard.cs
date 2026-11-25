using System;
using System.Globalization;

namespace AreArpg.Core
{
    /// <summary>One clay vessel of the Brennerhof: it breaks once and stays broken while the wanderer is in Aschenwacht.</summary>
    public struct KilnVase
    {
        public readonly int Id;
        public readonly long Xmm, Zmm;
        /// <summary>The slim glazed urn; otherwise the round-bellied amphora.</summary>
        public readonly bool Glazed;
        public KilnVase(int id, long x, long z, bool glazed) { Id = id; Xmm = x; Zmm = z; Glazed = glazed; }
    }

    /// <summary>
    /// Owner decision 2026-11-25: a side gate in the east wall of Aschenwacht's forecourt leads into the Brennerhof, the bell
    /// order's veiled kiln yard. Going there is optional and comes before the village; the wanderer returns through the same
    /// gate. Nothing fights there. The yard holds one puzzle and one secret. The puzzle is the gate of the covered kiln passage
    /// (Ofengang): it is bolted on the side of the upper yard. The secret is the Scherbenschlupf, a crawl way under a collapsed
    /// roof behind the west kiln. Two weapons lie there as fixed finds (owner: fixed finds, the random catalog stays frozen).
    /// The spear lies at the weapon bench by the south gate; the halberd lies in the Scherbenschlupf.
    ///
    /// The yard's own frame (<see cref="KilnLayout"/>, millimetres): x east, z north, the south gate in the south, the upper
    /// yard in the north. Aschenwacht's frame is <see cref="CourtLayout"/>.
    /// </summary>
    public static class KilnYard
    {
        public const string Title = "Aschenwacht – Der verschleierte Brennerhof";
        public const string Arrival = "Hinter dem Seitentor liegt der Brennerhof des Glockenordens. Hier brannten die Brüder den Ton für " +
            "die Glockenformen; die Öfen sind kalt, doch unter dem Nebel glimmt noch Glut.";
        public const string Objective = "Erkunde den Brennerhof. Durch das Südtor geht es zurück nach Aschenwacht.";

        /// <summary>The side gate in the forecourt's east wall, in Aschenwacht's frame.</summary>
        public const long SideGateXmm = 40300L, SideGateZmm = 3000L;
        /// <summary>Where the wanderer stands in the forecourt when he comes back out of the side gate.</summary>
        public const long CourtReturnXmm = 37600L, CourtReturnZmm = 3000L;

        /// <summary>The gate of the kiln passage, closed across the passage between the Topfhof and the kilns.</summary>
        public const long GateXmm = 0L, GateZmm = 10300L;
        /// <summary>
        /// Cells the closed gate blocks: its width plus the wanderer's radius, and its depth plus that radius. The baked walk map
        /// treats the passage as open; the rules close it here until the bolt is drawn.
        /// </summary>
        public const long GateHalfWidthMm = 2300L, GateHalfDepthMm = 750L;
        /// <summary>The bolt sits on the north side, inside the kiln passage, as the set's own gate demands.</summary>
        public const long LatchMinZmm = 10500L, LatchMaxZmm = 13700L, LatchHalfWidthMm = 2200L;
        /// <summary>From the Topfhof the gate can be looked at but not opened.</summary>
        public const long LockedMinZmm = 6900L, LockedMaxZmm = 10100L;

        /// <summary>Where the spear can be taken: in front of the weapon bench by the south gate.</summary>
        public const long SpearXmm = -4600L, SpearZmm = -2300L;
        /// <summary>Where the halberd lies: in the Scherbenschlupf, between its low arch and the kiln passage.</summary>
        public const long HalberdXmm = -2600L, HalberdZmm = 21400L;

        public const long VaseReachMm = 1800L;
        public static readonly KilnVase[] Vases =
        {
            new KilnVase(0, -3500L, -6000L, false), new KilnVase(1, -4600L, -5100L, true),
            new KilnVase(2, 5600L, -2700L, false), new KilnVase(3, 5900L, -1500L, true),
            new KilnVase(4, -10800L, 8000L, true), new KilnVase(5, 7000L, 9000L, false),
            new KilnVase(6, -11200L, 26100L, false), new KilnVase(7, 5600L, 28500L, true)
        };

        /// <summary>Bits of <see cref="CharacterProgression.KilnState"/>.</summary>
        public const int InsideBit = 1, VisitedBit = 2, GateBit = 4, VaseShift = 3;
        public const int AllState = (1 << (VaseShift + 8)) - 1;

        public const string GateLocked = "Das Gitter des Ofengangs ist verriegelt. Der Riegel sitzt auf der anderen Seite.";
        public const string GateOpened = "Du ziehst den Riegel zurück. Das Gitter hebt sich, der Ofengang ist offen.";
        public const string VaseBroken = "Der Ton zerbricht.";

        /// <summary>How many of the yard's two weapons the wanderer carries.</summary>
        public static int FoundCount(CharacterProgression p) =>
            (p.Equipment.OwnsRegionalRelic(RegionalRelic.B_Brennerspeer) ? 1 : 0) + (p.Equipment.OwnsRegionalRelic(RegionalRelic.B_Scherbenhellebarde) ? 1 : 0);

        /// <summary>Whether a point lies in the cells the closed gate blocks.</summary>
        public static bool InGate(long x, long z) =>
            Math.Abs(x - GateXmm) <= GateHalfWidthMm && Math.Abs(z - GateZmm) <= GateHalfDepthMm;

        public static bool OnLatchSide(long x, long z) =>
            Math.Abs(x - GateXmm) <= LatchHalfWidthMm && z >= LatchMinZmm && z <= LatchMaxZmm;

        public static bool OnLockedSide(long x, long z) =>
            Math.Abs(x - GateXmm) <= LatchHalfWidthMm && z >= LockedMinZmm && z <= LockedMaxZmm;

        /// <summary>Canonical decimal of the yard's state: no leading zero, every bit known, inside or broken implies visited.</summary>
        internal static bool TryReadState(string text, out int state)
        {
            state = 0;
            if (text.Length == 0 || text.Length > 4 || text[0] == '0') return false;
            foreach (char ch in text) { if (ch < '0' || ch > '9') return false; state = state * 10 + (ch - '0'); }
            return state > 0 && state <= AllState && (state & VisitedBit) != 0;
        }
    }

    public sealed partial class CharacterProgression
    {
        /// <summary>
        /// The Brennerhof while the wanderer is in Aschenwacht: inside, visited, the gate's bolt drawn and the broken vases
        /// (<see cref="KilnYard.InsideBit"/> and following). Written as <c>,b</c> plus the decimal after the court's traces.
        /// </summary>
        public int KilnState { get; private set; }
        public bool InKilnYard => (KilnState & KilnYard.InsideBit) != 0;
        public bool KilnVisited => (KilnState & KilnYard.VisitedBit) != 0;
        public bool KilnGateOpen => (KilnState & KilnYard.GateBit) != 0;
        public bool KilnVaseBroken(int id) => id >= 0 && id < KilnYard.Vases.Length && (KilnState & (1 << (KilnYard.VaseShift + id))) != 0;

        internal bool EnterKilnYard()
        {
            if (EntryAct != 1 || !InCourt || InKilnYard) return false;
            KilnState |= KilnYard.InsideBit | KilnYard.VisitedBit;
            return true;
        }

        internal bool LeaveKilnYard()
        {
            if (!InKilnYard) return false;
            KilnState &= ~KilnYard.InsideBit;
            return true;
        }

        internal bool OpenKilnGate()
        {
            if (!InKilnYard || KilnGateOpen) return false;
            KilnState |= KilnYard.GateBit;
            return true;
        }

        internal bool BreakKilnVase(int id)
        {
            if (!InKilnYard || id < 0 || id >= KilnYard.Vases.Length || KilnVaseBroken(id)) return false;
            KilnState |= 1 << (KilnYard.VaseShift + id);
            return true;
        }

        /// <summary>The court token after <c>1,1,c</c>: the traces, then optionally <c>,b</c> and the yard's state.</summary>
        private static bool ReadCourtToken(string text, out int traces, out int kiln)
        {
            traces = kiln = 0;
            int comma = text.IndexOf(',');
            if (comma < 0) return ReadCourtTraces(text, out traces);
            string yard = text.Substring(comma + 1);
            return ReadCourtTraces(text.Substring(0, comma), out traces) && yard.Length > 1 && yard[0] == 'b' &&
                KilnYard.TryReadState(yard.Substring(1), out kiln);
        }

        private string CourtToken() =>
            ",c" + (CourtTraces > 0 ? CourtTraces.ToString(CultureInfo.InvariantCulture) : "") +
            (KilnState > 0 ? ",b" + KilnState.ToString(CultureInfo.InvariantCulture) : "");
    }

    public sealed partial class ArenaSimulation
    {
        /// <summary>In the Brennerhof, the side yard of Aschenwacht.</summary>
        public bool InKilnYard { get { return Progression.EntryAct == 1 && Progression.InCourt && Progression.InKilnYard; } }

        /// <summary>In Aschenwacht's forecourt at the side gate to the Brennerhof.</summary>
        public bool AtKilnSideGate { get { return InCourt && DistanceSquared(PlayerXmm, PlayerZmm, KilnYard.SideGateXmm, KilnYard.SideGateZmm) <= FixedMath.Square(PassageRadiusMm); } }

        private bool CanStandInKilnYard(long x, long z) => KilnLayout.CanStand(x, z) && (Progression.KilnGateOpen || !KilnYard.InGate(x, z));

        /// <summary>The gate within reach while it is still bolted: from the passage side to open it, from the Topfhof to find it locked.</summary>
        public bool NearKilnGate { get { return InKilnYard && !GameOver && !Progression.KilnGateOpen && (KilnYard.OnLatchSide(PlayerXmm, PlayerZmm) || KilnYard.OnLockedSide(PlayerXmm, PlayerZmm)); } }

        /// <summary>The nearest unbroken vase within reach; null when none is.</summary>
        public KilnVase? NearKilnVase
        {
            get
            {
                if (!InKilnYard || GameOver) return null;
                KilnVase? best = null; long bestDistance = FixedMath.Square(KilnYard.VaseReachMm);
                foreach (KilnVase vase in KilnYard.Vases)
                {
                    if (Progression.KilnVaseBroken(vase.Id)) continue;
                    long distance = DistanceSquared(PlayerXmm, PlayerZmm, vase.Xmm, vase.Zmm);
                    if (distance <= bestDistance && (best == null || distance < bestDistance)) { best = vase; bestDistance = distance; }
                }
                return best;
            }
        }

        /// <summary>Through the side gate into the Brennerhof; nothing has to be done first.</summary>
        public ArenaSimulation TryEnterKilnYard()
        {
            if (!InCourt || !AtKilnSideGate || GameOver) return null;
            CharacterProgression nextProgress;
            if (!CharacterProgression.TryLoad(Progression.SaveString(), _journeyMaps, out nextProgress) || !nextProgress.EnterKilnYard()) return null;
            return new ArenaSimulation(_runSeed, nextProgress, CryptDepth, _lootCatalog, _journeyMaps);
        }

        /// <summary>Back through the south gate into Aschenwacht's forecourt; the yard keeps what happened there.</summary>
        public ArenaSimulation TryLeaveKilnYard()
        {
            if (!InKilnYard || GameOver) return null;
            CharacterProgression nextProgress;
            if (!CharacterProgression.TryLoad(Progression.SaveString(), _journeyMaps, out nextProgress) || !nextProgress.LeaveKilnYard()) return null;
            return new ArenaSimulation(_runSeed, nextProgress, CryptDepth, _lootCatalog, _journeyMaps);
        }

        private SocialResult OpenKilnGate()
        {
            if (!NearKilnGate) return new SocialResult { Text = "Hier ist kein Gitter in Reichweite." };
            if (!KilnYard.OnLatchSide(PlayerXmm, PlayerZmm)) return new SocialResult { Text = KilnYard.GateLocked };
            Progression.OpenKilnGate();
            return new SocialResult { Accepted = true, Text = KilnYard.GateOpened };
        }

        private SocialResult BreakKilnVase()
        {
            KilnVase? vase = NearKilnVase;
            if (vase == null || !Progression.BreakKilnVase(vase.Value.Id)) return new SocialResult { Text = "Hier steht keine Vase in Reichweite." };
            return new SocialResult { Accepted = true, Text = KilnYard.VaseBroken };
        }
    }
}
