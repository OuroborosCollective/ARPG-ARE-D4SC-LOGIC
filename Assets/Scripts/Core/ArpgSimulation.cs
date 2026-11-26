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
        public AshenCrypt.MemoryForge.NpcMind Mind { get; internal set; }
        public int Id;
        public int Kind; // 0: ghoul, 1: knight, 2: boss
        /// <summary>Content family; Legacy for every monster spawned before roles existed.</summary>
        public MonsterFamily Family = MonsterFamily.Legacy;
        /// <summary>Explicit combat role; None keeps the legacy behaviour and world hash.</summary>
        public MonsterRole Role = MonsterRole.None;
        public long Xmm;
        public long Zmm;
        public long PreviousXmm;
        public long PreviousZmm;
        public int Health;
        public int MaxHealth;
        public int AttackCooldownTicks;
        // Read-only projection of the last immediate strike. Combat, replay hashes and saves never consume it.
        public (long Xmm, long Zmm)? LastImmediateStrikeTarget { get; internal set; }
        public bool LootGranted;
        // The ember-warden's countdown is part of the authoritative replay state.
        public int BossWindupTicks;
        public int BossPulseCooldownTicks;
        public bool IsDead { get { return Health <= 0; } }
    }

    public sealed class LootState
    {
        public LootItem Item { get; internal set; }
        public bool HasFieldPauldron { get; internal set; }
        public int Id;
        public long Xmm;
        public long Zmm;
        public int WeaponBonus;
    }

    public sealed class SkillResult
    {
        public bool Activated;
        /// <summary>Skill slot of a combat-v2 impact (-1 for legacy results); presentation only.</summary>
        public int Slot = -1;
        public int TotalDamage;
        public int Hits;
        public int Kills;
        public string Message = "";
        /// <summary>Position of the accepted target when the ability was executed; presentation facing only.</summary>
        public bool HasTarget;
        public long TargetXmm, TargetZmm;
        /// <summary>Monsters that actually took damage, in application order; presentation hit reactions only.</summary>
        public readonly System.Collections.Generic.List<int> HitMonsterIds = new System.Collections.Generic.List<int>();
        /// <summary>Weapon-skill levels gained by this activation and the trained type; presentation feedback only.</summary>
        public int WeaponLevelUps;
        public WeaponFamily? TrainedFamily;
        /// <summary>Shots that missed (only the bow can miss); they deal and train nothing.</summary>
        public int Misses;
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
        public const long EmberPulseRadiusMm = 4000L;
        public const long CampXmm = -4500L;
        public const long CampZmm = -6000L;
        public const long NpcInteractionRadiusMm = 2400L;
        public bool AtWorldPassage
        {
            get
            {
                if (InJourney || InWoodlandRegion) return false;
                if (InKilnYard) return DistanceSquared(PlayerXmm, PlayerZmm, KilnLayout.ExitXmm, KilnLayout.ExitZmm) <= FixedMath.Square(PassageRadiusMm);
                if (InCourt && AtKilnSideGate) return true;
                return DistanceSquared(PlayerXmm, PlayerZmm, InCourt ? CourtLayout.ExitXmm : PassageXmm, InCourt ? CourtLayout.ExitZmm : PassageZmm) <= FixedMath.Square(PassageRadiusMm);
            }
        }
        /// <summary>
        /// Aschenwacht between the old crypt and the village: forecourt and bell court, open air, no fight. Its side yard, the
        /// Brennerhof, belongs to Aschenwacht in the save but is a place of its own (<see cref="InKilnYard"/>).
        /// </summary>
        public bool InCourt { get { return Progression.EntryAct == 1 && Progression.InCourt && !Progression.InKilnYard; } }

        /// <summary>Use the existing passage gate and carry surviving player resources into the next approved world.</summary>
        public ArenaSimulation TryTraversePassage()
        {
            if (AtJourneyPassage) return TraverseJourney();
            if (AtWoodlandPassage) return TraverseWoodland();
            if (!AtAnyPassage || GameOver) return null;
            ArenaSimulation next = AtNordwechsel ? EnterNordwechsel()
                : InNordwechselRift ? TryAdvanceCrypt()
                : Progression.EntryAct >= 2 ? TraverseHunger(AtReturnPassage)
                : InKilnYard ? TryLeaveKilnYard()
                : InCourt && AtKilnSideGate ? TryEnterKilnYard()
                : Progression.EntryAct == 1 && !InCourt ? TryEnterCourt() : TryAdvanceCrypt();
            if (next == null) return null;
            next.PlayerHealth = Math.Min(PlayerHealth, next.MaxPlayerHealth);
            next.Resource = Resource;
            for (int i = 0; i < Cooldowns.Length; i++) next.Cooldowns[i] = Cooldowns[i];
            if (next.InKilnYard)
            {
                next.PlayerXmm = next.PrevPlayerXmm = KilnLayout.SpawnXmm;
                next.PlayerZmm = next.PrevPlayerZmm = KilnLayout.SpawnZmm;
            }
            else if (next.InCourt && InKilnYard)
            {
                next.PlayerXmm = next.PrevPlayerXmm = KilnYard.CourtReturnXmm;
                next.PlayerZmm = next.PrevPlayerZmm = KilnYard.CourtReturnZmm;
            }
            else
                next.PlayerZmm = next.PrevPlayerZmm = next.InCourt ? CourtLayout.SpawnZmm : next.Progression.HungerStage > 0 && !next.InNordwechselRift ?
                    (next.Progression.EntryFromNorth ? 21000L : -21000L) : -23000L;
            next.BindCompanion(true);
            next.SaveRoadPlayer(); next.SaveInvestigationPlayer(); next.SaveDungeonPlayer();
            return next;
        }

        private readonly DeterministicRng _rng;
        private readonly ulong _runSeed;
        private readonly LootCatalog _lootCatalog;
        public System.Numerics.BigInteger CryptDepth { get; private set; }
        public int ChapterId { get { return CryptDepth.IsOne ? 1 : 2; } }
        public string ChapterName { get { return InJourney ? (InJourneyCave ? "Seitenhöhle – Ebene " + Progression.Journey.Depth : "Waldweg jenseits von Aschenzoll") : InWoodlandRegion ? (InForest ? "Der Wald nach Aschenzoll" : Progression.EntryAct == 9 ? "Die Bärenhöhle" : "Aschenzoll – Frachtplatz") : InDungeon ? "Krypta unter der Muehle - Ebene " + Progression.Dungeon.Depth : Progression.EntryAct == 7 ? "Das Rattlernest" : Progression.EntryAct == 6 ? "Die verborgene Krypta" : Progression.EntryAct == 5 ? "Der alte Speicher" : Progression.EntryAct == 4 ? "Die alte Muehle" : Progression.EntryAct == 3 ? "Die Aschestrasse" : InKilnYard ? KilnYard.Title : InCourt ? (PlayerZmm >= CourtLayout.BellCourtZmm ? "Aschenwacht – Der Glockenhof" : "Aschenwacht – Der entweihte Vorhof") : Progression.EntryAct == 2 ? (InsidePlace != VillagePlace.None ? VillagePlaceLayout.Name(InsidePlace) + " von Aschengrenze" : "Die Aschengrenze") : (ChapterId == 1 ? "Ashen Crypt" : "Ember Rift"); } }
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
        /// <summary>Enemy attacks that reached the player, and how many of them a shield blocked; feedback only.</summary>
        public int IncomingAttacks { get; private set; }
        public int BlockedAttacks { get; private set; }
        public readonly List<MonsterState> Monsters = new List<MonsterState>();
        public readonly List<LootState> GroundLoot = new List<LootState>();
        public readonly List<NpcAgent> Npcs = new List<NpcAgent>();
        public readonly int[] Cooldowns = new int[4];

        public ArenaSimulation(ulong seed, CharacterProgression progression = null)
            : this(seed, progression, progression?.EntryAct == CharacterProgression.NordwechselAct ?
                  progression.UnlockedCryptDepth : System.Numerics.BigInteger.One) { }

        public ArenaSimulation(ulong seed, CharacterProgression progression,
            System.Numerics.BigInteger cryptDepth)
            : this(seed, progression, cryptDepth, null) { }

        /// <summary>Explicit versioned loot opt-in preserves the legacy simulation and hash contract.</summary>
        public ArenaSimulation(ulong seed, CharacterProgression progression,
            System.Numerics.BigInteger cryptDepth, LootCatalog lootCatalog)
            : this(seed, progression, cryptDepth, lootCatalog, progression?.Journey.Maps) { }

        public ArenaSimulation(ulong seed, CharacterProgression progression,
            System.Numerics.BigInteger cryptDepth, LootCatalog lootCatalog, JourneyMapCatalog journeyMaps)
        {
            if (cryptDepth < System.Numerics.BigInteger.One)
                throw new ArgumentOutOfRangeException(nameof(cryptDepth));
            _runSeed = seed;
            _lootCatalog = lootCatalog;
            _journeyMaps = journeyMaps;
            CryptDepth = cryptDepth;
            Progression = progression ?? new CharacterProgression();
            if (Progression.Journey.Active && journeyMaps?.Resolve(Progression.Journey.MapHash) == null)
                throw new InvalidOperationException("Exact authored journey map unavailable");
            if (InNordwechselRift)
            {
                if (cryptDepth != Progression.UnlockedCryptDepth || !Progression.HasNordwechselCheckpoint)
                    throw new ArgumentException("Nordwechsel requires the reported station and its unlocked depth.", nameof(progression));
            }
            else if (Progression.EntryAct > 0 && !cryptDepth.IsOne)
                throw new ArgumentOutOfRangeException(nameof(cryptDepth), "The prologue requires its depth-one checkpoint.");
            Progression.Supplies.EnterArea(SupplyArea);
            PlayerHealth = MaxPlayerHealth;
            _rng = new DeterministicRng(InJourney ? JourneySeed : InForest ? Progression.Woodland.Seed : InDungeon ? Progression.Dungeon.Seed : seed);
            if (InJourney) { InitializeJourney(); Progression.JourneyCapture = SaveJourneyPlayer; BindForge(); return; }
            Progression.JourneyCapture = SaveJourneyPlayer;
            if (IsVillage)
            {
                Npcs.Add(new NpcAgent(0, Progression.Social.GetMemory(0), -3600, -3400));
                Npcs.Add(new NpcAgent(1, Progression.Social.GetMemory(1), -5500, -6000));
                Npcs.Add(new NpcAgent(2, Progression.Social.GetMemory(2), -6500, -3500));
            }
            else if (Progression.EntryAct == 0) Npcs.Add(new NpcAgent(0, Progression.Social.GetMemory(0), -2000, -21000));
            if (Progression.EntryAct >= 2 && !InNordwechselRift)
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
            if (InKilnYard)
            {
                // Nothing fights in the Brennerhof either: the wanderer stands inside its south gate.
                PlayerXmm = PrevPlayerXmm = KilnLayout.SpawnXmm;
                PlayerZmm = PrevPlayerZmm = KilnLayout.SpawnZmm;
                BindForge();
                return;
            }
            if (InCourt)
            {
                // Nothing fights here: the wanderer steps out of the crypt portal and may look around. Once he has been in the
                // Brennerhof, the side gate he last came out of is where he resumes.
                PlayerXmm = PrevPlayerXmm = Progression.KilnVisited ? KilnYard.CourtReturnXmm : CourtLayout.SpawnXmm;
                PlayerZmm = PrevPlayerZmm = Progression.KilnVisited ? KilnYard.CourtReturnZmm : CourtLayout.SpawnZmm;
                PlaceAschbeter();
                BindForge();
                return;
            }
            if (InNordwechselRift) PlayerZmm = PrevPlayerZmm = -23000L;
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

        /// <summary>
        /// The passage of the cleared entry crypt leads into the bell court, not straight to the village. The same
        /// conditions as for any advance hold: every guardian down, nothing left on the ground.
        /// </summary>
        public ArenaSimulation TryEnterCourt()
        {
            if (Progression.EntryAct != 1 || InCourt || !CryptCleared || LootOnGround ||
                CryptDepth != Progression.UnlockedCryptDepth) return null;
            CharacterProgression nextProgress;
            if (!CharacterProgression.TryLoad(Progression.SaveString(), _journeyMaps, out nextProgress)) return null;
            if (!nextProgress.EnterCourt()) return null;
            return new ArenaSimulation(_runSeed, nextProgress, CryptDepth, _lootCatalog, _journeyMaps);
        }

        /// <summary>Advance only after victory and all loot pickup; permanent character is reused.</summary>
        public ArenaSimulation TryAdvanceCrypt()
        {
            if (Progression.EntryAct >= 2 && !InNordwechselRift) return null;
            if (!CryptCleared || LootOnGround || (InNordwechselRift && !AtWorldPassage) ||
                CryptDepth != Progression.UnlockedCryptDepth) return null;
            CharacterProgression nextProgress;
            if (!CharacterProgression.TryLoad(Progression.SaveString(), _journeyMaps, out nextProgress)) return null;
            if (Progression.EntryAct == 1)
            {
                if (!nextProgress.EnterVillage()) return null;
                return new ArenaSimulation(_runSeed, nextProgress, CryptDepth, _lootCatalog, _journeyMaps);
            }
            if (!nextProgress.TryUnlockNextCryptDepth(CryptDepth)) return null;
            if (InNordwechselRift)
            {
                nextProgress.Regions.Clear(RegionNetwork.Glutklamm);
                nextProgress.Regions.Enter(RegionNetwork.Glutklamm, nextProgress.EntryAct);
            }
            return new ArenaSimulation(_runSeed, nextProgress,
                nextProgress.UnlockedCryptDepth, _lootCatalog, _journeyMaps);
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
            if (ArrivalScenePending) { result.Text = ListenFirst; return result; }
            // The story village decides Mara's rations once; the legacy arena village keeps its open camp.
            if (Progression.EntryAct == 2 && !CampDecisionOpen) { result.Text = CampAlreadyDecided; return result; }
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
            var donation = RememberNpcAction(mara,NpcAction.Help,true,"camp.donation");
            bool refilled = RecordCampRefill();
            // Filling the camp is the decision for the first way, with or without Mara's thanks.
            if (Progression.Social.CampProvisions >= SocialLedger.CampCapacity) RecordCampDecision("replaced", donation);
            result.Accepted = true;
            result.SpeakerId = SocialLedger.MaraNpcId; result.Speech = SpeechLine.ThankHelp;
            int missing = SocialLedger.CampCapacity - Progression.Social.CampProvisions;
            result.Text = refilled ? "Mara: Das Lager ist wieder voll. Danke, das vergesse ich dir nicht. Nebenaufgabe erledigt." :
                missing > 0 ? "Mara nimmt eine echte Ration an. Noch " + missing + (missing == 1 ? " Ration fehlt." : " Rationen fehlen.") :
                "Mara nimmt eine echte Ration an. Das Lager ist voll.";
            return result;
        }

        /// <summary>Entry-only direct ration theft with the supplier's existing cooldown and local guard perception.</summary>
        public SocialResult TakeCampRation()
        {
            var result = new SocialResult();
            if (Progression.EntryAct != 2 || GameOver) return result;
            if (!CampDecisionOpen) { result.Text = ArrivalScenePending ? ListenFirst : CampAlreadyDecided; return result; }
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
            var theft = RememberNpcAction(mara,NpcAction.Rob,true,"camp.theft",witnessed?new[]{guard.Id}:Array.Empty<int>());
            RecordCampDecision("stolen", theft);
            result.SpeakerId = SocialLedger.MaraNpcId; result.Speech = SpeechLine.Robbed;
            result.Text = witnessed ? "Du nimmst eine Ration. Mara protestiert; Dorn hat den Diebstahl gesehen und greift ein." :
                "Du nimmst eine Ration. Mara merkt sich den Übergriff; Dorn hat ihn nicht gesehen.";
            return result;
        }

        public SocialResult InteractNpc(int id, NpcAction action)
        {
            var result = new SocialResult();
            if (GameOver || action < NpcAction.Greet || action > NpcAction.Rob) return result;
            // The authored first-arrival scene has sole input authority until its
            // one-time Core receipt exists; presentation gating is not sufficient.
            if (ArrivalScenePending) { result.Text = ListenFirst; return result; }
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
                    string rumor = Progression.Social.RoadRumor(id); if (rumor.Length > 0) result.Text += " " + rumor;
                    string supplyNews = VillageSupplyNews(id); if (supplyNews.Length > 0) result.Text += " " + supplyNews;
                    // This greeting was range/cooldown/health validated by the existing Core owner.
                    // Record only a reference to what this NPC actually knows and just said;
                    // never infer knowledge for the player from the global event archive.
                    var campReceipt = LivingChronicle.HearAtRealGreeting(Progression, npc.Mind);
                    if (campReceipt != null)
                    {
                        result.ChronicleTextKey = "chronicle." + campReceipt.Event.Context;
                        result.ChronicleSpeakerId = npc.Mind.Id;
                        result.ChronicleKnowledgeOrigin = campReceipt.Origin;
                        result.ChronicleEventId = campReceipt.Event.Id;
                    }
                    break;
                case NpcAction.Help:
                    if (!Progression.Social.Help(id)) { result.Text = "Kein Bedarf oder kein Proviant."; return result; }
                    result.Text = "Du teilst Proviant und versorgst " + npc.Role + "."; break;
                case NpcAction.Attack:
                    if (Cooldowns[0] > 0) return result;
                    bool crit;
                    int damage = (int)Math.Min(npc.Memory.Health,
                        FixedMath.Damage(95L + WeaponBonus, 10000, 190, 2000, 1000, 1900, _rng, out crit));
                    npc.Memory.Health -= damage;
                    // A story-NPC is only ever knocked down, never permanently removed, while its quest is open.
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
                if (action == NpcAction.Greet)
                {
                    result.FollowupSpeech = NpcSpeech.Rumor(Progression.Social, id);
                    if (result.FollowupSpeech == SpeechLine.None && npc.Mind != null)
                    {
                        var forge = Progression.Social.Forge;
                        var memory = npc.Mind.Cognition.SocialRecall("player", forge.ProcessedTicks);
                        if (memory != null)
                        {
                            forge.Archive.Recollect(npc.Mind.Id, memory.EventId, forge.ProcessedTicks);
                            result.FollowupSpeech = NpcSpeech.Personal(memory);
                            result.Text += " " + NpcSpeech.Text(result.FollowupSpeech);
                        }
                    }
                }
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
                bool hostile = npc.Mood == NpcDisposition.Hostile;
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
                        { ReceiveHit(40, true, npc.Xmm, npc.Zmm); npc.AttackCooldownTicks = 15; }
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
            if (InJourney) return JourneyGeometry.CanStand(x,z) && (!InJourneyCave || Progression.Journey.Depth==Progression.Journey.FloorCount || z<=21000 || JourneyForwardOpen);
            if (InWoodlandRegion) return CanStandInWoodland(Progression.EntryAct, Progression.Woodland.Layout, x, z);
            if (Progression.EntryAct == 2)
            {
                // The rooms of the village houses lie outside the square; inside one, its walls and furniture decide.
                VillagePlace room = VillagePlaceLayout.RoomAt(x, z);
                if (room != VillagePlace.None) return VillagePlaceLayout.CanStandInRoom(room, x, z);
            }
            if (InKilnYard) return CanStandInKilnYard(x, z);
            if (InCourt) return CourtLayout.CanStand(x, z);
            if (x < -ArenaLimitMm || x > ArenaLimitMm || z < -ArenaLimitMm || z > ArenaLimitMm) return false;
            if (InDungeon) return Progression.Dungeon.Current.Layout.CanStand(x, z) && (z <= 21000 || DungeonForwardOpen);
            if (Progression.EntryAct == 2) return EntryWorldLayout.CanStand(x, z);
            if (!InNordwechselRift && Progression.EntryAct >= 5) return CanStandInInvestigation(Progression.EntryAct, Progression.Investigation.Approach, x, z, Progression.Rattler.Active);
            if (!InNordwechselRift && Progression.EntryAct >= 3) return CanStandInHungerRegion(Progression.EntryAct, x, z);
            long[] coordinates = { -7000, 7000 };
            foreach (long px in coordinates)
                foreach (long pz in coordinates)
                    if (DistanceSquared(x, z, px, pz) < FixedMath.Square(1200 + PlayerRadiusMm))
                        return false;
            return true;
        }

        private void MovePlayer()
        {
            // Sneaking lowers the base speed; the weapon and shield movement profile applies to both.
            long baseSpeed = Progression.EntryAct == 7 && Progression.Rattler.Sneaking ? 240 : PlayerSpeedMmPerTick;
            long speed = baseSpeed * Progression.MoveSpeedBp / 10000L;
            long dx = _directionXbp * speed / 10000L;
            long dz = _directionZbp * speed / 10000L;
            if (CanStand(PlayerXmm + dx, PlayerZmm + dz)) { PlayerXmm += dx; PlayerZmm += dz; }
            else if (CanStand(PlayerXmm + dx, PlayerZmm)) PlayerXmm += dx;
            else if (CanStand(PlayerXmm, PlayerZmm + dz)) PlayerZmm += dz;
        }

        public void Step()
        {
            CurrentTick++;
            // What one tick decided is read by the presentation after it; a tick after defeat must not repeat it.
            Impacts.Clear();
            Heals.Clear();
            if (Progression.Crafting.ActiveRegenTicks > 0 && PlayerHealth > 0)
                PlayerHealth = Math.Min(MaxPlayerHealth, PlayerHealth + Progression.Crafting.RegenPerTick);
            Progression.Crafting.AdvanceTick();
            Progression.CompanionRecovery.AdvanceTick(Progression.Companion);
            Progression.Village.AdvanceTick();
            Progression.Healing.AdvanceTick();
            var forge = Progression.Social.Forge;
            if (forge != null)
            {
                forge.ProcessedTicks = checked(forge.ProcessedTicks + 1);
                forge.Archive.AdvancePersonalTime(forge.ProcessedTicks);
            }
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
            if (CombatV2) { AdvanceCombatBeforeMove(); MovePlayerV2(); }
            else MovePlayer();
            StepFreight();
            CalmVillagers();
            StepNpcs();
            // A villager's blow can end the game in this very tick. From then on the village rests like the rest
            // of the world: no work step is finished and no stock moves while the player is down.
            if (!GameOver) StepVillageEconomy();
            StepRoadEncounter();
            StepCompanion();
            StepRattler();
            StepInvestigation();
            StepAschbeter();
            if (GameOver) { SaveDungeonPlayer(); SaveWoodlandPlayer(); return; }

            foreach (MonsterState enemy in Monsters)
            {
                if (enemy.IsDead || enemy.Kind == 4 || enemy.Kind == 5) continue;
                EnsureCreatureMind(enemy);
                if (enemy.AttackCooldownTicks > 0) enemy.AttackCooldownTicks--;
                if (enemy.BossPulseCooldownTicks > 0) enemy.BossPulseCooldownTicks--;
                if (CombatV2 && enemy.Role == MonsterRole.None && enemy.Kind != 3 && enemy.BossWindupTicks > 0)
                {
                    // Combat v2: a legacy melee swing was announced; it lands only if the player is still in reach.
                    enemy.BossWindupTicks--;
                    if (enemy.BossWindupTicks == 0 && CombatSight(enemy.Xmm, enemy.Zmm, PlayerXmm, PlayerZmm) &&
                        DistanceSquared(PlayerXmm, PlayerZmm, enemy.Xmm, enemy.Zmm) <= FixedMath.Square(CombatV2Rules.EnemyImpactReachMm))
                        ReceiveCreatureHit(enemy, enemy.Kind >= 2 ? 105 : (enemy.Kind == 1 ? 60 : 40), committed: true);
                    continue;
                }
                if (enemy.Role != MonsterRole.None && enemy.BossWindupTicks > 0)
                {
                    // A running announcement always resolves, even if the player breaks line of sight, so a stale
                    // windup can never resume later. The monster stands still; leaving the radius avoids the hit.
                    enemy.BossWindupTicks--;
                    if (enemy.BossWindupTicks == 0 &&
                        CombatSight(enemy.Xmm, enemy.Zmm, PlayerXmm, PlayerZmm) &&
                        DistanceSquared(PlayerXmm, PlayerZmm, enemy.Xmm, enemy.Zmm) <= FixedMath.Square(MonsterRoles.ImpactRadiusMm(enemy.Role)))
                        ReceiveCreatureHit(enemy, MonsterRoles.AttackDamage(enemy.Role), committed: true);
                    continue;
                }
                long distance2 = DistanceSquared(enemy.Xmm, enemy.Zmm, PlayerXmm, PlayerZmm);
                bool emberWarden = ChapterId == 2 && enemy.Kind == 3;
                if (emberWarden && enemy.BossWindupTicks > 0)
                {
                    // A committed pulse resolves after eight ticks even if its target has left aggro range.
                    enemy.BossWindupTicks--;
                    if (enemy.BossWindupTicks == 0 && distance2 <= FixedMath.Square(EmberPulseRadiusMm) &&
                        CombatSight(enemy.Xmm, enemy.Zmm, PlayerXmm, PlayerZmm))
                        ReceiveCreatureHit(enemy, 190, committed: true);
                    continue;
                }
                if (StepMonsterAgainstCompanion(enemy)) continue;
                if (distance2 > FixedMath.Square(14000) || !CombatSight(enemy.Xmm, enemy.Zmm, PlayerXmm, PlayerZmm)) continue;
                if (!ForgeCreatureMayAttack(enemy,PlayerXmm,PlayerZmm)) continue;
                if (emberWarden && enemy.BossPulseCooldownTicks == 0 && distance2 <= FixedMath.Square(10000))
                {
                    enemy.BossWindupTicks = 8;
                    enemy.BossPulseCooldownTicks = 55;
                    continue;
                }
                if (distance2 > FixedMath.Square(MonsterRoles.AttackRangeMm(enemy.Role)))
                {
                    long distance = Math.Max(1, FixedMath.IntegerSqrt(distance2));
                    long speed = enemy.Role != MonsterRole.None ? MonsterRoles.SpeedMmPerTick(enemy.Role, enemy.Family) : (enemy.Kind >= 2 ? 190 : MonsterSpeedMmPerTick);
                    long nx = enemy.Xmm + (PlayerXmm - enemy.Xmm) * speed / distance;
                    long nz = enemy.Zmm + (PlayerZmm - enemy.Zmm) * speed / distance;
                    if (CanStand(nx, nz)) { enemy.Xmm = nx; enemy.Zmm = nz; }
                    else if (CanStand(nx, enemy.Zmm)) enemy.Xmm = nx;
                    else if (CanStand(enemy.Xmm, nz)) enemy.Zmm = nz;
                }
                else if (enemy.Role != MonsterRole.None && MonsterRoles.TelegraphTicks(enemy.Role) > 0)
                {
                    if (enemy.AttackCooldownTicks == 0)
                    {
                        enemy.BossWindupTicks = MonsterRoles.TelegraphTicks(enemy.Role);
                        enemy.AttackCooldownTicks = MonsterRoles.AttackCooldownTicks(enemy.Role);
                        continue;
                    }
                }
                else if (enemy.AttackCooldownTicks == 0 && CombatV2)
                {
                    // Combat v2 (issue #127 phase 4 start values): no unannounced blow; the swing winds up first and the
                    // whole attack keeps the old rhythm (windup + impact + recovery).
                    int windup = CombatV2Rules.EnemyWindupTicks(enemy.Role, enemy.Kind);
                    enemy.BossWindupTicks = windup;
                    enemy.AttackCooldownTicks = windup + 1 + CombatV2Rules.EnemyRecoveryTicks(enemy.Role, enemy.Kind);
                }
                else if (enemy.AttackCooldownTicks == 0)
                {
                    int damage = enemy.Role != MonsterRole.None ? MonsterRoles.AttackDamage(enemy.Role) : (enemy.Kind >= 2 ? 105 : (enemy.Kind == 1 ? 60 : 40));
                    enemy.AttackCooldownTicks = enemy.Role != MonsterRole.None ? MonsterRoles.AttackCooldownTicks(enemy.Role) : (enemy.Kind >= 2 ? 12 : 17);
                    enemy.LastImmediateStrikeTarget = (PlayerXmm, PlayerZmm);
                    ReceiveCreatureHit(enemy, damage);
                    if (GameOver) break;
                }
            }
            if (CombatV2) AdvanceCombatAfterTick();
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
            // Weapon strikes (Klinge, Wucht) can miss with a bow until its skill makes it accurate.
            int accuracy = slot <= 1 ? Progression.WeaponAccuracyBp : 10000;
            if (accuracy < 10000 && _rng.NextBasisPoints() >= accuracy) { result.Misses++; return; }
            bool crit;
            var stats = new CombatStats(Progression, slot);
            long damage = enemy.Role != MonsterRole.None
                ? CombatRules.DamageWithArmor(stats, powerBp, MonsterRoles.ArmorBp(enemy.Role), _rng, out crit)
                : CombatRules.Damage(stats, powerBp, enemy.Kind, _rng, out crit);
            int appliedDamage = (int)Math.Min(enemy.Health, damage);
            enemy.Health = (int)Math.Max(0, enemy.Health - damage);
            RememberCreatureAssault(enemy,"player");
            PlayerHealth = Math.Min(MaxPlayerHealth,PlayerHealth+(int)((long)appliedDamage*Progression.Equipment.Bonus(WeaponAffix.Siphon)/10000));
            Progression.RecordPractice(slot, PracticeRules.ExperienceFromEffectiveAmount(appliedDamage));
            int weaponLevels = Progression.RecordWeaponPractice(appliedDamage);
            if (weaponLevels > 0) { result.WeaponLevelUps += weaponLevels; result.TrainedFamily = Progression.TrainingFamily; }
            result.TotalDamage = CombatRules.AddReportedDamage(result.TotalDamage, damage);
            result.Hits++;
            result.HitMonsterIds.Add(enemy.Id);
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

        private void ResolveMonsterDefeat(MonsterState enemy,SkillResult result,bool playerCredit=true)
        {
            if (enemy.IsDead && !enemy.LootGranted)
            {
                enemy.LootGranted = true;
                if(playerCredit)
                {
                    result.Kills++;Kills++;Progression.RecordKill();
                    Progression.EarnExperience(new System.Numerics.BigInteger(enemy.Kind >= 2 ? 1200 : (enemy.Kind == 1 ? 410 : 160)));
                }
                else Progression.RecordCompanionKill();
                // Generated places roll with their own seed. They used revision 1 whatever catalog the world was given;
                // a revision-2 world now rolls everything with its catalog, and the crypt floors count as depth.
                bool place = InJourney || InDungeon || InForest, second = LootRevisionTwo;
                LootCatalog catalog = second ? _lootCatalog : place ? LootCatalog.Version1 : _lootCatalog;
                ulong lootSeed = InJourney ? JourneySeed : InForest ? Progression.Woodland.Seed : InDungeon ? Progression.Dungeon.Seed : _runSeed;
                System.Numerics.BigInteger lootDepth = InJourney ? (InJourneyCave ? Progression.Journey.Depth : 1) :
                    InDungeon && second ? Progression.Dungeon.Depth : CryptDepth;
                // The woodland lets a returning character find the same unique piece after leaving the entry crypt.
                bool knightSource = !InJourney && enemy.Kind == 1 && enemy.Family == MonsterFamily.Legacy &&
                    (InForest || (Progression.EntryAct == 1 && enemy.Id == 2));
                bool pauldron = knightSource && Progression.Equipment.Active &&
                    !Progression.Equipment.FieldPauldronOwned && !GroundLoot.Exists(l => l.HasFieldPauldron);
                // The knight who carries the field pauldron always drops under revision 2: the piece rides on his drop.
                int flags = second && pauldron ? LootCatalog.GuaranteedFlag : 0;
                LootItem item = catalog == null ? null : catalog.GenerateFlagged(lootSeed, lootDepth, CurrentTick, enemy.Id, enemy.Kind, Progression.LootOrdinal, flags);
                if (!second || item != null)
                {
                    var drop = new LootState { Id = place ? enemy.Id : _nextLootId++, Xmm = enemy.Xmm, Zmm = enemy.Zmm,
                        WeaponBonus = enemy.Kind >= 2 ? 34 : (enemy.Kind == 1 ? 17 : 8), Item = item };
                    drop.HasFieldPauldron = pauldron && drop.Item != null;
                    GroundLoot.Add(drop);
                }
                if (second) DropSupply(lootSeed, lootDepth, enemy.Id, enemy.Kind, flags, enemy.Xmm, enemy.Zmm);
            }
        }

        /// <summary>Execute a shipped skill contract using the existing cooldown, resource and target owners.</summary>
        public SkillResult ActivateSkill(int slot)
        {
            var result = new SkillResult();
            if (GameOver) { result.Message = "Defeated - restart (R)"; return result; }
            if (slot < 0 || slot >= SkillCatalog.Count) { result.Message = "Unknown ability"; return result; }
            // Combat v2 starts a windup here; the hit lands on the action's impact tick inside Step.
            if (CombatV2) return StartSkillAction(slot);
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
            if (nearest == null && AschbeterInReach(range))
            {
                // The Aschbeter is no creature of the crypt: his own rules decide the swing on his next tick.
                Resource = Math.Min(100, Resource - skill.FocusCost + skill.FocusGain);
                Cooldowns[slot] = AttackCooldown(slot);
                SwingAtAschbeter(range);
                result.Activated = true; result.HasTarget = true; result.TargetXmm = Aschbeter.Xmm; result.TargetZmm = Aschbeter.Zmm;
                result.Message = skill.DisplayName + ": gegen den Aschbeter";
                return result;
            }
            if (nearest == null) { result.Message = "No enemy within reach"; return result; }
            Resource = Math.Min(100, Resource - skill.FocusCost + skill.FocusGain);
            Cooldowns[slot] = AttackCooldown(slot);
            result.Activated = true;
            ApplySkillHits(skill, slot, nearest, range, result);
            return result;
        }

        /// <summary>Deal one skill's damage to its nearest target, or to everything in range for an area skill.</summary>
        private void ApplySkillHits(SkillDefinition skill, int slot, MonsterState nearest, long range, SkillResult result)
        {
            result.HasTarget = true; result.TargetXmm = nearest.Xmm; result.TargetZmm = nearest.Zmm;
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
            if (result.Misses > 0 && result.Hits == 0) result.Message = skill.DisplayName + ": verfehlt";
            if (result.WeaponLevelUps > 0 && result.TrainedFamily.HasValue)
                result.Message += " · " + WeaponMasteryRules.SkillName(result.TrainedFamily.Value) + " Stufe " +
                    Progression.WeaponMastery.Level(result.TrainedFamily.Value);
        }

        /// <summary>Collect reachable drops through the canonical progression owner, preserving legacy rewards.</summary>
        public bool TryCollectLoot()
        {
            LastSupply = null; LastLoot = null; LastGearFind = RegionalRelic.None;
            if (GameOver) return false;
            for (int i = 0; i < GroundLoot.Count; i++)
            {
                LootState item = GroundLoot[i];
                if (DistanceSquared(PlayerXmm, PlayerZmm, item.Xmm, item.Zmm) > FixedMath.Square(PickupRadiusMm)) continue;
                if (item.Item != null)
                {
                    if (!Progression.CollectItem(item.Item)) return false;
                    LastLoot = item.Item;
                    // One find per loot: the drop that carries the field pauldron carries nothing else.
                    if (item.HasFieldPauldron) Progression.Equipment.CollectFieldPauldron(item.Item);
                    else
                    {
                        FindAschenwachtGear(item.Item);
                        if (LastGearFind == RegionalRelic.None) FindNachtvesper(item.Item);
                        if (LastGearFind == RegionalRelic.None) FindHunterAndCantorSets(item.Item);
                    }
                }
                else Progression.AddWeaponUpgrade(item.WeaponBonus);
                GroundLoot.RemoveAt(i);
                SaveWoodlandPlayer();
                return true;
            }
            return TryCollectSupply();
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
                // Role and family decide behaviour for new monsters; only hash them when set, so legacy monsters
                // keep the exact golden hash.
                if (enemy.Role != MonsterRole.None || enemy.Family != MonsterFamily.Legacy)
                {
                    Mix(ref h, 0x524F4C45UL); Mix(ref h, (ulong)enemy.Role); Mix(ref h, (ulong)enemy.Family);
                }
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
                if (loot.HasFieldPauldron) Mix(ref h, 0x46454C4453434831UL); // "FELDSCH1"
            }
            if (_lootCatalog != null)
            {
                Mix(ref h, 1); Mix(ref h, _lootCatalog.StableHash); Mix(ref h, _runSeed);
                foreach (LootState loot in GroundLoot) Mix(ref h, loot.Item == null ? 0 : loot.Item.StableHash);
            }
            MixEconomy(ref h);
            MixHouses(ref h);
            MixCombatV2(ref h); // armor and combat v2 only when present: legacy worlds keep their golden hashes
            MixHealing(ref h); // Orsel's course only when present
            MixSupplies(ref h); // supply bags only while some lie on the ground
            MixAschbeter(ref h); // the Aschbeter only while he stands in Aschenwacht
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
