using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AreArpg.EditorTools
{
    /// <summary>
    /// The crypt's enemy figures from the monster packs the owner bought on 2026-11-24: the ghoul (Core kind 0), the
    /// goblin with sword and shield as the knight (kind 1) and the horned demon as the heavy crypt boss (kind 2).
    /// Each figure gets a private, passive prefab EntryEnemy&lt;kind&gt; with its own controller: Idle, Move, Attack, Hit and
    /// Death. The colour textures are baked muted (dark, low saturation), because the packs are far brighter than the
    /// game. Nothing of the packs is part of the repository; without a pack its kind keeps the earlier figure.
    /// </summary>
    public static class CryptEnemyArtBuilder
    {
        private const string Output = "Assets/Resources/ARE/Visuals/";
        private const string Ghoul = "Assets/SimpleAssets/Necromancers/Ghoul/";
        private const string Goblin = "Assets/3D Stylized Goblin(animations+)/";
        private const string Demon = "Assets/Scary_Evil/Prefab/";
        private const string StarterPack = Output + "Vendor/AnimationsStarterPack/";
        /// <summary>The demon ships with 224,729 triangles; this is the copy reduced in Blender (see the evidence file).</summary>
        private const string DemonMesh = "Assets/PrivateArt/EnemyPacks/DemonLow.fbx";
        private const string MixamoTake = "mixamo.com";
        private const int TextureSize = 1024;

        private sealed class Take
        {
            public readonly string Asset, Name; public readonly bool Human;
            public Take(string asset, string name, bool human = false) { Asset = asset; Name = name; Human = human; }
        }

        /// <summary>One material of a figure: which renderers wear it and how its colour texture is muted.</summary>
        private sealed class Skin
        {
            public string Key, Renderers, Colour, Normal;
            public float Saturation = 1f, Gain = 1f; public Color Tint = Color.white;
        }

        private sealed class Figure
        {
            public int Kind; public string Label, Model; public bool Human;
            /// <summary>Height of the bind pose in metres; a hunched figure stands lower than this.</summary>
            public float Height;
            public Take Idle, Move, Attack, Hit, Death;
            /// <summary>Where in the attack clip the blow lands (measured on the weapon hand's speed peak and reviewed frames).</summary>
            public float BlowSeconds;
            public float MoveSpeed = 1f;
            public Skin[] Skins;
        }

        private static readonly Figure[] Figures =
        {
            new Figure
            {
                Kind = 0, Label = "Ghoul", Model = Ghoul + "FBX/Ghoul.fbx", Height = 2.25f, BlowSeconds = .47f,
                Idle = new Take(Ghoul + "Animation/Idle.fbx", "Idle"), Move = new Take(Ghoul + "Animation/Run.fbx", "Run"),
                Attack = new Take(Ghoul + "Animation/Attack.fbx", "Attack"), Hit = new Take(Ghoul + "Animation/Hit.fbx", "Hit"),
                Death = new Take(Ghoul + "Animation/Death2.fbx", "Death"),
                Skins = new[] { new Skin { Key = "", Renderers = "", Colour = Ghoul + "Texture/BaseBlue.tga",
                    Normal = Ghoul + "Texture/Body_low_Default OBJ_Normal.png", Saturation = .26f, Tint = new Color(.88f, .90f, .86f), Gain = .82f } },
            },
            new Figure
            {
                // The clip files carry the mesh on a far smaller skeleton (218 nodes) than Goblin.fbx (996 with its control rig).
                Kind = 1, Label = "Goblin", Model = Goblin + "controlers/Idle2Idle2.fbx", Height = 1.7f, BlowSeconds = .42f,
                Idle = new Take(Goblin + "controlers/Idle2Idle2.fbx", "Idle2"), Move = new Take(Goblin + "controlers/runrun.fbx", "run"),
                Attack = new Take(Goblin + "controlers/Attack1Attack1.fbx", "Attack1"), Hit = new Take(Goblin + "controlers/damagedamage.fbx", "damage"),
                Death = new Take(Goblin + "controlers/deathdeath.fbx", "death"),
                Skins = new[]
                {
                    new Skin { Key = "_Body", Renderers = "body", Colour = Goblin + "Textures/body/body_baseColor.png",
                        Normal = Goblin + "Textures/body/low2_body_Normal.png", Saturation = .34f, Tint = new Color(.80f, .83f, .74f), Gain = .66f },
                    new Skin { Key = "_Gear", Renderers = "", Colour = Goblin + "Textures/cloth/cloth_Albedo.png",
                        Normal = Goblin + "Textures/cloth/low2_cloth_Normal.png", Saturation = .40f, Tint = new Color(.85f, .82f, .78f), Gain = .62f },
                },
            },
            new Figure
            {
                // The pack has neither a flinch nor a death: both come from the licensed humanoid takes the hero already uses.
                Kind = 2, Label = "Demon", Model = DemonMesh, Human = true, Height = 2.6f, BlowSeconds = 1f, MoveSpeed = 1.2f,
                Idle = new Take(Demon + "evil@Idle (1).fbx", MixamoTake, true), Move = new Take(Demon + "evil@Walking.fbx", MixamoTake, true),
                Attack = new Take(Demon + "evil@Standing Melee Attack Backhand.fbx", MixamoTake, true),
                Hit = new Take(StarterPack + "Combat/GetHit.fbx", "GetHit", true), Death = new Take(StarterPack + "Combat/Death.fbx", "Death", true),
                Skins = new[] { new Skin { Key = "", Renderers = "", Colour = Demon + "a_huge_evil_with_an_h_0304192645_texture.png",
                    Saturation = .8f, Tint = new Color(1f, .97f, .93f), Gain = 2.1f } }, // charcoal in the pack: a black shape in the dark crypt
            },
        };

        private static string Key(Figure figure) { return "EntryEnemy" + figure.Kind; }

        /// <summary>Whether everything this kind's figure is built from is installed.</summary>
        public static bool Available(int kind)
        {
            Figure figure = Figures.FirstOrDefault(each => each.Kind == kind);
            if (figure == null) return false;
            var paths = new List<string> { figure.Model, figure.Idle.Asset, figure.Move.Asset, figure.Attack.Asset, figure.Hit.Asset, figure.Death.Asset };
            foreach (Skin skin in figure.Skins) { paths.Add(skin.Colour); if (skin.Normal != null) paths.Add(skin.Normal); }
            return paths.All(path => AssetDatabase.LoadMainAssetAtPath(path) != null);
        }

        /// <summary>Whether the entry enemy of that key is one of these figures (its controller is the one built here).</summary>
        public static bool Owns(string key)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Output + key + ".prefab");
            var animator = prefab != null ? prefab.GetComponentInChildren<Animator>(true) : null;
            return animator != null && animator.runtimeAnimatorController != null &&
                AssetDatabase.GetAssetPath(animator.runtimeAnimatorController) == Output + key + ".controller";
        }

        /// <summary>Build the figure of one kind; false (and nothing written) when its pack is not installed.</summary>
        public static bool TryBuild(int kind)
        {
            if (!Available(kind)) return false;
            Build(Figures.First(each => each.Kind == kind));
            return true;
        }

        [MenuItem("ARE ARPG/Art/Build Crypt Enemies")]
        public static void BuildAll()
        {
            var built = new List<string>();
            foreach (Figure figure in Figures)
                if (TryBuild(figure.Kind)) built.Add(figure.Kind + "=" + figure.Label);
            AssetDatabase.SaveAssets();
            Debug.Log("CRYPT_ENEMIES_BUILT " + (built.Count > 0 ? string.Join(",", built) : "none (packs not installed)"));
        }

        private static void Build(Figure figure)
        {
            // Every source is resolved before anything is written: a missing take leaves the previous figure whole.
            if (figure.Human) Humanoid(figure.Model, null, false);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(figure.Model);
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(figure.Model).OfType<Avatar>().FirstOrDefault();
            if (model == null || avatar == null || !avatar.isValid || avatar.isHuman != figure.Human)
                throw new InvalidOperationException(figure.Label + ": the model needs a valid " + (figure.Human ? "humanoid" : "generic") + " avatar: " + figure.Model);
            AnimationClip idle = Resolve(figure.Idle, true), move = Resolve(figure.Move, true), attack = Resolve(figure.Attack, false),
                hit = Resolve(figure.Hit, false), death = Resolve(figure.Death, false);
            if (figure.BlowSeconds <= 0f || figure.BlowSeconds >= attack.length)
                throw new InvalidOperationException(figure.Label + ": the blow must land inside the attack clip");

            string key = Key(figure);
            var materials = new Dictionary<Skin, Material>();
            foreach (Skin skin in figure.Skins) materials[skin] = SkinMaterial(key + skin.Key, skin);
            AnimatorController controller = Controller(figure, key,
                EntryCombatArtBuilder.Clip(idle, key + "Idle", true), EntryCombatArtBuilder.Clip(move, key + "Move", true),
                EntryCombatArtBuilder.Clip(attack, key + "Attack", false), EntryCombatArtBuilder.Clip(hit, key + "Hit", false),
                EntryCombatArtBuilder.Clip(death, key + "Death", false));

            float height = 0f; int triangles = 0;
            EntryWorldArtBuilder.Save(key, root =>
            {
                GameObject art = UnityEngine.Object.Instantiate(model, root, false);
                art.name = figure.Label;
                art.transform.localPosition = Vector3.zero; art.transform.localRotation = Quaternion.identity;
                foreach (Renderer renderer in art.GetComponentsInChildren<Renderer>(true))
                {
                    string worn = renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "";
                    Skin skin = figure.Skins.First(each => each.Renderers.Length == 0 ||
                        worn.StartsWith(each.Renderers, StringComparison.OrdinalIgnoreCase) || renderer.name.StartsWith(each.Renderers, StringComparison.OrdinalIgnoreCase));
                    renderer.sharedMaterials = Enumerable.Repeat(materials[skin], renderer.sharedMaterials.Length).ToArray();
                    if (renderer is SkinnedMeshRenderer skinned)
                    {
                        skinned.updateWhenOffscreen = false;
                        if (skinned.sharedMesh != null) triangles += skinned.sharedMesh.triangles.Length / 3;
                    }
                }
                Bounds bounds = Extent(art);
                art.transform.localScale *= figure.Height / bounds.size.y;
                bounds = Extent(art);
                art.transform.localPosition = new Vector3(0f, -bounds.min.y, 0f); // feet on the ground of the bind pose
                height = bounds.size.y;
                // A fallen or lunging body leaves the bind pose's box: widen it so the figure is not culled while visible.
                foreach (SkinnedMeshRenderer skinned in art.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    Bounds local = skinned.localBounds; local.Expand(local.size * .8f); skinned.localBounds = local;
                }
                Animator animator = art.GetComponent<Animator>();
                if (animator == null) animator = art.AddComponent<Animator>();
                animator.avatar = avatar; animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            });
            Debug.Log("CRYPT_ENEMY " + key + " " + figure.Label + " height=" + height.ToString("0.00") + " triangles=" + triangles +
                " clips=" + string.Join(",", new[] { idle, move, attack, hit, death }.Select(clip => clip.name + ":" + clip.length.ToString("0.00"))) +
                " blow=" + figure.BlowSeconds.ToString("0.00"));
        }

        private static Bounds Extent(GameObject art)
        {
            Renderer[] renderers = art.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("The model has nothing to draw: " + art.name);
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        /// <summary>Exactly one moving take of that name; humanoid takes are imported as such first.</summary>
        private static AnimationClip Resolve(Take take, bool loop)
        {
            if (take.Human && take.Name == MixamoTake) Humanoid(take.Asset, take.Name, loop);
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(take.Asset).OfType<AnimationClip>()
                .Where(clip => string.Equals(clip.name, take.Name, StringComparison.Ordinal)).ToArray();
            if (clips.Length != 1) throw new InvalidOperationException("Expected exactly one take '" + take.Name + "' in " + take.Asset);
            AnimationClip found = clips[0];
            if (found.legacy || found.humanMotion != take.Human || found.frameRate <= 0 || found.length * found.frameRate <= 1.5f)
                throw new InvalidOperationException(take.Name + " in " + take.Asset + " must be a moving " + (take.Human ? "humanoid" : "generic") + " take");
            return found;
        }

        /// <summary>
        /// Import a Mixamo-rigged model as humanoid. With a take name its one clip is that take: the turn and the height
        /// stay in the pose, the travel over the ground is left to the Core. Reimports only when a setting differs.
        /// </summary>
        private static void Humanoid(string asset, string take, bool loop)
        {
            var importer = AssetImporter.GetAtPath(asset) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Not a model: " + asset);
            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.Human || importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                changed = true;
            }
            if (take == null)
            {
                if (importer.importAnimation) { importer.importAnimation = false; changed = true; }
            }
            else
            {
                TakeInfo[] takes = importer.importedTakeInfos.Where(info => info.name == take).ToArray();
                if (takes.Length != 1) throw new InvalidOperationException("Expected exactly one take '" + take + "' in " + asset);
                float first = takes[0].startTime * takes[0].sampleRate, last = takes[0].stopTime * takes[0].sampleRate;
                ModelImporterClipAnimation[] current = importer.clipAnimations;
                bool same = current != null && current.Length == 1 && current[0].takeName == take && current[0].name == take &&
                    Mathf.Approximately(current[0].firstFrame, first) && Mathf.Approximately(current[0].lastFrame, last) &&
                    current[0].loopTime == loop && current[0].lockRootRotation && current[0].keepOriginalOrientation &&
                    current[0].lockRootHeightY && current[0].keepOriginalPositionY && !current[0].lockRootPositionXZ;
                if (!same)
                {
                    importer.clipAnimations = new[]
                    {
                        new ModelImporterClipAnimation
                        {
                            name = take, takeName = take, firstFrame = first, lastFrame = last, loopTime = loop,
                            lockRootRotation = true, keepOriginalOrientation = true, lockRootHeightY = true, keepOriginalPositionY = true,
                            lockRootPositionXZ = false,
                        },
                    };
                    changed = true;
                }
            }
            if (changed) importer.SaveAndReimport();
        }

        /// <summary>
        /// Idle and Move follow MoveMagnitude; the game cross-fades into Attack and Hit itself; Dead holds Death until the
        /// enemy stands again. The attack state's own speed makes the blow land one second after its start at the
        /// multiplier 1, so the game only has to divide by the Core's windup (see EnemyClipPresentation).
        /// </summary>
        private static AnimatorController Controller(Figure figure, string key, AnimationClip idleClip, AnimationClip moveClip,
            AnimationClip attackClip, AnimationClip hitClip, AnimationClip deathClip)
        {
            string path = Output + key + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach (AnimatorStateTransition transition in machine.anyStateTransitions) machine.RemoveAnyStateTransition(transition);
            foreach (ChildAnimatorState child in machine.states) machine.RemoveState(child.state);
            controller.parameters = new AnimatorControllerParameter[0];
            controller.AddParameter("MoveMagnitude", AnimatorControllerParameterType.Float);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            controller.AddParameter(new AnimatorControllerParameter { name = "AttackSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });

            AnimatorState idle = machine.AddState("Idle"); idle.motion = idleClip;
            AnimatorState move = machine.AddState("Move"); move.motion = moveClip; move.speed = figure.MoveSpeed;
            AnimatorState attack = machine.AddState("Attack"); attack.motion = attackClip;
            attack.speed = figure.BlowSeconds; attack.speedParameterActive = true; attack.speedParameter = "AttackSpeed";
            AnimatorState hit = machine.AddState("Hit"); hit.motion = hitClip;
            AnimatorState death = machine.AddState("Death"); death.motion = deathClip;
            machine.defaultState = idle;

            AnimatorStateTransition walk = idle.AddTransition(move); walk.AddCondition(AnimatorConditionMode.Greater, .1f, "MoveMagnitude");
            AnimatorStateTransition stop = move.AddTransition(idle); stop.AddCondition(AnimatorConditionMode.Less, .1f, "MoveMagnitude");
            foreach (AnimatorStateTransition transition in new[] { walk, stop })
            { transition.hasExitTime = false; transition.hasFixedDuration = true; transition.duration = .15f; }

            // After the blow and a short follow-through the enemy may walk on; standing, it plays the clip out.
            float free = Mathf.Min(.9f, (figure.BlowSeconds + .25f) / attackClip.length);
            Leave(attack, move, free, true); Leave(attack, idle, .92f, false);
            Leave(hit, move, .6f, true); Leave(hit, idle, .85f, false);

            AnimatorStateTransition fall = machine.AddAnyStateTransition(death);
            fall.AddCondition(AnimatorConditionMode.If, 0, "Dead");
            fall.hasExitTime = false; fall.hasFixedDuration = true; fall.duration = .08f; fall.canTransitionToSelf = false;
            AnimatorStateTransition rise = death.AddTransition(idle);
            rise.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            rise.hasExitTime = false; rise.hasFixedDuration = true; rise.duration = .1f;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void Leave(AnimatorState from, AnimatorState to, float exitTime, bool moving)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            if (moving) transition.AddCondition(AnimatorConditionMode.Greater, .1f, "MoveMagnitude");
            transition.hasExitTime = true; transition.exitTime = exitTime; transition.hasFixedDuration = true; transition.duration = .12f;
        }

        private static Material SkinMaterial(string key, Skin skin)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("The URP Lit shader is missing");
            Texture2D colour = Muted(key + "_Colour", skin);
            Texture2D normal = skin.Normal != null ? NormalMap(skin.Normal) : null;
            string path = Output + key + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            material.SetTexture("_BaseMap", colour); material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", normal);
            if (normal != null) material.EnableKeyword("_NORMALMAP"); else material.DisableKeyword("_NORMALMAP");
            material.SetFloat("_Metallic", 0f); material.SetFloat("_Smoothness", .18f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// The pack's colour texture, muted: colour' = (saturation * colour + (1 - saturation) * luminance) * tint * gain,
        /// per channel on the sRGB values, at the size the tablet needs.
        /// </summary>
        private static Texture2D Muted(string key, Skin skin)
        {
            var source = AssetDatabase.LoadAssetAtPath<Texture2D>(skin.Colour);
            if (source == null) throw new InvalidOperationException("Missing colour texture: " + skin.Colour);
            RenderTexture target = RenderTexture.GetTemporary(TextureSize, TextureSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            var copy = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                copy.ReadPixels(new Rect(0, 0, TextureSize, TextureSize), 0, 0);
                Color32[] pixels = copy.GetPixels32();
                float s = skin.Saturation, r = skin.Tint.r * skin.Gain, g = skin.Tint.g * skin.Gain, b = skin.Tint.b * skin.Gain;
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 p = pixels[i];
                    float grey = (1f - s) * (.2126f * p.r + .7152f * p.g + .0722f * p.b);
                    pixels[i] = new Color32(Channel((s * p.r + grey) * r), Channel((s * p.g + grey) * g), Channel((s * p.b + grey) * b), 255);
                }
                copy.SetPixels32(pixels);
                File.WriteAllBytes(Output + key + ".png", copy.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(copy);
            }
            string path = Output + key + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Default || !importer.sRGBTexture || importer.maxTextureSize != TextureSize || !importer.mipmapEnabled)
            {
                importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true;
                importer.maxTextureSize = TextureSize; importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static byte Channel(float value) { return (byte)Mathf.Clamp(Mathf.RoundToInt(value), 0, 255); }

        private static Texture2D NormalMap(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing normal map: " + path);
            if (importer.textureType != TextureImporterType.NormalMap || importer.maxTextureSize != TextureSize)
            {
                importer.textureType = TextureImporterType.NormalMap; importer.maxTextureSize = TextureSize;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
