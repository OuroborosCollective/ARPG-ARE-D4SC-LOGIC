using System;
using System.Collections.Generic;

namespace AreArpg.Core
{
    // This file has zero Unity dependencies so the same implementation is exercised in CI.
    public sealed class DeterministicRng
    {
        public ulong S0 { get; private set; }
        public ulong S1 { get; private set; }

        public DeterministicRng(ulong seed)
        {
            ulong x = seed;
            S0 = SplitMix64(ref x);
            S1 = SplitMix64(ref x);
            if (S0 == 0 && S1 == 0) S0 = 1;
        }

        private static ulong SplitMix64(ref ulong x)
        {
            unchecked
            {
                ulong z = (x += 0x9E3779B97F4A7C15UL);
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        internal void Restore(ulong a, ulong b)
        { if ((a | b) == 0) throw new ArgumentException("Empty RNG state"); S0 = a; S1 = b; }

        public ulong Next64()
        {
            unchecked
            {
                ulong a = S0;
                ulong b = S1;
                S0 = b;
                a ^= a << 23;
                S1 = a ^ b ^ (a >> 18) ^ (b >> 5);
                return S1 + b;
            }
        }

        public int NextBasisPoints() { return (int)(Next64() % 10000UL); }
    }

    public static class FixedMath
    {
        public const long BasisPoints = 10000L;
        public static long Clamp(long n, long min, long max) { return n < min ? min : (n > max ? max : n); }
        public static int ClampInt(int n, int min, int max) { return n < min ? min : (n > max ? max : n); }
        public static long Square(long n) { return checked(n * n); }

        // Binary search avoids Newton overflow at long.MaxValue.
        public static long IntegerSqrt(long n)
        {
            if (n <= 0) return 0;
            long lo = 1;
            long hi = Math.Min(n, 3037000499L);
            long answer = 0;
            while (lo <= hi)
            {
                long mid = lo + (hi - lo) / 2;
                if (mid <= n / mid) { answer = mid; lo = mid + 1; }
                else hi = mid - 1;
            }
            return answer;
        }

        public static long Damage(long weapon, int powerBp, long mainStat, int additiveBp,
            long armor, int critChanceBp, DeterministicRng rng, out bool crit)
        {
            crit = rng.NextBasisPoints() < ClampInt(critChanceBp, 0, 10000);
            long damage = checked(weapon * powerBp) / BasisPoints;
            damage = checked(damage * (BasisPoints + mainStat * 10)) / BasisPoints;
            damage = checked(damage * (BasisPoints + additiveBp)) / BasisPoints;
            if (crit) damage = checked(damage * 15000) / BasisPoints;
            damage = checked(damage * BasisPoints) / (BasisPoints + Math.Max(0, armor));
            return Math.Max(1, damage);
        }
    }

    public sealed class MonsterState
    {
        public int Id;
        public int Kind; // 0: ghoul, 1: knight, 2: boss
        public long Xmm;
        public long Zmm;
        public long PreviousXmm;
        public long PreviousZmm;
        public int Health;
        public int MaxHealth;
        public int AttackCooldownTicks;
        public bool LootGranted;
        // The ember-warden's countdown is part of the authoritative replay state.
        public int BossWindupTicks;
        public int BossPulseCooldownTicks;
        public bool IsDead { get { return Health <= 0; } }
    }

    public sealed class LootState
    {
        public LootItem Item { get; internal set; }
        public int Id;
        public long Xmm;
        public long Zmm;
        public int WeaponBonus;
    }

    public sealed class SkillResult
    {
        public bool Activated;
        public int TotalDamage;
        public int Hits;
        public int Kills;
        public string Message = "";
    }

    /// <summary>Standalone single-player simulation. Not a network server or a clone of any proprietary engine.</summary>
    public sealed partial class ArenaSimulation
    {
        public const int TickMs = 100;
        public const long ArenaLimitMm = 27000L;
        public const long PlayerSpeedMmPerTick = 620L;
        public const long MonsterSpeedMmPerTick = 260L;
        public const long PlayerRadiusMm = 450L;
        public const long PickupRadiusMm = 1600L;
        public const long PassageXmm = 0L;
        public const long PassageZmm = 24000L;
        public const long PassageRadiusMm = 1800L;
        public const long CampXmm = -4500L;
        public const long CampZmm = -6000L;
        public const long NpcInteractionRadiusMm = 2400L;
        public bool AtWorldPassage { get { return !InWoodlandRegion && DistanceSquared(PlayerXmm, PlayerZmm, PassageXmm, PassageZmm) <= FixedMath.Square(PassageRadiusMm); } }

        /// <summary>Use the existing passage gate and carry surviving player resources into the next approved world.</summary>
        public ArenaSimulation TryTraversePassage()
        {
            if (AtWoodlandPassage) return TraverseWoodland();
            if (!AtAnyPassage || GameOver) return null;
            ArenaSimulation next = Progression.EntryAct >= 2 ? TraverseHunger(AtReturnPassage) : TryAdvanceCrypt();
            if (next == null) return null;
            next.PlayerHealth = Math.Min(PlayerHealth, next.MaxPlayerHealth);
            next.Resource = Resource;
            for (int i = 0; i < Cooldowns.Length; i++) next.Cooldowns[i] = Cooldowns[i];
            next.PlayerZmm = next.PrevPlayerZmm = next.Progression.HungerStage > 0 ?
                (next.Progression.EntryFromNorth ? 21000L : -21000L) : -23000L;
            next.SaveRoadPlayer(); next.SaveInvestigationPlayer(); next.SaveDungeonPlayer();
            return next;
        }

        private readonly DeterministicRng _rng;
        private readonly ulong _runSeed;
        private readonly LootCatalog _lootCatalog;
        public System.Numerics.BigInteger CryptDepth { get; private set; }
        public int ChapterId { get { return CryptDepth.IsOne ? 1 : 2; } }
        public string ChapterName { get { return InWoodlandRegion ? (InForest ? "Der Wald nach Aschenzoll" : Progression.EntryAct == 9 ? "Die Bärenhöhle" : "Aschenzoll – Frachtplatz") : InDungeon ? "Krypta unter der Muehle - Ebene " + Progression.Dungeon.Depth : Progression.EntryAct == 7 ? "Das Rattlernest" : Progression.EntryAct == 6 ? "Die verborgene Krypta" : Progression.EntryAct == 5 ? "Der alte Speicher" : Progression.EntryAct == 4 ? "Die alte Muehle" : Progression.EntryAct == 3 ? "Die Aschestrasse" : Progression.EntryAct == 2 ? "Die Aschengrenze" : (ChapterId == 1 ? "Ashen Crypt" : "Ember Rift"); } }
        /// <summary>Legacy chapter one or the explicit entry's second act hosts the existing social scene.</summary>
        public bool IsVillage { get { return Progression.EntryAct == 2 || (Progression.EntryAct == 0 && ChapterId == 1); } }
        public bool CryptCleared { get { return !GameOver && Monsters.TrueForAll(m => m.IsDead); } }
        private long _directionXbp;
        private long _directionZbp;
        private int _nextLootId = 1;
        public ulong CurrentTick { get; private set; }
        public long PlayerXmm { get; private set; }
        public long PlayerZmm { get; private set; }
        public long PrevPlayerXmm { get; private set; }
        public long PrevPlayerZmm { get; private set; }
        public int PlayerHealth { get; private set; }
        public int MaxPlayerHealth { get { return CombatRules.MaximumHealth(Progression); } }
        public int Resource { get; private set; } = 100;
        public CharacterProgression Progression { get; private set; }
        public int WeaponBonus { get { return Progression.WeaponBonus; } }
        public int Kills { get; private set; }
        public System.Numerics.BigInteger LootCollected { get { return Progression.LootCollected; } }
        public bool GameOver { get { return PlayerHealth == 0; } }
        public readonly List<MonsterState> Monsters = new List<MonsterState>();
        public readonly List<LootState> GroundLoot = new List<LootState>();
        public readonly List<NpcAgent> Npcs = new List<NpcAgent>();
        public readonly int[] Cooldowns = new int[4];

        public ArenaSimulation(ulong seed, CharacterProgression progression = null)
            : this(seed, progression, System.Numerics.BigInteger.One) { }

        public ArenaSimulation(ulong seed, CharacterProgression progression,
            System.Numerics.BigInteger cryptDepth)
            : this(seed, progression, cryptDepth, null) { }

        /// <summary>Explicit versioned loot opt-in preserves the legacy simulation and hash contract.</summary>
        public ArenaSimulation(ulong seed, CharacterProgression progression,
            System.Numerics.BigInteger cryptDepth, LootCatalog lootCatalog)
        {
            if (cryptDepth < System.Numerics.BigInteger.One)
                throw new ArgumentOutOfRangeException(nameof(cryptDepth));
            _runSeed = seed;
            _lootCatalog = lootCatalog;
            CryptDepth = cryptDepth;
            Progression = progression ?? new CharacterProgression();
            if (Progression.EntryAct > 0 && !cryptDepth.IsOne)
                throw new ArgumentOutOfRangeException(nameof(cryptDepth), "Entry revision 1 uses its own two-act checkpoint.");
            PlayerHealth = MaxPlayerHealth;
            _rng = new DeterministicRng(InForest ? Progression.Woodland.Seed : InDungeon ? Progression.Dungeon.Seed : seed);
            if (IsVillage)
            {
                Npcs.Add(new NpcAgent(0, Progression.Social.GetMemory(0), -3600, -3400));
                Npcs.Add(new NpcAgent(1, Progression.Social.GetMemory(1), -5500, -6000));
                Npcs.Add(new NpcAgent(2, Progression.Social.GetMemory(2), -6500, -3500));
            }
            else if (Progression.EntryAct == 0) Npcs.Add(new NpcAgent(0, Progression.Social.GetMemory(0), -2000, -21000));
            if (Progression.EntryAct >= 2)
            {
                PlayerZmm = PrevPlayerZmm = Progression.HungerStage > 0 ? (Progression.EntryFromNorth ? 21000L : -21000L) : -23000L;
                if (InWoodlandRegion) { InitializeWoodland(); BindForge(); return; }
                InitializeRoadEncounter();
                InitializeInvestigation();
                if (InDungeon)
                {
                    CurrentTick = Progression.Dungeon.Tick;
                    GroundLoot = Progression.Dungeon.Current.Loot;
                    Monsters.AddRange(Progression.Dungeon.Current.Monsters);
                }
                if (Progression.EntryAct == 7) Monsters.Add(Progression.Rattler.Creature);
                BindForge();
                return;
            }
            // Fixed bounds preserve finite combat arithmetic even for astronomical depth.
            int depthScale = (int)System.Numerics.BigInteger.Min(cryptDepth - 1, 10000);
            if (ChapterId == 1)
            {
                Monsters.Add(NewMonster(1, 0, 3600, 2400, 320));
                Monsters.Add(NewMonster(2, 1, -4800, 2400, 650));
                Monsters.Add(NewMonster(3, 2, 0, 8000, 2400));
            }
            else
            {
                // New chapter: ash hunter, ember knight and a distinct channeling boss.
                Monsters.Add(NewMonster(1, 0, -5000, 5500, 420 + depthScale * 25));
                Monsters.Add(NewMonster(2, 1, 6000, 4500, 770 + depthScale * 55));
                Monsters.Add(NewMonster(3, 3, 0, 12000, 2800 + depthScale * 200));
            }
            BindForge();
        }

        /// <summary>Advance only after victory and all loot pickup; permanent character is reused.</summary>
        public ArenaSimulation TryAdvanceCrypt()
        {
            if (Progression.EntryAct >= 2) return null; // Story travel uses adjacent, location-gated passages instead.
            if (!CryptCleared || GroundLoot.Count != 0 ||
                CryptDepth != Progression.UnlockedCryptDepth) return null;
            CharacterProgression nextProgress;
            if (!CharacterProgression.TryLoad(Progression.SaveString(), out nextProgress)) return null;
            if (Progression.EntryAct == 1)
            {
                if (!nextProgress.EnterVillage()) return null;
                return new ArenaSimulation(_runSeed, nextProgress, CryptDepth, _lootCatalog);
            }
            if (!nextProgress.TryUnlockNextCryptDepth(CryptDepth)) return null;
            return new ArenaSimulation(_runSeed, nextProgress,
                nextProgress.UnlockedCryptDepth, _lootCatalog);
        }

        private static MonsterState NewMonster(int id, int kind, long x, long z, int health)
        {
            return new MonsterState { Id = id, Kind = kind, Xmm = x, Zmm = z,
                PreviousXmm = x, PreviousZmm = z, Health = health, MaxHealth = health };
        }

        public void SetMoveInput(long xbp, long zbp)
        {
            xbp = FixedMath.Clamp(xbp, -10000, 10000);
            zbp = FixedMath.Clamp(zbp, -10000, 10000);
            long squared = xbp * xbp + zbp * zbp;
            if (squared < 40000) { _directionXbp = 0; _directionZbp = 0; return; }
            long magnitude = FixedMath.IntegerSqrt(squared);
            _directionXbp = xbp * 10000 / magnitude;
            _directionZbp = zbp * 10000 / magnitude;
        }

        public NpcAgent NearestNpc()
        {
            NpcAgent found = null;
            long distance = FixedMath.Square(NpcInteractionRadiusMm);
            foreach (NpcAgent npc in Npcs)
            {
                long d = DistanceSquared(PlayerXmm, PlayerZmm, npc.Xmm, npc.Zmm);
                if (d < distance) { found = npc; distance = d; }
            }
            return found;
        }

        /// <summary>
        /// The stolen supplies scene reuses canonical camp stock, direct Mara memory and
        /// the existing NPC cooldown/proximity contract. No independent quest ledger.
        /// </summary>
        public SocialResult DonateCampRation()
        {
            var result = new SocialResult();
            if (!IsVillage)
            {
                result.Text = "Nur in der Aschengrenze gibt es Maras Lager.";
                return result;
            }
            if (GameOver)
            {
                result.Text = "Du bist kampfunfaehig und kannst nichts abgeben.";
                return result;
            }
            NpcAgent mara = Npcs.Find(n => n.Id == SocialLedger.MaraNpcId);
            if (mara == null)
            {
                result.Text = "Mara ist hier nicht anzutreffen.";
                return result;
            }
            if (mara.Memory.Health == 0)
            {
                result.Text = "Mara ist bewusstlos und kann nichts annehmen.";
                return result;
            }
            if (mara.InteractionCooldownTicks > 0)
            {
                result.Text = "Warte, Mara braucht einen Moment vor der naechsten Uebergabe.";
                return result;
            }
            if (DistanceSquared(PlayerXmm, PlayerZmm, mara.Xmm, mara.Zmm) >
                FixedMath.Square(NpcInteractionRadiusMm))
            {
                result.Text = "Nur direkt bei Mara kannst du eine Lagerration abgeben.";
                return result;
            }
            if (!Progression.Social.DonateCampRation(SocialLedger.MaraNpcId))
            {
                result.Text = Progression.Social.CampProvisions >= SocialLedger.CampCapacity ?
                    "Maras Lager ist voll. Keine Ration verbraucht." :
                    "Du hast keinen Proviant zum Uebergeben.";
                return result;
            }
            mara.InteractionCooldownTicks = 10;
            RememberNpcAction(mara,NpcAction.Help,true,"camp.donation");
            result.Accepted = true;
            result.SpeakerId = SocialLedger.MaraNpcId; result.Speech = SpeechLine.ThankHelp;
            result.Text = "Mara nimmt eine echte Ration an. Das Lager wird sicherer; sie merkt sich deine Hilfe.";
            return result;
        }

        /// <summary>Entry-only direct ration theft with the supplier's existing cooldown and local guard perception.</summary>
        public SocialResult TakeCampRation()
        {
            var result = new SocialResult();
            if (Progression.EntryAct != 2 || GameOver) return result;
            NpcAgent mara = Npcs.Find(n => n.Id == SocialLedger.MaraNpcId);
            if (mara == null || mara.InteractionCooldownTicks > 0 || mara.Memory.Health == 0 ||
                DistanceSquared(PlayerXmm, PlayerZmm, mara.Xmm, mara.Zmm) > FixedMath.Square(NpcInteractionRadiusMm) ||
                !EntryWorldLayout.HasSight(PlayerXmm, PlayerZmm, mara.Xmm, mara.Zmm))
            { result.Text = "Nur unmittelbar bei Mara kannst du nach ihren Vorräten greifen."; return result; }
            if (!Progression.Social.TakeCampRation())
            { result.Text = "Das Lager ist leer. Du nimmst nichts."; return result; }
            mara.InteractionCooldownTicks = 10;
            result.Accepted = true;
            NpcAgent guard = Npcs.Find(n => n.Id == 2);
            bool witnessed = guard != null && guard.Memory.Health > 0 &&
                DistanceSquared(PlayerXmm, PlayerZmm, guard.Xmm, guard.Zmm) <= FixedMath.Square(8000) &&
                EntryWorldLayout.HasSight(PlayerXmm, PlayerZmm, guard.Xmm, guard.Zmm);
            if (witnessed) Progression.Social.WitnessRationTheft(guard.Id);
            RememberNpcAction(mara,NpcAction.Rob,true,"camp.theft",witnessed?new[]{guard.Id}:Array.Empty<int>());
            result.SpeakerId = SocialLedger.MaraNpcId; result.Speech = SpeechLine.Robbed;
            result.Text = witnessed ? "Du nimmst eine Ration. Mara protestiert; Dorn hat den Diebstahl gesehen und greift ein." :
                "Du nimmst eine Ration. Mara merkt sich den Übergriff; Dorn hat ihn nicht gesehen.";
            return result;
        }

        public SocialResult InteractNpc(int id, NpcAction action)
        {
            var result = new SocialResult();
            if (GameOver || action < NpcAction.Greet || action > NpcAction.Rob) return result;
            NpcAgent npc = Npcs.Find(n => n.Id == id);
            if (npc == null || npc.InteractionCooldownTicks > 0 ||
                DistanceSquared(PlayerXmm, PlayerZmm, npc.Xmm, npc.Zmm) > FixedMath.Square(NpcInteractionRadiusMm)) return result;
            if (npc.Memory.Health == 0 && action != NpcAction.Rob && action != NpcAction.Help)
            { result.Text = "Diese Person ist bewusstlos."; return result; }
            bool wasAware=npc.Memory.Health>0;
            switch (action)
            {
                case NpcAction.Greet:
                    npc.Memory.Record(action, 0); result.Text = npc.Greeting();
                    string rumor = Progression.Social.RoadRumor(id); if (rumor.Length > 0) result.Text += " " + rumor; break;
                case NpcAction.Help:
                    if (!Progression.Social.Help(id)) { result.Text = "Kein Bedarf oder kein Proviant."; return result; }
                    result.Text = "Du teilst Proviant und versorgst " + npc.Role + "."; break;
                case NpcAction.Attack:
                    if (Cooldowns[0] > 0) return result;
                    bool crit;
                    int damage = (int)Math.Min(npc.Memory.Health,
                        FixedMath.Damage(95L + WeaponBonus, 10000, 190, 2000, 1000, 1900, _rng, out crit));
                    npc.Memory.Health -= damage;
                    if (npc.Memory.Health == 0) npc.Memory.RecoveryTicks = 300;
                    npc.Memory.Record(action, npc.Memory.Health == 0 ? 3 : 1);
                    Cooldowns[0] = 2;
                    result.Text = "Uebergriff: " + damage + " Schaden. Diese Tat bleibt in Erinnerung."; break;
                case NpcAction.Rob:
                    if (!Progression.Social.Rob(id)) { result.Text = "Der Beutel ist leer."; return result; }
                    result.Text = "Du raubst den vorhandenen Muenzbeutel. Vertrauen zerbricht."; break;
            }
            if (npc.Memory.Health > 0)
            {
                result.SpeakerId = id;
                result.Speech = action == NpcAction.Greet ? NpcSpeech.Greeting(npc) :
                    action == NpcAction.Help ? SpeechLine.ThankHelp :
                    action == NpcAction.Attack ? SpeechLine.Hurt : SpeechLine.Robbed;
                if (action == NpcAction.Greet) result.FollowupSpeech = NpcSpeech.Rumor(Progression.Social, id);
            }
            npc.InteractionCooldownTicks = 10;
            RememberNpcAction(npc,action,wasAware,action==NpcAction.Help?"provisions":"encounter");
            result.Accepted = true;
            return result;
        }

        private void MoveNpc(NpcAgent npc, long targetX, long targetZ, long speed)
        {
            long dx = targetX - npc.Xmm, dz = targetZ - npc.Zmm;
            long distance = FixedMath.IntegerSqrt(dx * dx + dz * dz);
            if (distance == 0) return;
            long step = Math.Min(speed, distance);
            long nx = npc.Xmm + dx * step / distance, nz = npc.Zmm + dz * step / distance;
            if (CanStand(nx, nz)) { npc.Xmm = nx; npc.Zmm = nz; }
            else if (CanStand(nx, npc.Zmm)) npc.Xmm = nx;
            else if (CanStand(npc.Xmm, nz)) npc.Zmm = nz;
        }

        private void StepNpcs()
        {
            foreach (NpcAgent npc in Npcs)
            {
                npc.PreviousXmm = npc.Xmm; npc.PreviousZmm = npc.Zmm;
                if (npc.AttackCooldownTicks > 0) npc.AttackCooldownTicks--;
                if (npc.InteractionCooldownTicks > 0) npc.InteractionCooldownTicks--;
                if (npc.Memory.Health == 0)
                {
                    npc.Behavior = NpcBehavior.Downed;
                    if (npc.Memory.RecoveryTicks > 0) npc.Memory.RecoveryTicks--;
                    if (npc.Memory.RecoveryTicks == 0)
                    { npc.Memory.Health = NpcMemory.MaxHealth / 4; npc.Memory.Fatigue = 800; }
                    continue;
                }
                if (CurrentTick % 20 == 0) npc.Memory.Hunger = Math.Min(1000, npc.Memory.Hunger + 1);
                long distance = DistanceSquared(npc.Xmm, npc.Zmm, PlayerXmm, PlayerZmm);
                bool hostile = npc.Memory.Disposition(npc.Personality) == NpcDisposition.Hostile;
                if(StepForgeNpc(npc,distance,hostile)){if(GameOver)break;continue;}
                if (hostile && distance < FixedMath.Square(14000))
                {
                    if (npc.Personality != NpcPersonality.Brave || npc.Memory.Health < NpcMemory.MaxHealth / 4)
                    {
                        npc.Behavior = NpcBehavior.Flee;
                        MoveNpc(npc, npc.Xmm + (npc.Xmm == PlayerXmm ? 1000 : npc.Xmm - PlayerXmm),
                            npc.Zmm + (npc.Zmm - PlayerZmm), 300);
                    }
                    else
                    {
                        npc.Behavior = NpcBehavior.Attack;
                        if (distance > FixedMath.Square(1500)) MoveNpc(npc, PlayerXmm, PlayerZmm, 240);
                        else if (npc.AttackCooldownTicks == 0)
                        { ReceiveDamage(40); npc.AttackCooldownTicks = 15; }
                    }
                }
                else if (npc.Memory.Hunger >= 700)
                {
                    npc.Behavior = NpcBehavior.SeekFood;
                    if (DistanceSquared(npc.Xmm, npc.Zmm, CampXmm, CampZmm) <= FixedMath.Square(500) &&
                        Progression.Social.EatAtCamp(npc.Memory)) npc.Behavior = NpcBehavior.Eat;
                    else MoveNpc(npc, CampXmm, CampZmm, 170);
                }
                else if (npc.Memory.Fatigue >= 700 || (npc.Behavior == NpcBehavior.Rest && npc.Memory.Fatigue > 100))
                { npc.Behavior = NpcBehavior.Rest; npc.Memory.Fatigue = Math.Max(0, npc.Memory.Fatigue - 3);
                    npc.Memory.Health = Math.Min(NpcMemory.MaxHealth, npc.Memory.Health + 1); }
                else if (npc.Id == 0)
                {
                    npc.Behavior = NpcBehavior.Travel;
                    long targetZ = npc.PatrolIndex == 0 ? -3000 : -9000;
                    if (DistanceSquared(npc.Xmm, npc.Zmm, -2500, targetZ) < FixedMath.Square(300)) npc.PatrolIndex = 1 - npc.PatrolIndex;
                    MoveNpc(npc, -2500, targetZ, 170);
                    if (CurrentTick % 10 == 0) npc.Memory.Fatigue = Math.Min(1000, npc.Memory.Fatigue + 1);
                }
                else { npc.Behavior = NpcBehavior.Rest; npc.Memory.Fatigue = Math.Max(0, npc.Memory.Fatigue - 1); }
                if (GameOver) break;
            }
        }

        private static long DistanceSquared(long ax, long az, long bx, long bz)
        {
            return FixedMath.Square(ax - bx) + FixedMath.Square(az - bz);
        }

        // The same obstacle specification is used by player and monster movement.
        /// <summary>Apply arena bounds and the active chapter collision model using integer coordinates.</summary>
        public bool CanStand(long x, long z)
        {
            if (InWoodlandRegion) return CanStandInWoodland(Progression.EntryAct, Progression.Woodland.Layout, x, z);
            if (x < -ArenaLimitMm || x > ArenaLimitMm || z < -ArenaLimitMm || z > ArenaLimitMm) return false;
            if (InDungeon) return Progression.Dungeon.Current.Layout.CanStand(x, z) && (z <= 21000 || DungeonForwardOpen);
            if (Progression.EntryAct == 2) return EntryWorldLayout.CanStand(x, z);
            if (Progression.EntryAct >= 5) return CanStandInInvestigation(Progression.EntryAct, Progression.Investigation.Approach, x, z, Progression.Rattler.Active);
            if (Progression.EntryAct >= 3) return CanStandInHungerRegion(Progression.EntryAct, x, z);
            long[] coordinates = { -7000, 7000 };
            foreach (long px in coordinates)
                foreach (long pz in coordinates)
                    if (DistanceSquared(x, z, px, pz) < FixedMath.Square(1200 + PlayerRadiusMm))
                        return false;
            return true;
        }

        private void MovePlayer()
        {
            long speed = Progression.EntryAct == 7 && Progression.Rattler.Sneaking ? 240 : PlayerSpeedMmPerTick;
            long dx = _directionXbp * speed / 10000L;
            long dz = _directionZbp * speed / 10000L;
            if (CanStand(PlayerXmm + dx, PlayerZmm + dz)) { PlayerXmm += dx; PlayerZmm += dz; }
            else if (CanStand(PlayerXmm + dx, PlayerZmm)) PlayerXmm += dx;
            else if (CanStand(PlayerXmm, PlayerZmm + dz)) PlayerZmm += dz;
        }

        public void Step()
        {
            CurrentTick++;
            if(Progression.Social.Forge!=null)Progression.Social.Forge.ProcessedTicks=checked(Progression.Social.Forge.ProcessedTicks+1);
            PrevPlayerXmm = PlayerXmm;
            PrevPlayerZmm = PlayerZmm;
            foreach (MonsterState monster in Monsters)
            {
                monster.PreviousXmm = monster.Xmm;
                monster.PreviousZmm = monster.Zmm;
            }
            for (int i = 0; i < Cooldowns.Length; i++)
                if (Cooldowns[i] > 0) Cooldowns[i]--;
            if (GameOver) { StepRoadEncounter(); SaveInvestigationPlayer(); SaveDungeonPlayer(); SaveWoodlandPlayer(); return; }
            MovePlayer();
            StepNpcs();
            StepRoadEncounter();
            StepCompanion();
            StepRattler();
            StepInvestigation();
            if (GameOver) { SaveDungeonPlayer(); SaveWoodlandPlayer(); return; }

            foreach (MonsterState enemy in Monsters)
            {
                if (enemy.IsDead || enemy.Kind == 4 || enemy.Kind == 5) continue;
                if (enemy.AttackCooldownTicks > 0) enemy.AttackCooldownTicks--;
                if (enemy.BossPulseCooldownTicks > 0) enemy.BossPulseCooldownTicks--;
                long distance2 = DistanceSquared(enemy.Xmm, enemy.Zmm, PlayerXmm, PlayerZmm);
                if (distance2 > FixedMath.Square(14000) || !CombatSight(enemy.Xmm, enemy.Zmm, PlayerXmm, PlayerZmm)) continue;
                if (ChapterId == 2 && enemy.Kind == 3)
                {
                    // Explicit 8-tick warning (800 ms) before an avoidable fire shockwave.
                    if (enemy.BossWindupTicks > 0)
                    {
                        enemy.BossWindupTicks--;
                        if (enemy.BossWindupTicks == 0 &&
                            DistanceSquared(PlayerXmm, PlayerZmm, enemy.Xmm, enemy.Zmm) <= FixedMath.Square(4000))
                            ReceiveDamage(190);
                        continue;
                    }
                    if (enemy.BossPulseCooldownTicks == 0 && distance2 <= FixedMath.Square(10000))
                    {
                        enemy.BossWindupTicks = 8;
                        enemy.BossPulseCooldownTicks = 55;
                        continue;
                    }
                }
                if (distance2 > FixedMath.Square(1650))
                {
                    long distance = Math.Max(1, FixedMath.IntegerSqrt(distance2));
                    long speed = enemy.Kind >= 2 ? 190 : MonsterSpeedMmPerTick;
                    long nx = enemy.Xmm + (PlayerXmm - enemy.Xmm) * speed / distance;
                    long nz = enemy.Zmm + (PlayerZmm - enemy.Zmm) * speed / distance;
                    if (CanStand(nx, nz)) { enemy.Xmm = nx; enemy.Zmm = nz; }
                    else if (CanStand(nx, enemy.Zmm)) enemy.Xmm = nx;
                    else if (CanStand(enemy.Xmm, nz)) enemy.Zmm = nz;
                }
                else if (enemy.AttackCooldownTicks == 0)
                {
                    enemy.AttackCooldownTicks = enemy.Kind >= 2 ? 12 : 17;
                    ReceiveDamage((enemy.Kind >= 2 ? 105 : (enemy.Kind == 1 ? 60 : 40)));
                    if (GameOver) break;
                }
            }
            SaveDungeonPlayer(); SaveWoodlandPlayer();
        }

        private MonsterState NearestMonster(long maxDistanceMm)
        {
            long bestDistance2 = FixedMath.Square(maxDistanceMm);
            MonsterState target = null;
            foreach (MonsterState enemy in Monsters)
            {
                if (enemy.IsDead || (enemy.Kind == 4 && !Progression.Road.Started)) continue;
                long d2 = DistanceSquared(PlayerXmm, PlayerZmm, enemy.Xmm, enemy.Zmm);
                if (d2 <= bestDistance2 && CombatSight(PlayerXmm, PlayerZmm, enemy.Xmm, enemy.Zmm)) { bestDistance2 = d2; target = enemy; }
            }
            return target;
        }

        /// <summary>Resolve existing combat rewards; only a transition to dead generates a catalog item.</summary>
        private void DealDamage(MonsterState enemy, int powerBp, int slot, SkillResult result)
        {
            bool crit;
            var stats = new CombatStats(Progression, slot);
            long damage = CombatRules.Damage(stats, powerBp, enemy.Kind, _rng, out crit);
            int appliedDamage = (int)Math.Min(enemy.Health, damage);
            enemy.Health = (int)Math.Max(0, enemy.Health - damage);
            PlayerHealth = Math.Min(MaxPlayerHealth,PlayerHealth+(int)((long)appliedDamage*Progression.Equipment.Bonus(WeaponAffix.Siphon)/10000));
            Progression.RecordPractice(slot, PracticeRules.ExperienceFromEffectiveAmount(appliedDamage));
            result.TotalDamage = CombatRules.AddReportedDamage(result.TotalDamage, damage);
            result.Hits++;
            if (enemy.Kind == 4)
            {
                var h = Progression.Road; h.Started = true; h.AggroTicks = 80;
                h.PlayerDamage = Math.Min(HunterEncounter.BearMaximumHealth, h.PlayerDamage + appliedDamage);
                // A real hit interrupts a swipe aimed at the hunter and turns the bear toward its attacker.
                if (!h.StrikePlayer) enemy.BossWindupTicks = 0;
                bool firstKill = enemy.IsDead && !enemy.LootGranted;
                FinishBear(); if (firstKill) result.Kills++; SaveRoadPlayer(); return;
            }
            if (enemy.Kind == 5) { FinishRattler(result); return; }
            ResolveMonsterDefeat(enemy,result);
        }

        private void ResolveMonsterDefeat(MonsterState enemy,SkillResult result)
        {
            if (enemy.IsDead && !enemy.LootGranted)
            {
                enemy.LootGranted = true;
                result.Kills++;
                Kills++;
                Progression.RecordKill();
                Progression.EarnExperience(new System.Numerics.BigInteger(
                    enemy.Kind >= 2 ? 1200 : (enemy.Kind == 1 ? 410 : 160)));
                GroundLoot.Add(new LootState { Id = InDungeon || InForest ? enemy.Id : _nextLootId++, Xmm = enemy.Xmm, Zmm = enemy.Zmm,
                    WeaponBonus = enemy.Kind >= 2 ? 34 : (enemy.Kind == 1 ? 17 : 8),
                    Item = InForest ? LootCatalog.Version1.Generate(Progression.Woodland.Seed, CryptDepth, CurrentTick, enemy.Id, enemy.Kind, Progression.LifetimeKills) : InDungeon ? LootCatalog.Version1.Generate(Progression.Dungeon.Seed, CryptDepth, CurrentTick, enemy.Id, enemy.Kind, Progression.LifetimeKills) :
                        _lootCatalog == null ? null : _lootCatalog.Generate(_runSeed, CryptDepth,
                        CurrentTick, enemy.Id, enemy.Kind, Progression.LifetimeKills) });
            }
        }

        /// <summary>Execute a shipped skill contract using the existing cooldown, resource and target owners.</summary>
        public SkillResult ActivateSkill(int slot)
        {
            var result = new SkillResult();
            if (GameOver) { result.Message = "Defeated - restart (R)"; return result; }
            if (slot < 0 || slot >= SkillCatalog.Count) { result.Message = "Unknown ability"; return result; }
            var skill = SkillCatalog.Get(slot);
            if (Cooldowns[slot] > 0) { result.Message = "Ability cooling down"; return result; }
            if (Resource < skill.FocusCost) { result.Message = "Insufficient focus"; return result; }
            if (skill.Target == SkillTarget.Self)
            {
                int previousHealth = PlayerHealth;
                PlayerHealth = Math.Min(MaxPlayerHealth, PlayerHealth + skill.BaseHealing + Progression.ResolveHealBonus + Progression.PracticeHealingBonus);
                int restored = PlayerHealth - previousHealth;
                if (restored > 0) Progression.RecordPractice(slot, PracticeRules.ExperienceFromEffectiveAmount(restored));
                Cooldowns[slot] = AttackCooldown(slot);
                result.Activated = true;
                result.Message = skill.DisplayName + ": restored " + restored + " HP";
                SaveRoadPlayer(); SaveInvestigationPlayer(); SaveWoodlandPlayer();
                return result;
            }
            long range = AttackRange(slot);
            MonsterState nearest = NearestMonster(range);
            if (nearest == null) { result.Message = "No enemy within reach"; return result; }
            Resource = Math.Min(100, Resource - skill.FocusCost + skill.FocusGain);
            Cooldowns[slot] = AttackCooldown(slot);
            result.Activated = true;
            if (skill.Target == SkillTarget.Area)
            {
                foreach (MonsterState enemy in Monsters)
                    if (!enemy.IsDead && DistanceSquared(PlayerXmm, PlayerZmm, enemy.Xmm, enemy.Zmm) <= FixedMath.Square(range) &&
                        CombatSight(PlayerXmm, PlayerZmm, enemy.Xmm, enemy.Zmm))
                        DealDamage(enemy, skill.PowerBp, slot, result);
            }
            else DealDamage(nearest, skill.PowerBp, slot, result);
            HearRattlerNoise(); SaveInvestigationPlayer(); SaveWoodlandPlayer();
            result.Message = skill.DisplayName +
                ": " + result.TotalDamage + " damage / " + result.Kills + " kills";
            return result;
        }

        /// <summary>Collect reachable drops through the canonical progression owner, preserving legacy rewards.</summary>
        public bool TryCollectLoot()
        {
            if (GameOver) return false;
            for (int i = 0; i < GroundLoot.Count; i++)
            {
                LootState item = GroundLoot[i];
                if (DistanceSquared(PlayerXmm, PlayerZmm, item.Xmm, item.Zmm) > FixedMath.Square(PickupRadiusMm)) continue;
                if (item.Item != null)
                {
                    if (!Progression.CollectItem(item.Item)) return false;
                }
                else Progression.AddWeaponUpgrade(item.WeaponBonus);
                GroundLoot.RemoveAt(i);
                SaveWoodlandPlayer();
                return true;
            }
            return false;
        }

        // Stable FNV-1a over every field that currently affects future simulation outcomes.
        /// <summary>Hash canonical simulation state; catalog mode additionally binds its revision, seed and ground receipts.</summary>
        public ulong ComputeWorldHash()
        {
            ulong h = 14695981039346656037UL;
            Mix(ref h, CurrentTick); Mix(ref h, _rng.S0); Mix(ref h, _rng.S1);
            byte[] depthBytes = CryptDepth.ToByteArray();
            Mix(ref h, (ulong)depthBytes.Length);
            foreach (byte depthByte in depthBytes) Mix(ref h, depthByte);
            Mix(ref h, (ulong)PlayerXmm); Mix(ref h, (ulong)PlayerZmm);
            Mix(ref h, (ulong)PrevPlayerXmm); Mix(ref h, (ulong)PrevPlayerZmm);
            Mix(ref h, (ulong)_directionXbp); Mix(ref h, (ulong)_directionZbp);
            Mix(ref h, (ulong)PlayerHealth); Mix(ref h, (ulong)Resource);
            Mix(ref h, Progression.StableHash); Mix(ref h, (ulong)Kills);
            Mix(ref h, (ulong)_nextLootId);
            Mix(ref h, (ulong)Npcs.Count);
            foreach (NpcAgent npc in Npcs)
            {
                Mix(ref h, (ulong)npc.Id); Mix(ref h, (ulong)npc.Xmm); Mix(ref h, (ulong)npc.Zmm);
                Mix(ref h, (ulong)npc.PreviousXmm); Mix(ref h, (ulong)npc.PreviousZmm);
                Mix(ref h, (ulong)npc.Behavior); Mix(ref h, (ulong)npc.PatrolIndex);
                Mix(ref h, (ulong)npc.AttackCooldownTicks); Mix(ref h, (ulong)npc.InteractionCooldownTicks);
            }
            foreach (int cooldown in Cooldowns) Mix(ref h, (ulong)cooldown);
            Mix(ref h, (ulong)Monsters.Count);
            foreach (MonsterState enemy in Monsters)
            {
                Mix(ref h, (ulong)enemy.Id); Mix(ref h, (ulong)enemy.Kind);
                Mix(ref h, (ulong)enemy.Xmm); Mix(ref h, (ulong)enemy.Zmm);
                Mix(ref h, (ulong)enemy.PreviousXmm); Mix(ref h, (ulong)enemy.PreviousZmm);
                Mix(ref h, (ulong)enemy.Health); Mix(ref h, (ulong)enemy.MaxHealth);
                Mix(ref h, (ulong)enemy.AttackCooldownTicks); Mix(ref h, enemy.LootGranted ? 1UL : 0UL);
                Mix(ref h, (ulong)enemy.BossWindupTicks); Mix(ref h, (ulong)enemy.BossPulseCooldownTicks);
            }
            Mix(ref h, (ulong)GroundLoot.Count);
            foreach (LootState loot in GroundLoot)
            {
                Mix(ref h, (ulong)loot.Id); Mix(ref h, (ulong)loot.Xmm);
                Mix(ref h, (ulong)loot.Zmm); Mix(ref h, (ulong)loot.WeaponBonus);
            }
            if (_lootCatalog != null)
            {
                Mix(ref h, 1); Mix(ref h, _lootCatalog.StableHash); Mix(ref h, _runSeed);
                foreach (LootState loot in GroundLoot) Mix(ref h, loot.Item == null ? 0 : loot.Item.StableHash);
            }
            return h;
        }

        private static void Mix(ref ulong h, ulong n)
        {
            unchecked
            {
                for (int i = 0; i < 8; i++)
                {
                    h ^= (byte)(n >> (i * 8));
                    h *= 1099511628211UL;
                }
            }
        }
    }
}
