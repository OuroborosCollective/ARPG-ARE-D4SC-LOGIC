using System.Collections;
using System.Linq;
using AreArpg.Core;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    /// <summary>
    /// Story figures keep their expression shapes (owner remark of 2026-11-23): at the mill Orsel's face keeps her stern
    /// mood, blinks within a few seconds and moves her lips while she answers. Presentation only; the Core is untouched.
    /// </summary>
    public sealed partial class EntryJourneyRuntimeTests
    {
        // Faces animate while seen by a camera. A dedicated rendered view keeps each actor on screen in batch PlayMode.
        private sealed class FaceReviewView : System.IDisposable
        {
            private readonly SkinnedMeshRenderer skin;
            private readonly Camera camera;
            private readonly RenderTexture target;

            public FaceReviewView(SkinnedMeshRenderer skin)
            {
                this.skin = skin;
                target = RenderTexture.GetTemporary(96, 96, 16);
                camera = new GameObject("Face review camera").AddComponent<Camera>();
                camera.targetTexture = target;
                camera.orthographic = true;
                camera.cullingMask = 1 << skin.gameObject.layer;
                camera.useOcclusionCulling = false;
                Render();
            }

            public void Render()
            {
                Bounds bounds = skin.bounds;
                camera.orthographicSize = Mathf.Max(.5f, bounds.extents.magnitude);
                camera.transform.position = bounds.center + Vector3.forward * (2f * camera.orthographicSize);
                camera.transform.LookAt(bounds.center, Vector3.up);
                camera.Render();
            }

            public void Dispose()
            {
                camera.enabled = false;
                camera.targetTexture = null;
                Object.Destroy(camera.gameObject);
                RenderTexture.ReleaseTemporary(target);
            }
        }

        /// <summary>Ilyan, Mara and Dorn stand in the camp after the crypt with the expression shapes of their new bake
        /// and the mood of their role: Ilyan sad, Mara smiling, Dorn grim.</summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator CampFiguresWearTheMoodOfTheirRole()
        {
            _runtime.enabled = false;
            ArrangeCryptKill();
            CollectCryptLoot();
            MoveStory(0, 24000); TravelStory();
            Assert.IsTrue(Game.IsVillage, "The camp follows the crypt");
            yield return null;
            var moods = new[] { ("ARE_NPC_0", "Expression_Brows_Sad_L", 45f), ("ARE_NPC_1", "Expression_Mouth_Happy_L", 18f), ("ARE_NPC_2", "Expression_Brow_Angry_L", 30f) };
            foreach (var (figure, shape, weight) in moods)
            {
                var root = Find(figure);
                Assert.IsNotNull(root, figure + " stands in the camp");
                var skin = root.GetComponentInChildren<SkinnedMeshRenderer>();
                Assert.IsNotNull(skin, figure + " is a bound figure");
                Assert.GreaterOrEqual(skin.sharedMesh.GetBlendShapeIndex("Expression_Eyes_UpperClosed_L"), 0, figure + " keeps the shapes to blink");
                int index = skin.sharedMesh.GetBlendShapeIndex(shape);
                Assert.GreaterOrEqual(index, 0, figure + " has " + shape);
                Assert.AreEqual(weight, skin.GetBlendShapeWeight(index), 0.01f, figure + " shows the mood of the role");
            }

            // Dorn's beard brings mouth shapes of its own ("…_1"); when he speaks, beard and mouth open together.
            var dorn = Find("ARE_NPC_2").GetComponentInChildren<SkinnedMeshRenderer>();
            int mouth = dorn.sharedMesh.GetBlendShapeIndex("Expression_Mouth_Oh_L"), beard = dorn.sharedMesh.GetBlendShapeIndex("Expression_Mouth_Oh_L_1");
            Assert.GreaterOrEqual(beard, 0, "Dorn's beard has its own mouth shape");
            float before = Time.captureDeltaTime;
            try
            {
                using var view = new FaceReviewView(dorn);
                Time.captureDeltaTime = .1f;
                _runtime.enabled = true;
                typeof(ArenaRuntime).GetMethod("FaceSpeak", Flags).Invoke(_runtime, new object[] { Find("ARE_NPC_2").transform, 1.5f });
                float open = 0f;
                for (int frame = 0; frame < 10; frame++)
                {
                    view.Render();
                    yield return null;
                    Assert.IsTrue(dorn.isVisible, "Dorn must be visible to the face-animation system.");
                    Assert.AreEqual(dorn.GetBlendShapeWeight(mouth), dorn.GetBlendShapeWeight(beard), 0.01f, "the beard follows the mouth");
                    open = Mathf.Max(open, dorn.GetBlendShapeWeight(mouth));
                }
                Assert.Greater(open, 10f, "Dorn's mouth moves while he speaks");
            }
            finally
            {
                _runtime.enabled = false;
                Time.captureDeltaTime = before;
            }
        }

        /// <summary>The rescued hunter moves his mouth while his recorded thanks (speaker 3) are heard (review on #133).</summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator RescuedHunterMovesHisMouthWhileHisThanksAreHeard()
        {
            _runtime.enabled = false;
            var p = CharacterProgression.CreateEntryProfile();
            typeof(CharacterProgression).GetMethod("EnterVillage", Flags).Invoke(p, null);
            typeof(CharacterProgression).GetMethod("DiscoverHungerClue", Flags).Invoke(p, null);
            typeof(CharacterProgression).GetMethod("TravelHunger", Flags).Invoke(p, new object[] { 3, false }); p.Social.EnableForge(); p.Equipment.Enable(p);
            var w = new ArenaSimulation(73, p, 1, LootCatalog.Version1);
            DungeonFixtureSet(p.Road, "Started", true); p.Road.Bear.Xmm = w.PlayerXmm + 1000; p.Road.Bear.Zmm = w.PlayerZmm; p.Road.Bear.Health = 1;
            Assert.Greater(w.ActivateSkill(0).Hits, 0);
            typeof(ArenaRuntime).GetMethod("InstallNewRun", Flags).Invoke(_runtime, new object[] { w, "Jäger gerettet" });
            MoveStory(2000, 9000); yield return null;
            TickOnce(); yield return null; // one real runtime tick builds the road figures

            var hunter = Find("ARE_Road_Hunter");
            Assert.IsNotNull(hunter, "The rescued hunter stands on the road");
            var face = hunter.GetComponentsInChildren<SkinnedMeshRenderer>().FirstOrDefault(s => s.sharedMesh != null && s.sharedMesh.GetBlendShapeIndex("Expression_Eyes_UpperClosed_L") >= 0);
            Assert.IsNotNull(face, "The hunter keeps a living face");
            int mouth = face.sharedMesh.GetBlendShapeIndex("Expression_Mouth_Oh_L");
            var atmosphere = (AshenAtmosphere)typeof(ArenaRuntime).GetField("_atmosphere", Flags).GetValue(_runtime);
            atmosphere.AcceptedSpeech(new SocialResult { Accepted = true, SpeakerId = 3, Speech = SpeechLine.HunterThanks });
            float before = Time.captureDeltaTime;
            try
            {
                using var view = new FaceReviewView(face);
                Time.captureDeltaTime = .1f;
                _runtime.enabled = true;
                float open = 0f;
                for (int frame = 0; frame < 15 && open <= 10f; frame++) { view.Render(); yield return null; open = Mathf.Max(open, face.GetBlendShapeWeight(mouth)); }
                Assert.IsTrue(face.isVisible, "The speaking hunter must be in view.");
                Assert.IsNotEmpty(atmosphere.Subtitle, "His thanks are still heard");
                Assert.Greater(open, 10f, "His mouth moves while he thanks the player");
            }
            finally
            {
                _runtime.enabled = false;
                Time.captureDeltaTime = before;
            }
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator OrselBlinksKeepsHerMoodAndMovesHerLips()
        {
            _runtime.enabled = false;
            ArrangeCryptKill();
            CollectCryptLoot();
            MoveStory(0, 24000); TravelStory();
            var ilyan = Game.Npcs.Find(n => n.Id == 0);
            ilyan.Xmm = Game.PlayerXmm + 1000; ilyan.Zmm = Game.PlayerZmm;
            yield return StoryAction(HungerAction.HearIlyan);
            MoveStory(0, 24000); TravelStory();
            MoveStory(0, -2000);
            yield return StoryAction(HungerAction.InspectTrail);
            MoveStory(0, 24000); TravelStory();
            Assert.AreEqual(4, Game.Progression.EntryAct);
            MoveStory(0, HealingRules.OrselMillZmm); MoveStory(HealingRules.OrselMillXmm + 1500, HealingRules.OrselMillZmm);
            yield return RunFrames(1);

            var orsel = Find(ArenaRuntime.OrselFigureName);
            Assert.IsNotNull(orsel, "Orsel stands at the mill");
            var skin = orsel.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.IsNotNull(skin, "Orsel's figure is bound");
            Mesh mesh = skin.sharedMesh;
            int blink = mesh.GetBlendShapeIndex("Expression_Eyes_UpperClosed_L"), brows = mesh.GetBlendShapeIndex("Expression_Brows_Lowered_L");
            int mouth = mesh.GetBlendShapeIndex("Expression_Mouth_Oh_L");
            Assert.GreaterOrEqual(blink, 0, "The bake keeps her expression shapes");
            Assert.AreEqual(35f, skin.GetBlendShapeWeight(brows), 0.01f, "She keeps her stern brows");

            float before = Time.captureDeltaTime;
            try
            {
                using var view = new FaceReviewView(skin);
                Time.captureDeltaTime = .1f;
                _runtime.enabled = true;
                float closed = 0f;
                for (int frame = 0; frame < 90 && closed < 99f; frame++) { view.Render(); yield return null; closed = Mathf.Max(closed, skin.GetBlendShapeWeight(blink)); }
                Assert.IsTrue(skin.isVisible, "Orsel must be in view during the blink observation.");
                Assert.Greater(closed, 99f, "She blinks within nine seconds");
                typeof(ArenaRuntime).GetMethod("FaceSpeak", Flags).Invoke(_runtime, new object[] { orsel.transform, 2f });
                float open = 0f;
                for (int frame = 0; frame < 15; frame++) { view.Render(); yield return null; open = Mathf.Max(open, skin.GetBlendShapeWeight(mouth)); }
                Assert.Greater(open, 10f, "Her lips move while she answers");
                for (int frame = 0; frame < 30; frame++) yield return null;
                Assert.AreEqual(0f, skin.GetBlendShapeWeight(mouth), 0.01f, "and rest when she is done");
            }
            finally
            {
                _runtime.enabled = false;
                Time.captureDeltaTime = before;
            }
        }
    }
}
