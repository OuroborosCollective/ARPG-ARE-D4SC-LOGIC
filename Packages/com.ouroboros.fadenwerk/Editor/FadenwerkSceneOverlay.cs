using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Fadenwerk.EditorTools
{
    /// <summary>
    /// Zeigt in der Scene View, wer mit wem verbunden ist:
    /// NPC → Ziel-Marker (gestrichelt), Auftraggeber → Quest-Ziele, Auftraggeber → Abgabe-NPC.
    /// </summary>
    [InitializeOnLoad]
    internal static class FadenwerkSceneOverlay
    {
        static readonly Color Gold = new Color(0.94f, 0.71f, 0.16f, 0.95f);
        static readonly Color Teal = new Color(0.08f, 0.72f, 0.65f, 0.95f);
        static readonly Color Violet = new Color(0.64f, 0.45f, 0.97f, 0.95f);
        static GUIStyle _label;

        static List<FadenwerkNpc> _npcs = new List<FadenwerkNpc>();
        static List<FadenwerkMarker> _markers = new List<FadenwerkMarker>();
        static double _nextScan;

        static FadenwerkSceneOverlay()
        {
            SceneView.duringSceneGui += OnScene;
        }

        static void OnScene(SceneView view)
        {
            if (!FadenwerkSettings.SceneOverlay || Event.current.type != EventType.Repaint) return;
            if (EditorApplication.timeSinceStartup > _nextScan)
            {
                _npcs = FadenwerkEditorData.Npcs();
                _markers = FadenwerkEditorData.Markers();
                _nextScan = EditorApplication.timeSinceStartup + 1.0;
            }
            if (_label == null)
            {
                _label = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };
                _label.normal.textColor = Color.white;
            }

            var npcById = new Dictionary<string, FadenwerkNpc>();
            foreach (FadenwerkNpc n in _npcs) if (n != null && !string.IsNullOrEmpty(n.npcId)) npcById[n.npcId] = n;
            var markerById = new Dictionary<string, FadenwerkMarker>();
            foreach (FadenwerkMarker m in _markers) if (m != null && !string.IsNullOrEmpty(m.markerId)) markerById[m.markerId] = m;

            Vector3 lift = Vector3.up * 1.8f;
            foreach (FadenwerkNpc n in npcById.Values)
            {
                FadenwerkMarker goal;
                if (!string.IsNullOrEmpty(n.goalMarkerId) && markerById.TryGetValue(n.goalMarkerId, out goal))
                {
                    Handles.color = Teal;
                    Handles.DrawDottedLine(n.transform.position + lift, goal.transform.position, 4f);
                }
                Handles.Label(n.transform.position + lift + Vector3.up * 0.4f, n.Label, _label);
            }
            foreach (FadenwerkMarker m in markerById.Values)
                Handles.Label(m.transform.position + Vector3.up * 0.6f, "◆ " + m.Label, _label);

            FadenwerkDatabase db = FadenwerkEditorData.Database;
            if (db == null) return;
            foreach (QuestDef q in db.quests)
            {
                FadenwerkNpc giver;
                if (string.IsNullOrEmpty(q.giverNpcId) || !npcById.TryGetValue(q.giverNpcId, out giver)) continue;
                Vector3 from = giver.transform.position + lift;
                foreach (ObjectiveDef o in q.objectives)
                {
                    Vector3? to = null;
                    FadenwerkNpc tn; FadenwerkMarker tm;
                    if (o.type == FwObjective.Talk && npcById.TryGetValue(o.targetId ?? "", out tn)) to = tn.transform.position + lift;
                    else if (o.type == FwObjective.Reach && markerById.TryGetValue(o.targetId ?? "", out tm)) to = tm.transform.position;
                    if (!to.HasValue) continue;
                    Handles.color = Gold;
                    Handles.DrawAAPolyLine(3f, from, to.Value);
                    Handles.Label((from + to.Value) * 0.5f, q.title, _label);
                }
                FadenwerkNpc turnIn;
                if (!string.IsNullOrEmpty(q.turnInNpcId) && q.turnInNpcId != q.giverNpcId && npcById.TryGetValue(q.turnInNpcId, out turnIn))
                {
                    Handles.color = Violet;
                    Handles.DrawDottedLine(from, turnIn.transform.position + lift, 6f);
                }
            }
        }
    }
}
