using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AreArpg.EditorTools
{
    /// <summary>Build optional private dungeon meshes and a cosmetic melee controller without vendor behaviours.</summary>
    public static class EntryCombatArtBuilder
    {
        private const string Catalog = "Assets/PrivateArt/entry-combat-art.json";
        private const string Output = "Assets/Resources/ARE/Visuals/";
        [Serializable] private sealed class Inputs
        {
            public string floor, wall, pillar, arch, palette, melee, hero, idle, move, weapon;
            public string idleClip, moveClip, meleeClip;
            public Vector3 weaponPosition;
            public Vector3 weaponEuler;
            public Vector3 weaponModelEuler;
            public float weaponLength, weaponGripFromBase;
        }
        private static bool _enabled;
        private static AnimatorController _controller;

        /// <summary>Missing optional catalog retains the established art; malformed configured inputs fail closed.</summary>
        public static void Prepare()
        {
            _enabled = false;
            _controller = null;
            if (!File.Exists(Catalog)) return;
            var input = JsonUtility.FromJson<Inputs>(File.ReadAllText(Catalog));
            if (input == null) throw new InvalidOperationException("Missing combat art inputs");
            string[] paths = { input.floor, input.wall, input.pillar, input.arch, input.palette, input.melee, input.hero, input.idle, input.move, input.weapon };
            foreach (string path in paths) ValidatePath(path);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(input.palette);
            if (texture == null) throw new InvalidOperationException("Missing dungeon palette");
            var material = AssetDatabase.LoadAssetAtPath<Material>(Output + "DungeonPalette.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, Output + "DungeonPalette.mat");
            }
            material.mainTexture = texture;
            material.color = Color.white;
            material.SetFloat("_Smoothness", .15f);
            EditorUtility.SetDirty(material);
            string[] keys = { "Floor", "Wall", "Pillar", "Arch" };
            for (int i = 0; i < keys.Length; i++) MeshPrefab(paths[i], "Dungeon" + keys[i], material);
            BuildController(input);
            BuildWeapon(input);
            BuildHero(input);
            _enabled = true;
        }

        /// <summary>Return a prepared private key or the previous revision's visual.</summary>
        public static string Key(string part, string previous) { return _enabled ? "Dungeon" + part : previous; }

        /// <summary>Rotate modular wall fronts without changing the previous art profile.</summary>
        public static float WallYaw { get { return _enabled ? 90f : 0f; } }

        public static string HeroKey { get { return _enabled ? "EntryCombatHero" : "Hero"; } }
        public static bool HasHero { get { return _enabled; } }

        /// <summary>Bind only the hero's reviewed humanoid rig; animation never moves the authority host.</summary>
        public static void BindHero(GameObject hero)
        {
            if (!_enabled) return;
            var animator = hero.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                throw new InvalidOperationException("Melee projection requires a humanoid hero");
            animator.runtimeAnimatorController = _controller;
            animator.applyRootMotion = false;
        }

        /// <summary>Reject external paths before loading any locally licensed asset.</summary>
        private static void ValidatePath(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Contains("..") ||
                !(path.StartsWith("Assets/PrivateArt/", StringComparison.Ordinal) ||
                  path.StartsWith("Assets/PrivateVendor/", StringComparison.Ordinal) ||
                  path.StartsWith(Output, StringComparison.Ordinal)))
                throw new InvalidOperationException("Combat art input must be private");
        }

        /// <summary>Copy mesh data only; no source prefab is instantiated or allowed to execute.</summary>
        private static void MeshPrefab(string path, string key, Material material)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (source == null) throw new InvalidOperationException("Missing dungeon mesh: " + key);
            var root = new GameObject(key);
            try
            {
                var highest = new System.Collections.Generic.HashSet<Renderer>();
                var lower = new System.Collections.Generic.HashSet<Renderer>();
                foreach (var group in source.GetComponentsInChildren<LODGroup>(true))
                {
                    var lods = group.GetLODs();
                    for (int level = 0; level < lods.Length; level++)
                        foreach (var renderer in lods[level].renderers)
                            if (renderer != null) (level == 0 ? highest : lower).Add(renderer);
                }
                // A renderer may be shared between levels; retain anything explicitly used at LOD0.
                lower.ExceptWith(highest);
                var meshes = source.GetComponentsInChildren<MeshFilter>(true)
                    .Where(m => m.GetComponent<MeshRenderer>() != null && !lower.Contains(m.GetComponent<MeshRenderer>())).ToArray();
                if (meshes.Length == 0) throw new InvalidOperationException("Empty dungeon mesh: " + key);
                foreach (var mesh in meshes)
                {
                    var part = new GameObject(mesh.name);
                    part.transform.SetParent(root.transform, false);
                    var relative = source.transform.worldToLocalMatrix * mesh.transform.localToWorldMatrix;
                    part.transform.localPosition = relative.GetColumn(3);
                    part.transform.localRotation = relative.rotation;
                    part.transform.localScale = relative.lossyScale;
                    if (mesh.sharedMesh == null) throw new InvalidOperationException("Missing mesh data: " + key);
                    part.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                    part.AddComponent<MeshRenderer>().sharedMaterials = Enumerable.Repeat(material, mesh.sharedMesh.subMeshCount).ToArray();
                }
                if (PrefabUtility.SaveAsPrefabAsset(root, Output + key + ".prefab") == null)
                    throw new IOException("Could not save dungeon visual: " + key);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        /// <summary>Keep the existing movement parameter contract, with the selected rig's own clips.</summary>
        private static void BuildController(Inputs input)
        {
            // Validate every source before touching clips or a controller already used by the hero.
            var idleSource = SourceClip(input.idle, input.idleClip, "EntryIdle");
            var moveSource = SourceClip(input.move, input.moveClip, "EntryMove");
            var attackSource = SourceClip(input.melee, input.meleeClip, "EntryStrike");
            var idleClip = Clip(idleSource, "EntryIdle", true);
            var moveClip = Clip(moveSource, "EntryMove", true);
            var attackClip = Clip(attackSource, "EntryStrike", false);
            string path = Output + "EntryMelee.controller";
            _controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (_controller == null) _controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = _controller.layers[0].stateMachine;
            foreach (var transition in machine.anyStateTransitions) machine.RemoveAnyStateTransition(transition);
            foreach (var state in machine.states) machine.RemoveState(state.state);
            _controller.parameters = new AnimatorControllerParameter[0];
            _controller.AddParameter("MoveMagnitude", AnimatorControllerParameterType.Float);
            _controller.AddParameter("Melee", AnimatorControllerParameterType.Trigger);
            var idle = machine.AddState("Idle"); idle.motion = idleClip;
            var move = machine.AddState("Move"); move.motion = moveClip;
            var attack = machine.AddState("Melee"); attack.motion = attackClip;
            machine.defaultState = idle;
            var walk = idle.AddTransition(move); walk.AddCondition(AnimatorConditionMode.Greater, .1f, "MoveMagnitude");
            var stop = move.AddTransition(idle); stop.AddCondition(AnimatorConditionMode.Less, .1f, "MoveMagnitude");
            foreach (var transition in new[] { walk, stop })
            { transition.hasExitTime = false; transition.hasFixedDuration = true; transition.duration = .12f; }
            var enter = machine.AddAnyStateTransition(attack);
            enter.AddCondition(AnimatorConditionMode.If, 0, "Melee");
            enter.hasExitTime = false; enter.duration = .04f; enter.hasFixedDuration = true;
            enter.canTransitionToSelf = true;
            var leave = attack.AddTransition(idle);
            leave.hasExitTime = true; leave.exitTime = .9f; leave.duration = .08f; leave.hasFixedDuration = true;
            EditorUtility.SetDirty(_controller);
        }

        /// <summary>Resolve exactly the reviewed humanoid take without writing any generated asset.</summary>
        private static AnimationClip SourceClip(string sourcePath, string sourceName, string key)
        {
            if (string.IsNullOrEmpty(sourceName)) throw new InvalidOperationException("Missing reviewed clip name: " + key);
            var clips = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>()
                .Where(c => string.Equals(c.name, sourceName, StringComparison.Ordinal)).ToArray();
            if (clips.Length != 1 || !clips[0].humanMotion)
                throw new InvalidOperationException("Expected exactly one humanoid clip: " + key);
            return clips[0];
        }

        /// <summary>Preserve private clip identities and strip events that could execute vendor gameplay.</summary>
        private static AnimationClip Clip(AnimationClip source, string key, bool loop)
        {
            string path = Output + key + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
            EditorUtility.CopySerialized(source, clip);
            clip.name = key;
            AnimationUtility.SetAnimationEvents(clip, new AnimationEvent[0]);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        /// <summary>Preserve the rigid weapon's proportions and put its reviewed grip at the hand origin.</summary>
        private static void BuildWeapon(Inputs input)
        {
            if (!(input.weaponLength > 0f) || float.IsInfinity(input.weaponLength) ||
                !(input.weaponGripFromBase >= 0f && input.weaponGripFromBase < input.weaponLength))
                throw new InvalidOperationException("Invalid reviewed weapon dimensions");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(input.weapon);
            if (source == null) throw new InvalidOperationException("Missing reviewed weapon");
            foreach (var component in source.GetComponentsInChildren<Component>(true))
                if (!(component is Transform || component is MeshFilter || component is MeshRenderer))
                    throw new InvalidOperationException("Weapon must be a passive rigid mesh");
            var root = new GameObject("EntryWeapon");
            try
            {
                var art = UnityEngine.Object.Instantiate(source, root.transform, false);
                art.transform.localPosition = Vector3.zero;
                art.transform.localRotation = Quaternion.Euler(input.weaponModelEuler);
                var renderers = art.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) throw new InvalidOperationException("Weapon has no visible mesh");
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                if (bounds.size.y <= 0f) throw new InvalidOperationException("Weapon needs a reviewed vertical blade axis");
                art.transform.localScale *= input.weaponLength / bounds.size.y;
                bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                art.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y - input.weaponGripFromBase, -bounds.center.z);
                if (PrefabUtility.SaveAsPrefabAsset(root, Output + "EntryWeapon.prefab") == null)
                    throw new IOException("Could not save reviewed weapon");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        /// <summary>Keep the selected passive rig and original texture slots; do not replace them with the old palette.</summary>
        private static void BuildHero(Inputs input)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(input.hero);
            if (source == null) throw new InvalidOperationException("Missing configured hero");
            foreach (var component in source.GetComponentsInChildren<Component>(true))
                if (!(component is Transform || component is SkinnedMeshRenderer || component is MeshRenderer ||
                      component is MeshFilter || component is Animator || component is LODGroup))
                    throw new InvalidOperationException("Hero source contains a non-visual component");
            var root = UnityEngine.Object.Instantiate(source);
            try
            {
                root.name = "EntryCombatHero";
                var animators = root.GetComponentsInChildren<Animator>(true);
                if (animators.Length != 1 || animators[0].avatar == null || !animators[0].avatar.isHuman || !animators[0].avatar.isValid)
                    throw new InvalidOperationException("Expected one valid humanoid hero rig");
                var animator = animators[0];
                animator.runtimeAnimatorController = _controller;
                animator.applyRootMotion = false;
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(HeroMaterial).ToArray();
                var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (hand == null) throw new InvalidOperationException("Hero has no right-hand weapon attachment");
                var socket = new GameObject("EntryWeaponSocket").transform;
                socket.SetParent(hand, false); socket.localPosition = input.weaponPosition;
                socket.localRotation = Quaternion.Euler(input.weaponEuler);
                if (PrefabUtility.SaveAsPrefabAsset(root, Output + "EntryCombatHero.prefab") == null)
                    throw new IOException("Could not save configured hero");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        /// <summary>Persist URP copies of the source's base, normal, metal and occlusion maps without altering originals.</summary>
        private static Material HeroMaterial(Material source)
        {
            if (source == null || source.mainTexture == null) throw new InvalidOperationException("Hero material requires its real base texture");
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long localId) || string.IsNullOrEmpty(guid))
                throw new InvalidOperationException("Hero material has no stable source identity");
            string path = Output + "EntryHeroMaterial_" + guid + "_" + localId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Missing pinned URP Lit shader");
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader; material.shaderKeywords = new string[0];
            material.mainTexture = source.mainTexture; material.mainTextureScale = source.mainTextureScale;
            material.mainTextureOffset = source.mainTextureOffset; material.color = source.color;
            foreach (string property in new[] { "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" })
                material.SetTexture(property, source.HasProperty(property) ? source.GetTexture(property) : null);
            if (material.GetTexture("_BumpMap") != null) material.EnableKeyword("_NORMALMAP");
            if (material.GetTexture("_MetallicGlossMap") != null) material.EnableKeyword("_METALLICSPECGLOSSMAP");
            if (material.GetTexture("_OcclusionMap") != null) material.EnableKeyword("_OCCLUSIONMAP");
            material.SetFloat("_Smoothness", source.HasProperty("_GlossMapScale") ? source.GetFloat("_GlossMapScale") : .3f);
            material.SetFloat("_Metallic", source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : 0f);
            bool cutout = source.HasProperty("_Mode") && source.GetFloat("_Mode") == 1f;
            if (source.HasProperty("_Mode") && source.GetFloat("_Mode") > 1f)
                throw new InvalidOperationException("Hero transparency requires an explicit reviewed conversion");
            material.SetFloat("_AlphaClip", cutout ? 1f : 0f);
            if (cutout) material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Cutoff", source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : .5f);
            material.renderQueue = cutout ? 2450 : 2000;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
