using System.Collections;
using System.Linq;
using System.Reflection;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    public sealed partial class EntryJourneyRuntimeTests
    {
        [UnityTest]
        public IEnumerator ThreeOrdersBindToTheAnimatedHero()
        {
            if (ThreeOrderSet.All.SelectMany(set => set.Pieces)
                .All(id => Resources.Load<GameObject>("ARE/Visuals/Werkarsenal/" + id) == null))
                Assert.Ignore("Requires the private Three Orders armour overlay");
            var hero = Find("Wanderer");
            Assert.IsNotNull(hero);
            var animator = hero.GetComponentsInChildren<Animator>(true).First(a => a.isHuman && a.avatar && a.avatar.isValid);
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var claim = typeof(WeaponEquipment).GetMethod("ClaimRegionalRelic", BindingFlags.Instance | BindingFlags.NonPublic);
            var toggle = typeof(ArenaRuntime).GetMethod("ToggleRegionalRelic", Flags);
            foreach (var set in ThreeOrderSet.All)
            {
                // Arrange ownership through the core. Drops, eligibility and receipt ordering are exercised by CoreVerification.
                foreach (var id in set.Pieces)
                {
                    Assert.AreEqual(true, claim.Invoke(Game.Progression.Equipment, new object[] { Game.Progression, id }));
                    toggle.Invoke(_runtime, new object[] { id });
                }
                yield return null;
                foreach (var id in set.Pieces)
                {
                    var instance = Find("Equipped_" + id);
                    Assert.IsNotNull(instance, id.ToString());
                    var skin = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                    Assert.AreSame(hips, skin.rootBone, id.ToString());
                    Assert.IsTrue(skin.bones.All(b => b && b.IsChildOf(animator.transform)), id + " uses hero bones");
                }
                var handSkin = Find("Equipped_" + set.Pieces[(int)ArmorSlot.Hands]).GetComponentInChildren<SkinnedMeshRenderer>();
                var joint = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                var rotation = joint.localRotation;
                var before = new Mesh(); var after = new Mesh();
                try
                {
                    handSkin.BakeMesh(before);
                    joint.localRotation = rotation * Quaternion.Euler(0f, 0f, 24f);
                    handSkin.BakeMesh(after);
                    var rest = before.vertices; var moved = after.vertices;
                    Assert.IsTrue(Enumerable.Range(0, rest.Length).Any(i => (rest[i] - moved[i]).sqrMagnitude > .000001f), set.Name + " deforms with forearm");
                }
                finally
                {
                    joint.localRotation = rotation;
                    Object.Destroy(before); Object.Destroy(after);
                }
                foreach (var id in set.Pieces) toggle.Invoke(_runtime, new object[] { id });
                yield return null;
                Assert.IsTrue(set.Pieces.All(id => !Find("Equipped_" + id)), set.Name + " removed");
            }
        }
    }
}
