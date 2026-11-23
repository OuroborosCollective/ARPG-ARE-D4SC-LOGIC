using System.Collections;
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
        /// <summary>Ilyan, Mara and Dorn stand in the camp after the crypt with the expression shapes of their new bake
        /// and the mood of their role: Ilyan sad, Mara smiling, Dorn grim.</summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator CampFiguresWearTheMoodOfTheirRole()
        {
            _runtime.enabled = false;
            ArrangeCryptKill();
            for (int i = 0; i < 3; i++) Assert.IsTrue(Game.TryCollectLoot());
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
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator OrselBlinksKeepsHerMoodAndMovesHerLips()
        {
            _runtime.enabled = false;
            ArrangeCryptKill();
            for (int i = 0; i < 3; i++) Assert.IsTrue(Game.TryCollectLoot());
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
                Time.captureDeltaTime = 0.1f;
                _runtime.enabled = true;
                float closed = 0f;
                for (int frame = 0; frame < 90 && closed < 99f; frame++) { yield return null; closed = Mathf.Max(closed, skin.GetBlendShapeWeight(blink)); }
                Assert.Greater(closed, 99f, "She blinks within nine seconds");
                typeof(ArenaRuntime).GetMethod("FaceSpeak", Flags).Invoke(_runtime, new object[] { orsel.transform, 2f });
                float open = 0f;
                for (int frame = 0; frame < 15; frame++) { yield return null; open = Mathf.Max(open, skin.GetBlendShapeWeight(mouth)); }
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
