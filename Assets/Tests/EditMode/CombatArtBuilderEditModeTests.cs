using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AreArpg.EditMode.Tests
{
    /// <summary>Native authoring fixtures; these do not replace the real private PlayMode acceptance.</summary>
    public sealed class CombatArtBuilderEditModeTests
    {
        private const string Folder = "Assets/PrivateArt/CombatBuilderTests";
        private const string Output = "Assets/Resources/ARE/Visuals/";
        private readonly HashSet<string> _outputs = new HashSet<string>();
        private Type _builder;
        private bool _ownsFolder;
        private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

        [SetUp]
        public void Setup()
        {
            Assert.IsFalse(Directory.Exists(Folder), "Never overwrite an existing authoring fixture folder");
            AssetDatabase.CreateFolder("Assets/PrivateArt", "CombatBuilderTests");
            _ownsFolder = true;
            _builder = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AreArpg.EditorTools.EntryCombatArtBuilder"))
                .FirstOrDefault(t => t != null);
            Assert.IsNotNull(_builder);
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (string path in _outputs) AssetDatabase.DeleteAsset(path);
            _outputs.Clear();
            if (_ownsFolder) AssetDatabase.DeleteAsset(Folder);
            _ownsFolder = false;
        }

        [Test]
        public void EmbeddedMaterialSlotsKeepDistinctCopies()
        {
            var texture = new Texture2D(2, 2);
            AssetDatabase.CreateAsset(texture, Folder + "/BaseTexture.asset");
            var first = new Material(Shader.Find("Standard")) { name = "First", color = Color.red, mainTexture = texture };
            var second = new Material(Shader.Find("Standard")) { name = "Second", color = Color.green, mainTexture = texture };
            AssetDatabase.CreateAsset(first, Folder + "/Embedded.asset");
            AssetDatabase.AddObjectToAsset(second, first);
            AssetDatabase.SaveAssets();
            var method = _builder.GetMethod("HeroMaterial", StaticPrivate);
            var a = (Material)method.Invoke(null, new object[] { first });
            _outputs.Add(AssetDatabase.GetAssetPath(a));
            var b = (Material)method.Invoke(null, new object[] { second });
            _outputs.Add(AssetDatabase.GetAssetPath(b));
            Assert.AreNotSame(a, b, "Materials in one source asset need separate output identities");
            Assert.AreNotEqual(AssetDatabase.GetAssetPath(a), AssetDatabase.GetAssetPath(b));
            Assert.AreEqual(Color.red, a.color);
            Assert.AreEqual(Color.green, b.color);
        }

        [Test]
        public void FlattenedDungeonMeshKeepsOnlyHighestDetail()
        {
            var root = new GameObject("LOD source");
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            try
            {
                var high = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var low = GameObject.CreatePrimitive(PrimitiveType.Cube);
                high.transform.SetParent(root.transform, false); low.transform.SetParent(root.transform, false);
                UnityEngine.Object.DestroyImmediate(high.GetComponent<Collider>());
                UnityEngine.Object.DestroyImmediate(low.GetComponent<Collider>());
                root.AddComponent<LODGroup>().SetLODs(new[] {
                    new LOD(.6f, new Renderer[] { high.GetComponent<Renderer>() }),
                    new LOD(.1f, new Renderer[] { low.GetComponent<Renderer>() }) });
                string path = Folder + "/Lods.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                AssetDatabase.CreateAsset(material, Folder + "/Fixture.mat");
                string key = "CombatBuilderFixture_" + AssetDatabase.AssetPathToGUID(path);
                _outputs.Add(Output + key + ".prefab");
                _builder.GetMethod("MeshPrefab", StaticPrivate).Invoke(null, new object[] { path, key, material });
                var result = AssetDatabase.LoadAssetAtPath<GameObject>(Output + key + ".prefab");
                Assert.AreEqual(1, result.GetComponentsInChildren<MeshRenderer>(true).Length,
                    "Flattening must not activate lower detail meshes beside LOD0");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void InvalidClipPreservesTheWorkingController()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR")))
                Assert.Ignore("Requires the actual reviewed private humanoid clips and controller");
            var inputType = _builder.GetNestedType("Inputs", BindingFlags.NonPublic);
            var input = JsonUtility.FromJson(File.ReadAllText("Assets/PrivateArt/entry-combat-art.json"), inputType);
            inputType.GetField("meleeClip").SetValue(input, "DefinitelyMissingReviewedTake");
            string controllerPath = Output + "EntryMelee.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            Assert.IsNotNull(controller);
            string before = EditorJsonUtility.ToJson(controller.layers[0].stateMachine);
            var backups = new Dictionary<string, byte[]>();
            foreach (string name in new[] { "EntryMelee.controller", "EntryIdle.anim", "EntryMove.anim", "EntryStrike.anim" })
                backups.Add(Output + name, File.ReadAllBytes(Output + name));
            try
            {
                var error = Assert.Throws<TargetInvocationException>(() => _builder.GetMethod("BuildController", StaticPrivate).Invoke(null, new[] { input }));
                Assert.IsInstanceOf<InvalidOperationException>(error.InnerException);
                Assert.AreEqual(before, EditorJsonUtility.ToJson(controller.layers[0].stateMachine),
                    "Rejected authoring input must leave existing states, transitions and motions intact");
            }
            finally
            {
                // Restore even the deliberately broken RED build without keeping test authoring changes.
                foreach (var backup in backups) File.WriteAllBytes(backup.Key, backup.Value);
                foreach (var backup in backups) AssetDatabase.ImportAsset(backup.Key, ImportAssetOptions.ForceUpdate);
            }
        }
    }
}
