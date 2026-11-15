using System;

namespace Fadenwerk.EditorTools
{
    /// <summary>Optional project-owned authoring adapter. No runtime state is owned by this extension point.</summary>
    public static class FadenwerkAuthoringBridge
    {
        public static Func<string> ReadState;
        public static Func<string,string> Command;
        public static FadenwerkDatabase Database { get { return FadenwerkEditorData.Database; } }
        public static void UseDatabase(FadenwerkDatabase db) { FadenwerkEditorData.SetDatabase(db); }
    }
}
