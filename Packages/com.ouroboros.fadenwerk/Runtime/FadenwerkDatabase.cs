using System.Collections.Generic;
using UnityEngine;

namespace Fadenwerk
{
    /// <summary>
    /// Alle Fadenwerk-Inhalte eines Projekts: Quests, Dialoge, Belohnungs-Kits und der Item-Katalog.
    /// NPCs und Marker liegen als Komponenten in den Szenen und werden über ihre ID angesprochen.
    /// </summary>
    [CreateAssetMenu(fileName = "FadenwerkDatabase", menuName = "Fadenwerk/Datenbank", order = 0)]
    public class FadenwerkDatabase : ScriptableObject
    {
        [Tooltip("Startwert für alle Zufallswürfe. Gleicher Seed = gleiche Belohnungen.")]
        public string worldSeed = "fadenwerk";
        public List<QuestDef> quests = new List<QuestDef>();
        public List<DialogueDef> dialogues = new List<DialogueDef>();
        public List<RewardKitDef> kits = new List<RewardKitDef>();
        public List<ItemEntry> items = new List<ItemEntry>();

        public QuestDef FindQuest(string id)
        {
            if (string.IsNullOrEmpty(id) || quests == null) return null;
            for (int i = 0; i < quests.Count; i++)
                if (quests[i] != null && quests[i].id == id) return quests[i];
            return null;
        }

        public DialogueDef FindDialogue(string id)
        {
            if (string.IsNullOrEmpty(id) || dialogues == null) return null;
            for (int i = 0; i < dialogues.Count; i++)
                if (dialogues[i] != null && dialogues[i].id == id) return dialogues[i];
            return null;
        }

        public RewardKitDef FindKit(string id)
        {
            if (string.IsNullOrEmpty(id) || kits == null) return null;
            for (int i = 0; i < kits.Count; i++)
                if (kits[i] != null && kits[i].id == id) return kits[i];
            return null;
        }

        public ItemEntry FindItem(string id)
        {
            if (string.IsNullOrEmpty(id) || items == null) return null;
            for (int i = 0; i < items.Count; i++)
                if (items[i] != null && items[i].id == id) return items[i];
            return null;
        }

        /// <summary>Stellt sicher, dass keine Liste null ist (z. B. nach einem Import).</summary>
        public void Sanitize()
        {
            if (worldSeed == null) worldSeed = "";
            if (quests == null) quests = new List<QuestDef>();
            if (dialogues == null) dialogues = new List<DialogueDef>();
            if (kits == null) kits = new List<RewardKitDef>();
            if (items == null) items = new List<ItemEntry>();
            quests.RemoveAll(q => q == null);
            dialogues.RemoveAll(d => d == null);
            kits.RemoveAll(k => k == null);
            items.RemoveAll(it => it == null);
            for (int i = 0; i < quests.Count; i++)
            {
                var q = quests[i];
                if (q.prerequisiteQuestIds == null) q.prerequisiteQuestIds = new List<string>();
                if (q.objectives == null) q.objectives = new List<ObjectiveDef>();
                q.objectives.RemoveAll(o => o == null);
            }
            for (int i = 0; i < dialogues.Count; i++)
            {
                var d = dialogues[i];
                if (d.nodes == null) d.nodes = new List<DialogueNode>();
                d.nodes.RemoveAll(n => n == null);
                for (int j = 0; j < d.nodes.Count; j++)
                {
                    if (d.nodes[j].choices == null) d.nodes[j].choices = new List<DialogueChoice>();
                    d.nodes[j].choices.RemoveAll(c => c == null);
                }
            }
            for (int i = 0; i < kits.Count; i++)
            {
                if (kits[i].entries == null) kits[i].entries = new List<KitEntry>();
                kits[i].entries.RemoveAll(e => e == null);
            }
        }
    }
}
