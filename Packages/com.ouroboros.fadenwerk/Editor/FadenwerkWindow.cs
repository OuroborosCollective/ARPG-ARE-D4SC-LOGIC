using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEditor;
using UnityEngine;

namespace Fadenwerk.EditorTools
{
    /// <summary>Fenster → Fadenwerk: Datenbank, Web-Server, Szenenübersicht und Prüfliste.</summary>
    public class FadenwerkWindow : EditorWindow
    {
        static readonly string[] Tabs = { "Start", "Szene", "Inhalte", "Prüfen" };
        int _tab;
        Vector2 _scroll;
        bool _showToken;
        List<FwIssue> _issues;
        double _nextIssueRefresh;
        string _hostDraft;

        [MenuItem("Window/Fadenwerk", priority = 2000)]
        [MenuItem("Tools/Fadenwerk/Fenster öffnen", priority = 0)]
        public static void Open()
        {
            var w = GetWindow<FadenwerkWindow>();
            w.titleContent = new GUIContent("Fadenwerk");
            w.minSize = new Vector2(360, 300);
            w.Show();
        }

        void OnEnable() { _hostDraft = FadenwerkSettings.PublicHost; }

        void OnInspectorUpdate()
        {
            // Zähler und Server-Status aktuell halten
            Repaint();
        }

        void OnGUI()
        {
            _tab = GUILayout.Toolbar(_tab, Tabs, GUILayout.Height(24));
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.Space(4);
            switch (_tab)
            {
                case 0: DrawStart(); break;
                case 1: DrawScene(); break;
                case 2: DrawContent(); break;
                case 3: DrawIssues(); break;
            }
            EditorGUILayout.EndScrollView();
        }

        // ---------- Start ----------

        void DrawStart()
        {
            EditorGUILayout.LabelField("Datenbank", EditorStyles.boldLabel);
            FadenwerkDatabase db = FadenwerkEditorData.Database;
            EditorGUI.BeginChangeCheck();
            var picked = (FadenwerkDatabase)EditorGUILayout.ObjectField("Aktive Datenbank", db, typeof(FadenwerkDatabase), false);
            if (EditorGUI.EndChangeCheck()) FadenwerkEditorData.SetDatabase(picked);
            if (db == null)
            {
                EditorGUILayout.HelpBox("Noch keine Datenbank. Sie speichert Quests, Dialoge, Belohnungs-Kits und den Item-Katalog.", MessageType.Info);
                if (GUILayout.Button("Datenbank anlegen", GUILayout.Height(28))) FadenwerkEditorData.CreateDatabase();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Browser-Oberfläche", EditorStyles.boldLabel);
            bool running = FadenwerkServer.IsRunning;
            using (new EditorGUI.DisabledScope(running))
            {
                FadenwerkSettings.Port = EditorGUILayout.IntField(new GUIContent("Port", "1024–65535"), FadenwerkSettings.Port);
                int bind = EditorGUILayout.Popup("Erreichbar über",
                    FadenwerkSettings.BindAll ? 1 : 0, new[] { "Nur lokal (SSH-Tunnel / Proxy) – empfohlen", "Alle Netzwerkschnittstellen" });
                FadenwerkSettings.BindAll = bind == 1;
            }
            FadenwerkSettings.AutoStart = EditorGUILayout.Toggle(new GUIContent("Mit Unity starten"), FadenwerkSettings.AutoStart);
            FadenwerkSettings.AutoSave = EditorGUILayout.Toggle(new GUIContent("Automatisch speichern", "Szenen und Datenbank nach jeder Änderung aus dem Browser sofort speichern"), FadenwerkSettings.AutoSave);

            EditorGUILayout.BeginHorizontal();
            if (!running)
            {
                if (GUILayout.Button("Server starten", GUILayout.Height(30))) FadenwerkServer.Start();
            }
            else
            {
                if (GUILayout.Button("Server stoppen", GUILayout.Height(30))) FadenwerkServer.Stop();
                if (GUILayout.Button("Neu starten", GUILayout.Height(30), GUILayout.Width(100))) FadenwerkServer.Restart();
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(FadenwerkServer.LastError)) EditorGUILayout.HelpBox(FadenwerkServer.LastError, MessageType.Error);
            if (running)
            {
                EditorGUILayout.HelpBox("Läuft · " + FadenwerkServer.RequestCount + " Anfragen" +
                    (string.IsNullOrEmpty(FadenwerkServer.LastClient) ? "" : " · zuletzt von " + FadenwerkServer.LastClient), MessageType.None);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Zugangsschlüssel", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            string token = FadenwerkSettings.Token;
            EditorGUILayout.SelectableLabel(_showToken ? token : new string('•', 16), GUILayout.Height(18));
            if (GUILayout.Button(_showToken ? "Verbergen" : "Zeigen", GUILayout.Width(80))) _showToken = !_showToken;
            if (GUILayout.Button("Neu", GUILayout.Width(50)) &&
                EditorUtility.DisplayDialog("Fadenwerk", "Neuen Schlüssel erzeugen? Geöffnete Browser müssen sich neu verbinden.", "Neu erzeugen", "Abbrechen"))
            {
                FadenwerkSettings.RegenerateToken();
                if (running) FadenwerkServer.Restart();
            }
            EditorGUILayout.EndHorizontal();

            int port = FadenwerkSettings.Port;
            string localUrl = "http://localhost:" + port + "/#t=" + token;
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Adresse auf diesem Rechner", EditorStyles.miniBoldLabel);
            UrlRow(localUrl, true);

            if (FadenwerkSettings.BindAll)
            {
                EditorGUILayout.LabelField("Adressen im Netzwerk", EditorStyles.miniBoldLabel);
                foreach (string ip in LocalIps()) UrlRow("http://" + ip + ":" + port + "/#t=" + token, false);
                EditorGUILayout.BeginHorizontal();
                _hostDraft = EditorGUILayout.TextField(new GUIContent("Eigene Adresse", "z. B. die öffentliche IP oder Domain deines VPS"), _hostDraft);
                if (GUILayout.Button("Übernehmen", GUILayout.Width(90))) FadenwerkSettings.PublicHost = _hostDraft;
                EditorGUILayout.EndHorizontal();
                if (!string.IsNullOrEmpty(FadenwerkSettings.PublicHost))
                    UrlRow("http://" + FadenwerkSettings.PublicHost + ":" + port + "/#t=" + token, false);
                EditorGUILayout.HelpBox("Ohne HTTPS geht der Datenverkehr unverschlüsselt durchs Netz. Port in der Firewall nur für deine eigene IP öffnen – oder lieber den SSH-Tunnel nutzen.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("Vom eigenen PC/Tablet aus per SSH-Tunnel:\nssh -L " + port + ":localhost:" + port + " benutzer@dein-vps\nDanach im Browser die Adresse oben öffnen.", MessageType.Info);
                if (GUILayout.Button("Tunnel-Befehl kopieren"))
                    EditorGUIUtility.systemCopyBuffer = "ssh -N -L " + port + ":localhost:" + port + " benutzer@dein-vps";
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Karte und Szene", EditorStyles.boldLabel);
            int plane = EditorGUILayout.Popup("Bodenebene", FadenwerkSettings.PlaneXY ? 1 : 0, new[] { "XZ – 3D-Spiel", "XY – 2D-Spiel" });
            FadenwerkSettings.PlaneXY = plane == 1;
            using (new EditorGUI.DisabledScope(FadenwerkSettings.PlaneXY))
                FadenwerkSettings.SnapToGround = EditorGUILayout.Toggle(new GUIContent("Auf Boden setzen", "Beim Verschieben aus dem Browser auf Terrain oder Collider stellen"), FadenwerkSettings.SnapToGround);
            EditorGUI.BeginChangeCheck();
            FadenwerkSettings.SceneOverlay = EditorGUILayout.Toggle(new GUIContent("Verbindungen in der Scene View", "Linien von NPCs zu Zielen, Quest-Zielen und Abgabe-NPCs"), FadenwerkSettings.SceneOverlay);
            if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox("Tipp für den VPS: Unity → Edit → Preferences → General → Interaction Mode auf \"No Throttling\" stellen. Sonst antwortet Unity langsam, solange das Fenster nicht im Vordergrund ist.", MessageType.None);
        }

        static void UrlRow(string url, bool local)
        {
            EditorGUILayout.BeginHorizontal();
            string shown = url.Substring(0, url.IndexOf("#t=", System.StringComparison.Ordinal)) + "#t=…";
            EditorGUILayout.SelectableLabel(shown, EditorStyles.textField, GUILayout.Height(18));
            if (GUILayout.Button("Kopieren", GUILayout.Width(70))) EditorGUIUtility.systemCopyBuffer = url;
            if (local && GUILayout.Button("Öffnen", GUILayout.Width(60))) Application.OpenURL(url);
            EditorGUILayout.EndHorizontal();
        }

        static List<string> LocalIps()
        {
            var list = new List<string>();
            try
            {
                foreach (IPAddress a in Dns.GetHostAddresses(Dns.GetHostName()))
                    if (a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a)) list.Add(a.ToString());
            }
            catch (System.Exception) { }
            return list;
        }

        // ---------- Szene ----------

        void DrawScene()
        {
            if (GUILayout.Button("Ausgewählte Objekte zu NPCs machen", GUILayout.Height(26)))
            {
                foreach (GameObject go in Selection.gameObjects)
                {
                    if (!go.scene.IsValid()) continue;
                    FadenwerkNpc npc = go.GetComponent<FadenwerkNpc>();
                    if (npc == null) npc = Undo.AddComponent<FadenwerkNpc>(go);
                    if (string.IsNullOrEmpty(npc.displayName)) npc.displayName = go.name;
                    npc.EnsureId();
                    FadenwerkEditorData.CommitSceneChange(npc);
                }
            }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Marker an Scene-View-Mitte")) CreateMarkerAtView();
            if (GUILayout.Button("Fehlende IDs vergeben")) FixIds();
            EditorGUILayout.EndHorizontal();

            List<FadenwerkNpc> npcs = FadenwerkEditorData.Npcs();
            FadenwerkDatabase db = FadenwerkEditorData.Database;
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("NPCs in den geladenen Szenen (" + npcs.Count + ")", EditorStyles.boldLabel);
            foreach (FadenwerkNpc n in npcs)
            {
                int gives = 0;
                if (db != null) foreach (QuestDef q in db.quests) if (q.giverNpcId == n.npcId) gives++;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(n.Label, GUILayout.MinWidth(80));
                EditorGUILayout.LabelField(gives > 0 ? gives + " Quest(s)" : "–", EditorStyles.miniLabel, GUILayout.Width(70));
                EditorGUILayout.LabelField(string.IsNullOrEmpty(n.dialogueId) ? "kein Dialog" : "Dialog", EditorStyles.miniLabel, GUILayout.Width(70));
                if (GUILayout.Button("Auswählen", GUILayout.Width(80))) SelectAndFrame(n.gameObject);
                EditorGUILayout.EndHorizontal();
            }

            List<FadenwerkMarker> markers = FadenwerkEditorData.Markers();
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Marker (" + markers.Count + ")", EditorStyles.boldLabel);
            foreach (FadenwerkMarker m in markers)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(m.Label, GUILayout.MinWidth(80));
                EditorGUILayout.LabelField(m.kind, EditorStyles.miniLabel, GUILayout.Width(70));
                if (GUILayout.Button("Auswählen", GUILayout.Width(80))) SelectAndFrame(m.gameObject);
                EditorGUILayout.EndHorizontal();
            }
        }

        static void SelectAndFrame(GameObject go)
        {
            Selection.activeGameObject = go;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        }

        static void CreateMarkerAtView()
        {
            SceneView sv = SceneView.lastActiveSceneView;
            Vector3 pos = sv != null ? sv.pivot : Vector3.zero;
            var go = new GameObject("Marker");
            Undo.RegisterCreatedObjectUndo(go, "Fadenwerk: Marker anlegen");
            go.transform.position = pos;
            FadenwerkMarker m = Undo.AddComponent<FadenwerkMarker>(go);
            m.label = "Neuer Marker";
            m.EnsureId();
            FadenwerkEditorData.CommitSceneChange(m);
            Selection.activeGameObject = go;
        }

        static void FixIds()
        {
            int n = 0;
            foreach (FadenwerkNpc npc in FadenwerkEditorData.Npcs())
            {
                Undo.RecordObject(npc, "Fadenwerk: IDs vergeben");
                if (npc.EnsureId()) { n++; FadenwerkEditorData.CommitSceneChange(npc); }
            }
            foreach (FadenwerkMarker m in FadenwerkEditorData.Markers())
            {
                Undo.RecordObject(m, "Fadenwerk: IDs vergeben");
                if (m.EnsureId()) { n++; FadenwerkEditorData.CommitSceneChange(m); }
            }
            EditorUtility.DisplayDialog("Fadenwerk", n == 0 ? "Alle NPCs und Marker haben schon eine ID." : n + " ID(s) vergeben.", "OK");
        }

        // ---------- Inhalte ----------

        void DrawContent()
        {
            FadenwerkDatabase db = FadenwerkEditorData.Database;
            if (db == null) { EditorGUILayout.HelpBox("Keine Datenbank – im Tab \"Start\" anlegen.", MessageType.Info); return; }
            EditorGUILayout.LabelField("Quests", db.quests.Count.ToString());
            EditorGUILayout.LabelField("Dialoge", db.dialogues.Count.ToString());
            EditorGUILayout.LabelField("Belohnungs-Kits", db.kits.Count.ToString());
            EditorGUILayout.LabelField("Items", db.items.Count.ToString());
            EditorGUILayout.Space(6);
            if (GUILayout.Button("Datenbank im Inspector bearbeiten", GUILayout.Height(26)))
            {
                Selection.activeObject = db;
                EditorGUIUtility.PingObject(db);
            }
            EditorGUILayout.HelpBox("Bequemer geht es in der Browser-Oberfläche: Karte, NPC-Texte, Quest-Ziele per Auswahl, Dialog-Vorschau und Probewürfe für Kits.", MessageType.None);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Items aus Ordner übernehmen", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Ordner im Project-Fenster auswählen (z. B. deine Waffen-Prefabs) und unten klicken. Jedes Prefab und ScriptableObject wird ein Katalog-Eintrag mit Verknüpfung zum Asset.", MessageType.None);
            if (GUILayout.Button("Ausgewählten Ordner als Waffen übernehmen")) ImportSelectedFolder(db, "weapon");
            if (GUILayout.Button("Ausgewählten Ordner als Gegenstände übernehmen")) ImportSelectedFolder(db, "misc");

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Beispielinhalte in die aktuelle Szene legen")) FadenwerkSample.Create();
        }

        static void ImportSelectedFolder(FadenwerkDatabase db, string category)
        {
            string folder = null;
            foreach (Object o in Selection.GetFiltered<Object>(SelectionMode.Assets))
            {
                string p = AssetDatabase.GetAssetPath(o);
                if (AssetDatabase.IsValidFolder(p)) { folder = p; break; }
            }
            if (folder == null) { EditorUtility.DisplayDialog("Fadenwerk", "Bitte zuerst im Project-Fenster einen Ordner auswählen.", "OK"); return; }
            var linked = new HashSet<Object>();
            foreach (ItemEntry it in db.items) if (it.asset != null) linked.Add(it.asset);
            var ids = new HashSet<string>();
            foreach (ItemEntry it in db.items) ids.Add(it.id);
            Undo.RecordObject(db, "Fadenwerk: Items importieren");
            int added = 0;
            var guids = new List<string>(AssetDatabase.FindAssets("t:Prefab", new[] { folder }));
            guids.AddRange(AssetDatabase.FindAssets("t:ScriptableObject", new[] { folder }));
            var seen = new HashSet<string>();
            foreach (string g in guids)
            {
                if (!seen.Add(g)) continue;
                Object a = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(g));
                if (a == null || a is FadenwerkDatabase || linked.Contains(a)) continue;
                string baseId = FwUtil.Slug(a.name);
                if (string.IsNullOrEmpty(baseId)) baseId = "item";
                string id = baseId;
                for (int n = 2; ids.Contains(id); n++) id = baseId + "_" + n;
                ids.Add(id);
                db.items.Add(new ItemEntry { id = id, displayName = a.name, category = category, asset = a });
                added++;
            }
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssetIfDirty(db);
            EditorUtility.DisplayDialog("Fadenwerk", added + " Item(s) aus " + folder + " übernommen.", "OK");
        }

        // ---------- Prüfen ----------

        void DrawIssues()
        {
            if (_issues == null || EditorApplication.timeSinceStartup > _nextIssueRefresh)
            {
                _issues = FadenwerkValidator.Run(FadenwerkEditorData.Database, FadenwerkEditorData.Npcs(), FadenwerkEditorData.Markers());
                _nextIssueRefresh = EditorApplication.timeSinceStartup + 2.0;
            }
            if (_issues.Count == 0) { EditorGUILayout.HelpBox("Keine Probleme gefunden.", MessageType.Info); return; }
            foreach (FwIssue i in _issues)
            {
                MessageType t = i.level == "error" ? MessageType.Error : i.level == "warn" ? MessageType.Warning : MessageType.Info;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox(i.message, t);
                if ((i.kind == "npc" || i.kind == "marker") && !string.IsNullOrEmpty(i.id) && GUILayout.Button("Zeigen", GUILayout.Width(60), GUILayout.Height(36)))
                {
                    Component c = i.kind == "npc" ? (Component)FadenwerkEditorData.FindNpc(i.id) : FadenwerkEditorData.FindMarker(i.id);
                    if (c != null) SelectAndFrame(c.gameObject);
                }
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
