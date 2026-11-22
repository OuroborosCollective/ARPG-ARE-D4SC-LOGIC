using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;

namespace AreArpg.EditMode.Tests
{
    /// <summary>
    /// Heads of figures built in two parts put on other bodies. The figures are private art; without them these tests
    /// are reported as ignored, never as passed.
    /// </summary>
    public sealed class FigureComposerTests
    {
        const string NotInstalled = "Requires the purchased BoZo pack with figures baked in two parts (private, never in the repository)";

        /// <summary>Game visual key and base preset of every figure the recipes build in two parts.</summary>
        static List<KeyValuePair<string, string>> TwoPartFigures()
        {
            Type recipes = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) recipes = recipes ?? assembly.GetType("AreArpg.EditorTools.BozoFigureRecipes");
            Assert.IsNotNull(recipes, "BozoFigureRecipes is missing");
            var result = new List<KeyValuePair<string, string>>();
            foreach (object figure in (Array)recipes.GetMethod("Load", BindingFlags.Public | BindingFlags.Static).Invoke(null, null))
            {
                Type type = figure.GetType();
                if (!(bool)type.GetField("parts").GetValue(figure)) continue;
                string key = ((string[])type.GetField("keys").GetValue(figure))[0];
                if (Resources.Load<GameObject>("ARE/Visuals/" + key) != null) result.Add(new KeyValuePair<string, string>(key, (string)type.GetField("base").GetValue(figure)));
            }
            if (result.Count < 2) Assert.Ignore(NotInstalled);
            return result;
        }

        static Transform Bone(Transform figure, string name)
        {
            foreach (Transform bone in figure.GetComponentsInChildren<Transform>(true)) if (bone.name == name) return bone;
            return null;
        }

        /// <summary>World positions of a part as it is posed now.</summary>
        static List<Vector3> Posed(SkinnedMeshRenderer skin)
        {
            var mesh = new Mesh();
            try
            {
                skin.BakeMesh(mesh);
                Matrix4x4 toWorld = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
                return mesh.vertices.Select(v => toWorld.MultiplyPoint3x4(v)).ToList();
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }

        /// <summary>How far the lowest band of a head lies from the nearest point of a body: the 90th percentile, in metres.</summary>
        static float NeckGap(List<Vector3> head, List<Vector3> body)
        {
            float low = head.Min(v => v.y);
            var near = body.Where(v => v.y > low - .12f && v.y < low + .15f).ToList();
            var gaps = head.Where(v => v.y < low + .03f).Select(v => Mathf.Sqrt(near.Min(b => (b - v).sqrMagnitude))).OrderBy(g => g).ToList();
            return gaps[gaps.Count * 9 / 10];
        }

        [Test]
        public void ATwoPartFigureHasABodyAndAHeadOnOneSkeleton()
        {
            foreach (var figure in TwoPartFigures())
            {
                GameObject prefab = Resources.Load<GameObject>("ARE/Visuals/" + figure.Key);
                Assert.IsTrue(FigureComposer.IsComposite(prefab), figure.Key + " is built in two parts");
                SkinnedMeshRenderer body = FigureComposer.Part(prefab.transform, FigureComposer.BodyPart), head = FigureComposer.Part(prefab.transform, FigureComposer.HeadPart);
                Assert.AreNotSame(body.sharedMesh, head.sharedMesh, figure.Key);
                CollectionAssert.AreEqual(body.bones, head.bones, figure.Key + ": one skeleton");
                Assert.AreSame(body.rootBone, head.rootBone, figure.Key);
                Assert.Greater(body.sharedMesh.vertexCount, 0, figure.Key);
                Assert.Greater(head.sharedMesh.vertexCount, 0, figure.Key);
                Assert.AreEqual(2, prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length, figure.Key + ": nothing but body and head");
            }
        }

        [Test]
        public void AnyHeadSitsOnTheNeckOfAnyBodyOfItsKindAndFollowsItsBones()
        {
            List<KeyValuePair<string, string>> figures = TwoPartFigures();
            int pairs = 0;
            foreach (var bodyFigure in figures)
            {
                GameObject bodyPrefab = Resources.Load<GameObject>("ARE/Visuals/" + bodyFigure.Key);
                // How the body's own head sits on it: the measure every other head is held against.
                var own = UnityEngine.Object.Instantiate(bodyPrefab);
                float ownGap;
                try { ownGap = NeckGap(Posed(FigureComposer.Part(own.transform, FigureComposer.HeadPart)), Posed(FigureComposer.Part(own.transform, FigureComposer.BodyPart))); }
                finally { UnityEngine.Object.DestroyImmediate(own); }
                foreach (var headFigure in figures)
                {
                    if (headFigure.Key == bodyFigure.Key || headFigure.Value != bodyFigure.Value) continue;
                    GameObject headPrefab = Resources.Load<GameObject>("ARE/Visuals/" + headFigure.Key);
                    SkinnedMeshRenderer donor = FigureComposer.Part(headPrefab.transform, FigureComposer.HeadPart);
                    Transform[] donorBones = donor.bones.ToArray();
                    var instance = UnityEngine.Object.Instantiate(bodyPrefab);
                    string pair = headFigure.Key + " on " + bodyFigure.Key;
                    try
                    {
                        Assert.IsTrue(FigureComposer.TryPutHead(instance.transform, headPrefab), pair);
                        SkinnedMeshRenderer head = FigureComposer.Part(instance.transform, FigureComposer.HeadPart), body = FigureComposer.Part(instance.transform, FigureComposer.BodyPart);
                        Assert.AreSame(donor.sharedMesh, head.sharedMesh, pair + ": the head keeps its mesh");
                        Assert.AreSame(donor.sharedMaterial, head.sharedMaterial, pair + ": the head keeps its colours");
                        Assert.AreSame(bodyPrefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(s => s.name == FigureComposer.BodyPart).sharedMesh, body.sharedMesh, pair + ": the body is untouched");
                        foreach (Transform bone in head.bones) Assert.IsTrue(bone != null && bone.IsChildOf(instance.transform), pair + ": every bone of the head is a bone of the body");
                        CollectionAssert.AreEqual(donorBones, donor.bones, pair + ": the figure the head comes from is left as it was");

                        Transform headBone = Bone(instance.transform, "head"), pelvis = Bone(instance.transform, "pelvis");
                        List<Vector3> posed = Posed(head);
                        float far = posed.Max(v => (v - headBone.position).magnitude);
                        Assert.Less(far, .5f, pair + ": the head stays at the head bone (farthest point " + far.ToString("F2") + " m)");
                        // A helmet's neck guard reaches down to the collar; nothing of a head belongs lower than the chest.
                        Assert.Greater(posed.Min(v => v.y), pelvis.position.y + .25f, pair + ": nothing of the head drops below the chest");
                        float gap = NeckGap(posed, Posed(body));
                        Assert.LessOrEqual(gap, ownGap + .02f, pair + ": the neck closes as well as with the body's own head (" + (gap * 1000f).ToString("F0") + " mm, own " + (ownGap * 1000f).ToString("F0") + " mm)");

                        // Turn the body's head: the head turns with it as one piece.
                        Vector3 before = headBone.InverseTransformPoint(posed.Aggregate(Vector3.zero, (a, v) => a + v) / posed.Count);
                        Vector3 centreBefore = posed.Aggregate(Vector3.zero, (a, v) => a + v) / posed.Count;
                        headBone.localRotation *= Quaternion.Euler(0f, 35f, 20f);
                        List<Vector3> turned = Posed(head);
                        Vector3 centre = turned.Aggregate(Vector3.zero, (a, v) => a + v) / turned.Count;
                        Assert.Greater((centre - centreBefore).magnitude, .005f, pair + ": the head moves with the body's head bone");
                        Assert.Less((headBone.InverseTransformPoint(centre) - before).magnitude, .02f, pair + ": the head turns as one piece with the bone");
                        pairs++;
                    }
                    finally { UnityEngine.Object.DestroyImmediate(instance); }
                }
            }
            Assert.Greater(pairs, 0, "At least one pair of figures of one kind was put together");
        }

        [Test]
        public void BonesAHeadBringsAlongHangUnderTheSameBoneOfTheNewBody()
        {
            List<KeyValuePair<string, string>> figures = TwoPartFigures();
            foreach (var bodyFigure in figures)
                foreach (var headFigure in figures)
                {
                    if (headFigure.Key == bodyFigure.Key || headFigure.Value != bodyFigure.Value) continue;
                    GameObject headPrefab = Resources.Load<GameObject>("ARE/Visuals/" + headFigure.Key);
                    var bodyPrefab = Resources.Load<GameObject>("ARE/Visuals/" + bodyFigure.Key);
                    var bodyNames = new HashSet<string>(bodyPrefab.GetComponentsInChildren<Transform>(true).Select(t => t.name));
                    Transform extra = FigureComposer.Part(headPrefab.transform, FigureComposer.HeadPart).bones.FirstOrDefault(b => !bodyNames.Contains(b.name));
                    if (extra == null) continue;
                    var instance = UnityEngine.Object.Instantiate(bodyPrefab);
                    try
                    {
                        Assert.IsTrue(FigureComposer.TryPutHead(instance.transform, headPrefab));
                        Transform copy = Bone(instance.transform, extra.name);
                        Assert.IsNotNull(copy, extra.name + " was brought along");
                        Assert.AreEqual(extra.parent.name, copy.parent.name, extra.name + " hangs under the same bone");
                        Assert.AreEqual(extra.localPosition.x, copy.localPosition.x, 1e-5f); Assert.AreEqual(extra.localPosition.y, copy.localPosition.y, 1e-5f);
                        Assert.IsNotNull(Bone(headPrefab.transform, extra.name), "the figure the head comes from keeps its bone");
                        return;
                    }
                    finally { UnityEngine.Object.DestroyImmediate(instance); }
                }
            Assert.Ignore("No head of the installed figures brings bones the other bodies lack");
        }

        [Test]
        public void AHeadThatCannotBePutOnLeavesTheBodyAsItWas()
        {
            List<KeyValuePair<string, string>> figures = TwoPartFigures();
            GameObject bodyPrefab = Resources.Load<GameObject>("ARE/Visuals/" + figures[0].Key);
            var instance = UnityEngine.Object.Instantiate(bodyPrefab);
            try
            {
                SkinnedMeshRenderer head = FigureComposer.Part(instance.transform, FigureComposer.HeadPart);
                Mesh mesh = head.sharedMesh; Material material = head.sharedMaterial; Transform[] bones = head.bones.ToArray();
                int transforms = instance.GetComponentsInChildren<Transform>(true).Length;
                var plain = new GameObject("NotAFigure");
                try
                {
                    Assert.IsFalse(FigureComposer.TryPutHead(instance.transform, null));
                    Assert.IsFalse(FigureComposer.TryPutHead(instance.transform, plain), "a head from something that is no figure in two parts");
                    Assert.IsFalse(FigureComposer.TryPutHead(plain.transform, bodyPrefab), "a body that is no figure in two parts");
                }
                finally { UnityEngine.Object.DestroyImmediate(plain); }
                Assert.AreSame(mesh, head.sharedMesh); Assert.AreSame(material, head.sharedMaterial);
                CollectionAssert.AreEqual(bones, head.bones);
                Assert.AreEqual(transforms, instance.GetComponentsInChildren<Transform>(true).Length, "nothing was added to the body");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
    }
}
