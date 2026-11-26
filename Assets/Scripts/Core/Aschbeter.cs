using System;
using System.Globalization;

namespace AreArpg.Core
{
    public enum AschbeterMode { Idle = 0, Shuffle = 1, TurnPositive = 2, TurnNegative = 3, Strike = 4, Fall = 5, Rise = 6, Dead = 7 }
    public enum AschbeterSwing { None = 0, Miss = 1, Deflected = 2, Staggered = 3, Fatal = 4 }

    /// <summary>
    /// The Aschbeter on Aschenwacht's procession path, an enemy by the rules of the owner's archive (owner decision 2026-11-26,
    /// answer A). The rules are the archive's own encounter (Spielprobe/AschbeterRegeln.gd), one call per 100 ms tick, every
    /// position in millimetres: he notices the wanderer at 8.5 m, turns towards him by 5 degrees every 0.3 s, shuffles at
    /// 0.32 m/s and strikes with his claw from 2.12 m; at the fifth tick of the strike the claw reaches 2.27 m and, within 25
    /// degrees in front of him, lands a blow that can kill. A swing reaches him only while he turns: from at least 45 degrees
    /// to the side it throws him down for good, a flatter one only throws him down (the second one is fatal); a swing while
    /// he does not turn is deflected. He can be avoided; nobody has to defeat him.
    ///
    /// Headings are 72 steps of 5 degrees: step 0 faces +z, increasing steps turn towards +x. The direction table is the
    /// archive's fixed integer table, so the same inputs give the same encounter everywhere.
    /// </summary>
    public sealed class AschbeterState
    {
        public const int DetectMm = 8500, StrikeStartMm = 2120, ClawReachMm = 2270, ContactTick = 5, StrikeTicks = 10;
        public const int RecoveryTicks = 13, FallTicks = 8, RiseTicks = 10, StepMm = 32, ShortReachMm = 1920, LongReachMm = 2550;
        public const int LookSteps = 72, FrontSteps = 5, SwingSteps = 9, FlankSteps = 9;
        /// <summary>Where the archive puts him: on the procession path, facing the crypt portal he guards.</summary>
        public const long StartXmm = 0L, StartZmm = 8000L;
        public const int StartHeading = 36;

        private static readonly int[] UnitX =
        {
            0, 87, 174, 259, 342, 423, 500, 574, 643, 707, 766, 819, 866, 906, 940, 966, 985, 996,
            1000, 996, 985, 966, 940, 906, 866, 819, 766, 707, 643, 574, 500, 423, 342, 259, 174, 87,
            0, -87, -174, -259, -342, -423, -500, -574, -643, -707, -766, -819, -866, -906, -940, -966, -985, -996,
            -1000, -996, -985, -966, -940, -906, -866, -819, -766, -707, -643, -574, -500, -423, -342, -259, -174, -87
        };
        private static readonly int[] UnitZ =
        {
            1000, 996, 985, 966, 940, 906, 866, 819, 766, 707, 643, 574, 500, 423, 342, 259, 174, 87,
            0, -87, -174, -259, -342, -423, -500, -574, -643, -707, -766, -819, -866, -906, -940, -966, -985, -996,
            -1000, -996, -985, -966, -940, -906, -866, -819, -766, -707, -643, -574, -500, -423, -342, -259, -174, -87,
            0, 87, 174, 259, 342, 423, 500, 574, 643, 707, 766, 819, 866, 906, 940, 966, 985, 996
        };

        public long Xmm { get; private set; }
        public long Zmm { get; private set; }
        public int Heading { get; private set; }
        public AschbeterMode Mode { get; private set; }
        public int ModeTicks { get; private set; }
        public int Stamina { get; private set; }
        public int ClawCooldown { get; private set; }
        public int StrikeCooldown { get; private set; }
        public int Ticks { get; private set; }
        /// <summary>What the last tick decided, read by the presentation: the swing's outcome, the claw's contact and hit.</summary>
        public AschbeterSwing LastSwing { get; private set; }
        public bool ClawContact { get; private set; }
        public bool ClawHit { get; private set; }
        /// <summary>The last swing that met him (miss, deflection, fall) and how many did so far; kept until the next one.</summary>
        public AschbeterSwing LastOutcome { get; private set; }
        public int Outcomes { get; private set; }
        public bool IsDead { get { return Mode == AschbeterMode.Dead; } }
        public bool Down { get { return Mode == AschbeterMode.Fall || Mode == AschbeterMode.Rise || Mode == AschbeterMode.Dead; } }

        public AschbeterState() : this(StartXmm, StartZmm, StartHeading) { }

        public AschbeterState(long x, long z, int heading)
        {
            Xmm = x; Zmm = z; Heading = Mod(heading, LookSteps);
            Mode = AschbeterMode.Idle; Stamina = 2;
        }

        /// <summary>The nearest of the 72 headings to a direction; <paramref name="ifCoincident"/> when there is none.</summary>
        public static int LookAt(long x, long z, int ifCoincident)
        {
            if (x == 0 && z == 0) return ifCoincident;
            int best = 0; long bestDot = long.MinValue;
            for (int i = 0; i < LookSteps; i++)
            {
                long dot = UnitX[i] * x + UnitZ[i] * z;
                if (dot > bestDot) { best = i; bestDot = dot; }
            }
            return best;
        }

        /// <summary>Signed steps from one heading to another, the shorter way; the exact half turn counts positive.</summary>
        public static int AngleSteps(int from, int to)
        {
            int delta = Mod(to - from, LookSteps);
            return delta > LookSteps / 2 ? delta - LookSteps : delta;
        }

        public static long DirectionX(int heading) { return UnitX[Mod(heading, LookSteps)]; }
        public static long DirectionZ(int heading) { return UnitZ[Mod(heading, LookSteps)]; }

        private static int Mod(int value, int m) { int r = value % m; return r < 0 ? r + m : r; }
        private static bool Within(long x, long z, long reach) { return x * x + z * z <= reach * reach; }

        private void SetMode(AschbeterMode value)
        {
            if (Mode == value) return;
            Mode = value; ModeTicks = 0;
        }

        /// <summary>
        /// One tick. <paramref name="swung"/> says the wanderer's swing lands this tick, facing <paramref name="swingHeading"/>
        /// with <paramref name="reachMm"/> (his weapon's reach, at most the long staff's); <paramref name="forwardClear"/> says
        /// the ground ahead of the Aschbeter is free for his next step.
        /// </summary>
        internal void Step(long playerX, long playerZ, bool playerAlive, bool swung, int swingHeading, long reachMm, bool forwardClear)
        {
            Ticks++; ModeTicks++;
            LastSwing = AschbeterSwing.None; ClawContact = false; ClawHit = false;
            StrikeCooldown = Math.Max(0, StrikeCooldown - 1);
            ClawCooldown = Math.Max(0, ClawCooldown - 1);
            if (Mode == AschbeterMode.Dead) return;
            if (Mode == AschbeterMode.Fall || Mode == AschbeterMode.Rise) { AdvanceFall(); return; }
            long ox = playerX - Xmm, oz = playerZ - Zmm;
            int error = AngleSteps(Heading, LookAt(ox, oz, Heading));
            if (Mode != AschbeterMode.Strike)
            {
                if (!playerAlive || !Within(ox, oz, DetectMm)) SetMode(AschbeterMode.Idle);
                else if (Math.Abs(error) > FrontSteps) SetMode(error > 0 ? AschbeterMode.TurnPositive : AschbeterMode.TurnNegative);
                else if (Within(ox, oz, StrikeStartMm)) SetMode(ClawCooldown == 0 ? AschbeterMode.Strike : AschbeterMode.Idle);
                else SetMode(AschbeterMode.Shuffle);
            }
            if (swung && playerAlive) ReceiveSwing(playerX, playerZ, swingHeading, reachMm, error);
            if (Mode == AschbeterMode.Strike)
            {
                if (ModeTicks == ContactTick)
                {
                    ClawContact = true;
                    ClawHit = playerAlive && Within(ox, oz, ClawReachMm) && Math.Abs(AngleSteps(Heading, LookAt(ox, oz, Heading))) <= FrontSteps;
                }
                else if (ModeTicks >= StrikeTicks) { SetMode(AschbeterMode.Idle); ClawCooldown = RecoveryTicks; }
            }
            else if (Mode == AschbeterMode.TurnPositive || Mode == AschbeterMode.TurnNegative)
            {
                if (ModeTicks > 0 && ModeTicks % 3 == 0) Heading = Mod(Heading + (Mode == AschbeterMode.TurnPositive ? 1 : -1), LookSteps);
            }
            else if (Mode == AschbeterMode.Shuffle && forwardClear)
            {
                Xmm += Forward(UnitX[Heading]); Zmm += Forward(UnitZ[Heading]);
            }
        }

        /// <summary>Where his next shuffle would take him, for the ground check before the tick.</summary>
        public long NextXmm { get { return Xmm + Forward(UnitX[Heading]); } }
        public long NextZmm { get { return Zmm + Forward(UnitZ[Heading]); } }

        private static long Forward(int component) { return Math.Sign(component) * ((Math.Abs(component) * StepMm + 500) / 1000); }

        private void AdvanceFall()
        {
            if (Mode == AschbeterMode.Fall && ModeTicks >= FallTicks) SetMode(Stamina == 0 ? AschbeterMode.Dead : AschbeterMode.Rise);
            else if (Mode == AschbeterMode.Rise && ModeTicks >= RiseTicks) { SetMode(AschbeterMode.Idle); ClawCooldown = RecoveryTicks; }
        }

        private void ReceiveSwing(long playerX, long playerZ, int swingHeading, long reachMm, int turnError)
        {
            if (StrikeCooldown > 0) return;
            bool longStaff = reachMm > ShortReachMm;
            StrikeCooldown = longStaff ? 8 : 6;
            long ix = Xmm - playerX, iz = Zmm - playerZ;
            Outcomes++;
            if (!Within(ix, iz, reachMm) || Math.Abs(AngleSteps(swingHeading, LookAt(ix, iz, swingHeading))) > SwingSteps)
            { LastSwing = LastOutcome = AschbeterSwing.Miss; return; }
            if (Mode != AschbeterMode.TurnPositive && Mode != AschbeterMode.TurnNegative) { LastSwing = LastOutcome = AschbeterSwing.Deflected; return; }
            // A flat, nearly frontal hit throws him down; from at least 45 degrees to the side it breaks him entirely.
            Stamina = Math.Abs(turnError) >= FlankSteps ? 0 : Math.Max(0, Stamina - 1);
            SetMode(AschbeterMode.Fall);
            LastSwing = LastOutcome = Stamina == 0 ? AschbeterSwing.Fatal : AschbeterSwing.Staggered;
        }

        /// <summary>The state's fields for the world hash, in a fixed order.</summary>
        internal long[] HashFields()
        {
            return new long[] { Ticks, Xmm, Zmm, Heading, (int)Mode, ModeTicks, Stamina, ClawCooldown, StrikeCooldown, (int)LastSwing, ClawContact ? 1 : 0, ClawHit ? 1 : 0,
                (int)LastOutcome, Outcomes };
        }
    }

    public sealed partial class CharacterProgression
    {
        /// <summary>The Aschbeter lies dead on the procession path; kept while the wanderer is in Aschenwacht.</summary>
        public bool AschbeterFallen { get; private set; }

        internal bool FellAschbeter()
        {
            if (EntryAct != 1 || !InCourt || AschbeterFallen) return false;
            AschbeterFallen = true;
            return true;
        }
    }

    public sealed partial class ArenaSimulation
    {
        /// <summary>The Aschbeter while the wanderer is in Aschenwacht's court; null elsewhere.</summary>
        public AschbeterState Aschbeter { get; private set; }

        private bool _aschbeterSwing;
        private int _aschbeterSwingHeading;
        private long _aschbeterSwingReach;

        /// <summary>He stands where the archive puts him, unless he already lies dead (then he lies there).</summary>
        private void PlaceAschbeter()
        {
            Aschbeter = InCourt && !Progression.AschbeterFallen ? new AschbeterState() : null;
        }

        /// <summary>Whether a swing now would reach him: the wanderer turns to him when no other foe is nearer.</summary>
        private bool AschbeterInReach(long reach)
        {
            var a = Aschbeter;
            if (a == null || a.Down || !InCourt) return false;
            long r = Math.Min(reach, AschbeterState.LongReachMm);
            return DistanceSquared(PlayerXmm, PlayerZmm, a.Xmm, a.Zmm) <= r * r;
        }

        /// <summary>A swing of the wanderer lands this tick; the Aschbeter's next step decides what it does to him.</summary>
        private void SwingAtAschbeter(long reach)
        {
            var a = Aschbeter;
            if (a == null || !InCourt) return;
            // The wanderer turns to him for the swing (as to any foe in reach), so the swing faces him.
            _aschbeterSwing = true;
            _aschbeterSwingReach = Math.Min(reach, AschbeterState.LongReachMm);
            _aschbeterSwingHeading = AschbeterState.LookAt(a.Xmm - PlayerXmm, a.Zmm - PlayerZmm, 0);
        }

        /// <summary>The Aschbeter in the world hash, only where he stands: every other world keeps its hash.</summary>
        private void MixAschbeter(ref ulong h)
        {
            if (Aschbeter == null) return;
            Mix(ref h, 0x4153434842455445UL); // "ASCHBETE"
            foreach (long v in Aschbeter.HashFields()) Mix(ref h, unchecked((ulong)v));
        }

        private void StepAschbeter()
        {
            var a = Aschbeter;
            if (a == null || !InCourt) return;
            bool swung = _aschbeterSwing; _aschbeterSwing = false;
            bool forward = CourtLayout.CanStand(a.NextXmm, a.NextZmm);
            a.Step(PlayerXmm, PlayerZmm, !GameOver, swung, _aschbeterSwingHeading, _aschbeterSwingReach, forward);
            // His claw is a blow like a creature's: a dodge escapes it, a raised guard and armour soften it; unsoftened it kills.
            if (a.ClawHit) ReceiveBlowFrom(MaxPlayerHealth, a.Xmm, a.Zmm);
            if (a.IsDead && Progression.FellAschbeter()) DropAschbeterLoot(a);
        }

        /// <summary>He falls like a guardian of the crypt: experience, a kill, and a guaranteed drop where he lies.</summary>
        private void DropAschbeterLoot(AschbeterState a)
        {
            Kills++; Progression.RecordKill();
            Progression.EarnExperience(new System.Numerics.BigInteger(1200));
            bool second = LootRevisionTwo;
            LootItem item = _lootCatalog == null ? null :
                _lootCatalog.GenerateFlagged(_runSeed, CryptDepth, CurrentTick, AschbeterLootId, 2, Progression.LootOrdinal, second ? LootCatalog.GuaranteedFlag : 0);
            if (second && item == null) return;
            GroundLoot.Add(new LootState { Id = _nextLootId++, Xmm = a.Xmm, Zmm = a.Zmm, WeaponBonus = 34, Item = item });
        }

        /// <summary>The monster id his drop is rolled with; no creature of the crypt uses it.</summary>
        internal const int AschbeterLootId = 90;
    }
}
