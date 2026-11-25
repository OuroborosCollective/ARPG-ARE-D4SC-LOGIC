using System.Linq;
using System.Reflection;
using AreArpg.Core;
using AreArpg.UnityClient;
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
            for (int kind = 0; kind < 3; kind++)
            {
                string key = "EntryEnemy" + kind;
                if (!(bool)owns.Invoke(null, new object[] { key })) continue;
                built++;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Visuals + key + ".prefab");
                foreach (Component component in prefab.GetComponentsInChildren<Component>(true))
                    Assert.IsTrue(component is Transform || component is MeshFilter || component is Renderer || component is LODGroup || component is Animator, key + " is passive art: " + (component == null ? "missing component" : component.GetType().Name));
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
                        Assert.IsTrue(material != null && material.mainTexture != null, key + " has a textured material on " + renderer.name);
                        Assert.IsTrue(AssetDatabase.GetAssetPath(material.mainTexture).StartsWith(Visuals), key + " wears the muted colour texture baked for the game");
                    }
                int triangles = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Sum(skin => skin.sharedMesh.triangles.Length / 3);
                Assert.LessOrEqual(triangles, 32000, key + " stays inside the figure budget of the tablet");
            }
            if (built == 0) Assert.Ignore("Requires the private enemy figures built from the monster packs");
        }
    }

    public sealed class EnemyClipContractRegressionTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject host, visual, hero;
        private ArenaRuntime runtime;
        private string controllerPath;

        [SetUp]
        public void CreateProjection()
        {
            host = new GameObject("Enemy projection under test");
            host.SetActive(false);
            runtime = host.AddComponent<ArenaRuntime>();
            visual = new GameObject("Test enemy");
            hero = new GameObject("Test hero");
            typeof(ArenaRuntime).GetField("_hero", Instance).SetValue(runtime, hero.transform);
        }

        [TearDown]
        public void RemoveProjection()
        {
            Object.DestroyImmediate(visual);
            Object.DestroyImmediate(hero);
            Object.DestroyImmediate(host);
            if (controllerPath != null) AssetDatabase.DeleteAsset(controllerPath);
        }

        private Animator BindFigure(bool complete)
        {
            controllerPath = AssetDatabase.GenerateUniqueAssetPath("Assets/EnemyClipContractTest.controller");
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            if (complete)
            {
                controller.AddParameter("AttackSpeed", AnimatorControllerParameterType.Float);
                machine.AddState("Attack");
                machine.AddState("Hit");
            }
            var idle = machine.AddState("Idle");
            machine.defaultState = idle;
            var death = machine.AddState("Death");
            var fall = new AnimationClip { name = "Fall" };
            fall.SetCurve("Animated", typeof(Transform), "localScale.x", AnimationCurve.Linear(0f, 1f, 4f, .9f));
            AssetDatabase.AddObjectToAsset(fall, controller);
            death.motion = fall;
            var transition = machine.AddAnyStateTransition(death);
            transition.hasExitTime = false;
            transition.duration = 0;
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
            new GameObject("Animated").transform.SetParent(visual.transform, false);
            var animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            animator.Update(0f);
            return animator;
        }

        [Test]
        public void IncompleteEnemyControllerKeepsTheFallbackDeath()
        {
            BindFigure(false);
            var usesClip = typeof(ArenaRuntime).GetMethod("EnemyDiesWithClip", Instance);
            Assert.IsFalse((bool)usesClip.Invoke(runtime, new object[] { 1, visual.transform }),
                "A controller with only Death and Dead must use the existing topple.");
        }

        [Test]
        public void DeathStateKeepsItsActualFourSecondMotionWhenItIsNamedFall()
        {
            var animator = BindFigure(true);
            var present = typeof(ArenaRuntime).GetMethod("PresentEnemyDeathClip", Instance);
            var args = new object[] { 1, visual.transform };
            Assert.IsTrue((bool)present.Invoke(runtime, args));
            animator.Update(.1f);
            animator.Update(.1f);
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Death"));
            Assert.AreEqual(4f, animator.GetCurrentAnimatorStateInfo(0).length, .01f);
            var starts = (System.Collections.Generic.Dictionary<int, float>)typeof(ArenaRuntime)
                .GetField("_deathTime", Instance).GetValue(runtime);
            starts[1] = Time.time - 3.5f;
            Assert.IsTrue((bool)present.Invoke(runtime, args), "The falling figure must still be visible.");
            Assert.IsTrue(runtime.IsPresentingDeath(1));
            Assert.AreEqual(0f, visual.transform.position.y, .0001f, "The four-second fall has not started sinking.");
            starts[1] = Time.time - 7f;
            Assert.IsFalse((bool)present.Invoke(runtime, args), "The completed fall and sink must still end.");
        }

        [Test]
        public void EnemyFacesTheCompanionWhoActuallyReceivesItsImmediateStrike()
        {
            BindFigure(true);
            var game = new ArenaSimulation(136);
            typeof(ArenaRuntime).GetField("_game", Instance).SetValue(runtime, game);
            var companion = game.Progression.Companion;
            typeof(CompanionState).GetProperty("Status").SetValue(companion, CompanionStatus.Waiting);
            typeof(CompanionState).GetProperty("Health").SetValue(companion, CompanionState.MaximumHealth);
            typeof(CompanionState).GetProperty("Area").SetValue(companion, game.CurrentAreaId);
            typeof(CompanionState).GetProperty("Xmm").SetValue(companion, game.PlayerXmm + 2400);
            typeof(CompanionState).GetProperty("Zmm").SetValue(companion, game.PlayerZmm);
            var enemy = game.Monsters.First(m => m.Kind == 0);
            foreach (var other in game.Monsters.Where(m => m != enemy)) other.Health = 0;
            enemy.Xmm = enemy.PreviousXmm = game.PlayerXmm + 1400;
            enemy.Zmm = enemy.PreviousZmm = game.PlayerZmm;
            enemy.AttackCooldownTicks = 0;
            hero.transform.position = new Vector3(game.PlayerXmm * .001f, 0, game.PlayerZmm * .001f);
            visual.transform.position = new Vector3(enemy.Xmm * .001f, 0, enemy.Zmm * .001f);
            var update = typeof(ArenaRuntime).GetMethod("UpdateEnemyClips", Instance);
            var args = new object[] { enemy, visual.transform };
            update.Invoke(runtime, args);
            int heroHealth = game.PlayerHealth;
            for (int tick = 0; tick < 8 && companion.Health == CompanionState.MaximumHealth; tick++) game.Step();
            Assert.Less(companion.Health, CompanionState.MaximumHealth, "The real combat tick struck the hunter.");
            Assert.AreEqual(heroHealth, game.PlayerHealth);
            Assert.Greater(enemy.AttackCooldownTicks, 0);
            update.Invoke(runtime, args);
            Assert.Greater(Vector3.Dot(visual.transform.forward, Vector3.right), .99f,
                "The companion is on the opposite side of the enemy from the hero.");
        }

        [Test]
        public void DisabledMixamoTakeIsReenabledBeforeResolvingItsAnimation()
        {
            const string source = "Assets/Scary_Evil/Prefab/evil@Standing Melee Attack Backhand.fbx";
            if (AssetImporter.GetAtPath(source) == null) Assert.Ignore("Requires the licensed demon attack FBX.");
            string copy = AssetDatabase.GenerateUniqueAssetPath("Assets/EnemyImportTakeTest.fbx");
            Assert.IsTrue(AssetDatabase.CopyAsset(source, copy));
            try
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(copy);
                importer.importAnimation = false;
                importer.SaveAndReimport();
                var configure = System.Type.GetType("AreArpg.EditorTools.CryptEnemyArtBuilder, Assembly-CSharp-Editor", true)
                    .GetMethod("Humanoid", BindingFlags.Static | BindingFlags.NonPublic);
                configure.Invoke(null, new object[] { copy, "mixamo.com", false });
                Assert.IsTrue(((ModelImporter)AssetImporter.GetAtPath(copy)).importAnimation);
                var clip = AssetDatabase.LoadAllAssetsAtPath(copy).OfType<AnimationClip>().Single(c => c.name == "mixamo.com");
                Assert.IsTrue(clip.humanMotion);
                Assert.Greater(clip.length, 0f);
                configure.Invoke(null, new object[] { copy, null, false });
                Assert.IsFalse(((ModelImporter)AssetImporter.GetAtPath(copy)).importAnimation);
            }
            finally { AssetDatabase.DeleteAsset(copy); }
        }
    }

}
