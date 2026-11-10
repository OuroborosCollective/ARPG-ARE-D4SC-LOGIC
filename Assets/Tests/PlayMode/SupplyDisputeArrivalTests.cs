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
    /// <summary>Verify that displayed camp supply follows real Core mutations in an isolated scene.</summary>
    public sealed class SupplyDisputeArrivalTests
    {
        private const string PrimaryKey = "ARE_ARPG_character_ac1";
        private const string BackupKey = "ARE_ARPG_character_ac1_backup";
        private bool _hadPrimary, _hadBackup, _snapshotTaken;
        private string _primary, _backup;
        private Scene _oldScene, _testScene;
        private ArenaRuntime _runtime;

        /// <summary>Temporarily run the real Unity runtime with a fresh, unsaved Chapter 1 profile.</summary>
        [UnitySetUp]
        public IEnumerator SetUpChapterOne()
        {
            _hadPrimary = PlayerPrefs.HasKey(PrimaryKey);
            _hadBackup = PlayerPrefs.HasKey(BackupKey);
            _primary = PlayerPrefs.GetString(PrimaryKey, "");
            _backup = PlayerPrefs.GetString(BackupKey, "");
            _snapshotTaken = true;
            _oldScene = SceneManager.GetActiveScene();
            PlayerPrefs.DeleteKey(PrimaryKey);
            PlayerPrefs.DeleteKey(BackupKey);
            _testScene = SceneManager.CreateScene("ARE_Supply_Dispute_Independent_Scene");
            Assert.IsTrue(SceneManager.SetActiveScene(_testScene));
            _runtime = new GameObject("SupplyDispute_RealRuntime").AddComponent<ArenaRuntime>();
            yield return null;
        }

        /// <summary>Restore both real save keys and unload only the scene owned by this test.</summary>
        [UnityTearDown]
        public IEnumerator RestoreOwnerKeys()
        {
            if (!_snapshotTaken) yield break;
            if (_oldScene.IsValid() && _oldScene.isLoaded) SceneManager.SetActiveScene(_oldScene);
            if (_testScene.IsValid() && _testScene.isLoaded)
            {
                var op = SceneManager.UnloadSceneAsync(_testScene);
                if (op != null) yield return op;
            }
            if (_hadPrimary) PlayerPrefs.SetString(PrimaryKey, _primary);
            else PlayerPrefs.DeleteKey(PrimaryKey);
            if (_hadBackup) PlayerPrefs.SetString(BackupKey, _backup);
            else PlayerPrefs.DeleteKey(BackupKey);
            PlayerPrefs.Save();
            Assert.AreEqual(_hadPrimary, PlayerPrefs.HasKey(PrimaryKey));
            Assert.AreEqual(_hadBackup, PlayerPrefs.HasKey(BackupKey));
            Assert.AreEqual(_primary, PlayerPrefs.GetString(PrimaryKey, ""));
            Assert.AreEqual(_backup, PlayerPrefs.GetString(BackupKey, ""));
        }

        /// <summary>Resolve Unity landmarks only from the isolated test scene, not the owner's scene.</summary>
        private GameObject FindLocal(string name)
        {
            foreach (var root in _testScene.GetRootGameObjects())
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name == name) return child.gameObject;
            return null;
        }

        /// <summary>Check 8/10 visible rations become 9/10 after a real donation, without UI authority.</summary>
        [UnityTest]
        public IEnumerator ExistingWorldShowsMissingRationsWithoutSecondGameplayOwner()
        {
            yield return null;
            Assert.IsNotNull(_runtime);
            GameObject shelf = FindLocal("Aschengrenze_Missing_Rations");
            Assert.IsNotNull(shelf, "Actual empty shelf must be rendered");
            foreach (Collider collider in shelf.GetComponentsInChildren<Collider>(true))
                Assert.IsFalse(collider.enabled, "Only the Core owns collision");
            var field = typeof(ArenaRuntime).GetField("_game", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field);
            var game = field.GetValue(_runtime) as ArenaSimulation;
            Assert.IsNotNull(game);
            Assert.AreEqual(8, game.Progression.Social.CampProvisions);
            Assert.AreEqual(SocialLedger.CampCapacity, 10);
            for (int i = 0; i < SocialLedger.CampCapacity; i++)
            {
                var item = FindLocal("Aschengrenze_Stored_Ration_" + i);
                Assert.IsNotNull(item, "Missing graphical stock unit " + i);
                Assert.AreEqual(i < 8, item.activeSelf, "Projection mismatch at " + i);
                foreach (Collider c in item.GetComponentsInChildren<Collider>(true))
                    Assert.IsFalse(c.enabled);
            }
            game.Npcs[SocialLedger.MaraNpcId].Xmm = 1000;
            game.Npcs[SocialLedger.MaraNpcId].Zmm = 0;
            Assert.IsTrue(game.DonateCampRation().Accepted);
            var present = typeof(ArenaRuntime).GetMethod("UpdatePresentation", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(present);
            present.Invoke(_runtime, new object[] { 1f });
            Assert.AreEqual(9, game.Progression.Social.CampProvisions);
            Assert.IsTrue(FindLocal("Aschengrenze_Stored_Ration_8").activeSelf);
            Assert.IsFalse(FindLocal("Aschengrenze_Stored_Ration_9").activeSelf);
            Assert.AreEqual("RATION AN MARA", ArenaRuntime.DonationButtonLabel(true));
            Assert.AreEqual("G: RATION AN MARA", ArenaRuntime.DonationButtonLabel(false));
            Assert.IsNotNull(FindLocal("ARE_NPC_0"));
            Assert.IsNotNull(FindLocal("ARE_NPC_1"));
            Assert.IsNotNull(FindLocal("ARE_NPC_2"));
            Assert.AreEqual(100, ArenaSimulation.TickMs);
        }
    }
}
