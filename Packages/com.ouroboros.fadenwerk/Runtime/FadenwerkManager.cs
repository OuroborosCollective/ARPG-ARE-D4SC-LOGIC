using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Fadenwerk
{
    [Serializable]
    public class QuestState
    {
        public string questId = "";
        /// <summary>"active" oder "completed"</summary>
        public string status = "active";
        public List<int> progress = new List<int>();
        public int completions;
    }

    [Serializable]
    public class QuestSaveData
    {
        public int playerLevel = 1;
        public List<QuestState> quests = new List<QuestState>();
        public List<string> flags = new List<string>();
        // Local one-shot claim ledger; final grant must still be committed atomically by AreArpg.Core.
        public List<string> claimedKitKeys = new List<string>();
    }

    /// <summary>
    /// Laufzeit-Zentrale für dein Spiel: Quests annehmen, Fortschritt melden, abschließen, Belohnungen würfeln.
    /// Einmal in die Start-Szene legen und die Datenbank zuweisen. Arbeitet komplett offline.
    /// </summary>
    [AddComponentMenu("Fadenwerk/Fadenwerk Manager")]
    public class FadenwerkManager : MonoBehaviour
    {
        public static FadenwerkManager Instance { get; private set; }

        public FadenwerkDatabase database;
        public int playerLevel = 1;
        public bool dontDestroyOnLoad = true;

        public event Action<QuestDef> QuestAccepted;
        /// <summary>Quest, Index des Ziels, aktueller Stand, benötigter Stand.</summary>
        public event Action<QuestDef, int, int, int> ObjectiveProgressed;
        public event Action<QuestDef> QuestReadyToTurnIn;
        public event Action<QuestDef, RewardResult> QuestCompleted;
        /// <summary>Hier gibst du dem Spieler Gold, XP und Gegenstände.</summary>
        public event Action<RewardResult> RewardGranted;

        public UnityEvent<string> onQuestAccepted = new UnityEvent<string>();
        public UnityEvent<string> onQuestCompleted = new UnityEvent<string>();

        QuestSaveData _data = new QuestSaveData();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _data.playerLevel = playerLevel;
            if (dontDestroyOnLoad && transform.parent == null) DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---------- Abfragen ----------

        public QuestState GetState(string questId)
        {
            for (int i = 0; i < _data.quests.Count; i++)
                if (_data.quests[i].questId == questId) return _data.quests[i];
            return null;
        }

        public bool IsActive(string questId)
        {
            QuestState s = GetState(questId);
            return s != null && s.status == "active";
        }

        public bool IsCompleted(string questId)
        {
            QuestState s = GetState(questId);
            return s != null && s.completions > 0;
        }

        public bool IsReadyToTurnIn(string questId)
        {
            if (database == null) return false;
            QuestDef q = database.FindQuest(questId);
            QuestState s = GetState(questId);
            if (q == null || s == null || s.status != "active") return false;
            for (int i = 0; i < q.objectives.Count; i++)
            {
                int need = Mathf.Max(1, q.objectives[i].count);
                int have = i < s.progress.Count ? s.progress[i] : 0;
                if (have < need) return false;
            }
            return true;
        }

        public bool CanAccept(string questId)
        {
            if (database == null) return false;
            QuestDef q = database.FindQuest(questId);
            if (q == null) return false;
            QuestState s = GetState(questId);
            if (s != null && s.status == "active") return false;
            if (s != null && s.completions > 0 && !q.repeatable) return false;
            if (playerLevel < q.requiredLevel) return false;
            for (int i = 0; i < q.prerequisiteQuestIds.Count; i++)
            {
                string pre = q.prerequisiteQuestIds[i];
                if (!string.IsNullOrEmpty(pre) && !IsCompleted(pre)) return false;
            }
            return true;
        }

        /// <summary>Quests, die dieser NPC gerade anbieten kann.</summary>
        public List<QuestDef> GetOffersFor(string npcId)
        {
            var list = new List<QuestDef>();
            if (database == null || string.IsNullOrEmpty(npcId)) return list;
            for (int i = 0; i < database.quests.Count; i++)
            {
                QuestDef q = database.quests[i];
                if (q != null && q.giverNpcId == npcId && CanAccept(q.id)) list.Add(q);
            }
            return list;
        }

        /// <summary>Aktive Quests, die bei diesem NPC abgegeben werden.</summary>
        public List<QuestDef> GetTurnInsFor(string npcId)
        {
            var list = new List<QuestDef>();
            if (database == null || string.IsNullOrEmpty(npcId)) return list;
            for (int i = 0; i < database.quests.Count; i++)
            {
                QuestDef q = database.quests[i];
                if (q == null || !IsActive(q.id)) continue;
                string turnIn = string.IsNullOrEmpty(q.turnInNpcId) ? q.giverNpcId : q.turnInNpcId;
                if (turnIn == npcId) list.Add(q);
            }
            return list;
        }

        public List<QuestDef> GetActiveQuests()
        {
            var list = new List<QuestDef>();
            if (database == null) return list;
            for (int i = 0; i < _data.quests.Count; i++)
            {
                if (_data.quests[i].status != "active") continue;
                QuestDef q = database.FindQuest(_data.quests[i].questId);
                if (q != null) list.Add(q);
            }
            return list;
        }

        public int GetProgress(string questId, int objectiveIndex)
        {
            QuestState s = GetState(questId);
            if (s == null || objectiveIndex < 0 || objectiveIndex >= s.progress.Count) return 0;
            return s.progress[objectiveIndex];
        }

        /// <summary>
        /// Prüft einen Quest-Zustand: available (annehmbar), active (läuft, Ziele offen), ready (abgabebereit),
        /// completed (mindestens einmal abgeschlossen und nicht erneut aktiv), notCompleted (noch nie abgeschlossen).
        /// </summary>
        public bool CheckQuestState(string questId, string state)
        {
            switch (state)
            {
                case "available": return CanAccept(questId);
                case "active": return IsActive(questId) && !IsReadyToTurnIn(questId);
                case "ready": return IsReadyToTurnIn(questId);
                case "completed": return IsCompleted(questId) && !IsActive(questId);
                case "notCompleted": return !IsCompleted(questId);
                default: return false; // unknown conditions must not authorize a reward
            }
        }

        public bool HasFlag(string flag)
        {
            return !string.IsNullOrEmpty(flag) && _data.flags.Contains(flag);
        }

        public void SetFlag(string flag)
        {
            if (!string.IsNullOrEmpty(flag) && !_data.flags.Contains(flag)) _data.flags.Add(flag);
        }

        // ---------- Aktionen ----------

        public bool AcceptQuest(string questId)
        {
            if (!CanAccept(questId)) return false;
            QuestDef q = database.FindQuest(questId);
            QuestState s = GetState(questId);
            if (s == null)
            {
                s = new QuestState();
                s.questId = questId;
                _data.quests.Add(s);
            }
            s.status = "active";
            s.progress.Clear();
            for (int i = 0; i < q.objectives.Count; i++) s.progress.Add(0);
            if (QuestAccepted != null) QuestAccepted(q);
            onQuestAccepted.Invoke(q.id);
            if (q.objectives.Count == 0 && QuestReadyToTurnIn != null) QuestReadyToTurnIn(q);
            return true;
        }

        /// <summary>
        /// Meldet Fortschritt. type ist talk, kill, collect, reach, deliver oder custom; targetId die Kennung aus dem Quest-Ziel.
        /// Gibt true zurück, wenn mindestens ein Ziel weitergezählt wurde.
        /// </summary>
        public bool Report(string type, string targetId, int amount)
        {
            if (database == null || string.IsNullOrEmpty(type) || string.IsNullOrEmpty(targetId) || amount <= 0) return false;
            bool any = false;
            for (int k = 0; k < _data.quests.Count; k++)
            {
                QuestState s = _data.quests[k];
                if (s.status != "active") continue;
                QuestDef q = database.FindQuest(s.questId);
                if (q == null) continue;
                bool wasReady = IsReadyToTurnIn(q.id);
                bool changed = false;
                for (int i = 0; i < q.objectives.Count; i++)
                {
                    ObjectiveDef o = q.objectives[i];
                    if (o.type != type || o.targetId != targetId) continue;
                    while (s.progress.Count <= i) s.progress.Add(0);
                    int need = Mathf.Max(1, o.count);
                    if (s.progress[i] >= need) continue;
                    s.progress[i] = Mathf.Min(need, s.progress[i] + amount);
                    changed = true;
                    if (ObjectiveProgressed != null) ObjectiveProgressed(q, i, s.progress[i], need);
                }
                if (changed)
                {
                    any = true;
                    if (!wasReady && IsReadyToTurnIn(q.id) && QuestReadyToTurnIn != null) QuestReadyToTurnIn(q);
                }
            }
            return any;
        }

        public bool ReportTalk(string npcId) { return Report(FwObjective.Talk, npcId, 1); }
        public bool ReportKill(string targetId, int amount = 1) { return Report(FwObjective.Kill, targetId, amount); }
        public bool ReportCollect(string itemId, int amount = 1) { return Report(FwObjective.Collect, itemId, amount); }
        public bool ReportDeliver(string itemId, int amount = 1) { return Report(FwObjective.Deliver, itemId, amount); }
        public bool ReportReach(string markerId) { return Report(FwObjective.Reach, markerId, 1); }
        public bool ReportCustom(string targetId, int amount = 1) { return Report(FwObjective.Custom, targetId, amount); }

        /// <summary>Schließt eine fertige Quest ab und würfelt ihr Belohnungs-Kit. null, wenn die Quest noch nicht fertig ist.</summary>
        public RewardResult CompleteQuest(string questId)
        {
            if (!IsReadyToTurnIn(questId)) return null;
            QuestDef q = database.FindQuest(questId);
            QuestState s = GetState(questId);
            s.status = "completed";
            s.completions++;
            RewardResult reward = null;
            if (!string.IsNullOrEmpty(q.rewardKitId))
            {
                string seed = database.worldSeed + "|quest|" + q.id + "|" + q.rewardKitId + "|" + s.completions;
                reward = FadenwerkRewards.Roll(database, q.rewardKitId, seed);
                if (RewardGranted != null) RewardGranted(reward);
            }
            if (QuestCompleted != null) QuestCompleted(q, reward);
            onQuestCompleted.Invoke(q.id);
            return reward;
        }

        /// <summary>Würfelt ein Kit außerhalb einer Quest, z. B. aus einem Dialog oder einer Truhe. reason unterscheidet die Würfe.</summary>
        public RewardResult GiveKit(string kitId, string reason)
        {
            // A source-unique reason is mandatory for one-shot manual grants.
            // This is defense-in-depth for the standalone package, NOT a substitute
            // for AreArpg.Core's atomic quest/inventory/save transaction.
            if (string.IsNullOrEmpty(reason) || database == null || database.FindKit(kitId) == null) return null;
            if (_data.claimedKitKeys == null) _data.claimedKitKeys = new List<string>();
            string claimKey = kitId.Length + ":" + kitId + "|" + reason;
            if (_data.claimedKitKeys.Contains(claimKey)) return null;
            string seed = database.worldSeed + "|kit|" + kitId + "|" + reason;
            RewardResult reward = FadenwerkRewards.Roll(database, kitId, seed);
            if (reward == null) return null;
            _data.claimedKitKeys.Add(claimKey);
            // In the standalone manager an event listener is an external effect:
            // save the ledger through the game's canonical Core adapter before external grant.
            if (RewardGranted != null) RewardGranted(reward);
            return reward;
        }

        // ---------- Spielstand ----------

        public string SaveToJson()
        {
            _data.playerLevel = playerLevel;
            return JsonUtility.ToJson(_data);
        }

        public void LoadFromJson(string json)
        {
            QuestSaveData loaded = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<QuestSaveData>(json);
            _data = loaded ?? new QuestSaveData();
            if (_data.quests == null) _data.quests = new List<QuestState>();
            if (_data.flags == null) _data.flags = new List<string>();
            if (_data.claimedKitKeys == null) _data.claimedKitKeys = new List<string>();
            for (int i = 0; i < _data.quests.Count; i++)
                if (_data.quests[i].progress == null) _data.quests[i].progress = new List<int>();
            playerLevel = Mathf.Max(1, _data.playerLevel);
        }

        public void ResetProgress()
        {
            _data = new QuestSaveData();
            _data.playerLevel = playerLevel;
        }
    }
}
