namespace AreArpg.Core
{
    // Authored IDs describe only the accepted local response. They own no quest or memory state.
    public enum SpeechLine
    {
        None, Greeting, Remember, ThankHelp, Wary, Hostile, Hurt, Robbed,
        IlyanSecret, IlyanProtected, MaraInformed, SuppliesReplaced, CryptReport,
        HunterThanks, RumorRescue, RumorDeath, RumorRefused, RumorTheft
    }

    public static class NpcSpeech
    {
        public static string Text(SpeechLine line)
        {
            switch (line)
            {
                case SpeechLine.Greeting: return "Sei gegrüßt, Reisender.";
                case SpeechLine.Remember: return "Wir sind uns schon begegnet. Was führt dich her?";
                case SpeechLine.ThankHelp: return "Deinen Beistand vergesse ich nicht. Danke.";
                case SpeechLine.Wary: return "Ich erinnere mich an deinen Übergriff. Halte Abstand.";
                case SpeechLine.Hostile: return "Bleib weg. Deine Taten sind nicht vergessen.";
                case SpeechLine.Hurt: return "Hör auf! Was soll das?";
                case SpeechLine.Robbed: return "Lass meinen Beutel! Das werde ich mir merken.";
                case SpeechLine.IlyanSecret: return "Ich bringe Verwundeten heimlich Essen. Folge der Aschestraße zur alten Mühle.";
                case SpeechLine.IlyanProtected: return "Danke, dass du schweigst. Aber das fehlende Essen wird auffallen.";
                case SpeechLine.MaraInformed: return "Ilyan versorgt also die Verwundeten. Jetzt verstehe ich den Fehlbestand.";
                case SpeechLine.SuppliesReplaced: return "Das Lager ist wieder versorgt. Danke für die Rationen.";
                case SpeechLine.CryptReport: return "Eine Krypta unter dem Speicher? Und frische Verbände? Wir müssen herausfinden, was mit den Vermissten geschehen ist.";
                case SpeechLine.HunterThanks: return "Du hast mir das Leben gerettet. Nimm meinen Beutel. Das vergesse ich dir nicht.";
                case SpeechLine.RumorRescue: return "Der Reisende erzählt, du hättest den Jäger vor dem Bären gerettet.";
                case SpeechLine.RumorDeath: return "Ein Jäger wurde auf der Aschestraße von einem Bären getötet.";
                case SpeechLine.RumorRefused: return "Der Reisende sagt, du sahst den Angriff und hast nicht geholfen.";
                case SpeechLine.RumorTheft: return "Der Reisende sah, wie du den toten Jäger beraubt hast.";
                default: return "";
            }
        }

        public static SpeechLine Greeting(NpcAgent npc)
        {
            switch (npc.Memory.Disposition(npc.Personality))
            {
                case NpcDisposition.Hostile: return SpeechLine.Hostile;
                case NpcDisposition.Wary: return SpeechLine.Wary;
                case NpcDisposition.Friendly: return SpeechLine.ThankHelp;
                default: return npc.Memory.Meetings > 1 ? SpeechLine.Remember : SpeechLine.Greeting;
            }
        }

        public static SpeechLine Rumor(SocialLedger social, int npc)
        {
            if (social.RoadKnowledge(npc, RoadFact.CorpseTheft) > 0) return SpeechLine.RumorTheft;
            if (social.RoadKnowledge(npc, RoadFact.RefusedHelp) > 0) return SpeechLine.RumorRefused;
            if (social.RoadKnowledge(npc, RoadFact.Rescue) > 0) return SpeechLine.RumorRescue;
            if (social.RoadKnowledge(npc, RoadFact.Death) > 0) return SpeechLine.RumorDeath;
            return SpeechLine.None;
        }
    }
}
