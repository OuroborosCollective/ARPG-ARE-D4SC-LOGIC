using System;
using System.Collections.Generic;
using System.Reflection;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AreArpg.EditMode.Tests
{
    /// <summary>
    /// Owner rule 2026-11-19: every human figure the game shows is built from the unified BoZo figures. The purchased
    /// pack and the baked figures are private; without them these checks are reported as ignored, never as passed.
    /// </summary>
    public sealed class BozoFigureVisualTests
    {
        const string Baked = "Assets/PrivateArt/BozoCharacters/";
        const string NotInstalled = "Requires the purchased BoZo pack with baked figures (private, never in the repository)";

        static Type Recipes() { return Tool("BozoFigureRecipes"); }

        [Test]
        public void RebuildingALegacyPropKeepsItsSourceFinish()
        {
            string bakeFolder = Baked.TrimEnd('/');
            string propsFolder = Baked + "Props";
            bool madeBakeFolder = !AssetDatabase.IsValidFolder(bakeFolder);
            bool madePropsFolder = !AssetDatabase.IsValidFolder(propsFolder);
            string name = "PropFinishProbe_" + GUID.Generate();
            string sourcePath = "Assets/" + name + ".mat";
            string outputPath = null;
            try
            {
                if (madeBakeFolder) AssetDatabase.CreateFolder("Assets/PrivateArt", "BozoCharacters");
                if (madePropsFolder) AssetDatabase.CreateFolder(bakeFolder, "Props");
                var source = new Material(Shader.Find("Standard")) { name = name };
                source.SetFloat("_Glossiness", .8f);
                source.SetFloat("_Metallic", .3f);
                AssetDatabase.CreateAsset(source, sourcePath);
                object prop = Activator.CreateInstance(Recipes().GetNestedType("Prop"));
                var worn = Tool("FigureProps").GetMethod("Worn", BindingFlags.Static | BindingFlags.NonPublic);
                var first = (Material)worn.Invoke(null, new[] { (object)source, prop });
                outputPath = AssetDatabase.GetAssetPath(first);
                float initial = first.GetFloat("_Smoothness");
                string guid = AssetDatabase.AssetPathToGUID(outputPath);
                AssetDatabase.SaveAssetIfDirty(first);
                var rebuilt = (Material)worn.Invoke(null, new[] { (object)source, prop });
                Assert.AreEqual(outputPath, AssetDatabase.GetAssetPath(rebuilt));
                Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(rebuilt)));
                Assert.AreEqual(initial, rebuilt.GetFloat("_Smoothness"), .0001f, "rebuilding must keep the same finish");
                Assert.AreEqual(.4f, initial, .0001f, "the worn finish halves the source glossiness");
                Assert.AreEqual(.3f, rebuilt.GetFloat("_Metallic"), .0001f);
                Assert.AreEqual(.8f, source.GetFloat("_Glossiness"), .0001f);
            }
            finally
            {
                if (!string.IsNullOrEmpty(outputPath)) AssetDatabase.DeleteAsset(outputPath);
                AssetDatabase.DeleteAsset(sourcePath);
                if (madePropsFolder) AssetDatabase.DeleteAsset(propsFolder);
                if (madeBakeFolder) AssetDatabase.DeleteAsset(bakeFolder);
            }
        }

        static Type Tool(string name)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType("AreArpg.EditorTools." + name);
                if (type != null) return type;
            }
            Assert.Fail(name + " is missing");
            return null;
        }

        [Test]
        public void BuildGuardRestoresNestedResourcesFolders()
        {
            Type guard = Tool("BozoBuildGuard");
            string vendor = (string)guard.GetField("VendorRoot").GetRawConstantValue();
            bool createdVendor = !AssetDatabase.IsValidFolder(vendor);
            string probe = vendor + "/BuildGuardRegression";
            Assert.IsFalse(AssetDatabase.IsValidFolder(probe));
            try
            {
                if (createdVendor) AssetDatabase.CreateFolder("Assets", System.IO.Path.GetFileName(vendor));
                AssetDatabase.CreateFolder(vendor, "BuildGuardRegression");
                AssetDatabase.CreateFolder(probe, "Resources");
                AssetDatabase.CreateFolder(probe + "/Resources", "Child");
                AssetDatabase.CreateFolder(probe + "/Resources/Child", "Resources");
                guard.GetMethod("Exclude").Invoke(null, null);
                Assert.IsTrue(AssetDatabase.IsValidFolder(probe + "/ResourcesNotInBuild/Child/ResourcesNotInBuild"));
                guard.GetMethod("Restore").Invoke(null, null);
                Assert.IsTrue(AssetDatabase.IsValidFolder(probe + "/Resources/Child/Resources"));
            }
            finally
            {
                try { guard.GetMethod("Restore").Invoke(null, null); }
                finally { AssetDatabase.DeleteAsset(createdVendor ? vendor : probe); }
            }
        }

        /// <summary>
        /// The pack keeps all its parts in Resources folders. During a player build they are hidden, otherwise the whole
        /// purchased pack would be shipped inside the APK; afterwards the creator finds its parts again.
        /// </summary>
        [Test]
        public void BuildGuardHidesThePackFromPlayerBuildsAndRestoresIt()
        {
            Type guard = Tool("BozoBuildGuard");
            MethodInfo folders = guard.GetMethod("Folders", BindingFlags.Public | BindingFlags.Static);
            var before = (string[])folders.Invoke(null, new object[] { "Resources" });
            if (before.Length == 0) Assert.Ignore(NotInstalled);
            const string part = "Top/Top_RangerVest";
            Assert.IsNotNull(Resources.Load(part), "The creator loads its parts by name");
            try
            {
                Assert.AreEqual(before.Length, (int)guard.GetMethod("Exclude", BindingFlags.Public | BindingFlags.Static).Invoke(null, null));
                Assert.IsEmpty((string[])folders.Invoke(null, new object[] { "Resources" }), "No pack folder may stay a Resources folder during a build");
                Assert.IsNull(Resources.Load(part), "Hidden parts are not shipped");
            }
            finally { guard.GetMethod("Restore", BindingFlags.Public | BindingFlags.Static).Invoke(null, null); }
            CollectionAssert.AreEqual(before, (string[])folders.Invoke(null, new object[] { "Resources" }));
            Assert.IsNotNull(Resources.Load(part), "The creator finds its parts again after the build");
        }

        static T Field<T>(object target, string name) { return (T)target.GetType().GetField(name).GetValue(target); }

        /// <summary>Game visuals of the figures baked in this workspace with the height their role asks for.</summary>
        static Dictionary<string, float> BakedFigures()
        {
            Type recipes = Recipes();
            MethodInfo owns = recipes.GetMethod("Owns", BindingFlags.Public | BindingFlags.Static);
            var result = new Dictionary<string, float>();
            foreach (object figure in (Array)recipes.GetMethod("Load", BindingFlags.Public | BindingFlags.Static).Invoke(null, null))
                foreach (string key in Field<string[]>(figure, "keys"))
                    if ((bool)owns.Invoke(null, new object[] { key })) result.Add(key, Field<float>(figure, "height"));
            return result;
        }

        /// <summary>
        /// What a recipe lets a figure carry hangs on the named bone of its game visual as plain meshes: nothing that
        /// runs or collides, in a private material of its own, at the size the recipe asks for. A prop whose private
        /// model is not installed is left out; every other one must be there.
        /// </summary>
        [Test]
        public void WhatAFigureCarriesHangsOnItsBonesAsPassiveMeshes()
        {
            if (BakedFigures().Count == 0) Assert.Ignore(NotInstalled);
            int carried = 0;
            foreach (object figure in (Array)Recipes().GetMethod("Load", BindingFlags.Public | BindingFlags.Static).Invoke(null, null))
            {
                var props = Field<Array>(figure, "props");
                if (props == null || props.Length == 0) continue;
                foreach (string key in Field<string[]>(figure, "keys"))
                {
                    GameObject prefab = Resources.Load<GameObject>("ARE/Visuals/" + key);
                    if (prefab == null) continue;
                    var host = new GameObject("FigureHost");
                    try
                    {
                        Assert.IsTrue(PassiveArtVisuals.TryBindGroundedPrefab(host.transform, prefab, 0f), key + " must still pass the game's passive-art check");
                        Animator animator = host.GetComponentInChildren<Animator>(true);
                        foreach (object prop in props)
                        {
                            string name = Field<string>(prop, "name"), where = key + ": " + name;
                            Transform holder = null;
                            foreach (Transform node in host.GetComponentsInChildren<Transform>(true))
                                if (node.name == PassiveArtVisuals.CarriedPrefix + name) holder = node;
                            if (AssetDatabase.LoadAssetAtPath<GameObject>(Field<string>(prop, "asset")) == null)
                            {
                                Assert.IsNull(holder, where + ": no model, no prop");
                                continue;
                            }
                            Assert.IsNotNull(holder, where + " is in the recipe but not on the figure");
                            var bone = (HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), Field<string>(prop, "bone"));
                            Assert.AreSame(animator.GetBoneTransform(bone), holder.parent, where + " hangs on its bone");
                            foreach (Component component in holder.GetComponentsInChildren<Component>(true))
                                Assert.IsTrue(component is Transform || component is MeshFilter || component is MeshRenderer, where + ": only plain meshes, found " + component.GetType().Name);
                            MeshRenderer[] renderers = holder.GetComponentsInChildren<MeshRenderer>(true);
                            Assert.Greater(renderers.Length, 0, where + " shows something");
                            Bounds bounds = renderers[0].bounds;
                            foreach (MeshRenderer renderer in renderers)
                            {
                                bounds.Encapsulate(renderer.bounds);
                                Assert.IsNotNull(renderer.GetComponent<MeshFilter>().sharedMesh, where + ": mesh");
                                foreach (Material material in renderer.sharedMaterials)
                                {
                                    Assert.IsTrue(material != null && material.shader.isSupported, where + ": the material must render");
                                    StringAssert.StartsWith(Baked + "Props/", AssetDatabase.GetAssetPath(material), where + ": a worn copy of the pack's material, never the pack's own");
                                }
                            }
                            float size = Field<float>(prop, "size");
                            // The bounds are measured in the rest pose, where the prop sits as it was fitted; a turned box is a little larger.
                            if (size > 0f) Assert.That(Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z), Is.InRange(size * .6f, size * 1.45f), where + ": size");
                            Assert.Greater(bounds.min.y, 0.02f, where + " stays above the ground the figure stands on");
                            carried++;
                        }
                        // A stand-in that borrows the body takes all of it off again.
                        PassiveArtVisuals.DropCarried(host.transform);
                        foreach (Transform node in host.GetComponentsInChildren<Transform>(true))
                            StringAssert.DoesNotStartWith(PassiveArtVisuals.CarriedPrefix, node.name, key + ": dropped");
                        Assert.AreEqual(FigureComposer.IsComposite(prefab) ? 2 : 1, host.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length, key + ": the body stays");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(host); }
                }
            }
            if (carried == 0) Assert.Ignore("No built figure carries anything here (the private prop models are not installed)");
        }

        /// <summary>
        /// A colour variant brings no second model into the game: its visual draws the mesh of its model figure with
        /// a material of its own.
        /// </summary>
        [Test]
        public void AColourVariantDrawsTheMeshOfItsModelWithItsOwnMaterial()
        {
            Type recipes = Recipes();
            var all = (Array)recipes.GetMethod("Load", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            int variants = 0;
            foreach (object figure in all)
            {
                string model = Field<string>(figure, "sameShapeAs");
                if (string.IsNullOrEmpty(model)) continue;
                object modelFigure = null;
                foreach (object other in all) if (Field<string>(other, "id") == model) modelFigure = other;
                Assert.IsNotNull(modelFigure, Field<string>(figure, "id") + " names a model that is no figure");
                string key = Field<string[]>(figure, "keys")[0], modelKey = Field<string[]>(modelFigure, "keys")[0];
                GameObject visual = Resources.Load<GameObject>("ARE/Visuals/" + key), modelVisual = Resources.Load<GameObject>("ARE/Visuals/" + modelKey);
                if (visual == null || modelVisual == null) continue;
                SkinnedMeshRenderer skin = visual.GetComponentInChildren<SkinnedMeshRenderer>(true), modelSkin = modelVisual.GetComponentInChildren<SkinnedMeshRenderer>(true);
                Assert.AreSame(modelSkin.sharedMesh, skin.sharedMesh, key + " draws the mesh of " + modelKey);
                Assert.AreNotSame(modelSkin.sharedMaterial, skin.sharedMaterial, key + " has colours of its own");
                Assert.IsNull(AssetDatabase.LoadAssetAtPath<Mesh>(Baked + Field<string>(figure, "id") + "/" + Field<string>(figure, "id") + "_GameMesh.asset"),
                    key + " keeps no second copy of the mesh");
                variants++;
            }
            if (variants == 0) Assert.Ignore(NotInstalled);
        }

        [Test]
        public void AVisualWithoutABakedFigureStaysWithItsPreviousBuilder()
        {
            MethodInfo owns = Recipes().GetMethod("Owns", BindingFlags.Public | BindingFlags.Static);
            Assert.IsFalse((bool)owns.Invoke(null, new object[] { "EntryEnemy0" }), "Monsters are not part of the figure recipes");
            Assert.IsFalse((bool)owns.Invoke(null, new object[] { "NoSuchVisual" }));
        }

        /// <summary>A mistyped recipe key must not overwrite art that is not a figure (monsters, weapons, worlds).</summary>
        [Test]
        public void ARecipeKeyCannotOverwriteOtherArt()
        {
            if (BakedFigures().Count == 0) Assert.Ignore(NotInstalled);
            Type baker = Tool("BozoFigureBaker");
            MethodInfo mayWrite = baker.GetMethod("MayWrite", BindingFlags.Public | BindingFlags.Static);
            foreach (string foreign in new[] { "EntryEnemy0", "Enemy0", "EntryWeapon", "EntryCryptWorld" })
                if (Resources.Load<GameObject>("ARE/Visuals/" + foreign) != null)
                    Assert.IsFalse((bool)mayWrite.Invoke(null, new object[] { foreign }), foreign + " is not a figure and must stay");
            Assert.IsTrue((bool)mayWrite.Invoke(null, new object[] { "EntryNpc0" }), "A figure may be renewed");
            Assert.IsTrue((bool)mayWrite.Invoke(null, new object[] { "VillagerNotYetBuilt" }), "A new visual may be created");
        }

        [Test]
        public void EveryBakedFigureIsAcceptedByTheGameAsOnePassiveHumanoid()
        {
            Dictionary<string, float> figures = BakedFigures();
            if (figures.Count == 0) Assert.Ignore(NotInstalled);
            foreach (KeyValuePair<string, float> figure in figures)
            {
                string key = figure.Key;
                GameObject prefab = Resources.Load<GameObject>("ARE/Visuals/" + key);
                Assert.IsNotNull(prefab, key + " belongs to the unified figures but has no game visual");
                var host = new GameObject("FigureHost");
                var posed = new Mesh();
                try
                {
                    Assert.IsTrue(PassiveArtVisuals.TryBindGroundedPrefab(host.transform, prefab, 0f), key + " must pass the game's passive-art check");
                    SkinnedMeshRenderer[] skins = host.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    // One merged mesh, or a figure in two parts (body and head) on the same skeleton with the same material.
                    bool twoParts = FigureComposer.IsComposite(prefab);
                    Assert.AreEqual(twoParts ? 2 : 1, skins.Length, key + (twoParts ? ": body and head" : ": one merged mesh"));
                    SkinnedMeshRenderer skin = skins[0];
                    long triangles = 0;
                    foreach (SkinnedMeshRenderer each in skins)
                    {
                        Assert.AreEqual(1, each.sharedMaterials.Length, key + ": one material");
                        Assert.AreSame(skin.sharedMaterial, each.sharedMaterial, key + ": body and head share the figure's material");
                        Assert.IsTrue(each.sharedMaterial != null && each.sharedMaterial.shader.isSupported, key + ": the material must render");
                        StringAssert.StartsWith(Baked, AssetDatabase.GetAssetPath(each.sharedMaterial), key + ": the game keeps the figure's own material");
                        StringAssert.StartsWith(Baked, AssetDatabase.GetAssetPath(each.sharedMesh), key);
                        // The sliders are baked into the game mesh; only a story figure keeps the pack's expression shapes
                        // (owner remark 2026-11-23), and then at least the ones it blinks with.
                        for (int shape = 0; shape < each.sharedMesh.blendShapeCount; shape++)
                            StringAssert.StartsWith("Expression_", each.sharedMesh.GetBlendShapeName(shape), key + ": the sliders are baked into the game mesh");
                        if (each.sharedMesh.blendShapeCount > 0)
                            Assert.GreaterOrEqual(each.sharedMesh.GetBlendShapeIndex("Expression_Eyes_UpperClosed_L"), 0, key + ": a living face can blink");
                        CollectionAssert.AreEqual(skin.bones, each.bones, key + ": one skeleton");
                        for (int part = 0; part < each.sharedMesh.subMeshCount; part++) triangles += each.sharedMesh.GetIndexCount(part) / 3;
                    }
                    AssertReadableNormalMap(key, skin.sharedMaterial);
                    Assert.Less(triangles, 32000, key + ": triangle budget of one figure");

                    Animator animator = host.GetComponentInChildren<Animator>(true);
                    Assert.IsTrue(animator != null && animator.avatar != null && animator.avatar.isHuman && animator.avatar.isValid, key + ": humanoid rig");
                    Assert.IsFalse(animator.applyRootMotion, key + ": the Core moves the figure, not the animation");
                    AssertAvatarKnowsTheReshapedBones(key, animator);
                    Assert.IsFalse(PassiveArtVisuals.NeedsSharedLocomotion(animator), key + ": idle and walk come with the figure");
                    var controller = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
                    Assert.IsNotNull(controller, key);
                    Assert.IsTrue(Array.Exists(controller.parameters, p => p.name == "MoveMagnitude" && p.type == AnimatorControllerParameterType.Float),
                        key + ": walks on the game's MoveMagnitude signal");

                    float low = float.MaxValue, high = float.MinValue;
                    foreach (SkinnedMeshRenderer each in skins)
                    {
                        each.BakeMesh(posed); // world-sized: the scale of all parents is baked into the vertices
                        Matrix4x4 toWorld = Matrix4x4.TRS(each.transform.position, each.transform.rotation, Vector3.one);
                        foreach (Vector3 vertex in posed.vertices)
                        {
                            float y = toWorld.MultiplyPoint3x4(vertex).y;
                            low = Mathf.Min(low, y); high = Mathf.Max(high, y);
                        }
                    }
                    Assert.AreEqual(0f, low, .02f, key + " stands on its visible feet");
                    Assert.AreEqual(figure.Value, high - low, figure.Value * .02f, key + " has the height of its role");
                }
                finally { UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(posed); }
            }
        }

        /// <summary>
        /// The pack saves its normal atlas in a packing this project does not read. Used as it is, every surface seems
        /// to face away from the light and the figure looks as if it stood in the dark (owner finding 2026-11-19).
        /// </summary>
        static void AssertReadableNormalMap(string key, Material material)
        {
            Texture normal = material.HasProperty("_Normal") ? material.GetTexture("_Normal") : null;
            Assert.IsNotNull(normal, key + ": normal map");
            string path = AssetDatabase.GetAssetPath(normal);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.IsTrue(importer != null && importer.textureType == TextureImporterType.NormalMap,
                key + ": the normal atlas must be imported as a normal map, otherwise the figure is lit as if it stood in the dark");
            var pixels = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                Assert.IsTrue(pixels.LoadImage(System.IO.File.ReadAllBytes(path)), key);
                Color32[] colors = pixels.GetPixels32();
                long outward = 0; int samples = 0;
                for (int i = 0; i < colors.Length; i += 97) { outward += colors[i].b; samples++; }
                Assert.Greater(outward / (float)samples, 200f, key + ": normals must point out of the surface (blue channel)");
            }
            finally { UnityEngine.Object.DestroyImmediate(pixels); }
        }

        /// <summary>
        /// The body sliders scale bones. An avatar that describes the unscaled skeleton makes Unity place hands and
        /// feet beside the real bones (the hero's sword hand missed its grip by four centimetres).
        /// </summary>
        static void AssertAvatarKnowsTheReshapedBones(string key, Animator animator)
        {
            var known = new Dictionary<string, Vector3>();
            foreach (SkeletonBone bone in animator.avatar.humanDescription.skeleton) known[bone.name] = bone.scale;
            foreach (Transform bone in animator.GetComponentsInChildren<Transform>(true))
            {
                Vector3 scale;
                if (!known.TryGetValue(bone.name, out scale)) continue;
                Assert.Less((bone.localScale - scale).magnitude, .001f, key + ": the avatar must describe the scale of bone " + bone.name);
            }
        }

        [Test]
        public void HeroAndMillPatientsComeFromTheUnifiedFigures()
        {
            foreach (string id in new[] { "hero", "patient_a", "patient_b", "patient_c" })
            {
                string path = (string)Recipes().GetMethod("BakedPath").Invoke(null, new object[] { id });
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) Assert.Ignore(NotInstalled);
            }
            foreach (string key in new[] { "EntryCombatHero", "EntryHero" })
            {
                GameObject prefab = Resources.Load<GameObject>("ARE/Visuals/" + key);
                Assert.IsNotNull(prefab, key);
                GameObject hero = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    SkinnedMeshRenderer[] skins = hero.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    Assert.AreEqual(1, skins.Length, key + ": one merged mesh");
                    StringAssert.StartsWith(Baked, AssetDatabase.GetAssetPath(skins[0].sharedMesh), key + " must be the unified hero figure");
                    foreach (Material material in skins[0].sharedMaterials)
                    {
                        StringAssert.StartsWith(Baked, AssetDatabase.GetAssetPath(material), key + " keeps the figure's own material");
                        AssertReadableNormalMap(key, material);
                    }
                    Animator animator = hero.GetComponentInChildren<Animator>(true);
                    Assert.IsTrue(animator != null && animator.avatar != null && animator.avatar.isHuman, key + ": humanoid rig");
                    AssertAvatarKnowsTheReshapedBones(key, animator);
                    Assert.IsTrue(PassiveArtVisuals.HasPlayableClip(animator.runtimeAnimatorController), key + ": combat animations");
                    Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand), socket = null;
                    foreach (Transform node in hero.GetComponentsInChildren<Transform>(true))
                    {
                        if (node.name != "EntryWeaponSocket") continue;
                        Assert.IsNull(socket, key + ": exactly one weapon socket");
                        socket = node;
                    }
                    Assert.IsNotNull(socket, key + ": weapon socket");
                    Assert.AreSame(hand, socket.parent, key + ": the weapon is held in the right hand");
                }
                finally { UnityEngine.Object.DestroyImmediate(hero); }
            }
            for (int id = 0; id < 3; id++)
            {
                GameObject patient = Resources.Load<GameObject>("ARE/Visuals/MillPatient" + id);
                Assert.IsNotNull(patient, "MillPatient" + id);
                int parts = 0;
                foreach (Renderer renderer in patient.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.name == "Blanket") continue;
                    parts++;
                    foreach (Material material in renderer.sharedMaterials)
                        StringAssert.StartsWith(Baked, AssetDatabase.GetAssetPath(material.mainTexture),
                            "MillPatient" + id + " must wear the colour atlas of a unified figure");
                }
                Assert.Greater(parts, 0, "MillPatient" + id + " has a body");
            }
        }
    }
}
