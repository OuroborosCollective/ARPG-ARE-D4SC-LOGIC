using System;
using System.Globalization;
using System.Linq;

namespace AreArpg.Core
{
    public enum RoadFact { Rescue = 0, Death = 1, RefusedHelp = 2, CorpseTheft = 3 }

    /// <summary>Persistent state of the single authored road encounter. Its bear is the actual combat entity.</summary>
    public sealed class HunterEncounter
    {
        public const int BearId = 41;
        public const int BearMaximumHealth = 6500;
        public const long HunterX = 2000, HunterZ = 9000;
        public readonly MonsterState Bear = new MonsterState { Id = BearId, Kind = 4, Xmm = 1500, Zmm = 7000,
            PreviousXmm = 1500, PreviousZmm = 7000, Health = BearMaximumHealth, MaxHealth = BearMaximumHealth };
        public bool Started { get; internal set; }
        public int Age { get; internal set; }
        public int HunterHealth { get; internal set; } = 700;
        public int AggroTicks { get; internal set; }
        public int ArrowCooldown { get; internal set; }
        public int ArrowFlash { get; internal set; }
        public int PlayerDamage { get; internal set; }
        public int WitnessOpportunity { get; internal set; }
        public long StrikeX { get; internal set; }
        public long StrikeZ { get; internal set; }
        public bool StrikePlayer { get; internal set; }
        public int Purse { get; internal set; } = 12;
        public bool FavorClaimed { get; internal set; }
        public int TravelerRegion { get; internal set; } = 3;
        public long TravelerX { get; internal set; } = -4500;
        public long TravelerZ { get; internal set; } = 6500;
        public void RestartPlayer() { ResumePlayer = false; }
        public bool Rescued { get { return Started && Bear.IsDead && HunterHealth > 0 && PlayerDamage > 0; } }
        public bool ResumePlayer { get; internal set; }
        internal long SavedX, SavedZ;
        internal int SavedHealth, SavedResource;
        internal readonly int[] SavedCooldowns = new int[4];
        internal ulong SavedRng0, SavedRng1;

        internal string Token()
        {
            object[] f = { Started ? 1 : 0, Age, HunterHealth, AggroTicks, ArrowCooldown, ArrowFlash, PlayerDamage,
                WitnessOpportunity, StrikeX, StrikeZ, StrikePlayer ? 1 : 0, Purse, FavorClaimed ? 1 : 0,
                TravelerRegion, TravelerX, TravelerZ, Bear.Xmm, Bear.Zmm, Bear.Health, Bear.AttackCooldownTicks,
                Bear.BossWindupTicks, Bear.LootGranted ? 1 : 0, ResumePlayer ? 1 : 0, SavedX, SavedZ,
                SavedHealth, SavedResource, SavedCooldowns[0], SavedCooldowns[1], SavedCooldowns[2], SavedCooldowns[3],
                SavedRng0, SavedRng1 };
            return string.Join(",", f.Select(x => Convert.ToString(x, CultureInfo.InvariantCulture)));
        }

        internal static bool TryRead(string token, out HunterEncounter result)
        {
            result = null;
            string[] f = token.Split(','); if (f.Length != 33) return false;
            var n = new long[31];
            for (int i = 0; i < n.Length; i++)
                if (!long.TryParse(f[i], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out n[i]) ||
                    n[i].ToString(CultureInfo.InvariantCulture) != f[i]) return false;
            ulong r0, r1;
            if (!ulong.TryParse(f[31], NumberStyles.None, CultureInfo.InvariantCulture, out r0) ||
                !ulong.TryParse(f[32], NumberStyles.None, CultureInfo.InvariantCulture, out r1) ||
                r0.ToString(CultureInfo.InvariantCulture) != f[31] || r1.ToString(CultureInfo.InvariantCulture) != f[32]) return false;
            int[] boolean = { 0, 10, 12, 21, 22 };
            foreach (int i in boolean) if (n[i] < 0 || n[i] > 1) return false;
            int[] position = { 8, 9, 14, 15, 16, 17, 23, 24 };
            foreach (int i in position) if (n[i] < -27000 || n[i] > 27000) return false;
            for (int i = 0; i < n.Length; i++) if (!position.Contains(i) && (n[i] < 0 || n[i] > int.MaxValue)) return false;
            if (n[1] > 1000000 || n[2] > 700 || n[3] > 80 || n[4] > 12 || n[5] > 3 ||
                n[6] > BearMaximumHealth || n[7] > 30 || (n[11] != 0 && n[11] != 12) ||
                (n[13] != 2 && n[13] != 3) || n[18] > BearMaximumHealth || n[19] > 16 || n[20] > 8 ||
                n[26] > 100 || n[27] > 300 || n[28] > 300 || n[29] > 300 || n[30] > 300 ||
                (n[22] == 1 && (r0 | r1) == 0) || (n[18] == 0) != (n[21] == 1) ||
                (n[12] == 1 && (n[2] == 0 || n[18] != 0 || n[6] == 0 || n[11] != 0)) ||
                (n[11] == 0 && n[2] > 0 && n[12] == 0)) return false;
            var h = new HunterEncounter { Started = n[0] == 1, Age = (int)n[1], HunterHealth = (int)n[2],
                AggroTicks = (int)n[3], ArrowCooldown = (int)n[4], ArrowFlash = (int)n[5], PlayerDamage = (int)n[6],
                WitnessOpportunity = (int)n[7], StrikeX = n[8], StrikeZ = n[9], StrikePlayer = n[10] == 1,
                Purse = (int)n[11], FavorClaimed = n[12] == 1, TravelerRegion = (int)n[13], TravelerX = n[14], TravelerZ = n[15],
                ResumePlayer = n[22] == 1, SavedX = n[23], SavedZ = n[24], SavedHealth = (int)n[25], SavedResource = (int)n[26],
                SavedRng0 = r0, SavedRng1 = r1 };
            h.Bear.Xmm = h.Bear.PreviousXmm = n[16]; h.Bear.Zmm = h.Bear.PreviousZmm = n[17];
            h.Bear.Health = (int)n[18]; h.Bear.AttackCooldownTicks = (int)n[19]; h.Bear.BossWindupTicks = (int)n[20]; h.Bear.LootGranted = n[21] == 1;
            for (int i = 0; i < 4; i++) h.SavedCooldowns[i] = (int)n[27 + i];
            if (!h.Started && token != new HunterEncounter().Token()) return false;
            result = h; return true;
        }
    }

    public sealed partial class SocialLedger
    {
        // Four one-time causal facts, five knowers: village 0..2, hunter 3, road traveler 4.
        // 0 unknown; 1 witnessed/direct; 2..6 reported by source NPC 0..4. Reports never relay.
        private readonly int[] _roadFacts = new int[4];
        private readonly int[,] _roadKnowledge = new int[5, 4];
        public int RoadEventCount { get; private set; }
        public int RoadEventId(RoadFact fact) { return _roadFacts[(int)fact]; }
        public int RoadKnowledge(int npc, RoadFact fact) { return _roadKnowledge[npc, (int)fact]; }
        internal void RecordRoadFact(RoadFact fact, bool travelerWitness, bool hunterDirect = false)
        {
            int f = (int)fact; if (_roadFacts[f] != 0) return;
            _roadFacts[f] = ++RoadEventCount;
            if (travelerWitness) _roadKnowledge[4, f] = 1;
            if (hunterDirect) _roadKnowledge[3, f] = 1;
        }
        internal void ReportRoadFact(int source, int receiver)
        {
            // A real meeting transfers the most consequential missing fact, once; no rumor amplification.
            for (int f = 3; f >= 0; f--)
                if (_roadKnowledge[source, f] == 1 && _roadKnowledge[receiver, f] == 0)
                { _roadKnowledge[receiver, f] = source + 2; return; }
        }
        internal void ReceiveHunterCoins(HunterEncounter h)
        { Coins += h.Purse; h.Purse = 0; }
        public string RoadRumor(int npc)
        {
            if (_roadKnowledge[npc, 3] > 0) return "Der Reisende sah, wie du den toten Jaeger beraubt hast.";
            if (_roadKnowledge[npc, 2] > 0) return "Der Reisende sagt, du sahst den Angriff und hast nicht geholfen.";
            if (_roadKnowledge[npc, 0] > 0) return "Der Reisende erzaehlt, du haettest den Jaeger vor dem Baeren gerettet.";
            if (_roadKnowledge[npc, 1] > 0) return "Ein Jaeger wurde auf der Aschestrasse von einem Baeren getoetet.";
            return "";
        }
        internal string RoadSocialToken()
        {
            var f = new int[25]; f[0] = RoadEventCount;
            for (int j = 0; j < 4; j++) { f[1 + j] = _roadFacts[j]; for (int i = 0; i < 5; i++) f[5 + i * 4 + j] = _roadKnowledge[i, j]; }
            return string.Join(",", f.Select(x => x.ToString(CultureInfo.InvariantCulture)));
        }
        internal bool ReadRoadSocial(string text)
        {
            string[] f = text.Split(','); if (f.Length != 25) return false;
            var n = new int[25];
            for (int i = 0; i < n.Length; i++) if (!int.TryParse(f[i], NumberStyles.None, CultureInfo.InvariantCulture, out n[i]) ||
                n[i].ToString(CultureInfo.InvariantCulture) != f[i]) return false;
            if (n[0] > 4 || n[0] != n.Skip(1).Take(4).Count(x => x > 0)) return false;
            for (int id = 1; id <= n[0]; id++) if (n.Skip(1).Take(4).Count(x => x == id) != 1) return false;
            for (int j = 0; j < 4; j++)
            {
                if (n[1 + j] > n[0]) return false;
                for (int i = 0; i < 5; i++)
                {
                    int k = n[5 + i * 4 + j];
                    if (k > 6 || (n[1 + j] == 0 && k != 0) || (k >= 2 && (k - 2 == i || n[5 + (k - 2) * 4 + j] != 1))) return false;
                    _roadKnowledge[i, j] = k;
                }
                _roadFacts[j] = n[1 + j];
            }
            RoadEventCount = n[0]; return true;
        }
    }

    public sealed partial class CharacterProgression
    {
        public HunterEncounter Road { get; private set; } = new HunterEncounter();
    }

    public sealed partial class ArenaSimulation
    {
        private void InitializeRoadEncounter()
        {
            var h = Progression.Road;
            if (Progression.EntryAct != 3) return;
            Monsters.Add(h.Bear);
            if (!h.ResumePlayer) return;
            PlayerXmm = PrevPlayerXmm = h.SavedX; PlayerZmm = PrevPlayerZmm = h.SavedZ;
            PlayerHealth = Math.Min(MaxPlayerHealth, h.SavedHealth); Resource = h.SavedResource;
            for (int i = 0; i < 4; i++) Cooldowns[i] = h.SavedCooldowns[i];
            _rng.Restore(h.SavedRng0, h.SavedRng1);
        }
        private bool TravelerSees(long x, long z)
        {
            var h = Progression.Road;
            return h.TravelerRegion == 3 && DistanceSquared(h.TravelerX, h.TravelerZ, x, z) <= FixedMath.Square(11000);
        }
        private void FinishBear()
        {
            var h = Progression.Road; var bear = h.Bear;
            if (!bear.IsDead || bear.LootGranted) return;
            bear.LootGranted = true; bear.BossWindupTicks = 0;
            if (h.PlayerDamage > 0)
            {
                Kills++; Progression.RecordKill(); Progression.EarnExperience(1200);
                Progression.AddWeaponUpgrade(12); // One real upgrade/provision through the existing inventory owner.
            }
            if (h.Rescued) Progression.Social.RecordRoadFact(RoadFact.Rescue,
                TravelerSees(bear.Xmm, bear.Zmm) && TravelerSees(PlayerXmm, PlayerZmm), true);
        }
        private void SaveRoadPlayer()
        {
            var h = Progression.Road;
            if (Progression.EntryAct != 3 || !h.Started) return;
            h.ResumePlayer = true; h.SavedX = PlayerXmm; h.SavedZ = PlayerZmm;
            h.SavedHealth = PlayerHealth; h.SavedResource = Resource;
            for (int i = 0; i < 4; i++) h.SavedCooldowns[i] = Cooldowns[i];
            h.SavedRng0 = _rng.S0; h.SavedRng1 = _rng.S1;
        }
        private void StepRoadEncounter()
        {
            var h = Progression.Road; var bear = h.Bear;
            bool local = Progression.EntryAct == 3;
            if (!h.Started && local && !GameOver && DistanceSquared(PlayerXmm, PlayerZmm, HunterEncounter.HunterX, HunterEncounter.HunterZ) <= FixedMath.Square(10500)) h.Started = true;
            if (!h.Started) return;
            h.Age = h.Age >= 1000000 ? 100 : h.Age + 1;
            if (h.ArrowFlash > 0) h.ArrowFlash--;
            if (h.ArrowCooldown > 0) h.ArrowCooldown--;
            if (h.AggroTicks > 0) h.AggroTicks--;
            if (local && !GameOver && h.HunterHealth > 0 && !bear.IsDead &&
                TravelerSees(PlayerXmm, PlayerZmm) && TravelerSees(HunterEncounter.HunterX, HunterEncounter.HunterZ))
                h.WitnessOpportunity = Math.Min(30, h.WitnessOpportunity + 1);
            if (!bear.IsDead && h.Age > 30)
            {
                bool targetPlayer = local && !GameOver && h.AggroTicks > 0 && DistanceSquared(bear.Xmm, bear.Zmm, PlayerXmm, PlayerZmm) <= FixedMath.Square(12000);
                if (h.HunterHealth > 0 && targetPlayer && h.ArrowCooldown == 0 && DistanceSquared(bear.Xmm, bear.Zmm, HunterEncounter.HunterX, HunterEncounter.HunterZ) <= FixedMath.Square(16000))
                { bear.Health = Math.Max(0, bear.Health - 120); h.ArrowCooldown = 12; h.ArrowFlash = 3; FinishBear(); }
                if (!bear.IsDead && (targetPlayer || h.HunterHealth > 0))
                {
                    long tx = targetPlayer ? PlayerXmm : HunterEncounter.HunterX, tz = targetPlayer ? PlayerZmm : HunterEncounter.HunterZ;
                    if (bear.BossWindupTicks > 0)
                    {
                        bear.BossWindupTicks--;
                        if (bear.BossWindupTicks == 0)
                        {
                            if (h.StrikePlayer && local && !GameOver && DistanceSquared(PlayerXmm, PlayerZmm, h.StrikeX, h.StrikeZ) <= FixedMath.Square(2400)) PlayerHealth = Math.Max(0, PlayerHealth - 320);
                            if (!h.StrikePlayer && h.HunterHealth > 0)
                            {
                                h.HunterHealth = Math.Max(0, h.HunterHealth - 140);
                                if (h.HunterHealth == 0)
                                {
                                    bool saw = TravelerSees(HunterEncounter.HunterX, HunterEncounter.HunterZ);
                                    Progression.Social.RecordRoadFact(RoadFact.Death, saw);
                                    if (saw && h.WitnessOpportunity == 30 && h.PlayerDamage == 0 && !GameOver)
                                        Progression.Social.RecordRoadFact(RoadFact.RefusedHelp, true);
                                }
                            }
                            bear.AttackCooldownTicks = 16;
                        }
                    }
                    else if (bear.AttackCooldownTicks > 0) bear.AttackCooldownTicks--;
                    else
                    {
                        long dx = tx - bear.Xmm, dz = tz - bear.Zmm;
                        long distance = FixedMath.IntegerSqrt(dx * dx + dz * dz);
                        if (distance > 1900)
                        { long step = Math.Min(430, distance); bear.Xmm += dx * step / distance; bear.Zmm += dz * step / distance; }
                        else { bear.BossWindupTicks = 8; h.StrikeX = tx; h.StrikeZ = tz; h.StrikePlayer = targetPlayer; }
                    }
                }
            }
            // A living eyewitness physically walks home after the encounter. No broadcast to village memories.
            if (bear.IsDead || h.HunterHealth == 0)
            {
                long tx = h.TravelerRegion == 3 ? 0 : -5500, tz = h.TravelerRegion == 3 ? -24000 : -6000;
                long dx = tx - h.TravelerX, dz = tz - h.TravelerZ, distance = FixedMath.IntegerSqrt(dx * dx + dz * dz);
                if (distance > 200) { long step = Math.Min(170, distance); h.TravelerX += dx * step / distance; h.TravelerZ += dz * step / distance; }
                else if (h.TravelerRegion == 3) { h.TravelerRegion = 2; h.TravelerX = 0; h.TravelerZ = 21000; }
                if (h.TravelerRegion == 2 && IsVillage && h.Age % 100 == 0)
                    foreach (var npc in Npcs)
                        if (npc.Memory.Health > 0 && DistanceSquared(h.TravelerX, h.TravelerZ, npc.Xmm, npc.Zmm) <= FixedMath.Square(2400) && EntryWorldLayout.HasSight(h.TravelerX, h.TravelerZ, npc.Xmm, npc.Zmm)) Progression.Social.ReportRoadFact(4, npc.Id);
            }
            SaveRoadPlayer();
        }
        public SocialResult InteractHunter(bool robCorpse)
        {
            var h = Progression.Road; var r = new SocialResult();
            if (Progression.EntryAct != 3 || GameOver || DistanceSquared(PlayerXmm, PlayerZmm, HunterEncounter.HunterX, HunterEncounter.HunterZ) > FixedMath.Square(2400)) return r;
            if (robCorpse)
            {
                if (h.HunterHealth > 0 || h.Purse == 0) return r;
                Progression.Social.ReceiveHunterCoins(h);
                Progression.Social.RecordRoadFact(RoadFact.CorpseTheft, TravelerSees(PlayerXmm, PlayerZmm));
                r.Text = "Du nimmst die 12 Muenzen aus dem Beutel des toten Jaegers.";
            }
            else
            {
                if (!h.Rescued || h.FavorClaimed) return r;
                h.FavorClaimed = true; Progression.Social.ReceiveHunterCoins(h);
                r.SpeakerId = 3; r.Speech = SpeechLine.HunterThanks;
                r.Text = "Jaeger: Du hast mir das Leben gerettet. Nimm meinen Beutel. Das vergesse ich dir nicht.";
            }
            r.Accepted = true; return r;
        }
    }
}
