using System.Collections;
using System.Reflection;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    /// <summary>Real Unity adapter test; arranged combat fixtures are not an unattended player playthrough.</summary>
    public sealed class EntryJourneyRuntimeTests
    {
        private readonly string[] _keys = { "ARE_ARPG_entry_v1", "ARE_ARPG_entry_v1_backup" };
        private readonly string[] _saved = new string[2];
        private readonly bool[] _had = new bool[2];
        private Scene _previous, _scene;
        private ArenaRuntime _runtime;
        private GameObject[] _previousRoots;
        private bool[] _previousActive;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private ArenaSimulation Game { get { return (ArenaSimulation)typeof(ArenaRuntime).GetField("_game", Flags).GetValue(_runtime); } }

        /// <summary>Use a separate entry save slot and restore all its original values after the test.</summary>
        [UnitySetUp]
        public IEnumerator Setup()
        {
            for (int i = 0; i < _keys.Length; i++) { _had[i] = PlayerPrefs.HasKey(_keys[i]); _saved[i] = PlayerPrefs.GetString(_keys[i], ""); PlayerPrefs.DeleteKey(_keys[i]); }
            _previous = SceneManager.GetActiveScene();
            _previousRoots = _previous.GetRootGameObjects();
            _previousActive = new bool[_previousRoots.Length];
            for (int i = 0; i < _previousRoots.Length; i++)
            {
                _previousActive[i] = _previousRoots[i].activeSelf;
                var root = _previousRoots[i];
                // Never disable the test runner/coroutine host. Only suspend the previous game projection.
                if (root.GetComponent<ArenaRuntime>() != null ||
                    root.GetComponentsInChildren<Renderer>(true).Length > 0 ||
                    root.GetComponentsInChildren<Camera>(true).Length > 0 ||
                    root.GetComponentsInChildren<Light>(true).Length > 0)
                    root.SetActive(false);
            }
            _scene = SceneManager.CreateScene("AshenEntry");
            SceneManager.SetActiveScene(_scene);
            _runtime = new GameObject("Entry_Runtime_Check").AddComponent<ArenaRuntime>();
            yield return null;
        }

        /// <summary>Unload test projections and restore the two entry keys without touching legacy profiles.</summary>
        [UnityTearDown]
        public IEnumerator Restore()
        {
            if (_previous.IsValid() && _previous.isLoaded) SceneManager.SetActiveScene(_previous);
            if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
            for (int i = 0; i < _previousRoots.Length; i++)
                if (_previousRoots[i] != null) _previousRoots[i].SetActive(_previousActive[i]);
            for (int i = 0; i < _keys.Length; i++)
                if (_had[i]) PlayerPrefs.SetString(_keys[i], _saved[i]); else PlayerPrefs.DeleteKey(_keys[i]);
            PlayerPrefs.Save();
        }

        /// <summary>Travel through canonical kill/pickup/passage, real PlayerPrefs and private village projection.</summary>
        [UnityTest]
        public IEnumerator CryptLeadsToVillageAndResumesItsCheckpoint()
        {
            Assert.AreEqual(1, Game.Progression.EntryAct);
            Assert.AreEqual(1, Object.FindObjectsByType<ArenaRuntime>(FindObjectsSortMode.None).Length,
                "The fixture must not run or render another world's authority");
            Assert.AreEqual(0, Game.Npcs.Count);
            Assert.IsNull(Find("Aschengrenze_Supply_Hall"), "Village must not appear inside act-one crypt");
            yield return Capture("act1-crypt.png");
            _runtime.enabled = false;
            // Test-only arrangement; each kill/drop/pickup still uses the actual Core implementation.
            foreach (var enemy in Game.Monsters) { enemy.Xmm = 1000; enemy.Zmm = 0; enemy.Health = 1; }
            Assert.AreEqual(3, Game.ActivateSkill(3).Kills);
            for (int i = 0; i < 3; i++) Assert.IsTrue(Game.TryCollectLoot());
            Game.SetMoveInput(0, 10000);
            for (int tick = 0; tick < 50 && !Game.AtWorldPassage; tick++) Game.Step();
            Assert.IsTrue(Game.AtWorldPassage);
            typeof(ArenaRuntime).GetMethod("AdvanceCrypt", Flags).Invoke(_runtime, null);
            Assert.AreEqual(2, Game.Progression.EntryAct);
            Assert.AreEqual(0, Game.Monsters.Count);
            Assert.AreEqual(3, Game.Npcs.Count);
            Assert.IsTrue(CharacterProgression.TryLoad(PlayerPrefs.GetString(_keys[0]), out var loaded));
            Assert.AreEqual(2, loaded.EntryAct);
            Assert.AreEqual(3, loaded.Items.Count);
            yield return null;
            Assert.IsNotNull(Find("Aschengrenze_Supply_Hall"));
            Assert.IsNotNull(Find(AschengrenzeArt.BackdropLabel));
            _runtime.enabled = true;
            yield return new WaitForSeconds(0.8f);
            yield return Capture("act2-arrival.png");
            // Real input along the approach; no player teleport is used for the village view.
            _runtime.enabled = false;
            Game.SetMoveInput(0, 10000);
            for (int i = 0; i < 28; i++) Game.Step();
            Game.SetMoveInput(-10000, 0);
            for (int i = 0; i < 10; i++) Game.Step();
            Game.SetMoveInput(0, 0);
            _runtime.enabled = true;
            yield return new WaitForSeconds(0.8f);
            yield return Capture("act2-supply-square.png");
            _runtime.enabled = false;
            var mara = Game.Npcs.Find(n => n.Id == SocialLedger.MaraNpcId);
            for (int tick = 0; tick < 60; tick++)
            {
                long dx = mara.Xmm - Game.PlayerXmm, dz = mara.Zmm - Game.PlayerZmm;
                if (dx * dx + dz * dz <= 1800L * 1800L) break;
                Game.SetMoveInput(System.Math.Sign(dx) * 10000, System.Math.Sign(dz) * 10000);
                Game.Step();
            }
            Game.SetMoveInput(0, 0);
            int stock = Game.Progression.Social.CampProvisions;
            var provisions = Game.Progression.Social.Provisions;
            typeof(ArenaRuntime).GetField("_pendingTakeRation", Flags).SetValue(_runtime, true);
            _runtime.enabled = true;
            for (int frame = 0; frame < 120 && Game.Progression.Social.CampProvisions == stock; frame++)
                yield return null;
            _runtime.enabled = false;
            Assert.AreEqual(stock - 1, Game.Progression.Social.CampProvisions,
                "The real Unity action queue must execute the Core ration transaction");
            Assert.AreEqual(provisions + 1, Game.Progression.Social.Provisions);
            Assert.AreEqual(System.Numerics.BigInteger.One, mara.Memory.HostileActions);
            Assert.IsTrue(CharacterProgression.TryLoad(PlayerPrefs.GetString(_keys[0]), out var afterTheft));
            Assert.AreEqual(stock - 1, afterTheft.Social.CampProvisions);
            Assert.AreEqual(provisions + 1, afterTheft.Social.Provisions);
            Assert.AreEqual(System.Numerics.BigInteger.One, afterTheft.Social.GetMemory(1).HostileActions);
            typeof(ArenaRuntime).GetMethod("Restart", Flags).Invoke(_runtime, null);
            Assert.AreEqual(2, Game.Progression.EntryAct);
            Assert.AreEqual(0, Game.Monsters.Count);
            Assert.AreEqual(3, Game.Progression.Items.Count);
            Assert.AreEqual(stock - 1, Game.Progression.Social.CampProvisions);
            Assert.AreEqual(System.Numerics.BigInteger.One, Game.Progression.Social.GetMemory(1).HostileActions);
        }

        /// <summary>Native private rig consumes only accepted melee results; posing cannot change Core hashes.</summary>
        [UnityTest]
        public IEnumerator MeleeAnimationRequiresAnAcceptedCoreStrike()
        {
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR")))
                Assert.Ignore("Requires the reviewed private character and animation overlay");
            _runtime.enabled = false;
            var hero = Find("Wanderer");
            var animator = hero.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator);
            Assert.IsTrue(animator.HasState(0, Animator.StringToHash("Melee")), "Reviewed melee state is missing");
            var queue = (System.Collections.Generic.Queue<int>)typeof(ArenaRuntime).GetField("_pendingSkills", Flags).GetValue(_runtime);
            foreach (var enemy in Game.Monsters) { enemy.Xmm = 20000; enemy.Zmm = 20000; }
            queue.Enqueue(0);
            TickOnce();
            animator.Update(.05f);
            Assert.IsFalse(animator.GetCurrentAnimatorStateInfo(0).IsName("Melee"), "Rejected out-of-range attacks must not animate");
            var target = Game.Monsters[0]; target.Xmm = Game.PlayerXmm + 1000; target.Zmm = Game.PlayerZmm; target.Health = 1000;
            queue.Enqueue(0);
            TickOnce();
            Assert.Less(target.Health, 1000, "A real Core hit must precede the visual");
            ulong hash = Game.ComputeWorldHash();
            // The first evaluation consumes the trigger; a second advances the fixed-duration blend.
            animator.Update(0f);
            animator.Update(.15f);
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Melee"),
                "Accepted Core hit must enter Melee after its 40 ms transition");
            Assert.IsFalse(animator.applyRootMotion);
            var weapon = Find("Wanderer's sword");
            Assert.IsTrue(weapon.transform.IsChildOf(animator.GetBoneTransform(HumanBodyBones.RightHand)), "Weapon must follow the animated hand");
            Assert.IsNotNull(Find("Art_EntryWeapon"), "Private melee acceptance requires the fitted weapon mesh");
            var position = hero.transform.position;
            animator.speed = 0f;
            yield return Capture("act1-melee-strike.png");
            yield return Capture("hero-strike-detail.png", true);
            animator.speed = 1f;
            animator.Update(2f); animator.Update(.2f);
            Assert.IsFalse(animator.GetCurrentAnimatorStateInfo(0).IsName("Melee"), "Melee must return to locomotion");
            Assert.AreEqual(position, hero.transform.position);
            Assert.AreEqual(hash, Game.ComputeWorldHash(), "Animation may not write canonical state");
            yield return Capture("act1-melee-ready.png");
            yield return Capture("hero-ready-detail.png", true);
        }

        /// <summary>Exercise the real input queue on a scheduled tick without wall-clock waiting.</summary>
        private void TickOnce()
        {
            typeof(ArenaRuntime).GetField("_accumulator", Flags).SetValue(_runtime, .1f);
            typeof(ArenaRuntime).GetMethod("Update", Flags).Invoke(_runtime, null);
        }

        /// <summary>Search only the owned fixture scene, never another test's projection.</summary>
        [UnityTest]
        public IEnumerator EnemyFacesItsAcceptedCoreDisplacement()
        {
            _runtime.enabled = false;
            var enemy = Game.Monsters[0];
            enemy.PreviousXmm = 2000; enemy.Xmm = 1000;
            enemy.PreviousZmm = enemy.Zmm = 0;
            typeof(ArenaRuntime).GetMethod("UpdatePresentation", Flags).Invoke(_runtime, new object[] { 1f });
            Assert.Greater(Vector3.Dot(Find("Enemy_" + enemy.Id).transform.forward, Vector3.left), .99f);
            yield return null;
        }

        /// <summary>Development geometry must expose exactly the Core footprints, even without private world art.</summary>
        [UnityTest]
        public IEnumerator DevelopmentVillageShowsEveryCanonicalBuilding()
        {
            _runtime.enabled = false;
            var method = typeof(ArenaRuntime).GetMethod("BuildEntryVillageFallback", Flags);
            Assert.IsNotNull(method, "Entry needs its own geometry fallback instead of legacy crypt pillars");
            method.Invoke(_runtime, null);
            for (int i = 0; i < EntryWorldLayout.BuildingCount; i++)
            {
                var b = EntryWorldLayout.Building(i);
                var visual = Find("Entry_Footprint_" + b.Id);
                Assert.IsNotNull(visual);
                var bounds = visual.GetComponent<Renderer>().bounds;
                Assert.AreEqual(b.X / 1000f, bounds.center.x, .001f);
                Assert.AreEqual(b.Z / 1000f, bounds.center.z, .001f);
                Assert.AreEqual(b.HalfWidth * 2 / 1000f, bounds.size.x, .001f);
                Assert.AreEqual(b.HalfDepth * 2 / 1000f, bounds.size.z, .001f);
            }
            yield return null;
        }

        /// <summary>A missing cosmetic ration cannot hide real stock from the player.</summary>
        [UnityTest]
        public IEnumerator MissingRationAssetStillShowsStock()
        {
            _runtime.enabled = false;
            var method = typeof(ArenaRuntime).GetMethod("CreateEntryRation", Flags);
            Assert.IsNotNull(method);
            var ration = (Transform)method.Invoke(_runtime, new object[] { 0, "DefinitelyMissingEntryRation" });
            Assert.IsTrue(ration.GetComponent<Renderer>().enabled);
            Assert.IsTrue(ration.gameObject.activeSelf);
            yield return null;
        }

#if UNITY_EDITOR
        /// <summary>Missing private ground input must fail before the material is touched.</summary>
        [UnityTest]
        public IEnumerator MissingGroundTextureRejectsTheArtBuild()
        {
            _runtime.enabled = false;
            System.Type builder = null;
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                if ((builder = assembly.GetType("AreArpg.EditorTools.AreVillageArtBuilder")) != null) break;
            Assert.IsNotNull(builder);
            var inputs = (System.Collections.Generic.Dictionary<string, string>)builder.GetField("Inputs", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var saved = new System.Collections.Generic.Dictionary<string, string>(inputs);
            var root = new GameObject("MissingGroundFixture");
            try
            {
                inputs["ground.base"] = "Assets/PrivateArt/DefinitelyMissingGround.png";
                inputs["ground.normal"] = "Assets/PrivateArt/DefinitelyMissingNormal.png";
                var error = Assert.Throws<TargetInvocationException>(() => builder.GetMethod("Ground", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { root.transform }));
                Assert.IsInstanceOf<System.InvalidOperationException>(error.InnerException);
                Assert.AreEqual(0, root.transform.childCount);
            }
            finally { inputs.Clear(); foreach (var pair in saved) inputs.Add(pair.Key, pair.Value); Object.Destroy(root); }
            yield return null;
        }
#endif

        /// <summary>Search only the owned fixture scene, never another test's projection.</summary>
        private GameObject Find(string name)
        {
            foreach (var root in _scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            return null;
        }

        /// <summary>Optional real camera render for private visual inspection; never a gameplay input.</summary>
        private IEnumerator Capture(string name, bool heroDetail = false)
        {
            string directory = System.Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR");
            if (string.IsNullOrEmpty(directory)) yield break;
            foreach (string actorName in Game.IsVillage ? new[] { "Wanderer", "ARE_NPC_0", "ARE_NPC_1", "ARE_NPC_2" } : new[] { "Wanderer" })
            {
                var actor = Find(actorName);
                Assert.IsNotNull(actor, "Private visual acceptance requires the actual actor");
                var skins = actor.GetComponentsInChildren<SkinnedMeshRenderer>();
                Assert.IsNotEmpty(skins, "A primitive cannot satisfy actor art acceptance");
                foreach (var skin in skins)
                    foreach (var material in skin.sharedMaterials)
                        Assert.IsTrue(material != null && material.mainTexture != null, actorName + " has an untextured character material");
                foreach (var animator in actor.GetComponentsInChildren<Animator>())
                    Assert.IsNotNull(animator.runtimeAnimatorController, actorName + " is missing its idle/movement controller");
            }
            yield return null;
            var camera = (Camera)typeof(ArenaRuntime).GetField("_camera", Flags).GetValue(_runtime);
            var previous = camera.targetTexture;
            Vector3 previousPosition = camera.transform.position;
            Quaternion previousRotation = camera.transform.rotation;
            var active = RenderTexture.active;
            var target = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                if (heroDetail)
                {
                    Vector3 focus = Find("Wanderer").transform.position;
                    focus.y = 1f;
                    camera.transform.position = focus + new Vector3(3f, 1.2f, 4f);
                    camera.transform.LookAt(focus);
                }
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                pixels.Apply();
                System.IO.Directory.CreateDirectory(directory);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, name), pixels.EncodeToPNG());
            }
            finally
            {
                camera.transform.SetPositionAndRotation(previousPosition, previousRotation);
                camera.targetTexture = previous; RenderTexture.active = active;
                Object.Destroy(pixels); target.Release(); Object.Destroy(target);
            }
        }
    }
}
