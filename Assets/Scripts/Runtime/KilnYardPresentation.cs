using System.Collections.Generic;
using AreArpg.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    public sealed partial class ArenaRuntime
    {
        /// <summary>The Brennerhof's private kit, built by BrennerhofBuilder from the pack in the editor (never in the repository).</summary>
        private const string KilnKit = "ARE/Visuals/Brennerhof/";

        /// <summary>The eight beds of embers in their light niches, as the pack places them; embers and ash rise from them.</summary>
        private static readonly Vector3[] KilnVents =
        {
            new Vector3(-8f, .142f, -6f), new Vector3(5f, .166f, -4f), new Vector3(-6f, .298f, 7f), new Vector3(7f, .298f, 7f),
            new Vector3(-10f, .514f, 25f), new Vector3(4f, .55f, 28f), new Vector3(-4f, .442f, 19f), new Vector3(6f, .598f, 32f)
        };

        private const float KilnGateLift = 2.8f, KilnGateSeconds = 1.05f, ShardGravity = 9.81f;

        private sealed class KilnShard
        {
            public Transform Piece;
            public Vector3 Velocity, Spin;
            public bool Resting;
        }

        private readonly Transform[] _kilnVases = new Transform[KilnYard.Vases.Length];
        private readonly Transform[] _kilnShardRoots = new Transform[KilnYard.Vases.Length];
        private readonly List<KilnShard> _kilnShards = new List<KilnShard>();
        private Transform _kilnGate;
        private Vector3 _kilnGateClosed;
        private float _kilnGateOpenFor = -1f;
        private int _kilnShownState = -1;
        private ArenaSimulation _kilnWorld;
        private AudioSource _kilnWind;
        private AudioClip[] _kilnBreakClips;
        private readonly List<MeshRenderer> _kilnFog = new List<MeshRenderer>();
        private MaterialPropertyBlock _kilnFogBlock;
        private float _kilnFogDaylight = -1f;
        private static readonly int FogCool = Shader.PropertyToID("_Cool"), FogWarm = Shader.PropertyToID("_Warm");

        /// <summary>
        /// The Brennerhof's living parts on top of its passive art: the eight vases, the gate of the kiln passage, embers and ash
        /// over the light niches with their glow, and the draught through the alleys. All of it only shows the rules' state.
        /// </summary>
        private void AddKilnYard(Transform scenery)
        {
            _kilnWorld = _game;
            _kilnShownState = _game.Progression.KilnState;
            _kilnShards.Clear();
            _kilnBreakClips = new AudioClip[4];
            for (int i = 0; i < _kilnBreakClips.Length; i++) _kilnBreakClips[i] = Resources.Load<AudioClip>(KilnKit + "Tonbruch_" + (i + 1));
            foreach (KilnVase vase in KilnYard.Vases)
            {
                bool broken = _game.Progression.KilnVaseBroken(vase.Id);
                _kilnVases[vase.Id] = KilnPiece(scenery, "Vase_" + vase.Id, !broken);
                _kilnShardRoots[vase.Id] = null;
                if (broken) ScatterKilnVase(scenery, vase, new Vector3(vase.Xmm * .001f, 0f, vase.Zmm * .001f - 1f), true);
            }
            _kilnGate = KilnPiece(scenery, "Gittertor", true);
            if (_kilnGate != null)
            {
                _kilnGateClosed = _kilnGate.localPosition;
                if (_game.Progression.KilnGateOpen) _kilnGate.localPosition = _kilnGateClosed + Vector3.up * KilnGateLift;
            }
            _kilnGateOpenFor = -1f;
            // The pack's ground fog is unlit; its colours follow the daylight here so the fog does not glow at night.
            _kilnFog.Clear();
            foreach (var renderer in scenery.GetComponentsInChildren<MeshRenderer>(true))
                if (renderer.name.StartsWith("Bodennebel", System.StringComparison.Ordinal) && renderer.sharedMaterial != null &&
                    renderer.sharedMaterial.HasProperty(FogCool)) _kilnFog.Add(renderer);
            _kilnFogDaylight = -1f;
            AddKilnEmbers(scenery);
            var windClip = Resources.Load<AudioClip>(KilnKit + "Zugwind_Loop");
            if (windClip != null && _atmosphere != null)
            {
                var wind = new GameObject("Brennerhof_Zugwind");
                wind.transform.SetParent(scenery, false);
                _kilnWind = wind.AddComponent<AudioSource>();
                _kilnWind.clip = windClip; _kilnWind.loop = true; _kilnWind.spatialBlend = 0f; _kilnWind.playOnAwake = false;
                _kilnWind.volume = _atmosphere.EffectsVolume * .35f; _kilnWind.mute = _atmosphere.Muted;
                _kilnWind.Play();
            }
        }

        /// <summary>One prefab of the kit; its art keeps the world pose the pack gives it, so the root stays at the origin.</summary>
        private static Transform KilnPiece(Transform scenery, string name, bool visible)
        {
            var root = new GameObject("Brennerhof_" + name).transform;
            root.SetParent(scenery, false);
            if (!PassiveArtVisuals.TryBindPrefab(root, Resources.Load<GameObject>(KilnKit + name)))
            {
                Destroy(root.gameObject);
                return null;
            }
            root.gameObject.SetActive(visible);
            return root;
        }

        private void AddKilnEmbers(Transform scenery)
        {
            var holder = new GameObject("Brennerhof_Glut").transform;
            holder.SetParent(scenery, false);
            Material ember = Resources.Load<Material>(KilnKit + "Glutflug"), ash = Resources.Load<Material>(KilnKit + "Ascheflug");
            bool reduced = _atmosphere != null && _atmosphere.ReducedEffects;
            foreach (Vector3 vent in KilnVents)
            {
                var light = new GameObject("Glutnische").AddComponent<Light>();
                light.transform.SetParent(holder, false);
                light.transform.localPosition = vent + Vector3.up * .45f;
                light.type = LightType.Point; light.shadows = LightShadows.None;
                light.color = new Color(1f, .5f, .22f); light.range = 4.5f; light.intensity = 1.3f;
                if (ember != null) KilnParticles(holder, vent, ember, reduced ? 3f : 7f, new Color(1f, .62f, .25f), .03f, .07f, .5f, 1.1f, 2.4f);
                if (ash != null) KilnParticles(holder, vent, ash, reduced ? 1.5f : 3.5f, new Color(.55f, .53f, .5f, .7f), .05f, .12f, .25f, .6f, 4.5f);
            }
        }

        private static void KilnParticles(Transform holder, Vector3 vent, Material material, float rate, Color color,
            float minSize, float maxSize, float minSpeed, float maxSpeed, float life)
        {
            var go = new GameObject(material.name);
            go.transform.SetParent(holder, false);
            go.transform.localPosition = vent + Vector3.up * .15f;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = true; main.playOnAwake = true; main.maxParticles = 48;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * .7f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = system.emission; emission.rateOverTime = rate;
            var shape = system.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 14f; shape.radius = .35f;
            var force = system.forceOverLifetime; force.enabled = true; force.space = ParticleSystemSimulationSpace.World;
            force.x = new ParticleSystem.MinMaxCurve(.06f, .24f); force.z = new ParticleSystem.MinMaxCurve(-.05f, .12f);
            var fade = system.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .15f), new GradientAlphaKey(.8f, .6f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            system.Play();
        }

        /// <summary>Follow the rules' state of the yard: a vase broken since the last frame bursts, a drawn bolt lifts the gate.</summary>
        private void UpdateKilnYard()
        {
            if (!_game.InKilnYard || _kilnWorld == null) return;
            if (!ReferenceEquals(_kilnWorld, _game)) { _kilnWorld = _game; _kilnShownState = _game.Progression.KilnState; }
            int state = _game.Progression.KilnState;
            if (state != _kilnShownState)
            {
                var hero = new Vector3(_game.PlayerXmm * .001f, 0f, _game.PlayerZmm * .001f);
                foreach (KilnVase vase in KilnYard.Vases)
                {
                    int bit = 1 << (KilnYard.VaseShift + vase.Id);
                    if ((state & bit) == 0 || (_kilnShownState & bit) != 0) continue;
                    if (_kilnVases[vase.Id] != null) _kilnVases[vase.Id].gameObject.SetActive(false);
                    var scenery = _kilnVases[vase.Id] != null ? _kilnVases[vase.Id].parent : _kilnGate != null ? _kilnGate.parent : null;
                    if (scenery == null) continue;
                    ScatterKilnVase(scenery, vase, hero, false);
                    PlayKilnBreak(scenery, vase);
                }
                if ((state & KilnYard.GateBit) != 0 && (_kilnShownState & KilnYard.GateBit) == 0) _kilnGateOpenFor = 0f;
                _kilnShownState = state;
            }
            if (_kilnGate != null && _kilnGateOpenFor >= 0f && _kilnGateOpenFor < KilnGateSeconds)
            {
                _kilnGateOpenFor = Mathf.Min(KilnGateSeconds, _kilnGateOpenFor + Time.deltaTime);
                _kilnGate.localPosition = _kilnGateClosed + Vector3.up * (KilnGateLift * Mathf.SmoothStep(0f, 1f, _kilnGateOpenFor / KilnGateSeconds));
            }
            StepKilnShards(Time.deltaTime);
            ShadeKilnFog();
            if (_kilnWind != null && _atmosphere != null) { _kilnWind.volume = _atmosphere.EffectsVolume * .35f; _kilnWind.mute = _atmosphere.Muted; }
        }

        /// <summary>Darken the ground fog with the ambient light the sky sets (night 0.26 .. day 0.47 in red).</summary>
        private void ShadeKilnFog()
        {
            if (_kilnFog.Count == 0) return;
            float daylight = Mathf.Clamp01((RenderSettings.ambientLight.r - .26f) / .21f);
            if (Mathf.Abs(daylight - _kilnFogDaylight) < .01f) return;
            _kilnFogDaylight = daylight;
            if (_kilnFogBlock == null) _kilnFogBlock = new MaterialPropertyBlock();
            float shade = Mathf.Lerp(.22f, 1f, daylight);
            foreach (var renderer in _kilnFog)
            {
                if (renderer == null) continue;
                var material = renderer.sharedMaterial;
                renderer.GetPropertyBlock(_kilnFogBlock);
                Color cool = material.GetColor(FogCool), warm = material.GetColor(FogWarm);
                _kilnFogBlock.SetColor(FogCool, new Color(cool.r * shade, cool.g * shade, cool.b * shade, cool.a));
                _kilnFogBlock.SetColor(FogWarm, new Color(warm.r * shade, warm.g * shade, warm.b * shade, warm.a));
                renderer.SetPropertyBlock(_kilnFogBlock);
            }
        }

        /// <summary>
        /// Sixteen shards fly out from the side the blow came from and come to rest on the ground the rules name. A vase that
        /// was already broken when the yard was built gets its shards at rest at once. Only presentation: the rules know just
        /// that the vase is broken.
        /// </summary>
        private void ScatterKilnVase(Transform scenery, KilnVase vase, Vector3 from, bool settleAtOnce)
        {
            var root = KilnPiece(scenery, "Scherben_" + vase.Id, true);
            if (root == null) return;
            _kilnShardRoots[vase.Id] = root;
            var centre = new Vector3(vase.Xmm * .001f, 0f, vase.Zmm * .001f);
            Vector3 push = centre - from; push.y = 0f;
            push = push.sqrMagnitude > 1e-4f ? push.normalized : Vector3.forward;
            var pieces = root.GetComponentsInChildren<MeshRenderer>(true);
            var shards = new List<KilnShard>();
            for (int i = 0; i < pieces.Length; i++)
            {
                var piece = pieces[i].transform;
                Vector3 out_ = piece.position - centre; out_.y = 0f;
                out_ = out_.sqrMagnitude > 1e-4f ? out_.normalized : push;
                out_.y = .48f + .44f * ((i * 7) % 5) / 4f;
                var shard = new KilnShard
                {
                    Piece = piece,
                    Velocity = out_ * (.95f + .18f * (i % 4)) + push * .55f,
                    Spin = new Vector3(Mathf.Sin(i * 2.3f), Mathf.Cos(i * .89f), Mathf.Sin(i * 1.37f)) * 240f
                };
                shards.Add(shard);
            }
            if (settleAtOnce)
            {
                for (int step = 0; step < 240; step++) { foreach (var shard in shards) StepKilnShard(shard, 1f / 60f); }
                foreach (var shard in shards) { shard.Resting = true; }
            }
            else _kilnShards.AddRange(shards);
        }

        private void StepKilnShards(float dt)
        {
            if (_kilnShards.Count == 0) return;
            dt = Mathf.Min(dt, 1f / 20f);
            bool moving = false;
            foreach (var shard in _kilnShards)
            {
                if (shard.Resting || shard.Piece == null) continue;
                StepKilnShard(shard, dt);
                moving |= !shard.Resting;
            }
            if (!moving) _kilnShards.Clear();
        }

        private void StepKilnShard(KilnShard shard, float dt)
        {
            if (shard.Resting) return;
            shard.Velocity.y -= ShardGravity * dt;
            Vector3 p = shard.Piece.position + shard.Velocity * dt;
            float ground = GroundHeight(p.x, p.z) + .02f;
            shard.Piece.rotation = Quaternion.Euler(shard.Spin * dt) * shard.Piece.rotation;
            if (p.y <= ground && shard.Velocity.y < 0f)
            {
                p.y = ground;
                shard.Velocity = new Vector3(shard.Velocity.x * .35f, -shard.Velocity.y * .18f, shard.Velocity.z * .35f);
                shard.Spin *= .4f;
                if (shard.Velocity.sqrMagnitude < .04f) shard.Resting = true;
            }
            shard.Piece.position = p;
        }

        /// <summary>One of the four breaking sounds, chosen by the vase's place as the pack does, heard from where the vase stood.</summary>
        private void PlayKilnBreak(Transform scenery, KilnVase vase)
        {
            if (_kilnBreakClips == null || _atmosphere == null) return;
            int n = Mathf.Abs(Mathf.RoundToInt(vase.Xmm * .011f + vase.Zmm * .023f)) % _kilnBreakClips.Length;
            AudioClip clip = _kilnBreakClips[n];
            if (clip == null) return;
            var source = new GameObject("Tonbruch_" + vase.Id).AddComponent<AudioSource>();
            source.transform.SetParent(scenery, false);
            source.transform.position = new Vector3(vase.Xmm * .001f, 1f, vase.Zmm * .001f);
            source.spatialBlend = .9f; source.minDistance = 2f; source.maxDistance = 23f; source.dopplerLevel = 0f;
            source.volume = _atmosphere.EffectsVolume; source.mute = _atmosphere.Muted;
            source.clip = clip;
            source.Play();
            Destroy(source.gameObject, clip.length + .5f);
        }

        /// <summary>In Aschenwacht's forecourt: the side gate in the east wall and its glow, the way into the Brennerhof.</summary>
        private void AddKilnSideGate(Transform scenery)
        {
            float x = KilnYard.SideGateXmm * .001f, z = KilnYard.SideGateZmm * .001f;
            var arch = new GameObject("Aschenwacht_Seitentor").transform;
            arch.SetParent(scenery, false);
            if (!PassiveArtVisuals.TryBindPrefab(arch, Resources.Load<GameObject>(KilnKit + "Seitentor"))) Destroy(arch.gameObject);
            // The glow is a passage marker of its own, like the bell tower's, not part of the place's lit scenery.
            var glow = new GameObject("ARE_Kiln_Passage");
            glow.transform.position = new Vector3(x - .6f, 1.5f + GroundHeight(x - .6f, z), z);
            _chapterScenery.Add(glow.transform);
            var light = glow.AddComponent<Light>();
            light.type = LightType.Point; light.range = 5f; light.intensity = 2f; light.shadows = LightShadows.None;
            light.color = new Color(1f, .58f, .28f);
        }

        /// <summary>Without the private kit: a plain floor under the yard and the passage at its south gate.</summary>
        private void BuildKilnFallback()
        {
            ChapterShape(PrimitiveType.Cube, "Brennerhof_FallbackFloor", new Vector3(0f, -.05f, 8f), new Vector3(36f, .1f, 66f), new Color(.30f, .29f, .28f));
            _worldPassage = ChapterShape(PrimitiveType.Cylinder, "ARE_World_Passage",
                new Vector3(KilnLayout.ExitXmm / 1000f, .03f, KilnLayout.ExitZmm / 1000f), new Vector3(3.6f, .02f, 3.6f), new Color(.25f, .68f, .72f));
            _worldPassage.gameObject.SetActive(false);
            RenderSettings.ambientLight = new Color(0.47f, 0.48f, 0.5f);
        }
    }
}
