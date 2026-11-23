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

        /// <summary>
        /// Drive the real client input queue, projection and PlayerPrefs save adapter under loot revision 2 (issue #48):
        /// a character's first kill always leaves a supply bag, a weapon only sometimes. Whatever fell is shown, glints
        /// within the particle limit, is taken through the real pickup input, and is kept by the save.
        /// </summary>
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
            Assert.AreEqual(1, game.GroundSupplies.Count, "Client must bind loot revision 2: the first kill leaves a supply bag");
            SupplyBag bag = game.GroundSupplies[0].Bag;
            LootItem item = game.GroundLoot.Count > 0 ? game.GroundLoot[0].Item : null;
            if (game.GroundLoot.Count > 0) Assert.AreEqual(2, item.CatalogRevision, "A weapon that falls carries revision 2");
            var coins = game.Progression.Social.Coins; var rations = game.Progression.Social.Provisions;
            yield return null;
            yield return null;
            Assert.IsNotNull(FindIsolated(ArenaRuntime.SupplyVisualPrefix + "1"), "The bag the Core dropped is shown");
            if (item != null) Assert.IsNotNull(FindIsolated("Loot_1"), "Existing projection must consume the real drop");
            var atmosphere = (AshenAtmosphere)typeof(ArenaRuntime).GetField("_atmosphere", flags).GetValue(runtime);
            var glint = (ParticleSystem)typeof(ArenaRuntime).GetField("_lootGlint", flags).GetValue(runtime);
            Assert.IsNotNull(glint, "Loot on the ground glints");
            Assert.AreEqual(ArenaRuntime.LootGlintLimit, glint.main.maxParticles, "The glint keeps its mobile limit");
            yield return new WaitForSeconds(0.7f);
            Assert.That(glint.particleCount, Is.InRange(1, ArenaRuntime.LootGlintLimit), "Sparks rise over the bag");
            bool reducedBefore = atmosphere.ReducedEffects;
            try
            {
                atmosphere.SetPreferences(atmosphere.MusicVolume, atmosphere.VoiceVolume, atmosphere.EffectsVolume, atmosphere.Muted, true);
                glint.Clear();
                yield return new WaitForSeconds(0.7f);
                if (item == null || item.Rarity != ItemRarity.Legendary)
                    Assert.AreEqual(0, glint.particleCount, "With reduced effects ordinary loot stops glinting");
            }
            finally { atmosphere.SetPreferences(atmosphere.MusicVolume, atmosphere.VoiceVolume, atmosphere.EffectsVolume, atmosphere.Muted, reducedBefore); }

            for (int pickup = 0; pickup < 3 && game.LootOnGround; pickup++)
            {
                typeof(ArenaRuntime).GetField("_pendingPickup", flags).SetValue(runtime, true);
                yield return new WaitForSeconds(0.25f);
            }
            Assert.IsFalse(game.LootOnGround, "The real pickup input takes weapon and bag");
            StringAssert.StartsWith("Vorratsbeutel aufgenommen", (string)typeof(ArenaRuntime).GetField("_message", flags).GetValue(runtime));
            Assert.AreEqual(coins + bag.Coins, game.Progression.Social.Coins);
            Assert.AreEqual(rations + bag.Rations, game.Progression.Social.Provisions);
            Assert.AreEqual(1, bag.Rations, "A character's first bags hold a ration");
            Assert.AreEqual(item != null ? 1 : 0, game.Progression.Items.Count);
            yield return null;
            Assert.IsNull(FindIsolated(ArenaRuntime.SupplyVisualPrefix + "1"), "A taken bag leaves the scene");
            Assert.IsTrue(CharacterProgression.TryLoad(PlayerPrefs.GetString(PrimaryKey), out var loaded));
            Assert.AreEqual(game.Progression.Social.Coins, loaded.Social.Coins);
            Assert.AreEqual(item != null ? 1 : 0, loaded.Items.Count);
            if (item != null) Assert.AreEqual(item.InstanceId, loaded.Items[0].InstanceId);
            Assert.AreEqual(item != null ? 8 : 0, loaded.WeaponBonus);
            Assert.AreEqual(100, ArenaSimulation.TickMs);
            typeof(ArenaRuntime).GetMethod("Restart", flags).Invoke(runtime, null);
            var restarted = (ArenaSimulation)typeof(ArenaRuntime).GetField("_game", flags).GetValue(runtime);
            Assert.AreNotSame(game, restarted);
            restarted.Monsters[0].Xmm = 1000; restarted.Monsters[0].Zmm = 0; restarted.Monsters[0].Health = 1;
            Assert.AreEqual(1, restarted.ActivateSkill(0).Kills);
            Assert.AreEqual(1, restarted.GroundSupplies.Count, "Restart must preserve the Core loot catalog");
            Assert.AreEqual(new System.Numerics.BigInteger(2), restarted.GroundSupplies[0].Bag.KillOrdinal);
        }

        private GameObject FindIsolated(string name)
        {
            foreach (var root in _isolated.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            return null;
        }
    }
}
