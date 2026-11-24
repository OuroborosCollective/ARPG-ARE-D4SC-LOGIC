using System;
using System.IO;
using System.Reflection;
using AshenCrypt.WorldForge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AshenCrypt.WorldForge.Tests
{
    public class TreeWrapperIdentityTests
    {
        private string folder;

        [SetUp]
        public void SetUp()
        {
            folder = "Assets/__WFWrapperIdentity_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            AssetDatabase.CreateFolder(folder, "First");
            AssetDatabase.CreateFolder(folder, "Second");
            AssetDatabase.CreateFolder(folder, "Wrappers");
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(folder)) AssetDatabase.DeleteAsset(folder);
        }

        private GameObject Source(string directory, string name, PrimitiveType shape)
        {
            var root = new GameObject(name);
            try
            {
                var visual = GameObject.CreatePrimitive(shape);
                visual.transform.SetParent(root.transform, false);
                return PrefabUtility.SaveAsPrefabAsset(root, folder + "/" + directory + "/" + name + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private GameObject Wrap(GameObject source)
        {
            Type utility = typeof(WFBlenderIntake).Assembly.GetType("AshenCrypt.WorldForge.EditorTools.WFEditorUtil");
            Assert.IsNotNull(utility);
            MethodInfo create = utility.GetMethod("CreateTreeWrapper", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(create);
            return (GameObject)create.Invoke(null, new object[] { source, folder + "/Wrappers", .012f });
        }

        private string CollisionPath(out GameObject first, out GameObject second)
        {
            first = Source("First", "SharedTree", PrimitiveType.Cube);
            second = Source("Second", "SharedTree", PrimitiveType.Sphere);
            Wrap(first);
            return AssetDatabase.GetAssetPath(Wrap(second));
        }

        private string OccupiedTarget(bool fallback, out GameObject source)
        {
            string collision = CollisionPath(out GameObject first, out GameObject second);
            source = fallback ? second : first;
            return fallback ? collision : AssetDatabase.GetAssetPath(Wrap(first));
        }

        private void AssertRefusedWithFilesIntact(GameObject source, string path, string payloadPath)
        {
            byte[] contents = File.ReadAllBytes(payloadPath);
            byte[] metadata = File.ReadAllBytes(path + ".meta");
            Exception error = null;
            try { Wrap(source); }
            catch (TargetInvocationException ex) { error = ex.InnerException; }

            Assert.IsTrue(File.Exists(payloadPath), "The occupied destination must remain present.");
            CollectionAssert.AreEqual(contents, File.ReadAllBytes(payloadPath),
                "The existing destination's bytes must survive the refused import.");
            Assert.IsTrue(File.Exists(path + ".meta"), "Existing asset identity must remain present.");
            CollectionAssert.AreEqual(metadata, File.ReadAllBytes(path + ".meta"),
                "The existing destination's metadata must survive the refused import.");
            Assert.IsInstanceOf<InvalidOperationException>(error,
                "An occupied destination must be refused before SaveAsPrefabAsset.");
        }

        [Test]
        public void ForeignPrefabAtIdentityPathIsRefusedWithoutChangingBytesOrGuid()
        {
            string path = CollisionPath(out GameObject first, out GameObject second);
            var foreign = new GameObject("OtherSourceOwner");
            try
            {
                var child = (GameObject)PrefabUtility.InstantiatePrefab(first);
                child.transform.SetParent(foreign.transform, false);
                Assert.IsNotNull(PrefabUtility.SaveAsPrefabAsset(foreign, path));
            }
            finally { UnityEngine.Object.DestroyImmediate(foreign); }
            AssertRefusedWithFilesIntact(second, path, path);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnimportedOccupiedFileIsRefusedWithoutChangingBytesOrGuid(bool fallback)
        {
            string path = OccupiedTarget(fallback, out GameObject source);
            byte[] metadata = File.ReadAllBytes(path + ".meta");
            Assert.IsTrue(AssetDatabase.DeleteAsset(path));
            // Keep the foreign file outside the importer while this synchronous test owns it.
            // It must be rejected from its presence, without importing malformed fixture bytes.
            AssetDatabase.DisallowAutoRefresh();
            try
            {
                File.WriteAllText(path, "This destination belongs to an external source.\n");
                File.WriteAllBytes(path + ".meta", metadata);
                AssertRefusedWithFilesIntact(source, path, path);
            }
            finally
            {
                try
                {
                    if (File.Exists(path)) File.Delete(path);
                    if (File.Exists(path + ".meta")) File.Delete(path + ".meta");
                }
                finally
                {
                    AssetDatabase.AllowAutoRefresh();
                    AssetDatabase.Refresh();
                }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OccupiedDirectoryIsRefusedWithoutChangingBytesOrGuid(bool fallback)
        {
            string path = OccupiedTarget(fallback, out GameObject source);
            Assert.IsTrue(AssetDatabase.DeleteAsset(path));
            Assert.IsNotEmpty(AssetDatabase.CreateFolder(folder + "/Wrappers", Path.GetFileName(path)));
            string marker = path + "/owner.txt";
            File.WriteAllText(marker, "Existing directory contents must remain intact.\n");
            AssertRefusedWithFilesIntact(source, path, marker);
            Assert.IsTrue(Directory.Exists(path));
        }

        [TestCase("SharedTree", "SharedTree")]
        [TestCase("Tree Elm", "Tree_Elm")]
        public void DistinctPrefabSourcesRetainSeparateWrappersWhenNamesCollide(string firstName, string secondName)
        {
            GameObject first = Source("First", firstName, PrimitiveType.Cube);
            GameObject second = Source("Second", secondName, PrimitiveType.Sphere);
            Assert.AreNotEqual(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(first)),
                               AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(second)));
            GameObject firstWrapper = Wrap(first);
            string firstPath = AssetDatabase.GetAssetPath(firstWrapper);
            string firstGuid = AssetDatabase.AssetPathToGUID(firstPath);
            GameObject secondWrapper = Wrap(second);
            string secondPath = AssetDatabase.GetAssetPath(secondWrapper);

            Assert.AreNotEqual(firstPath, secondPath,
                "A model name is not a source identity: the second model must not receive the first model's prefab.");
            Assert.AreEqual(first, PrefabUtility.GetCorrespondingObjectFromSource(
                firstWrapper.transform.GetChild(0).gameObject));
            Assert.AreEqual(second, PrefabUtility.GetCorrespondingObjectFromSource(
                secondWrapper.transform.GetChild(0).gameObject));
            Assert.AreEqual(firstPath, AssetDatabase.GetAssetPath(Wrap(first)));
            Assert.AreEqual(secondPath, AssetDatabase.GetAssetPath(Wrap(second)));
            Assert.AreEqual(firstGuid, AssetDatabase.AssetPathToGUID(firstPath),
                "Repeating an import must retain the first wrapper's existing GUID.");
        }
    }
}
