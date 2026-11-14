using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace Fadenwerk.EditorTools
{
    /// <summary>Legt Beispiel-NPCs, Marker, Quests, einen Dialog und Kits an – zum Ausprobieren.</summary>
    internal static class FadenwerkSample
    {
        [MenuItem("Tools/Fadenwerk/Beispielentwurf in eigener Szene", priority = 20)]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var db = FadenwerkEditorData.CreateDatabase();
            CreateAuthoringScene(db);
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("Fadenwerk",
                    "Beispielentwurf in eigener Szene und neuer Datenbank angelegt. " +
                    "Quests und Belohnungen sind dadurch noch nicht im Spiel aktiv.", "OK");
        }

        /// <summary>Create authoring data in a separate scene without any quest or reward runtime.</summary>
        public static GameObject CreateAuthoringScene(FadenwerkDatabase db)
        {
            if (db == null) throw new System.ArgumentNullException(nameof(db));
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("Authoring requires Edit Mode");
            var previous = SceneManager.GetActiveScene();
            const string folder = "Assets/FadenwerkSamples";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "FadenwerkSamples");
            string scenePath = AssetDatabase.GenerateUniqueAssetPath(folder + "/FadenwerkAuthoring.unity");
            if (!AssetDatabase.CopyAsset("Packages/com.ouroboros.fadenwerk/Editor/FadenwerkAuthoring.unity", scenePath))
                throw new System.InvalidOperationException("Authoring scene template is unavailable");
            var authored = default(UnityEngine.SceneManagement.Scene);
            try
            {
                authored = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(authored);
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();

                SceneView sv = SceneView.lastActiveSceneView;
                Vector3 c = sv != null ? sv.pivot : Vector3.zero;
                bool xy = FadenwerkSettings.PlaneXY;
                System.Func<float, float, Vector3> at = (a, b) => xy ? c + new Vector3(a, b, 0f) : c + new Vector3(a, 0f, b);

                var root = new GameObject("Fadenwerk Beispiel");
                Undo.RegisterCreatedObjectUndo(root, "Fadenwerk: Beispiel");
                root.transform.position = c;

                FadenwerkNpc mira = Npc(root, "Mira", "Kräuterhändlerin", at(-4f, 0f),
                    "Die Wiesen hinter der Mühle sind voller Heilkraut, aber seit die Wölfe zurück sind, traut sich niemand mehr hinaus.");
                FadenwerkNpc tarn = Npc(root, "Tarn", "Schmied", at(5f, 2f),
                    "Ohne Erz keine Klingen. Ohne Klingen keine Wache. Und ohne Wache – na, du kannst es dir denken.");
                FadenwerkNpc ole = Npc(root, "Ole", "Torwache", at(0f, 9f),
                    "Halt. Wer nach Norden will, meldet sich bei mir.");
                FadenwerkMarker mill = Marker(root, "Alte Mühle", "ziel", at(-12f, 10f));
                FadenwerkMarker forest = Marker(root, "Waldrand", "ort", at(12f, 14f));
                Marker(root, "Anschlagtafel Dorfplatz", "tafel", at(1f, -3f));
                mira.goalText = "Will ihren Kräutergarten an der Mühle zurück.";
                mira.goalMarkerId = mill.markerId;
                ole.goalText = "Bewacht den Weg zum Waldrand.";
                ole.goalMarkerId = forest.markerId;

                Undo.RecordObject(db, "Fadenwerk: Beispiel");
                AddItem(db, "heilkraut", "Heilkraut", "material", "common");
                AddItem(db, "eisenerz", "Eisenerz", "material", "common");
                AddItem(db, "heiltrank", "Heiltrank", "consumable", "common");
                AddItem(db, "bronzeschwert", "Bronzeschwert", "weapon", "uncommon");
                AddItem(db, "runenklinge", "Runenklinge", "weapon", "rare");

                if (db.FindKit("kit_bsp_kraeuter") == null)
                    db.kits.Add(new RewardKitDef
                    {
                        id = "kit_bsp_kraeuter", name = "Kräuterlohn", goldMin = 10, goldMax = 25, xpMin = 40, xpMax = 60, rolls = 1,
                        entries = new List<KitEntry>
                        {
                            new KitEntry { itemId = "heiltrank", minQuantity = 2, maxQuantity = 2, weight = 0, guaranteed = true },
                            new KitEntry { itemId = "heilkraut", minQuantity = 1, maxQuantity = 3, weight = 80 },
                            new KitEntry { itemId = "runenklinge", minQuantity = 1, maxQuantity = 1, weight = 5 },
                        }
                    });
                if (db.FindKit("kit_bsp_schmied") == null)
                    db.kits.Add(new RewardKitDef
                    {
                        id = "kit_bsp_schmied", name = "Schmiedelohn", goldMin = 20, goldMax = 40, xpMin = 60, xpMax = 90, rolls = 0,
                        entries = new List<KitEntry> { new KitEntry { itemId = "bronzeschwert", minQuantity = 1, maxQuantity = 1, guaranteed = true } }
                    });

                if (db.FindQuest("q_bsp_kraeuter") == null)
                    db.quests.Add(new QuestDef
                    {
                        id = "q_bsp_kraeuter", title = "Kräuter für Mira", giverNpcId = mira.npcId, requiredLevel = 1,
                        description = "Mira braucht Heilkraut von den Wiesen hinter der alten Mühle.",
                        offerText = "Bring mir fünf Heilkräuter von der Mühle – ich zahle gut.",
                        progressText = "Noch nicht genug Kraut? Die Wiesen hinter der Mühle, nicht vergessen.",
                        completeText = "Wunderbar! Damit komme ich über den Winter.",
                        rewardKitId = "kit_bsp_kraeuter",
                        objectives = new List<ObjectiveDef>
                        {
                            new ObjectiveDef { type = FwObjective.Reach, targetId = mill.markerId, count = 1, text = "Zur alten Mühle gehen" },
                            new ObjectiveDef { type = FwObjective.Collect, targetId = "heilkraut", count = 5, text = "Heilkraut sammeln" },
                        }
                    });
                if (db.FindQuest("q_bsp_erz") == null)
                    db.quests.Add(new QuestDef
                    {
                        id = "q_bsp_erz", title = "Erz für die Esse", giverNpcId = tarn.npcId, turnInNpcId = tarn.npcId, requiredLevel = 1,
                        description = "Tarn braucht Eisenerz. Ole an der Torwache weiß, wo es welches gibt.",
                        offerText = "Frag Ole am Tor nach dem alten Stollen und bring mir drei Brocken Eisenerz.",
                        progressText = "Drei Brocken. Nicht zwei.",
                        completeText = "Gutes Erz. Hier, nimm die Klinge – du hast sie dir verdient.",
                        rewardKitId = "kit_bsp_schmied",
                        prerequisiteQuestIds = new List<string> { "q_bsp_kraeuter" },
                        objectives = new List<ObjectiveDef>
                        {
                            new ObjectiveDef { type = FwObjective.Talk, targetId = ole.npcId, count = 1, text = "Mit Ole sprechen" },
                            new ObjectiveDef { type = FwObjective.Collect, targetId = "eisenerz", count = 3, text = "Eisenerz sammeln" },
                        }
                    });

                if (db.FindDialogue("d_bsp_mira") == null)
                {
                    var d = new DialogueDef { id = "d_bsp_mira", title = "Mira – Gespräch", startNodeId = "start" };
                    d.nodes.Add(new DialogueNode
                    {
                        id = "start", text = mira.loreText,
                        choices = new List<DialogueChoice>
                        {
                            new DialogueChoice { text = "Kann ich helfen?", nextNodeId = "angebot", conditionQuestId = "q_bsp_kraeuter", conditionState = "available" },
                            new DialogueChoice { text = "Hier sind deine Kräuter.", nextNodeId = "dank", conditionQuestId = "q_bsp_kraeuter", conditionState = "ready" },
                            new DialogueChoice { text = "Wie läuft es?", nextNodeId = "unterwegs", conditionQuestId = "q_bsp_kraeuter", conditionState = "active" },
                            new DialogueChoice { text = "Auf Wiedersehen.", action = FwAction.End },
                        }
                    });
                    d.nodes.Add(new DialogueNode
                    {
                        id = "angebot", text = "Bring mir fünf Heilkräuter von der Mühle – ich zahle gut.",
                        choices = new List<DialogueChoice>
                        {
                            new DialogueChoice { text = "Abgemacht.", action = FwAction.OfferQuest, actionParam = "q_bsp_kraeuter" },
                            new DialogueChoice { text = "Vielleicht später.", nextNodeId = "start" },
                        }
                    });
                    d.nodes.Add(new DialogueNode
                    {
                        id = "unterwegs", text = "Noch nicht genug Kraut? Die Wiesen hinter der Mühle, nicht vergessen.",
                        choices = new List<DialogueChoice> { new DialogueChoice { text = "Bin dran.", action = FwAction.End } }
                    });
                    d.nodes.Add(new DialogueNode
                    {
                        id = "dank", text = "Wunderbar! Damit komme ich über den Winter.",
                        choices = new List<DialogueChoice> { new DialogueChoice { text = "Gern geschehen.", action = FwAction.CompleteQuest, actionParam = "q_bsp_kraeuter" } }
                    });
                    db.dialogues.Add(d);
                }
                mira.dialogueId = "d_bsp_mira";

                EditorUtility.SetDirty(db);
                if (AssetDatabase.Contains(db)) AssetDatabase.SaveAssetIfDirty(db);
                foreach (Component comp in root.GetComponentsInChildren<Component>(true))
                    if (comp is FadenwerkNpc || comp is FadenwerkMarker) EditorUtility.SetDirty(comp);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
                Undo.CollapseUndoOperations(group);
                Selection.activeGameObject = root;
                return root;
            }
            catch
            {
                if (authored.IsValid() && authored.isLoaded) EditorSceneManager.CloseScene(authored, true);
                AssetDatabase.DeleteAsset(scenePath);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                throw;
            }
        }

        static FadenwerkNpc Npc(GameObject root, string name, string role, Vector3 pos, string lore)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Undo.RegisterCreatedObjectUndo(go, "Fadenwerk: Beispiel");
            go.name = name;
            go.transform.SetParent(root.transform, false);
            go.transform.position = pos + (FadenwerkSettings.PlaneXY ? Vector3.zero : Vector3.up);
            FadenwerkNpc n = go.AddComponent<FadenwerkNpc>();
            n.displayName = name;
            n.role = role;
            n.loreText = lore;
            n.EnsureId();
            return n;
        }

        static FadenwerkMarker Marker(GameObject root, string label, string kind, Vector3 pos)
        {
            var go = new GameObject("Marker " + label);
            Undo.RegisterCreatedObjectUndo(go, "Fadenwerk: Beispiel");
            go.transform.SetParent(root.transform, false);
            go.transform.position = pos;
            FadenwerkMarker m = go.AddComponent<FadenwerkMarker>();
            m.label = label;
            m.kind = kind;
            m.EnsureId();
            return m;
        }

        static void AddItem(FadenwerkDatabase db, string id, string name, string category, string rarity)
        {
            if (db.FindItem(id) != null) return;
            db.items.Add(new ItemEntry { id = id, displayName = name, category = category, rarity = rarity });
        }
    }
}
