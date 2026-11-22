using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreArpg.PlayMode.Tests
{
    public sealed partial class EntryJourneyRuntimeTests
    {
        private static string ShoulderFolder => Environment.GetEnvironmentVariable("ASHEN_SHOULDER_OUTPUT");
        private static Animator FittedAnimator(Transform hero)
        {
            var animator=hero.GetComponentsInChildren<Animator>().FirstOrDefault(a => a.avatar && a.avatar.isValid && a.isHuman && a.runtimeAnimatorController);
            if(!animator && string.IsNullOrEmpty(ShoulderFolder))
                Assert.Ignore("Dieser Passformtest nutzt das lizenzierte Humanoid im privaten Editorprofil.");
            Assert.IsNotNull(animator,"Echter Humanoid im angeforderten Profil");
            return animator;
        }
        private IEnumerator ReloadShoulderCheckpoint()
        {
            Assert.IsTrue(SceneManager.SetActiveScene(_previous));
            // The normal client also owns scene roots outside the ArenaRuntime GameObject.
            yield return SceneManager.UnloadSceneAsync(_scene);
            _scene=SceneManager.CreateScene("AshenEntry");
            Assert.IsTrue(SceneManager.SetActiveScene(_scene));
            _runtime=new GameObject("Feldschulter_Checkpoint").AddComponent<ArenaRuntime>();
            yield return null;
        }
        private GameObject ActiveObject(string name) =>
            _scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == name && t.gameObject.activeInHierarchy)?.gameObject;

        private static Texture2D ShoulderImage(Camera camera, string stem, int width=896, int height=896)
        {
            var target = RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var active = RenderTexture.active;
            var previous = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(width,height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply();
                if (stem != null && !string.IsNullOrEmpty(ShoulderFolder))
                    File.WriteAllBytes(System.IO.Path.Combine(ShoulderFolder,stem+".png"),image.EncodeToPNG());
                return image;
            }
            finally { camera.targetTexture=previous; RenderTexture.active=active; RenderTexture.ReleaseTemporary(target); }
        }

        // A fixed portrait camera isolates the purchased avatar and compares two renders in the same pose.
        private static int ShoulderPortrait(Transform hero, Transform shoulder, string stem, bool bust)
        {
            var avatar=FittedAnimator(hero);
            var upper=avatar.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var head=avatar.GetBoneTransform(HumanBodyBones.Head);
            var rig = new GameObject("Schulter_Werklicht");
            int mask=1<<29;
            var layers=hero.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>t.gameObject.layer);
            var lights=new List<Light>();
            var frames=new List<Texture2D>();
            bool wasEquipped=shoulder && shoulder.gameObject.activeSelf;
            try
            {
                foreach(var t in layers.Keys) t.gameObject.layer=29;
                var camera=rig.AddComponent<Camera>(); camera.enabled=false;
                camera.cullingMask=mask; camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=new Color(.12f,.147f,.16f,1); camera.nearClipPlane=.025f;
                camera.farClipPlane=10f; camera.fieldOfView=bust?28:24;
                var focus=bust?Vector3.Lerp(head.position,upper.position,.50f)-Vector3.up*.05f:
                    upper.position+hero.rotation*new Vector3(-.055f,-.009f,.012f);
                camera.transform.position=focus + hero.rotation*(bust?new Vector3(-.65f,.13f,1.29f):new Vector3(-.62f,.16f,.53f));
                camera.transform.LookAt(focus,Vector3.up);
                void Light(Vector3 from, Color colour,float intensity)
                {
                    var o=new GameObject("Feldschulter Streiflicht"); o.transform.SetParent(rig.transform,true);
                    var light=o.AddComponent<Light>(); light.type=LightType.Directional;light.color=colour;light.intensity=intensity;
                    light.cullingMask=mask;light.shadows=LightShadows.None;light.transform.rotation=Quaternion.LookRotation(-(hero.rotation*from),Vector3.up);
                    lights.Add(light);
                }
                Light(new Vector3(-1,1.4f,1.1f),new Color(1,.83f,.65f),2.15f);
                Light(new Vector3(.65f,.6f,.3f),new Color(.55f,.76f,1),.80f);
                Light(new Vector3(-.7f,.7f,-1),new Color(.66f,.82f,1),2.1f);
                frames.Add(ShoulderImage(camera,null));
                frames.Add(ShoulderImage(camera,stem));
                if (!shoulder) return 0;
                frames.Add(ShoulderImage(camera,null));
                shoulder.gameObject.SetActive(false);
                frames.Add(ShoulderImage(camera,stem+"-ohne"));
                var on=frames[1].GetPixels32(); var off=frames[3].GetPixels32(); var control=frames[2].GetPixels32();
                int changed=0, repeat=0;
                bool Visible(Color32 a,Color32 b) => Math.Max(Math.Abs(a.r-b.r),Math.Max(Math.Abs(a.g-b.g),Math.Abs(a.b-b.b)))>12;
                for(int i=0;i<on.Length;i++){if(Visible(on[i],off[i]))changed++;if(Visible(on[i],control[i]))repeat++;}
                Assert.Greater(changed,Math.Max(350,2*repeat),"Isolierter sichtbarer Schulteranteil "+stem);
                return changed;
            }
            finally
            {
                if (shoulder) shoulder.gameObject.SetActive(wasEquipped);
                foreach(var it in layers) if(it.Key) it.Key.gameObject.layer=it.Value;
                foreach(var image in frames) if(image) UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(rig);
            }
        }

        [UnityTest]
        public IEnumerator FieldHealerUnarmouredReferencePortrait()
        {
            yield return null; _runtime.enabled=false;
            var hero=Find("Wanderer").transform;
            ShoulderPortrait(hero,null,"Anprobe-Ausgang",true);
        }

        [UnityTest]
        public IEnumerator FieldHealerKnightDropFitsThroughMotionAndFreshSave()
        {
            _runtime.enabled=false;
            var initialHero=Find("Wanderer").transform;
            var initialAvatar=FittedAnimator(initialHero);
            var fittedSkin=initialAvatar.GetComponentsInChildren<SkinnedMeshRenderer>()
                .Single(s=>s.sharedMesh && s.sharedMesh.name=="hero_GameMesh");
            var originalSkin=fittedSkin.sharedMesh;var originalMaterials=fittedSkin.sharedMaterials;
            var originalBones=fittedSkin.bones;
            var game=Game; var gear=game.Progression.Equipment;
            Assert.IsTrue(gear.Active);
            Assert.IsFalse(game.TrySetFieldPauldronEquipped(true));
            var knight=game.Monsters.Single(m=>m.Id==2 && m.Kind==1);
            int hp=knight.Health;
            Assert.AreEqual(knight.MaxHealth,hp);
            // Approach its authored spawn with movement input and fight using the normal skill costs and cooldowns.
            int ticks=0;
            while(!knight.IsDead && !game.GameOver && ticks++<240)
            {
                game.SetMoveInput((int)(knight.Xmm-game.PlayerXmm),(int)(knight.Zmm-game.PlayerZmm));
                game.Step(); game.ActivateSkill(0);game.ActivateSkill(1);
                if(game.PlayerHealth<game.MaxPlayerHealth*2/3) game.ActivateSkill(2);
            }
            game.SetMoveInput(0,0);
            Assert.IsTrue(knight.IsDead,"Ritter mit echten Trefferpunkten, ticks "+ticks);
            var ground=game.GroundLoot.Single(l=>l.HasFieldPauldron);
            var ordinal=ground.Item.KillOrdinal; var instance=ground.Item.InstanceId;
            Assert.IsFalse(gear.FieldPauldronOwned);
            typeof(ArenaRuntime).GetMethod("UpdatePresentation",Flags).Invoke(_runtime,new object[]{1f});
            yield return null;
            Assert.IsNotNull(ActiveObject("GroundedFieldPauldron"),"Wearable in world loot");
            typeof(ArenaRuntime).GetField("_pendingPickup",Flags).SetValue(_runtime,true);
            _runtime.enabled=true;
            for(int i=0;i<90 && !gear.FieldPauldronOwned;i++)yield return null;
            Assert.IsTrue(gear.FieldPauldronOwned,"Tatsächlicher Pickup-Eingang");
            _runtime.enabled=false; Assert.IsFalse(gear.FieldPauldronEquipped);
            Assert.AreEqual(ordinal,gear.FieldPauldronOrdinal);
            _runtime.OpenMenu(ArenaRuntime.MenuInventory);yield return null;
            var inv=ActiveObject("FieldPauldronIcon");Assert.IsNotNull(inv);
            inv.GetComponentInParent<Button>().onClick.Invoke();yield return null;
            var equip=ActiveObject("EquipFieldPauldron");Assert.IsNotNull(equip);
            equip.GetComponent<Button>().onClick.Invoke();yield return null;
            if(!string.IsNullOrEmpty(ShoulderFolder))
                yield return CaptureMenu("Feldschulter-Inventar.png",1366,768);
            _runtime.CloseMenu();yield return null;
            Assert.IsTrue(gear.FieldPauldronEquipped);
            var hero=Find("Wanderer").transform; var a=FittedAnimator(hero);
            var upper=a.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var part=ActiveObject("Equipped_FieldPauldron").transform;
            Assert.AreSame(upper,part.parent);
            Assert.AreNotSame(originalSkin,fittedSkin.sharedMesh,"Kauf-Schulter wird beim Tragen gewechselt");
            Assert.AreEqual(originalSkin.vertexCount,fittedSkin.sharedMesh.vertexCount);
            CollectionAssert.AreEqual(originalBones,fittedSkin.bones);CollectionAssert.AreEqual(originalMaterials,fittedSkin.sharedMaterials);
            long omitted=(long)(originalSkin.GetIndexCount(0)-fittedSkin.sharedMesh.GetIndexCount(0))/3;
            Assert.Greater(omitted,0);Assert.Less(omitted,originalSkin.GetIndexCount(0)/12);
            part.gameObject.SetActive(false);
            Assert.AreSame(originalSkin,fittedSkin.sharedMesh,"Exakte ursprüngliche Projektion wiederhergestellt");
            part.gameObject.SetActive(true);
            var lod=part.GetComponentInChildren<LODGroup>();
            Assert.IsNotNull(lod);Assert.AreEqual(3,lod.lodCount);
            if(!string.IsNullOrEmpty(ShoulderFolder))
            {
                var main=Camera.main; var img=ShoulderImage(main,"Krypta-Spielkamera",1152,720);
                UnityEngine.Object.DestroyImmediate(img);
            }
            var position=part.localPosition;var rotation=part.localRotation;var fittedScale=part.localScale;
            var worldHash=game.ComputeWorldHash();var save=game.Progression.SaveString();
            var metrics=new StringBuilder();metrics.AppendLine("source "+instance+" ordinal "+ordinal+" fullHP "+hp+" ticks "+ticks);
            metrics.AppendLine("left source triangles removed "+omitted+" original vertices "+originalSkin.vertexCount);
            var poseRotations=new List<Quaternion>();
            foreach(var pose in new[]{"Idle","Move","Melee"})
            {
                a.SetFloat("MoveMagnitude",pose=="Move"?1:0);
                a.Play("Base Layer."+pose,0,pose=="Idle"?.30f:pose=="Move"?.31f:.44f);
                a.Update(1f/60f);
                Assert.IsTrue(a.GetCurrentAnimatorStateInfo(0).IsName("Base Layer."+pose),pose);
                var clip=a.runtimeAnimatorController.animationClips.First(c=>c.name==(pose=="Melee"?"EntryStrike":"Entry"+pose));
                Assert.AreEqual(clip.length,a.GetCurrentAnimatorStateInfo(0).length,.055f,pose+" Bewegungsdauer");
                poseRotations.Add(Quaternion.Inverse(a.GetBoneTransform(HumanBodyBones.Chest).rotation)*upper.rotation);
                Assert.Less(Vector3.Distance(position,part.localPosition),.00001f);
                Assert.Less(Quaternion.Angle(rotation,part.localRotation),.01f);
                float centreDistance=Vector3.Distance(upper.position,lod.GetLODs()[0].renderers[0].bounds.center);
                Assert.Less(centreDistance,.17f,"Kuppe bleibt am Schultergelenk "+pose);
                var visible=ShoulderPortrait(hero,part,pose,true);
                metrics.AppendLine(pose+" "+clip.name+" pixels "+visible+" socket "+Metres(part.position-upper.position)+" centreFromJoint "+centreDistance.ToString("F4",CultureInfo.InvariantCulture));
                if(pose=="Idle")ShoulderPortrait(hero,part,"Detail",false);
            }
            float movement=Quaternion.Angle(poseRotations[1],poseRotations[2]);
            Assert.Greater(movement,4f,"native Bewegung zwischen Lauf und Schlag");
            Assert.AreEqual(worldHash,game.ComputeWorldHash());Assert.AreEqual(save,game.Progression.SaveString());
            Assert.AreEqual(save,PlayerPrefs.GetString(_keys[0]));
            Assert.IsTrue(CharacterProgression.TryLoad(PlayerPrefs.GetString(_keys[0]),out var read));
            Assert.IsTrue(read.Equipment.FieldPauldronEquipped); Assert.AreEqual(ordinal,read.Equipment.FieldPauldronOrdinal);
            // Destroy the old projection before starting the normal Awake load path from the verified PlayerPrefs value.
            yield return ReloadShoulderCheckpoint();
            Assert.AreNotSame(game,Game);Assert.IsTrue(Game.Progression.Equipment.FieldPauldronEquipped);
            Assert.AreEqual(ordinal,Game.Progression.Equipment.FieldPauldronOrdinal);
            Assert.IsNotNull(ActiveObject("Equipped_FieldPauldron"));
            _runtime.enabled=false;
            _runtime.OpenMenu(ArenaRuntime.MenuInventory);yield return null;
            ActiveObject("FieldPauldronIcon").GetComponentInParent<Button>().onClick.Invoke();yield return null;
            ActiveObject("EquipFieldPauldron").GetComponent<Button>().onClick.Invoke();yield return null;
            _runtime.CloseMenu();yield return null;
            Assert.IsFalse(Game.Progression.Equipment.FieldPauldronEquipped);
            Assert.IsNull(ActiveObject("Equipped_FieldPauldron"));
            Assert.IsTrue(CharacterProgression.TryLoad(PlayerPrefs.GetString(_keys[0]),out var unworn));
            Assert.IsTrue(unworn.Equipment.FieldPauldronOwned);Assert.IsFalse(unworn.Equipment.FieldPauldronEquipped);
            // An unused projection really constructs its first socket during the strike.
            yield return ReloadShoulderCheckpoint();
            _runtime.enabled=false;Assert.IsNull(ActiveObject("Equipped_FieldPauldron"));
            var secondAvatar=FittedAnimator(Find("Wanderer").transform);
            secondAvatar.Play("Base Layer.Melee",0,.44f);secondAvatar.Update(1f/60f);
            Assert.IsTrue(secondAvatar.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Melee"));
            Assert.IsTrue(Game.TrySetFieldPauldronEquipped(true));
            typeof(ArenaRuntime).GetMethod("UpdateEquipmentPresentation",Flags).Invoke(_runtime,null);
            var fittedAgain=ActiveObject("Equipped_FieldPauldron").transform;
            Assert.Less(Vector3.Distance(fittedScale,fittedAgain.localScale),.0003f,"Erstmaliges Anlegen mitten im Schlag");
            Assert.Less(Quaternion.Angle(rotation,fittedAgain.localRotation),.02f);
            metrics.AppendLine("arm rotation degrees "+movement.ToString("F2",CultureInfo.InvariantCulture)+" / persisted equipped + unequipped");
            if(!string.IsNullOrEmpty(ShoulderFolder))
                File.WriteAllText(System.IO.Path.Combine(ShoulderFolder,"native-play.txt"),metrics.ToString());
        }
    }
}
