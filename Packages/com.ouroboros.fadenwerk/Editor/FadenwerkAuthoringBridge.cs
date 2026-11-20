using System;
using System.Collections.Generic;
using System.Text;

namespace Fadenwerk.EditorTools
{
    /// <summary>Optional project-owned authoring adapter. No runtime state is owned by this extension point.</summary>
    public static class FadenwerkAuthoringBridge
    {
        public static Func<string> ReadState;
        public static Func<string,string> Command;
        public static FadenwerkDatabase Database { get { return FadenwerkEditorData.Database; } }
        public static void UseDatabase(FadenwerkDatabase db) { FadenwerkEditorData.SetDatabase(db); }

        /// <summary>
        /// Optional project-owned browser page, served at "/?{ExtensionPageKey}" by the same server (and therefore
        /// behind the same owner-authenticated proxy). Called on a background thread: must be thread-safe.
        /// </summary>
        public static string ExtensionPageKey;
        public static Func<byte[]> ExtensionPage;

        /// <summary>
        /// Optional project-owned API below "/api/x/". Runs on the Unity main thread, also in Play Mode; the
        /// extension owns its own conflict checks. Arguments: method, path below /api/x/, body, query.
        /// Throw InvalidOperationException for a user-facing refusal (HTTP 422).
        /// </summary>
        public static Func<string, string, string, IDictionary<string, string>, FadenwerkExtensionReply> Extension;

        public static bool ServerRunning { get { return FadenwerkServer.IsRunning; } }
        public static void StartServer() { FadenwerkServer.Start(); }
    }

    /// <summary>Response of a project extension call.</summary>
    public sealed class FadenwerkExtensionReply
    {
        public int status = 200;
        public string contentType = "application/json; charset=utf-8";
        public byte[] body = new byte[0];

        public static FadenwerkExtensionReply Json(string json, int status = 200)
        {
            return new FadenwerkExtensionReply { status = status, body = Encoding.UTF8.GetBytes(json ?? "{}") };
        }

        public static FadenwerkExtensionReply Error(string message, int status = 400)
        {
            return Json(FwJson.Error(message), status);
        }

        public static FadenwerkExtensionReply Bytes(byte[] data, string type)
        {
            return new FadenwerkExtensionReply { status = 200, contentType = type, body = data ?? new byte[0] };
        }
    }
}
