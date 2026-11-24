using System.Collections;
using System.Collections.Generic;
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
        /// <summary>
        /// Ember plants in the open ash field beside the ash road (owner direction 2026-11-20). The plants are
        /// private art: without them the field stays bare and the visual part is reported as ignored.
        /// </summary>
        [UnityTest]
        public IEnumerator AshFieldGrowsEmberPlantsWhereTheScatterPutsThemAndNowhereElse()
        {
            _runtime.enabled = false;
            ArrangeCryptKill();
            CollectCryptLoot();
            MoveStory(0, 24000); TravelStory();
            var ilyan = Game.Npcs.Find(n => n.Id == 0);
            ilyan.Xmm = Game.PlayerXmm + 1000; ilyan.Zmm = Game.PlayerZmm;
            yield return StoryAction(HungerAction.HearIlyan);
            MoveStory(0, 24000); TravelStory();
            Assert.AreEqual(3, Game.Progression.EntryAct);
            MoveStory(0, -2000);
            yield return StoryAction(HungerAction.InspectTrail);

            GameObject verges = Find("AshRoad_Verges");
            Assert.IsNotNull(verges, "The ash road builds its roadside dressing");
            Transform[] plants = verges.GetComponentsInChildren<Transform>(true)
                .Where(t => t.parent == verges.transform && t.name.StartsWith("Ash_Flora_", System.StringComparison.Ordinal)).ToArray();
            List<AshFloraScatter.Spot> spots = AshFloraScatter.Spots(false);
            Assert.AreEqual(spots.Count(spot => PassiveArtVisuals.Has(spot.Key)), plants.Length,
                "One plant for every scatter spot whose visual is installed; none without the private art");
            foreach (Transform plant in plants)
            {
                Assert.IsEmpty(plant.GetComponentsInChildren<Collider>(true), plant.name + ": plants never block movement, the Core owns it");
                Assert.IsEmpty(plant.GetComponentsInChildren<Light>(true), plant.name + ": glow is material, lights are budgeted");
                Assert.IsEmpty(plant.GetComponentsInChildren<ParticleSystem>(true), plant.name);
                Renderer[] renderers = plant.GetComponentsInChildren<Renderer>(true);
                Assert.IsNotEmpty(renderers, plant.name);
                foreach (Renderer renderer in renderers)
                {
                    Bounds bounds = renderer.bounds;
                    Assert.Greater(Mathf.Abs(bounds.center.x) - bounds.extents.x, 5f, plant.name + " reaches into the road");
                    Assert.Less(Mathf.Abs(bounds.center.x) + bounds.extents.x, 28f, plant.name + " leaves the ground");
                    Assert.Less(Mathf.Abs(bounds.center.z) + bounds.extents.z, 28f, plant.name + " leaves the ground");
                    Assert.That(bounds.min.y, Is.InRange(-.2f, .05f), plant.name + " stands on the ash");
                    Assert.Less(bounds.size.y, 1.1f, plant.name + ": at most hip-high, the hero stays readable");
                    Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, renderer.shadowCastingMode, plant.name);
                }
            }
            if (plants.Length == 0) Assert.Ignore("Requires the private ashen world plants (never in the repository)");

            // Evidence: the hero in the field, then a close look at an ember mushroom cluster.
            MoveStory(11000, -5000);
            _runtime.enabled = true;                       // a few real frames: the hero's figure follows the Core
            for (int frame = 0; frame < 4; frame++) yield return null;
            _runtime.enabled = false;
            Camera.main.GetComponent<ArenaCameraRig>().SnapToTarget();
            yield return null;
            yield return Capture("ash-field-walk.png");
            Transform elder = plants.FirstOrDefault(plant => plant.GetComponentsInChildren<MeshRenderer>(true)
                .Any(renderer => renderer.sharedMaterials.Any(material => material != null && material.name == "EmberCap") && renderer.bounds.size.y > .5f));
            if (elder != null)
            {
                Vector3 at = elder.GetComponentInChildren<Renderer>().bounds.center;
                yield return Capture("ash-field-mushrooms.png", overridePosition: at + new Vector3(1.9f, 1.25f, -2.4f), overrideFocus: at + Vector3.up * .1f);
            }
        }
    }
}
