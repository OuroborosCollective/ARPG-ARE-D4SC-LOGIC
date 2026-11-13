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

        private IEnumerator VerifyAschenzoll(bool release)
        {
            InstallWoodlandFixture();Game.Progression.Social.EnableForge();
            // Returning from a loot sweep can encounter another live group. Finish that encounter too.
            for(int sweep=0;sweep<32;sweep++)
            {
                WoodlandWalk(-36000,0);
                if(Game.GroundLoot.Count==0)break;
                while(Game.GroundLoot.Count>0){var drop=Game.GroundLoot[0];WoodlandWalk(drop.Xmm,drop.Zmm);Game.TryCollectLoot();}
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
