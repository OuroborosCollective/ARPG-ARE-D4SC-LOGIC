using System.Collections.Generic;

namespace Fadenwerk
{
    /// <summary>
    /// Spielt einen Dialog ab. Deine Oberfläche zeigt Node.text und VisibleChoices() an und ruft Choose() auf.
    /// </summary>
    public class FadenwerkDialogueRunner
    {
        public DialogueDef Dialogue { get; private set; }
        public DialogueNode Node { get; private set; }
        public FadenwerkNpc Npc { get; private set; }
        /// <summary>Die letzte Belohnung, die eine Antwort ausgelöst hat (oder null).</summary>
        public RewardResult LastReward { get; private set; }

        FadenwerkManager _manager;

        public bool IsRunning
        {
            get { return Dialogue != null && Node != null; }
        }

        public bool Start(FadenwerkManager manager, string dialogueId, FadenwerkNpc npc)
        {
            End();
            if (manager == null || manager.database == null) return false;
            DialogueDef d = manager.database.FindDialogue(dialogueId);
            if (d == null || d.nodes == null || d.nodes.Count == 0) return false;
            DialogueNode start = d.FindNode(d.startNodeId) ?? d.nodes[0];
            _manager = manager;
            Dialogue = d;
            Node = start;
            Npc = npc;
            return true;
        }

        /// <summary>Name des Sprechers des aktuellen Knotens.</summary>
        public string SpeakerName()
        {
            if (Node == null) return "";
            if (!string.IsNullOrEmpty(Node.speaker)) return Node.speaker;
            return Npc != null ? Npc.Label : "";
        }

        /// <summary>Antworten, die der Spieler gerade wählen darf.</summary>
        public List<DialogueChoice> VisibleChoices()
        {
            var list = new List<DialogueChoice>();
            if (Node == null || Node.choices == null) return list;
            for (int i = 0; i < Node.choices.Count; i++)
            {
                DialogueChoice c = Node.choices[i];
                if (c == null) continue;
                if (!string.IsNullOrEmpty(c.requiresFlag) && (_manager == null || !_manager.HasFlag(c.requiresFlag))) continue;
                if (!string.IsNullOrEmpty(c.conditionQuestId) && !string.IsNullOrEmpty(c.conditionState)
                    && (_manager == null || !_manager.CheckQuestState(c.conditionQuestId, c.conditionState))) continue;
                list.Add(c);
            }
            return list;
        }

        public void Choose(DialogueChoice choice)
        {
            if (!IsRunning || choice == null || _manager == null || Node.choices == null) return;
            // Do not execute a foreign, outdated, or now-hidden choice: authorization at action time.
            bool belongsToCurrentNode = false;
            for (int i = 0; i < Node.choices.Count; i++)
                if (object.ReferenceEquals(Node.choices[i], choice)) { belongsToCurrentNode = true; break; }
            if (!belongsToCurrentNode) return;
            bool stillVisible = false;
            foreach (DialogueChoice visible in VisibleChoices())
                if (object.ReferenceEquals(visible, choice)) { stillVisible = true; break; }
            if (!stillVisible) return;
            LastReward = null;
            string action = string.IsNullOrEmpty(choice.action) ? FwAction.None : choice.action;
            if (action == FwAction.OfferQuest) _manager.AcceptQuest(choice.actionParam);
            else if (action == FwAction.CompleteQuest) LastReward = _manager.CompleteQuest(choice.actionParam);
            else if (action == FwAction.GiveKit) LastReward = _manager.GiveKit(choice.actionParam, Dialogue.id + "|" + Node.id);
            else if (action == FwAction.SetFlag) _manager.SetFlag(choice.actionParam);

            if (action == FwAction.End || string.IsNullOrEmpty(choice.nextNodeId)) { End(); return; }
            DialogueNode next = Dialogue.FindNode(choice.nextNodeId);
            if (next == null) { End(); return; }
            Node = next;
        }

        public void End()
        {
            Dialogue = null;
            Node = null;
            Npc = null;
        }
    }
}
