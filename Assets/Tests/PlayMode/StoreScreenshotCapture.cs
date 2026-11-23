using System;
using System.Collections;
using System.IO;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    /// <summary>
    /// Google Play screenshots from the real chapter journey (owner request 2026-11-23): per area the gameplay camera and
    /// one staged view, 1920×1080. Runs only with ASHEN_STORE_OUTPUT set and the licensed art of the live workspace.
    /// </summary>
    public sealed partial class EntryJourneyRuntimeTests
    {
        private static void StoreRender(Camera camera, string path)
        {
            const int width = 1920, height = 1080;
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 4);
            var old = RenderTexture.active; var previous = camera.targetTexture;
            Texture2D tex = null;
            try
            {
                camera.targetTexture = target; camera.aspect = (float)width / height; camera.Render(); RenderTexture.active = target;
                tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0); tex.Apply();
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous; camera.ResetAspect(); RenderTexture.active = old; RenderTexture.ReleaseTemporary(target);
                if (tex) UnityEngine.Object.Destroy(tex);
            }
        }

        private IEnumerator StoreFrame(string output, string area, string key, Vector3 eyeOffset, float height)
        {
            yield return null;
            var camera = Camera.main; Assert.IsNotNull(camera);
            camera.GetComponent<ArenaCameraRig>().SnapToTarget();
            yield return null;
            var position = camera.transform.position; var rotation = camera.transform.rotation; float fov = camera.fieldOfView;
            try
            {
                StoreRender(camera, System.IO.Path.Combine(output, area + "-spiel.png"));
                // A missing anchor fails the capture instead of silently leaving out the staged view.
                var layer = Find("ARE_BuildLayer_" + area);
                Assert.IsNotNull(layer, area + " build layer");
                var anchor = layer.transform.Find(key);
                Assert.IsNotNull(anchor, area + " anchor " + key);
                Vector3 focus = anchor.position + Vector3.up * height;
                camera.transform.position = focus + eyeOffset; camera.transform.LookAt(focus); camera.fieldOfView = 53f;
                StoreRender(camera, System.IO.Path.Combine(output, area + "-szene.png"));
            }
            finally { camera.fieldOfView = fov; camera.transform.SetPositionAndRotation(position, rotation); }
        }

        private static string StoreOutput()
        {
            string output = Environment.GetEnvironmentVariable("ASHEN_STORE_OUTPUT");
            if (string.IsNullOrEmpty(output)) Assert.Ignore("Set ASHEN_STORE_OUTPUT to capture the Google Play screenshots.");
            System.IO.Directory.CreateDirectory(output);
            return output;
        }

        [UnityTest, Category("PrivateArt"), Timeout(1200000)]
        public IEnumerator StoreScreenshotsOfTheCryptVillageRoadAndMill()
        {
            string output = StoreOutput();
            _runtime.enabled = false;
            Assert.AreEqual(1, Game.Progression.EntryAct);
            yield return StoreFrame(output, "krypta", "Bau_b0202_Brazier", new Vector3(2.35f, 1.4f, -3.3f), .42f);
            foreach (var enemy in Game.Monsters) { enemy.Xmm = 1000; enemy.Zmm = 0; enemy.Health = 1; }
            Game.ActivateSkill(3);
            for (int k = 0; k < 3; k++) Assert.IsTrue(Game.TryCollectLoot());
            MoveStory(0, 24000); TravelStory();
            yield return StoreFrame(output, "aschengrenze", "Bau_b0202_BloodcapBank", new Vector3(-2.02f, 1.12f, -3.15f), .40f);
            var ilyan = Game.Npcs.Find(n => n.Id == 0); ilyan.Xmm = Game.PlayerXmm + 1000; ilyan.Zmm = Game.PlayerZmm;
            yield return StoryAction(HungerAction.HearIlyan);
            MoveStory(0, 24000); TravelStory();
            yield return StoreFrame(output, "aschestrasse", "Bau_b0002_BloodcapBank", new Vector3(2.95f, 1.35f, -3.95f), .6f);
            MoveStory(0, -2000); yield return StoryAction(HungerAction.InspectTrail);
            MoveStory(0, 24000); TravelStory();
            yield return StoreFrame(output, "muehle", "Bau_b0201_StoneOutcrop", new Vector3(-3.5f, 1.65f, -3.1f), .74f);
        }

        [UnityTest, Category("PrivateArt"), Timeout(1200000)]
        public IEnumerator StoreScreenshotsOfTheStoreAndTheHiddenCrypt()
        {
            string output = StoreOutput();
            yield return CompleteSupplyJourney();
            MoveStory(0, 24000); TravelStory();
            yield return StoreFrame(output, "speicher", "Bau_b0004_Brazier", new Vector3(-3.65f, 1.65f, -3.1f), .65f);
            MoveStory(0, 0); MoveStory(-6500, 0); yield return StoryAction(HungerAction.StoreBandage);
            MoveStory(0, 0); MoveStory(0, -3000); MoveStory(6500, -3000); yield return StoryAction(HungerAction.StoreTracks);
            MoveStory(0, -3000); MoveStory(0, 18000); yield return StoryAction(HungerAction.InspectSeal);
            yield return StoryAction(HungerAction.OpenSeal); MoveStory(0, 24000); TravelStory();
            yield return StoreFrame(output, "verborgene-krypta", "Bau_b0003_CryptArch", new Vector3(1.9f, 1.15f, -6.9f), 1.8f);
        }
    }
}
