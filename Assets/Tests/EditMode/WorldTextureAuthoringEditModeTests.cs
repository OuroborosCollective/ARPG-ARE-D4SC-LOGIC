using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AreArpg.EditMode.Tests
{
    public sealed class WorldTextureFixtureIsolationTests
    {
        [Test] public void CleanupPreservesFilesThatWereOnDiskBeforeSetup()
        {
            const string root = "Assets/Resources/ARE/Visuals/WorldSurfaces/";
            string path = root + "Unimported_" + Guid.NewGuid().ToString("N") + ".txt";
            bool ownsRoot = !System.IO.Directory.Exists(root);
            const string contents = "Pre-existing asset must survive fixture cleanup.";
            try
            {
                System.IO.Directory.CreateDirectory(root);
                System.IO.File.WriteAllText(path, contents);
                CollectionAssert.DoesNotContain(AssetDatabase.GetAllAssetPaths(), path);
                var fixture = new WorldTextureAuthoringEditModeTests();
                fixture.Setup();
                fixture.Cleanup();
                Assert.IsTrue(System.IO.File.Exists(path), "cleanup deleted a pre-existing unimported asset");
                Assert.AreEqual(contents, System.IO.File.ReadAllText(path));
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                if (ownsRoot && System.IO.Directory.Exists(root) && System.IO.Directory.GetFileSystemEntries(root).Length == 0)
                    AssetDatabase.DeleteAsset(root.TrimEnd('/'));
            }
        }
    }

    public sealed class WorldTextureAuthoringEditModeTests
    {
        private const string Root = "Assets/Resources/ARE/Visuals/WorldSurfaces/";
        private Type _author;
        private string _fixture;
        private HashSet<string> _before;
        private bool _ownsRoot;

        [SetUp] public void Setup()
        {
            _author = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AreArpg.EditorTools.WorldTextureAuthoring")).First(t => t != null);
            _fixture = "Assets/PrivateArt/WorldUvTest_" + Guid.NewGuid().ToString("N") + ".asset";
            _before = new HashSet<string>(AssetDatabase.GetAllAssetPaths());
            _ownsRoot = !System.IO.Directory.Exists(Root);
            System.IO.Directory.CreateDirectory(Root);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [TearDown] public void Cleanup()
        {
            foreach (string path in AssetDatabase.GetAllAssetPaths())
                if (!_before.Contains(path) && (path.StartsWith(Root, StringComparison.Ordinal) || path == _fixture))
                    AssetDatabase.DeleteAsset(path);
            if (_ownsRoot && System.IO.Directory.Exists(Root) && System.IO.Directory.GetFileSystemEntries(Root).Length == 0)
                AssetDatabase.DeleteAsset(Root.TrimEnd('/'));
        }

        [Test] public void SurfaceOnlyRecipeCanEnumerateItsBoundAssets()
        {
            string id = "TestSurface_" + Guid.NewGuid().ToString("N");
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, Root + id + ".mat");
            Type type = _author.GetNestedType("Recipe", BindingFlags.NonPublic);
            object recipe = JsonUtility.FromJson("{\"surfaces\":[{\"id\":\"" + id + "\"}]}", type);
            var paths = (SortedSet<string>)_author.GetMethod("BoundAssets", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new[] { recipe });
            CollectionAssert.Contains(paths, Root + id + ".mat");
        }

        [Test] public void ReapplyingPlanarUvsUsesNewScaleAndPreservesTheSource()
        {
            var source = new Mesh { name = "OriginalFixture", vertices = new[] { Vector3.zero, new Vector3(4, 0, 0), new Vector3(0, 0, 4) }, triangles = new[] { 0, 2, 1 } };
            source.RecalculateNormals(); AssetDatabase.CreateAsset(source, _fixture);
            var vertices = source.vertices;
            var project = _author.GetMethod("PlanarMesh", BindingFlags.NonPublic | BindingFlags.Static);
            var first = (Mesh)project.Invoke(null, new object[] { source, 2f });
            Vector2[] uv = first.uv;
            var second = (Mesh)project.Invoke(null, new object[] { first, 4f });
            Assert.AreNotSame(first, second, "changed scale must not reuse stale UVs");
            for (int i = 0; i < uv.Length; i++) Assert.AreEqual(uv[i] * .5f, second.uv[i]);
            Assert.AreSame(first, project.Invoke(null, new object[] { second, 2f }), "returning to a scale reuses the same original-source derivation");
            CollectionAssert.AreEqual(vertices, source.vertices);
            Assert.IsEmpty(source.uv);
            CollectionAssert.AreEqual(uv, first.uv, "other prefabs sharing the first mesh must not change");
        }
    }
}
