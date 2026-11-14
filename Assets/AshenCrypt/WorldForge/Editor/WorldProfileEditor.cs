using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AshenCrypt.WorldForge.EditorTools
{
    /// <summary>Inspector with an add/reorder UI for the polymorphic height-layer stack.</summary>
    [CustomEditor(typeof(WorldProfile))]
    public class WorldProfileEditor : UnityEditor.Editor
    {
        private SerializedProperty _layers;
        private static readonly Dictionary<long, bool> Foldouts = new Dictionary<long, bool>();

        private void OnEnable() { _layers = serializedObject.FindProperty("heightLayers"); }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var profile = (WorldProfile)target;

            EditorGUILayout.HelpBox(
                "World: " + profile.WorldSizeX + " × " + profile.WorldSizeZ + " m, " + profile.tilesX + "×" + profile.tilesZ + " tiles, " +
                profile.SampleSpacing.ToString("0.###") + " m per height sample.", MessageType.None);
            if (GUILayout.Button("Open World Designer", GUILayout.Height(26))) WFWorldDesignerWindow.Open(profile);

            DrawPropertiesExcluding(serializedObject, "m_Script", "heightLayers");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Height stack (evaluated top → bottom)", EditorStyles.boldLabel);
            for (int i = 0; i < _layers.arraySize; i++)
            {
                SerializedProperty el = _layers.GetArrayElementAtIndex(i);
                object value = el.managedReferenceValue;
                string title = value == null ? "<missing type>" : ((WFHeightLayer)value).DisplayName + "  (" + value.GetType().Name + ")";
                long id = el.managedReferenceId;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                bool open;
                if (!Foldouts.TryGetValue(id, out open)) open = false;
                SerializedProperty enabled = value != null ? el.FindPropertyRelative("enabled") : null;
                if (enabled != null) enabled.boolValue = EditorGUILayout.Toggle(enabled.boolValue, GUILayout.Width(16));
                open = EditorGUILayout.Foldout(open, (i + 1) + ". " + title, true);
                Foldouts[id] = open;
                GUI.enabled = i > 0;
                if (GUILayout.Button("▲", GUILayout.Width(24))) { _layers.MoveArrayElement(i, i - 1); EndRow(); break; }
                GUI.enabled = i < _layers.arraySize - 1;
                if (GUILayout.Button("▼", GUILayout.Width(24))) { _layers.MoveArrayElement(i, i + 1); EndRow(); break; }
                GUI.enabled = true;
                if (GUILayout.Button("✕", GUILayout.Width(24))) { _layers.DeleteArrayElementAtIndex(i); EndRow(); break; }
                EditorGUILayout.EndHorizontal();
                if (open && value != null)
                {
                    EditorGUI.indentLevel++;
                    SerializedProperty it = el.Copy();
                    SerializedProperty end = el.GetEndProperty();
                    bool enter = true;
                    while (it.NextVisible(enter) && !SerializedProperty.EqualContents(it, end))
                    {
                        enter = false;
                        if (it.name == "enabled") continue;
                        EditorGUILayout.PropertyField(it, true);
                    }
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("+ Add height layer", GUILayout.Height(22)))
            {
                var menu = new GenericMenu();
                foreach (Type t in TypeCache.GetTypesDerivedFrom<WFHeightLayer>())
                {
                    if (t.IsAbstract) continue;
                    Type captured = t;
                    menu.AddItem(new GUIContent(Pretty(t)), false, () => AddLayer(captured));
                }
                menu.ShowAsContext();
            }
            serializedObject.ApplyModifiedProperties();
        }

        private static void EndRow()
        {
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void AddLayer(Type t)
        {
            serializedObject.Update();
            int i = _layers.arraySize;
            _layers.arraySize++;
            _layers.GetArrayElementAtIndex(i).managedReferenceValue = Activator.CreateInstance(t);
            serializedObject.ApplyModifiedProperties();
        }

        private static string Pretty(Type t)
        {
            string n = t.Name.Replace("WF", "").Replace("Layer", "");
            switch (n)
            {
                case "Noise": case "Stamp": case "ImportRaw": return "Shape/" + n;
                case "HydraulicErosion": case "ThermalErosion": return "Erosion/" + n;
                case "PathCarve": case "FlattenPoi": case "Boundary": return "Gameplay/" + n;
                default: return "Utility/" + n;
            }
        }
    }
}
