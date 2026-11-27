using System.Collections;
using System.Collections.Generic;
using AreArpg.UnityClient;
using AreArpg.Core;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    public sealed partial class EntryJourneyRuntimeTests
    {
        [UnityTest]
        public IEnumerator GlutFindTravelsFromFloorToHandAndSave()
        {
            if (Resources.Load<TextAsset>("ARE/GearBanks/glut") == null) Assert.Ignore("Private Glut assets required.");
            for (ulong seed = 1; seed <= 180; seed++)
            {
                var character = CharacterProgression.CreateEntryProfile();
                character.Equipment.Enable(character);
                var world = new ArenaSimulation(seed, character, 1, LootCatalog.Version3);
                var ghoul = world.Monsters.First(m => m.Kind == 0);
                ghoul.Xmm = world.PlayerXmm + 700; ghoul.Zmm = world.PlayerZmm; ghoul.Health = 1;
                Assert.AreEqual(1, world.ActivateSkill(0).Kills);
                var drop = world.GroundLoot.FirstOrDefault();
                if (drop == null) continue;
                var weapon = character.Equipment.ForItem(drop.Item);
                if (!weapon.Definition.IsGlutArmory || weapon.Definition.Family == WeaponFamily.Shield) continue;
                typeof(ArenaRuntime).GetMethod("InstallNewRun", Flags).Invoke(_runtime, new object[] {world, "Glut"});
                yield return null;
                var floor = Find("Loot_" + drop.Id);
                Assert.IsNotNull(floor);
                Assert.IsTrue(floor.GetComponentsInChildren<MeshFilter>().Any(m => m.sharedMesh != null && m.sharedMesh.vertexCount > 1000));
                Assert.IsTrue(world.TryCollectLoot());
                Assert.IsTrue(world.TryEquipWeapon(weapon.Ordinal));
                yield return null;
                var hand = Find("Equipped_" + weapon.Definition.Id);
                Assert.IsNotNull(hand);
                Assert.IsNotNull(hand.GetComponentInChildren<MeshFilter>());
                Assert.IsTrue(CharacterProgression.TryLoad(character.SaveString(), out var loaded));
                Assert.AreEqual(weapon.Definition.Id, loaded.Equipment.Weapon.Definition.Id);
                Assert.AreEqual(weapon.AffixDescription, loaded.Equipment.Weapon.AffixDescription);
                Debug.Log("GLUT_EQUIPPED seed=" + seed + " model=" + hand.name + " name=" + weapon.DisplayName + " rarity=" + weapon.Rarity + " affix=" +weapon.AffixDescription);
                yield break;
            }
            Assert.Fail("No qualifying weapon dropped in the reviewed seed range.");
        }

        [UnityTest]
        public IEnumerator GlutArmoryBindsReviewedMeshesAndIcons()
        {
            if (Resources.Load<TextAsset>("ARE/GearBanks/glut") == null) Assert.Ignore("Private Glut assets required.");
            Assert.AreEqual(Resources.Load<TextAsset>("ARE/GearBanks/schwarz") == null ? 3 : 4, Game.LootRevision);
            var models = Resources.LoadAll<GameObject>("ARE/Visuals/Glut");
            Assert.AreEqual(21, models.Length);
            var materials = new HashSet<Material>();
            int triangles = 0;
            foreach (var model in models)
            {
                var host = new GameObject("Review_" + model.name);
                Assert.IsTrue(PassiveArtVisuals.TryBind(host.transform, "Glut/" + model.name), model.name);
                var icon = Resources.Load<Texture2D>("ARE/GlutIcons/" + model.name);
                Assert.IsNotNull(icon, model.name);
                var mesh = host.GetComponentInChildren<MeshFilter>().sharedMesh;
                Assert.That(mesh.bounds.size.y, Is.InRange(0.3f, 3.4f), model.name);
                Assert.That(mesh.subMeshCount, Is.InRange(4, 9), model.name);
                for (int i = 0; i < mesh.subMeshCount; i++) triangles += (int)mesh.GetIndexCount(i)/3;
                var renderer = host.GetComponentInChildren<MeshRenderer>();
                Assert.AreEqual(mesh.subMeshCount, renderer.sharedMaterials.Length);
                foreach (var material in renderer.sharedMaterials)
                {
                    Assert.IsNotNull(material);
                    Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name, model.name + "/" + material.name);
                    if (material.name != "ember")
                    {
                        foreach (var map in new[] {"_BaseMap","_BumpMap","_MetallicGlossMap"})
                            Assert.IsNotNull(material.GetTexture(map),model.name + "/" + material.name + "/" + map);
                    }
                    materials.Add(material);
                }
                Object.Destroy(host);
            }
            Debug.Log("GLUT_ART models=" + models.Length + " triangles=" + triangles + " sharedMaterials=" + materials.Count);
            Assert.That(materials.Count, Is.InRange(6, 10));
            yield return null;
        }
        [UnityTest]
        public IEnumerator SchwarzArmoryBindsReviewedMeshesAndIcons()
        {
            if (Resources.Load<TextAsset>("ARE/GearBanks/schwarz") == null) Assert.Ignore("Private Schwarzschleier assets required.");
            Assert.AreEqual(Resources.Load<TextAsset>("ARE/GearBanks/schwarz") == null ? 3 : 4, Game.LootRevision);
            var models = Resources.LoadAll<GameObject>("ARE/Visuals/Schwarz");
            Assert.AreEqual(21, models.Length);
            var materials = new HashSet<Material>();
            int triangles = 0;
            foreach (var model in models)
            {
                var host = new GameObject("Review_" + model.name);
                Assert.IsTrue(PassiveArtVisuals.TryBind(host.transform, "Schwarz/" + model.name), model.name);
                var icon = Resources.Load<Texture2D>("ARE/SchwarzIcons/" + model.name);
                Assert.IsNotNull(icon, model.name);
                var mesh = host.GetComponentInChildren<MeshFilter>().sharedMesh;
                Assert.That(mesh.bounds.size.y, Is.InRange(0.3f, 3.4f), model.name);
                Assert.That(mesh.subMeshCount, Is.InRange(4, 9), model.name);
                for (int i = 0; i < mesh.subMeshCount; i++) triangles += (int)mesh.GetIndexCount(i)/3;
                var renderer = host.GetComponentInChildren<MeshRenderer>();
                Assert.AreEqual(mesh.subMeshCount, renderer.sharedMaterials.Length);
                foreach (var material in renderer.sharedMaterials)
                {
                    Assert.IsNotNull(material);
                    Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name, model.name + "/" + material.name);
                    if (material.name != "amethyst" && material.name != "moon")
                    {
                        foreach (var map in new[] {"_BaseMap","_BumpMap","_MetallicGlossMap"})
                            Assert.IsNotNull(material.GetTexture(map),model.name + "/" + material.name + "/" + map);
                    }
                    materials.Add(material);
                }
                Object.Destroy(host);
            }
            Debug.Log("SCHWARZ_ART models=" + models.Length + " triangles=" + triangles + " sharedMaterials=" + materials.Count);
            Assert.That(materials.Count, Is.InRange(6, 10));
            yield return null;
        }
    }
}
