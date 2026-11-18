using System;
using System.Globalization;

namespace AreArpg.Core
{
    /// <summary>
    /// Defeat consequences for a companion (#76): a hit that would leave the companion badly wounded (but not
    /// lethally zero) makes them withdraw. They can be rescued in person or treated; either way they stay
    /// unavailable for one to two game days and then return. A hit that reaches zero keeps the confirmed lethal
    /// death path. This state is its own AC22 save section.
    /// </summary>
    public sealed class CompanionRecoveryState
    {
        /// <summary>Health at or below this fraction counts as a defeat that withdraws the companion.</summary>
        public const int WoundThresholdHealth = 200;

        public bool Wounded { get; private set; }
        public int RecoveryTicks { get; private set; }
        /// <summary>True once this wound has been answered by a personal rescue; cleared on the next wound.</summary>
        public bool Rescued { get; private set; }
        /// <summary>Whether the companion was following before the wound, so recovery restores that status.</summary>
        public bool ResumeFollowing { get; private set; }
        /// <summary>How often Orsel has already revived this companion at the mill (0..MaxRevivals).</summary>
        public int RevivalsUsed { get; private set; }
        /// <summary>True after the last allowed death; only the holy revive cloth can bring them back.</summary>
        public bool PermanentlyDead { get; private set; }

        /// <summary>Orsel revives a fallen companion this many times; the next death is permanent.</summary>
        public const int MaxRevivals = 3;

        /// <summary>Withdraw a companion after a non-lethal defeat. Returns false if already wounded or dead.</summary>
        internal bool Wound(CompanionState companion, int recoveryDays)
        {
            if (companion == null || companion.Health <= 0 || companion.Status == CompanionStatus.Dead) return false;
            if (Wounded) return false;
            Wounded = true;
            Rescued = false;
            ResumeFollowing = companion.Status == CompanionStatus.Following;
            companion.Status = CompanionStatus.Waiting;
            companion.TargetId = -1;
            companion.AttackFlash = 0;
            int days = Math.Max(1, Math.Min(2, recoveryDays));
            RecoveryTicks = days * (int)WorldSkyClock.TicksPerDay;
            return true;
        }

        /// <summary>Record a personal rescue once per wound; a second rescue is refused.</summary>
        internal bool MarkRescued()
        {
            if (!Wounded || Rescued) return false;
            Rescued = true;
            return true;
        }

        /// <summary>Called when a companion dies: after the last allowed revival the death is permanent.</summary>
        internal void MarkDeath()
        {
            if (RevivalsUsed >= MaxRevivals) PermanentlyDead = true;
        }

        /// <summary>Orsel revives a fallen (not permanently dead) companion at the mill sickbed, awaiting pickup.</summary>
        internal bool ReviveAtMill(CompanionState companion)
        {
            if (companion == null || PermanentlyDead || companion.Status != CompanionStatus.Dead) return false;
            if (RevivalsUsed >= MaxRevivals) return false;
            RevivalsUsed++;
            companion.Health = CompanionState.MaximumHealth / 2;
            companion.Status = CompanionStatus.Waiting;
            companion.TargetId = -1;
            return true;
        }

        /// <summary>The holy revive cloth brings a permanently dead companion back, once crafted.</summary>
        internal bool ReviveFromPermanentDeath(CompanionState companion)
        {
            if (companion == null || !PermanentlyDead) return false;
            PermanentlyDead = false;
            companion.Health = CompanionState.MaximumHealth / 2;
            companion.Status = CompanionStatus.Waiting;
            companion.TargetId = -1;
            return true;
        }

        /// <summary>Advance the recovery once per 100-ms tick; the companion returns when it reaches zero.</summary>
        internal void AdvanceTick(CompanionState companion)
        {
            if (!Wounded) return;
            if (RecoveryTicks > 0) RecoveryTicks--;
            if (RecoveryTicks == 0)
            {
                Wounded = false;
                if (companion != null && companion.Status != CompanionStatus.Dead)
                {
                    companion.Health = Math.Max(CompanionState.MaximumHealth / 2, companion.Health);
                    // Only a companion who was following returns to following; a dismissed one stays put.
                    if (companion.HasJoined && ResumeFollowing) companion.Status = CompanionStatus.Following;
                }
            }
        }

        internal string Token()
        {
            // Keep the version-1 layout until a v2-only value exists, so shipped #76 saves round-trip byte-for-byte.
            bool v2 = RevivalsUsed != 0 || PermanentlyDead;
            if (!v2)
                return "1," + (Wounded ? "1" : "0") + "," + RecoveryTicks.ToString(CultureInfo.InvariantCulture) +
                    "," + (Rescued ? "1" : "0") + "," + (ResumeFollowing ? "1" : "0");
            return "2," + (Wounded ? "1" : "0") + "," + RecoveryTicks.ToString(CultureInfo.InvariantCulture) +
                "," + (Rescued ? "1" : "0") + "," + (ResumeFollowing ? "1" : "0") +
                "," + RevivalsUsed.ToString(CultureInfo.InvariantCulture) + "," + (PermanentlyDead ? "1" : "0");
        }

        internal static bool TryRead(string token, out CompanionRecoveryState state)
        {
            state = null;
            if (string.IsNullOrEmpty(token)) return false;
            string[] f = token.Split(',');
            // Version 1 (five fields) is the original #76 form; version 2 adds revivals and permanent death.
            if (f.Length == 5 && f[0] == "1")
            {
                if ((f[1] != "0" && f[1] != "1") || (f[3] != "0" && f[3] != "1") || (f[4] != "0" && f[4] != "1")) return false;
                long oldTicks;
                if (!long.TryParse(f[2], NumberStyles.None, CultureInfo.InvariantCulture, out oldTicks) || oldTicks < 0 ||
                    oldTicks.ToString(CultureInfo.InvariantCulture) != f[2]) return false;
                if (f[1] == "0" && (oldTicks != 0 || f[3] == "1" || f[4] == "1")) return false;
                state = new CompanionRecoveryState { Wounded = f[1] == "1", RecoveryTicks = (int)oldTicks, Rescued = f[3] == "1", ResumeFollowing = f[4] == "1" };
                return true;
            }
            if (f.Length != 7 || f[0] != "2" || (f[1] != "0" && f[1] != "1") || (f[3] != "0" && f[3] != "1") ||
                (f[4] != "0" && f[4] != "1") || (f[6] != "0" && f[6] != "1")) return false;
            long ticks, revivals;
            if (!long.TryParse(f[2], NumberStyles.None, CultureInfo.InvariantCulture, out ticks) || ticks < 0 ||
                ticks.ToString(CultureInfo.InvariantCulture) != f[2]) return false;
            if (!long.TryParse(f[5], NumberStyles.None, CultureInfo.InvariantCulture, out revivals) || revivals < 0 || revivals > MaxRevivals ||
                revivals.ToString(CultureInfo.InvariantCulture) != f[5]) return false;
            if (f[1] == "0" && (ticks != 0 || f[3] == "1" || f[4] == "1")) return false; // no countdown, rescue or resume without a wound
            state = new CompanionRecoveryState { Wounded = f[1] == "1", RecoveryTicks = (int)ticks, Rescued = f[3] == "1",
                ResumeFollowing = f[4] == "1", RevivalsUsed = (int)revivals, PermanentlyDead = f[6] == "1" };
            return true;
        }
    }

    public sealed partial class CharacterProgression
    {
        private CompanionRecoveryState _companionRecovery = new CompanionRecoveryState();
        public CompanionRecoveryState CompanionRecovery { get { return _companionRecovery; } }
        private const string CompanionRecoverySectionName = "companion";

        private void SyncCompanionRecoverySection()
        {
            // Persist whenever there is real recovery state: a wound, used revivals or permanent death.
            if (_companionRecovery.Wounded || _companionRecovery.RevivalsUsed > 0 || _companionRecovery.PermanentlyDead)
                SetSection(CompanionRecoverySectionName, _companionRecovery.Token());
            else _sections.Remove(CompanionRecoverySectionName);
        }

        private void RestoreCompanionRecoverySection()
        {
            string token;
            if (_sections.TryGetValue(CompanionRecoverySectionName, out token) && CompanionRecoveryState.TryRead(token, out var state))
                _companionRecovery = state;
        }
    }
}
