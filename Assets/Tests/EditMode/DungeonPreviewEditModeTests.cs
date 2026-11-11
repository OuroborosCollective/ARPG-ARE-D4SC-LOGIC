using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AreArpg.EditMode.Tests
{
    /// <summary>Real private-asset authoring tests; no substitute for gameplay or Android evidence.</summary>
    public sealed class DungeonPreviewEditModeTests
    {
        private const string PathPrefix = "Assets/PrivateArt/DungeonPreviews/test-preview";
        private MethodInfo _build;
        private string _json;
        private bool _ownsOutput;
        private const string Reordered = "{\"version\":1,\"id\":\"test-preview\",\"cellMm\":4000,\"rooms\":[{\"id\":\"east\",\"x\":2,\"z\":1,\"width\":3,\"length\":3},{\"id\":\"west\",\"x\":-3,\"z\":1,\"width\":3,\"length\":3},{\"id\":\"hall\",\"x\":0,\"z\":0,\"width\":2,\"length\":4}],\"doors\":[{\"a\":\"hall\",\"b\":\"east\",\"axis\":\"z\",\"x\":2,\"z\":2},{\"a\":\"hall\",\"b\":\"west\",\"axis\":\"z\",\"x\":0,\"z\":2},{\"a\":\"hall\",\"b\":\"\",\"axis\":\"x\",\"x\":0,\"z\":0}]}";

        /// <summary>Bind the public authoring entrypoint and refuse any pre-existing test output.</summary>
        [SetUp]
        public void Setup()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR")))
                Assert.Ignore("Requires reviewed private dungeon assets in the native Unity Editor");
            Assert.IsFalse(File.Exists(PathPrefix + ".prefab") || File.Exists(PathPrefix + ".receipt.json"),
                "Preserve any existing authoring work");
            _ownsOutput = true;
            var builder = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AreArpg.EditorTools.EntryWorldArtBuilder"))
                .First(t => t != null);
            _build = builder.GetMethod("BuildDungeonPreview", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(_build, "Existing world builder must expose the preview authoring boundary");
            _json = File.ReadAllText("docs/examples/dungeon-preview-v1.json").Replace("crypt-three-rooms", "test-preview");
        }
        /// <summary>Remove only this fixture's explicitly owned private outputs.</summary>
        [TearDown]
        public void Cleanup()
        {
            if (!_ownsOutput) return;
            AssetDatabase.DeleteAsset(PathPrefix + ".prefab");
            AssetDatabase.DeleteAsset(PathPrefix + ".receipt.json");
            _ownsOutput = false;
        }
        private string Build(string json) { return (string)_build.Invoke(null, new object[] { json }); }

        /// <summary>Build real modules, compare independent geometry expectations and rebuild in another input order.</summary>
        [Test]
        public void ReviewedRoomsBuildARepeatablePassivePreview()
        {
            string live = "Assets/Resources/ARE/Visuals/EntryCryptWorld.prefab";
            byte[] liveBefore = File.ReadAllBytes(live);
            string path = Build(_json);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.AreEqual(56, prefab.transform.childCount, "26 original floors +27 walls +3 archways");
            Assert.AreEqual(26, prefab.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("floor-")));
            Assert.AreEqual(27, prefab.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("wall-")));
            Assert.AreEqual(3, prefab.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("door-")));
            foreach (var c in prefab.GetComponentsInChildren<Component>(true))
                Assert.IsTrue(c is Transform || c is MeshFilter || c is Renderer || c is LODGroup, "Passive preview only");
            foreach (var t in prefab.GetComponentsInChildren<Transform>())
                Assert.AreEqual(Vector3.one, t.localScale, "Original art proportions must remain intact");
            Assert.AreEqual(new Vector3(2f, 0f, 0f), prefab.transform.Find("door-x-0-0").localPosition);
            Assert.AreEqual(new Vector3(0f, 0f, 10f), prefab.transform.Find("door-z-0-2").localPosition);
            byte[] before = File.ReadAllBytes(path), receipt = File.ReadAllBytes(PathPrefix + ".receipt.json");
            Assert.AreEqual(path, Build(Reordered));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path), "Equivalent ordering must not rewrite the prefab");
            CollectionAssert.AreEqual(receipt, File.ReadAllBytes(PathPrefix + ".receipt.json"));
            CollectionAssert.AreEqual(liveBefore, File.ReadAllBytes(live), "Authoring must not replace the playable crypt");
            Capture(prefab);
        }

        /// <summary>Reject malformed topology and schema without changing the last accepted artifact.</summary>
        [TestCase("overlap")]
        [TestCase("duplicate-id")]
        [TestCase("bad-door")]
        [TestCase("disconnected")]
        [TestCase("wrong-version")]
        [TestCase("path-traversal")]
        [TestCase("unknown-dimensions")]
        public void InvalidPlanPreservesTheReviewedPreview(string fault)
        {
            string path = Build(_json);
            byte[] prefab = File.ReadAllBytes(path), receipt = File.ReadAllBytes(PathPrefix + ".receipt.json");
            string bad = Reordered;
            switch (fault)
            {
                case "overlap": bad = bad.Replace("\"id\":\"east\",\"x\":2", "\"id\":\"east\",\"x\":0"); break;
                case "duplicate-id": bad = bad.Replace("\"id\":\"east\"", "\"id\":\"west\""); break;
                case "bad-door": bad = bad.Replace("\"b\":\"west\"", "\"b\":\"missing\""); break;
                case "disconnected": bad = bad.Replace("{\"a\":\"hall\",\"b\":\"west\",\"axis\":\"z\",\"x\":0,\"z\":2},", ""); break;
                case "wrong-version": bad = bad.Replace("\"version\":1", "\"version\":2"); break;
                case "path-traversal": bad = bad.Replace("test-preview", "../escape"); break;
                case "unknown-dimensions": bad = bad.Replace("\"width\":", "\"widthInTiles\":"); break;
            }
            var e = Assert.Throws<TargetInvocationException>(() => Build(bad));
            Assert.IsInstanceOf<ArgumentException>(e.InnerException);
            CollectionAssert.AreEqual(prefab, File.ReadAllBytes(path));
            CollectionAssert.AreEqual(receipt, File.ReadAllBytes(PathPrefix + ".receipt.json"));
        }

        [Test]
        public void ManualPrefabChangesArePreserved()
        {
            string path = Build(_json);
            File.AppendAllText(path, "\n");
            byte[] manual = File.ReadAllBytes(path);
            var e = Assert.Throws<TargetInvocationException>(() => Build(_json));
            Assert.IsInstanceOf<InvalidOperationException>(e.InnerException);
            CollectionAssert.AreEqual(manual, File.ReadAllBytes(path));
        }

        /// <summary>Capture the actual imported meshes in a temporary, isolated Editor preview scene.</summary>
        private static void Capture(GameObject prefab)
        {
            string directory = Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR");
            var scene = EditorSceneManager.NewPreviewScene();
            var root = UnityEngine.Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(root, scene);
            var cameraObject = new GameObject("Preview camera");
            var lightObject = new GameObject("Preview light");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            var camera = cameraObject.AddComponent<Camera>();
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.035f, .045f, .06f);
            camera.orthographic = true; camera.orthographicSize = 15f; camera.farClipPlane = 200f;
            Vector3 focus = new Vector3(4f, 0f, 8f);
            camera.transform.position = focus + new Vector3(24f, 32f, -34f);
            camera.transform.LookAt(focus);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.3f;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            var target = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var active = RenderTexture.active;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(System.IO.Path.Combine(directory, "dungeon-preview.png"), pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = active;
                UnityEngine.Object.DestroyImmediate(pixels); target.Release(); UnityEngine.Object.DestroyImmediate(target);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
