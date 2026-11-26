using AreArpg.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    public sealed partial class ArenaRuntime
    {
        /// <summary>The Aschbeter's private figure with its seven clips, built by AschenwachtCourtBuilder (never in the repository).</summary>
        private const string AschbeterFigure = "Aschbeter";
        private const float AschbeterTurnDegreesPerSecond = 40f;

        private Transform _aschbeterView;
        private Animator _aschbeterAnimator;
        private AschbeterState _aschbeterShownState;
        private AschbeterMode _aschbeterShownMode = (AschbeterMode)(-1);
        private int _aschbeterShownOutcomes;

        /// <summary>
        /// Show the Aschbeter where the rules put him (owner decision 2026-11-26: an enemy by the archive's rules): he follows
        /// the rules' position and heading smoothly, and each mode plays its clip of the archive (rest, shuffle, the two turns,
        /// the claw strike, the fall and the death pose). Only presentation: the rules decide everything.
        /// </summary>
        private void UpdateAschbeter()
        {
            AschbeterState a = _game != null && _game.InCourt ? _game.Aschbeter : null;
            if (a == null)
            {
                if (_aschbeterView) { _aschbeterView.gameObject.SetActive(false); Destroy(_aschbeterView.gameObject); }
                _aschbeterView = null; _aschbeterAnimator = null; _aschbeterShownState = null;
                return;
            }
            float x = a.Xmm * .001f, z = a.Zmm * .001f;
            var target = new Vector3(x, GroundHeight(x, z), z);
            var heading = Quaternion.Euler(0f, a.Heading * 5f, 0f);
            if (!ReferenceEquals(a, _aschbeterShownState) || !_aschbeterView)
            {
                if (_aschbeterView) Destroy(_aschbeterView.gameObject);
                _aschbeterView = BuildAschbeterView();
                _aschbeterView.SetPositionAndRotation(target, heading);
                _aschbeterAnimator = _aschbeterView.GetComponentInChildren<Animator>(true);
                _aschbeterShownState = a; _aschbeterShownMode = (AschbeterMode)(-1); _aschbeterShownOutcomes = a.Outcomes;
                _chapterScenery.Add(_aschbeterView);
            }
            float dt = Time.deltaTime;
            _aschbeterView.position = Vector3.Lerp(_aschbeterView.position, target, 1f - Mathf.Exp(-12f * dt));
            _aschbeterView.rotation = Quaternion.RotateTowards(_aschbeterView.rotation, heading, AschbeterTurnDegreesPerSecond * dt);
            if (a.Mode != _aschbeterShownMode)
            {
                PlayAschbeterClip(a.Mode, _aschbeterShownMode);
                _aschbeterShownMode = a.Mode;
            }
            // Several rule ticks can pass in one frame: the outcome counter, not the tick's own field, tells a new swing.
            if (a.Outcomes != _aschbeterShownOutcomes)
            {
                _message = a.LastOutcome == AschbeterSwing.Deflected ? "Der Hieb gleitet an der Kutte ab. Triff ihn, wenn er sich dreht." :
                    a.LastOutcome == AschbeterSwing.Staggered ? "Der Aschbeter stürzt, doch er rafft sich wieder auf." :
                    a.LastOutcome == AschbeterSwing.Fatal ? "Der Hieb trifft die Flanke. Der Aschbeter fällt." : _message;
                _aschbeterShownOutcomes = a.Outcomes;
            }
        }

        private Transform BuildAschbeterView()
        {
            var root = new GameObject("ARE_Aschbeter").transform;
            if (PassiveArtVisuals.TryBind(root, AschbeterFigure))
            {
                foreach (var animator in root.GetComponentsInChildren<Animator>(true)) animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                return root;
            }
            // Without the private figure: a dark hooded column marks where the rules put him.
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(body.GetComponent<Collider>());
            body.name = "Aschbeter_Fallback";
            body.transform.SetParent(root, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            body.transform.localScale = new Vector3(.7f, 1f, .7f);
            var renderer = body.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { color = new Color(.08f, .07f, .07f) };
            return root;
        }

        /// <summary>
        /// The clip of a mode. Increasing headings turn him clockwise seen from above, so <c>TurnPositive</c> plays his right
        /// turn; rising after a flat hit blends slowly back into his rest; death follows the fall in its own pose.
        /// </summary>
        private void PlayAschbeterClip(AschbeterMode mode, AschbeterMode previous)
        {
            if (_aschbeterAnimator == null || _aschbeterAnimator.runtimeAnimatorController == null) return;
            string state; float blend = .15f;
            switch (mode)
            {
                case AschbeterMode.Shuffle: state = "Shuffle"; break;
                case AschbeterMode.TurnPositive: state = "TurnRight"; break;
                case AschbeterMode.TurnNegative: state = "TurnLeft"; break;
                case AschbeterMode.Strike: state = "ClawStrike"; blend = .05f; break;
                case AschbeterMode.Fall: state = "Fall"; blend = .05f; break;
                case AschbeterMode.Rise: state = "Idle"; blend = 1f; break;
                case AschbeterMode.Dead: state = "DeathStay"; blend = previous == AschbeterMode.Fall ? .1f : 0f; break;
                default: state = "Idle"; break;
            }
            int hash = Animator.StringToHash(state);
            if (!_aschbeterAnimator.HasState(0, hash)) return;
            _aschbeterAnimator.CrossFadeInFixedTime(hash, blend, 0, 0f);
        }
    }
}
