using System;
using System.Globalization;
using System.Linq;

namespace AreArpg.Core
{
    public enum SealApproach { Undecided = 0, Investigated = 1, Broken = 2 }

    /// <summary>One authored investigation, owned and serialized by CharacterProgression.</summary>
    public sealed class CryptInvestigation
    {
        public bool Active { get; internal set; }
        public int Clues { get; internal set; }
        public SealApproach Approach { get; internal set; }
        public bool TraceFound { get; internal set; }
        public bool Reported { get; internal set; }
        // 0 armed/not triggered; 1..10 warning ticks; -1 permanently spent.
        public int TrapTicks { get; internal set; }
        internal int SavedAct, SavedHealth, SavedResource;
        internal long SavedX, SavedZ;
        internal readonly int[] SavedCooldowns = new int[4];
        internal ulong SavedRng0, SavedRng1;
        public void RestartPlayer() { SavedAct = 0; }

        internal string Token()
        {
            object[] f = { Active ? 1 : 0, Clues, (int)Approach, TraceFound ? 1 : 0, Reported ? 1 : 0,
                TrapTicks, SavedAct, SavedX, SavedZ, SavedHealth, SavedResource,
                SavedCooldowns[0], SavedCooldowns[1], SavedCooldowns[2], SavedCooldowns[3], SavedRng0, SavedRng1 };
            return string.Join(",", f.Select(x => Convert.ToString(x, CultureInfo.InvariantCulture)));
        }

        internal static bool TryRead(string token, int act, out CryptInvestigation result)
        {
            result = null; string[] f = token.Split(','); if (f.Length != 17) return false;
            var n = new long[15];
            for (int i = 0; i < n.Length; i++)
                if (!long.TryParse(f[i], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out n[i]) ||
                    n[i].ToString(CultureInfo.InvariantCulture) != f[i]) return false;
            ulong a, b;
            if (!ulong.TryParse(f[15], NumberStyles.None, CultureInfo.InvariantCulture, out a) ||
                !ulong.TryParse(f[16], NumberStyles.None, CultureInfo.InvariantCulture, out b) ||
                a.ToString(CultureInfo.InvariantCulture) != f[15] || b.ToString(CultureInfo.InvariantCulture) != f[16]) return false;
            if (n[0] != 1 || n[1] < 0 || n[1] > 7 || n[2] < 0 || n[2] > 2 || n[3] < 0 || n[3] > 1 ||
                n[4] < 0 || n[4] > n[3] || n[5] < -1 || n[5] > 10 ||
                (n[2] == 0 && (n[3] != 0 || act == 6)) || (n[2] == 1 && n[1] != 7) ||
                (n[2] == 2 && (n[1] & 4) == 0) || (n[2] != 2 && n[5] != 0) ||
                (n[6] != 0 && (n[6] != act || (act != 5 && act != 6) || (a | b) == 0)) ||
                n[7] < -27000 || n[7] > 27000 || n[8] < -27000 || n[8] > 27000 ||
                n[9] < 0 || n[9] > int.MaxValue || n[10] < 0 || n[10] > 100) return false;
            if (n[6] != 0 && !ArenaSimulation.CanStandInInvestigation(act, (SealApproach)n[2], n[7], n[8])) return false;
            for (int i = 11; i < 15; i++) if (n[i] < 0 || n[i] > 300) return false;
            var r = new CryptInvestigation { Active = true, Clues = (int)n[1], Approach = (SealApproach)n[2],
                TraceFound = n[3] == 1, Reported = n[4] == 1, TrapTicks = (int)n[5], SavedAct = (int)n[6],
                SavedX = n[7], SavedZ = n[8], SavedHealth = (int)n[9], SavedResource = (int)n[10], SavedRng0 = a, SavedRng1 = b };
            for (int i = 0; i < 4; i++) r.SavedCooldowns[i] = (int)n[11 + i];
            result = r; return true;
        }
    }

    public sealed partial class CharacterProgression
    {
        public CryptInvestigation Investigation { get; private set; } = new CryptInvestigation();
    }

    public sealed partial class ArenaSimulation
    {
        public bool AtInvestigationSeal { get { return Progression.EntryAct == 5 && DistanceSquared(PlayerXmm, PlayerZmm, 0, 18000) <= FixedMath.Square(NpcInteractionRadiusMm); } }
        public const long CryptTrapZmm = -3000;
        public const long CryptTrapRadiusMm = 2400;

        private void InitializeInvestigation()
        {
            var c = Progression.Investigation;
            if (c.SavedAct == 0 || c.SavedAct != Progression.EntryAct) return;
            PlayerXmm = PrevPlayerXmm = c.SavedX; PlayerZmm = PrevPlayerZmm = c.SavedZ;
            PlayerHealth = Math.Min(MaxPlayerHealth, c.SavedHealth); Resource = c.SavedResource;
            for (int i = 0; i < 4; i++) Cooldowns[i] = c.SavedCooldowns[i];
            _rng.Restore(c.SavedRng0, c.SavedRng1);
        }

        private void SaveInvestigationPlayer()
        {
            var c = Progression.Investigation; int act = Progression.EntryAct;
            if (!c.Active || (act != 5 && act != 6)) return;
            c.SavedAct = act; c.SavedX = PlayerXmm; c.SavedZ = PlayerZmm;
            c.SavedHealth = PlayerHealth; c.SavedResource = Resource;
            for (int i = 0; i < 4; i++) c.SavedCooldowns[i] = Cooldowns[i];
            c.SavedRng0 = _rng.S0; c.SavedRng1 = _rng.S1;
        }

        private void StepInvestigation()
        {
            var c = Progression.Investigation;
            if (Progression.EntryAct == 6 && c.Approach == SealApproach.Broken && !GameOver)
            {
                if (c.TrapTicks > 0)
                {
                    c.TrapTicks--;
                    if (c.TrapTicks == 0)
                    {
                        c.TrapTicks = -1;
                        if (DistanceSquared(PlayerXmm, PlayerZmm, 0, CryptTrapZmm) <= FixedMath.Square(CryptTrapRadiusMm))
                            PlayerHealth = Math.Max(0, PlayerHealth - 320);
                    }
                }
                else if (c.TrapTicks == 0 && DistanceSquared(PlayerXmm, PlayerZmm, 0, CryptTrapZmm) <= FixedMath.Square(3000))
                    c.TrapTicks = 10;
            }
            SaveInvestigationPlayer();
        }

        /// <summary>One geometry contract for walkability and visible storage/crypt obstacles.</summary>
        public readonly struct InvestigationBox
        {
            public readonly long X, Z, HalfX, HalfZ;
            public InvestigationBox(long x, long z, long halfX, long halfZ) { X = x; Z = z; HalfX = halfX; HalfZ = halfZ; }
        }
        public static InvestigationBox InvestigationObstacle(bool crypt, int index)
        {
            if (index < 0 || index > 1) throw new ArgumentOutOfRangeException(nameof(index));
            return crypt ? new InvestigationBox(index == 0 ? -7500 : 7500, 4000, 1500, 2800) :
                index == 0 ? new InvestigationBox(-7000, -8000, 2000, 2000) : new InvestigationBox(7000, 5000, 2000, 2500);
        }
        internal static bool CanStandInInvestigation(int act, SealApproach approach, long x, long z)
        {
            if (x < -12000 + PlayerRadiusMm || x > 12000 - PlayerRadiusMm || z < -ArenaLimitMm || z > ArenaLimitMm) return false;
            for (int i = 0; i < 2; i++)
            {
                var box = InvestigationObstacle(act == 6, i);
                if (Math.Abs(x - box.X) <= box.HalfX + PlayerRadiusMm &&
                    Math.Abs(z - box.Z) <= box.HalfZ + PlayerRadiusMm) return false;
            }
            if ((act == 6 || approach == SealApproach.Undecided) &&
                z > 20500 - PlayerRadiusMm) return false;
            return true;
        }

        private SocialResult ActOnInvestigation(HungerAction action)
        {
            var c = Progression.Investigation; var result = new SocialResult { Text = "Gehe zum passenden Hinweis." };
            if (GameOver || !c.Active || Progression.HungerStage != 4) return result;
            int bit = action == HungerAction.StoreBandage ? 1 : action == HungerAction.StoreTracks ? 2 : action == HungerAction.InspectSeal ? 4 : 0;
            long x = bit == 1 ? -6500 : bit == 2 ? 6500 : 0, z = bit == 1 ? 0 : bit == 2 ? -3000 : 18000;
            if (bit != 0 && Progression.EntryAct == 5 && (c.Clues & bit) == 0 &&
                DistanceSquared(PlayerXmm, PlayerZmm, x, z) <= FixedMath.Square(NpcInteractionRadiusMm))
            {
                c.Clues |= bit;
                result.Text = bit == 1 ? "Der Verband traegt denselben Knoten wie im Verletztenlager." : bit == 2 ?
                    "Die Schleifspur endet nicht hier: Staub rieselt durch die Fugen unter dem Speicher." :
                    "Das Siegel haelt einen alten Zugmechanismus. Gewaltsames Brechen kann lose Deckensteine ausloesen. Erst untersuchen oder das Risiko bewusst eingehen.";
            }
            else if ((action == HungerAction.OpenSeal || action == HungerAction.BreakSeal) && Progression.EntryAct == 5 &&
                c.Approach == SealApproach.Undecided && (c.Clues & 4) != 0 &&
                DistanceSquared(PlayerXmm, PlayerZmm, 0, 18000) <= FixedMath.Square(NpcInteractionRadiusMm))
            {
                if (action == HungerAction.OpenSeal && c.Clues != 7)
                { result.Text = "Untersuche zuerst Verband und Schleifspur. Der Mechanismus ist noch unklar."; return result; }
                c.Approach = action == HungerAction.OpenSeal ? SealApproach.Investigated : SealApproach.Broken;
                result.Text = c.Approach == SealApproach.Investigated ? "Du entlastest den Zugmechanismus. Der Abstieg oeffnet sich kontrolliert." :
                    "Das Siegel bricht. Ein Ruck geht durch das Mauerwerk. Achte unten auf Staub und markierte Fallstellen!";
            }
            else if (action == HungerAction.InspectCryptTrace && Progression.EntryAct == 6 && !c.TraceFound &&
                DistanceSquared(PlayerXmm, PlayerZmm, 0, 15000) <= FixedMath.Square(NpcInteractionRadiusMm))
            { c.TraceFound = true; result.Text = "Frischer Verbandsstoff liegt vor einer verriegelten inneren Tuer. Von dahinter kommen dumpfe Schlaege. Die Spur endet nicht im Speicher. Berichte Ilyan; wer dahinter ist, bleibt ungeklaert."; }
            else if (action == HungerAction.ReportCrypt && Progression.EntryAct == 2 && c.TraceFound && !c.Reported)
            {
                var npc = HungerContact(0); if (npc == null) return result;
                npc.InteractionCooldownTicks = 10; c.Reported = true;
                result.Text = "Ilyan hoert deinen Bericht: Eine verborgene Krypta und frische Spuren. Er weiss nun davon. Ob dort Vermisste leben oder ein Menschenfresser lauert, ist noch nicht bewiesen.";
            }
            else return result;
            SaveInvestigationPlayer(); result.Accepted = true; return result;
        }

        private string InvestigationObjective
        {
            get
            {
                var c = Progression.Investigation;
                if (!c.Active) return "Folge der Spur vom Verletztenlager durch den Nordweg zum alten Speicher.";
                if (c.Reported) return "Ilyan kennt die Spuren. Die innere Kryptatuer bleibt verschlossen; Ursache und Vermisste sind noch ungeklaert.";
                if (c.TraceFound) return "Kehre durch Speicher, Muehle und Aschestrasse zu Ilyan zurueck und berichte persoenlich.";
                if (c.Approach != SealApproach.Undecided) return c.Approach == SealApproach.Broken ?
                    "Steige in die Krypta. Der Siegelbruch hat Steine geloest: weiche roten Fallstellen aus. Untersuche den Verband vor der inneren Tuer." :
                    "Der Zugang ist gesichert. Steige in die Krypta und untersuche den Verband vor der inneren Tuer.";
                return "Im Speicher: Verband im Westen, Schleifspur im Suedosten und Siegel im Norden untersuchen. Dann kontrolliert oeffnen oder gewaltsam brechen.";
            }
        }
    }
}
