using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AreArpg.EditMode.Tests
{
    /// <summary>
    /// The village's dark pictures go onto its built visuals as private copies of the source materials: the source
    /// stays as it was, a second run changes nothing, a rebuild that puts the source back is noticed, and bindings
    /// that reach outside the private folder or the built visuals are refused before anything is written.
    /// </summary>
    public sealed class VillageRetextureTests
    {
        private const string Folder = "Assets/PrivateArt/VillageRetextureTest/";
        private const string BindingsFile = "bindings.json";
        // The editor tools are not an assembly a test can reference: the pass is reached by name.
        private static readonly Type Tool = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("AreArpg.EditorTools.VillageRetexture")).First(type => type != null);
        private Material _source, _own, _other;
        private Texture2D _old, _dark;

        [SetUp]
        public void Build()
        {
            if (AssetDatabase.IsValidFolder(Folder.TrimEnd('/'))) AssetDatabase.DeleteAsset(Folder.TrimEnd('/'));
            Directory.CreateDirectory(Folder);
            _old = Picture("old", new Color(.8f, .3f, .1f));
            _dark = Picture("dark", new Color(.2f, .2f, .22f));
            Picture("dark_normal", new Color(.5f, .5f, 1f));
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            _source = Material(lit, "Source", _old, new Color(.9f, .8f, .7f));
            _own = Material(lit, "Own", _old, new Color(.6f, .6f, .6f));
            _other = Material(lit, "Other", null, Color.white);
            var root = new GameObject("House");
            try
            {
                Part(root, "Wall", _source, _own);
                Part(root, "Hill", _source);
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "House.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            Bind("\"prefabs\": [\"" + Folder + "House.prefab\"]");
        }

        [TearDown]
        public void Remove()
        {
            AssetDatabase.DeleteAsset(Folder.TrimEnd('/'));
        }

        [Test]
        public void TheBuiltPrefabWearsACopyAndTheSourceStaysAsItWas()
        {
            Assert.Greater(Apply(Folder), 0);

            Material copy = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Materials/Source.mat");
            Assert.IsNotNull(copy, "the copy lives in the private folder");
            Assert.AreEqual(Where(_dark), Where(copy.GetTexture("_BaseMap")));
            Assert.AreEqual(new Color(.9f, .8f, .7f), copy.GetColor("_BaseColor"), "a copy keeps the source's other values");
            Assert.AreEqual(Where(_old), Where(_source.GetTexture("_BaseMap")), "the source material is only read");
            Assert.AreEqual(Where(_dark), Where(_own.GetTexture("_BaseMap")), "a material of the game's own builders changes where it is");
            Assert.IsNotNull(_own.GetTexture("_BumpMap"));
            Assert.IsTrue(_own.IsKeywordEnabled("_NORMALMAP"));
            Assert.AreEqual(Color.white, _own.GetColor("_BaseColor"));
            Assert.AreEqual(.1f, _own.GetFloat("_Smoothness"), 1e-4f);

            Material[] wall = Slots("Wall"), hill = Slots("Hill");
            Assert.AreEqual(Where(copy), Where(wall[0]));
            Assert.AreEqual(Where(_own), Where(wall[1]));
            Assert.AreEqual(Where(_other), Where(hill[0]), "a named part takes the material its swap gives it");
            var normal = (TextureImporter)AssetImporter.GetAtPath(Folder + "dark_normal.png");
            Assert.AreEqual(TextureImporterType.NormalMap, normal.textureType);
            var picture = (TextureImporter)AssetImporter.GetAtPath(Folder + "dark.png");
            Assert.AreEqual(64, picture.GetPlatformTextureSettings("Android").maxTextureSize, "pictures keep the tablet's budget");
            Assert.IsFalse(picture.isReadable);

            CollectionAssert.IsEmpty(Unapplied(Folder));
            Assert.AreEqual(0, Apply(Folder), "a second run finds nothing to change");
        }

        [Test]
        public void ARebuildThatPutsTheSourceBackIsNoticedAndRepaired()
        {
            Apply(Folder);
            GameObject root = PrefabUtility.LoadPrefabContents(Folder + "House.prefab");
            try
            {
                root.transform.Find("Wall").GetComponent<Renderer>().sharedMaterials = new[] { _source, _own };
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "House.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            List<string> left = Unapplied(Folder);
            Assert.AreEqual(1, left.Count, string.Join(" | ", left));
            Assert.AreEqual(1, Apply(Folder));
            CollectionAssert.IsEmpty(Unapplied(Folder));
        }

        [Test]
        public void BindingsThatReachOutsideAreRefusedBeforeAnythingIsWritten()
        {
            Assert.Throws<InvalidOperationException>(() => Apply("Assets/Scripts/"), "pictures live below the private art");
            Assert.Throws<InvalidOperationException>(() => Apply(Folder + "../VillageRetextureTest/"), "no climbing out");

            Bind("\"prefabs\": [\"Assets/Scenes/Somewhere.prefab\"]");
            Assert.Throws<InvalidOperationException>(() => Apply(Folder), "only built visuals are written");
            Bind("\"prefabs\": [\"" + Folder + "../VillageRetextureTest/House.prefab\"]");
            Assert.Throws<InvalidOperationException>(() => Apply(Folder));
            Bind("\"prefabs\": [\"" + Folder + "House.prefab\"]", picture: "../dark");
            Assert.Throws<InvalidOperationException>(() => Apply(Folder), "a picture is a plain name");
            Bind("\"prefabs\": [\"" + Folder + "House.prefab\"]", picture: "missing");
            Assert.Throws<InvalidOperationException>(() => Apply(Folder), "a missing picture stops the run");
            Bind("\"prefabs\": [\"" + Folder + "House.prefab\"]", own: "Assets/PrivateArt/README.md");
            Assert.Throws<InvalidOperationException>(() => Apply(Folder), "nothing outside the folder changes in place");

            Assert.AreEqual(Where(_source), Where(Slots("Wall")[0]), "the prefab still wears its source");
            Assert.AreEqual(Where(_old), Where(_own.GetTexture("_BaseMap")));
        }

        [Test]
        public void TheInstalledVillageWearsItsDarkPictures()
        {
            if (!(bool)Tool.GetProperty("Installed").GetValue(null)) Assert.Ignore("Requires the private village pictures");
            CollectionAssert.IsEmpty(Unapplied((string)Tool.GetField("DefaultFolder").GetValue(null)));
        }

        private static void Bind(string prefabs, string picture = "dark", string own = Folder + "Own.mat")
        {
            File.WriteAllText(Folder + BindingsFile, "{\"maxSize\": 64, \"bindings\": ["
                + "{\"material\": \"" + Folder + "Source.mat\", \"albedo\": \"" + picture + "\"},"
                + "{\"material\": \"" + own + "\", \"albedo\": \"dark\", \"normal\": \"dark_normal\", \"smoothness\": 0.1, \"tintWhite\": true, \"inPlace\": true}],"
                + prefabs + ", \"swaps\": [{\"prefab\": \"" + Folder + "House.prefab\", \"child\": \"Hill\", \"material\": \"" + Folder + "Other.mat\"}]}");
        }

        /// <summary>Unity hands out a new managed wrapper after a reimport: an asset is known by where it lives.</summary>
        private static string Where(UnityEngine.Object asset) { return AssetDatabase.GetAssetPath(asset); }

        private static int Apply(string folder) { return (int)Call("Apply", folder); }

        private static List<string> Unapplied(string folder) { return (List<string>)Call("Unapplied", folder); }

        private static object Call(string name, string folder)
        {
            try { return Tool.GetMethod(name, BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { folder }); }
            catch (TargetInvocationException wrapped) { ExceptionDispatchInfo.Capture(wrapped.InnerException).Throw(); throw; }
        }

        private static Material[] Slots(string part)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "House.prefab").transform.Find(part).GetComponent<Renderer>().sharedMaterials;
        }

        private static void Part(GameObject root, string name, params Material[] materials)
        {
            var part = new GameObject(name);
            part.transform.SetParent(root.transform, false);
            part.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            part.AddComponent<MeshRenderer>().sharedMaterials = materials;
        }

        private static Material Material(Shader shader, string name, Texture2D picture, Color tint)
        {
            var material = new Material(shader);
            material.SetTexture("_BaseMap", picture);
            material.SetColor("_BaseColor", tint);
            AssetDatabase.CreateAsset(material, Folder + name + ".mat");
            return material;
        }

        private static Texture2D Picture(string name, Color colour)
        {
            var pixels = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            try
            {
                var fill = new Color[16];
                for (int i = 0; i < fill.Length; i++) fill[i] = colour;
                pixels.SetPixels(fill);
                File.WriteAllBytes(Folder + name + ".png", pixels.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(pixels); }
            AssetDatabase.ImportAsset(Folder + name + ".png", ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + name + ".png");
        }
    }
}
