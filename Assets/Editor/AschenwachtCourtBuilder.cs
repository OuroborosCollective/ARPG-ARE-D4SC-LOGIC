using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace AreArpg.EditorTools
{
    /// <summary>
    /// Builds Aschenwacht (owner decision 2026-11-25: the desecrated forecourt, the connector and the bell court between the
    /// old crypt and the village) as one passive prefab from the owner's two private sets. Every placement of the source
    /// scenes is kept: each building, grave, stone, grass tuft and the Aschbeter stand where the sets put them, in the frame
    /// of CourtLayout (the set turned half a turn so that the way leads north). The models are the reduced FBX of
    /// Tools/Art/aschenwacht/convert.py; materials come from the sets' own PBR maps. The prefab stays passive (no light, no collider, no script: the art
    /// binding rejects them); the runtime lights its fire baskets. Nothing here is part of the repository.
    /// </summary>
    public static class AschenwachtCourtBuilder
    {
        public const string Placements = "Assets/PrivateArt/Aschenwacht/station-placements.tsv";
        public const string CourtModels = "Assets/PrivateArt/Aschenwacht/Game/";
        public const string CourtMaterials = "Assets/Aschenwacht/Materials/";
        public const string ForecourtModels = "Assets/PrivateArt/Aschenwacht_II_Game/Models/";
        public const string ForecourtTextures = "Assets/PrivateArt/Aschenwacht_II/Texturen/";
        public const string ForecourtMasks = "Assets/PrivateArt/Aschenwacht_II_Game/Masks/";
        public const string ForecourtMaterials = "Assets/PrivateArt/Aschenwacht_II_Game/Materials/";
        public const string AschbeterController = "Assets/PrivateArt/Aschenwacht_II_Game/Aschbeter.controller";
        public const string Output = "Assets/Resources/ARE/Visuals/EntryCourtWorld.prefab";
        public const string FigureGroup = "Figuren";
        private const int MaximumTexture = 1024;
        private static readonly string[] DoubleSided = { "Char", "Grass", "Leaf", "Linen" };
        private static readonly string[] NoShadows = { "Grass", "Leaf", "Wax", "Ember", "Ash" };
        private static readonly Dictionary<string, float> Glow = new Dictionary<string, float> { { "Ember", 2.2f }, { "Garnet", .55f } };
        private static readonly string[] LoopingTakes = { "Idle", "Shuffle", "TurnLeft", "TurnRight", "DeathStay" };

        private sealed class Placement
        {
            public string Group, Set, Model;
            public Vector3 Position, Scale;
            public Quaternion Rotation;
        }

        /// <summary>True where the private sets and the placement table are installed.</summary>
        public static bool Installed => File.Exists(Placements) && AssetDatabase.IsValidFolder(ForecourtModels.TrimEnd('/')) &&
            AssetDatabase.IsValidFolder(CourtModels.TrimEnd('/')) && AssetDatabase.IsValidFolder(ForecourtTextures.TrimEnd('/'));

        /// <summary>Build when the private sets are installed; otherwise leave the game's fallback in place.</summary>
        public static bool TryBuild()
        {
            if (!Installed) { Debug.LogWarning("ARE ARPG: Aschenwacht sets not installed; the court keeps its fallback."); return false; }
            Build();
            return true;
        }

        [MenuItem("ARE ARPG/Art/Build Aschenwacht")]
        public static void Build()
        {
            if (!Installed) throw new InvalidOperationException("Aschenwacht sets or placements missing");
            List<Placement> placements = Read(Placements);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Dictionary<string, Material> forecourt = ForecourtMaterialSet();
            Dictionary<string, Material> court = AssetDatabase.FindAssets("t:Material", new[] { CourtMaterials.TrimEnd('/') })
                .Select(guid => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid))).ToDictionary(m => m.name, StringComparer.Ordinal);
            PrepareModels(ForecourtModels, forecourt);
            PrepareModels(CourtModels, court);
            RuntimeAnimatorController idle = AschbeterIdle();

            var root = new GameObject("EntryCourtWorld");
            var groups = new Dictionary<string, Transform>(StringComparer.Ordinal);
            int count = 0;
            try
            {
                foreach (Placement p in placements)
                {
                    string path = (p.Set == "I" ? CourtModels : ForecourtModels) + p.Model + ".fbx";
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (model == null) throw new InvalidOperationException("Aschenwacht model missing: " + path);
                    if (!groups.TryGetValue(p.Group, out Transform group))
                    {
                        group = new GameObject(p.Group).transform;
                        group.SetParent(root.transform, false);
                        groups.Add(p.Group, group);
                    }
                    var holder = new GameObject(p.Model).transform;
                    holder.SetParent(group, false);
                    holder.localPosition = p.Position;
                    holder.localRotation = p.Rotation;
                    holder.localScale = p.Scale;
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, holder);
                    if (p.Group == FigureGroup && p.Model == "Aschbeter")
                    {
                        Animator animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
                        animator.runtimeAnimatorController = idle;
                        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                        animator.applyRootMotion = false;
                    }
                    else
                    {
                        // Scenery and the bell court's two still figures stand as they were placed.
                        foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(animator);
                    }
                    count++;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(Output));
                PrefabUtility.SaveAsPrefabAsset(root, Output);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("ARE_ASCHENWACHT_BUILT placements=" + count + " groups=" + groups.Count);
        }

        private static List<Placement> Read(string path)
        {
            var list = new List<Placement>();
            string[] lines = File.ReadAllLines(path);
            if (lines.Length < 2 || !lines[0].StartsWith("group\tset\tmodel", StringComparison.Ordinal)) throw new InvalidOperationException("Aschenwacht placement table corrupt");
            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].Length == 0) continue;
                string[] f = lines[i].Split('\t');
                if (f.Length != 13 || (f[1] != "I" && f[1] != "II") || f[2].IndexOfAny(new[] { '/', '\\', '.' }) >= 0)
                    throw new InvalidOperationException("Aschenwacht placement row corrupt: " + (i + 1));
                float V(int k) => float.Parse(f[k], NumberStyles.Float, CultureInfo.InvariantCulture);
                var rotation = new Quaternion(V(6), V(7), V(8), V(9));
                if (Mathf.Abs(rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w - 1f) > 1e-3f)
                    throw new InvalidOperationException("Aschenwacht placement rotation not normalised: " + (i + 1));
                list.Add(new Placement { Group = f[0], Set = f[1], Model = f[2], Position = new Vector3(V(3), V(4), V(5)), Rotation = rotation, Scale = new Vector3(V(10), V(11), V(12)) });
            }
            return list;
        }

        /// <summary>One URP/Lit material per surface of the forecourt set, from its colour, normal, mask and emission maps.</summary>
        private static Dictionary<string, Material> ForecourtMaterialSet()
        {
            Directory.CreateDirectory(ForecourtMaterials);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) throw new InvalidOperationException("URP/Lit missing");
            var set = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (string folder in AssetDatabase.GetSubFolders(ForecourtTextures.TrimEnd('/')))
            {
                string surface = Path.GetFileName(folder);
                Texture2D colour = Texture(folder + "/BaseColor.png", TextureImporterType.Default, true);
                Texture2D normal = Texture(folder + "/Normal.png", TextureImporterType.NormalMap, false);
                Texture2D mask = Texture(ForecourtMasks + surface + "_Maske.png", TextureImporterType.Default, false);
                if (colour == null || normal == null || mask == null) throw new InvalidOperationException("Aschenwacht surface incomplete: " + surface);
                string path = ForecourtMaterials + surface + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(lit); AssetDatabase.CreateAsset(material, path); }
                material.shader = lit;
                material.SetTexture("_BaseMap", colour);
                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 1f);
                material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_MetallicGlossMap", mask);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.SetFloat("_Smoothness", 1f);
                material.SetFloat("_SmoothnessTextureChannel", 0f);
                material.SetTexture("_OcclusionMap", mask);
                material.SetFloat("_OcclusionStrength", 1f);
                material.EnableKeyword("_OCCLUSIONMAP");
                bool twoSided = DoubleSided.Contains(surface);
                material.SetFloat("_Cull", twoSided ? 0f : 2f);
                material.doubleSidedGI = twoSided;
                if (Glow.TryGetValue(surface, out float strength))
                {
                    Texture2D emission = Texture(folder + "/Emissive.png", TextureImporterType.Default, true);
                    material.SetTexture("_EmissionMap", emission);
                    material.SetColor("_EmissionColor", Color.white * strength);
                    material.EnableKeyword("_EMISSION");
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                else
                {
                    material.DisableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", Color.black);
                }
                material.SetShaderPassEnabled("ShadowCaster", !NoShadows.Contains(surface));
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                set.Add(surface, material);
            }
            AssetDatabase.SaveAssets();
            return set;
        }

        private static Texture2D Texture(string path, TextureImporterType type, bool colour)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            if (importer.textureType != type || importer.sRGBTexture != colour || importer.maxTextureSize != MaximumTexture || !importer.mipmapEnabled ||
                importer.isReadable || !android.overridden || android.maxTextureSize != MaximumTexture || android.format != TextureImporterFormat.ASTC_6x6)
            {
                importer.textureType = type;
                importer.sRGBTexture = colour;
                importer.maxTextureSize = MaximumTexture;
                importer.mipmapEnabled = true;
                importer.isReadable = false;
                importer.anisoLevel = 2;
                android.overridden = true; android.maxTextureSize = MaximumTexture; android.format = TextureImporterFormat.ASTC_6x6;
                importer.SetPlatformTextureSettings(android);
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Models arrive without animation (the takes keep theirs) and wear the set's materials by name.</summary>
        private static void PrepareModels(string folder, Dictionary<string, Material> materials)
        {
            string avatarModel = folder + "Aschbeter.fbx";
            // A take copies its avatar from the model, so every model is prepared before any take.
            var paths = AssetDatabase.FindAssets("t:Model", new[] { folder.TrimEnd('/') }).Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => Path.GetFileName(path).Contains("@") ? 1 : 0).ThenBy(path => path, StringComparer.Ordinal).ToList();
            foreach (string path in paths)
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                bool take = Path.GetFileName(path).Contains("@");
                bool figure = Path.GetFileName(path).StartsWith("Aschbeter", StringComparison.Ordinal);
                bool changed = false;
                void Set<T>(T now, T want, Action<T> apply) { if (!EqualityComparer<T>.Default.Equals(now, want)) { apply(want); changed = true; } }
                Set(importer.bakeAxisConversion, true, v => importer.bakeAxisConversion = v);
                Set(importer.isReadable, false, v => importer.isReadable = v);
                Set(importer.importCameras, false, v => importer.importCameras = v);
                Set(importer.importLights, false, v => importer.importLights = v);
                Set(importer.addCollider, false, v => importer.addCollider = v);
                Set(importer.importAnimation, take, v => importer.importAnimation = v);
                if (figure)
                {
                    Set(importer.animationType, ModelImporterAnimationType.Generic, v => importer.animationType = v);
                    if (take)
                    {
                        var source = AssetDatabase.LoadAssetAtPath<Avatar>(avatarModel);
                        if (source == null) throw new InvalidOperationException("Aschbeter avatar missing: import the model before its takes");
                        Set(importer.avatarSetup, ModelImporterAvatarSetup.CopyFromOther, v => importer.avatarSetup = v);
                        if (importer.sourceAvatar != source) { importer.sourceAvatar = source; changed = true; }
                        string clip = Path.GetFileNameWithoutExtension(path).Split('@')[1];
                        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
                        if (clips.Length > 0)
                        {
                            bool loop = LoopingTakes.Contains(clip);
                            foreach (var c in clips) { c.name = clip; c.loopTime = loop; }
                            if (importer.clipAnimations.Length != clips.Length || importer.clipAnimations.Any(c => c.name != clip || c.loopTime != loop))
                            { importer.clipAnimations = clips; changed = true; }
                        }
                    }
                    else Set(importer.avatarSetup, ModelImporterAvatarSetup.CreateFromThisModel, v => importer.avatarSetup = v);
                }
                if (!take)
                {
                    Set(importer.materialImportMode, ModelImporterMaterialImportMode.ImportViaMaterialDescription, v => importer.materialImportMode = v);
                    Dictionary<AssetImporter.SourceAssetIdentifier, UnityEngine.Object> map = importer.GetExternalObjectMap();
                    foreach (var name in SourceMaterialNames(path))
                    {
                        if (!materials.TryGetValue(name, out Material material)) throw new InvalidOperationException("No material '" + name + "' for " + path);
                        var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), name);
                        if (!map.TryGetValue(id, out UnityEngine.Object current) || current != material) { importer.AddRemap(id, material); changed = true; }
                    }
                }
                if (changed) importer.SaveAndReimport();
            }
        }

        /// <summary>The material names an imported model asks for (its default sub-assets or existing remaps).</summary>
        private static IEnumerable<string> SourceMaterialNames(string path)
        {
            var names = new SortedSet<string>(StringComparer.Ordinal);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                foreach (Material material in renderer.sharedMaterials)
                    if (material != null) names.Add(material.name.Split('.')[0].Replace(" (Instance)", ""));
            return names;
        }

        /// <summary>The Aschbeter stands in his covered rest pose: one looping Idle state.</summary>
        private static RuntimeAnimatorController AschbeterIdle()
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ForecourtModels + "Aschbeter@Idle.fbx").OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview", StringComparison.Ordinal));
            if (clip == null) throw new InvalidOperationException("Aschbeter idle clip missing");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AschbeterController);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(AschbeterController);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState idle = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Idle") ?? machine.AddState("Idle");
            idle.motion = clip;
            machine.defaultState = idle;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }
    }
}
