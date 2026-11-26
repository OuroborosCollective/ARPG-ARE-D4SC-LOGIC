using System;
using UnityEngine;
using UnityEngine.Android;

namespace AreArpg.UnityClient
{
    public sealed partial class ArenaRuntime
    {
        /// <summary>
        /// Owner link 2026-11-26 (Unity 6000.6, optimization for Android). Three hints for the operating system, on Android
        /// only:
        /// - the game says once that it is gameplay that may be interrupted (game state hinting, Android 13+), on top of the
        ///   loading hints Unity gives by itself;
        /// - in the tablet's battery game mode the game runs at <see cref="BatteryFrameRate"/> frames a second instead of 60;
        /// - "fully drawn" is reported on the first frame the wanderer can play, not when the first scene starts loading
        ///   (the game builds its world and hero after that), so Android times and prioritises the real start.
        /// </summary>
        public const int BatteryFrameRate = 30;
        private bool _fullyDrawnReported;

        private void ApplyAndroidHints()
        {
            if (Application.platform != RuntimePlatform.Android) return;
            try
            {
                AndroidGame.Automatic.SetGameState(AndroidGameState.GamePlayInterruptible);
                if (AndroidGame.GameMode == AndroidGameMode.Battery) Application.targetFrameRate = BatteryFrameRate;
            }
            catch (Exception error) { Debug.LogWarning("Ashen Crypt: Android hints unavailable: " + error.Message); }
        }

        private void ReportFullyDrawnWhenPlayable()
        {
            if (_fullyDrawnReported || _game == null || _hero == null) return;
            _fullyDrawnReported = true;
            if (Application.platform != RuntimePlatform.Android) return;
            try { DiagnosticsReporting.CallReportFullyDrawn(); }
            catch (Exception error) { Debug.LogWarning("Ashen Crypt: report fully drawn failed: " + error.Message); }
        }
    }
}
