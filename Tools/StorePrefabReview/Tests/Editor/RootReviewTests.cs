using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ouroboros.StorePrefabReview.Tests
{
    public class RootReviewTests
    {
        string folder, path, marker;
        string absoluteProject;
        [SetUp] public void SetUp()
        {
            absoluteProject = Directory.GetParent(Application.dataPath).FullName;
            folder = "Assets/RootReviewTest_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            path = folder + "/Fixture.prefab";
            marker = Path.Combine(absoluteProject, "StorePrefabReview.copy");
        }
        [TearDown] public void TearDown()
        {
            if (File.Exists(marker) && File.ReadAllLines(marker).LastOrDefault() == folder)
                File.Delete(marker);
            AssetDatabase.DeleteAsset(folder);
        }
        void AuthorizeTestCopy()
        {
            if (File.Exists(marker)) Assert.Ignore("Run tests in an empty disposable project without another copy marker.");
            File.WriteAllLines(marker, new[] { absoluteProject, folder });
        }
        void Create(bool dirty, bool physicsBody = false)
        {
            var root = new GameObject("Fixture");
            try
            {
                var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
                child.name = "Stone"; child.transform.SetParent(root.transform, false);
                child.transform.localRotation = Quaternion.Euler(13, 27, -9);
                child.transform.localPosition = new Vector3(3, 1, -4);
                var collider = child.GetComponent<BoxCollider>();
                collider.center = new Vector3(.1f, -.2f, .3f); collider.size = new Vector3(2, 3, 4);
                if (physicsBody) root.AddComponent<Rigidbody>();
                if (dirty)
                {
                    root.transform.SetLocalPositionAndRotation(new Vector3(2,-3,7), Quaternion.Euler(31,11,63));
                    root.transform.localScale = new Vector3(-2,.5f,3);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        [Test] public void RegularPrefabPreservesSignedScaleShearColliderAndLocalIds()
        {
            AuthorizeTestCopy(); Create(true);
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var old = root.transform.GetChild(0);
            Matrix4x4 matrix = old.localToWorldMatrix;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(old.GetComponent<BoxCollider>(),out string guid,out long id);
            byte[] meta = File.ReadAllBytes(path + ".meta");
            Assert.AreEqual(1, RootReview.Repair(folder));
            root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsTrue(RootReview.IsIdentity(root.transform));
            var rebuilt = root.GetComponentInChildren<BoxCollider>();
            Assert.AreEqual(matrix, rebuilt.transform.localToWorldMatrix);
            Assert.AreEqual(new Vector3(2,3,4), rebuilt.size);
            Assert.AreEqual(new Vector3(.1f,-.2f,.3f), rebuilt.center);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(rebuilt,out string guidAfter,out long idAfter);
            Assert.AreEqual((guid,id),(guidAfter,idAfter)); CollectionAssert.AreEqual(meta,File.ReadAllBytes(path + ".meta"));
            var bytes = File.ReadAllBytes(path);
            Assert.AreEqual(0,RootReview.Repair(folder)); CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
        }
        [TestCase(24f)]
        [TestCase(-31f)]
        public void UphillAndDownhillKeepTheSameWorldVertices(float slope)
        {
            AuthorizeTestCopy(); Create(true);
            var hill=new GameObject("Placed on a slope");
            hill.transform.SetLocalPositionAndRotation(new Vector3(40,12,-9),Quaternion.Euler(slope,52,0));
            hill.transform.localScale=new Vector3(.8f,1.2f,1.5f);
            try {
                Vector3[] WorldVertices()
                {
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    try {
                        instance.transform.SetParent(hill.transform,false);
                        var filter=instance.GetComponentInChildren<MeshFilter>();
                        return filter.sharedMesh.vertices.Select(v=>filter.transform.TransformPoint(v)).ToArray();
                    } finally {Object.DestroyImmediate(instance);}
                }
                var previous=WorldVertices();
                Assert.AreEqual(1,RootReview.Repair(folder));
                CollectionAssert.AreEqual(previous,WorldVertices());
            } finally {Object.DestroyImmediate(hill);}
        }
        [Test] public void AnimatorOnAnotherPrefabBlocksSafeAndAnimatedWritesTogether()
        {
            AuthorizeTestCopy(); Create(true); byte[] original=File.ReadAllBytes(path);
            string animatedPath=folder+"/Z_Animated.prefab";
            var actor=new GameObject("Animated");
            try {
                actor.AddComponent<Animator>(); actor.transform.localScale=Vector3.one*2;
                PrefabUtility.SaveAsPrefabAsset(actor,animatedPath);
            } finally {Object.DestroyImmediate(actor);}
            var animated=File.ReadAllBytes(animatedPath);
            Assert.Throws<InvalidOperationException>(()=>RootReview.Repair(folder));
            CollectionAssert.AreEqual(original,File.ReadAllBytes(path));
            CollectionAssert.AreEqual(animated,File.ReadAllBytes(animatedPath));
        }
        [Test] public void CleanPrefabKeepsExactBytes()
        {
            AuthorizeTestCopy(); Create(false);
            var bytes=File.ReadAllBytes(path);
            Assert.AreEqual(0,RootReview.Repair(folder)); CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
        }
        [Test] public void UnmarkedProjectRefusesWrite()
        {
            Create(true); var bytes=File.ReadAllBytes(path);
            Assert.Throws<InvalidOperationException>(()=>RootReview.Repair(folder));
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
        }
        [Test] public void RigidbodyIsReportedForManualReview()
        {
            AuthorizeTestCopy(); Create(true,true); var bytes=File.ReadAllBytes(path);
            Assert.Throws<InvalidOperationException>(()=>RootReview.Repair(folder));
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
        }
        [Test] public void DependentPrefabBlocksTheWholeBatchBeforeWrites()
        {
            AuthorizeTestCopy(); Create(true);
            var host=new GameObject("Host");
            try {
                var nested=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                nested.transform.SetParent(host.transform,false);
                PrefabUtility.SaveAsPrefabAsset(host,folder+"/Consumer.prefab");
            } finally {Object.DestroyImmediate(host);}
            var bytes=File.ReadAllBytes(path);
            Assert.Throws<InvalidOperationException>(()=>RootReview.Repair(folder));
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
        }
        [Test] public void TinyRotationAndTranslationAreDetected()
        {
            var go = new GameObject("Tiny");
            try {
                go.transform.localPosition=new Vector3(1e-8f,0,0); Assert.IsFalse(RootReview.IsIdentity(go.transform));
                go.transform.localPosition=Vector3.zero;
                go.transform.localRotation=new Quaternion(0.000001f,0,0,1); Assert.IsFalse(RootReview.IsIdentity(go.transform));
            } finally {Object.DestroyImmediate(go);}
        }
    }
}
