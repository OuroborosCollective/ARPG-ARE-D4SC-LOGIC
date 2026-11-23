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
