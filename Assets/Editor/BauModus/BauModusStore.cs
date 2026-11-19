using System;
using System.Collections.Generic;
using System.IO;
using AreArpg.UnityClient;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Bau-Modus plans on disk: Assets/Resources/ARE/BuildLayouts/&lt;area&gt;.json (published with the game, versioned in
/// Git) plus a private undo history in Library/BauModus. Every write bumps the revision for conflict detection.
/// </summary>
internal static class BauModusStore
{
    public const string Folder = "Assets/Resources/ARE/BuildLayouts";
    const int HistoryLimit = 40;

    [Serializable] class History { public List<string> items = new List<string>(); }

    static string LibraryDir { get { return Path.Combine(Directory.GetCurrentDirectory(), "Library/BauModus"); } }
    public static string AssetPath(string area) { return Folder + "/" + area + ".json"; }

    public static BuildArea Area(string key)
    {
        BuildArea area = BuildAreas.Find(key);
        if (area == null) throw new InvalidOperationException("Unbekanntes Baugebiet '" + key + "'.");
        return area;
    }

    public static BuildLayoutData Load(string area)
    {
        Area(area);
        string path = AssetPath(area);
        if (!File.Exists(path)) return BuildLayoutRules.Empty(area);
        try { return BuildLayoutRules.Parse(File.ReadAllText(path), area); }
        catch (FormatException e) { throw new InvalidOperationException("Bauplan " + path + " ist beschädigt: " + e.Message); }
    }

    /// <summary>Validate, keep the previous plan for undo, write the next revision and import it.</summary>
    public static BuildLayoutData Commit(BuildLayoutData previous, BuildLayoutData next)
    {
        next.schema = BuildLayoutRules.Schema;
        next.area = previous.area;
        next.revision = previous.revision + 1;
        string error = BuildLayoutRules.Validate(next, previous.area);
        if (error != null) throw new InvalidOperationException(error);
        History history = ReadHistory(previous.area);
        history.items.Add(BuildLayoutRules.ToJson(previous));
        while (history.items.Count > HistoryLimit) history.items.RemoveAt(0);
        Write(next);
        WriteHistory(previous.area, history);
        return next;
    }

    public static bool CanUndo(string area) { return ReadHistory(area).items.Count > 0; }

    /// <summary>Restore the previous plan as a new revision (revisions only ever grow).</summary>
    public static BuildLayoutData Undo(BuildLayoutData current)
    {
        History history = ReadHistory(current.area);
        if (history.items.Count == 0) throw new InvalidOperationException("Es gibt nichts mehr rückgängig zu machen.");
        string last = history.items[history.items.Count - 1];
        history.items.RemoveAt(history.items.Count - 1);
        BuildLayoutData restored;
        try { restored = BuildLayoutRules.Parse(last, current.area); }
        catch (FormatException e) { throw new InvalidOperationException("Verlaufseintrag ist beschädigt: " + e.Message); }
        restored.revision = current.revision + 1;
        Write(restored);
        WriteHistory(current.area, history);
        return restored;
    }

    static void Write(BuildLayoutData data)
    {
        Directory.CreateDirectory(Folder);
        Directory.CreateDirectory(LibraryDir);
        string path = AssetPath(data.area);
        // Write next to Library first so the asset database never sees a half-written plan.
        string temp = Path.Combine(LibraryDir, data.area + ".json.tmp");
        File.WriteAllText(temp, BuildLayoutRules.ToJson(data));
        File.Copy(temp, path, true);
        File.Delete(temp);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
    }

    static string HistoryPath(string area) { return Path.Combine(LibraryDir, "history-" + area + ".json"); }

    static History ReadHistory(string area)
    {
        try
        {
            string path = HistoryPath(area);
            if (File.Exists(path))
            {
                History history = JsonUtility.FromJson<History>(File.ReadAllText(path));
                if (history != null && history.items != null) return history;
            }
        }
        catch (Exception) { }
        return new History();
    }

    static void WriteHistory(string area, History history)
    {
        Directory.CreateDirectory(LibraryDir);
        File.WriteAllText(HistoryPath(area), JsonUtility.ToJson(history));
    }
}
