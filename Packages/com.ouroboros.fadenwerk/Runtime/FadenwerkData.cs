using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Fadenwerk
{
    /// <summary>Art eines Quest-Ziels. Als Text gespeichert, damit JSON und Inspector gleich aussehen.</summary>
    public static class FwObjective
    {
        public const string Talk = "talk";
        public const string Kill = "kill";
        public const string Collect = "collect";
        public const string Reach = "reach";
        public const string Deliver = "deliver";
        public const string Custom = "custom";
    }

    /// <summary>Aktion einer Dialog-Antwort.</summary>
    public static class FwAction
    {
        public const string None = "none";
        public const string OfferQuest = "offerQuest";
        public const string CompleteQuest = "completeQuest";
        public const string GiveKit = "giveKit";
        public const string SetFlag = "setFlag";
        public const string End = "end";
    }

    public enum FadenwerkIdKind { Npc, Marker, Quest, Dialogue, Kit, Item }

    /// <summary>Zeigt im Inspector eine Auswahlliste der vorhandenen IDs statt eines Textfelds.</summary>
    public sealed class FadenwerkIdAttribute : PropertyAttribute
    {
        public readonly FadenwerkIdKind kind;
        public FadenwerkIdAttribute(FadenwerkIdKind kind) { this.kind = kind; }
    }

    /// <summary>Zeigt im Inspector eine feste Auswahl statt eines Textfelds.</summary>
    public sealed class FadenwerkOptionsAttribute : PropertyAttribute
    {
        public readonly string[] options;
        public FadenwerkOptionsAttribute(params string[] options) { this.options = options; }
    }

    [Serializable]
    public class ObjectiveDef
    {
        [Tooltip("talk, kill, collect, reach, deliver oder custom")]
        public string type = FwObjective.Talk;
        [Tooltip("talk: NPC-ID · reach: Marker-ID · collect/deliver: Item-ID · kill/custom: frei wählbare Kennung, die dein Spiel meldet")]
        public string targetId = "";
        public int count = 1;
        [TextArea(1, 3)] public string text = "";
    }

    [Serializable]
    public class QuestDef
    {
        public string id = "";
        public string title = "";
        [TextArea(3, 8)] public string description = "";
        [FadenwerkId(FadenwerkIdKind.Npc)] public string giverNpcId = "";
        [Tooltip("Leer = Abgabe beim Auftraggeber")]
        [FadenwerkId(FadenwerkIdKind.Npc)] public string turnInNpcId = "";
        public int requiredLevel = 1;
        public bool repeatable;
        [FadenwerkId(FadenwerkIdKind.Quest)] public List<string> prerequisiteQuestIds = new List<string>();
        public List<ObjectiveDef> objectives = new List<ObjectiveDef>();
        [FadenwerkId(FadenwerkIdKind.Kit)] public string rewardKitId = "";
        [TextArea(2, 5)] public string offerText = "";
        [TextArea(2, 5)] public string progressText = "";
        [TextArea(2, 5)] public string completeText = "";
    }

    [Serializable]
    public class DialogueChoice
    {
        public string text = "";
        [Tooltip("Leer = Gespräch endet")]
        public string nextNodeId = "";
        [Tooltip("none, offerQuest, completeQuest, giveKit, setFlag oder end")]
        public string action = FwAction.None;
        [Tooltip("Quest-ID, Kit-ID oder Flag-Name – je nach Aktion")]
        public string actionParam = "";
        [Tooltip("Antwort nur zeigen, wenn dieses Flag gesetzt ist (leer = immer)")]
        public string requiresFlag = "";
        [Tooltip("Antwort nur zeigen, wenn diese Quest im gewählten Zustand ist (leer = immer)")]
        [FadenwerkId(FadenwerkIdKind.Quest)] public string conditionQuestId = "";
        [Tooltip("available = annehmbar · active = läuft · ready = abgabebereit · completed = abgeschlossen · notCompleted = noch nie abgeschlossen")]
        [FadenwerkOptions("", "available", "active", "ready", "completed", "notCompleted")]
        public string conditionState = "";
    }

    [Serializable]
    public class DialogueNode
    {
        public string id = "";
        [Tooltip("Leer = der NPC spricht")]
        public string speaker = "";
        [TextArea(2, 6)] public string text = "";
        public List<DialogueChoice> choices = new List<DialogueChoice>();
    }

    [Serializable]
    public class DialogueDef
    {
        public string id = "";
        public string title = "";
        public string startNodeId = "";
        public List<DialogueNode> nodes = new List<DialogueNode>();

        public DialogueNode FindNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId) || nodes == null) return null;
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i] != null && nodes[i].id == nodeId) return nodes[i];
            return null;
        }
    }

    [Serializable]
    public class KitEntry
    {
        [FadenwerkId(FadenwerkIdKind.Item)] public string itemId = "";
        public int minQuantity = 1;
        public int maxQuantity = 1;
        [Tooltip("Gewicht für die Zufallswürfe (ganze Zahl, 0 = nie)")]
        public int weight = 100;
        [Tooltip("Immer enthalten, unabhängig von den Würfen")]
        public bool guaranteed;
    }

    [Serializable]
    public class RewardKitDef
    {
        public string id = "";
        public string name = "";
        public int goldMin;
        public int goldMax;
        public int xpMin;
        public int xpMax;
        [Tooltip("Anzahl der Zufallswürfe über die nicht garantierten Einträge")]
        public int rolls = 1;
        public List<KitEntry> entries = new List<KitEntry>();
    }

    [Serializable]
    public class ItemEntry
    {
        public string id = "";
        public string displayName = "";
        [FadenwerkOptions("weapon", "armor", "consumable", "material", "quest", "misc")]
        public string category = "misc";
        [FadenwerkOptions("common", "uncommon", "rare", "epic", "legendary")]
        public string rarity = "common";
        [TextArea(1, 4)] public string description = "";
        [Tooltip("Dein Unity-Asset: Waffen-Prefab, ScriptableObject usw.")]
        public UnityEngine.Object asset;
    }

    public static class FwUtil
    {
        /// <summary>Macht aus einem Namen eine Kennung aus a-z, 0-9 und Unterstrich.</summary>
        public static string Slug(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            bool lastUnderscore = false;
            string lower = s.ToLowerInvariant();
            for (int i = 0; i < lower.Length; i++)
            {
                char c = lower[i];
                string rep = null;
                if (c == 'ä') rep = "ae";
                else if (c == 'ö') rep = "oe";
                else if (c == 'ü') rep = "ue";
                else if (c == 'ß') rep = "ss";
                if (rep != null) { sb.Append(rep); lastUnderscore = false; continue; }
                bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
                if (ok) { sb.Append(c); lastUnderscore = false; }
                else if (!lastUnderscore && sb.Length > 0) { sb.Append('_'); lastUnderscore = true; }
            }
            return sb.ToString().TrimEnd('_');
        }

        public static string NewId(string prefix, string name)
        {
            string slug = Slug(name);
            if (slug.Length > 24) slug = slug.Substring(0, 24).TrimEnd('_');
            string rnd = Guid.NewGuid().ToString("N").Substring(0, 4);
            return string.IsNullOrEmpty(slug) ? prefix + "_" + rnd : prefix + "_" + slug + "_" + rnd;
        }

        /// <summary>FNV-1a über die Zeichen eines Textes, als 16 Hex-Zeichen. Nur zum Vergleichen von Ständen.</summary>
        public static string Hash64(string s)
        {
            unchecked
            {
                ulong h = 14695981039346656037UL;
                if (s != null)
                {
                    for (int i = 0; i < s.Length; i++)
                    {
                        h ^= s[i];
                        h *= 1099511628211UL;
                    }
                }
                return h.ToString("x16");
            }
        }
    }
}
