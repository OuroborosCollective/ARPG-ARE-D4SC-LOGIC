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
            return "1," + (Wounded ? "1" : "0") + "," + RecoveryTicks.ToString(CultureInfo.InvariantCulture) +
                "," + (Rescued ? "1" : "0") + "," + (ResumeFollowing ? "1" : "0");
        }

        internal static bool TryRead(string token, out CompanionRecoveryState state)
        {
            state = null;
            if (string.IsNullOrEmpty(token)) return false;
            string[] f = token.Split(',');
            if (f.Length != 5 || f[0] != "1" || (f[1] != "0" && f[1] != "1") || (f[3] != "0" && f[3] != "1") || (f[4] != "0" && f[4] != "1")) return false;
            long ticks;
            if (!long.TryParse(f[2], NumberStyles.None, CultureInfo.InvariantCulture, out ticks) || ticks < 0 ||
                ticks.ToString(CultureInfo.InvariantCulture) != f[2]) return false;
            if (f[1] == "0" && (ticks != 0 || f[3] == "1" || f[4] == "1")) return false; // no countdown, rescue or resume without a wound
            state = new CompanionRecoveryState { Wounded = f[1] == "1", RecoveryTicks = (int)ticks, Rescued = f[3] == "1", ResumeFollowing = f[4] == "1" };
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
            if (_companionRecovery.Wounded) SetSection(CompanionRecoverySectionName, _companionRecovery.Token());
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
