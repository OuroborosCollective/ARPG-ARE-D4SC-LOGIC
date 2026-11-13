#if UNITY_EDITOR
using System;
using System.Linq;
using AreArpg.Core;
using AreArpg.UnityClient;
using AshenCrypt.MemoryForge;
using UnityEditor;
using UnityEngine;

namespace AreArpg.EditorTools
{
    /// <summary>Inspects the live Core. Candidate evaluation never changes a bound NPC or the world.</summary>
    public sealed class NpcForgeInspectorWindow : EditorWindow
    {
        private int _selected;
        private Vector2 _scroll;
        private int _candidateOffset;
        private Decision _candidate;
        private string _candidateHash;
        [MenuItem("ARE ARPG/NPC Memory Forge")]
        public static void Open() => GetWindow<NpcForgeInspectorWindow>("NPC Memory Forge");
        private void OnInspectorUpdate(){if(EditorApplication.isPlaying)Repaint();}
        private void OnGUI()
        {
            EditorGUILayout.LabelField("ASHEN CRYPT",EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("NPC Memory Forge",EditorStyles.boldLabel);
            var runtime=UnityEngine.Object.FindFirstObjectByType<ArenaRuntime>();var world=runtime?.Simulation;
            if(world?.Progression.Social.Forge==null||world.Npcs.Count==0)
            {EditorGUILayout.HelpBox("Im Play Mode einen Ort mit NPCs betreten. Angezeigt werden ausschließlich die vorhandenen Core-Daten.",MessageType.Info);return;}
            _selected=Mathf.Clamp(_selected,0,world.Npcs.Count-1);
            int next=EditorGUILayout.Popup("NPC",_selected,world.Npcs.Select(n=>NpcForgeState.Identity(n.Id)+" · "+n.Role).ToArray());
            if(next!=_selected){_candidate=null;_selected=next;}
            var npc=world.Npcs[_selected];var mind=npc.Mind;
            if(mind==null){EditorGUILayout.HelpBox("Für diesen Legacy-NPC ist Forge nicht aktiviert.",MessageType.Info);return;}
            _scroll=EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("Archetyp",mind.Controller.ArchetypeId);
            EditorGUILayout.LabelField("Controller",mind.Controller.ControllerId+" / v"+mind.Controller.Version);
            EditorGUILayout.SelectableLabel(mind.Controller.Hash,EditorStyles.textField,GUILayout.Height(20));
            EditorGUILayout.LabelField("Fähigkeiten",mind.Controller.Archetype.Capabilities.ToString());
            EditorGUILayout.LabelField("Fallback",mind.Controller.Archetype.Fallback.ToString());
            EditorGUILayout.LabelField("Core-Ticks",world.Progression.Social.Forge.ProcessedTicks.ToString());
            EditorGUILayout.LabelField("Zustand",npc.Behavior+" · HP "+npc.Memory.Health+" · Hunger "+npc.Memory.Hunger+" · Müdigkeit "+npc.Memory.Fatigue);
            DrawDecision(mind.DecisionState.LastDecision,mind.DecisionState.LastObservations);
            EditorGUILayout.Space();EditorGUILayout.LabelField("Beziehung zum Spieler",EditorStyles.boldLabel);
            var relationship=mind.RelationshipTo("player");
            foreach(RelationshipAxis axis in Enum.GetValues(typeof(RelationshipAxis)))EditorGUILayout.LabelField(axis.ToString(),relationship[axis].ToString());
            EditorGUILayout.LabelField("Erhaltene Legacy-Konten",npc.Memory.HelpfulActions+" Hilfe / "+npc.Memory.HostileActions+" Übergriffe");
            EditorGUILayout.Space();EditorGUILayout.LabelField("Erinnerungen · letzte 64 Einträge",EditorStyles.boldLabel);
            foreach(var memory in mind.Known.Reverse().Take(64))
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("#"+memory.Event.Id+" · "+memory.Event.Kind+" · "+memory.Event.Context,EditorStyles.boldLabel);
                EditorGUILayout.LabelField(memory.Event.Actor+" → "+memory.Event.Target+" · Tick "+memory.Event.Tick);
                EditorGUILayout.LabelField(memory.Origin+" / Quelle: "+memory.Source+" / "+(memory.IsFact?"direkte Kenntnis":"keine gesicherte Tatsache"));
                EditorGUILayout.LabelField("Relevanz",memory.Relevance(world.Progression.Social.Forge.ProcessedTicks)+" / 10000"+(memory.Event.Essential?" · dauerhaft":""));
                EditorGUILayout.SelectableLabel(memory.Checksum,EditorStyles.miniLabel,GUILayout.Height(18));
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.Space();EditorGUILayout.LabelField("Isolierter Gewichtungskandidat",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Vergleicht dieselben Beobachtungen. Keine Änderung an NPC, Spielstand oder freigegebener Registry. Übernahme erfordert versioniertes Artefakt und Regressionen.",MessageType.None);
            _candidateOffset=EditorGUILayout.IntSlider("Observe: Basisänderung",_candidateOffset,-5000,5000);
            using(new EditorGUI.DisabledScope(mind.DecisionState.LastObservations==null))
            if(GUILayout.Button("Kandidaten auswerten"))
            {
                var rules=mind.Controller.Rules.Select(r=>new UtilityRule(r.Action,r.Order,Mathf.Clamp(r.BasePriority+(r.Action==ForgeAction.Observe?_candidateOffset:0),-10000,10000),Enumerable.Range(0,(int)Feature.Count).Select(i=>r.Weight((Feature)i)).ToArray()));
                var candidate=new ControllerArtifact(mind.Controller.Archetype,mind.Controller.ControllerId,mind.Controller.Version+1,rules,mind.Controller.DecisionInterval,mind.Controller.MinimumDuration,mind.Controller.Hysteresis);
                _candidate=UtilityEvaluator.Evaluate(candidate,mind.DecisionState.LastObservations);_candidateHash=candidate.Hash;
            }
            if(_candidate!=null){EditorGUILayout.LabelField("Kandidat: "+_candidate.Action+" · "+_candidate.Score);EditorGUILayout.SelectableLabel(_candidateHash,EditorStyles.miniLabel,GUILayout.Height(18));}
            EditorGUILayout.EndScrollView();
        }
        private static void DrawDecision(Decision decision,ObservationFrame observations)
        {
            EditorGUILayout.Space();EditorGUILayout.LabelField("Beobachtungen und Entscheidung",EditorStyles.boldLabel);
            if(decision==null){EditorGUILayout.LabelField("Noch keine Entscheidung seit dem Spawn.");return;}
            EditorGUILayout.LabelField(decision.Action+" · "+decision.Reason+" · Wert "+decision.Score);
            for(int i=0;i<(int)Feature.Count;i++)if((observations.PresentFeatures&(1UL<<i))!=0)EditorGUILayout.LabelField(((Feature)i).ToString(),observations[(Feature)i].ToString());
            foreach(var score in decision.Priorities)
            {
                EditorGUILayout.LabelField(score.Action.ToString(),score.Legal?score.Value.ToString():"durch Weltregel ausgeschlossen");
                if(score.Legal)EditorGUILayout.SelectableLabel(score.Contributions,EditorStyles.miniLabel,GUILayout.Height(32));
            }
        }
    }
}
#endif
