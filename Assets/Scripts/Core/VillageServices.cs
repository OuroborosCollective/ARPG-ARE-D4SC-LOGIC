using System;
using System.Globalization;
using System.Text;

namespace AreArpg.Core
{
    /// <summary>Story-NPCs whose quest must be finished before they may die.</summary>
    public enum StoryNpc { Ilyan = 0, Mara = 1, Dorn = 2, Orsel = 3 }

    /// <summary>
    /// Mill healer (Orsel) and village services: Orsel teaches first aid, a buffet meal and a healing spell only
    /// after the mill quest; her half-brother Orkel goes missing for honey in a cave and can be hired once returned.
    /// Also holds the story-NPC death rule and the per-village population growth. Own AC22 section "village".
    /// </summary>
    public sealed class VillageServicesState
    {
        public bool OrselTeachesUnlocked { get; private set; }
        public bool LearnedFirstAid { get; private set; }
        public bool LearnedHealSpell { get; private set; }
        public bool LearnedBuffet { get; private set; }
        public int HealSpellCooldownTicks { get; private set; }
        public int BuffetTicks { get; private set; }
        /// <summary>Orkel quest: 0 not offered, 1 offered, 2 in the cave, 3 found fleeing, 4 found with honey, 5 returned.</summary>
        public int OrkelQuestStage { get; private set; }
        public bool OrkelRecruited { get; private set; }
        public int Population { get; private set; } = 3;
        public int MainQuestsCompleted { get; private set; }

        public const int BasePopulation = 3;
        public const int HealSpellCooldown = 300;   // 30 s
        public const int HealSpellAmount = 500;
        public const int BuffetDurationTicks = 1800;         // 3 min
        public const int BuffetBonusBp = 1000;       // +10 %

        /// <summary>The mill quest is finished once the shelter beds are inspected at the old mill (act 4, stage 4).</summary>
        public static bool MillQuestCompleted(CharacterProgression p)
        {
            return p != null && p.EntryAct >= 4 && p.HungerStage >= 4;
        }

        /// <summary>Unlock Orsel's teachings; only after the mill quest and only once.</summary>
        internal bool UnlockOrselTeaches(CharacterProgression p)
        {
            if (!MillQuestCompleted(p) || OrselTeachesUnlocked) return false;
            OrselTeachesUnlocked = true;
            return true;
        }

        public bool LearnFirstAid() { if (!OrselTeachesUnlocked || LearnedFirstAid) return false; LearnedFirstAid = true; return true; }
        public bool LearnHealSpell() { if (!OrselTeachesUnlocked || LearnedHealSpell) return false; LearnedHealSpell = true; return true; }
        public bool LearnBuffet() { if (!OrselTeachesUnlocked || LearnedBuffet) return false; LearnedBuffet = true; return true; }

        public int BuffetAttackBp { get { return BuffetTicks > 0 ? BuffetBonusBp : 0; } }
        public int BuffetDefenseBp { get { return BuffetTicks > 0 ? BuffetBonusBp : 0; } }

        internal void AdvanceTick()
        {
            if (BuffetTicks > 0) BuffetTicks--;
            if (HealSpellCooldownTicks > 0) HealSpellCooldownTicks--;
        }

        internal void SetHealSpellCooldown() { HealSpellCooldownTicks = HealSpellCooldown; }
        internal void StartBuffet() { BuffetTicks = BuffetDurationTicks; }

        /// <summary>Start Orkel's side quest once Orsel trusts the player (teachings unlocked).</summary>
        internal bool OfferOrkelQuest()
        {
            if (!OrselTeachesUnlocked || OrkelQuestStage != 0) return false;
            OrkelQuestStage = 1;
            return true;
        }

        internal bool EnterOrkelCave() { if (OrkelQuestStage != 1) return false; OrkelQuestStage = 2; return true; }
        internal bool ResolveOrkelFind(bool honey) { if (OrkelQuestStage != 2) return false; OrkelQuestStage = honey ? 4 : 3; return true; }

        /// <summary>Return Orkel to Orsel; the player may then hire him.</summary>
        internal bool ReturnOrkelToOrsel()
        {
            if (OrkelQuestStage != 3 && OrkelQuestStage != 4) return false;
            OrkelQuestStage = 5;
            return true;
        }

        internal bool RecruitOrkel()
        {
            if (OrkelQuestStage != 5 || OrkelRecruited) return false;
            OrkelRecruited = true;
            return true;
        }

        /// <summary>A completed main quest grows the village by one new resident.</summary>
        internal void RecordMainQuestCompleted() { MainQuestsCompleted++; Population = BasePopulation + MainQuestsCompleted; }

        public bool HasProgress
        {
            get
            {
                return OrselTeachesUnlocked || LearnedFirstAid || LearnedHealSpell || LearnedBuffet ||
                    OrkelQuestStage != 0 || OrkelRecruited || MainQuestsCompleted != 0;
            }
        }

        internal string Token()
        {
            var b = new StringBuilder("1");
            b.Append(',').Append(OrselTeachesUnlocked ? "1" : "0").Append(',').Append(LearnedFirstAid ? "1" : "0")
                .Append(',').Append(LearnedHealSpell ? "1" : "0").Append(',').Append(LearnedBuffet ? "1" : "0")
                .Append(',').Append(HealSpellCooldownTicks.ToString(CultureInfo.InvariantCulture))
                .Append(',').Append(BuffetTicks.ToString(CultureInfo.InvariantCulture))
                .Append(',').Append(OrkelQuestStage.ToString(CultureInfo.InvariantCulture))
                .Append(',').Append(OrkelRecruited ? "1" : "0")
                .Append(',').Append(Population.ToString(CultureInfo.InvariantCulture))
                .Append(',').Append(MainQuestsCompleted.ToString(CultureInfo.InvariantCulture));
            return b.ToString();
        }

        private static bool Flag(string s) { return s == "0" || s == "1"; }
        private static bool Count(string s, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(s) || s.Length > 9 || (s.Length > 1 && s[0] == '0')) return false;
            foreach (char c in s) if (c < '0' || c > '9') return false;
            return int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        internal static bool TryRead(string token, out VillageServicesState state)
        {
            state = null;
            if (string.IsNullOrEmpty(token)) return false;
            string[] f = token.Split(',');
            if (f.Length != 11 || f[0] != "1") return false;
            if (!Flag(f[1]) || !Flag(f[2]) || !Flag(f[3]) || !Flag(f[4]) || !Flag(f[8])) return false;
            int cd, buffet, stage, pop, quests;
            if (!Count(f[5], out cd) || !Count(f[6], out buffet) || !Count(f[7], out stage) ||
                !Count(f[9], out pop) || !Count(f[10], out quests)) return false;
            if (stage > 5 || pop < BasePopulation || pop != BasePopulation + quests) return false;
            if (f[2] == "1" && f[1] != "1" || f[3] == "1" && f[1] != "1" || f[4] == "1" && f[1] != "1") return false;
            if (f[8] == "1" && stage != 5) return false;
            state = new VillageServicesState
            {
                OrselTeachesUnlocked = f[1] == "1", LearnedFirstAid = f[2] == "1", LearnedHealSpell = f[3] == "1",
                LearnedBuffet = f[4] == "1", HealSpellCooldownTicks = cd, BuffetTicks = buffet,
                OrkelQuestStage = stage, OrkelRecruited = f[8] == "1", Population = pop, MainQuestsCompleted = quests
            };
            return true;
        }
    }

    public sealed partial class CharacterProgression
    {
        private VillageServicesState _village = new VillageServicesState();
        public VillageServicesState Village { get { return _village; } }
        private const string VillageSectionName = "village";

        private void SyncVillageSection()
        {
            if (_village.HasProgress) SetSection(VillageSectionName, _village.Token());
            else _sections.Remove(VillageSectionName);
        }

        private void RestoreVillageSection()
        {
            string token;
            if (_sections.TryGetValue(VillageSectionName, out token) && VillageServicesState.TryRead(token, out var state))
                _village = state;
        }
    }

    public sealed partial class ArenaSimulation
    {
        /// <summary>A story-NPC may only die once its own quest is finished; otherwise the quest stays completable.</summary>
        public static bool StoryNpcCanDie(StoryNpc npc, CharacterProgression p)
        {
            switch (npc)
            {
                // The village arc and Orsel's mill quest are complete at act 4, stage 4.
                case StoryNpc.Ilyan:
                case StoryNpc.Mara:
                case StoryNpc.Dorn:
                case StoryNpc.Orsel:
                    return VillageServicesState.MillQuestCompleted(p);
                default:
                    return false;
            }
        }

        /// <summary>Orsel teaches the three mill lessons after the mill quest, once.</summary>
        public SocialResult OrselTeach(int lesson)
        {
            var r = new SocialResult { SpeakerId = 3 };
            if (GameOver || !Progression.Village.OrselTeachesUnlocked) { r.Text = "Orsel: Erst muss die Mühle versorgt sein."; return r; }
            bool ok = lesson == 0 ? Progression.Village.LearnFirstAid() : lesson == 1 ? Progression.Village.LearnHealSpell() : Progression.Village.LearnBuffet();
            r.Accepted = ok;
            r.Text = ok ? "Orsel: Merk es dir gut." : "Orsel: Das habe ich dir schon gezeigt.";
            return r;
        }

        /// <summary>Cast the learned healing spell; heals the player and starts the cooldown.</summary>
        public bool CastHealSpell()
        {
            if (GameOver || !Progression.Village.LearnedHealSpell || Progression.Village.HealSpellCooldownTicks > 0) return false;
            PlayerHealth = Math.Min(MaxPlayerHealth, PlayerHealth + VillageServicesState.HealSpellAmount);
            Progression.Village.SetHealSpellCooldown();
            return true;
        }

        /// <summary>Eat the learned buffet; a short attack-and-defence buff.</summary>
        public bool EatBuffet()
        {
            if (GameOver || !Progression.Village.LearnedBuffet || Progression.Village.BuffetTicks > 0) return false;
            Progression.Village.StartBuffet();
            return true;
        }
    }
}
