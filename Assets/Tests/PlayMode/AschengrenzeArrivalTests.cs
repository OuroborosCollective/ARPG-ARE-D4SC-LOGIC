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
    public sealed class AschengrenzeArrivalTests
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
            _isolated = SceneManager.CreateScene("Aschengrenze_Review_ChapterOne_Check");
            Assert.IsTrue(SceneManager.SetActiveScene(_isolated));
            new GameObject("Aschengrenze_Review_Runtime").AddComponent<ArenaRuntime>();
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

        /// <summary>Find only objects born inside the test scene, never an unrelated scene's objects.</summary>
        private GameObject FindInIsolatedScene(string name)
        {
            foreach (GameObject root in _isolated.GetRootGameObjects())
            {
                if (root.name == name) return root;
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name == name) return child.gameObject;
            }
            return null;
        }

        /// <summary>Require the real Chapter 1 geometry, canonical NPCs and unchanged Core labels, regardless of prior saves.</summary>
        [UnityTest]
        public IEnumerator ArrivalBuildsRecognizableVillageAndPreservesCoreOwners()
        {
            yield return null;
            string[] landmarks =
            {
                "Aschengrenze_Approach_Road", "Aschengrenze_Market_Square",
                "Aschengrenze_Supply_Hall", "Aschengrenze_Watchtower",
                "Aschengrenze_Signal_Beacon"
            };
            foreach (string name in landmarks)
            {
                GameObject landmark = FindInIsolatedScene(name);
                Assert.IsNotNull(landmark, "Missing actual chapter-one Unity landmark: " + name);
                foreach (Collider collider in landmark.GetComponentsInChildren<Collider>(true))
                    Assert.IsFalse(collider.enabled, "Projection-only landmark cannot own collision: " + name);
            }
            Assert.IsNotNull(FindInIsolatedScene("Passage_Arch"));
            for (int id = 0; id < 3; id++)
                Assert.IsNotNull(FindInIsolatedScene("ARE_NPC_" + id), "Missing canonical NPC " + id);
            Assert.AreEqual(100, ArenaSimulation.TickMs);
            var chapterOne = new ArenaSimulation(17);
            var chapterTwo = new ArenaSimulation(17, new CharacterProgression(), new BigInteger(2));
            Assert.AreEqual("Ashen Crypt", chapterOne.ChapterName, "Core identity remains unchanged");
            Assert.AreEqual("Die Aschengrenze", ArenaRuntime.DisplayChapterName(chapterOne));
            Assert.AreEqual("Ember Rift", ArenaRuntime.DisplayChapterName(chapterTwo));
        }
    }
}
