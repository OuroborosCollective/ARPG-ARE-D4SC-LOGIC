using System;
using System.Globalization;
using System.Linq;

namespace AreArpg.Core
{
    /// <summary>Single blind den inhabitant; persisted by the existing character owner, never by Unity AI.</summary>
    public sealed class RattlerEncounter
    {
        public const int MaximumHealth = 5200;
        public const long HearingRadius = 14000, SmellRadius = 2800, StrikeRadius = 2600;
        public const string RoyalBrandId = "immergruen.royal.brand";
        public readonly MonsterState Creature = new MonsterState { Id = 42, Kind = 5, Xmm = 0, Zmm = 8000,
            PreviousXmm = 0, PreviousZmm = 8000, Health = MaximumHealth, MaxHealth = MaximumHealth };
        public bool Active { get; internal set; }
        public bool Sneaking { get; internal set; }
        public bool BrandExamined { get; internal set; }
        public bool Reported { get; internal set; }
        public int MemoryTicks { get; internal set; }
        public long TargetX { get; internal set; }
        public long TargetZ { get; internal set; } = 8000;
        public long StrikeX { get; internal set; }
        public long StrikeZ { get; internal set; }

        internal void RestartDungeonRun()
        {
            Creature.Health=MaximumHealth; Creature.LootGranted=false; Creature.AttackCooldownTicks=Creature.BossWindupTicks=0;
            Creature.Xmm=Creature.PreviousXmm=0; Creature.Zmm=Creature.PreviousZmm=17000;
            TargetX=StrikeX=StrikeZ=0; TargetZ=17000; MemoryTicks=0; Sneaking=false;
        }

        internal string Token()
        {
            object[] f = { Active ? 1 : 0, Sneaking ? 1 : 0, BrandExamined ? 1 : 0, Reported ? 1 : 0,
                Creature.Xmm, Creature.Zmm, Creature.Health, Creature.AttackCooldownTicks, Creature.BossWindupTicks,
                Creature.LootGranted ? 1 : 0, MemoryTicks, TargetX, TargetZ, StrikeX, StrikeZ };
            return string.Join(",", f.Select(v => Convert.ToString(v, CultureInfo.InvariantCulture)));
        }

        internal static bool TryRead(string token, out RattlerEncounter result, CryptDungeonLayout dungeonLayout = null)
        {
            result = null; var f = token.Split(','); if (f.Length != 15) return false;
            var n = new long[15];
            for (int i = 0; i < n.Length; i++)
                if (!long.TryParse(f[i], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out n[i]) ||
                    n[i].ToString(CultureInfo.InvariantCulture) != f[i]) return false;
            if (n[0] != 1 || n[1] < 0 || n[1] > 1 || n[2] < 0 || n[2] > 1 || n[3] < 0 || n[3] > n[2] ||
                n[6] < 0 || n[6] > MaximumHealth || n[7] < 0 || n[7] > 14 || n[8] < 0 || n[8] > 10 ||
                n[9] < 0 || n[9] > 1 || (n[6] == 0) != (n[9] == 1) ||
                (n[6] == 0 && n[8] != 0) || (n[2] == 1 && n[6] != 0 && dungeonLayout == null) ||
                // A windup freezes its strike point, so it may legitimately outlive perception memory (MemoryTicks 0).
                n[10] < 0 || n[10] > 60) return false;
            foreach (int i in new[] { 4, 11, 13 })
                if (!(dungeonLayout != null ? dungeonLayout.CanStand(n[i], n[i + 1]) : ArenaSimulation.CanStandInRattlerDen(n[i], n[i + 1]))) return false;
            var r = new RattlerEncounter { Active = true, Sneaking = n[1] == 1, BrandExamined = n[2] == 1,
                Reported = n[3] == 1, MemoryTicks = (int)n[10], TargetX = n[11], TargetZ = n[12], StrikeX = n[13], StrikeZ = n[14] };
            r.Creature.Xmm = r.Creature.PreviousXmm = n[4]; r.Creature.Zmm = r.Creature.PreviousZmm = n[5];
            r.Creature.Health = (int)n[6]; r.Creature.AttackCooldownTicks = (int)n[7];
            r.Creature.BossWindupTicks = (int)n[8]; r.Creature.LootGranted = n[9] == 1;
            result = r; return true;
        }
    }

    public sealed partial class CharacterProgression
    {
        public RattlerEncounter Rattler { get; private set; } = new RattlerEncounter();
    }

    public sealed partial class ArenaSimulation
    {
        /// <summary>The den has an unobstructed central floor, matching its visible walls and sealed north end.</summary>
        public static bool CanStandInRattlerDen(long x, long z)
        {
            // Direct bounds instead of Math.Abs: long.MinValue from a damaged save must be rejected, not throw.
            const long halfWidth = 12000 - PlayerRadiusMm;
            return x >= -halfWidth && x <= halfWidth && z >= -ArenaLimitMm && z <= 20500 - PlayerRadiusMm;
        }

        private void HearRattlerNoise()
        {
            var r = Progression.Rattler; var m = r.Creature;
            if (Progression.EntryAct != 7 || m.IsDead ||
                DistanceSquared(m.Xmm, m.Zmm, PlayerXmm, PlayerZmm) > FixedMath.Square(RattlerEncounter.HearingRadius)) return;
            r.TargetX = PlayerXmm; r.TargetZ = PlayerZmm; r.MemoryTicks = 60;
        }

        /// <summary>No sight query: actual footsteps, attack noise and nearby scent update a last perceived position.</summary>
        private void StepRattler()
        {
            if (Progression.EntryAct != 7 || GameOver) return;
            var r = Progression.Rattler; var m = r.Creature; if (m.IsDead) return;
            if (r.MemoryTicks > 0) r.MemoryTicks--;
            if (m.AttackCooldownTicks > 0) m.AttackCooldownTicks--;
            bool moved = PlayerXmm != PrevPlayerXmm || PlayerZmm != PrevPlayerZmm;
            if ((!r.Sneaking && moved) || DistanceSquared(m.Xmm, m.Zmm, PlayerXmm, PlayerZmm) <= FixedMath.Square(RattlerEncounter.SmellRadius)) HearRattlerNoise();
            if (m.BossWindupTicks > 0)
            {
                m.BossWindupTicks--;
                if (m.BossWindupTicks == 0)
                {
                    if (DistanceSquared(PlayerXmm, PlayerZmm, r.StrikeX, r.StrikeZ) <= FixedMath.Square(RattlerEncounter.StrikeRadius) &&
                        (!InDungeon || Progression.Dungeon.Current.Layout.HasSight(m.Xmm, m.Zmm, PlayerXmm, PlayerZmm)))
                        ReceiveDamage(280);
                    m.AttackCooldownTicks = 14;
                }
                return;
            }
            // A lost trail returns to the authored feeding place, never to an unseen moving player.
            if (r.MemoryTicks == 0) { r.TargetX = 0; r.TargetZ = InDungeon ? 17000 : 8000; }
            long dx = r.TargetX - m.Xmm, dz = r.TargetZ - m.Zmm;
            long d2 = dx * dx + dz * dz;
            if (r.MemoryTicks > 0 && d2 <= FixedMath.Square(2100) && m.AttackCooldownTicks == 0)
            { r.StrikeX = r.TargetX; r.StrikeZ = r.TargetZ; m.BossWindupTicks = 10; return; }
            if (d2 <= FixedMath.Square(400)) return;
            long distance = Math.Max(1, FixedMath.IntegerSqrt(d2));
            long step = Math.Min(380, distance);
            long nx = m.Xmm + dx * step / distance, nz = m.Zmm + dz * step / distance;
            if (InDungeon ? CanStand(nx, nz) : CanStandInRattlerDen(nx, nz)) { m.Xmm = nx; m.Zmm = nz; }
        }

        private void FinishRattler(SkillResult result)
        {
            var m = Progression.Rattler.Creature;
            if (!m.IsDead || m.LootGranted) return;
            m.LootGranted = true; m.BossWindupTicks = 0;
            result.Kills++; Kills++; Progression.RecordKill(); Progression.EarnExperience(1200);
            if (InDungeon) GroundLoot.Add(new LootState { Id=42,Xmm=m.Xmm,Zmm=m.Zmm,WeaponBonus=34,
                Item=LootCatalog.Version1.Generate(Progression.Dungeon.Seed,CryptDepth,CurrentTick,42,2,Progression.LifetimeKills) });
            else Progression.AddWeaponUpgrade(12);
        }

        private SocialResult ActOnRattler(HungerAction action)
        {
            var r = Progression.Rattler; var c = Progression.Investigation;
            var result = new SocialResult { Text = "Gehe zum passenden Hinweis." };
            if (GameOver) return result;
            if (action == HungerAction.OpenInnerDoor && Progression.EntryAct == 6 && (!InDungeon || Progression.Dungeon.Depth == 2) && c.TraceFound && !r.Active &&
                DistanceSquared(PlayerXmm, PlayerZmm, 0, 15000) <= FixedMath.Square(NpcInteractionRadiusMm))
            {
                r.Active = true;
                if (InDungeon) r.RestartDungeonRun();
                result.Text = "Der Riegel gibt nach. Dahinter scharrt etwas. Leise Schritte vermeiden Laerm; aus der Naehe kann es dich riechen.";
            }
            else if (action == HungerAction.ToggleSneak && Progression.EntryAct == 7)
            { r.Sneaking = !r.Sneaking; result.Text = r.Sneaking ? "Du schleichst. Naher Geruch und Angriffe verraten dich weiterhin." : "Du gehst wieder schnell. Deine Schritte sind hoerbar."; }
            else if (action == HungerAction.InspectRoyalBrand && Progression.EntryAct == 7 && r.Creature.IsDead && !r.BrandExamined &&
                DistanceSquared(PlayerXmm, PlayerZmm, r.Creature.Xmm, r.Creature.Zmm) <= FixedMath.Square(NpcInteractionRadiusMm))
            { r.BrandExamined = true; result.Text = "Am Hals des blinden MaulwurfRattlers: das genaue Koenigswappen Immergruens. Das Tier stammt aus den tiefen Aschenglutfällen. Seine Anwesenheit und das Mal deuten auf Abrichtung. Ein Auftraggeber ist damit noch nicht bewiesen."; }
            else if (action == HungerAction.ReportRoyalBrand && Progression.EntryAct == 2 && r.BrandExamined && !r.Reported)
            {
                var npc = HungerContact(0); if (npc == null) return result;
                npc.InteractionCooldownTicks = 10; r.Reported = true; c.Reported = true;
                result.Text = "Ilyan: Immergruen? Oben gruene Wiesen, unten Asche. Alle duerfen handeln; wohnen nur die Reichen oder vom Koenig Erlaubte. Abends werfen sie selbst hungernde Kinder hinaus. Das Brandmal ist eine Spur. Wer das Tier auf wen abgerichtet hat, muessen wir erst beweisen.";
            }
            else return result;
            SaveInvestigationPlayer(); result.Accepted = true; return result;
        }

        private string RattlerObjective
        {
            get
            {
                var r = Progression.Rattler;
                if (r.Reported) return "Ilyan kennt das koenigliche Brandmal. Die Spur fuehrt nach Immergruen ueber den Aschenglutfällen. Abrichter, Zielgeruch und Auftrag bleiben unbewiesen.";
                if (r.BrandExamined) return "Das Halsbrandmal stimmt mit dem Wappen des Koenigs von Immergruen ueberein. Berichte Ilyan persoenlich.";
                if (r.Creature.IsDead) return "Untersuche das Brandmal am Hals des MaulwurfRattlers.";
                return "Hinter der inneren Tuer lauert ein blinder MaulwurfRattler. Schleichen vermeidet Schrittlaerm. Naher Geruch und Angriffe verraten dich. Weiche dem angekuendigten Hieb aus.";
            }
        }
    }
}
