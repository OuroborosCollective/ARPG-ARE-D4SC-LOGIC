#if ARE_BOZO_CHARACTERS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AreArpg.UnityClient;
using Bozo.ModularCharacters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace AreArpg.EditorTools
{
    /// <summary>
    /// Builds the game's human figures from the purchased BoZo pack. Compiled only where the pack is installed
    /// (scripting define ARE_BOZO_CHARACTERS in the private Assets/csc.rsp).
    ///
    /// Two steps: (1) Play Mode – the vendor system assembles each recipe, merges it into one mesh and one material and
    /// saves a private prefab; (2) Edit Mode – every result becomes a passive game visual (no scripts, no physics):
    /// scaled to its height, standing on its feet, with the shared humanoid locomotion.
    /// </summary>
    [InitializeOnLoad]
    public static class BozoFigureBaker
    {
        public const string VendorRoot = "Assets/BoZo_StylizedModularCharacters";
        public const string BakeFolder = BozoFigureRecipes.BakeFolder;
        public const string SourceFolder = BakeFolder + "/Sources";
        public const string Visuals = "Assets/Resources/ARE/Visuals/";
        const string BasePrefab = VendorRoot + "/Prefabs/BSMC_CharacterBase.prefab";
        const string PresetRoot = VendorRoot + "/CustomCharacters/Resources/";
        const string StageScene = BakeFolder + "/BakeStage.unity";
        const string QueueKey = "ARE.Bozo.Queue", FinalizeKey = "ARE.Bozo.Finalize", ReturnSceneKey = "ARE.Bozo.ReturnScene";
        const string StagingFolder = "Assets/PrivateArt/BozoBakeStaging";
        [Serializable] sealed class SceneSetupState { public SceneSetup[] scenes; }
        static string ReportPath { get { return Path.Combine(Directory.GetCurrentDirectory(), "Library/BozoCharacters/report.txt"); } }

        static BozoFigureBaker()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetString(QueueKey, "") != "") RunSession();
                if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(FinalizeKey, false))
                    EditorApplication.delayCall += AfterSession;
            };
        }

        [MenuItem("ARE ARPG/Art/BoZo Characters/Bake All Figures")]
        public static void BakeAll() { Bake(BozoFigureRecipes.Load().Select(f => f.id).ToArray()); }

        /// <summary>Start a bake session for the given figure ids (enters Play Mode in a private stage scene).</summary>
        public static void Bake(string[] ids)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play-Modus zuerst beenden.");
            BozoBuildGuard.Restore(); // an interrupted player build leaves the pack's part folders hidden
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefab) == null) throw new InvalidOperationException("BoZo-Paket fehlt: " + BasePrefab);
            var all = BozoFigureRecipes.Load();
            foreach (string id in ids) if (all.All(f => f.id != id)) throw new InvalidOperationException("Unbekannte Figur: " + id);
            foreach (var scene in Enumerable.Range(0, EditorSceneManager.sceneCount).Select(EditorSceneManager.GetSceneAt))
                if (scene.isDirty) throw new InvalidOperationException("Offene Szene hat ungespeicherte Änderungen: " + scene.path);
            EnsureFolder(BakeFolder); EnsureFolder(SourceFolder);
            var settings = CharacterToolSettingsProvider.Get();
            EnsureFolder(StagingFolder);
            settings._prefabFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(StagingFolder);
            EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
            foreach (string id in ids)
            {
                AssetDatabase.DeleteAsset(StagingFolder + "/" + id);
                foreach (string stale in new[] { StagedPartsRecordPath(id), PreviousPartsRecordPath(id) }) if (File.Exists(stale)) File.Delete(stale);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, "");
            SessionState.SetString(ReturnSceneKey, JsonUtility.ToJson(new SceneSetupState { scenes = EditorSceneManager.GetSceneManagerSetup() }));
            try
            {
                var stage = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                if (!EditorSceneManager.SaveScene(stage, StageScene)) throw new IOException("Bake-Szene konnte nicht gespeichert werden.");
            }
            catch
            {
                settings._prefabFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(BakeFolder);
                EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
                RestoreScenes();
                throw;
            }
            SessionState.SetString(QueueKey, string.Join(",", ids));
            EditorApplication.EnterPlaymode();
        }

        // ------------------------------------------------------------------ step 1: Play Mode

        static async void RunSession()
        {
            string[] ids = SessionState.GetString(QueueKey, "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            SessionState.SetString(QueueKey, "");
            try
            {
                var recipes = BozoFigureRecipes.Load();
                foreach (string id in ids)
                {
                    try
                    {
                        await BakeOne(recipes.First(f => f.id == id));
                        Report(id + "\tBAKED");
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                        Report(id + "\tFAILED\t" + e.Message.Replace('\n', ' '));
                    }
                }
            }
            finally
            {
                SessionState.SetBool(FinalizeKey, true);
                EditorApplication.ExitPlaymode();
            }
        }

        static async Task BakeOne(BozoFigureRecipes.Figure recipe)
        {
            CharacterData data = Compose(recipe);
            // Instantiate below an inactive parent so the settings are in place before the vendor's Awake runs.
            var holder = new GameObject("BozoBakeHolder"); holder.SetActive(false);
            GameObject go = null;
            try
            {
                go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefab), holder.transform);
                go.name = recipe.id;
                var system = go.GetComponent<OutfitSystem>();
                if (system == null) throw new InvalidOperationException("Basisfigur ohne OutfitSystem.");
                system.loadMode = OutfitSystem.LoadMode.Manual;
                system.characterData = null;
                system._textureSize = (OutfitSystem.TextureSize)recipe.texture;
                system.async = false;
                go.transform.SetParent(null, false);
                Object.Destroy(holder);
                await Frames(3);

                bool loaded = false;
                UnityAction onLoaded = () => loaded = true;
                system.OnLoadComplete.AddListener(onLoaded);
                try
                {
                    _ = system.LoadCharacter(data);
                    await Until(() => loaded, 240, recipe.id + ": Laden/Zusammenfügen");
                }
                finally { system.OnLoadComplete.RemoveListener(onLoaded); }
                await Until(() => !system.isMerging && !system.isloading && !system.isUpdatingTexture, 120, recipe.id + ": Texturen");
                await Frames(6);

                int outfits = system.GetOutfits().Count(o => o != null);
                if (outfits < recipe.outfits.Length) throw new InvalidOperationException("Nur " + outfits + " Teile geladen (Rezept nennt " + recipe.outfits.Length + ").");
                // Split figures and figures with own fabrics need to know which run of vertices is which part.
                if (recipe.parts || HasFabric(recipe)) RecordParts(recipe.id, system);
                system.prefabName = recipe.id;
                system.cleanScriptsAfterPrefab = true;
                await system.SaveCharacterToPrefab();
                await Frames(2);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(StagingFolder + "/" + recipe.id + "/" + recipe.id + ".prefab") == null)
                    throw new InvalidOperationException("Hersteller-Speichern lieferte kein Prefab.");
                var candidate = AssetDatabase.LoadAssetAtPath<GameObject>(StagingFolder + "/" + recipe.id + "/" + recipe.id + ".prefab");
                var skins = candidate.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var animator = candidate.GetComponentInChildren<Animator>(true);
                if (skins.Length != 1 || skins[0].sharedMesh == null || skins[0].sharedMaterial == null ||
                    animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                    throw new InvalidOperationException("Temporärer Bake ist unvollständig: " + recipe.id);
                // The complete bake stays in the staging folder. Publishing rewrites assets as text, which the
                // editor refuses while playing: it follows in Edit Mode (AfterSession).
            }
            finally
            {
                if (go != null) Object.DestroyImmediate(go);
                if (holder != null) Object.DestroyImmediate(holder);
            }
        }

        /// <summary>Recipe = vendor preset + overrides (shapes, removed slots, outfits with colours).</summary>
        static CharacterData Compose(BozoFigureRecipes.Figure recipe)
        {
            var preset = AssetDatabase.LoadAssetAtPath<DataObject>(PresetRoot + recipe.@base + ".asset");
            if (preset == null) throw new InvalidOperationException("Vorlage fehlt: " + recipe.@base);
            CharacterData source = preset.GetCharacterData();
            var data = new CharacterData(source) { material = string.IsNullOrEmpty(source.material) ? "BSMC_StylizedShader" : source.material };
            data.characterName = recipe.id;
            SetShapes(data.bodyIDs, data.bodyShapes, recipe.body);
            SetShapes(data.faceIDs, data.faceShapes, recipe.face);
            var removed = new HashSet<string>(recipe.remove ?? new string[0]);
            foreach (var outfit in recipe.outfits) removed.Add(outfit.slot);
            data.outfitDatas = data.outfitDatas.Where(o => !removed.Contains(Slot(o.outfit))).ToList();
            foreach (var outfit in recipe.outfits)
            {
                string path = outfit.slot + "/" + outfit.id;
                if (Resources.Load<Outfit>(path) == null) throw new InvalidOperationException("Teil fehlt im Paket: " + path);
                var colors = new List<Color>();
                foreach (string hex in outfit.colors ?? new string[0])
                {
                    Color color;
                    if (!BozoFigureRecipes.TryColor(hex, out color)) break; // later channels keep the part's own colours
                    colors.Add(color);
                }
                data.outfitDatas.Add(new OutfitData
                {
                    outfit = path, colors = colors, alpha = 1f, decal = "", decalColors = new List<Color>(), pattern = "",
                    patternColors = new List<Color>(), patternScale = Vector4.one, decalScale = Vector4.one, decalDatas = new List<DecalData>()
                });
            }
            return data;
        }

        static string Slot(string outfitPath)
        {
            int slash = outfitPath.IndexOf('/');
            return slash >= 0 ? outfitPath.Substring(0, slash) : outfitPath;
        }

        static void SetShapes(List<string> ids, List<float> values, BozoFigureRecipes.Shape[] shapes)
        {
            foreach (var shape in shapes ?? new BozoFigureRecipes.Shape[0])
            {
                int index = ids.IndexOf(shape.id);
                if (index >= 0) values[index] = shape.value;
                else { ids.Add(shape.id); values.Add(shape.value); }
            }
        }

        static async Task Frames(int count) { for (int i = 0; i < count; i++) await Task.Yield(); }

        static async Task Until(Func<bool> done, double seconds, string what)
        {
            double end = EditorApplication.timeSinceStartup + seconds;
            while (!done())
            {
                if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play-Modus wurde beendet: " + what);
                if (EditorApplication.timeSinceStartup > end) throw new TimeoutException("Zeitüberschreitung: " + what);
                await Task.Yield();
            }
        }

        static void Report(string line)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.AppendAllText(ReportPath, line + "\n");
        }
        static string BakedPath(string id) { return BozoFigureRecipes.BakedPath(id); }

        // ------------------------------------------------------------------ step 2: Edit Mode

        static void RestoreScenes()
        {
            string json = SessionState.GetString(ReturnSceneKey, "");
            if (string.IsNullOrEmpty(json)) return;
            EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<SceneSetupState>(json).scenes);
            SessionState.EraseString(ReturnSceneKey);
        }

        // Publish only a complete vendor bake. Preserve existing .meta files (GUIDs) and roll back bytes on errors.
        static void PublishBake(string id)
        {
            string source = StagingFolder + "/" + id, target = BakeFolder + "/" + id;
            var serialization = EditorSettings.serializationMode;
            try
            {
                EditorSettings.serializationMode = SerializationMode.ForceText;
                AssetDatabase.ForceReserializeAssets(AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith(source + "/", StringComparison.Ordinal)).ToArray());
            }
            finally { EditorSettings.serializationMode = serialization; }
            string backup = Path.Combine("Library/BozoCharacters/Backup", id);
            if (Directory.Exists(backup)) Directory.Delete(backup, true);
            Directory.CreateDirectory(backup);
            bool existed = Directory.Exists(target);
            if (existed)
            {
                CopyTree(target, backup + "/assets");
                File.WriteAllText(backup + "/.existing", "");
            }
            try
            {
                var guids = new Dictionary<string, string>();
                foreach (string meta in Directory.GetFiles(source, "*.meta", SearchOption.AllDirectories))
                {
                    string destination = target + meta.Substring(source.Length);
                    if (File.Exists(destination))
                    {
                        string oldGuid = File.ReadAllLines(meta).First(l => l.StartsWith("guid: ")).Substring(6);
                        string newGuid = File.ReadAllLines(destination).First(l => l.StartsWith("guid: ")).Substring(6);
                        guids[oldGuid] = newGuid;
                    }
                }
                foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                {
                    string destination = target + file.Substring(source.Length);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    if (file.EndsWith(".meta") && File.Exists(destination)) continue;
                    byte[] bytes = File.ReadAllBytes(file);
                    // Unity YAML references must point at the retained production GUIDs.
                    if (file.EndsWith(".meta") || System.Text.Encoding.UTF8.GetString(bytes.Take(5).ToArray()) == "%YAML")
                    {
                        string yaml = System.Text.Encoding.UTF8.GetString(bytes);
                        foreach (var pair in guids) yaml = yaml.Replace("guid: " + pair.Key, "guid: " + pair.Value);
                        bytes = System.Text.Encoding.UTF8.GetBytes(yaml);
                    }
                    File.WriteAllBytes(destination, bytes);
                }
                // Remove temporary GUID duplicates before importing production files.
                Directory.Delete(source, true);
                if (File.Exists(source + ".meta")) File.Delete(source + ".meta");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var published = AssetDatabase.LoadAssetAtPath<GameObject>(BakedPath(id));
                if (published == null || AssetDatabase.GetDependencies(BakedPath(id), true)
                    .Any(path => path.StartsWith(StagingFolder + "/", StringComparison.Ordinal)))
                    throw new IOException("Bake enthält ungültige temporäre Referenzen: " + id);
            }
            catch
            {
                if (Directory.Exists(target)) Directory.Delete(target, true);
                if (existed) CopyTree(backup + "/assets", target);
                else if (File.Exists(target + ".meta")) File.Delete(target + ".meta");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                throw;
            }
            finally { AssetDatabase.DeleteAsset(source); }
        }

        sealed class AssetSnapshot
        {
            readonly string[] paths;
            readonly Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();
            public AssetSnapshot(IEnumerable<string> targets)
            {
                paths = targets.ToArray();
                foreach (string path in paths)
                {
                    if (Directory.Exists(path))
                        foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories)) files[file] = File.ReadAllBytes(file);
                    else if (File.Exists(path)) files[path] = File.ReadAllBytes(path);
                    if (File.Exists(path + ".meta")) files[path + ".meta"] = File.ReadAllBytes(path + ".meta");
                }
            }
            public void Restore()
            {
                foreach (string path in paths)
                {
                    if (Directory.Exists(path)) Directory.Delete(path, true);
                    else if (File.Exists(path)) File.Delete(path);
                    if (File.Exists(path + ".meta")) File.Delete(path + ".meta");
                }
                foreach (var file in files)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(file.Key));
                    File.WriteAllBytes(file.Key, file.Value);
                }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }

        static void CopyTree(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
            foreach (string folder in Directory.GetDirectories(source)) CopyTree(folder, Path.Combine(target, Path.GetFileName(folder)));
        }

        static void AfterSession()
        {
            SessionState.SetBool(FinalizeKey, false);
            RestoreScenes();
            var settings = CharacterToolSettingsProvider.Get();
            settings._prefabFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(BakeFolder);
            EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
            var baked = File.Exists(ReportPath)
                ? File.ReadAllLines(ReportPath).Where(l => l.EndsWith("\tBAKED")).Select(l => l.Split('\t')[0]).ToArray() : new string[0];
            // Back in Edit Mode the staged bakes are published. A bake that cannot be published is reported and
            // leaves the figure that was there before untouched.
            var published = new List<string>();
            foreach (string id in baked)
            {
                try { PublishBake(id); PromotePartsRecord(id); published.Add(id); }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    Report(id + "\tFAILED\t" + e.Message.Replace('\n', ' '));
                }
            }
            FinalizeFigures(published.ToArray(), true);
        }

        [MenuItem("ARE ARPG/Art/BoZo Characters/Rebuild Game Visuals From Baked Figures")]
        public static void FinalizeAll()
        {
            FinalizeFigures(BozoFigureRecipes.Load().Where(f => AssetDatabase.LoadAssetAtPath<GameObject>(BakedPath(f.id)) != null).Select(f => f.id).ToArray());
        }

        /// <summary>Turn baked vendor prefabs into the passive visuals the game binds.</summary>
        public static void FinalizeFigures(string[] ids) { FinalizeFigures(ids, false); }

        static void FinalizeFigures(string[] ids, bool fromBake)
        {
            var recipes = BozoFigureRecipes.Load();
            // A colour variant uses the mesh of its model figure: the models come first.
            ids = ids.OrderBy(id => string.IsNullOrEmpty(recipes.First(f => f.id == id).sameShapeAs) ? 0 : 1).ToArray();
            var unused = new List<string>();
            var rebuilt = new List<string>();
            foreach (string id in ids)
            {
                var recipe = recipes.First(f => f.id == id);
                AssetSnapshot snapshot = null;
                try
                {
                    foreach (string key in recipe.keys ?? new string[0])
                        if (!MayWrite(key)) throw new InvalidOperationException("Visual " + key + " ist keine Figur dieses Systems.");
                    snapshot = new AssetSnapshot(new[] { BakeFolder + "/" + id, BakeFolder + "/Animation", SourceFolder }
                        .Concat((recipe.keys ?? new string[0]).Select(key => Visuals + key + ".prefab")));
                    EnsureFolder(SourceFolder);
                    Mesh[] parts = recipe.parts ? GameParts(recipe) : null;
                    Mesh mesh = recipe.parts ? null : GameMesh(recipe);
                    Material material = GameMaterial(recipe);
                    Avatar avatar = GameAvatar(recipe, recipes);
                    foreach (string key in recipe.keys ?? new string[0]) Save(recipe, mesh, parts, material, avatar, Visuals + key + ".prefab", key, true);
                    foreach (string key in recipe.sources ?? new string[0]) Save(recipe, mesh, parts, material, avatar, SourceFolder + "/" + key + ".prefab", key, false);
                    // Meshes the figure no longer uses: a colour variant keeps none of its own, a figure in two parts no
                    // mesh in one piece, a figure in one piece no parts. They go after the whole run (see below).
                    bool variant = !string.IsNullOrEmpty(recipe.sameShapeAs);
                    if (variant || recipe.parts) unused.Add(GameMeshPath(recipe.id));
                    if (variant || !recipe.parts) { unused.Add(PartPath(recipe.id, FigureComposer.BodyPart)); unused.Add(PartPath(recipe.id, FigureComposer.HeadPart)); }
                    AssetDatabase.SaveAssets();
                    ReloadRewritten();
                    rebuilt.Add(id);
                    if (File.Exists(PreviousPartsRecordPath(id))) File.Delete(PreviousPartsRecordPath(id)); // the new record holds
                    Report(id + "\tVISUALS\t" + string.Join(",", (recipe.keys ?? new string[0]).Concat(recipe.sources ?? new string[0])));
                }
                catch (Exception e)
                {
                    if (snapshot != null) snapshot.Restore();
                    ReloadRewritten(); // the restored files count, not what this attempt left in memory
                    if (fromBake)
                    {
                        string backup = Path.Combine("Library/BozoCharacters/Backup", id);
                        string target = BakeFolder + "/" + id;
                        if (Directory.Exists(target)) Directory.Delete(target, true);
                        if (File.Exists(backup + "/.existing")) CopyTree(backup + "/assets", target);
                        else if (File.Exists(target + ".meta")) File.Delete(target + ".meta");
                        RestorePartsRecord(id);
                        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    }
                    Debug.LogException(e);
                    Report(id + "\tVISUALS_FAILED\t" + e.Message.Replace('\n', ' '));
                }
            }
            // Only now, and only what no visual draws any more: a colour variant that was not part of this run, or
            // whose own rebuild failed, may still draw the mesh its model just stopped using.
            var drawn = new HashSet<string>(AssetDatabase.FindAssets("t:Prefab", new[] { Visuals.TrimEnd('/'), SourceFolder })
                .Select(AssetDatabase.GUIDToAssetPath).SelectMany(path => AssetDatabase.GetDependencies(path, false)));
            foreach (string path in unused.Distinct())
                if (!drawn.Contains(path) && AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();
            WriteFolkIndex(rebuilt);
            Report("DONE");
        }

        /// <summary>
        /// The game's list of the pool figures baked here (FolkIndex), next to their visuals. A figure's line comes from
        /// the recipe of a successful rebuild; a figure whose rebuild failed keeps the line of the visual that was
        /// restored. Without arguments it only drops lines of visuals that are gone and adds missing ones.
        /// </summary>
        public static void WriteFolkIndex(IEnumerable<string> rebuiltIds = null)
        {
            if (!File.Exists(BozoFigurePool.PoolPath)) return;
            string path = BozoFigurePool.IndexPath;
            string before = File.Exists(path) ? File.ReadAllText(path) : "";
            string text = BozoFigurePool.UpdateIndex(before, rebuiltIds, key => File.Exists(Visuals + key + ".prefab"));
            if (text == before) return;
            File.WriteAllText(path, text);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        /// <summary>
        /// The merged mesh of the pack still carries every expression and body slider as a blend shape (about 80, more
        /// than half of the mesh data). The figures of the game are passive, so the sliders the recipe set are written
        /// into the vertices and the blend shapes are dropped: same look, less memory and a smaller build. The mesh is
        /// also stored without a second copy for scripts. The full mesh stays next to it for later use.
        /// </summary>
        static Mesh GameMesh(BozoFigureRecipes.Figure recipe)
        {
            if (!string.IsNullOrEmpty(recipe.sameShapeAs)) return SharedGameMesh(recipe);
            return SaveMesh(BuildGameMesh(recipe), GameMeshPath(recipe.id));
        }

        /// <summary>
        /// Meshes rewritten in place during this run. The editor keeps drawing the old data of such a mesh: once its
        /// vertex count changed, figures using it showed no body or no head at all. Each one is therefore unloaded as
        /// soon as it is saved, and its next use reads the file.
        /// </summary>
        static readonly List<Mesh> Rewritten = new List<Mesh>();

        /// <summary>Store a game mesh, keeping the identity of an existing asset, without a second copy for scripts.</summary>
        static Mesh SaveMesh(Mesh mesh, string path)
        {
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
            else { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); Rewritten.Add(saved); } // keep the asset identity
            var serialized = new SerializedObject(saved);
            serialized.FindProperty("m_IsReadable").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(saved);
            return saved;
        }

        /// <summary>Unload the meshes rewritten in place (after saving them): every later use loads them from their file.</summary>
        static void ReloadRewritten()
        {
            foreach (Mesh mesh in Rewritten) if (mesh != null) Resources.UnloadAsset(mesh);
            Rewritten.Clear();
        }

        /// <summary>
        /// Unload every game mesh of the baked figures, so the editor draws what their files hold. For a session that
        /// rewrote meshes before the baker unloaded them itself.
        /// </summary>
        public static int ReloadGameMeshes()
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Mesh", new[] { BakeFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("_Game")) continue;
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is Mesh) { Resources.UnloadAsset(asset); count++; }
            }
            return count;
        }

        static string GameMeshPath(string id) { return BakeFolder + "/" + id + "/" + id + "_GameMesh.asset"; }
        static string PartPath(string id, string part) { return BakeFolder + "/" + id + "/" + id + "_Game" + part + ".asset"; }
        static string PartsRecordPath(string id) { return Path.Combine(Directory.GetCurrentDirectory(), "Library/BozoCharacters/Parts/" + id + ".txt"); }
        static string StagedPartsRecordPath(string id) { return PartsRecordPath(id) + ".staged"; }
        static string PreviousPartsRecordPath(string id) { return PartsRecordPath(id) + ".previous"; }

        /// <summary>
        /// A bake writes its parts record next to the figure's record; only a published bake takes it over. A bake that
        /// fails or cannot be published leaves the record that belongs to the figure still in use (review on #133). The
        /// record it replaces is kept (empty when there was none) until the figure's visuals are rebuilt, because a
        /// failed rebuild brings the old bake back and with it the old record (<see cref="RestorePartsRecord"/>).
        /// </summary>
        static void PromotePartsRecord(string id)
        {
            string staged = StagedPartsRecordPath(id), record = PartsRecordPath(id);
            if (!File.Exists(staged)) return;
            if (File.Exists(record)) File.Copy(record, PreviousPartsRecordPath(id), true);
            else File.WriteAllText(PreviousPartsRecordPath(id), "");
            File.Copy(staged, record, true);
            File.Delete(staged);
        }

        /// <summary>The visuals of a published bake failed and its old files came back: so does its old record.</summary>
        static void RestorePartsRecord(string id)
        {
            string previous = PreviousPartsRecordPath(id), record = PartsRecordPath(id);
            if (!File.Exists(previous)) return; // this bake took over no record
            if (new FileInfo(previous).Length == 0) { if (File.Exists(record)) File.Delete(record); }
            else File.Copy(previous, record, true);
            File.Delete(previous);
        }

        /// <summary>
        /// The pack merges a figure by putting the mesh of every outfit after the previous one (BoZoCharacterOptimizer):
        /// each outfit is one unbroken run of vertices. Right after loading, the outfits the merge used are the ones it
        /// switched off. Their types and vertex counts are written down in merge order, and they must add up to the
        /// merged mesh, or the figure cannot be split.
        /// </summary>
        static void RecordParts(string id, OutfitSystem system)
        {
            var lines = new List<string>();
            int total = 0;
            foreach (Outfit item in system.GetOutfits())
            {
                if (item == null || !item.mergeMesh || item.gameObject.activeSelf || (item.Type != null && system.hiddenTypes.ContainsKey(item.Type))) continue;
                string type = item.Type != null ? item.Type.name : "";
                if (BozoFigureRecipes.IsHeadSlot(type) == null) throw new InvalidOperationException(id + ": Teiletyp '" + type + "' ist weder Kopf noch Körper zugeordnet.");
                foreach (SkinnedMeshRenderer renderer in item.skinnedRenderers)
                {
                    if (renderer == null || renderer.sharedMesh == null) continue;
                    if (renderer.gameObject != item.gameObject && !renderer.gameObject.activeSelf) continue;
                    lines.Add(type + "\t" + renderer.sharedMesh.vertexCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    total += renderer.sharedMesh.vertexCount;
                }
            }
            SkinnedMeshRenderer merged = system.GetCharacterBody();
            int count = merged != null && merged.sharedMesh != null ? merged.sharedMesh.vertexCount : -1;
            if (total != count) throw new InvalidOperationException(id + ": die Teile ergeben " + total + " Eckpunkte, das zusammengefügte Modell hat " + count + ".");
            Directory.CreateDirectory(Path.GetDirectoryName(PartsRecordPath(id)));
            File.WriteAllLines(StagedPartsRecordPath(id), lines);
        }

        /// <summary>For every vertex of the merged figure: does it belong to the head? From the record of the bake.</summary>
        static bool[] HeadVertices(string id, int vertexCount)
        {
            if (!File.Exists(PartsRecordPath(id))) throw new InvalidOperationException(id + ": keine Aufzeichnung der Teile; die Figur muss mit \"parts\" neu erzeugt werden.");
            var head = new bool[vertexCount];
            int at = 0;
            foreach (string line in File.ReadAllLines(PartsRecordPath(id)))
            {
                string[] f = line.Split('\t');
                bool? isHead = f.Length == 2 ? BozoFigureRecipes.IsHeadSlot(f[0]) : null;
                if (isHead == null || !int.TryParse(f[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int count) ||
                    count <= 0 || at + count > vertexCount)
                    throw new InvalidOperationException(id + ": Aufzeichnung der Teile passt nicht zum Modell.");
                for (int v = at; v < at + count; v++) head[v] = isHead.Value;
                at += count;
            }
            if (at != vertexCount) throw new InvalidOperationException(id + ": die Teile ergeben " + at + " Eckpunkte, das Modell hat " + vertexCount + ".");
            return head;
        }

        /// <summary>Body and head of a figure built in two parts, split from its game mesh along the outfits.</summary>
        static Mesh[] GameParts(BozoFigureRecipes.Figure recipe)
        {
            if (!string.IsNullOrEmpty(recipe.sameShapeAs)) return SharedGameParts(recipe);
            Mesh full = BuildGameMesh(recipe);
            try
            {
                bool[] head = HeadVertices(recipe.id, full.vertexCount);
                return new[]
                {
                    SaveMesh(PartOf(full, head, false, recipe.id + "_Game" + FigureComposer.BodyPart), PartPath(recipe.id, FigureComposer.BodyPart)),
                    SaveMesh(PartOf(full, head, true, recipe.id + "_Game" + FigureComposer.HeadPart), PartPath(recipe.id, FigureComposer.HeadPart)),
                };
            }
            finally { Object.DestroyImmediate(full); }
        }

        /// <summary>A colour variant in two parts uses both parts of its model, once its own split matches them.</summary>
        static Mesh[] SharedGameParts(BozoFigureRecipes.Figure recipe)
        {
            var model = BozoFigureRecipes.Load().First(f => f.id == recipe.sameShapeAs);
            var shared = new[] { AssetDatabase.LoadAssetAtPath<Mesh>(PartPath(model.id, FigureComposer.BodyPart)), AssetDatabase.LoadAssetAtPath<Mesh>(PartPath(model.id, FigureComposer.HeadPart)) };
            if (shared[0] == null || shared[1] == null) throw new InvalidOperationException("Grundfigur zuerst erzeugen: " + model.id + " (für " + recipe.id + ").");
            Mesh full = BuildGameMesh(recipe);
            try
            {
                bool[] head = HeadVertices(recipe.id, full.vertexCount);
                for (int p = 0; p < 2; p++)
                {
                    Mesh own = PartOf(full, head, p == 1, "probe");
                    try
                    {
                        string difference = MeshDifference(own, shared[p]);
                        if (difference != null) throw new InvalidOperationException(recipe.id + " hat nicht die Form von " + model.id + " (" + (p == 1 ? "Kopf" : "Körper") + "): " + difference);
                    }
                    finally { Object.DestroyImmediate(own); }
                }
            }
            finally { Object.DestroyImmediate(full); }
            return shared;
        }

        /// <summary>
        /// The triangles of one side of the neck with only the vertices they use. Every outfit is one run of vertices,
        /// so no triangle may join head and body; one that does means the record does not fit the figure.
        /// </summary>
        static Mesh PartOf(Mesh full, bool[] headVertex, bool wantHead, string name)
        {
            int[] triangles = full.triangles;
            var kept = new List<int>(triangles.Length);
            for (int t = 0; t < triangles.Length; t += 3)
            {
                bool a = headVertex[triangles[t]], b = headVertex[triangles[t + 1]], c = headVertex[triangles[t + 2]];
                if (a != b || b != c) throw new InvalidOperationException(name + ": ein Dreieck verbindet Kopf und Körper.");
                if (a == wantHead) { kept.Add(triangles[t]); kept.Add(triangles[t + 1]); kept.Add(triangles[t + 2]); }
            }
            if (kept.Count == 0) throw new InvalidOperationException(name + ": dieser Teil der Figur ist leer.");
            var newIndex = new int[full.vertexCount];
            for (int v = 0; v < newIndex.Length; v++) newIndex[v] = -1;
            var order = new List<int>();
            foreach (int v in kept) if (newIndex[v] < 0) { newIndex[v] = order.Count; order.Add(v); }
            var part = new Mesh { name = name };
            if (order.Count > 65535) part.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            Vector3[] vertices = full.vertices, normals = full.normals;
            Vector4[] tangents = full.tangents;
            Color[] colors = full.colors;
            BoneWeight[] weights = full.boneWeights;
            part.vertices = order.Select(v => vertices[v]).ToArray();
            if (normals.Length == vertices.Length) part.normals = order.Select(v => normals[v]).ToArray();
            if (tangents.Length == vertices.Length) part.tangents = order.Select(v => tangents[v]).ToArray();
            if (colors.Length == vertices.Length) part.colors = order.Select(v => colors[v]).ToArray();
            for (int channel = 0; channel < 4; channel++)
            {
                var uvs = new List<Vector4>();
                full.GetUVs(channel, uvs);
                if (uvs.Count != vertices.Length) continue;
                int dimension = full.GetVertexAttributeDimension((UnityEngine.Rendering.VertexAttribute)((int)UnityEngine.Rendering.VertexAttribute.TexCoord0 + channel));
                var picked = order.Select(v => uvs[v]).ToList();
                if (dimension <= 2) part.SetUVs(channel, picked.Select(u => new Vector2(u.x, u.y)).ToList());
                else if (dimension == 3) part.SetUVs(channel, picked.Select(u => new Vector3(u.x, u.y, u.z)).ToList());
                else part.SetUVs(channel, picked);
            }
            if (weights.Length == vertices.Length) part.boneWeights = order.Select(v => weights[v]).ToArray();
            part.bindposes = full.bindposes;
            part.triangles = kept.Select(v => newIndex[v]).ToArray();
            part.RecalculateBounds();
            return part;
        }

        /// <summary>
        /// A colour variant has no mesh of its own. Its bake is held, vertex by vertex, against the very mesh it is
        /// going to draw: the saved mesh of its model figure, not a fresh build of the model's bake, which may be
        /// newer than what was saved. Only if both are the same shape the variant takes that mesh. A variant whose
        /// parts or sliders drifted from its model is refused instead of being shown with the wrong body.
        /// </summary>
        static Mesh SharedGameMesh(BozoFigureRecipes.Figure recipe)
        {
            var model = BozoFigureRecipes.Load().First(f => f.id == recipe.sameShapeAs);
            var shared = AssetDatabase.LoadAssetAtPath<Mesh>(GameMeshPath(model.id));
            if (shared == null) throw new InvalidOperationException("Grundfigur zuerst erzeugen: " + model.id + " (für " + recipe.id + ").");
            Mesh own = BuildGameMesh(recipe);
            try
            {
                string difference = MeshDifference(own, shared);
                if (difference != null) throw new InvalidOperationException(recipe.id + " hat nicht die Form von " + model.id + ": " + difference);
            }
            finally { Object.DestroyImmediate(own); }
            return shared;
        }

        static string MeshDifference(Mesh a, Mesh b)
        {
            if (a.vertexCount != b.vertexCount) return "Anzahl der Eckpunkte " + a.vertexCount + " statt " + b.vertexCount;
            int[] ta = a.triangles, tb = b.triangles;
            if (ta.Length != tb.Length) return "Anzahl der Dreiecke";
            for (int i = 0; i < ta.Length; i++) if (ta[i] != tb[i]) return "Dreiecke";
            Vector3[] va = a.vertices, vb = b.vertices;
            for (int i = 0; i < va.Length; i++) if ((va[i] - vb[i]).sqrMagnitude > 1e-8f) return "Lage der Eckpunkte";
            Vector2[] ua = a.uv, ub = b.uv;
            if (ua.Length != ub.Length) return "Texturkoordinaten";
            for (int i = 0; i < ua.Length; i++) if ((ua[i] - ub[i]).sqrMagnitude > 1e-10f) return "Texturkoordinaten";
            if (a.bindposes.Length != b.bindposes.Length) return "Knochen";
            return null;
        }

        /// <summary>The game mesh of a figure in memory, built from its bake; not saved.</summary>
        static Mesh BuildGameMesh(BozoFigureRecipes.Figure recipe)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(BakedPath(recipe.id));
            if (source == null) throw new InvalidOperationException("Gebackene Figur fehlt: " + recipe.id);
            var skins = source.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s => s.sharedMesh != null).ToArray();
            if (skins.Length != 1) throw new InvalidOperationException("Erwartet genau ein zusammengefügtes Mesh: " + recipe.id);
            Mesh full = skins[0].sharedMesh;
            Vector3[] vertices = full.vertices, normals = full.normals;
            Vector4[] tangents = full.tangents;
            int count = vertices.Length;
            var moved = new Vector3[count]; var turned = new Vector3[count]; var leaned = new Vector3[count];
            for (int shape = 0; shape < full.blendShapeCount; shape++)
            {
                float weight = skins[0].GetBlendShapeWeight(shape);
                if (weight == 0f) continue;
                // Unity blends linearly from the rest shape to the first frame and between neighbouring frames.
                int upper = 0, last = full.GetBlendShapeFrameCount(shape) - 1;
                while (upper < last && full.GetBlendShapeFrameWeight(shape, upper) < weight) upper++;
                float upperWeight = full.GetBlendShapeFrameWeight(shape, upper);
                float lowerWeight = upper > 0 ? full.GetBlendShapeFrameWeight(shape, upper - 1) : 0f;
                if (Mathf.Approximately(upperWeight, lowerWeight)) throw new InvalidOperationException("Unlesbare Blend Shape: " + full.GetBlendShapeName(shape));
                float blend = (weight - lowerWeight) / (upperWeight - lowerWeight);
                full.GetBlendShapeFrameVertices(shape, upper, moved, turned, leaned);
                AddDeltas(vertices, normals, tangents, moved, turned, leaned, blend);
                if (upper == 0) continue;
                full.GetBlendShapeFrameVertices(shape, upper - 1, moved, turned, leaned);
                AddDeltas(vertices, normals, tangents, moved, turned, leaned, 1f - blend);
            }
            var mesh = Object.Instantiate(full);
            mesh.name = recipe.id + "_GameMesh";
            mesh.ClearBlendShapes();
            mesh.vertices = vertices;
            if (normals.Length == count) { for (int i = 0; i < count; i++) normals[i].Normalize(); mesh.normals = normals; }
            if (tangents.Length == count)
            {
                for (int i = 0; i < count; i++)
                {
                    Vector3 direction = ((Vector3)tangents[i]).normalized;
                    tangents[i] = new Vector4(direction.x, direction.y, direction.z, tangents[i].w);
                }
                mesh.tangents = tangents;
            }
            if (recipe.expressions) KeepExpressions(full, skins[0], mesh);
            if (recipe.tuckHair)
                Debug.Log("BOZO_HAIR_TUCKED " + recipe.id + " triangles=" + FigureHairTuck.Apply(mesh, vertices, PartRanges(recipe.id, count)));
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Prefix of the pack's facial expression shapes (blink, mouth, brows) that the game drives.</summary>
        public const string ExpressionPrefix = "Expression_";

        /// <summary>
        /// Put the face's expression shapes back on the flattened game mesh, frame by frame and unchanged: they move only
        /// the face, on top of the baked look. A shape that was already set at bake time is part of the look and stays
        /// baked flat.
        /// </summary>
        static void KeepExpressions(Mesh full, SkinnedMeshRenderer skin, Mesh mesh)
        {
            int count = full.vertexCount;
            var moved = new Vector3[count]; var turned = new Vector3[count]; var leaned = new Vector3[count];
            for (int shape = 0; shape < full.blendShapeCount; shape++)
            {
                string name = full.GetBlendShapeName(shape);
                if (!name.StartsWith(ExpressionPrefix, StringComparison.Ordinal) || skin.GetBlendShapeWeight(shape) != 0f) continue;
                for (int frame = 0; frame < full.GetBlendShapeFrameCount(shape); frame++)
                {
                    full.GetBlendShapeFrameVertices(shape, frame, moved, turned, leaned);
                    mesh.AddBlendShapeFrame(name, full.GetBlendShapeFrameWeight(shape, frame), moved, turned, leaned);
                }
            }
        }

        static void AddDeltas(Vector3[] vertices, Vector3[] normals, Vector4[] tangents, Vector3[] moved, Vector3[] turned, Vector3[] leaned, float amount)
        {
            bool hasNormals = normals.Length == vertices.Length, hasTangents = tangents.Length == vertices.Length;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] += moved[i] * amount;
                if (hasNormals) normals[i] += turned[i] * amount;
                if (hasTangents) tangents[i] += (Vector4)(leaned[i] * amount);
            }
        }

        /// <summary>
        /// The pack bakes its normal atlas in a two-channel packing (x in alpha, y in green, red and blue empty) and
        /// saves it as an ordinary texture. This project reads normal maps as plain x/y/z, so every surface of the
        /// baked figure seems to face away from the light: the figure looks as if it stood in the dark and its eyes
        /// turn glassy. The game material is a copy of the baked one whose normal atlas is written as a regular
        /// normal map and imported as one, so each platform decodes it correctly.
        /// </summary>
        static Material GameMaterial(BozoFigureRecipes.Figure recipe)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(BakedPath(recipe.id));
            if (source == null) throw new InvalidOperationException("Gebackene Figur fehlt: " + recipe.id);
            Material baked = source.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s => s.sharedMesh != null)
                .Select(s => s.sharedMaterial).FirstOrDefault();
            if (baked == null) throw new InvalidOperationException("Gebackene Figur ohne Material: " + recipe.id);
            string folder = BakeFolder + "/" + recipe.id + "/";
            string path = folder + recipe.id + "_GameMaterial.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(baked); AssetDatabase.CreateAsset(material, path); }
            else { material.shader = baked.shader; material.CopyPropertiesFromMaterial(baked); } // keep the asset identity
            Texture packed = baked.HasProperty("_Normal") ? baked.GetTexture("_Normal") : null;
            if (packed != null)
            {
                string normalPath = folder + recipe.id + "_GameNormal.png";
                string normalStamp = "normal" + FabricStamp(recipe);
                if (Outdated(AssetDatabase.GetAssetPath(packed), normalPath, normalStamp))
                    WriteNormalMap(AssetDatabase.GetAssetPath(packed), normalPath, recipe.texture, normalStamp, FabricRelief(recipe));
                material.SetTexture("_Normal", AssetDatabase.LoadAssetAtPath<Texture2D>(SharedNormal(recipe, normalPath)));
            }
            Texture colours = baked.HasProperty("_Diffuse") ? baked.GetTexture("_Diffuse") : null;
            float light = BozoFigureRecipes.LoadLight();
            // With a fabric the colours are rewritten even without a lift (a curve of exponent 1 keeps the brightness).
            if (colours != null && (light < 1f || HasFabric(recipe)))
            {
                string colourPath = folder + recipe.id + "_GameColours.png";
                string stamp = "light=" + light.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + FabricStamp(recipe);
                if (Outdated(AssetDatabase.GetAssetPath(colours), colourPath, stamp))
                    WriteLiftedColours(AssetDatabase.GetAssetPath(colours), colourPath, recipe.texture, light, stamp, FabricWeave(recipe));
                material.SetTexture("_Diffuse", AssetDatabase.LoadAssetAtPath<Texture2D>(colourPath));
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// A colour variant wears its model's parts in the same places, so its normal atlas is normally the model's,
        /// byte for byte. It then draws the model's texture: the game carries one normal atlas per model instead of
        /// one per look. Its own copy stays in the private folder, where nothing of the game refers to it.
        /// </summary>
        static string SharedNormal(BozoFigureRecipes.Figure recipe, string ownPath)
        {
            if (string.IsNullOrEmpty(recipe.sameShapeAs)) return ownPath;
            string modelPath = BakeFolder + "/" + recipe.sameShapeAs + "/" + recipe.sameShapeAs + "_GameNormal.png";
            if (!File.Exists(modelPath) || !File.Exists(ownPath) || AssetDatabase.LoadAssetAtPath<Texture2D>(modelPath) == null) return ownPath;
            return File.ReadAllBytes(modelPath).SequenceEqual(File.ReadAllBytes(ownPath)) ? modelPath : ownPath;
        }

        /// <summary>A derived texture is rebuilt when the baked source is newer or it was made with other settings.</summary>
        static bool Outdated(string sourcePath, string derivedPath, string stamp)
        {
            var importer = AssetImporter.GetAtPath(derivedPath);
            return importer == null || importer.userData != stamp || !File.Exists(derivedPath) ||
                   File.GetLastWriteTimeUtc(sourcePath) > File.GetLastWriteTimeUtc(derivedPath);
        }

        /// <summary>
        /// Lift the brightness of the colour atlas (brightness to the power of <paramref name="light"/>, hue and
        /// saturation unchanged, transparency unchanged). The pack multiplies each colour with its painted shading;
        /// unlifted, the figures stand darker than the sunlit world around them.
        /// </summary>
        static void WriteLiftedColours(string sourcePath, string targetPath, int size, float light, string stamp,
            Action<Color32[], int, int> post = null)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(sourcePath))) throw new IOException("Unlesbare Farbtextur: " + sourcePath);
                Color32[] pixels = texture.GetPixels32();
                var curve = new byte[256];
                for (int value = 0; value < 256; value++) curve[value] = (byte)Mathf.RoundToInt(Mathf.Pow(value / 255f, light) * 255f);
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 p = pixels[i];
                    int brightest = Mathf.Max(p.r, Mathf.Max(p.g, p.b));
                    if (brightest == 0) continue;
                    // Scaling all three channels by the same factor keeps hue and saturation.
                    float gain = curve[brightest] / (float)brightest;
                    pixels[i] = new Color32((byte)Mathf.Min(255, Mathf.RoundToInt(p.r * gain)), (byte)Mathf.Min(255, Mathf.RoundToInt(p.g * gain)),
                        (byte)Mathf.Min(255, Mathf.RoundToInt(p.b * gain)), p.a);
                }
                post?.Invoke(pixels, texture.width, texture.height);
                texture.SetPixels32(pixels);
                File.WriteAllBytes(targetPath, texture.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(targetPath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = false;
            importer.maxTextureSize = size;
            importer.mipmapEnabled = true;
            importer.userData = stamp;
            importer.SaveAndReimport();
        }

        // ---------------------------------------------------------------- own fabrics (owner 2026-11-23)

        static readonly System.Globalization.CultureInfo Invariant = System.Globalization.CultureInfo.InvariantCulture;

        /// <summary>Whether any part is worn with a fabric of its own or darker and duller than it was baked.</summary>
        static bool HasFabric(BozoFigureRecipes.Figure recipe) { return recipe.outfits.Any(o => !string.IsNullOrEmpty(o.fabric) || o.Graded); }

        /// <summary>The fabric settings as part of a texture's stamp: a changed fabric rebuilds the derived textures.</summary>
        static string FabricStamp(BozoFigureRecipes.Figure recipe)
        {
            if (!HasFabric(recipe)) return "";
            // "v3": skin that outfits carry or let through stays free of fabric; older derived textures are rebuilt.
            var used = recipe.outfits.Where(o => !string.IsNullOrEmpty(o.fabric) || o.Graded).ToList();
            // The maps' content is part of the stamp: a regenerated map under the same name rebuilds both atlases.
            // (A part without a grade keeps the stamp it had before grades existed, so its textures are not rebuilt.)
            return ";fabric=v3," + string.Join(",", used.Select(o =>
                o.slot + ":" + o.fabric + ":" + o.fabricStrength.ToString("0.###", Invariant) + ":" + o.fabricScale.ToString("0.###", Invariant) +
                (o.Graded ? ":gain=" + o.gain.ToString("0.###", Invariant) + ":saturation=" + o.saturation.ToString("0.###", Invariant) : ""))) +
                ";maps=" + string.Join(",", used.Where(o => !string.IsNullOrEmpty(o.fabric)).Select(o => o.fabric).Distinct().OrderBy(n => n, StringComparer.Ordinal)
                    .Select(n => n + ":" + FileFingerprint(FabricPath(n))));
        }

        /// <summary>FNV-1a over a file's bytes, as hex; "missing" for an absent file (the bake then reports it).</summary>
        static string FileFingerprint(string path)
        {
            if (!File.Exists(path)) return "missing";
            ulong h = 14695981039346656037UL;
            unchecked { foreach (byte b in File.ReadAllBytes(path)) { h ^= b; h *= 1099511628211UL; } }
            return h.ToString("x16", Invariant);
        }

        /// <summary>One worn part on the atlas: its pixels, its fabric (Height is null without one) and its grade.</summary>
        sealed class FabricLayer { public bool[] Mask; public float[] Height; public int Size; public float Strength, Scale, Gain = 1f, Saturation = 1f; }

        static string FabricPath(string name) { return Path.Combine(Directory.GetCurrentDirectory(), "Tools/Art/fabric/" + name + ".png"); }

        /// <summary>The runs of vertices of each part in the merged mesh, from the record of the bake (type, start, count).</summary>
        static List<KeyValuePair<string, Vector2Int>> PartRanges(string id, int vertexCount)
        {
            if (!File.Exists(PartsRecordPath(id))) throw new InvalidOperationException(id + ": keine Aufzeichnung der Teile; die Figur muss neu erzeugt werden.");
            var ranges = new List<KeyValuePair<string, Vector2Int>>();
            int at = 0;
            foreach (string line in File.ReadAllLines(PartsRecordPath(id)))
            {
                string[] field = line.Split('\t');
                int count = int.Parse(field[1], Invariant);
                ranges.Add(new KeyValuePair<string, Vector2Int>(field[0], new Vector2Int(at, count)));
                at += count;
            }
            if (at != vertexCount) throw new InvalidOperationException(id + ": die Aufzeichnung der Teile passt nicht zum Modell.");
            return ranges;
        }

        /// <summary>For every part with a fabric: the atlas pixels its UV triangles cover (with a seam margin) and the fabric.</summary>
        static List<FabricLayer> FabricLayers(BozoFigureRecipes.Figure recipe, int width, int height)
        {
            var layers = new List<FabricLayer>();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(BakedPath(recipe.id));
            if (source == null) throw new InvalidOperationException("Gebackene Figur fehlt: " + recipe.id);
            SkinnedMeshRenderer body = source.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.sharedMesh != null);
            Mesh mesh = body.sharedMesh;
            bool[] skinColoured = SkinTexels(recipe, body.sharedMaterial, width, height);
            Vector2[] uv = mesh.uv;
            int[] triangles = mesh.triangles;
            var ranges = PartRanges(recipe.id, mesh.vertexCount);
            // Outfits bring their own skin pieces (a neckline, a bare shoulder) that borrow the body's atlas islands;
            // whatever lies on the body's or the head's islands stays skin and never takes a fabric.
            var skinVertex = new bool[mesh.vertexCount];
            foreach (var run in ranges.Where(r => r.Key == "Body" || r.Key == "Head"))
                for (int v = run.Value.x; v < run.Value.x + run.Value.y; v++) skinVertex[v] = true;
            var skin = new bool[width * height];
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                if (skinVertex[a] && skinVertex[b] && skinVertex[c]) FillTriangle(skin, width, height, uv[a], uv[b], uv[c]);
            }
            foreach (var outfit in recipe.outfits.Where(o => !string.IsNullOrEmpty(o.fabric) || o.Graded))
            {
                bool woven = !string.IsNullOrEmpty(outfit.fabric);
                if (woven && (!(outfit.fabricStrength >= 0f && outfit.fabricStrength <= 1f) || !(outfit.fabricScale > 0f && outfit.fabricScale <= 64f)))
                    throw new InvalidOperationException(recipe.id + ": ungültige Stoffwerte für " + outfit.slot);
                var inside = new bool[mesh.vertexCount];
                bool any = false;
                foreach (var run in ranges.Where(r => r.Key == outfit.slot))
                    for (int v = run.Value.x; v < run.Value.x + run.Value.y; v++) { inside[v] = true; any = true; }
                if (!any) throw new InvalidOperationException(recipe.id + ": das Teil " + outfit.slot + " fehlt in der Aufzeichnung.");
                var mask = new bool[width * height];
                for (int t = 0; t + 2 < triangles.Length; t += 3)
                {
                    int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                    if (inside[a] && inside[b] && inside[c] && !OnMask(skin, width, height, (uv[a] + uv[b] + uv[c]) / 3f))
                        FillTriangle(mask, width, height, uv[a], uv[b], uv[c]);
                }
                mask = Grow(mask, width, height, 2);
                for (int i = 0; i < mask.Length; i++) if (skin[i] || skinColoured[i]) mask[i] = false;
                if (!woven)
                {
                    layers.Add(new FabricLayer { Mask = mask, Gain = outfit.gain, Saturation = outfit.saturation });
                    continue;
                }
                string path = FabricPath(outfit.fabric);
                if (!File.Exists(path)) throw new InvalidOperationException("Stoff fehlt: " + path);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                try
                {
                    if (!texture.LoadImage(File.ReadAllBytes(path)) || texture.width != texture.height) throw new IOException("Unlesbarer Stoff: " + path);
                    Color32[] pixels = texture.GetPixels32();
                    var heights = new float[pixels.Length];
                    for (int i = 0; i < pixels.Length; i++) heights[i] = pixels[i].r / 255f;
                    layers.Add(new FabricLayer { Mask = mask, Height = heights, Size = texture.width, Strength = outfit.fabricStrength, Scale = outfit.fabricScale,
                        Gain = outfit.gain, Saturation = outfit.saturation });
                }
                finally { Object.DestroyImmediate(texture); }
            }
            return layers;
        }

        static void FillTriangle(bool[] mask, int width, int height, Vector2 a, Vector2 b, Vector2 c)
        {
            float ax = a.x * width, ay = a.y * height, bx = b.x * width, by = b.y * height, cx = c.x * width, cy = c.y * height;
            float area = (bx - ax) * (cy - ay) - (cx - ax) * (by - ay);
            if (Mathf.Abs(area) < 1e-6f) return;
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ax, Mathf.Min(bx, cx)))), maxX = Mathf.Min(width - 1, Mathf.CeilToInt(Mathf.Max(ax, Mathf.Max(bx, cx))));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ay, Mathf.Min(by, cy)))), maxY = Mathf.Min(height - 1, Mathf.CeilToInt(Mathf.Max(ay, Mathf.Max(by, cy))));
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float px = x + .5f, py = y + .5f;
                    float w0 = (bx - px) * (cy - py) - (cx - px) * (by - py);
                    float w1 = (cx - px) * (ay - py) - (ax - px) * (cy - py);
                    float w2 = (ax - px) * (by - py) - (bx - px) * (ay - py);
                    if (area > 0f ? (w0 >= 0f && w1 >= 0f && w2 >= 0f) : (w0 <= 0f && w1 <= 0f && w2 <= 0f)) mask[y * width + x] = true;
                }
        }

        /// <summary>
        /// Skin that a tight outfit lets through. Such outfits share the body's atlas layout and are painted over the
        /// body's skin, so where they are open (a cut-out, a bare shoulder) their own triangles carry skin. Those pixels
        /// are told apart by colour: the hue and saturation of the recipe's skin tones (tone, shade, highlight of body and
        /// head), at least half as bright as the brightest tone. Cloth colours are pre-darkened for the lift and stay below.
        /// </summary>
        static bool[] SkinTexels(BozoFigureRecipes.Figure recipe, Material baked, int width, int height)
        {
            var result = new bool[width * height];
            var tones = new List<Vector3>();
            foreach (var outfit in recipe.outfits.Where(o => o.slot == "Body" || o.slot == "Head"))
                foreach (string hex in outfit.colors.Take(3))
                    if (ColorUtility.TryParseHtmlString(hex, out Color c)) { Color.RGBToHSV(c, out float h, out float sat, out float v); tones.Add(new Vector3(h, sat, v)); }
            Texture atlas = baked != null && baked.HasProperty("_Diffuse") ? baked.GetTexture("_Diffuse") : null;
            if (tones.Count == 0 || atlas == null) return result;
            float centre = tones[0].x, low = 0f, high = 0f, satLow = 1f, satHigh = 0f, bright = 0f;
            foreach (Vector3 tone in tones)
            {
                float offset = Mathf.Repeat(tone.x - centre + .5f, 1f) - .5f;
                low = Mathf.Min(low, offset); high = Mathf.Max(high, offset);
                satLow = Mathf.Min(satLow, tone.y); satHigh = Mathf.Max(satHigh, tone.y);
                bright = Mathf.Max(bright, tone.z);
            }
            const float hueMargin = 8f / 360f, satMargin = .12f;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(atlas)))) throw new IOException("Unlesbare Farbtextur: " + AssetDatabase.GetAssetPath(atlas));
                Color32[] pixels = texture.GetPixels32();
                int w = texture.width, h = texture.height;
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        Color32 p = pixels[Mathf.Min(h - 1, y * h / height) * w + Mathf.Min(w - 1, x * w / width)];
                        Color.RGBToHSV(p, out float hue, out float sat, out float value);
                        float offset = Mathf.Repeat(hue - centre + .5f, 1f) - .5f;
                        result[y * width + x] = value >= .5f * bright && offset >= low - hueMargin && offset <= high + hueMargin &&
                            sat >= satLow - satMargin && sat <= satHigh + satMargin;
                    }
            }
            finally { Object.DestroyImmediate(texture); }
            return result;
        }

        /// <summary>Whether an atlas point lies on a marked pixel.</summary>
        static bool OnMask(bool[] mask, int width, int height, Vector2 uv)
        {
            int x = Mathf.FloorToInt(uv.x * width), y = Mathf.FloorToInt(uv.y * height);
            return x >= 0 && y >= 0 && x < width && y < height && mask[y * width + x];
        }

        /// <summary>Widen a mask by some pixels, so the fabric also covers the seams the texture filter reaches.</summary>
        static bool[] Grow(bool[] mask, int width, int height, int pixels)
        {
            for (int step = 0; step < pixels; step++)
            {
                var next = (bool[])mask.Clone();
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        if (mask[y * width + x]) continue;
                        if ((x > 0 && mask[y * width + x - 1]) || (x + 1 < width && mask[y * width + x + 1]) ||
                            (y > 0 && mask[(y - 1) * width + x]) || (y + 1 < height && mask[(y + 1) * width + x])) next[y * width + x] = true;
                    }
                mask = next;
            }
            return mask;
        }

        /// <summary>The fabric's height at an atlas point, repeating, smoothly between its pixels.</summary>
        static float FabricHeight(FabricLayer layer, float u, float v)
        {
            float fx = u * layer.Scale * layer.Size, fy = v * layer.Scale * layer.Size;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            int n = layer.Size;
            int ax = ((x0 % n) + n) % n, ay = ((y0 % n) + n) % n, bx = (ax + 1) % n, by = (ay + 1) % n;
            float h00 = layer.Height[ay * n + ax], h10 = layer.Height[ay * n + bx], h01 = layer.Height[by * n + ax], h11 = layer.Height[by * n + bx];
            return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), ty);
        }

        /// <summary>
        /// The worn look in the colours: first the part's grade (duller, darker), then the weave: raised threads a little
        /// lighter, the gaps between them darker.
        /// </summary>
        static Action<Color32[], int, int> FabricWeave(BozoFigureRecipes.Figure recipe)
        {
            if (!HasFabric(recipe)) return null;
            return (pixels, width, height) =>
            {
                foreach (FabricLayer layer in FabricLayers(recipe, width, height))
                    for (int y = 0; y < height; y++)
                        for (int x = 0; x < width; x++)
                        {
                            int i = y * width + x;
                            if (!layer.Mask[i]) continue;
                            float gain = layer.Gain;
                            if (layer.Height != null) gain *= Mathf.Lerp(1f, .72f + .42f * FabricHeight(layer, (x + .5f) / width, (y + .5f) / height), layer.Strength);
                            Color32 p = pixels[i];
                            float grey = (1f - layer.Saturation) * (.2126f * p.r + .7152f * p.g + .0722f * p.b), s = layer.Saturation;
                            pixels[i] = new Color32((byte)Mathf.Clamp(Mathf.RoundToInt((s * p.r + grey) * gain), 0, 255),
                                (byte)Mathf.Clamp(Mathf.RoundToInt((s * p.g + grey) * gain), 0, 255),
                                (byte)Mathf.Clamp(Mathf.RoundToInt((s * p.b + grey) * gain), 0, 255), p.a);
                        }
            };
        }

        /// <summary>Weave as relief: the normal map leans with the fabric's slopes, so light picks out the threads.</summary>
        static Action<Color32[], int, int> FabricRelief(BozoFigureRecipes.Figure recipe)
        {
            if (!HasFabric(recipe)) return null;
            return (pixels, width, height) =>
            {
                foreach (FabricLayer layer in FabricLayers(recipe, width, height))
                {
                    if (layer.Height == null) continue; // a grade without a fabric changes colours only
                    float du = 1f / width, dv = 1f / height, k = 1.1f * layer.Strength;
                    for (int y = 0; y < height; y++)
                        for (int x = 0; x < width; x++)
                        {
                            int i = y * width + x;
                            if (!layer.Mask[i]) continue;
                            float u = (x + .5f) * du, v = (y + .5f) * dv;
                            float sx = FabricHeight(layer, u + du, v) - FabricHeight(layer, u - du, v);
                            float sy = FabricHeight(layer, u, v + dv) - FabricHeight(layer, u, v - dv);
                            Color32 p = pixels[i];
                            var n = new Vector3(p.r / 255f * 2f - 1f - sx * k, p.g / 255f * 2f - 1f - sy * k, p.b / 255f * 2f - 1f);
                            if (n.z < .2f) n.z = .2f;
                            n.Normalize();
                            pixels[i] = new Color32((byte)Mathf.RoundToInt((n.x * .5f + .5f) * 255f), (byte)Mathf.RoundToInt((n.y * .5f + .5f) * 255f),
                                (byte)Mathf.RoundToInt((n.z * .5f + .5f) * 255f), 255);
                        }
                }
            };
        }

        static void WriteNormalMap(string packedPath, string normalPath, int size, string stamp = "normal", Action<Color32[], int, int> post = null)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(packedPath))) throw new IOException("Unlesbare Normal-Textur: " + packedPath);
                Color32[] pixels = texture.GetPixels32();
                bool twoChannel = true;
                for (int i = 0; i < pixels.Length && twoChannel; i += 7) twoChannel = pixels[i].r <= 8 && pixels[i].b <= 8;
                if (twoChannel)
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        Color32 p = pixels[i];
                        if (p.a == 0 && p.g == 0) { pixels[i] = new Color32(128, 128, 255, 255); continue; } // unused atlas area
                        float x = p.a / 255f * 2f - 1f, y = p.g / 255f * 2f - 1f;
                        float z = Mathf.Sqrt(Mathf.Clamp01(1f - x * x - y * y));
                        pixels[i] = new Color32(p.a, p.g, (byte)Mathf.RoundToInt((z * .5f + .5f) * 255f), 255);
                    }
                post?.Invoke(pixels, texture.width, texture.height);
                texture.SetPixels32(pixels);
                File.WriteAllBytes(normalPath, texture.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(normalPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(normalPath);
            importer.textureType = TextureImporterType.NormalMap;
            importer.maxTextureSize = size;
            importer.mipmapEnabled = true;
            importer.userData = stamp;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// The body sliders of the pack scale bones (shoulders, chest, limbs). The pack's avatar describes the
        /// unscaled skeleton, so everything Unity solves on the avatar lands beside the real bones: the hero's sword
        /// hand missed its grip by four centimetres. Each reshaped figure gets its own avatar: the pack's description
        /// with the bone scales of the baked figure.
        /// </summary>
        static Avatar GameAvatar(BozoFigureRecipes.Figure recipe, BozoFigureRecipes.Figure[] recipes)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(BakedPath(recipe.id));
            if (source == null) throw new InvalidOperationException("Gebackene Figur fehlt: " + recipe.id);
            // A colour variant has its model's preset and body sliders (checked with the recipes), so its bones carry
            // the same scales: it uses the avatar its model's visual uses now instead of bringing a copy of it into the
            // game. That is the pack's own avatar when the model's bones are not reshaped any more.
            if (!string.IsNullOrEmpty(recipe.sameShapeAs))
            {
                Avatar shared = VisualAvatar(recipes.FirstOrDefault(f => f.id == recipe.sameShapeAs));
                if (shared != null) return shared;
            }
            GameObject figure = Object.Instantiate(source);
            try
            {
                Animator animator = figure.GetComponentsInChildren<Animator>(true).FirstOrDefault(a => a.avatar != null && a.avatar.isHuman);
                if (animator == null) throw new InvalidOperationException("Gebackene Figur ohne humanoides Rig: " + recipe.id);
                HumanDescription description = animator.avatar.humanDescription;
                SkeletonBone[] skeleton = description.skeleton;
                var bones = new Dictionary<string, Transform>();
                foreach (Transform bone in animator.GetComponentsInChildren<Transform>(true))
                    if (!bones.ContainsKey(bone.name)) bones.Add(bone.name, bone);
                bool reshaped = false;
                for (int i = 0; i < skeleton.Length; i++)
                {
                    Transform bone;
                    if (!bones.TryGetValue(skeleton[i].name, out bone) || (bone.localScale - skeleton[i].scale).sqrMagnitude < 1e-8f) continue;
                    skeleton[i].scale = bone.localScale;
                    reshaped = true;
                }
                if (!reshaped) return animator.avatar;
                description.skeleton = skeleton;
                Avatar avatar = AvatarBuilder.BuildHumanAvatar(animator.gameObject, description);
                if (avatar == null || !avatar.isValid || !avatar.isHuman) throw new InvalidOperationException("Avatar der Figur ist ungültig: " + recipe.id);
                avatar.name = recipe.id + "_GameAvatar";
                string path = BakeFolder + "/" + recipe.id + "/" + recipe.id + "_GameAvatar.asset";
                var saved = AssetDatabase.LoadAssetAtPath<Avatar>(path);
                if (saved == null) { AssetDatabase.CreateAsset(avatar, path); return avatar; }
                EditorUtility.CopySerialized(avatar, saved); // keep the asset identity: the hero prefabs refer to it
                EditorUtility.SetDirty(saved);
                Object.DestroyImmediate(avatar);
                return saved;
            }
            finally { Object.DestroyImmediate(figure); }
        }

        /// <summary>The avatar a figure's game visual uses now (models are rebuilt before their variants), or null.</summary>
        static Avatar VisualAvatar(BozoFigureRecipes.Figure figure)
        {
            foreach (string key in figure != null ? figure.keys ?? new string[0] : new string[0])
            {
                var visual = AssetDatabase.LoadAssetAtPath<GameObject>(Visuals + key + ".prefab");
                Animator animator = visual != null ? visual.GetComponentInChildren<Animator>(true) : null;
                if (animator != null && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman) return animator.avatar;
            }
            return null;
        }

        /// <summary>
        /// A recipe key names a file among all game visuals. It may create a new visual, renew a figure or replace
        /// one of the former human figures, but never overwrite other art (monsters, weapons, worlds) by mistake.
        /// </summary>
        public static bool MayWrite(string key)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(Visuals + key + ".prefab");
            if (existing == null || Array.IndexOf(BozoFigureRecipes.RequiredKeys, key) >= 0) return true;
            // Every visual this tool saves holds the pack's figure under this name; it stays a figure even while the
            // mesh it drew is gone (a model figure was rebuilt in two parts before its colour variants).
            if (existing.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("BozoFigure_", StringComparison.Ordinal))) return true;
            SkinnedMeshRenderer[] skins = existing.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            return skins.Length > 0 && skins.All(s => s.sharedMesh != null &&
                AssetDatabase.GetAssetPath(s.sharedMesh).StartsWith(BakeFolder + "/", StringComparison.Ordinal));
        }

        static void Save(BozoFigureRecipes.Figure recipe, Mesh mesh, Mesh[] parts, Material material, Avatar avatar, string path, string key, bool gameVisual)
        {
            if (gameVisual && !MayWrite(key))
                throw new InvalidOperationException("Visual " + key + " ist keine Figur dieses Systems und wird nicht überschrieben.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(BakedPath(recipe.id));
            if (source == null) throw new InvalidOperationException("Gebackene Figur fehlt: " + recipe.id);
            // Game visuals idle and walk with the pack's own humanoid clips; source figures get their controller
            // from the tool that consumes them (hero builder, resting patients).
            RuntimeAnimatorController controller = gameVisual ? Locomotion(recipe.motion) : null;
            var root = new GameObject(key);
            try
            {
                var art = Object.Instantiate(source, root.transform, false);
                art.name = "BozoFigure_" + recipe.id;
                art.transform.localPosition = Vector3.zero; art.transform.localRotation = Quaternion.identity;
                MakePassive(art);
                SkinnedMeshRenderer[] skins = art.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (parts == null)
                    foreach (SkinnedMeshRenderer skin in skins)
                    {
                        skin.sharedMesh = mesh;
                        skin.sharedMaterial = material;
                    }
                else
                {
                    // Two renderers on the one skeleton: the body, and the head that another figure's head can replace.
                    if (skins.Length != 1) throw new InvalidOperationException("Erwartet genau ein zusammengefügtes Mesh: " + recipe.id);
                    SkinnedMeshRenderer body = skins[0];
                    var headObject = Object.Instantiate(body.gameObject, body.transform.parent, false);
                    body.name = FigureComposer.BodyPart; headObject.name = FigureComposer.HeadPart;
                    SkinnedMeshRenderer head = headObject.GetComponent<SkinnedMeshRenderer>();
                    body.sharedMesh = parts[0]; head.sharedMesh = parts[1];
                    body.sharedMaterial = material; head.sharedMaterial = material;
                    head.bones = body.bones; head.rootBone = body.rootBone; head.localBounds = body.localBounds;
                }
                var animators = art.GetComponentsInChildren<Animator>(true);
                if (animators.Length == 1) animators[0].avatar = avatar;
                if (animators.Length != 1 || animators[0].avatar == null || !animators[0].avatar.isHuman || !animators[0].avatar.isValid)
                    throw new InvalidOperationException("Erwartet genau ein gültiges humanoides Rig: " + recipe.id);
                animators[0].runtimeAnimatorController = controller;
                animators[0].applyRootMotion = false;
                animators[0].cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                Bounds bounds = EntryWorldArtBuilder.VisibleBody(art);
                if (bounds.size.y < .2f) throw new InvalidOperationException("Figur ohne sichtbaren Körper: " + recipe.id);
                art.transform.localScale *= recipe.height / bounds.size.y;
                bounds = EntryWorldArtBuilder.VisibleBody(art);
                art.transform.localPosition -= new Vector3(bounds.center.x, 0f, bounds.center.z);
                EntryWorldArtBuilder.StandOnVisibleFeet(art);
                // What the figure carries hangs on its bones only now: its height and its stand are measured on the body alone.
                if (gameVisual) FigureProps.Attach(recipe, root.transform, animators[0]);
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new IOException("Konnte nicht speichern: " + path);
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>
        /// Idle and walk from the pack (humanoid clips), switched by the float "MoveMagnitude" exactly like the game's
        /// existing villager controller (walk above 0.16, idle below 0.1).
        /// </summary>
        public static RuntimeAnimatorController Locomotion(string motion)
        {
            string suffix = motion == "f" ? "F" : "M";
            string folder = BakeFolder + "/Animation";
            string path = folder + "/BozoLocomotion" + suffix + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(path);
            if (controller != null) return controller;
            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(VendorRoot + "/Animations/BMAC_" + suffix + "_Idle.anim");
            var walk = AssetDatabase.LoadAssetAtPath<AnimationClip>(VendorRoot + "/Animations/BMAC_" + suffix + "_Walk.anim");
            if (idle == null || walk == null) throw new InvalidOperationException("Idle-/Walk-Clips des Pakets fehlen.");
            EnsureFolder(folder);
            controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("MoveMagnitude", AnimatorControllerParameterType.Float);
            var machine = controller.layers[0].stateMachine;
            var idleState = machine.AddState("Idle"); idleState.motion = idle;
            var walkState = machine.AddState("Walk"); walkState.motion = walk;
            machine.defaultState = idleState;
            var toWalk = idleState.AddTransition(walkState);
            toWalk.hasExitTime = false; toWalk.duration = .15f; toWalk.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Greater, .16f, "MoveMagnitude");
            var toIdle = walkState.AddTransition(idleState);
            toIdle.hasExitTime = false; toIdle.duration = .15f; toIdle.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Less, .1f, "MoveMagnitude");
            EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
            return controller;
        }

        /// <summary>Only transforms, meshes, renderers and the animator stay: exactly what PassiveArtVisuals accepts.</summary>
        static void MakePassive(GameObject art)
        {
            foreach (var extra in art.GetComponentsInChildren<Camera>(true).Select(c => c.gameObject)
                         .Concat(art.GetComponentsInChildren<Light>(true).Select(l => l.gameObject)).Distinct().ToArray())
                Object.DestroyImmediate(extra);
            for (int pass = 0; pass < 3; pass++) // dependent components (RequireComponent) go in later passes
                foreach (Component component in art.GetComponentsInChildren<Component>(true))
                {
                    if (component == null || component is Transform || component is MeshFilter || component is MeshRenderer ||
                        component is SkinnedMeshRenderer || component is Animator || component is LODGroup) continue;
                    Object.DestroyImmediate(component);
                }
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(art);
            foreach (Transform child in art.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
            foreach (SkinnedMeshRenderer skin in art.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin.sharedMesh == null) Object.DestroyImmediate(skin.gameObject);
                else skin.updateWhenOffscreen = false;
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
#endif
