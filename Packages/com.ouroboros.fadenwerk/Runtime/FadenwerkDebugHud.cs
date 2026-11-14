using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Fadenwerk
{
    /// <summary>
    /// Einfache Test-Oberfläche für den Play-Modus: mit NPCs sprechen, Quests annehmen und abgeben, Belohnungen sehen.
    /// Gedacht zum Ausprobieren deiner Inhalte – für das fertige Spiel baust du deine eigene Oberfläche
    /// auf FadenwerkManager und FadenwerkDialogueRunner auf.
    /// </summary>
    [AddComponentMenu("Fadenwerk/Fadenwerk Test-HUD")]
    public class FadenwerkDebugHud : MonoBehaviour
    {
        public KeyCode talkKey = KeyCode.E;
        public bool showQuestLog = true;

        readonly FadenwerkDialogueRunner _runner = new FadenwerkDialogueRunner();
        FadenwerkNpc _talking;
        FadenwerkNpc _near;
        Transform _player;
        float _nextPlayerSearch;
        string _toast = "";
        float _toastUntil;
        Vector2 _scroll;
        FadenwerkManager _hooked;

        void Update()
        {
            FadenwerkManager mgr = FadenwerkManager.Instance;
            if (mgr != _hooked)
            {
                Unhook();
                if (mgr != null)
                {
                    mgr.RewardGranted += OnReward;
                    mgr.QuestAccepted += OnAccepted;
                    mgr.QuestReadyToTurnIn += OnReady;
                    _hooked = mgr;
                }
            }
            _near = FindNearest();
        }

        void OnDestroy() { Unhook(); }

        void Unhook()
        {
            if (_hooked == null) return;
            _hooked.RewardGranted -= OnReward;
            _hooked.QuestAccepted -= OnAccepted;
            _hooked.QuestReadyToTurnIn -= OnReady;
            _hooked = null;
        }

        void OnReward(RewardResult r) { Toast("Belohnung: " + Describe(r)); }
        void OnAccepted(QuestDef q) { Toast("Quest angenommen: " + q.title); }
        void OnReady(QuestDef q) { Toast("Ziele erfüllt: " + q.title); }

        void Toast(string text)
        {
            _toast = text;
            _toastUntil = Time.unscaledTime + 4f;
        }

        public static string Describe(RewardResult r)
        {
            if (r == null) return "nichts";
            var sb = new StringBuilder();
            if (r.gold > 0) sb.Append(r.gold).Append(" Gold");
            if (r.xp > 0) { if (sb.Length > 0) sb.Append(", "); sb.Append(r.xp).Append(" XP"); }
            for (int i = 0; i < r.items.Count; i++)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(r.items[i].quantity).Append("× ").Append(r.items[i].displayName);
            }
            return sb.Length > 0 ? sb.ToString() : "nichts";
        }

        Vector3 ReferencePosition(out bool hasPlayer)
        {
            hasPlayer = false;
            if (_player == null && Time.unscaledTime >= _nextPlayerSearch)
            {
                _nextPlayerSearch = Time.unscaledTime + 1f;
                GameObject go = GameObject.FindWithTag("Player");
                if (go != null) _player = go.transform;
            }
            if (_player != null) { hasPlayer = true; return _player.position; }
            Camera cam = Camera.main;
            return cam != null ? cam.transform.position : Vector3.zero;
        }

        FadenwerkNpc FindNearest()
        {
            bool hasPlayer;
            Vector3 p = ReferencePosition(out hasPlayer);
            FadenwerkNpc best = null;
            float bestDist = float.MaxValue;
            List<FadenwerkNpc> list = FadenwerkNpc.Active;
            for (int i = 0; i < list.Count; i++)
            {
                FadenwerkNpc n = list[i];
                if (n == null) continue;
                float d = (n.transform.position - p).magnitude;
                // Ohne Spieler-Objekt (Tag "Player") gilt der NPC, der der Kamera am nächsten ist.
                if (hasPlayer && d > Mathf.Max(0.5f, n.interactRadius)) continue;
                if (d < bestDist) { bestDist = d; best = n; }
            }
            return best;
        }

        void Open(FadenwerkNpc npc)
        {
            FadenwerkManager mgr = FadenwerkManager.Instance;
            if (mgr == null || npc == null) return;
            mgr.ReportTalk(npc.npcId);
            _scroll = Vector2.zero;
            if (!string.IsNullOrEmpty(npc.dialogueId) && _runner.Start(mgr, npc.dialogueId, npc)) { _talking = null; return; }
            _talking = npc;
        }

        void OnGUI()
        {
            FadenwerkManager mgr = FadenwerkManager.Instance;
            float scale = Mathf.Max(1f, Screen.height / 720f);
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale;
            float h = Screen.height / scale;

            if (mgr == null)
            {
                GUI.Label(new Rect(10, 10, 700, 24), "Fadenwerk: Es liegt kein FadenwerkManager in der Szene.");
                GUI.matrix = oldMatrix;
                return;
            }

            bool panelOpen = _runner.IsRunning || _talking != null;
            Event e = Event.current;
            if (!panelOpen && _near != null && e.type == EventType.KeyDown && e.keyCode == talkKey)
            {
                Open(_near);
                e.Use();
                panelOpen = true;
            }

            if (showQuestLog) DrawQuestLog(mgr);

            if (panelOpen) DrawPanel(mgr, w, h);
            else if (_near != null)
            {
                var r = new Rect(w * 0.5f - 150f, h - 70f, 300f, 44f);
                if (GUI.Button(r, "Sprechen: " + _near.Label + "  [" + talkKey + "]")) Open(_near);
            }

            if (!string.IsNullOrEmpty(_toast) && Time.unscaledTime < _toastUntil)
                GUI.Box(new Rect(w * 0.5f - 260f, 14f, 520f, 30f), _toast);

            GUI.matrix = oldMatrix;
        }

        void DrawQuestLog(FadenwerkManager mgr)
        {
            List<QuestDef> active = mgr.GetActiveQuests();
            if (active.Count == 0) return;
            GUILayout.BeginArea(new Rect(10, 10, 340, 360), GUI.skin.box);
            GUILayout.Label("Quests  (+1 = Fortschritt zum Testen melden)");
            for (int i = 0; i < active.Count; i++)
            {
                QuestDef q = active[i];
                GUILayout.Label((mgr.IsReadyToTurnIn(q.id) ? "✓ " : "• ") + q.title);
                for (int k = 0; k < q.objectives.Count; k++)
                {
                    ObjectiveDef o = q.objectives[k];
                    string text = string.IsNullOrEmpty(o.text) ? o.type + " " + o.targetId : o.text;
                    int have = mgr.GetProgress(q.id, k), need = Mathf.Max(1, o.count);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("    " + text + "  " + have + "/" + need);
                    if (have < need && GUILayout.Button("+1", GUILayout.Width(36)))
                    {
                        mgr.Report(o.type, o.targetId, 1);
                        GUILayout.EndHorizontal();
                        break;
                    }
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndArea();
        }

        void DrawPanel(FadenwerkManager mgr, float w, float h)
        {
            float pw = Mathf.Min(620f, w - 20f);
            float ph = Mathf.Min(360f, h - 20f);
            GUILayout.BeginArea(new Rect((w - pw) * 0.5f, h - ph - 10f, pw, ph), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);

            if (_runner.IsRunning)
            {
                GUILayout.Label(_runner.SpeakerName());
                GUILayout.Label(_runner.Node.text);
                GUILayout.Space(6f);
                List<DialogueChoice> choices = _runner.VisibleChoices();
                if (choices.Count == 0)
                {
                    if (GUILayout.Button("Beenden", GUILayout.Height(34f))) _runner.End();
                }
                else
                {
                    for (int i = 0; i < choices.Count; i++)
                    {
                        if (GUILayout.Button(choices[i].text, GUILayout.Height(34f)))
                        {
                            _runner.Choose(choices[i]);
                            break;
                        }
                    }
                }
            }
            else if (_talking != null)
            {
                FadenwerkNpc npc = _talking;
                GUILayout.Label(npc.Label + (string.IsNullOrEmpty(npc.role) ? "" : "  ·  " + npc.role));
                if (!string.IsNullOrEmpty(npc.loreText)) GUILayout.Label(npc.loreText);
                GUILayout.Space(6f);

                List<QuestDef> turnIns = mgr.GetTurnInsFor(npc.npcId);
                for (int i = 0; i < turnIns.Count; i++)
                {
                    QuestDef q = turnIns[i];
                    bool ready = mgr.IsReadyToTurnIn(q.id);
                    GUILayout.Label(q.title + ": " + (ready ? q.completeText : q.progressText));
                    if (ready && GUILayout.Button("Abschließen: " + q.title, GUILayout.Height(34f)))
                    {
                        mgr.CompleteQuest(q.id);
                        break;
                    }
                }

                List<QuestDef> offers = mgr.GetOffersFor(npc.npcId);
                for (int i = 0; i < offers.Count; i++)
                {
                    QuestDef q = offers[i];
                    GUILayout.Label(q.title + ": " + (string.IsNullOrEmpty(q.offerText) ? q.description : q.offerText));
                    if (GUILayout.Button("Annehmen: " + q.title, GUILayout.Height(34f)))
                    {
                        mgr.AcceptQuest(q.id);
                        break;
                    }
                }

                GUILayout.Space(6f);
                if (GUILayout.Button("Schließen", GUILayout.Height(34f))) _talking = null;
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
