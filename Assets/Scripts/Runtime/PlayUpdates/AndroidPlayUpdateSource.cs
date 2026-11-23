#if UNITY_ANDROID
using System;
using UnityEngine;
using UnityEngine.Android;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// Google Play In-App Updates (com.google.android.play:app-update, added to the Gradle project by the editor build
    /// step). Play's callbacks arrive on the Java UI thread; their values are read there and handed to Unity's main thread.
    /// </summary>
    public sealed class AndroidPlayUpdateSource : IPlayUpdateSource
    {
        private const int Immediate = 1;     // AppUpdateType.IMMEDIATE
        private const int RequestCode = 47011;
        private AndroidJavaObject _manager, _info;

        public void Check(Action<PlayUpdateState> done)
        {
            try
            {
                using (var factory = new AndroidJavaClass("com.google.android.play.core.appupdate.AppUpdateManagerFactory"))
                    _manager = factory.CallStatic<AndroidJavaObject>("create", AndroidApplication.currentActivity);
                using (AndroidJavaObject task = _manager.Call<AndroidJavaObject>("getAppUpdateInfo"))
                {
                    task.Call<AndroidJavaObject>("addOnSuccessListener", new SuccessListener(info =>
                    {
                        _info = info;
                        var state = new PlayUpdateState(info.Call<int>("updateAvailability"),
                            info.Call<bool>("isUpdateTypeAllowed", Immediate), info.Call<int>("availableVersionCode"));
                        AndroidApplication.InvokeOnUnityMainThread(() => done(state));
                    }));
                    task.Call<AndroidJavaObject>("addOnFailureListener", new FailureListener(message =>
                        AndroidApplication.InvokeOnUnityMainThread(() => done(PlayUpdateState.Failed(message)))));
                }
            }
            catch (Exception e)
            {
                // No Play Core in this build or no Play services on the device: the game simply starts.
                done(PlayUpdateState.Failed(e.GetType().Name + ": " + e.Message));
            }
        }

        public void StartUpdate()
        {
            if (_manager == null || _info == null) return;
            AndroidJavaObject manager = _manager, info = _info;
            AndroidApplication.InvokeOnUIThread(() =>
            {
                try
                {
                    using (var options = new AndroidJavaClass("com.google.android.play.core.appupdate.AppUpdateOptions"))
                    using (AndroidJavaObject immediate = options.CallStatic<AndroidJavaObject>("defaultOptions", Immediate))
                        manager.Call<bool>("startUpdateFlowForResult", info, AndroidApplication.currentActivity, immediate, RequestCode);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Play update could not start: " + e.Message);
                }
            });
        }

        private sealed class SuccessListener : AndroidJavaProxy
        {
            private readonly Action<AndroidJavaObject> _done;
            public SuccessListener(Action<AndroidJavaObject> done) : base("com.google.android.gms.tasks.OnSuccessListener") { _done = done; }
            public void onSuccess(AndroidJavaObject result) { _done(result); }
        }

        private sealed class FailureListener : AndroidJavaProxy
        {
            private readonly Action<string> _done;
            public FailureListener(Action<string> done) : base("com.google.android.gms.tasks.OnFailureListener") { _done = done; }
            public void onFailure(AndroidJavaObject error)
            {
                string message = null;
                try { message = error != null ? error.Call<string>("getMessage") : null; } catch (Exception) { }
                _done(message ?? "Play update check failed");
            }
        }
    }
}
#endif
