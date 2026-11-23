using System;

namespace AreArpg.UnityClient
{
    /// <summary>What Google Play reported about a newer version of the installed game.</summary>
    public readonly struct PlayUpdateState
    {
        // Play's UpdateAvailability values.
        public const int UpdateNotAvailable = 1, UpdateAvailable = 2, UpdateInProgress = 3;

        public readonly bool Known;
        public readonly int Availability;
        public readonly bool ImmediateAllowed;
        public readonly int AvailableVersionCode;
        public readonly string Problem;

        public PlayUpdateState(int availability, bool immediateAllowed, int availableVersionCode)
        {
            Known = true; Availability = availability; ImmediateAllowed = immediateAllowed;
            AvailableVersionCode = availableVersionCode; Problem = null;
        }

        private PlayUpdateState(string problem)
        {
            Known = false; Availability = 0; ImmediateAllowed = false; AvailableVersionCode = 0;
            Problem = string.IsNullOrEmpty(problem) ? "unknown" : problem;
        }

        /// <summary>No answer from Play: not installed from Play, offline, or no Play services on the device.</summary>
        public static PlayUpdateState Failed(string problem) { return new PlayUpdateState(problem); }
    }

    public enum PlayUpdateAction { None, Offer, Resume }

    /// <summary>
    /// The update check at game start (owner request 2026-11-23): only when Google Play reports a newer version does the
    /// game offer it; an update the player already started is resumed. New content still reaches Play as a new bundle.
    /// </summary>
    public static class PlayUpdatePolicy
    {
        public const string Title = "Neue Version verfügbar";
        public const string Body = "Eine neuere Version von Ashen Crypt liegt bei Google Play bereit. Dein Spielstand bleibt erhalten.";
        public const string UpdateNow = "Jetzt aktualisieren";
        public const string Later = "Später";

        public static PlayUpdateAction Decide(PlayUpdateState state)
        {
            if (!state.Known || !state.ImmediateAllowed) return PlayUpdateAction.None;
            if (state.Availability == PlayUpdateState.UpdateInProgress) return PlayUpdateAction.Resume;
            return state.Availability == PlayUpdateState.UpdateAvailable ? PlayUpdateAction.Offer : PlayUpdateAction.None;
        }
    }

    /// <summary>Where the game asks Google Play; on Android the real Play Core client, in tests a fake.</summary>
    public interface IPlayUpdateSource
    {
        /// <summary>Ask once; <paramref name="done"/> is called on Unity's main thread.</summary>
        void Check(Action<PlayUpdateState> done);

        /// <summary>Start Play's full-screen update; Play downloads, installs and restarts the game.</summary>
        void StartUpdate();
    }
}
