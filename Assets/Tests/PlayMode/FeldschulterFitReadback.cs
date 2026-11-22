using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    public sealed partial class EntryJourneyRuntimeTests
    {
        private static readonly HumanBodyBones[] ShoulderReferenceBones =
        {
            HumanBodyBones.Hips, HumanBodyBones.Chest, HumanBodyBones.Neck, HumanBodyBones.Head,
            HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm,
            HumanBodyBones.RightUpperArm
        };

        private static string Metres(Vector3 p) =>
            string.Format(CultureInfo.InvariantCulture, "{0:F5},{1:F5},{2:F5}", p.x, p.y, p.z);

        [UnityTest]
        public IEnumerator FieldHealerShoulderFitUsesThePurchasedHumanoid()
        {
            yield return null;
            _runtime.enabled = false;
            var root = Find("Wanderer").transform;
            var avatar = FittedAnimator(root);
            var upper = avatar.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var skin = root.GetComponentsInChildren<SkinnedMeshRenderer>().First(s =>
                s.sharedMesh && Array.IndexOf(s.bones, upper) >= 0);
            var report = new StringBuilder();
            report.AppendLine("Unity " + Application.unityVersion + " / " + SystemInfo.graphicsDeviceName);
            report.AppendLine("Avatar " + avatar.avatar.name + " / " + skin.sharedMesh.name + " / " + avatar.runtimeAnimatorController.name);
            report.AppendLine("hero scale " + Metres(root.lossyScale) + " skin scale " + Metres(skin.transform.lossyScale));
            foreach (var slot in ShoulderReferenceBones)
            {
                var bone = avatar.GetBoneTransform(slot);
                int i = Array.IndexOf(skin.bones, bone);
                Assert.IsNotNull(bone, slot.ToString());
                var bind = i < 0 ? bone.position :
                    skin.transform.TransformPoint(skin.sharedMesh.bindposes[i].inverse.MultiplyPoint3x4(Vector3.zero));
                report.AppendLine(slot + " " + bone.name + " index " + i + " pose " + Metres(root.InverseTransformPoint(bone.position)) +
                    " bind " + Metres(root.InverseTransformPoint(bind)) + " scale " + Metres(bone.lossyScale));
            }
            foreach(var clip in avatar.runtimeAnimatorController.animationClips.Distinct())
                report.AppendLine("clip " + clip.name + " length " + clip.length.ToString("F3",CultureInfo.InvariantCulture));
            string dir = Environment.GetEnvironmentVariable("ASHEN_SHOULDER_OUTPUT");
            if (!string.IsNullOrEmpty(dir)) File.WriteAllText(System.IO.Path.Combine(dir,"rig.txt"),report.ToString());
            Debug.Log(report.ToString());
            Assert.Greater(Vector3.Distance(upper.position,avatar.GetBoneTransform(HumanBodyBones.LeftLowerArm).position),.12f);
        }

        [Test]
        public void FieldHealerShoulderRequiresAnImportedThreeLevelModel()
        {
            var source = Resources.Load<GameObject>("Equipment/Feldschulter/Feldschulter");
            Assert.IsNotNull(source, "Das authorierte originale Schultermodell fehlt im Unity-Resource-Pfad.");
            var sizes = Enumerable.Range(0,3).Select(i => source.GetComponentsInChildren<MeshFilter>(true)
                .Single(m => m.name == "Feldschulter_LOD"+i).sharedMesh.GetIndexCount(0) / 3).ToArray();
            Assert.Greater(sizes[0],sizes[1]); Assert.Greater(sizes[1],sizes[2]); Assert.Greater(sizes[2],0);
        }
    }
}
