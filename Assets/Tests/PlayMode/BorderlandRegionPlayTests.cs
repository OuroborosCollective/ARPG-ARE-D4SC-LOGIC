using System.Collections;
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
        [TestCase(AreArpg.Core.MonsterRole.Ranged, "RangedQuiver")]
        [TestCase(AreArpg.Core.MonsterRole.Shield, "ShieldPlate")]
        [TestCase(AreArpg.Core.MonsterRole.Swarm, "SwarmSpines")]
        [TestCase(AreArpg.Core.MonsterRole.Elite, "EliteCrest")]
        public void AuthoredRoleSilhouetteFollowsTheCoreMonster(AreArpg.Core.MonsterRole role, string key)
        {
            var monster = AreArpg.Core.MonsterCatalog.Spawn("ork.grunt", 908, 2000, 2000, 1);
            monster.Role = role; // Projection fixture; the scene still uses its production spawn method.
            var spawn = typeof(AreArpg.UnityClient.ArenaRuntime).GetMethod("SpawnMonster", Flags);
            spawn.Invoke(_runtime, new object[] { monster });
            var roleArt = Find("Role_" + key);
            Assert.IsNotNull(roleArt);
            Assert.IsNotNull(roleArt.GetComponentInChildren<LODGroup>());
            Assert.IsTrue(roleArt.transform.IsChildOf(Find("Enemy_908").transform));
            Assert.IsEmpty(roleArt.GetComponentsInChildren<Collider>());
        }

        [UnityTest]
        public IEnumerator SnowAshAndBloodcapUseTheSavedSkyAndReducedEffects()
        {
            _runtime.enabled = false;
            foreach (var e in Game.Monsters) { e.Xmm = 1000; e.Zmm = 0; e.Health = 1; }
            Game.ActivateSkill(3);
            for (int i = 0; i < 3; i++) Assert.IsTrue(Game.TryCollectLoot());
            MoveStory(0, 24000); TravelStory(); yield return null;
            var ilyan = Game.Npcs.Find(n => n.Id == 0);
            ilyan.Xmm = Game.PlayerXmm + 1000; ilyan.Zmm = Game.PlayerZmm;
            yield return StoryAction(HungerAction.HearIlyan);
            MoveStory(0, 24000); TravelStory(); yield return null;
            Assert.AreEqual(3, Game.Progression.EntryAct);
            var terrain = Find("WorldForge_" + RegionNetwork.Aschenstrasse);
            Assert.IsNotNull(terrain);
            Assert.IsEmpty(terrain.GetComponentsInChildren<Collider>());
            var ground = Find("AshRoad_Ground").GetComponent<Renderer>().sharedMaterial;
            Assert.AreSame(WorldSurfacePresentation.AshSnow, ground);
            Assert.IsNotNull(ground.GetTexture("_Mask"));
            var bank = Find("Bau_b0002_BloodcapBank");
            var gill = bank.GetComponentInChildren<LODGroup>().GetLODs()[0].renderers[0];
            Assert.IsTrue(gill.sharedMaterial.IsKeywordEnabled("_EMISSION"));
            Assert.IsNotNull(gill.sharedMaterial.GetTexture("_EmissionMap"));
            Assert.IsEmpty(bank.GetComponentsInChildren<Light>());
            var focus = bank.transform.position + new Vector3(0, .24f, 0);
            var eye = focus + new Vector3(1.80f, .65f, -2.45f);
            var forge = Game.Progression.Social.Forge;
            var clock = typeof(NpcForgeState).GetProperty("ProcessedTicks",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var refresh = typeof(ArenaRuntime).GetMethod("RefreshWorldSky", Flags);
            var appear = typeof(ArenaRuntime).GetMethod("RefreshRegionalAppearance", Flags);
            var air = (AshenAtmosphere)typeof(ArenaRuntime).GetField("_atmosphere", Flags).GetValue(_runtime);
            bool prior = air.ReducedEffects;
            ulong tick = forge.ProcessedTicks;
            var block = new MaterialPropertyBlock();
            try
            {
                for (int shot = 0; shot < 3; shot++)
                {
                    clock.SetValue(forge, shot == 0 ? 0UL : 7200UL);
                    air.SetPreferences(air.MusicVolume, air.VoiceVolume, air.EffectsVolume, air.Muted, shot == 2);
                    string saved = Game.Progression.SaveString();
                    ulong hash = Game.ComputeWorldHash();
                    refresh.Invoke(_runtime, null);
                    appear.Invoke(_runtime, null);
                    Assert.That(Shader.GetGlobalFloat("_AREEmberGain"), Is.EqualTo(shot == 0 ? .70f : shot == 1 ? 1.50f : .28f).Within(.01f));
                    gill.GetPropertyBlock(block);
                    Assert.That(block.GetColor("_EmissionColor").r, Is.EqualTo(shot == 2 ? .22f : .92f).Within(.01f));
                    string label = shot == 0 ? "tag" : shot == 1 ? "nacht" : "reduziert";
                    yield return Capture("schneeasche-" + label + ".png", overridePosition:eye, overrideFocus:focus);
                    Assert.AreEqual(saved, Game.Progression.SaveString());
                    Assert.AreEqual(hash, Game.ComputeWorldHash());
                }
            }
            finally
            {
                clock.SetValue(forge, tick);
                air.SetPreferences(air.MusicVolume, air.VoiceVolume, air.EffectsVolume, air.Muted, prior);
                refresh.Invoke(_runtime, null);
                appear.Invoke(_runtime, null);
            }
        }

        [UnityTest]
        public IEnumerator RegionalSceneryTravelsWithTheRealVillageAndAshRoad()
        {
            _runtime.enabled = false;
            foreach (var enemy in Game.Monsters) { enemy.Xmm = 1000; enemy.Zmm = 0; enemy.Health = 1; }
            Game.ActivateSkill(3);
            for (int i = 0; i < 3; i++) Assert.IsTrue(Game.TryCollectLoot());
            MoveStory(0, 24000); TravelStory(); yield return null;
            var village = Find("ARE_BuildLayer_aschengrenze");
            Assert.IsNotNull(village, "published village art must reach the normal chapter entry");
            Assert.IsNotEmpty(village.GetComponentsInChildren<LODGroup>());
            Assert.IsEmpty(village.GetComponentsInChildren<Collider>());
            yield return Capture("borderland-village.png");
            var ilyan = Game.Npcs.Find(n => n.Id == 0);
            // Same dialogue proximity fixture as the chapter-travel regression.
            ilyan.Xmm = Game.PlayerXmm + 1000; ilyan.Zmm = Game.PlayerZmm;
            yield return StoryAction(AreArpg.Core.HungerAction.HearIlyan);
            MoveStory(0, 24000); TravelStory(); yield return null;
            Assert.AreEqual(3, Game.Progression.EntryAct);
            Assert.IsNull(Find("ARE_BuildLayer_aschengrenze"));
            var road = Find("ARE_BuildLayer_aschestrasse");
            Assert.IsNotNull(road);
            Assert.IsNotEmpty(road.GetComponentsInChildren<LODGroup>());
            Assert.IsEmpty(road.GetComponentsInChildren<Light>());
        }
    }
}
