using System.Collections.Generic;
using AreArpg.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>Bounded local playback. Audio, particles and preferences never write simulation state.</summary>
    public sealed class AshenAtmosphere : MonoBehaviour
    {
        public const int EffectVoices = 6;
        public const string PreferencePrefix = "ARE_ARPG_audio_v1_";
        private AtmosphereLibrary _library;
        private readonly List<AudioSource> _sources = new List<AudioSource>();
        private readonly float[] _effectGains = new float[EffectVoices];
        private AudioSource _musicA, _musicB, _ambience, _water, _voice;
        private readonly AudioSource[] _effects = new AudioSource[EffectVoices];
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private readonly Queue<KeyValuePair<int, SpeechLine>> _speech = new Queue<KeyValuePair<int, SpeechLine>>();
        private ParticleSystem _ash, _dust, _sparks;
        private Material _particleMaterial;
        private Texture2D _particleTexture;
        private Transform _hero;
        private int _act = -1, _effectIndex, _stepVariant, _speaker = -1;
        private float _walked, _subtitleLeft, _fade = 1f;
        private ulong _lastStepTick;
        private bool _suspended;
        private SpeechLine _currentLine;
        public float MusicVolume { get; private set; }
        public float VoiceVolume { get; private set; }
        public float EffectsVolume { get; private set; }
        public bool Muted { get; private set; }
        public bool ReducedEffects { get; private set; }
        public string Subtitle { get; private set; } = "";
        public int SpeakerId { get { return _speaker; } }
        public string LastEffectKey { get; private set; } = "";
        public int FootstepCount { get; private set; }
        public int SpeechCount { get; private set; }
        public bool HasMedia { get { return _library != null; } }

        public void Initialize(Transform hero)
        {
            _hero = hero;
            _library = Resources.Load<AtmosphereLibrary>("AshenAtmosphere");
            if (_library != null) foreach (var entry in _library.Clips) if (entry.Clip != null) _clips[entry.Key] = entry.Clip;
            _musicA = Source("Music A", true, 0); _musicB = Source("Music B", true, 0);
            _ambience = Source("Region ambience", true, 0); _water = Source("Mill water", true, 1);
            _water.transform.position = new Vector3(7, 1, 7);
            _voice = Source("NPC speech", false, 0); _voice.priority = 32;
            for (int i = 0; i < _effects.Length; i++) _effects[i] = Source("World effect " + i, false, .65f);
            MusicVolume = ReadVolume("music", .35f); VoiceVolume = ReadVolume("voice", .9f); EffectsVolume = ReadVolume("effects", .65f);
            Muted = PlayerPrefs.GetInt(PreferencePrefix + "mute", 0) != 0;
            ReducedEffects = PlayerPrefs.GetInt(PreferencePrefix + "reduced", 0) != 0;
            foreach (var source in _sources) source.mute = Muted;
            _musicA.volume = _musicB.volume = 0;
            _voice.volume = VoiceVolume; _ambience.volume = EffectsVolume * .27f; _water.volume = EffectsVolume * .3f;
            BuildParticles();
        }

        private static float ReadVolume(string key, float fallback)
        {
            float value = PlayerPrefs.GetFloat(PreferencePrefix + key, fallback);
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp01(value);
        }

        private AudioSource Source(string name, bool loop, float spatial)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>(); source.playOnAwake = false; source.loop = loop;
            source.spatialBlend = spatial; source.dopplerLevel = 0; source.minDistance = 8; source.maxDistance = 38;
            source.rolloffMode = AudioRolloffMode.Linear; source.priority = 128; _sources.Add(source); return source;
        }

        private AudioClip Clip(string key) { _clips.TryGetValue(key, out var clip); return clip; }
        private void Loop(AudioSource source, AudioClip clip)
        { source.Stop(); source.clip = clip; if (clip != null && !_suspended) source.Play(); }

        public void EnterRegion(int act)
        {
            _act = act; _walked = 0; _lastStepTick = 0; CancelSpeech();
            foreach (var source in _effects) source.Stop();
            // Exactly two music sources crossfade; rapid travel reuses the same bounded pair.
            var old = _musicA; _musicA = _musicB; _musicB = old; _fade = 0;
            _musicA.volume = 0;
            Loop(_musicA, Clip((act >= 5 && act <= 7) || act == 9 || act <= 1 ? "music_crypt" : "music_road"));
            Loop(_ambience, Clip(act == 2 ? "ambient_village" : act == 4 ? "ambient_mill" : "ambient_wind"));
            Loop(_water, act == 4 ? Clip("water_mill") : null);
            _ash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (!_suspended) { if ((act >= 2 && act <= 4) || act == 8 || act == 10) _ash.Play(); else _dust.Play(); }
            ApplyParticleBudget();
        }

        public void AcceptedSpeech(SocialResult result)
        {
            if (!result.Accepted || result.Speech == SpeechLine.None || result.SpeakerId < 0) return;
            // A deliberate new conversation replaces old chatter; at most its two lines are queued.
            CancelSpeech(); _speaker = result.SpeakerId;
            _speech.Enqueue(new KeyValuePair<int, SpeechLine>(result.SpeakerId, result.Speech));
            if (result.FollowupSpeech != SpeechLine.None) _speech.Enqueue(new KeyValuePair<int, SpeechLine>(result.SpeakerId, result.FollowupSpeech));
            NextSpeech();
        }

        private void NextSpeech()
        {
            if (_suspended) return; // Keep the bounded queue; begin its audio and subtitle together on resume.
            if (_speech.Count == 0) { Subtitle = ""; _speaker = -1; _currentLine = SpeechLine.None; return; }
            var next = _speech.Dequeue(); _speaker = next.Key; _currentLine = next.Value;
            Subtitle = (_speaker == 0 ? "Ilyan" : _speaker == 1 ? "Mara" : _speaker == 2 ? "Dorn" : "Jäger") + ": " + NpcSpeech.Text(next.Value);
            _voice.clip = Clip("voice_" + _speaker + "_" + (int)next.Value);
            _subtitleLeft = _voice.clip != null ? _voice.clip.length + .3f : Mathf.Max(3f, Subtitle.Length / 13f);
            _voice.volume = VoiceVolume; _voice.mute = Muted;
            if (_voice.clip != null) { _voice.Play(); SpeechCount++; }
        }

        public void CancelSpeech()
        { _speech.Clear(); _voice.Stop(); _subtitleLeft = 0; Subtitle = ""; _speaker = -1; _currentLine = SpeechLine.None; }

        public void ObserveTick(ArenaSimulation game, long oldX, long oldZ, int oldHealth)
        {
            if (game.GameOver) { CancelSpeech(); return; }
            if (_speaker >= 0 && _speaker < 3)
            {
                var npc = game.Npcs.Find(n => n.Id == _speaker);
                long dx = npc == null ? 100000 : npc.Xmm - game.PlayerXmm, dz = npc == null ? 0 : npc.Zmm - game.PlayerZmm;
                if (npc == null || npc.Memory.Health <= 0 || dx * dx + dz * dz > 100000000) CancelSpeech();
            }
            else if (_speaker == 3)
            {
                long dx = HunterEncounter.HunterX - game.PlayerXmm, dz = HunterEncounter.HunterZ - game.PlayerZmm;
                if (game.Progression.EntryAct != 3 || game.Progression.Road.HunterHealth <= 0 || dx * dx + dz * dz > 100000000) CancelSpeech();
            }
            if (game.PlayerHealth < oldHealth) Effect("hurt", Position(game), .7f);
            long movedX = game.PlayerXmm - oldX, movedZ = game.PlayerZmm - oldZ;
            float distance = Mathf.Sqrt((float)(movedX * movedX + movedZ * movedZ)) / 1000f;
            if (distance > 2f) { _walked = 0; return; } // Teleports/dashes do not become a burst of walking sounds.
            if (distance <= .001f) return;
            _walked += distance;
            if (_walked >= 1.45f && game.CurrentTick - _lastStepTick >= 2)
            {
                _walked %= 1.45f; _lastStepTick = game.CurrentTick;
                string surface = AtmosphereSurfaces.At(_act, game.PlayerXmm, game.PlayerZmm).ToString().ToLowerInvariant();
                Effect("step_" + surface + "_" + (_stepVariant++ % 2), Position(game), .7f); FootstepCount++;
            }
        }

        private static Vector3 Position(ArenaSimulation game) { return new Vector3(game.PlayerXmm / 1000f, .4f + (game.InForest?game.Progression.Woodland.Layout.HeightMm(game.PlayerXmm,game.PlayerZmm)/1000f:0), game.PlayerZmm / 1000f); }

        public void Skill(SkillResult result, int slot, ArenaSimulation game)
        {
            if (!result.Activated || _suspended) return;
            Effect(slot <= 1 ? "swing" : "spell", Position(game), .8f);
            if (result.Hits > 0) { Effect("impact", Position(game), .8f); _sparks.transform.position = Position(game); _sparks.Emit(ReducedEffects ? 3 : 9); }
        }

        public void Effect(string key, Vector3 position, float volume)
        {
            if (_suspended) return; // Transient world effects are dropped while the app is inactive.
            var clip = Clip(key); if (clip == null) return;
            int index = _effectIndex; _effectIndex = (_effectIndex + 1) % EffectVoices;
            _effectGains[index] = volume; var source = _effects[index]; source.Stop(); source.transform.position = position;
            source.clip = clip; source.volume = volume * EffectsVolume; source.mute = Muted || _suspended; source.Play();
            LastEffectKey = key;
        }

        public void SetPreferences(float music, float voice, float effects, bool muted, bool reduced)
        {
            MusicVolume = Mathf.Clamp01(music); VoiceVolume = Mathf.Clamp01(voice); EffectsVolume = Mathf.Clamp01(effects);
            Muted = muted; ReducedEffects = reduced;
            PlayerPrefs.SetFloat(PreferencePrefix + "music", MusicVolume); PlayerPrefs.SetFloat(PreferencePrefix + "voice", VoiceVolume);
            PlayerPrefs.SetFloat(PreferencePrefix + "effects", EffectsVolume); PlayerPrefs.SetInt(PreferencePrefix + "mute", muted ? 1 : 0);
            PlayerPrefs.SetInt(PreferencePrefix + "reduced", reduced ? 1 : 0); PlayerPrefs.Save(); ApplyParticleBudget();
        }

        private void Update()
        {
            if (_musicA == null || _suspended) return;
            _fade = Mathf.Min(1, _fade + Time.unscaledDeltaTime / 1.5f);
            float duck = _subtitleLeft > 0 ? .3f : 1f;
            _musicA.volume = MusicVolume * duck * _fade; _musicB.volume = MusicVolume * duck * (1 - _fade);
            if (_fade >= 1 && _musicB.isPlaying) _musicB.Stop();
            _voice.volume = VoiceVolume; _ambience.volume = EffectsVolume * .27f; _water.volume = EffectsVolume * .3f;
            for (int i = 0; i < _effects.Length; i++) _effects[i].volume = EffectsVolume * _effectGains[i];
            foreach (var source in _sources) source.mute = Muted;
            if (_subtitleLeft > 0) { _subtitleLeft -= Time.unscaledDeltaTime; if (_subtitleLeft <= 0) NextSpeech(); }
            if (_hero != null) { _ash.transform.position = _hero.position + Vector3.up * 5; _dust.transform.position = _hero.position; }
        }

        public void Suspend(bool suspended)
        {
            _suspended = suspended;
            foreach (var source in _sources) if (suspended) source.Pause(); else source.UnPause();
            if (suspended) { _ash.Pause(); _dust.Pause(); _sparks.Pause(); }
            else
            {
                if ((_act >= 2 && _act <= 4) || _act == 8 || _act == 10) _ash.Play(); else _dust.Play();
                _sparks.Play();
                // Region changes may have selected new loops while suspended.
                ResumeLoop(_musicA); ResumeLoop(_ambience); ResumeLoop(_water);
                if (_fade < 1f) ResumeLoop(_musicB);
                if (_currentLine == SpeechLine.None) NextSpeech();
            }
        }

        private static void ResumeLoop(AudioSource source)
        { if (source.clip != null && !source.isPlaying) source.Play(); }

        private void BuildParticles()
        {
            _particleTexture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
            { float a = Mathf.Clamp01(1 - Vector2.Distance(new Vector2(x, y), new Vector2(7.5f, 7.5f)) / 7.5f); _particleTexture.SetPixel(x, y, new Color(1, 1, 1, a * a)); }
            _particleTexture.Apply();
            _particleMaterial = new Material(Shader.Find("AshenCrypt/Atmosphere"));
            _particleMaterial.mainTexture = _particleTexture;
            _ash = Particles("Falling ash", 160, 18, .075f, new Color(.74f, .70f, .60f, .5f), new Vector3(20, 1, 20), 8);
            var velocity = _ash.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = .3f; velocity.y = -.75f; velocity.z = .08f;
            _dust = Particles("Low crypt mist", 24, 2, 2.3f, new Color(.42f, .55f, .6f, .055f), new Vector3(18, .3f, 18), 10);
            _sparks = Particles("Confirmed hit motes", 30, 0, .09f, new Color(.9f, .63f, .3f, .6f), Vector3.one * .3f, .35f);
            var main = _sparks.main; main.startSpeed = 2.5f; _sparks.Play();
        }

        private ParticleSystem Particles(string name, int limit, float rate, float size, Color color, Vector3 area, float lifetime)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false); var p = go.AddComponent<ParticleSystem>();
            p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = p.main; main.maxParticles = limit; main.startLifetime = lifetime; main.startSpeed = 0;
            main.startSize = size; main.startColor = color; main.simulationSpace = ParticleSystemSimulationSpace.World; main.playOnAwake = false;
            var shape = p.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = area;
            var emission = p.emission; emission.rateOverTime = rate;
            var renderer = p.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = _particleMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            return p;
        }

        private void ApplyParticleBudget()
        {
            if (_ash == null) return;
            var ash = _ash.main; ash.maxParticles = ReducedEffects ? 40 : 160;
            var rate = _ash.emission; rate.rateOverTime = ReducedEffects ? 4 : 18;
            var dust = _dust.main; dust.maxParticles = ReducedEffects ? 6 : 24;
            var drift = _dust.emission; drift.rateOverTime = ReducedEffects ? .5f : 2;
        }

        private void OnDisable() { if (_musicA != null) Suspend(true); }
        private void OnEnable() { if (_musicA != null) Suspend(false); }
        private void OnDestroy() { if (_particleMaterial != null) Destroy(_particleMaterial); if (_particleTexture != null) Destroy(_particleTexture); }
    }
}
