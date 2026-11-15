using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Fadenwerk.EditorTools
{
    /// <summary>Auswahlliste für Fadenwerk-IDs (NPCs, Marker, Quests …) im Inspector.</summary>
    [CustomPropertyDrawer(typeof(FadenwerkIdAttribute))]
    internal class FadenwerkIdDrawer : PropertyDrawer
    {
        sealed class Options
        {
            public string[] ids;
            public GUIContent[] labels;
            public double time;
        }

        static readonly Dictionary<FadenwerkIdKind, Options> Cache = new Dictionary<FadenwerkIdKind, Options>();

        static Options Get(FadenwerkIdKind kind)
        {
            Options o;
            if (Cache.TryGetValue(kind, out o) && EditorApplication.timeSinceStartup - o.time < 1.5) return o;
            var ids = new List<string>();
            var labels = new List<string>();
            FadenwerkDatabase db = FadenwerkEditorData.Database;
            switch (kind)
            {
                case FadenwerkIdKind.Npc:
                    foreach (FadenwerkNpc n in FadenwerkEditorData.Npcs()) if (!string.IsNullOrEmpty(n.npcId)) { ids.Add(n.npcId); labels.Add(n.Label + "  (" + n.npcId + ")"); }
                    break;
                case FadenwerkIdKind.Marker:
                    foreach (FadenwerkMarker m in FadenwerkEditorData.Markers()) if (!string.IsNullOrEmpty(m.markerId)) { ids.Add(m.markerId); labels.Add(m.Label + "  (" + m.markerId + ")"); }
                    break;
                case FadenwerkIdKind.Quest:
                    if (db != null) foreach (QuestDef q in db.quests) { ids.Add(q.id); labels.Add((string.IsNullOrEmpty(q.title) ? q.id : q.title) + "  (" + q.id + ")"); }
                    break;
                case FadenwerkIdKind.Dialogue:
                    if (db != null) foreach (DialogueDef d in db.dialogues) { ids.Add(d.id); labels.Add((string.IsNullOrEmpty(d.title) ? d.id : d.title) + "  (" + d.id + ")"); }
                    break;
                case FadenwerkIdKind.Kit:
                    if (db != null) foreach (RewardKitDef k in db.kits) { ids.Add(k.id); labels.Add((string.IsNullOrEmpty(k.name) ? k.id : k.name) + "  (" + k.id + ")"); }
                    break;
                case FadenwerkIdKind.Item:
                    if (db != null) foreach (ItemEntry it in db.items) { ids.Add(it.id); labels.Add((string.IsNullOrEmpty(it.displayName) ? it.id : it.displayName) + "  (" + it.id + ")"); }
                    break;
            }
            o = new Options { time = EditorApplication.timeSinceStartup };
            o.ids = new string[ids.Count + 1];
            o.labels = new GUIContent[ids.Count + 1];
            o.ids[0] = "";
            o.labels[0] = new GUIContent("— keine —");
            for (int i = 0; i < ids.Count; i++)
            {
                o.ids[i + 1] = ids[i];
                // Schrägstriche würden im Popup Untermenüs erzeugen
                o.labels[i + 1] = new GUIContent(labels[i].Replace('/', '∕'));
            }
            Cache[kind] = o;
            return o;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String) { EditorGUI.PropertyField(position, property, label); return; }
            var attr = (FadenwerkIdAttribute)attribute;
            Options o = Get(attr.kind);
            string value = property.stringValue ?? "";
            int index = System.Array.IndexOf(o.ids, value);
            GUIContent[] labels = o.labels;
            string[] ids = o.ids;
            if (index < 0)
            {
                // Unbekannte ID trotzdem anzeigen, damit nichts verloren geht
                labels = new GUIContent[o.labels.Length + 1];
                ids = new string[o.ids.Length + 1];
                System.Array.Copy(o.labels, labels, o.labels.Length);
                System.Array.Copy(o.ids, ids, o.ids.Length);
                labels[labels.Length - 1] = new GUIContent("⚠ " + value + " (nicht gefunden)");
                ids[ids.Length - 1] = value;
                index = ids.Length - 1;
            }
            EditorGUI.BeginProperty(position, label, property);
            int picked = EditorGUI.Popup(position, label, index, labels);
            if (picked != index) property.stringValue = ids[picked];
            EditorGUI.EndProperty();
        }
    }

    /// <summary>Feste Auswahl (z. B. Seltenheit) statt Freitext.</summary>
    [CustomPropertyDrawer(typeof(FadenwerkOptionsAttribute))]
    internal class FadenwerkOptionsDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String) { EditorGUI.PropertyField(position, property, label); return; }
            string[] opts = ((FadenwerkOptionsAttribute)attribute).options;
            string value = property.stringValue ?? "";
            var list = new List<string>(opts);
            if (!list.Contains(value)) list.Add(value);
            var shown = new GUIContent[list.Count];
            for (int i = 0; i < list.Count; i++) shown[i] = new GUIContent(string.IsNullOrEmpty(list[i]) ? "— immer —" : list[i]);
            int index = list.IndexOf(value);
            EditorGUI.BeginProperty(position, label, property);
            int picked = EditorGUI.Popup(position, label, index, shown);
            if (picked != index) property.stringValue = list[picked];
            EditorGUI.EndProperty();
        }
    }

    /// <summary>NPC-Inspector mit Übersicht der verknüpften Quests.</summary>
    [CustomEditor(typeof(FadenwerkNpc))]
    [CanEditMultipleObjects]
    internal class FadenwerkNpcEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (targets.Length != 1) return;
            var npc = (FadenwerkNpc)target;
            FadenwerkDatabase db = FadenwerkEditorData.Database;
            if (db == null) return;
            var gives = new List<string>();
            var takes = new List<string>();
            var talks = new List<string>();
            foreach (QuestDef q in db.quests)
            {
                string t = string.IsNullOrEmpty(q.title) ? q.id : q.title;
                if (q.giverNpcId == npc.npcId) gives.Add(t);
                if (!string.IsNullOrEmpty(q.turnInNpcId) && q.turnInNpcId == npc.npcId) takes.Add(t);
                foreach (ObjectiveDef o in q.objectives) if (o.type == FwObjective.Talk && o.targetId == npc.npcId) { talks.Add(t); break; }
            }
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Fadenwerk-Verknüpfungen", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Vergibt", gives.Count == 0 ? "–" : string.Join(", ", gives.ToArray()), EditorStyles.wordWrappedLabel);
            if (takes.Count > 0) EditorGUILayout.LabelField("Nimmt ab", string.Join(", ", takes.ToArray()), EditorStyles.wordWrappedLabel);
            if (talks.Count > 0) EditorGUILayout.LabelField("Gesprächsziel in", string.Join(", ", talks.ToArray()), EditorStyles.wordWrappedLabel);
        }
    }
}
