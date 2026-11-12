using System;
using AreArpg.Core;

internal static class NpcSpeechTests
{
    public static void Run(Action<bool, string> check)
    {
        var w = new ArenaSimulation(137);
        var npc = w.Npcs[0]; npc.Xmm = 20000; npc.Zmm = 20000;
        var remote = w.InteractNpc(0, NpcAction.Greet);
        check(!remote.Accepted && remote.Speech == SpeechLine.None, "speech-rejected-contact-is-silent");
        Contact(w, 0);
        var greeting = w.InteractNpc(0, NpcAction.Greet);
        check(greeting.Accepted && greeting.SpeakerId == 0 && greeting.Speech == SpeechLine.Greeting && greeting.FollowupSpeech == SpeechLine.None, "speech-first-contact-no-invented-knowledge");
        check(w.InteractNpc(0, NpcAction.Greet).Speech == SpeechLine.None, "speech-cooldown-no-duplicate-trigger");
        Contact(w, 0); var help = w.InteractNpc(0, NpcAction.Help);
        check(help.Accepted && help.Speech == SpeechLine.ThankHelp, "speech-real-help-response");
        Contact(w, 0); check(w.InteractNpc(0, NpcAction.Greet).Speech == SpeechLine.ThankHelp, "speech-remembers-help");
        // Explicit witness fixture: use the production ledger's one-time fact and personal report methods.
        w.Progression.Social.RecordRoadFact(RoadFact.Rescue, true, true);
        Contact(w, 0); check(w.InteractNpc(0, NpcAction.Greet).FollowupSpeech == SpeechLine.None, "speech-remote-rescue-does-not-leak");
        w.Progression.Social.ReportRoadFact(4, 0);
        Contact(w, 0); check(w.InteractNpc(0, NpcAction.Greet).FollowupSpeech == SpeechLine.RumorRescue, "speech-known-rumor-follows-greeting");
        Contact(w, 1); check(w.InteractNpc(1, NpcAction.Greet).FollowupSpeech == SpeechLine.None, "speech-uninformed-neighbor-remains-uninformed");
        string save = w.Progression.SaveString(); ulong hash = w.ComputeWorldHash();
        foreach (SpeechLine line in Enum.GetValues(typeof(SpeechLine)))
            check(line == SpeechLine.None || NpcSpeech.Text(line).Length > 0, "speech-authored-subtitle-" + line);
        check(w.Progression.SaveString() == save && w.ComputeWorldHash() == hash, "speech-reading-text-has-no-world-effect");
        npc.Memory.Health = 0; npc.InteractionCooldownTicks = 0;
        var downed = w.InteractNpc(0, NpcAction.Greet);
        check(!downed.Accepted && downed.Speech == SpeechLine.None, "speech-unconscious-npc-is-silent");
    }

    private static void Contact(ArenaSimulation w, int id)
    { var n = w.Npcs.Find(x => x.Id == id); n.Xmm = w.PlayerXmm + 1000; n.Zmm = w.PlayerZmm; n.InteractionCooldownTicks = 0; }
}
