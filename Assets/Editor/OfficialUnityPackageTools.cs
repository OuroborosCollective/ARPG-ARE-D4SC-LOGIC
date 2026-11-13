using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace AreArpg.EditorTools
{
    /// <summary>Explicit editor tooling only. UPM owns dependency resolution; no gameplay services are started.</summary>
    public static class OfficialUnityPackageTools
    {
        private const string Logcat = "com.unity.mobile.android-logcat";
        private static AddRequest _request;
        private static double _deadline;

        [MenuItem("ARE ARPG/Tools/Install Android Logcat")]
        public static void InstallAndroidDiagnostics()
        {
            if (_request != null) throw new InvalidOperationException("Package installation already running.");
            _request = Client.Add(Logcat + "@1.4.7");
            _deadline = EditorApplication.timeSinceStartup + 300;
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (!_request.IsCompleted && EditorApplication.timeSinceStartup < _deadline) return;
            EditorApplication.update -= Poll;
            bool success = _request.IsCompleted && _request.Status == StatusCode.Success;
            string detail = success ? _request.Result.name + "@" + _request.Result.version :
                _request.Error?.message ?? "UPM resolution timed out; inspect package state before retrying.";
            _request = null;
            if (success) Debug.Log("ARE_TOOLING_PACKAGE " + detail);
            else Debug.LogError("ARE_TOOLING_PACKAGE " + detail);
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
        }

        /// <summary>Run after UPM resolution/recompile. This proves editor loading, not an attached Android device.</summary>
        public static void VerifyAndroidDiagnostics()
        {
            try
            {
                var package = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Single(p => p.name == Logcat);
                if (package.version != "1.4.7") throw new InvalidOperationException("Unexpected Logcat version.");
                var assembly = System.Reflection.Assembly.Load("Unity.Mobile.AndroidLogcat.Editor");
                var type = assembly.GetType("Unity.Android.Logcat.AndroidLogcatConsoleWindow", true);
                if (!typeof(EditorWindow).IsAssignableFrom(type)) throw new InvalidOperationException("Logcat window type is invalid.");
                string report = JsonUtility.ToJson(new Receipt
                {
                    unity = Application.unityVersion,
                    package = package.name,
                    version = package.version,
                    assembly = assembly.GetName().Name,
                    windowType = type.FullName,
                    deviceExecution = "NOT_TESTED"
                }, true);
                string path = Environment.GetEnvironmentVariable("ARE_TOOLING_REPORT");
                if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, report);
                Debug.Log("ARE_TOOLING_VERIFIED " + report);
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        [Serializable]
        private sealed class Receipt
        {
            public string unity, package, version, assembly, windowType, deviceExecution;
        }
    }
}
