using System;
using System.Linq;
using AshenCrypt.MemoryForge.Integration;
using UnityEditor;
using UnityEngine;

namespace AshenCrypt.MemoryForge.Editor
{
    [CustomEditor(typeof(ForgeControllerAsset))]
    public sealed class ForgeControllerAssetEditor : UnityEditor.Editor
    {
        private string _message="";
        public override void OnInspectorGUI()
        {
            var asset=(ForgeControllerAsset)target;
            EditorGUILayout.LabelField("MEMORY FORGE",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Configuration only. Live memories belong to your game. Approved versions must be retained for historical saves.",MessageType.Info);
            DrawDefaultInspector();
            if(GUILayout.Button("Run regression cases"))asset.EvaluateRegressions(out _,out _,out _message);
            if(GUILayout.Button("Review and seal this candidate"))
            {Undo.RecordObject(asset,"Seal Forge controller");asset.ReviewAndSeal(out _message);EditorUtility.SetDirty(asset);}
            if(GUILayout.Button("Fork next version as a new asset"))
            {
                try
                {
                    var candidate=asset.BuildCandidate();var fork=CreateInstance<ForgeControllerAsset>();fork.CopyCandidate(candidate,asset.Regressions.Select(r=>r.Build()));fork.Version=checked(asset.Version+1);
                    string path=AssetDatabase.GenerateUniqueAssetPath(System.IO.Path.ChangeExtension(AssetDatabase.GetAssetPath(asset),null)+"-v"+fork.Version+".asset");
                    AssetDatabase.CreateAsset(fork,path);Selection.activeObject=fork;
                }
                catch(Exception e){_message=e.Message;}
            }
            if(!string.IsNullOrEmpty(_message))EditorGUILayout.HelpBox(_message,MessageType.None);
            EditorGUILayout.LabelField("Reviewed artifact SHA-256");EditorGUILayout.SelectableLabel(asset.ReviewedHash,EditorStyles.textField,GUILayout.Height(20));
            EditorGUILayout.LabelField("Regression receipt SHA-256");EditorGUILayout.SelectableLabel(asset.RegressionHash,EditorStyles.textField,GUILayout.Height(20));
        }
    }
    [CustomEditor(typeof(ForgeNpcView))]
    public sealed class ForgeNpcViewEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint()=>Application.isPlaying;
        public override void OnInspectorGUI()
        {
            var mind=((ForgeNpcView)target).Mind;
            if(mind==null){EditorGUILayout.HelpBox("Your existing simulation must call view.Bind(hostOwnedMind). This component does not create an NPC or a second clock.",MessageType.Info);return;}
            EditorGUILayout.LabelField(mind.Id,EditorStyles.boldLabel);
            EditorGUILayout.LabelField(mind.Controller.ArchetypeId+" · "+mind.Controller.ControllerId+" v"+mind.Controller.Version);
            EditorGUILayout.SelectableLabel(mind.Controller.Hash,EditorStyles.textField,GUILayout.Height(20));
            EditorGUILayout.LabelField("Capabilities",mind.Controller.Archetype.Capabilities.ToString());
            var decision=mind.DecisionState.LastDecision;
            if(decision!=null)
            {
                EditorGUILayout.LabelField("Decision",decision.Action+" / "+decision.Reason);
                foreach(var priority in decision.Priorities)
                {
                    EditorGUILayout.LabelField(priority.Action.ToString(),priority.Legal?priority.Value.ToString():"Forbidden by host/archetype");
                    if(priority.Legal)EditorGUILayout.SelectableLabel(priority.Contributions,EditorStyles.miniLabel,GUILayout.Height(32));
                }
            }
            EditorGUILayout.LabelField("Last 32 memories",EditorStyles.boldLabel);
            foreach(var k in mind.Known.Reverse().Take(32))EditorGUILayout.HelpBox("#"+k.Event.Id+" · "+k.Event.Kind+" · "+k.Event.Context+"\n"+k.Origin+" / source "+k.Source+" / tick "+k.Event.Tick+"\n"+(k.IsFact?"Direct knowledge":"Unconfirmed / refuted"),MessageType.None);
        }
    }
}
