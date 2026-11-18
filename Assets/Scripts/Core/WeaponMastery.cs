using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace AreArpg.Core
{
    /// <summary>
    /// Revision 1 of the RuneScape-style weapon-type skills (owner order 2026-11-17). Each droppable attacking
    /// weapon type has its own level 1-99 on the classic experience table. Effective damage dealt with an equipped
    /// weapon of that type trains it at the existing practice rate. Every fifth level grants one freely spendable
    /// stat point and +2.5 % damage with that weapon type. Pure integer rules; no wall clock, RNG or floats.
    /// </summary>
    public static class WeaponMasteryRules
    {
        public const int Revision = 1;
        public const int MaxLevel = 99;
        public const int LevelsPerStatPoint = 5;
        public const int DamageBonusBpPerStep = 250;
        public const long MaxExperience = 200000000L;
        /// <summary>Persisted in WeaponFamily enum order, including families that have no skill yet.</summary>
        public const int FamilyCount = 8;

        // Classic table: floor(sum(floor(l + 300 * 2^(l/7))) / 4); index = level. Frozen for revision 1.
        private static readonly long[] Table = { 0,0,83,174,276,388,512,650,801,969,1154,1358,1584,1833,2107,2411,2746,3115,3523,3973,4470,5018,5624,6291,7028,7842,8740,9730,10824,12031,13363,14833,16456,18247,20224,22406,24815,27473,30408,33648,37224,41171,45529,50339,55649,61512,67983,75127,83014,91721,101333,111945,123660,136594,150872,166636,184040,203254,224466,247886,273742,302288,333804,368599,407015,449428,496254,547953,605032,668051,737627,814445,899257,992895,1096278,1210421,1336443,1475581,1629200,1798808,1986068,2192818,2421087,2673114,2951373,3258594,3597792,3972294,4385776,4842295,5346332,5902831,6517253,7195629,7944614,8771558,9684577,10692629,11805606,13034431 };
        private static readonly WeaponFamily[] Trained =
            { WeaponFamily.Sword, WeaponFamily.Dagger, WeaponFamily.Axe, WeaponFamily.Staff, WeaponFamily.Bow, WeaponFamily.Shield };

        /// <summary>Every droppable weapon type. The shield never strikes; its skill raises the block chance instead.</summary>
        public static IReadOnlyList<WeaponFamily> TrainedFamilies => Trained;
        public static bool Trains(WeaponFamily family) => Array.IndexOf(Trained, family) >= 0;

        public static long ExperienceForLevel(int level)
        {
            if (level < 1 || level > MaxLevel) throw new ArgumentOutOfRangeException(nameof(level));
            return Table[level];
        }

        public static int LevelForExperience(long experience)
        {
            if (experience < 0) throw new ArgumentOutOfRangeException(nameof(experience));
            int level = 1;
            while (level < MaxLevel && Table[level + 1] <= experience) level++;
            return level;
        }

        public static int StatPoints(int level)
        {
            if (level < 1 || level > MaxLevel) throw new ArgumentOutOfRangeException(nameof(level));
            return level / LevelsPerStatPoint;
        }

        public static int DamageBonusBp(int level) => StatPoints(level) * DamageBonusBpPerStep;
        public static int DamageBonusBp(WeaponFamily family, int level) => family == WeaponFamily.Shield ? 0 : DamageBonusBp(level);

        public static string SkillName(WeaponFamily family)
        {
            switch (family)
            {
                case WeaponFamily.Sword: return "Schwertkunst";
                case WeaponFamily.Dagger: return "Dolchkunst";
                case WeaponFamily.Axe: return "Axtkunst";
                case WeaponFamily.Staff: return "Stabkunst";
                case WeaponFamily.Bow: return "Bogenkunst";
                case WeaponFamily.Shield: return "Schildkunst";
                default: throw new ArgumentOutOfRangeException(nameof(family));
            }
        }
    }

    /// <summary>Per-type experience plus the stat ranks bought with weapon points; owned by CharacterProgression.</summary>
    public sealed class WeaponMasteryState
    {
        private readonly long[] _experience = new long[WeaponMasteryRules.FamilyCount];
        private readonly int[] _bonusRanks = new int[3];

        public long Experience(WeaponFamily family) => _experience[(int)family];
        public int Level(WeaponFamily family) => WeaponMasteryRules.LevelForExperience(_experience[(int)family]);
        public long ExperienceToNextLevel(WeaponFamily family)
        {
            int level = Level(family);
            return level >= WeaponMasteryRules.MaxLevel ? 0 : WeaponMasteryRules.ExperienceForLevel(level + 1) - _experience[(int)family];
        }
        public int StatPointsEarned
        {
            get { int points = 0; foreach (var family in WeaponMasteryRules.TrainedFamilies) points += WeaponMasteryRules.StatPoints(Level(family)); return points; }
        }
        public int StatPointsSpent => _bonusRanks[0] + _bonusRanks[1] + _bonusRanks[2];
        public int StatPointsUnspent => StatPointsEarned - StatPointsSpent;
        public int BonusRank(MasteryPath path) => _bonusRanks[(int)path];
        public bool HasProgress { get { foreach (long xp in _experience) if (xp > 0) return true; return false; } }

        /// <summary>Add training experience, saturating at the cap. Returns the number of levels gained.</summary>
        internal int Record(WeaponFamily family, long amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0 || !WeaponMasteryRules.Trains(family)) return 0;
            int index = (int)family, before = Level(family);
            long current = _experience[index];
            _experience[index] = amount > WeaponMasteryRules.MaxExperience - current ? WeaponMasteryRules.MaxExperience : current + amount;
            return Level(family) - before;
        }

        internal bool TrySpend(MasteryPath path)
        {
            if (StatPointsUnspent <= 0 || (int)path < 0 || (int)path > 2) return false;
            _bonusRanks[(int)path]++;
            return true;
        }

        internal string Token()
        {
            var b = new StringBuilder("1");
            foreach (long xp in _experience) b.Append(',').Append(xp.ToString(CultureInfo.InvariantCulture));
            foreach (int rank in _bonusRanks) b.Append(',').Append(rank.ToString(CultureInfo.InvariantCulture));
            return b.ToString();
        }

        private static bool Canonical(string text, long max, out long value)
        {
            value = 0;
            if (string.IsNullOrEmpty(text) || text.Length > 12 || (text.Length > 1 && text[0] == '0')) return false;
            foreach (char c in text) if (c < '0' || c > '9') return false;
            return long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value <= max;
        }

        /// <summary>Reject unknown revisions, non-canonical numbers, experience above the cap, progress on types
        /// without a skill, an empty wrapper and more spent ranks than the levels earned.</summary>
        internal static bool TryRead(string token, out WeaponMasteryState state)
        {
            state = null;
            if (token == null) return false;
            string[] f = token.Split(',');
            if (f.Length != 1 + WeaponMasteryRules.FamilyCount + 3 || f[0] != "1") return false;
            var s = new WeaponMasteryState();
            for (int i = 0; i < WeaponMasteryRules.FamilyCount; i++)
            {
                if (!Canonical(f[1 + i], WeaponMasteryRules.MaxExperience, out long xp)) return false;
                if (xp > 0 && !WeaponMasteryRules.Trains((WeaponFamily)i)) return false;
                s._experience[i] = xp;
            }
            long spent = 0; // summed as long: three int ranks must never wrap around to a small total
            for (int i = 0; i < 3; i++)
            {
                if (!Canonical(f[1 + WeaponMasteryRules.FamilyCount + i], int.MaxValue, out long rank)) return false;
                s._bonusRanks[i] = (int)rank;
                spent += rank;
            }
            if (!s.HasProgress || spent > s.StatPointsEarned) return false;
            state = s;
            return true;
        }
    }

    public sealed partial class CharacterProgression
    {
        public WeaponMasteryState WeaponMastery { get; private set; } = new WeaponMasteryState();

        /// <summary>The weapon type that the next effective hit trains; null when unarmed.</summary>
        public WeaponFamily? TrainingFamily
        {
            get
            {
                var weapon = Equipment.Weapon;
                return weapon != null && WeaponMasteryRules.Trains(weapon.Definition.Family) ? weapon.Definition.Family : (WeaponFamily?)null;
            }
        }

        public int WeaponMasteryDamageBonusBp
        {
            get { var family = TrainingFamily; return family.HasValue ? WeaponMasteryRules.DamageBonusBp(family.Value, WeaponMastery.Level(family.Value)) : 0; }
        }

        public BigInteger TotalMightRank => MightRank + WeaponMastery.BonusRank(MasteryPath.Might);
        public BigInteger TotalVitalityRank => VitalityRank + WeaponMastery.BonusRank(MasteryPath.Vitality);
        public BigInteger TotalResolveRank => ResolveRank + WeaponMastery.BonusRank(MasteryPath.Resolve);
        /// <summary>Points from character levels only; weapon points are tracked by WeaponMastery.</summary>
        public BigInteger UnspentLevelPoints => Level - BigInteger.One - SpentMasteryPoints;

        /// <summary>Called only by the canonical player damage path with the health actually removed.</summary>
        internal int RecordWeaponPractice(int appliedDamage)
        {
            var family = TrainingFamily;
            if (!family.HasValue || appliedDamage <= 0) return 0;
            return WeaponMastery.Record(family.Value, (long)PracticeRules.ExperienceFromEffectiveAmount(appliedDamage));
        }

        /// <summary>True only while an AC21 checkpoint restores its inner checkpoint: the weapon ranks already count for
        /// every saved-health check there, while the inner round-trip checks still compare the inner payload.</summary>
        private bool _restoringWeaponMastery;

        /// <summary>AC21 wraps the complete previous checkpoint only once weapon skills have progress.</summary>
        private string WeaponMasteryPayload()
        {
            string previous = Payload();
            if (!WeaponMastery.HasProgress || _restoringWeaponMastery) return previous;
            string inner = previous + "|" + HashText(previous).ToString("X16", CultureInfo.InvariantCulture);
            return "AC21|" + Convert.ToBase64String(Encoding.UTF8.GetBytes(inner)) + "|" + WeaponMastery.Token();
        }

        /// <summary>True when a checkpoint may hold journey progress, also inside an AC21 weapon-skill wrapper. Decides which
        /// backup may replace a primary that fails to load. Only an AC21 whose inner part positively starts with a pre-journey
        /// version (AC1 to AC19) counts as journey-free; an unreadable or damaged inner prefix counts as journey progress, so
        /// recovery never picks an older backup over it. A truncated tail keeps the prefix and stays recoverable.</summary>
        public static bool MayHoldJourneyProgress(string saved)
        {
            if (string.IsNullOrEmpty(saved)) return false;
            if (saved.StartsWith("AC20|", StringComparison.Ordinal)) return true;
            if (!saved.StartsWith("AC21|", StringComparison.Ordinal)) return false;
            int end = saved.IndexOf('|', 5);
            string inner = end < 0 ? saved.Substring(5) : saved.Substring(5, end - 5);
            // Eight base64 characters hold the first six bytes: enough for an "AC19|" prefix, even after truncation.
            if (inner.Length < 8) return true;
            string head;
            try { head = Encoding.ASCII.GetString(Convert.FromBase64String(inner.Substring(0, 8))); }
            catch (FormatException) { return true; }
            int bar = head.IndexOf('|');
            if (!head.StartsWith("AC", StringComparison.Ordinal) || bar < 3 || bar > 4 || head[2] == '0') return true;
            for (int i = 2; i < bar; i++) if (head[i] < '0' || head[i] > '9') return true;
            int version = int.Parse(head.Substring(2, bar - 2), NumberStyles.None, CultureInfo.InvariantCulture);
            return version < 1 || version > 19;
        }

        private static bool TryLoadWeaponMastery(string saved, JourneyMapCatalog maps, out CharacterProgression progress)
        {
            progress = null;
            if (saved.Length > 64 * 1024 * 1024) return false;
            string[] f = saved.Split('|');
            if (f.Length != 4 || f[3] != HashText(string.Join("|", f, 0, 3)).ToString("X16", CultureInfo.InvariantCulture)) return false;
            try
            {
                byte[] bytes = Convert.FromBase64String(f[1]);
                if (Convert.ToBase64String(bytes) != f[1]) return false;
                string inner = new UTF8Encoding(false, true).GetString(bytes);
                // Read the weapon ranks first: a Vitality rank bought with weapon points raises the maximum health
                // that the inner checkpoint's saved-health checks compare against.
                if (inner.StartsWith("AC21|", StringComparison.Ordinal) || !WeaponMasteryState.TryRead(f[2], out var mastery) ||
                    !TryLoadJourney(inner, maps, mastery, out var p)) return false;
                p._restoringWeaponMastery = false;
                if (p.SaveString() != saved) return false;
                progress = p;
                return true;
            }
            catch (Exception e) when (e is FormatException || e is ArgumentException || e is OverflowException || e is InvalidOperationException) { return false; }
        }
    }
}
