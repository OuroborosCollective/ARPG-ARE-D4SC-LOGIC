using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Fadenwerk.EditorTools
{
    internal sealed class FwIssue
    {
        /// <summary>error, warn oder info</summary>
        public string level;
        public string message;
        /// <summary>npc, marker, quest, dialogue, kit, item oder leer</summary>
        public string kind;
        public string id;

        public FwIssue(string level, string message, string kind, string id)
        {
            this.level = level; this.message = message; this.kind = kind; this.id = id;
        }
    }

    /// <summary>Prüft Datenbank und Szene. Dieselbe Liste erscheint im Unity-Fenster und im Browser.</summary>
    internal static class FadenwerkValidator
    {
        static readonly Regex IdRe = new Regex("^[A-Za-z0-9_.:-]+$");
        static readonly HashSet<string> ObjTypes = new HashSet<string> { "talk", "kill", "collect", "reach", "deliver", "custom" };
        static readonly HashSet<string> Actions = new HashSet<string> { "", "none", "offerQuest", "completeQuest", "giveKit", "setFlag", "end" };
        static readonly HashSet<string> States = new HashSet<string> { "", "available", "active", "ready", "completed", "notCompleted" };

        public static List<FwIssue> Run(FadenwerkDatabase db, List<FadenwerkNpc> npcs, List<FadenwerkMarker> markers)
        {
            var o = new List<FwIssue>();
            if (db == null)
            {
                o.Add(new FwIssue("error", "Keine Fadenwerk-Datenbank – im Unity-Fenster \"Datenbank anlegen\" wählen.", "", ""));
                return o;
            }

            var npcIds = new HashSet<string>();
            foreach (FadenwerkNpc n in npcs)
            {
                if (string.IsNullOrEmpty(n.npcId)) { o.Add(new FwIssue("error", "NPC \"" + n.name + "\" hat keine ID.", "npc", "")); continue; }
                if (!npcIds.Add(n.npcId)) o.Add(new FwIssue("error", "NPC-ID doppelt in den geladenen Szenen: " + n.npcId, "npc", n.npcId));
            }
            var markerIds = new HashSet<string>();
            foreach (FadenwerkMarker m in markers)
            {
                if (string.IsNullOrEmpty(m.markerId)) { o.Add(new FwIssue("error", "Marker \"" + m.name + "\" hat keine ID.", "marker", "")); continue; }
                if (!markerIds.Add(m.markerId)) o.Add(new FwIssue("error", "Marker-ID doppelt: " + m.markerId, "marker", m.markerId));
            }

            var questIds = Ids(db.quests, q => q.id, "Quest", "quest", o);
            var dialogueIds = Ids(db.dialogues, d => d.id, "Dialog", "dialogue", o);
            var kitIds = Ids(db.kits, k => k.id, "Kit", "kit", o);
            var itemIds = Ids(db.items, it => it.id, "Item", "item", o);

            foreach (FadenwerkNpc n in npcs)
            {
                if (string.IsNullOrEmpty(n.npcId)) continue;
                string tag = "NPC \"" + n.Label + "\"";
                if (!string.IsNullOrEmpty(n.dialogueId) && !dialogueIds.Contains(n.dialogueId))
                    o.Add(new FwIssue("error", tag + ": Dialog \"" + n.dialogueId + "\" existiert nicht.", "npc", n.npcId));
                if (!string.IsNullOrEmpty(n.goalMarkerId) && !markerIds.Contains(n.goalMarkerId))
                    o.Add(new FwIssue("warn", tag + ": Ziel-Marker \"" + n.goalMarkerId + "\" ist in keiner geladenen Szene.", "npc", n.npcId));
            }

            foreach (QuestDef q in db.quests)
            {
                string tag = "Quest \"" + (string.IsNullOrEmpty(q.title) ? q.id : q.title) + "\"";
                if (string.IsNullOrEmpty(q.title)) o.Add(new FwIssue("warn", tag + ": Titel fehlt.", "quest", q.id));
                if (string.IsNullOrEmpty(q.giverNpcId)) o.Add(new FwIssue("warn", tag + ": kein Auftraggeber – niemand bietet sie an.", "quest", q.id));
                else if (!npcIds.Contains(q.giverNpcId)) o.Add(new FwIssue("info", tag + ": Auftraggeber \"" + q.giverNpcId + "\" ist in keiner geladenen Szene.", "quest", q.id));
                if (!string.IsNullOrEmpty(q.turnInNpcId) && !npcIds.Contains(q.turnInNpcId))
                    o.Add(new FwIssue("info", tag + ": Abgabe-NPC \"" + q.turnInNpcId + "\" ist in keiner geladenen Szene.", "quest", q.id));
                foreach (string pre in q.prerequisiteQuestIds)
                {
                    if (string.IsNullOrEmpty(pre)) continue;
                    if (pre == q.id) o.Add(new FwIssue("error", tag + ": setzt sich selbst voraus.", "quest", q.id));
                    else if (!questIds.Contains(pre)) o.Add(new FwIssue("error", tag + ": Voraussetzung \"" + pre + "\" existiert nicht.", "quest", q.id));
                }
                if (q.objectives.Count == 0) o.Add(new FwIssue("warn", tag + ": keine Ziele – sie ist sofort abgabebereit.", "quest", q.id));
                for (int i = 0; i < q.objectives.Count; i++)
                {
                    ObjectiveDef ob = q.objectives[i];
                    string ot = tag + ", Ziel " + (i + 1);
                    if (!ObjTypes.Contains(ob.type)) { o.Add(new FwIssue("error", ot + ": unbekannte Art \"" + ob.type + "\".", "quest", q.id)); continue; }
                    if (ob.count < 1) o.Add(new FwIssue("error", ot + ": Anzahl muss mindestens 1 sein.", "quest", q.id));
                    if (string.IsNullOrEmpty(ob.targetId)) { o.Add(new FwIssue("error", ot + ": Ziel-Kennung fehlt.", "quest", q.id)); continue; }
                    if (ob.type == "talk" && !npcIds.Contains(ob.targetId))
                        o.Add(new FwIssue("warn", ot + ": NPC \"" + ob.targetId + "\" ist in keiner geladenen Szene.", "quest", q.id));
                    if (ob.type == "reach" && !markerIds.Contains(ob.targetId))
                        o.Add(new FwIssue("warn", ot + ": Marker \"" + ob.targetId + "\" ist in keiner geladenen Szene.", "quest", q.id));
                    if ((ob.type == "collect" || ob.type == "deliver") && !itemIds.Contains(ob.targetId))
                        o.Add(new FwIssue("error", ot + ": Item \"" + ob.targetId + "\" fehlt im Item-Katalog.", "quest", q.id));
                }
                if (!string.IsNullOrEmpty(q.rewardKitId) && !kitIds.Contains(q.rewardKitId))
                    o.Add(new FwIssue("error", tag + ": Belohnungs-Kit \"" + q.rewardKitId + "\" existiert nicht.", "quest", q.id));
            }

            foreach (DialogueDef d in db.dialogues)
            {
                string tag = "Dialog \"" + (string.IsNullOrEmpty(d.title) ? d.id : d.title) + "\"";
                if (d.nodes.Count == 0) { o.Add(new FwIssue("error", tag + ": hat keine Knoten.", "dialogue", d.id)); continue; }
                var nodeIds = new HashSet<string>();
                foreach (DialogueNode n in d.nodes)
                {
                    if (string.IsNullOrEmpty(n.id)) o.Add(new FwIssue("error", tag + ": ein Knoten hat keine ID.", "dialogue", d.id));
                    else if (!nodeIds.Add(n.id)) o.Add(new FwIssue("error", tag + ": Knoten-ID doppelt: " + n.id, "dialogue", d.id));
                }
                if (string.IsNullOrEmpty(d.startNodeId) || !nodeIds.Contains(d.startNodeId))
                    o.Add(new FwIssue("warn", tag + ": Startknoten fehlt – es beginnt mit dem ersten Knoten.", "dialogue", d.id));
                foreach (DialogueNode n in d.nodes)
                {
                    foreach (DialogueChoice c in n.choices)
                    {
                        string ct = tag + ", Knoten \"" + n.id + "\", Antwort \"" + c.text + "\"";
                        if (!string.IsNullOrEmpty(c.nextNodeId) && !nodeIds.Contains(c.nextNodeId))
                            o.Add(new FwIssue("error", ct + ": Folgeknoten \"" + c.nextNodeId + "\" existiert nicht.", "dialogue", d.id));
                        string a = c.action ?? "";
                        if (!Actions.Contains(a)) { o.Add(new FwIssue("error", ct + ": unbekannte Aktion \"" + a + "\".", "dialogue", d.id)); continue; }
                        if ((a == "offerQuest" || a == "completeQuest") && !questIds.Contains(c.actionParam))
                            o.Add(new FwIssue("error", ct + ": Quest \"" + c.actionParam + "\" existiert nicht.", "dialogue", d.id));
                        if (a == "giveKit" && !kitIds.Contains(c.actionParam))
                            o.Add(new FwIssue("error", ct + ": Kit \"" + c.actionParam + "\" existiert nicht.", "dialogue", d.id));
                        if (a == "setFlag" && string.IsNullOrEmpty(c.actionParam))
                            o.Add(new FwIssue("error", ct + ": Flag-Name fehlt.", "dialogue", d.id));
                        if (!States.Contains(c.conditionState ?? ""))
                            o.Add(new FwIssue("error", ct + ": unbekannter Quest-Zustand \"" + c.conditionState + "\".", "dialogue", d.id));
                        else if (!string.IsNullOrEmpty(c.conditionState) && !questIds.Contains(c.conditionQuestId))
                            o.Add(new FwIssue("error", ct + ": Bedingungs-Quest \"" + c.conditionQuestId + "\" existiert nicht.", "dialogue", d.id));
                    }
                }
            }

            foreach (RewardKitDef k in db.kits)
            {
                string tag = "Kit \"" + (string.IsNullOrEmpty(k.name) ? k.id : k.name) + "\"";
                if (k.goldMin > k.goldMax) o.Add(new FwIssue("error", tag + ": Gold min > max.", "kit", k.id));
                if (k.xpMin > k.xpMax) o.Add(new FwIssue("error", tag + ": XP min > max.", "kit", k.id));
                if (k.goldMin < 0 || k.xpMin < 0) o.Add(new FwIssue("error", tag + ": Gold und XP dürfen nicht negativ sein.", "kit", k.id));
                bool anyWeighted = false;
                for (int i = 0; i < k.entries.Count; i++)
                {
                    KitEntry e = k.entries[i];
                    string et = tag + ", Eintrag " + (i + 1);
                    if (!itemIds.Contains(e.itemId)) o.Add(new FwIssue("error", et + ": Item \"" + e.itemId + "\" fehlt im Katalog.", "kit", k.id));
                    if (e.minQuantity > e.maxQuantity) o.Add(new FwIssue("error", et + ": Menge min > max.", "kit", k.id));
                    if (e.weight < 0) o.Add(new FwIssue("error", et + ": Gewicht darf nicht negativ sein.", "kit", k.id));
                    if (!e.guaranteed && e.weight > 0) anyWeighted = true;
                }
                if (k.rolls > 0 && !anyWeighted && k.entries.Count > 0)
                    o.Add(new FwIssue("warn", tag + ": " + k.rolls + " Würfe, aber kein Eintrag mit Gewicht – es wird nichts gewürfelt.", "kit", k.id));
                if (k.entries.Count == 0 && k.goldMax <= 0 && k.xpMax <= 0)
                    o.Add(new FwIssue("warn", tag + ": ist leer.", "kit", k.id));
            }

            foreach (ItemEntry it in db.items)
            {
                if (it.asset == null && !string.IsNullOrEmpty(it.id))
                    o.Add(new FwIssue("info", "Item \"" + (string.IsNullOrEmpty(it.displayName) ? it.id : it.displayName) + "\": kein Unity-Asset verknüpft.", "item", it.id));
            }
            return o;
        }

        static HashSet<string> Ids<T>(List<T> list, System.Func<T, string> getId, string label, string kind, List<FwIssue> o)
        {
            var set = new HashSet<string>();
            foreach (T x in list)
            {
                string id = getId(x);
                if (string.IsNullOrEmpty(id)) { o.Add(new FwIssue("error", label + " ohne ID.", kind, "")); continue; }
                if (!IdRe.IsMatch(id)) o.Add(new FwIssue("error", label + "-ID \"" + id + "\": nur Buchstaben, Ziffern und _ . : - erlaubt.", kind, id));
                if (!set.Add(id)) o.Add(new FwIssue("error", label + "-ID doppelt: " + id, kind, id));
            }
            return set;
        }
    }
}
