using System;
using System.Linq;
using AshenCrypt.MemoryForge;

namespace AreArpg.Core
{
    /// <summary>One authored line of a fixed story scene; the speaker is a village NPC id (0 Ilyan, 1 Mara, 2 Dorn).</summary>
    public readonly struct SceneLine
    {
        public readonly int SpeakerId; public readonly string Text;
        public SceneLine(int speakerId, string text) { SpeakerId = speakerId; Text = text; }
    }

    /// <summary>
    /// Owner decision 2026-11-15: the first arrival in the Aschengrenze opens with a fixed, non-skippable scene that
    /// explains the missing rations before any choice. Ilyan is an impoverished former pathreader who now begs.
    /// </summary>
    public static class ArrivalScene
    {
        public const string Contract = "AshenCrypt.Scene.ArrivalAschengrenze.v1";
        internal const string Context = "story.arrival.aschengrenze";
        public static readonly SceneLine[] Lines =
        {
            new SceneLine(1, "Zwei Rationen fehlen. Gestern Abend habe ich jede einzelne gezählt – und heute Morgen ist das Lager wieder leichter."),
            new SceneLine(2, "Dann ist ein Dieb unter uns. Wer in Hungerzeiten stiehlt, nimmt den anderen das Leben. Ich finde ihn."),
            new SceneLine(1, "Ohne die Vorräte stehen wir den nächsten Ascheregen nicht durch. Jede Ration zählt."),
            new SceneLine(0, "Seht mich nicht so an. Seit die Händler die Wege meiden, braucht niemand mehr einen Pfadleser. Ja, ich bettle – aber ich bin kein Dieb."),
            new SceneLine(2, "Hunger macht aus jedem einen Dieb, Ilyan. Auch aus dir."),
            new SceneLine(1, "Du da – du kommst aus der alten Krypta? Wenn du Proviant hast, hilf uns aus. Oder finde heraus, wohin unsere Rationen verschwinden, bevor Dorn den Falschen bestraft.")
        };
        /// <summary>Shown after the scene: both paths and what each one means for the story.</summary>
        public const string Paths = "Zwei Wege: Gib Mara sofort Ersatz aus deinem Proviant – das Lager ist versorgt, doch wer die Rationen nahm, bleibt offen. " +
            "Oder finde heraus, wer sie nahm und warum: Sprich mit Ilyan.";
    }

    public sealed partial class CharacterProgression
    {
        internal const string CampDecisionPrefix = "camp.decision.";
        /// <summary>
        /// Owner rule 2026-11-15: Mara's missing rations are decided once. The decision is made by refilling the camp,
        /// stealing from it, or asking Ilyan (the investigation); it is persisted and never reopens.
        /// </summary>
        public bool CampDecisionMade =>
            HungerStage >= 1 || Social.CampRefilled || Social.CampProvisions >= SocialLedger.CampCapacity || CampDecisionRecorded;

        /// <summary>A refill or theft was recorded as the decision in the player's story archive.</summary>
        internal bool CampDecisionRecorded =>
            Social.Forge != null && Social.Forge.Archive.Events.Any(e => e.Kind == MemoryKind.Quest && e.Context.StartsWith(CampDecisionPrefix, StringComparison.Ordinal));
    }

    public sealed partial class ArenaSimulation
    {
        internal const string ListenFirst = "Hör zuerst zu, was im Lager geschehen ist.";
        internal const string CampAlreadyDecided = "Über Maras fehlende Rationen ist bereits entschieden.";

        /// <summary>The ration panel exists only between the arrival scene and the player's single decision.</summary>
        public bool CampDecisionOpen => IsVillage && !GameOver && Progression.EntryAct == 2 && !ArrivalScenePending && !Progression.CampDecisionMade;

        /// <summary>Story village: only while the decision is open. Legacy arena village: whenever the village is shown.</summary>
        public bool CampPanelVisible => Progression.EntryAct == 2 ? CampDecisionOpen : IsVillage && !GameOver;

        /// <summary>Persist which way the player chose for Mara's rations (once, story village only).</summary>
        private void RecordCampDecision(string way)
        {
            var forge = Progression.Social.Forge;
            if (forge == null || Progression.EntryAct != 2 || Progression.CampDecisionRecorded) return;
            forge.Archive.Append(MemoryKind.Quest, "player", "mara", forge.ProcessedTicks, 4000, true, CharacterProgression.CampDecisionPrefix + way, new RelationshipChange());
        }

        /// <summary>
        /// The arrival scene is due: first village visit, rations not yet decided (an older save may already have refilled
        /// the camp or heard Ilyan before the scene existed), scene not yet witnessed.
        /// </summary>
        public bool ArrivalScenePending
        {
            get
            {
                var forge = Progression.Social.Forge;
                return forge != null && !GameOver && IsVillage && Progression.EntryAct == 2 && !Progression.CampDecisionMade &&
                    !forge.Archive.Events.Any(e => e.Kind == MemoryKind.Quest && e.Context == ArrivalScene.Context);
            }
        }

        /// <summary>
        /// Record once that the player witnessed the scene. Only the player's story record changes; NPC memories,
        /// relationships and speech stay as they were, so the scene cannot alter social behaviour.
        /// </summary>
        public bool CompleteArrivalScene()
        {
            if (!ArrivalScenePending) return false;
            var forge = Progression.Social.Forge;
            forge.Archive.Append(MemoryKind.Quest, "player", "world.aschengrenze", forge.ProcessedTicks, 3000, true, ArrivalScene.Context, new RelationshipChange());
            return true;
        }
    }
}
