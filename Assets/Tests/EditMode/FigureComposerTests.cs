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

        /// <summary>
        /// Game visual key and base preset of every model figure the recipes build in two parts. A colour variant
        /// draws the body and head meshes of its model, so the models stand for every figure of the pool.
        /// </summary>
        static List<KeyValuePair<string, string>> TwoPartFigures()
        {
            Type recipes = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) recipes = recipes ?? assembly.GetType("AreArpg.EditorTools.BozoFigureRecipes");
            Assert.IsNotNull(recipes, "BozoFigureRecipes is missing");
            var result = new List<KeyValuePair<string, string>>();
            foreach (object figure in (Array)recipes.GetMethod("Load", BindingFlags.Public | BindingFlags.Static).Invoke(null, null))
            {
                Type type = figure.GetType();
                if (!(bool)type.GetField("parts").GetValue(figure) || !string.IsNullOrEmpty((string)type.GetField("sameShapeAs").GetValue(figure))) continue;
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

        /// <summary>
        /// A hole at the neck opens where the head ends above the body. Around the neck (within nine centimetres of the
        /// neck bone, seen from above) this is how far the lowest point of the head lies above the highest point of the
        /// body below the head bone: negative where the neck reaches down into the collar, as it should.
        /// </summary>
        static float NeckOpening(List<Vector3> head, List<Vector3> body, Transform neck, Transform headBone)
        {
            Func<Vector3, bool> nearNeck = v => new Vector2(v.x - neck.position.x, v.z - neck.position.z).magnitude < .09f;
            float headLow = head.Where(nearNeck).Min(v => v.y);
            float bodyTop = body.Where(v => nearNeck(v) && v.y < headBone.position.y).Max(v => v.y);
            return headLow - bodyTop;
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
            // How each figure's own head sits on its own body: the measure every pair is held against. A long beard
            // reaches far below the head bone on its own figure, and may do so on another body too.
            var ownOpenings = new Dictionary<string, float>();
            var ownReach = new Dictionary<string, float>();
            var ownDrop = new Dictionary<string, float>();
            foreach (var figure in figures)
            {
                var own = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("ARE/Visuals/" + figure.Key));
                try
                {
                    Transform ownHeadBone = Bone(own.transform, "head");
                    List<Vector3> ownHead = Posed(FigureComposer.Part(own.transform, FigureComposer.HeadPart));
                    ownOpenings[figure.Key] = NeckOpening(ownHead, Posed(FigureComposer.Part(own.transform, FigureComposer.BodyPart)), Bone(own.transform, "neck_01"), ownHeadBone);
                    ownReach[figure.Key] = ownHead.Max(v => (v - ownHeadBone.position).magnitude);
                    ownDrop[figure.Key] = ownHeadBone.position.y - ownHead.Min(v => v.y);
                }
                finally { UnityEngine.Object.DestroyImmediate(own); }
            }
            int pairs = 0;
            foreach (var bodyFigure in figures)
            {
                GameObject bodyPrefab = Resources.Load<GameObject>("ARE/Visuals/" + bodyFigure.Key);
                float ownOpening = ownOpenings[bodyFigure.Key];
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
                        // Bodies are up to a sixth taller than the figure a head comes from, and the head grows with them.
                        float far = posed.Max(v => (v - headBone.position).magnitude);
                        float reach = Mathf.Max(.5f, ownReach[headFigure.Key] * 1.15f + .03f);
                        Assert.Less(far, reach, pair + ": the head stays at the head bone (farthest point " + far.ToString("F2") + " m, allowed " + reach.ToString("F2") + " m)");
                        // A helmet's neck guard reaches down to the collar; nothing of a head belongs lower than the chest,
                        // unless it hangs that low on its own figure too (a long beard).
                        float lowest = Mathf.Min(pelvis.position.y + .25f, headBone.position.y - ownDrop[headFigure.Key] * 1.15f - .03f);
                        Assert.Greater(posed.Min(v => v.y), lowest, pair + ": nothing of the head drops below the chest (or below its own beard)");
                        float opening = NeckOpening(posed, Posed(body), Bone(instance.transform, "neck_01"), headBone);
                        Assert.LessOrEqual(opening, Mathf.Max(ownOpening, 0f) + .015f, pair + ": no hole opens at the neck (head ends " + (opening * 1000f).ToString("F0") +
                            " mm above the body, its own head " + (ownOpening * 1000f).ToString("F0") + " mm)");

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
