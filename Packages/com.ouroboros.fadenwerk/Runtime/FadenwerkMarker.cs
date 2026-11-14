using System.Collections.Generic;
using UnityEngine;

namespace Fadenwerk
{
    /// <summary>
    /// Ein benannter Ort in der Szene: Quest-Ziel, Treffpunkt, Anschlagtafel, Spawn.
    /// Erreicht der Spieler den Radius, kannst du FadenwerkManager.ReportReach(markerId) melden –
    /// mit "Automatisch melden" erledigt das der Marker selbst.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Fadenwerk/Fadenwerk Marker")]
    public class FadenwerkMarker : MonoBehaviour
    {
        public string markerId = "";
        public string label = "";
        [FadenwerkOptions("ziel", "ort", "tafel", "spawn")]
        public string kind = "ziel";
        public float radius = 2f;
        [Tooltip("Meldet das Erreichen selbst, sobald das Objekt mit dem Tag \"Player\" im Radius ist")]
        public bool autoReport = true;

        public static readonly List<FadenwerkMarker> Active = new List<FadenwerkMarker>();

        Transform _player;
        float _nextSearch;
        float _nextReport;
        bool _reported;

        public string Label
        {
            get { return string.IsNullOrEmpty(label) ? gameObject.name : label; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Active.Clear(); }

        void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        void OnDisable() { Active.Remove(this); }

        void Reset()
        {
            EnsureId();
            if (string.IsNullOrEmpty(label)) label = gameObject.name;
        }

        public bool EnsureId()
        {
            if (!string.IsNullOrEmpty(markerId)) return false;
            markerId = FwUtil.NewId("marker", gameObject.name);
            return true;
        }

        void Update()
        {
            if (!autoReport || FadenwerkManager.Instance == null) return;
            if (_player == null)
            {
                if (Time.unscaledTime < _nextSearch) return;
                _nextSearch = Time.unscaledTime + 1f;
                var go = GameObject.FindWithTag("Player");
                if (go == null) return;
                _player = go.transform;
            }
            bool inside = (_player.position - transform.position).sqrMagnitude <= radius * radius;
            if (!inside) { _reported = false; return; }
            // Solange der Spieler im Radius steht, einmal pro Sekunde versuchen – so zählt es auch,
            // wenn die Quest erst angenommen wird, während er schon dort steht.
            if (_reported || Time.unscaledTime < _nextReport) return;
            _nextReport = Time.unscaledTime + 1f;
            _reported = FadenwerkManager.Instance.ReportReach(markerId);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.08f, 0.72f, 0.65f, 0.9f);
            Gizmos.DrawWireCube(transform.position, new Vector3(0.5f, 0.5f, 0.5f));
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.08f, 0.72f, 0.65f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, radius));
        }
    }
}
