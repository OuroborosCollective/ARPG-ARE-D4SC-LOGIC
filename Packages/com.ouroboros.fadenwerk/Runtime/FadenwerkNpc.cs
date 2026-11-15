using System.Collections.Generic;
using UnityEngine;

namespace Fadenwerk
{
    /// <summary>
    /// Macht ein GameObject zu einem Fadenwerk-NPC. Die npcId verbindet das Objekt mit Quests und Dialogen.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Fadenwerk/Fadenwerk NPC")]
    public class FadenwerkNpc : MonoBehaviour
    {
        [Tooltip("Feste Kennung. Quests und Dialoge verweisen darauf – nach dem Verknüpfen nicht mehr ändern.")]
        public string npcId = "";
        public string displayName = "";
        [Tooltip("z. B. Händler, Wache, Schmied")]
        public string role = "";
        [Tooltip("Was der NPC sagt, wenn kein Dialog zugewiesen ist")]
        [TextArea(3, 10)] public string loreText = "";
        [Tooltip("Wohin will der NPC, was ist sein Ziel?")]
        [TextArea(1, 4)] public string goalText = "";
        [FadenwerkId(FadenwerkIdKind.Marker)] public string goalMarkerId = "";
        [FadenwerkId(FadenwerkIdKind.Dialogue)] public string dialogueId = "";
        [Tooltip("Abstand, ab dem der Spieler mit dem NPC sprechen kann")]
        public float interactRadius = 2.5f;

        /// <summary>Alle aktiven NPCs der geladenen Szenen (zur Laufzeit).</summary>
        public static readonly List<FadenwerkNpc> Active = new List<FadenwerkNpc>();

        public string Label
        {
            get { return string.IsNullOrEmpty(displayName) ? gameObject.name : displayName; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Active.Clear(); }

        void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        void OnDisable() { Active.Remove(this); }

        void Reset()
        {
            EnsureId();
            if (string.IsNullOrEmpty(displayName)) displayName = gameObject.name;
        }

        /// <summary>Vergibt eine ID, falls noch keine vorhanden ist. Gibt true zurück, wenn etwas geändert wurde.</summary>
        public bool EnsureId()
        {
            if (!string.IsNullOrEmpty(npcId)) return false;
            npcId = FwUtil.NewId("npc", gameObject.name);
            return true;
        }

        public static FadenwerkNpc FindActive(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < Active.Count; i++)
                if (Active[i] != null && Active[i].npcId == id) return Active[i];
            return null;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.94f, 0.71f, 0.16f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.94f, 0.71f, 0.16f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, interactRadius));
        }
    }
}
