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
            foreach (string id in ids) AssetDatabase.DeleteAsset(StagingFolder + "/" + id);
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
                if (recipe.parts) RecordParts(recipe.id, system);
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
                try { PublishBake(id); published.Add(id); }
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
                    Avatar avatar = GameAvatar(recipe);
                    foreach (string key in recipe.keys ?? new string[0]) Save(recipe, mesh, parts, material, avatar, Visuals + key + ".prefab", key, true);
                    foreach (string key in recipe.sources ?? new string[0]) Save(recipe, mesh, parts, material, avatar, SourceFolder + "/" + key + ".prefab", key, false);
                    // Meshes the figure no longer uses go only now, after every visual of the figure points at what it
                    // uses: until then the old visual is whole. A colour variant keeps none of its own; a figure in
                    // two parts keeps no mesh in one piece, a figure in one piece no parts.
                    bool variant = !string.IsNullOrEmpty(recipe.sameShapeAs);
                    var unused = new List<string>();
                    if (variant || recipe.parts) unused.Add(GameMeshPath(recipe.id));
                    if (variant || !recipe.parts) { unused.Add(PartPath(recipe.id, FigureComposer.BodyPart)); unused.Add(PartPath(recipe.id, FigureComposer.HeadPart)); }
                    foreach (string path in unused) if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
                    AssetDatabase.SaveAssets();
                    Report(id + "\tVISUALS\t" + string.Join(",", (recipe.keys ?? new string[0]).Concat(recipe.sources ?? new string[0])));
                }
                catch (Exception e)
                {
                    if (snapshot != null) snapshot.Restore();
                    if (fromBake)
                    {
                        string backup = Path.Combine("Library/BozoCharacters/Backup", id);
                        string target = BakeFolder + "/" + id;
                        if (Directory.Exists(target)) Directory.Delete(target, true);
                        if (File.Exists(backup + "/.existing")) CopyTree(backup + "/assets", target);
                        else if (File.Exists(target + ".meta")) File.Delete(target + ".meta");
                        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    }
                    Debug.LogException(e);
                    Report(id + "\tVISUALS_FAILED\t" + e.Message.Replace('\n', ' '));
                }
            }
            AssetDatabase.SaveAssets();
            Report("DONE");
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

        /// <summary>Store a game mesh, keeping the identity of an existing asset, without a second copy for scripts.</summary>
        static Mesh SaveMesh(Mesh mesh, string path)
        {
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
            else { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); } // keep the asset identity
            var serialized = new SerializedObject(saved);
            serialized.FindProperty("m_IsReadable").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(saved);
            return saved;
        }

        static string GameMeshPath(string id) { return BakeFolder + "/" + id + "/" + id + "_GameMesh.asset"; }
        static string PartPath(string id, string part) { return BakeFolder + "/" + id + "/" + id + "_Game" + part + ".asset"; }
        static string PartsRecordPath(string id) { return Path.Combine(Directory.GetCurrentDirectory(), "Library/BozoCharacters/Parts/" + id + ".txt"); }

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
            File.WriteAllLines(PartsRecordPath(id), lines);
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
            mesh.RecalculateBounds();
            return mesh;
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
                if (Outdated(AssetDatabase.GetAssetPath(packed), normalPath, "normal"))
                    WriteNormalMap(AssetDatabase.GetAssetPath(packed), normalPath, recipe.texture);
                material.SetTexture("_Normal", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
            }
            Texture colours = baked.HasProperty("_Diffuse") ? baked.GetTexture("_Diffuse") : null;
            float light = BozoFigureRecipes.LoadLight();
            if (colours != null && light < 1f)
            {
                string colourPath = folder + recipe.id + "_GameColours.png";
                string stamp = "light=" + light.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                if (Outdated(AssetDatabase.GetAssetPath(colours), colourPath, stamp))
                    WriteLiftedColours(AssetDatabase.GetAssetPath(colours), colourPath, recipe.texture, light, stamp);
                material.SetTexture("_Diffuse", AssetDatabase.LoadAssetAtPath<Texture2D>(colourPath));
            }
            EditorUtility.SetDirty(material);
            return material;
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
        static void WriteLiftedColours(string sourcePath, string targetPath, int size, float light, string stamp)
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

        static void WriteNormalMap(string packedPath, string normalPath, int size)
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
                texture.SetPixels32(pixels);
                File.WriteAllBytes(normalPath, texture.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(normalPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(normalPath);
            importer.textureType = TextureImporterType.NormalMap;
            importer.maxTextureSize = size;
            importer.mipmapEnabled = true;
            importer.userData = "normal";
            importer.SaveAndReimport();
        }

        /// <summary>
        /// The body sliders of the pack scale bones (shoulders, chest, limbs). The pack's avatar describes the
        /// unscaled skeleton, so everything Unity solves on the avatar lands beside the real bones: the hero's sword
        /// hand missed its grip by four centimetres. Each reshaped figure gets its own avatar: the pack's description
        /// with the bone scales of the baked figure.
        /// </summary>
        static Avatar GameAvatar(BozoFigureRecipes.Figure recipe)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(BakedPath(recipe.id));
            if (source == null) throw new InvalidOperationException("Gebackene Figur fehlt: " + recipe.id);
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
