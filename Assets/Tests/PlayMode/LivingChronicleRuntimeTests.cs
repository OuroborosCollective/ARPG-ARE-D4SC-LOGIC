using System.Collections;
using System.Linq;
using System.Reflection;
using AreArpg.Core;
using AreArpg.UnityClient;
using AshenCrypt.MemoryForge;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AreArpg.PlayMode.Tests
{
    public sealed partial class EntryJourneyRuntimeTests
    {
        [UnityTest]
        public IEnumerator LivingChronicleShowsRealNpcReportOnlyAfterPlayerConversationAndReload()
        {
            EnterVillageAfterScene();
            Assert.IsNotNull(_runtime);
            var world = Game;
            Assert.IsNotNull(world.Progression.Social.Forge);
            var mara = world.Npcs.Single(n => n.Id == SocialLedger.MaraNpcId);
            var ilyan = world.Npcs.Single(n => n.Id == 0);
            mara.Xmm = world.PlayerXmm + 900;
            mara.Zmm = world.PlayerZmm;
            mara.InteractionCooldownTicks = 0;

            Assert.IsTrue(world.DonateCampRation().Accepted);
            var archive = world.Progression.Social.Forge.Archive;
            var action = archive.Events.Last(e => e.Context == "camp.donation");
            Assert.IsNotNull(archive.Get("mara").Knowledge(action.Id));
            Assert.IsNull(archive.Get("ilyan").Knowledge(action.Id));

            for (int tick = 0; tick < 125 && archive.Get("ilyan").Knowledge(action.Id) == null; tick++)
            {
                ilyan.Xmm = mara.Xmm + 900;
                ilyan.Zmm = mara.Zmm;
                world.Step();
            }
            var reported = archive.Get("ilyan").Knowledge(action.Id);
            Assert.IsNotNull(reported, "A real village contact did not relay the witnessed ration event");
            Assert.AreEqual(KnowledgeOrigin.Reported, reported.Origin);
            Assert.AreEqual("mara", reported.Source);
            Assert.IsFalse(LivingChronicle.VisibleToPlayer(world.Progression)
                .Any(e => e.EventId == action.Id && e.Origin == ChronicleOrigin.Heard));

            ilyan.Xmm = world.PlayerXmm + 900;
            ilyan.Zmm = world.PlayerZmm;
            ilyan.InteractionCooldownTicks = 0;
            var conversation = world.InteractNpc(0, NpcAction.Greet);
            Assert.IsTrue(conversation.Accepted);
            Assert.AreEqual("chronicle.camp.donation", conversation.ChronicleTextKey);
            Assert.AreEqual("ilyan", conversation.ChronicleSpeakerId);
            Assert.AreEqual(KnowledgeOrigin.Reported, conversation.ChronicleKnowledgeOrigin);
            Assert.AreEqual(action.Id, conversation.ChronicleEventId);
            var presented = typeof(ArenaRuntime).GetMethod("PresentedNpcResult",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(presented);
            var text = (string)presented.Invoke(_runtime, new object[] { conversation });
            StringAssert.Contains("Mir wurde erzaehlt:", text);
            StringAssert.Contains("Du hast Mara eine echte Ration gebracht.", text);
            Assert.IsTrue(LivingChronicle.VisibleToPlayer(world.Progression).Any(e =>
                e.EventId == action.Id && e.Origin == ChronicleOrigin.Heard &&
                e.SourceNpcId == "ilyan" && e.KnowledgeOrigin == KnowledgeOrigin.Reported));

            string saved = world.Progression.SaveString();
            var fields = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(ArenaRuntime).GetField("_hungerJournalOpen", fields).SetValue(_runtime, true);
            typeof(ArenaRuntime).GetField("_chroniclePageOpen", fields).SetValue(_runtime, true);
            var view = typeof(ArenaRuntime).GetMethod("CachedPlayerChronicle", fields);
            Assert.IsNotNull(view);
            var projected = (System.Collections.Generic.IReadOnlyList<ChronicleEntry>)view.Invoke(_runtime, null);
            Assert.IsTrue(projected.Any(e => e.EventId == action.Id &&
                e.Origin == ChronicleOrigin.Heard && e.SourceNpcId == "ilyan"));
            Assert.AreEqual(saved, world.Progression.SaveString(),
                "Opening the real Unity Chronicle projection must not change canonical save");

            Assert.IsTrue(CharacterProgression.TryLoad(saved, out var restored));
            Assert.AreEqual(saved, restored.SaveString());
            Assert.IsTrue(LivingChronicle.VisibleToPlayer(restored).Any(e =>
                e.EventId == action.Id && e.Origin == ChronicleOrigin.Heard));
            yield return null;
        }
    }
}
