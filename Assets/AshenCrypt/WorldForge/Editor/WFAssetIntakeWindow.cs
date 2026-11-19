using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace AshenCrypt.WorldForge.EditorTools
{
    /// <summary>
    /// Explicit review station for pre-existing World Forge profiles.
    /// Source scanning happens only when requested; import notifications update known paths.
    /// Neither import nor suggestions commit world content.
    /// </summary>
    public sealed class WFAssetIntakeWindow : EditorWindow
    {
        private sealed class Entry
        {
            public string guid;
            public string path;
            public GameObject source;
            public WFAssetIntakeVerdict verdict;
        }

        [SerializeField] private BiomeProfile biome;
        [SerializeField] private WorldProfile world;
        [SerializeField] private DefaultAsset selectedFolder;
        [SerializeField] private string search = "";
        private string rootFolder = "Assets/PrivateArt";
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly HashSet<string> changedPaths = new HashSet<string>(StringComparer.Ordinal);
        private ISet<string> runtimeBoundNames;
        private string selectedGuid = "";
        private Vector2 resultScroll;
        private string feedback = "Keine Asset-Scans gestartet.";
        private int changeCount;
        private static WFAssetIntakeWindow active;

        [MenuItem("Tools/World Forge/Asset-Eingang", priority = 1)]
        public static void Open()
        {
            var window = GetWindow<WFAssetIntakeWindow>("Asset-Eingang");
            window.minSize = new Vector2(580f, 460f);
            window.Show();
        }

        private void OnEnable() { active = this; }
        private void OnDisable()
        {
            if (active == this) active = null;
            changedPaths.Clear();
        }

        public static void QueueChanged(IEnumerable<string> paths)
        {
            if (active == null || paths == null) return;
            foreach (string path in paths)
                if (!string.IsNullOrEmpty(path) &&
                    (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                     path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) ||
                     path.EndsWith(".obj", StringComparison.OrdinalIgnoreCase)))
                    active.changedPaths.Add(path);
        }

        private bool InScope(string path)
        {
            return path.StartsWith(rootFolder.TrimEnd('/') + "/", StringComparison.Ordinal) ||
                   path == rootFolder;
        }

        private void Update()
        {
            if (changedPaths.Count == 0) return;
            var updates = changedPaths.ToArray();
            changedPaths.Clear();
            foreach (string path in updates)
            {
                if (!InScope(path)) continue;
                foreach (string key in entries.Where(x => x.Value.path == path)
                                               .Select(x => x.Key).ToArray()) entries.Remove(key);
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid)) continue; // removed asset: keep canonical approvals.
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) Upsert(guid, path, prefab);
                changeCount++;
            }
            feedback = "Inkrementell aktualisiert: " + changeCount +
                       " geänderte Assets. Bestätigte Regeln bleiben erhalten.";
            Repaint();
        }

        private void Upsert(string guid, string path, GameObject source)
        {
            var verdict = WFAssetIntakeEvaluator.Assess(source, path, runtimeBoundNames);
            entries[guid] = new Entry { guid = guid, path = path, source = source, verdict = verdict };
        }

        private static ISet<string> CollectBoundNames()
        {
            // Negative protection hints from the existing, visible Unity code; never executes it.
            var result = new HashSet<string>(StringComparer.Ordinal);
            const string sourceFolder = "Assets/Scripts/Runtime";
            if (!AssetDatabase.IsValidFolder(sourceFolder)) return result;
            var pattern = new Regex(
                @"(?:GameObject\.Find|transform\.Find|Transform\.Find)\s*\(\s*""([^""]{1,100})""",
                RegexOptions.CultureInvariant);
            foreach (string guid in AssetDatabase.FindAssets("t:MonoScript", new[] { sourceFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    string absolute = Path.Combine(Application.dataPath, path.Substring("Assets/".Length));
                    var file = new FileInfo(absolute);
                    if (!file.Exists || file.Length > 262144) continue;
                    string source = File.ReadAllText(absolute);
                    foreach (Match match in pattern.Matches(source))
                        result.Add(match.Groups[1].Value);
                }
                catch (IOException) { /* unreadable code => no positive classification benefit */ }
            }
            return result;
        }

        private void ScanExplicitly()
        {
            if (!AssetDatabase.IsValidFolder(rootFolder))
                throw new InvalidOperationException("PRIVATE_ASSET_FOLDER_MISSING");
            runtimeBoundNames = CollectBoundNames();
            entries.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:GameObject", new[] { rootFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!InScope(path)) continue;
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) Upsert(guid, path, prefab);
            }
            feedback = "Einmaliger manueller Scan: " + entries.Count + " echte GameObject-Assets. " +
                       "Keine automatischen Freigaben und keine Unity-Szene geändert.";
        }

        private bool IsRejected(string guid)
        {
            return biome != null && biome.rejectedAssetGuids != null &&
                   biome.rejectedAssetGuids.Contains(guid);
        }

        private bool IsApproved(string guid)
        {
            if (biome != null && biome.spawnRules != null &&
                biome.spawnRules.Any(x => x != null && x.approvedAssetGuid == guid)) return true;
            return world != null && world.pois != null &&
                   world.pois.Any(x => x != null && x.approvedAssetGuid == guid);
        }

        private void ChangeRejection(string guid, bool reject)
        {
            if (biome == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(biome)))
                throw new InvalidOperationException("SAVED_BIOME_REQUIRED_FOR_DECISION");
            Undo.RecordObject(biome, reject ? "Reject Asset Intake" : "Reopen Asset Intake");
            if (biome.rejectedAssetGuids == null)
                biome.rejectedAssetGuids = new List<string>();
            if (reject && !biome.rejectedAssetGuids.Contains(guid))
                biome.rejectedAssetGuids.Add(guid);
            if (!reject) biome.rejectedAssetGuids.Remove(guid);
            EditorUtility.SetDirty(biome);
            AssetDatabase.SaveAssetIfDirty(biome);
        }

        private void Approve(Entry entry, bool asPoi)
        {
            if (entry == null || entry.source == null) throw new InvalidOperationException("NO_SELECTED_ASSET");
            // Re-inspect before each mutation. The cached screen is not an authorization.
            WFAssetIntakeVerdict now = WFAssetIntakeEvaluator.Assess(
                entry.source, entry.path, runtimeBoundNames);
            if (now.kind == WFAssetIntakeKind.Protected || now.kind == WFAssetIntakeKind.ReviewRequired)
                throw new InvalidOperationException("CANDIDATE_NOT_ELIGIBLE");
            if (asPoi)
            {
                if (world == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(world)))
                    throw new InvalidOperationException("SAVED_WORLD_PROFILE_REQUIRED");
                WFAssetIntakeApprovals.ConfirmPoi(world, entry.source, entry.guid, now);
                AssetDatabase.SaveAssetIfDirty(world);
            }
            else
            {
                if (biome == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(biome)))
                    throw new InvalidOperationException("SAVED_BIOME_REQUIRED");
                WFAssetIntakeApprovals.ConfirmSpawn(biome, entry.source, entry.guid, now);
                AssetDatabase.SaveAssetIfDirty(biome);
            }
            feedback = "Nur bestehendes " + (asPoi ? "WorldProfile.pois" : "BiomeProfile.spawnRules") +
                       " gespeichert: " + entry.path + " (GUID " + entry.guid + ").";
        }

        private void Safely(Action action)
        {
            try { action(); }
            catch (Exception error)
            {
                feedback = "Verweigert: " + error.Message;
                Debug.LogError("[World Forge Asset-Eingang] " + error);
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("World Forge – Asset-Eingang", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Nur Freigabe nach Vorschau. Figuren, Effekte und geschützte Pakete bleiben gesperrt. " +
                                    "Keine automatischen Platzierungen, keine zweite Spielautorität.", MessageType.Info);
            biome = (BiomeProfile)EditorGUILayout.ObjectField("BiomeProfile", biome, typeof(BiomeProfile), false);
            world = (WorldProfile)EditorGUILayout.ObjectField("WorldProfile", world, typeof(WorldProfile), false);
            selectedFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                "Paketordner", selectedFolder, typeof(DefaultAsset), false);
            if (selectedFolder != null)
            {
                string folder = AssetDatabase.GetAssetPath(selectedFolder);
                if (AssetDatabase.IsValidFolder(folder) &&
                    folder.StartsWith("Assets/PrivateArt/", StringComparison.Ordinal))
                    rootFolder = folder;
            }
            EditorGUILayout.LabelField("Quelle", rootFolder, EditorStyles.miniLabel);
            if (GUILayout.Button("Paket/PrivateArt jetzt einmalig scannen", GUILayout.Height(28)))
                Safely(ScanExplicitly);
            search = EditorGUILayout.TextField("Suche", search);
            EditorGUILayout.LabelField(feedback, EditorStyles.wordWrappedMiniLabel);
            int protectedCount = entries.Values.Count(e => e.verdict.kind == WFAssetIntakeKind.Protected);
            int reviewCount = entries.Values.Count(e => e.verdict.kind == WFAssetIntakeKind.ReviewRequired);
            int candidateCount = entries.Count - protectedCount - reviewCount;
            EditorGUILayout.LabelField("Gesichtet " + entries.Count + " | geschützt " + protectedCount +
                " | Einzelprüfung " + reviewCount + " | Vorschläge " + candidateCount,
                EditorStyles.helpBox);

            resultScroll = EditorGUILayout.BeginScrollView(resultScroll, GUILayout.Height(Mathf.Max(150, position.height - 375)));
            int displayed = 0;
            foreach (Entry entry in entries.Values.OrderBy(e => e.path, StringComparer.Ordinal))
            {
                if (!string.IsNullOrEmpty(search) &&
                    entry.path.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (displayed++ >= 160) break;
                string label = (entry.guid == selectedGuid ? "▶ " : "") +
                    entry.verdict.kind + " | " + entry.path.Substring(rootFolder.Length).TrimStart('/');
                if (GUILayout.Button(label, EditorStyles.miniButtonLeft))
                    selectedGuid = entry.guid;
            }
            EditorGUILayout.EndScrollView();
            if (displayed > 160)
                EditorGUILayout.LabelField("Nur erste 160 Treffer angezeigt; suche nach Pfad.", EditorStyles.miniLabel);

            Entry selected;
            if (!entries.TryGetValue(selectedGuid, out selected)) return;
            EditorGUILayout.Space(5);
            EditorGUILayout.ObjectField("Asset", selected.source, typeof(GameObject), false);
            EditorGUILayout.HelpBox(selected.verdict.reason + " | Größe: " +
                selected.verdict.size.ToString("F2") +
                " m | Freihalteradius: " + selected.verdict.clearanceRadius.ToString("F2") + " m",
                selected.verdict.kind == WFAssetIntakeKind.Protected ? MessageType.Warning : MessageType.Info);
            Texture image = AssetPreview.GetAssetPreview(selected.source);
            if (image == null) image = AssetPreview.GetMiniThumbnail(selected.source);
            if (image != null) GUILayout.Label(image, GUILayout.Width(100), GUILayout.Height(90));
            if (IsApproved(selected.guid)) EditorGUILayout.LabelField("Bereits bestätigt", EditorStyles.boldLabel);
            if (IsRejected(selected.guid))
            {
                if (GUILayout.Button("Ablehnung aufheben"))
                    Safely(() => ChangeRejection(selected.guid, false));
                return;
            }
            using (new EditorGUI.DisabledScope(
                IsApproved(selected.guid) ||
                selected.verdict.kind != WFAssetIntakeKind.Spawn))
                if (GUILayout.Button("Als Spawn bestätigen + nur BiomeProfile speichern"))
                    Safely(() => Approve(selected, false));
            using (new EditorGUI.DisabledScope(
                IsApproved(selected.guid) ||
                selected.verdict.kind == WFAssetIntakeKind.Protected ||
                selected.verdict.kind == WFAssetIntakeKind.ReviewRequired))
                if (GUILayout.Button("Als POI bestätigen + nur WorldProfile speichern"))
                    Safely(() => Approve(selected, true));
            if (GUILayout.Button("Ablehnen (im bestehenden BiomeProfile merken)"))
                Safely(() => ChangeRejection(selected.guid, true));
        }
    }

    internal sealed class WFAssetIntakePostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
                                          string[] movedAssets, string[] movedFromAssetPaths)
        {
            // No global FindAssets here: notifications update ONLY a previously scanned window.
            WFAssetIntakeWindow.QueueChanged(importedAssets);
            WFAssetIntakeWindow.QueueChanged(deletedAssets);
            WFAssetIntakeWindow.QueueChanged(movedAssets);
            WFAssetIntakeWindow.QueueChanged(movedFromAssetPaths);
        }
    }
}
