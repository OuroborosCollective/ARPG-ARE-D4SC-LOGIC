using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Fadenwerk.EditorTools
{
    internal sealed class FwRequest
    {
        public string method;
        public string path;
        public Dictionary<string, string> query = new Dictionary<string, string>();
        public string body = "";
        internal HttpListenerContext ctx;
    }

    internal sealed class FwResponse
    {
        public int status = 200;
        public string contentType = "application/json; charset=utf-8";
        public byte[] body;

        public static FwResponse Json(string json, int status = 200)
        {
            return new FwResponse { status = status, body = Encoding.UTF8.GetBytes(json ?? "{}") };
        }

        public static FwResponse Error(string message, int status = 400)
        {
            return Json(FwJson.Error(message), status);
        }

        public static FwResponse Bytes(byte[] data, string type)
        {
            return new FwResponse { status = 200, contentType = type, body = data ?? new byte[0] };
        }
    }

    /// <summary>
    /// HTTP-Server im Unity-Editor. Liefert die Browser-Oberfläche aus und beantwortet /api/*.
    /// Annahme im Hintergrundthread, Ausführung im Hauptthread (EditorApplication.update), Antwort wieder im Hintergrund.
    /// </summary>
    [InitializeOnLoad]
    internal static class FadenwerkServer
    {
        /// <summary>GUID der Datei Editor/Web/FadenwerkUI.html (steht in deren .meta).</summary>
        const string UiGuid = "7f1e2d3c4b5a69788796a5b4c3d2e1f0";
        const int MaxBody = 4 * 1024 * 1024;
        const string SessionKey = "Fadenwerk.ServerRunning";

        static HttpListener _listener;
        static Thread _thread;
        static volatile bool _running;
        static byte[] _html;
        static string _token = "";
        static readonly ConcurrentQueue<FwRequest> Queue = new ConcurrentQueue<FwRequest>();

        public static bool IsRunning { get { return _running; } }
        public static string LastError { get; private set; }
        public static int RequestCount { get; private set; }
        public static string LastClient { get; private set; }

        static FadenwerkServer()
        {
            AssemblyReloadEvents.beforeAssemblyReload += () => StopInternal(false);
            EditorApplication.quitting += () => StopInternal(false);
            EditorApplication.update += Pump;
            if (FadenwerkSettings.AutoStart || SessionState.GetBool(SessionKey, false))
                EditorApplication.delayCall += () => { if (!_running) Start(); };
        }

        public static void Start()
        {
            if (_running) return;
            LastError = null;
            _token = FadenwerkSettings.Token;
            _html = LoadUi();
            int port = FadenwerkSettings.Port;
            var prefixes = new List<string[]>();
            if (FadenwerkSettings.BindAll) prefixes.Add(new[] { "http://*:" + port + "/" });
            else
            {
                prefixes.Add(new[] { "http://localhost:" + port + "/", "http://127.0.0.1:" + port + "/" });
                prefixes.Add(new[] { "http://127.0.0.1:" + port + "/" });
            }

            foreach (string[] set in prefixes)
            {
                try
                {
                    var l = new HttpListener();
                    foreach (string p in set) l.Prefixes.Add(p);
                    l.Start();
                    _listener = l;
                    break;
                }
                catch (Exception e)
                {
                    LastError = "Server konnte nicht starten (Port " + port + "): " + e.Message;
                }
            }
            if (_listener == null)
            {
                Debug.LogError("[Fadenwerk] " + LastError);
                return;
            }
            LastError = null;
            _running = true;
            SessionState.SetBool(SessionKey, true);
            _thread = new Thread(AcceptLoop) { IsBackground = true, Name = "Fadenwerk HTTP" };
            _thread.Start();
            Debug.Log("[Fadenwerk] Web-Oberfläche läuft auf Port " + port + (FadenwerkSettings.BindAll ? " (alle Netzwerkschnittstellen)" : " (nur lokal)") + ".");
        }

        public static void Stop() { StopInternal(true); }

        static void StopInternal(bool userAction)
        {
            if (userAction) SessionState.SetBool(SessionKey, false);
            if (!_running && _listener == null) return;
            _running = false;
            try { if (_listener != null) _listener.Close(); } catch (Exception) { }
            _listener = null;
            FwRequest p;
            while (Queue.TryDequeue(out p)) Send(p.ctx, FwResponse.Error("Server wird neu gestartet – gleich noch einmal versuchen.", 503));
        }

        public static void Restart()
        {
            StopInternal(false);
            Start();
        }

        static byte[] LoadUi()
        {
            string path = AssetDatabase.GUIDToAssetPath(UiGuid);
            TextAsset ta = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (ta == null)
            {
                foreach (string g in AssetDatabase.FindAssets("FadenwerkUI"))
                {
                    ta = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(g));
                    if (ta != null) break;
                }
            }
            if (ta == null)
                return Encoding.UTF8.GetBytes("<!doctype html><meta charset=utf-8><p>Fadenwerk: FadenwerkUI.html nicht gefunden. Paket neu importieren.</p>");
            return Encoding.UTF8.GetBytes(ta.text);
        }

        // ---------- Hintergrund: annehmen ----------

        static void AcceptLoop()
        {
            while (_running)
            {
                HttpListenerContext ctx;
                try
                {
                    HttpListener l = _listener;
                    if (l == null) break;
                    ctx = l.GetContext();
                }
                catch (Exception)
                {
                    if (!_running) break;
                    Thread.Sleep(50);
                    continue;
                }
                // Jede Verbindung im Thread-Pool lesen, damit ein langsamer Browser keine anderen blockiert.
                HttpListenerContext c = ctx;
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try { Accept(c); }
                    catch (Exception e) { Send(c, FwResponse.Error("Interner Fehler: " + e.Message, 500)); }
                });
            }
        }

        static void Accept(HttpListenerContext ctx)
        {
            HttpListenerRequest req = ctx.Request;
            string path = req.Url.AbsolutePath;
            LastClient = req.RemoteEndPoint != null ? req.RemoteEndPoint.Address.ToString() : "";

            if (req.HttpMethod == "GET" && (path == "/" || path == "/index.html"))
            {
                var r = FwResponse.Bytes(_html, "text/html; charset=utf-8");
                Send(ctx, r);
                return;
            }
            if (path == "/favicon.ico") { Send(ctx, new FwResponse { status = 204, body = new byte[0] }); return; }
            if (!path.StartsWith("/api/", StringComparison.Ordinal)) { Send(ctx, FwResponse.Error("Nicht gefunden", 404)); return; }

            string token = req.Headers["X-Fadenwerk-Token"] ?? "";
            if (!SafeEquals(token, _token))
            {
                Thread.Sleep(250); // bremst automatisches Durchprobieren (läuft im Thread-Pool)
                Send(ctx, FwResponse.Error("Zugangsschlüssel fehlt oder ist falsch.", 401));
                return;
            }
            if (req.HttpMethod != "GET" && req.HttpMethod != "POST") { Send(ctx, FwResponse.Error("Methode nicht erlaubt", 405)); return; }

            var p = new FwRequest { method = req.HttpMethod, path = path, ctx = ctx };
            foreach (string key in req.QueryString.AllKeys)
                if (key != null) p.query[key] = req.QueryString[key] ?? "";
            if (req.HttpMethod == "POST")
            {
                if (req.ContentLength64 > MaxBody) { Send(ctx, FwResponse.Error("Anfrage zu groß", 413)); return; }
                using (var reader = new StreamReader(req.InputStream, Encoding.UTF8))
                {
                    var buf = new char[8192];
                    var sb = new StringBuilder();
                    int n;
                    while ((n = reader.Read(buf, 0, buf.Length)) > 0)
                    {
                        sb.Append(buf, 0, n);
                        if (sb.Length > MaxBody) { Send(ctx, FwResponse.Error("Anfrage zu groß", 413)); return; }
                    }
                    p.body = sb.ToString();
                }
            }
            Queue.Enqueue(p);
        }

        static bool SafeEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length || b.Length == 0) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        // ---------- Hauptthread: ausführen ----------

        static void Pump()
        {
            if (Queue.IsEmpty) return;
            var sw = Stopwatch.StartNew();
            FwRequest p;
            while (sw.ElapsedMilliseconds < 60 && Queue.TryDequeue(out p))
            {
                FwResponse r;
                try { r = FadenwerkApi.Handle(p); }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    r = FwResponse.Error("Fehler in Unity: " + e.Message, 500);
                }
                RequestCount++;
                HttpListenerContext ctx = p.ctx;
                FwResponse resp = r;
                ThreadPool.QueueUserWorkItem(_ => Send(ctx, resp));
            }
        }

        // ---------- Antworten ----------

        static void Send(HttpListenerContext ctx, FwResponse r)
        {
            if (ctx == null) return;
            try
            {
                HttpListenerResponse res = ctx.Response;
                res.StatusCode = r.status;
                res.ContentType = r.contentType;
                res.Headers["Cache-Control"] = "no-store";
                res.Headers["X-Content-Type-Options"] = "nosniff";
                res.Headers["Referrer-Policy"] = "no-referrer";
                res.Headers["X-Frame-Options"] = "DENY";
                if (r.contentType.StartsWith("text/html", StringComparison.Ordinal))
                    res.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' blob: data:; connect-src 'self'; frame-ancestors 'none'";
                byte[] body = r.body ?? new byte[0];
                res.ContentLength64 = body.Length;
                if (body.Length > 0) res.OutputStream.Write(body, 0, body.Length);
                res.OutputStream.Close();
            }
            catch (Exception) { /* Browser hat die Verbindung geschlossen */ }
        }
    }
}
