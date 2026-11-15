using System;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Fadenwerk.EditorTools
{
    /// <summary>
    /// Einstellungen pro Projekt und Rechner. Liegen in den EditorPrefs (nicht im Projekt),
    /// damit der Zugangsschlüssel nie in der Versionsverwaltung landet.
    /// </summary>
    internal static class FadenwerkSettings
    {
        static string _prefix;

        static string P
        {
            get
            {
                if (_prefix == null)
                    _prefix = "Fadenwerk." + FwUtil.Hash64(Application.dataPath).Substring(0, 10) + ".";
                return _prefix;
            }
        }

        public static int Port
        {
            get { return Mathf.Clamp(EditorPrefs.GetInt(P + "port", 8095), 1024, 65535); }
            set { int v = Mathf.Clamp(value, 1024, 65535); if (!EditorPrefs.HasKey(P + "port") || EditorPrefs.GetInt(P + "port") != v) EditorPrefs.SetInt(P + "port", v); }
        }

        /// <summary>false = nur 127.0.0.1/localhost (für SSH-Tunnel oder Reverse-Proxy), true = alle Netzwerkschnittstellen.</summary>
        public static bool BindAll
        {
            get { return EditorPrefs.GetBool(P + "bindAll", false); }
            set { if (EditorPrefs.GetBool(P + "bindAll", !value) != value || !EditorPrefs.HasKey(P + "bindAll")) EditorPrefs.SetBool(P + "bindAll", value); }
        }

        public static bool AutoStart
        {
            get { return EditorPrefs.GetBool(P + "autoStart", false); }
            set { if (EditorPrefs.GetBool(P + "autoStart", !value) != value || !EditorPrefs.HasKey(P + "autoStart")) EditorPrefs.SetBool(P + "autoStart", value); }
        }

        /// <summary>Szenen und Datenbank nach jeder Änderung aus dem Browser sofort speichern.</summary>
        public static bool AutoSave
        {
            get { return EditorPrefs.GetBool(P + "autoSave", true); }
            set { if (EditorPrefs.GetBool(P + "autoSave", !value) != value || !EditorPrefs.HasKey(P + "autoSave")) EditorPrefs.SetBool(P + "autoSave", value); }
        }

        /// <summary>Verbindungslinien NPC → Ziel in der Scene View zeigen.</summary>
        public static bool SceneOverlay
        {
            get { return EditorPrefs.GetBool(P + "sceneOverlay", true); }
            set { if (EditorPrefs.GetBool(P + "sceneOverlay", !value) != value || !EditorPrefs.HasKey(P + "sceneOverlay")) EditorPrefs.SetBool(P + "sceneOverlay", value); }
        }

        /// <summary>false = Boden ist XZ (3D), true = XY (2D-Spiel).</summary>
        public static bool PlaneXY
        {
            get { return EditorPrefs.GetBool(P + "planeXY", false); }
            set { if (EditorPrefs.GetBool(P + "planeXY", !value) != value || !EditorPrefs.HasKey(P + "planeXY")) EditorPrefs.SetBool(P + "planeXY", value); }
        }

        /// <summary>Beim Verschieben aus dem Browser auf Terrain oder Collider setzen (nur 3D).</summary>
        public static bool SnapToGround
        {
            get { return EditorPrefs.GetBool(P + "snapGround", true); }
            set { if (EditorPrefs.GetBool(P + "snapGround", !value) != value || !EditorPrefs.HasKey(P + "snapGround")) EditorPrefs.SetBool(P + "snapGround", value); }
        }

        /// <summary>Adresse, unter der du die Oberfläche im Browser öffnest (nur für den Link im Fenster).</summary>
        public static string PublicHost
        {
            get { return EditorPrefs.GetString(P + "publicHost", ""); }
            set { EditorPrefs.SetString(P + "publicHost", value ?? ""); }
        }

        public static string DatabaseGuid
        {
            get { return EditorPrefs.GetString(P + "dbGuid", ""); }
            set { EditorPrefs.SetString(P + "dbGuid", value ?? ""); }
        }

        public static string Token
        {
            get
            {
                string t = EditorPrefs.GetString(P + "token", "");
                if (string.IsNullOrEmpty(t) || t.Length < 24)
                {
                    t = NewToken();
                    EditorPrefs.SetString(P + "token", t);
                }
                return t;
            }
        }

        public static void RegenerateToken()
        {
            EditorPrefs.SetString(P + "token", NewToken());
        }

        static string NewToken()
        {
            var bytes = new byte[24];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            // URL-sicheres Base64 ohne Füllzeichen
            return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }
    }
}
