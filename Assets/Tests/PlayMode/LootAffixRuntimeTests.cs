using System.Collections;
using System.Numerics;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    /// <summary>Checks the real chapter-one Unity projection independently of existing gameplay saves.</summary>
    public sealed class LootAffixRuntimeTests
    {
        private const string PrimaryKey = "ARE_ARPG_character_ac1";
        private const string BackupKey = "ARE_ARPG_character_ac1_backup";
        private string _originalPrimary, _originalBackup;
        private bool _hadPrimary, _hadBackup;
        private bool _snapshotCaptured;
        private Scene _before, _isolated;

        /// <summary>Snapshot both persistent keys and create a completely separate temporary Unity scene.</summary>
        [UnitySetUp]
        public IEnumerator PrepareIndependentWorld()
        {
            _hadPrimary = PlayerPrefs.HasKey(PrimaryKey);
            _hadBackup = PlayerPrefs.HasKey(BackupKey);
            _originalPrimary = PlayerPrefs.GetString(PrimaryKey, "");
            _originalBackup = PlayerPrefs.GetString(BackupKey, "");
            _snapshotCaptured = true;
            _before = SceneManager.GetActiveScene();
            // Ignore ALL existing saves only within the test process. No global PlayerPrefs.DeleteAll().
            PlayerPrefs.DeleteKey(PrimaryKey);
            PlayerPrefs.DeleteKey(BackupKey);
            // Do not PlayerPrefs.Save() a temporary empty profile.
            _isolated = SceneManager.CreateScene("Loot_Affix_Isolated_Check");
            Assert.IsTrue(SceneManager.SetActiveScene(_isolated));
            new GameObject("Loot_Affix_Runtime").AddComponent<ArenaRuntime>();
            yield return null;
        }

        /// <summary>Restore both keys byte-for-byte at the API level and unload the isolated scene.</summary>
        [UnityTearDown]
        public IEnumerator RestoreOriginalWorld()
        {
            if (!_snapshotCaptured) yield break;
            if (_before.IsValid() && _before.isLoaded)
                SceneManager.SetActiveScene(_before);
            if (_isolated.IsValid() && _isolated.isLoaded)
            {
                AsyncOperation unloading = SceneManager.UnloadSceneAsync(_isolated);
                if (unloading != null) yield return unloading;
            }
            if (_hadPrimary) PlayerPrefs.SetString(PrimaryKey, _originalPrimary);
            else PlayerPrefs.DeleteKey(PrimaryKey);
            if (_hadBackup) PlayerPrefs.SetString(BackupKey, _originalBackup);
            else PlayerPrefs.DeleteKey(BackupKey);
            PlayerPrefs.Save();
            Assert.AreEqual(_hadPrimary, PlayerPrefs.HasKey(PrimaryKey));
            Assert.AreEqual(_hadBackup, PlayerPrefs.HasKey(BackupKey));
            Assert.AreEqual(_originalPrimary, PlayerPrefs.GetString(PrimaryKey, ""));
            Assert.AreEqual(_originalBackup, PlayerPrefs.GetString(BackupKey, ""));
        }

        /// <summary>Drive the real client input queue, projection and PlayerPrefs save adapter.</summary>
        [UnityTest]
        public IEnumerator CanonicalKillProjectsAndPersistsVersionedLoot()
        {
            ArenaRuntime runtime = null;
            foreach (var root in _isolated.GetRootGameObjects())
                if (root.GetComponent<ArenaRuntime>() != null) runtime = root.GetComponent<ArenaRuntime>();
            Assert.IsNotNull(runtime);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var game = (ArenaSimulation)typeof(ArenaRuntime).GetField("_game", flags).GetValue(runtime);
            game.Monsters[0].Xmm = 1000; game.Monsters[0].Zmm = 0; game.Monsters[0].Health = 1;
            Assert.AreEqual(1, game.ActivateSkill(0).Kills);
            var item = game.GroundLoot[0].Item;
            Assert.IsNotNull(item, "Client must explicitly bind the Core catalog");
            yield return null;
            bool projected = false;
            foreach (var root in _isolated.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Loot_1") projected = true;
            Assert.IsTrue(projected, "Existing projection must consume the real drop");
            typeof(ArenaRuntime).GetField("_pendingPickup", flags).SetValue(runtime, true);
            yield return new WaitForSeconds(0.25f);
            Assert.AreEqual(0, game.GroundLoot.Count);
            Assert.AreEqual(1, game.Progression.Items.Count);
            Assert.IsTrue(CharacterProgression.TryLoad(PlayerPrefs.GetString(PrimaryKey), out var loaded));
            Assert.AreEqual(item.InstanceId, loaded.Items[0].InstanceId);
            Assert.AreEqual(8, loaded.WeaponBonus);
            Assert.AreEqual(100, ArenaSimulation.TickMs);
            typeof(ArenaRuntime).GetMethod("Restart", flags).Invoke(runtime, null);
            var restarted = (ArenaSimulation)typeof(ArenaRuntime).GetField("_game", flags).GetValue(runtime);
            Assert.AreNotSame(game, restarted);
            restarted.Monsters[0].Xmm = 1000; restarted.Monsters[0].Zmm = 0; restarted.Monsters[0].Health = 1;
            Assert.AreEqual(1, restarted.ActivateSkill(0).Kills);
            var nextItem = restarted.GroundLoot[0].Item;
            Assert.IsNotNull(nextItem, "Restart must preserve the Core loot catalog");
            Assert.AreNotEqual(item.InstanceId, nextItem.InstanceId);

        }
    }
}
