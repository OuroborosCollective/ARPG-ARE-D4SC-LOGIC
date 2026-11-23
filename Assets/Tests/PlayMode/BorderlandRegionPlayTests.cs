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
        public IEnumerator ChestBundleUsesTheGroundedBagUntilTheRealPickup()
        {
            InstallJourneyStationFixture();
            MoveStory(0, 18000); TravelStory(); yield return null; DungeonProject();
            Game.JourneyGeometry.Chest(0, out var x, out var z);
            JourneyNativeWalk(x, z);
            yield return StoryAction(HungerAction.OpenWestChest);
            var receipt = Game.GroundLoot.Find(item => item.Id == 9000);
            Assert.IsNotNull(receipt);
            DungeonProject();
            var drop = Find("Loot_" + receipt.Id);
            Assert.IsNotNull(drop.transform.Find("GroundedBag"));
            var mesh = drop.GetComponentInChildren<LODGroup>().GetLODs()[0].renderers[0];
            float floor = Game.JourneyGeometry.HeightMm(x, z) * .001f;
            Assert.That(mesh.bounds.min.y, Is.EqualTo(floor).Within(.015f));
            Assert.IsEmpty(drop.GetComponentsInChildren<Light>());
            while (Game.GroundLoot.Exists(item => item.Id == receipt.Id))
                Assert.IsTrue(Game.TryCollectLoot());
            DungeonProject(); yield return null;
            Assert.IsNull(Find("Loot_" + receipt.Id));
        }

        [UnityTest]
        public IEnumerator SnowAshAndBloodcapUseTheSavedSkyAndReducedEffects()
        {
            _runtime.enabled = false;
            ArrangeCryptKill();
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
            Assert.AreSame(ground, Find("Grass verge").GetComponent<Renderer>().sharedMaterial,
                "The shoulder and field must share one continuous ash and fissure pattern.");
            Assert.IsNotNull(ground.GetTexture("_Mask"));
            var bank = Find("Bau_b0002_BloodcapBank");
            var gill = bank.GetComponentInChildren<LODGroup>().GetLODs()[0].renderers[0];
            var wood = Find("Bau_b0012_EmberSnag");
            var grass = Find("Bau_b0013_EmberSedge");
            Assert.IsNotNull(wood, "Charred bark must be visible at the actual ash-road verge.");
            Assert.IsNotNull(grass);
            foreach (var piece in new[] { wood, grass })
            {
                var trim = piece.GetComponentInChildren<LODGroup>().GetLODs()[0].renderers[0];
                Assert.IsTrue(trim.sharedMaterial.IsKeywordEnabled("_EMISSION"));
                Assert.IsEmpty(piece.GetComponentsInChildren<Light>());
            }
            Assert.That(ground.GetTexture("_Mask").width, Is.EqualTo(1024));
            var slate = Resources.Load<Material>("ARE/Borderland/RoughSlate");
            foreach (string label in new[] { "AshRoad_Paving", "AshRoad_Boundary" })
            {
                var piece = Find(label);
                bool usesSlate = false;
                foreach (var renderer in piece.GetComponentsInChildren<MeshRenderer>())
                    if (renderer.enabled && renderer.sharedMaterial == slate) usesSlate = true;
                Assert.IsTrue(usesSlate, label + " needs exposed stone under the ash.");
            }
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
                    if (Resources.Load<GameObject>("ARE/Visuals/AshFloraMushrooms") != null)
                    {
                        bool foundCap = false;
                        foreach (var leaf in Find("AshRoad_Verges").GetComponentsInChildren<MeshRenderer>())
                        {
                            var slots = leaf.sharedMaterials;
                            for (int k = 0; k < slots.Length; k++)
                            {
                                if (slots[k] == null || slots[k].name != "EmberCap") continue;
                                leaf.GetPropertyBlock(block,k);
                                float source = slots[k].GetColor("_EmissionColor").r;
                                Assert.That(block.GetColor("_EmissionColor").r,
                                    Is.EqualTo(source * (shot == 2 ? .08f : .38f)).Within(.02f));
                                foundCap = true;
                            }
                        }
                        Assert.IsTrue(foundCap);
                    }
                    string label = shot == 0 ? "tag" : shot == 1 ? "nacht" : "reduziert";
                    yield return Capture("schneeasche-" + label + ".png", overridePosition:eye, overrideFocus:focus);
                    var groveFocus = (wood.transform.position + bank.transform.position) * .5f + Vector3.up * 1.1f;
                    yield return Capture("glutlicht-" + label + ".png", overridePosition:groveFocus + new Vector3(3.6f,1.15f,-4.6f), overrideFocus:groveFocus);
                    if (shot == 0 && !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("ARE_ENTRY_EVIDENCE_DIR")))
                        yield return MeasureRoadView(eye, focus);
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

        private IEnumerator MeasureRoadView(Vector3 eye, Vector3 focus)
        {
            var camera = Camera.main;
            var previousTarget = camera.targetTexture;
            var position = camera.transform.position;
            var rotation = camera.transform.rotation;
            var active = RenderTexture.active;
            var target = new RenderTexture(1280, 720, 24);
            var watch = new System.Diagnostics.Stopwatch();
            var ms = new double[32];
            try
            {
                camera.targetTexture = target;
                camera.transform.position = eye;
                camera.transform.LookAt(focus);
                for (int i = -3; i < ms.Length; i++)
                {
                    watch.Restart();
                    camera.Render();
                    watch.Stop();
                    if (i >= 0) ms[i] = watch.Elapsed.TotalMilliseconds;
                }
                System.Array.Sort(ms);
                Debug.LogFormat("BORDERLAND_EDITOR camera-submit 1280x720 samples={0} medianMs={1:F2} p95Ms={2:F2} allocatedBytes={3} device={4}",
                    ms.Length, (ms[15] + ms[16]) * .5, ms[30], UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(),
                    SystemInfo.graphicsDeviceName);
            }
            finally
            {
                camera.transform.SetPositionAndRotation(position, rotation);
                camera.targetTexture = previousTarget;
                RenderTexture.active = active;
                target.Release();
                Object.Destroy(target);
            }
            yield break;
        }

        [UnityTest]
        public IEnumerator RegionalSceneryTravelsWithTheRealVillageAndAshRoad()
        {
            _runtime.enabled = false;
            ArrangeCryptKill();
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
