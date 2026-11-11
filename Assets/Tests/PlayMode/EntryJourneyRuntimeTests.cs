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
            typeof(ArenaRuntime).GetMethod("Restart", Flags).Invoke(_runtime, null);
            Assert.AreEqual(2, Game.Progression.EntryAct);
            Assert.AreEqual(0, Game.Monsters.Count);
            Assert.AreEqual(3, Game.Progression.Items.Count);
        }

        /// <summary>Search only the owned fixture scene, never another test's projection.</summary>
        private GameObject Find(string name)
        {
            foreach (var root in _scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            return null;
        }

        /// <summary>Optional real camera render for private visual inspection; never a gameplay input.</summary>
        private IEnumerator Capture(string name)
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
            var active = RenderTexture.active;
            var target = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                pixels.Apply();
                System.IO.Directory.CreateDirectory(directory);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, name), pixels.EncodeToPNG());
            }
            finally { camera.targetTexture = previous; RenderTexture.active = active; Object.Destroy(pixels); target.Release(); Object.Destroy(target); }
        }
    }
}
