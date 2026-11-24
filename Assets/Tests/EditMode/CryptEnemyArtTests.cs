using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AreArpg.EditMode.Tests
{
    /// <summary>
    /// The crypt's enemy figures built from the monster packs (owner task 2026-11-24) keep the contract the game
    /// relies on: passive prefabs, a controller with Idle, Move, Attack, Hit and Death, the float AttackSpeed as the
    /// attack's multiplier, the bool Dead, and an attack state whose own speed is the second of the clip at which the
    /// blow lands. The figures are private art: a kind without its pack is skipped, and without any the test is ignored.
    /// </summary>
    public sealed class CryptEnemyArtTests
    {
        private const string Visuals = "Assets/Resources/ARE/Visuals/";

        [Test]
        public void BuiltEnemyFiguresKeepTheClipContract()
        {
            // The builder lives in the editor scripts, which test assemblies reach by name only.
            var owns = System.Type.GetType("AreArpg.EditorTools.CryptEnemyArtBuilder, Assembly-CSharp-Editor", true)
                .GetMethod("Owns", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.IsFalse((bool)owns.Invoke(null, new object[] { "EntryHero" }), "Only enemy figures belong to the builder");
            int built = 0;
            for (int kind = 0; kind < 4; kind++)
            {
                string key = "EntryEnemy" + kind;
                if (!(bool)owns.Invoke(null, new object[] { key })) continue;
                built++;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Visuals + key + ".prefab");
                foreach (Component component in prefab.GetComponentsInChildren<Component>(true))
                    Assert.IsTrue(component is Transform || component is Renderer || component is MeshFilter || component is Animator, key + " is passive art: " + component.GetType().Name);
                Animator[] animators = prefab.GetComponentsInChildren<Animator>(true);
                Assert.AreEqual(1, animators.Length, key + " has one animator");
                Assert.IsFalse(animators[0].applyRootMotion, key + ": the Core owns the position");
                Assert.IsTrue(animators[0].avatar != null && animators[0].avatar.isValid, key + " has a valid avatar");
                var controller = (AnimatorController)animators[0].runtimeAnimatorController;
                var states = controller.layers[0].stateMachine.states.ToDictionary(child => child.state.name, child => child.state);
                foreach (string name in new[] { "Idle", "Move", "Attack", "Hit", "Death" })
                {
                    Assert.IsTrue(states.ContainsKey(name), key + " has the state " + name);
                    var clip = states[name].motion as AnimationClip;
                    Assert.IsNotNull(clip, key + "/" + name + " plays a clip");
                    Assert.Greater(clip.length, .3f, key + "/" + name + " is a moving clip");
                    Assert.AreEqual(name == "Idle" || name == "Move", clip.isLooping, key + "/" + name + ": only standing and walking loop");
                    Assert.IsEmpty(clip.events, key + "/" + name + " carries no vendor events");
                }
                Assert.AreEqual("Idle", controller.layers[0].stateMachine.defaultState.name);
                var parameters = controller.parameters.ToDictionary(p => p.name, p => p.type);
                Assert.AreEqual(AnimatorControllerParameterType.Float, parameters["MoveMagnitude"], key + ": locomotion follows MoveMagnitude");
                Assert.AreEqual(AnimatorControllerParameterType.Float, parameters["AttackSpeed"]);
                Assert.AreEqual(AnimatorControllerParameterType.Bool, parameters["Dead"]);
                AnimatorState attack = states["Attack"];
                Assert.IsTrue(attack.speedParameterActive && attack.speedParameter == "AttackSpeed", key + ": the game paces the attack");
                var swing = (AnimationClip)attack.motion;
                Assert.That(attack.speed, Is.InRange(.2f, swing.length - .2f), key + ": the blow lands inside the attack clip");
                Assert.IsTrue(controller.layers[0].stateMachine.anyStateTransitions.Any(t => t.destinationState == states["Death"] &&
                    t.conditions.Any(c => c.parameter == "Dead" && c.mode == AnimatorConditionMode.If)), key + ": Dead leads into Death from anywhere");
                Assert.IsTrue(states["Death"].transitions.All(t => t.conditions.Any(c => c.parameter == "Dead" && c.mode == AnimatorConditionMode.IfNot)),
                    key + ": only standing up again leaves Death");
                foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        Assert.IsTrue(material != null && material.shader.isSupported, key + ": the material on " + renderer.name + " must render");
                        // The body wears the muted texture baked for the game; what it holds (a weapon) wears a private worn copy of its
                        // own material, which may be a plain colour.
                        if (renderer is SkinnedMeshRenderer)
                            Assert.IsTrue(material.mainTexture != null && AssetDatabase.GetAssetPath(material.mainTexture).StartsWith(Visuals),
                                key + " wears the muted colour texture baked for the game");
                        else
                            StringAssert.StartsWith("Assets/PrivateArt/BozoCharacters/Props/", AssetDatabase.GetAssetPath(material), key + ": " + renderer.name + " wears a worn copy");
                    }
                foreach (MeshRenderer held in prefab.GetComponentsInChildren<MeshRenderer>(true))
                {
                    bool carried = false;
                    for (Transform node = held.transform; node != null && !carried; node = node.parent) carried = node.name.StartsWith("FigureProp_");
                    Assert.IsTrue(carried, key + ": a rigid mesh on an enemy is something it holds, hung on a bone: " + held.name);
                }
                int triangles = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Sum(skin => skin.sharedMesh.triangles.Length / 3);
                Assert.LessOrEqual(triangles, 32000, key + " stays inside the figure budget of the tablet");
            }
            if (built == 0) Assert.Ignore("Requires the private enemy figures built from the monster packs");
        }
    }
}
