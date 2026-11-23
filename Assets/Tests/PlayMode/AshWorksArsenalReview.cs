using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        [Serializable] private sealed class ArsenalAsset
        {
            public string id,name,slot,prefab;
            public int meshCount,vertices;
        }
        [Serializable] private sealed class ArsenalAssetList
        {
            public List<ArsenalAsset> entries=new List<ArsenalAsset>();
        }
        private static GameObject AuthoredRelicSource(RegionalRelic id)
        {
            var method=typeof(ArenaRuntime).GetMethod("RegionalSourcePrefab",BindingFlags.Static|BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            return (GameObject)method.Invoke(null,new object[]{id});
        }

        private void CheckLiveHand(RegionalRelic id,HumanBodyBones side)
        {
            var live=ArtisanOne("Equipped_"+id);
            var avatar=ArtisanOne("Wanderer").GetComponentsInChildren<Animator>().Single(a=>a.isHuman&&a.avatar&&a.runtimeAnimatorController);
            var hand=avatar.GetBoneTransform(side);
            Assert.IsTrue(live.IsChildOf(hand),id+" does not project to the actor's actual hand");
            var meshes=new HashSet<Mesh>(AuthoredRelicSource(id).GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh));
            Assert.IsTrue(live.GetComponentsInChildren<MeshFilter>(false).Any(f=>f.sharedMesh&&meshes.Contains(f.sharedMesh)),id+" displays different geometry than its acquired item");
        }

        [Test]
        public void WorkshopModelsAndAschepassMaterialsLoadFromTheShippedProject()
        {
            CheckWorkshopCatalog();
            foreach (string key in new[] { "StoneOutcrop", "ShaleLedge", "BloodcapBank", "CryptArch", "RibPillar", "CaveMouth", "Brazier", "GrainBay" })
            {
                var prefab = Resources.Load<GameObject>("ARE/Borderland/" + key);
                Assert.IsNotNull(prefab, key);
                var detail = prefab.GetComponentsInChildren<Transform>(true).Single(t => t.name == "AschepassDetail");
                var meshes = detail.GetComponentsInChildren<MeshFilter>(true);
                Assert.IsNotEmpty(meshes, key);
                foreach (var mesh in meshes)
                {
                    Assert.IsTrue(mesh.sharedMesh && mesh.sharedMesh.vertexCount > 0, key + " missing mesh reference");
                    var materials = mesh.GetComponent<Renderer>().sharedMaterials;
                    Assert.IsNotEmpty(materials, key);
                    foreach (var material in materials)
                        Assert.IsTrue(material && material.shader && material.shader.isSupported &&
                            material.shader.name != "Hidden/InternalErrorShader", key + " lost a shader or material reference");
                }
            }
        }

        private static void CheckWorkshopCatalog()
        {
            // The workshop's 23 pieces lie in the world. Orsel's staff (M_Heilerstab) is handed over by her and borrows a
            // skinned staff model from the weapon catalog, so it is no workshop piece.
            var catalog=new ArsenalAssetList();var all=RegionalRelics.All.Where(id=>!RegionalRelics.GrantedOnly(id)).ToList();
            Assert.AreEqual(23,all.Count);
            Assert.AreEqual(24,RegionalRelics.All.Count);
            foreach(var id in all)
            {
                var prefab=AuthoredRelicSource(id);Assert.IsNotNull(prefab,"Missing native fund prefab "+id);
                var clone=UnityEngine.Object.Instantiate(prefab);clone.name="AshWorks_ProfileView_"+id;
                try
                {
                    var meshes=clone.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh).Select(f=>f.sharedMesh).Distinct().ToArray();
                    Assert.IsNotEmpty(meshes,"No imported mesh on "+id);
                    foreach(var mesh in meshes)
                    {
                        Assert.Greater(mesh.vertexCount,12,id+" empty or surrogate");
                        Assert.Greater(mesh.GetIndexCount(0),24u,id+" empty triangles");
                    }
                    foreach(var renderer in clone.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        Assert.IsNotEmpty(renderer.sharedMaterials,id+" lacks materials");
                        foreach(var mat in renderer.sharedMaterials)
                            Assert.IsTrue(mat&&mat.shader&&mat.shader.isSupported&&mat.shader.name!="Hidden/InternalErrorShader",id+" invalid project material");
                    }
                    var slot=RegionalRelics.Slot(id);var hand=RegionalRelics.HandDefinition(id);
                    if(slot==RegionalRelicSlot.Weapon||slot==RegionalRelicSlot.Shield)
                    {
                        Assert.IsNotNull(hand,id+" missing hand combat definition");
                        var actual=Resources.Load<GameObject>("ARE/Visuals/"+hand.VisualKey);
                        Assert.AreSame(prefab,actual,id+" configured hand visual differs from the pickup");
                    }
                    catalog.entries.Add(new ArsenalAsset{id=id.ToString(),name=RegionalRelics.Name(id),slot=slot.ToString(),
                        prefab=prefab.name,meshCount=meshes.Length,vertices=meshes.Sum(m=>m.vertexCount)});
                }
                finally{UnityEngine.Object.DestroyImmediate(clone);}
            }
            foreach(string name in new[]{"R_Wangenmaske","R_Gratharnisch","R_Scharrstulpe_L","R_Scharrstulpe_R","R_Gratschiene_L","R_Gratschiene_R"})
                Assert.IsNotNull(Resources.Load<GameObject>("ARE/Visuals/Werkarsenal/"+name),name+" missing from wearable Rattler set");
            ArtisanWrite("Werkarsenal-Katalog.json",System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(catalog,true)));

        }

        [UnityTest, Category("PrivateArt")]
        public IEnumerator RegionalArsenalModelsAndLiveHandsMatchTheCatalog()
        {
            CheckWorkshopCatalog();
            yield return ReachArtisanRoad();AshWorksJourney.Walk(Game,0,-2000);
            yield return StoryAction(HungerAction.InspectTrail);
            yield return ClaimAndEquipArtisan(RegionalRelic.G_Zollmesser,5700,-9700,true);
            CheckLiveHand(RegionalRelic.G_Zollmesser,HumanBodyBones.RightHand);
            yield return ClaimAndEquipArtisan(RegionalRelic.G_Ziegelschild,9800,-1000,true);
            CheckLiveHand(RegionalRelic.G_Ziegelschild,HumanBodyBones.LeftHand);
            var hero=ArtisanOne("Wanderer");var hands=CheckWearables(hero,new[]{"Equipped_G_Zollmesser","Equipped_G_Ziegelschild"},true);
            ArtisanPortrait("Zollmesser-und-Ziegelschild",hero,hands,false);
            AshWorksJourney.Walk(Game,-5100,-12600,820);yield return StoryAction(HungerAction.ClaimRegionalRelic);
            var equipment=Game.Progression.Equipment;
            Assert.IsTrue(equipment.OwnsRegionalRelic(RegionalRelic.G_Ofengabel));
            string before=Game.Progression.SaveString();
            Assert.IsFalse(Game.TrySetRegionalRelicEquipped(RegionalRelic.G_Ofengabel,true));
            Assert.AreEqual(before,Game.Progression.SaveString());
            Assert.IsTrue(Game.TryUnequipWeapon(true));Assert.IsTrue(Game.TrySetRegionalRelicEquipped(RegionalRelic.G_Ofengabel,true));
            ProjectCameraFrame();yield return null;
            CheckLiveHand(RegionalRelic.G_Ofengabel,HumanBodyBones.RightHand);
            Assert.IsFalse(ArtisanSceneObjects().Any(t=>t.name=="Equipped_G_Ziegelschild"));
            Assert.IsFalse(ArtisanSceneObjects().Any(t=>t.name=="Equipped_G_Zollmesser"));
            Assert.IsTrue(CharacterProgression.TryLoad(Game.Progression.SaveString(),NativeJourneyMaps,out var restored));
            Assert.AreEqual("G_Ofengabel",restored.Equipment.Weapon.Definition.Id);
        }
    }
}
