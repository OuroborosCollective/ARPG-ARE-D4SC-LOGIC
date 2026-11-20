using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
        [SerializeField] private bool blenderOpen;
        [SerializeField] private string blenderJob = "";
        private Task<string> blenderSubmit;
        private WFBlenderJobState blenderState;
        private double nextBlenderPoll;
        private Vector2 windowScroll;
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
                     path.EndsWith(".obj", StringComparison.OrdinalIgnoreCase) ||
                     path.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) ||
                     path.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase)))
                    active.changedPaths.Add(path);
        }

        private bool InScope(string path)
        {
            return path.StartsWith(rootFolder.TrimEnd('/') + "/", StringComparison.Ordinal) ||
                   path == rootFolder;
        }

        private void Update()
        {
            if (blenderSubmit != null && blenderSubmit.IsCompleted)
            {
                var completed = blenderSubmit;
                blenderSubmit = null;
                Safely(() => SelectBlenderJob(completed.GetAwaiter().GetResult()));
                Repaint();
            }
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

        private void ScanExplicitly()
        {
            if (!AssetDatabase.IsValidFolder(rootFolder))
                throw new InvalidOperationException("PRIVATE_ASSET_FOLDER_MISSING");
            runtimeBoundNames = WFAssetIntakeEvaluator.CollectRuntimeBoundNames();
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

        private void SelectBlenderJob(string id)
        {
            blenderJob = id;
            if (WFBlenderIntake.IsJobId(id)) blenderState = WFBlenderIntake.State(id);
        }

        private void OnInspectorUpdate()
        {
            if (!blenderOpen || !WFBlenderIntake.IsJobId(blenderJob) ||
                EditorApplication.timeSinceStartup < nextBlenderPoll) return;
            nextBlenderPoll = EditorApplication.timeSinceStartup + 3;
            var state = WFBlenderIntake.State(blenderJob);
            if (state != blenderState) { blenderState = state; Repaint(); }
        }

        private void ReviewBlenderFbx()
        {
            var model = WFBlenderIntake.ImportFbx(blenderJob);
            string path = AssetDatabase.GetAssetPath(model);
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (!InScope(path))
            {
                selectedFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(WFBlenderIntake.AssetRoot);
                rootFolder = WFBlenderIntake.AssetRoot;
                entries.Clear();
            }
            runtimeBoundNames = WFAssetIntakeEvaluator.CollectRuntimeBoundNames();
            Upsert(guid, path, model);
            selectedGuid = guid;
            search = "";
            EditorGUIUtility.PingObject(model);
            feedback = "FBX zur Einzelprüfung geladen: " + path;
        }

        private void DrawBlender()
        {
            blenderOpen = EditorGUILayout.Foldout(blenderOpen, "Blender-Export", true);
            if (!blenderOpen) return;
            using (new EditorGUI.DisabledScope(blenderSubmit != null))
            {
                if (GUILayout.Button(blenderSubmit == null ? "Datei für FBX-, GLB- und glTF-Export auswählen" :
                    "Quelldatei wird an den Dienst übergeben …"))
                {
                    string path = EditorUtility.OpenFilePanelWithFilters("Gepackte .blend oder eigenständige .fbx/.glb auswählen",
                        "", new[] {"Blender, FBX, GLB", "blend,fbx,glb"});
                    if (!string.IsNullOrEmpty(path)) blenderSubmit = Task.Run(() => WFBlenderIntake.Submit(path));
                }
                string id = EditorGUILayout.TextField("Auftrags-ID", blenderJob).Trim();
                if (id != blenderJob) SelectBlenderJob(id);
            }
            if (string.IsNullOrEmpty(blenderJob)) return;
            if (!WFBlenderIntake.IsJobId(blenderJob))
            {
                EditorGUILayout.HelpBox("Eine Auftrags-ID aus Editor oder CLI hat 64 kleine Hexadezimalzeichen.", MessageType.Info);
                return;
            }
            switch (blenderState)
            {
                case WFBlenderJobState.Ready:
                    string directory = Path.Combine(WFBlenderIntake.Root, "results", blenderJob);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("FBX-, GLB- und glTF-Ordner öffnen"))
                            Safely(() => EditorUtility.RevealInFinder(Path.Combine(directory, "model.glb")));
                        if (GUILayout.Button("Ordnerpfad kopieren"))
                            EditorGUIUtility.systemCopyBuffer = directory;
                    }
                    if (GUILayout.Button("Exportiertes FBX in den Asset-Eingang importieren"))
                        Safely(ReviewBlenderFbx);
                    break;
                case WFBlenderJobState.Failed:
                    EditorGUILayout.HelpBox("Export fehlgeschlagen. Fehlerprotokoll: " + WFBlenderIntake.Root +
                        "/failed/" + blenderJob + "/error.txt", MessageType.Error);
                    using (new EditorGUI.DisabledScope(blenderSubmit != null))
                        if (GUILayout.Button("Erneut versuchen"))
                        {
                            string id = blenderJob;
                            blenderSubmit = Task.Run(() => WFBlenderIntake.Retry(id));
                        }
                    break;
                case WFBlenderJobState.Incomplete:
                    EditorGUILayout.HelpBox("Exportdateien oder Beleg sind unvollständig oder beschädigt. Bitte die Originaldatei erneut einreichen.", MessageType.Error);
                    break;
                case WFBlenderJobState.Queued:
                    EditorGUILayout.HelpBox("Auftrag im Blender-Dienst. Der Export wird verarbeitet.", MessageType.Info);
                    break;
                default:
                    EditorGUILayout.HelpBox("Zu dieser ID ist kein Auftrag im lokalen Blender-Dienst sichtbar.", MessageType.Info);
                    break;
            }
        }

        private void OnGUI()
        {
            windowScroll = EditorGUILayout.BeginScrollView(windowScroll);
            try { DrawIntake(); }
            finally { EditorGUILayout.EndScrollView(); }
        }

        private void DrawIntake()
        {
            EditorGUILayout.LabelField("World Forge – Asset-Eingang", EditorStyles.boldLabel);
            DrawBlender();
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
                    folder.StartsWith("Assets/PrivateArt/", StringComparison.Ordinal) &&
                    !string.Equals(rootFolder, folder, StringComparison.Ordinal))
                {
                    rootFolder = folder;
                    entries.Clear();
                    changedPaths.Clear();
                    runtimeBoundNames = null;
                    selectedGuid = "";
                    feedback = "Paketordner geändert: Vorschau verworfen; bitte erneut scannen.";
                }
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
                    entry.verdict.kind + " | " + (InScope(entry.path) ? entry.path.Substring(rootFolder.Length).TrimStart('/') : entry.path);
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
