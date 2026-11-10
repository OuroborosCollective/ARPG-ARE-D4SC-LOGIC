using System;
using System.Globalization;
using System.Numerics;

namespace AreArpg.Core
{
    public enum NpcAction { Greet = 0, Help = 1, Attack = 2, Rob = 3 }
    public enum NpcPersonality { Cautious = 0, Peaceful = 1, Brave = 2 }
    public enum NpcDisposition { Neutral = 0, Friendly = 1, Wary = 2, Hostile = 3 }
    public enum NpcBehavior { Travel = 0, Rest = 1, SeekFood = 2, Eat = 3, Flee = 4, Attack = 5, Downed = 6 }

    public sealed class SocialResult
    {
        public bool Accepted;
        public string Text = "";
    }

    /// <summary>One NPC's local memory of this single-player character; no global shared reputation.</summary>
    public sealed class NpcMemory
    {
        public const int MaxHealth = 800;
        public BigInteger PositivePoints { get; private set; }
        public BigInteger NegativePoints { get; private set; }
        public BigInteger HelpfulActions { get; private set; }
        public BigInteger HostileActions { get; private set; }
        public BigInteger Meetings { get; private set; }
        public int Health { get; internal set; } = MaxHealth;
        public int Hunger { get; internal set; }
        public int Fatigue { get; internal set; }
        public int Purse { get; internal set; }
        public int RecoveryTicks { get; internal set; }
        public string RecentEvents { get; private set; } = "";
        public bool NeedsHelp { get { return Hunger >= 400 || Fatigue >= 400 || Health <= MaxHealth - 120; } }

        internal void Record(NpcAction action, int severity)
        {
            if (action < NpcAction.Greet || action > NpcAction.Rob || severity < 0 || severity > 3)
                throw new ArgumentOutOfRangeException(nameof(action));
            Meetings++;
            if (action == NpcAction.Help) { PositivePoints += 8; HelpfulActions++; }
            if (action == NpcAction.Attack || action == NpcAction.Rob)
            {
                NegativePoints += 20 * Math.Max(1, severity) + 5 * BigInteger.Min(HostileActions, 10);
                HostileActions++;
            }
            RecentEvents += ((int)action).ToString(CultureInfo.InvariantCulture);
            if (RecentEvents.Length > 6) RecentEvents = RecentEvents.Substring(RecentEvents.Length - 6);
        }

        public NpcDisposition Disposition(NpcPersonality personality)
        {
            int threshold = personality == NpcPersonality.Brave ? 18 : (personality == NpcPersonality.Cautious ? 35 : 60);
            if (NegativePoints >= threshold && NegativePoints * 2 + HostileActions * 10 > PositivePoints)
                return NpcDisposition.Hostile;
            if (NegativePoints > PositivePoints) return NpcDisposition.Wary;
            return PositivePoints >= 8 ? NpcDisposition.Friendly : NpcDisposition.Neutral;
        }

        internal string Token()
        {
            return string.Join(",", new[] { PositivePoints.ToString(CultureInfo.InvariantCulture),
                NegativePoints.ToString(CultureInfo.InvariantCulture), HelpfulActions.ToString(CultureInfo.InvariantCulture),
                HostileActions.ToString(CultureInfo.InvariantCulture), Meetings.ToString(CultureInfo.InvariantCulture),
                Health.ToString(CultureInfo.InvariantCulture), Hunger.ToString(CultureInfo.InvariantCulture),
                Fatigue.ToString(CultureInfo.InvariantCulture), Purse.ToString(CultureInfo.InvariantCulture), RecoveryTicks.ToString(CultureInfo.InvariantCulture),
                RecentEvents.Length == 0 ? "x" : RecentEvents });
        }

        internal static bool TryRead(string text, out NpcMemory memory)
        {
            memory = null;
            string[] f = text.Split(',');
            if (f.Length != 11) return false;
            var n = new BigInteger[10];
            for (int i = 0; i < n.Length; i++)
                if (!SocialLedger.ReadNumber(f[i], out n[i])) return false;
            BigInteger repeat = n[3] <= 10 ? n[3] * (n[3] - 1) / 2 : 45 + 10 * (n[3] - 10);
            BigInteger minimumHarm = 20 * n[3] + 5 * repeat;
            BigInteger maximumHarm = 60 * n[3] + 5 * repeat;
            if (n[5] > MaxHealth || n[6] > 1000 || n[7] > 1000 || n[8] > 30 || n[9] > 300 ||
                ((n[5] == 0) != (n[9] > 0)) ||
                n[2] + n[3] > n[4] || n[0] != n[2] * 8 || n[1] < minimumHarm || n[1] > maximumHarm) return false;
            if (f[10].Length == 0) return false;
            string events = f[10] == "x" ? "" : f[10];
            if (new BigInteger(events.Length) != BigInteger.Min(n[4], 6)) return false;
            foreach (char c in events) if (c < '0' || c > '3') return false;
            memory = new NpcMemory { PositivePoints = n[0], NegativePoints = n[1],
                HelpfulActions = n[2], HostileActions = n[3], Meetings = n[4],
                Health = (int)n[5], Hunger = (int)n[6], Fatigue = (int)n[7], Purse = (int)n[8], RecoveryTicks = (int)n[9], RecentEvents = events };
            return true;
        }
    }

    /// <summary>Persistent local social state, owned and checksummed by CharacterProgression.</summary>
    public sealed class SocialLedger
    {
        /// <summary>Maximum real camp stock: one canonical capacity for Core and Unity projection.</summary>
        public const int CampCapacity = 10;
        /// <summary>The established supplier NPC's canonical ID; not a separate social owner.</summary>
        public const int MaraNpcId = 1;
        private readonly NpcMemory[] _people = new NpcMemory[3];
        public BigInteger Provisions { get; private set; } = 3;
        public BigInteger Coins { get; private set; }
        // Only newly created first-region profiles begin with a real two-ration shortage.
        // Existing AC5 saves retain their exact stored camp stock without migration.
        public int CampProvisions { get; private set; } = 8;

        public SocialLedger()
        {
            _people[0] = new NpcMemory { Health = 600, Hunger = 500, Fatigue = 450, Purse = 6 };
            _people[1] = new NpcMemory { Hunger = 150, Fatigue = 100, Purse = 15 };
            _people[2] = new NpcMemory { Hunger = 100, Fatigue = 100, Purse = 10 };
        }

        public NpcMemory GetMemory(int id)
        {
            if (id < 0 || id >= _people.Length) throw new ArgumentOutOfRangeException(nameof(id));
            return _people[id];
        }

        internal bool Help(int id)
        {
            NpcMemory m = GetMemory(id);
            if (Provisions.IsZero || !m.NeedsHelp) return false;
            Provisions--;
            m.Hunger = Math.Max(0, m.Hunger - 400);
            m.Fatigue = Math.Max(0, m.Fatigue - 300);
            m.Health = Math.Min(NpcMemory.MaxHealth, m.Health + 120);
            m.RecoveryTicks = 0;
            m.Record(NpcAction.Help, 0);
            return true;
        }

        internal bool Rob(int id)
        {
            NpcMemory m = GetMemory(id);
            if (m.Purse == 0) return false;
            Coins += m.Purse; m.Purse = 0;
            m.Record(NpcAction.Rob, 2);
            return true;
        }

        /// <summary>One real player ration becomes one camp ration. Exact transaction and direct memory.</summary>
        internal bool DonateCampRation(int npcId)
        {
            if (npcId != MaraNpcId || Provisions.IsZero || CampProvisions >= CampCapacity) return false;
            Provisions--;
            CampProvisions++;
            GetMemory(MaraNpcId).Record(NpcAction.Help, 0);
            return true;
        }

        internal void ProvisionFromLoot() { Provisions++; }
        internal bool EatAtCamp(NpcMemory m)
        {
            if (CampProvisions == 0 || m.Hunger < 700) return false;
            CampProvisions--; m.Hunger = Math.Max(0, m.Hunger - 600);
            return true;
        }

        internal string Token()
        {
            return Provisions.ToString(CultureInfo.InvariantCulture) + "," +
                Coins.ToString(CultureInfo.InvariantCulture) + "," + CampProvisions.ToString(CultureInfo.InvariantCulture) +
                ";" + _people[0].Token() + ";" + _people[1].Token() + ";" + _people[2].Token();
        }

        internal static bool ReadNumber(string text, out BigInteger value)
        {
            return BigInteger.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) &&
                value >= 0 && value.ToString(CultureInfo.InvariantCulture) == text;
        }

        internal static bool TryRead(string text, out SocialLedger ledger)
        {
            ledger = null;
            string[] chunks = text.Split(';');
            if (chunks.Length != 4) return false;
            string[] header = chunks[0].Split(',');
            BigInteger provisions, coins, food;
            if (header.Length != 3 || !ReadNumber(header[0], out provisions) ||
                !ReadNumber(header[1], out coins) || !ReadNumber(header[2], out food) || food > 10) return false;
            var result = new SocialLedger { Provisions = provisions, Coins = coins, CampProvisions = (int)food };
            for (int i = 0; i < result._people.Length; i++)
                if (!NpcMemory.TryRead(chunks[i + 1], out result._people[i])) return false;
            ledger = result;
            return true;
        }
    }

    public sealed class NpcAgent
    {
        public int Id { get; private set; }
        public string Role { get { return Id == 0 ? "Abenteurer" : (Id == 1 ? "Versorger" : "Wache"); } }
        public NpcPersonality Personality { get { return (NpcPersonality)Id; } }
        public NpcMemory Memory { get; private set; }
        public long Xmm, Zmm, PreviousXmm, PreviousZmm;
        public int AttackCooldownTicks, InteractionCooldownTicks, PatrolIndex;
        public NpcBehavior Behavior;

        internal NpcAgent(int id, NpcMemory memory, long x, long z)
        {
            Id = id; Memory = memory; Xmm = PreviousXmm = x; Zmm = PreviousZmm = z;
        }

        public string Greeting()
        {
            NpcDisposition disposition = Memory.Disposition(Personality);
            if (disposition == NpcDisposition.Hostile) return "Bleib weg. Deine Taten sind nicht vergessen.";
            if (disposition == NpcDisposition.Wary) return "Ich erinnere mich an deinen Uebergriff. Halte Abstand.";
            if (disposition == NpcDisposition.Friendly) return "Da bist du wieder. Deinen Beistand vergesse ich nicht.";
            return Memory.Meetings > 1 ? "Wir sind uns schon begegnet. Was fuehrt dich her?" : "Sei gegruesst, Reisender.";
        }
    }
}

