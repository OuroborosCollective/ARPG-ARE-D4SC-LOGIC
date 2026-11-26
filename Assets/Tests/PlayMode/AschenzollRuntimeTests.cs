using System.Collections;
using System.Linq;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    public sealed partial class EntryJourneyRuntimeTests
    {
        [UnityTest] public IEnumerator AschenzollOriginalEvidenceSurvivesNativeSaveAndPersonalReport()
        { yield return VerifyAschenzoll(false); }
        [UnityTest] public IEnumerator AschenzollReleasedCargoRequiresActualUpperRampInspection()
        { yield return VerifyAschenzoll(true); }

        [UnityTest]
        public IEnumerator NordwechselCommitsChapterTwoBeforeSwitchAndRestoresItInAshenEntry()
        {
            // Arrange the already reported prologue. Walk the new passage, write and reload through the real client.
            InstallJourneyStationFixture();
            Game.Progression.UseCombatV2();
            MoveStory(-9500, 12500);
            DungeonProject();
            CaptureNordwechselFrame("aschenzoll-nordbogen");
            MoveStory(-9500, 18000);
            typeof(ArenaRuntime).GetMethod("SaveCharacter", Flags, null, System.Type.EmptyTypes, null)
                .Invoke(_runtime, null);
            var source = Game;
            ulong sourceHash = source.ComputeWorldHash();
            string station = PlayerPrefs.GetString(_keys[0]);
            TravelStory();
            Assert.AreEqual(2, Game.ChapterId, "The second, spatially distinct northern arch must lead to Ember Rift.");
            Assert.AreNotSame(source, Game);
            Assert.AreEqual(sourceHash, source.ComputeWorldHash(), "Travel must keep the source world replayable.");
            Assert.AreNotEqual(sourceHash, Game.ComputeWorldHash());
            Assert.AreEqual(station, PlayerPrefs.GetString(_keys[1]), "The last valid station checkpoint is the backup.");
            string destination = PlayerPrefs.GetString(_keys[0]);
            Assert.IsTrue(CharacterProgression.TryLoad(destination, NativeJourneyMaps, out var saved));
            Assert.AreEqual(Game.Progression.StableHash, saved.StableHash);
            Assert.AreEqual("2", saved.UnlockedCryptDepth.ToString());
            Assert.IsTrue(saved.Freight.Reported);
            Assert.AreEqual(FreightChoice.Secured, saved.Freight.Choice);
            yield return null;
            Assert.IsNotNull(Find("Ember_Rift_Causeway"));
            Assert.IsNotNull(Find("Ember_Rift_Boss_Dais"));
            Assert.That(ArenaRuntime.DisplayChapterName(Game), Does.Contain("Glutklamm"));
            Assert.IsNull(Find("Aschenzoll_EmptyFreightCart"));
            DungeonProject();
            CaptureNordwechselFrame("glutklamm-causeway");

            // Recreate the actual entry runtime and let Awake read PlayerPrefs again.
            Assert.IsTrue(UnityEngine.SceneManagement.SceneManager.SetActiveScene(_previous));
            yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(_scene);
            _scene = UnityEngine.SceneManagement.SceneManager.CreateScene("AshenEntry");
            Assert.IsTrue(UnityEngine.SceneManagement.SceneManager.SetActiveScene(_scene));
            _runtime = new GameObject("Nordwechsel_Checkpoint").AddComponent<ArenaRuntime>();
            _runtime.enabled = false;
            Assert.AreEqual(2, Game.ChapterId);
            Assert.AreEqual(saved.StableHash, Game.Progression.StableHash);
            Assert.IsNotNull(Find("Ember_Rift_Causeway"));
            var warden = Game.Monsters.Single(m => m.Kind == 3);
            for (int i = 0; i < 200 && warden.BossWindupTicks == 0 && !Game.GameOver; i++)
            {
                Game.SetMoveInput((warden.Xmm - Game.PlayerXmm) * 10, (warden.Zmm - Game.PlayerZmm) * 10);
                Game.Step();
            }
            Assert.AreEqual(8, warden.BossWindupTicks);
            DungeonProject();
            var warning = Find("Ember_Warden_Telegraph");
            Assert.IsNotNull(warning);
            Assert.IsTrue(warning.activeInHierarchy);
            var ring = warning.GetComponent<LineRenderer>();
            Assert.IsTrue(ring.loop);
            for (int i = 0; i < ring.positionCount; i++)
            {
                var vertex = ring.GetPosition(i);
                Assert.That(new Vector2(vertex.x, vertex.z).magnitude,
                    Is.EqualTo(ArenaSimulation.EmberPulseRadiusMm * .001f).Within(.005f));
            }
            CaptureNordwechselFrame("glutklamm-warden");
            int health = warden.Health;
            for (int i = 0; i < 120 && !Game.GameOver && warden.Health == health; i++)
            {
                Game.SetMoveInput((warden.Xmm - Game.PlayerXmm) * 10, (warden.Zmm - Game.PlayerZmm) * 10);
                Game.ActivateSkill(0);
                Game.Step();
            }
            Assert.Less(warden.Health, health, "A normal combat ability can hit the chapter boss.");
        }

        [UnityTest]
        public IEnumerator NordwechselReadbackFailureKeepsTheStationWorldActive()
        {
            InstallJourneyStationFixture();
            // A real previously visited forest binds this save to the shipped map catalog.
            MoveStory(0, 18000);
            TravelStory();
            Assert.IsTrue(Game.InJourney);
            var route = Game.JourneyGeometry;
            JourneyNativeWalk(route.ReturnX, route.ReturnZ, false);
            TravelStory();
            Assert.IsTrue(Game.InAschenzoll);
            MoveStory(-9500, 18000);
            var station = Game;
            string previousCheckpoint = PlayerPrefs.GetString(_keys[0]);
            string previousBackup = PlayerPrefs.GetString(_keys[1]);
            var field = typeof(ArenaRuntime).GetField("_journeyMaps", Flags);
            var maps = field.GetValue(_runtime);
            try
            {
                // The simulation retains its correct catalog. The independent persistence reader loses it.
                field.SetValue(_runtime, null);
                LogAssert.Expect(LogType.Error, "ARPG character save readback failed; no further overwrites allowed.");
                TravelStory();
                Assert.AreSame(station, Game);
                Assert.AreEqual("1", Game.CryptDepth.ToString());
                Assert.AreEqual(previousCheckpoint, PlayerPrefs.GetString(_keys[0]));
                Assert.AreEqual(previousBackup, PlayerPrefs.GetString(_keys[1]));
                Assert.IsTrue((bool)typeof(ArenaRuntime).GetField("_storageFault", Flags).GetValue(_runtime));
            }
            finally { field.SetValue(_runtime, maps); }
            yield return null;
        }

        private void CaptureNordwechselFrame(string name)
        {
            var directory = System.Environment.GetEnvironmentVariable("ASHEN_NORDWECHSEL_OUTPUT");
            if (string.IsNullOrEmpty(directory)) return;
            var camera = Camera.main;
            camera.GetComponent<ArenaCameraRig>().SnapToTarget();
            System.IO.Directory.CreateDirectory(directory);
            var frame = RenderArea(camera, System.IO.Path.Combine(directory, name + ".png"));
            UnityEngine.Object.Destroy(frame);
        }

        private IEnumerator VerifyAschenzoll(bool release)
        {
            InstallWoodlandFixture();Game.Progression.Social.EnableForge();
            // Returning from a loot sweep can encounter another live group. Finish that encounter too.
            for(int sweep=0;sweep<32;sweep++)
            {
                WoodlandWalk(-36000,0);
                if(Game.GroundLoot.Count==0)break;
                while(Game.GroundLoot.Count>0){var drop=Game.GroundLoot[0];WoodlandWalk(drop.Xmm,drop.Zmm);Game.TryCollectLoot();}
                while(Game.GroundSupplies.Count>0){var bag=Game.GroundSupplies[0];WoodlandWalk(bag.Xmm,bag.Zmm);Game.TryCollectLoot();} // loot revision 2: bags too
            }
            Assert.AreEqual(0,Game.GroundLoot.Count,"All encounter loot secured before cave travel");
            Assert.AreEqual(9,Game.WoodlandPassage);
            Assert.IsTrue(CharacterProgression.TryLoad(Game.Progression.SaveString(),out _),"Forest checkpoint is valid before cave travel");
            TravelStory();yield return null;
            Assert.AreEqual(9,Game.Progression.EntryAct,"Reach the cave through the real loot-gated passage");
            MoveStory(0,5000);yield return StoryAction(HungerAction.InspectCubs);
            yield return StoryAction(HungerAction.SpareCubs);
            Assert.AreEqual(CubChoice.Spared,Game.Progression.Woodland.Choice);
            MoveStory(0,-18000);TravelStory();yield return null;
            // The existing forest route is walked and fought; the actual travel adapter owns arrival and saving.
            WoodlandWalk(0,48000);
            while(Game.GroundLoot.Count>0){var loot=Game.GroundLoot[0];WoodlandWalk(loot.Xmm,loot.Zmm);Game.TryCollectLoot();}
                while(Game.GroundSupplies.Count>0){var bag=Game.GroundSupplies[0];WoodlandWalk(bag.Xmm,bag.Zmm);Game.TryCollectLoot();} // loot revision 2: bags too
            WoodlandWalk(0,48000);TravelStory();yield return null;
            Assert.IsTrue(Game.InAschenzoll);
            Assert.That(Game.HungerObjective,Does.StartWith(Game.FreightObjective));
            Assert.That(Game.HungerObjective,Does.Contain("Mara"),"Optional cub follow-up remains visible");
            MoveStory(-5000,-12000);yield return StoryAction(HungerAction.InspectFreightNotice);
            MoveStory(13500,-4000);yield return StoryAction(HungerAction.InspectFreightCage);
            var cart=Find("Aschenzoll_EmptyFreightCart");Assert.IsNotNull(cart);
            Assert.IsEmpty(cart.GetComponentsInChildren<SkinnedMeshRenderer>(),"The cage contains no prisoner or animal");
            MoveStory(-3000,-1000);yield return StoryAction(HungerAction.ReadFreightManifest);
            var actions=(HungerAction[])typeof(ArenaRuntime).GetMethod("HungerActions",Flags).Invoke(_runtime,null);
            CollectionAssert.AreEquivalent(new[]{HungerAction.SecureFreight,HungerAction.ReleaseFreight},actions);
            yield return StoryAction(release?HungerAction.ReleaseFreight:HungerAction.SecureFreight);
            Assert.IsFalse(Game.Progression.Freight.EvidenceReady);
            Assert.IsFalse(Game.ActOnHunger(release?HungerAction.SecureFreight:HungerAction.ReleaseFreight).Accepted);
            if(release)
            {
                for(int tick=0;tick<12;tick++)Game.Step();
                typeof(ArenaRuntime).GetMethod("SaveCharacter",Flags,null,System.Type.EmptyTypes,null).Invoke(_runtime,null);
                Assert.IsTrue(CharacterProgression.TryLoad(PlayerPrefs.GetString(_keys[0]),out var mid));
                int at=Game.Progression.Freight.MotionTicks;
                var resumed=new ArenaSimulation(71,mid,1,LootCatalog.Version1);
                typeof(ArenaRuntime).GetMethod("InstallNewRun",Flags).Invoke(_runtime,new object[]{resumed,"Weiterfahrt geladen"});yield return null;
                Assert.AreEqual(at,Game.Progression.Freight.MotionTicks);
                MoveStory(13500,14000);
                for(int tick=0;tick<100;tick++)Game.Step();
                Assert.IsFalse(Game.Progression.Freight.ArrivalInspected);
                yield return StoryAction(HungerAction.InspectFreightArrival);
                Assert.IsTrue(Game.Progression.Freight.ArrivalInspected);Assert.IsFalse(Game.Progression.Freight.OriginalCollected);
            }
            else
            {
                yield return StoryAction(HungerAction.TakeFreightOriginal);
                MoveStory(11000,14000);MoveStory(11000,-4000);
                Assert.AreEqual(0,Game.Progression.Freight.MotionTicks);
                Assert.IsTrue(Game.Progression.Freight.OriginalCollected);
            }
            DungeonProject();yield return null;
            cart=Find("Aschenzoll_EmptyFreightCart");Assert.AreEqual(Game.Progression.Freight.CargoZ/1000f,cart.transform.position.z,.001f);
            Assert.IsFalse(Find("Freight_OriginalManifest").activeSelf);
            string unchanged=Game.Progression.SaveString();DungeonProject();Assert.AreEqual(unchanged,Game.Progression.SaveString(),"Projection cannot move cargo or change evidence");
            Camera.main.GetComponent<ArenaCameraRig>().SnapToTarget();
            yield return Capture(release?"aschenzoll-upper-ramp.png":"aschenzoll-secured-empty-cage.png");
            Assert.IsTrue(CharacterProgression.TryLoad(PlayerPrefs.GetString(_keys[0]),out var saved));Assert.IsTrue(saved.Freight.EvidenceReady);
            // Isolate local reporting from already covered forest combat; no travel fact or evidence is invented.
            typeof(CharacterProgression).GetMethod("TravelHunger",Flags).Invoke(saved,new object[]{2,false});
            var village=new ArenaSimulation(71,saved,1,LootCatalog.Version1);
            typeof(ArenaRuntime).GetMethod("InstallNewRun",Flags).Invoke(_runtime,new object[]{village,"Bericht an Ilyan"});yield return null;
            var ilyan=Game.Npcs.First(n=>n.Id==0);ilyan.Xmm=ilyan.PreviousXmm=Game.PlayerXmm+1000;ilyan.Zmm=ilyan.PreviousZmm=Game.PlayerZmm;
            yield return StoryAction(HungerAction.ReportFreightEvidence);
            Assert.IsTrue(Game.Progression.Freight.Reported);
            Assert.That(Game.HungerObjective,Does.Contain("Nordwechsel"),"Completed branch remains in the village journal");
            Assert.That(Game.HungerObjective,Does.Contain("Mara"));
            Assert.IsTrue(CharacterProgression.TryLoad(PlayerPrefs.GetString(_keys[0]),out var final));
            Assert.IsTrue(final.Freight.Reported);Assert.AreEqual(release,final.Freight.ArrivalInspected);
            Assert.AreEqual(1,final.Social.Forge.Archive.Get("ilyan").Known.Count(k=>k.Event.Context=="story.reportfreightevidence"));
            Assert.AreEqual(0,final.Social.Forge.Archive.Get("mara").Known.Count(k=>k.Event.Context=="story.reportfreightevidence"));
        }
    }
}
