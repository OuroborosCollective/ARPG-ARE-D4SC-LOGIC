using System;
using System.Collections.Generic;
using AreArpg.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>Presentation and input only. The Unity physics engine never decides gameplay outcomes.</summary>
    public sealed partial class ArenaRuntime : MonoBehaviour
    {
        private const ulong DemoSeed = 0xA11CE2026UL;
        private bool _entryProfile;
        private string CharacterSaveKey { get { return _entryProfile ? "ARE_ARPG_entry_v1" : "ARE_ARPG_character_ac1"; } }
        private string CharacterBackupKey { get { return CharacterSaveKey + "_backup"; } }
        private const int CatchUpLimit = 5;
        private const float TickSeconds = ArenaSimulation.TickMs / 1000f;

        private ArenaSimulation _game;
        private float _accumulator;
        private Transform _hero;
        private Camera _camera;
        private ArenaCameraRig _cameraRig;
        private readonly List<Transform> _chapterScenery = new List<Transform>();
        // Read-only projection index; SocialLedger exclusively owns the stock.
        private readonly List<Transform> _campRationVisuals = new List<Transform>();
        private Light _sun;
        private Transform _bossWarning;
        private bool _bossWarningVisible;
        private readonly Dictionary<int, Transform> _monsterVisuals = new Dictionary<int, Transform>();
        private readonly Dictionary<int, Transform> _lootVisuals = new Dictionary<int, Transform>();
        private readonly Dictionary<int, Transform> _npcVisuals = new Dictionary<int, Transform>();
        private readonly Dictionary<Transform, Animator[]> _actorAnimations = new Dictionary<Transform, Animator[]>();
        private readonly Queue<KeyValuePair<int, NpcAction>> _pendingNpcActions = new Queue<KeyValuePair<int, NpcAction>>();
        private Animator[] _heroMeleeAnimations = new Animator[0];
        private readonly Queue<int> _pendingSkills = new Queue<int>();
        private bool _pendingPickup;
        private bool _pendingCampDonation;
        private bool _pendingTakeRation;
        private readonly Queue<MasteryPath> _pendingMastery = new Queue<MasteryPath>();
        private readonly ArenaHudTouchCapture _movementTouch = new ArenaHudTouchCapture();
        private bool _storageFault;
        private bool _appPaused, _appUnfocused;
        private Transform _worldPassage;
        private bool _passageAnnounced;
        private float _regionFade;
        private string _message = "Erkunde die Krypta. Was du tust, darin wirst du besser.";
        private GUIStyle _labelStyle;
        private GUIStyle _smallStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallInAnyScene()
        {
            if (FindObjectOfType<ArenaRuntime>() != null) return;
            new GameObject("ARE_ARPG_Runtime").AddComponent<ArenaRuntime>();
        }

        /// <summary>Load only an independently verified character save and install its real chapter projection.</summary>
        private void Awake()
        {
            _entryProfile = gameObject.scene.name == "AshenEntry";
            Application.targetFrameRate = 60;
            CharacterProgression saved;
            if (!TryLoadCharacter(out saved))
            {
                _storageFault = true;
                _message = "Character save is invalid. Back it up before recovery; gameplay locked.";
                Debug.LogError("ARPG character save could not be validated; refusing to overwrite character progress.");
            }
            _game = new ArenaSimulation(DemoSeed, saved,
                saved != null ? saved.UnlockedCryptDepth : System.Numerics.BigInteger.One, LootCatalog.Version1);
            if (!_storageFault) _game.Progression.Equipment.Enable(_game.Progression);
            if (_game.IsVillage)
                _message = "Aschengrenze: Lager, Wache und Nordpass. Deine Taten bleiben in Erinnerung.";
            if (_game.Progression.EntryAct == 1)
                _message = "AKT I: Verlasse die Krypta. Besiege ihre Waechter und sichere die Beute.";
            if (_game.Progression.EntryAct == 2)
                _message = "AKT II: Folge dem Dorfweg. Ilyan hungert; Mara und Dorn erwarten dich.";
            if (_game.Progression.HungerStage > 0) _message = _game.HungerObjective;
            BuildWorld();
            BuildHud();
        }

        /// <summary>Load only the selected entry or legacy slot through the existing verified persistence contract.</summary>
        private bool TryLoadCharacter(out CharacterProgression saved)
        {
            saved = null;
            string primary = PlayerPrefs.GetString(CharacterSaveKey, "");
            string backup = PlayerPrefs.GetString(CharacterBackupKey, "");
            if (string.IsNullOrEmpty(primary) && string.IsNullOrEmpty(backup))
            {
                saved = _entryProfile ? CharacterProgression.CreateEntryProfile() : new CharacterProgression();
                return true;
            }
            if (CharacterProgression.TryLoad(primary, out saved)) return true;
            if (CharacterProgression.TryLoad(backup, out saved)) return true;
            saved = null;
            return false;
        }

        /// <summary>Persist the active Core progression; gameplay data never comes from visual state.</summary>
        private void SaveCharacter()
        {
            if (_game != null) SaveCharacter(_game.Progression);
        }

        /// <summary>Verify the stored checkpoint before permitting a world transition.</summary>
        private void SaveCharacter(CharacterProgression progress)
        {
            if (_storageFault || progress == null) return;
            string primary = PlayerPrefs.GetString(CharacterSaveKey, "");
            CharacterProgression previous;
            if (CharacterProgression.TryLoad(primary, out previous))
                PlayerPrefs.SetString(CharacterBackupKey, primary);
            string next = progress.SaveString();
            PlayerPrefs.SetString(CharacterSaveKey, next);
            PlayerPrefs.Save();
            CharacterProgression verified;
            if (!CharacterProgression.TryLoad(PlayerPrefs.GetString(CharacterSaveKey, ""), out verified) ||
                verified.StableHash != progress.StableHash)
            {
                _storageFault = true;
                Debug.LogError("ARPG character save readback failed; no further overwrites allowed.");
            }
        }

        private void OnApplicationPause(bool paused)
        {
            _appPaused = paused;
            if (_atmosphere != null) _atmosphere.Suspend(_appPaused || _appUnfocused);
            if (paused)
            {
                ReleasePresentationTouches();
                SaveCharacter();
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            _appUnfocused = !focused;
            if (_atmosphere != null) _atmosphere.Suspend(_appPaused || _appUnfocused);
            if (!focused) ReleasePresentationTouches();
        }

        /// <summary>Discard interrupted gestures without changing chosen zoom or character progression.</summary>
        private void ReleasePresentationTouches()
        {
            _movementTouch.Reset();
            _orbitMouseHeld = false;
            if (_cameraRig != null) _cameraRig.ClearTouches();
        }

        private void OnApplicationQuit()
        {
            SaveCharacter();
        }

        private static Material Material(Color color, float metallic = 0f)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            return material;
        }

        /// <summary>Create a projection host and remove its physics component; Core collision remains authoritative.</summary>
        private static Transform Shape(PrimitiveType primitive, string label, Vector3 position,
            Vector3 size, Color color, float metallic = 0f)
        {
            GameObject go = GameObject.CreatePrimitive(primitive);
            go.name = label;
            go.transform.position = position;
            go.transform.localScale = size;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) collider.enabled = false; // Collision is handled by ArenaSimulation.
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null && !WorldSurfacePresentation.TryApply(renderer, label))
                renderer.sharedMaterial = Material(color, metallic);
            return go.transform;
        }

        /// <summary>Build passive Unity scenery around the single canonical ArenaSimulation.</summary>
        private void BuildWorld()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.27f, 0.30f, 0.39f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.055f, 0.067f, 0.09f);
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.014f;
            BuildChapterScenery();

            var lightGo = new GameObject("ARE Moonlight");
            Light sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.65f, 0.76f, 1f);
            sun.intensity = 1.15f;
            sun.transform.rotation = Quaternion.Euler(50f, -28f, 0f);
            _sun = sun;
            ApplyEntryLighting();

            _hero = Shape(PrimitiveType.Capsule, "Wanderer", new Vector3(0f, 1.05f, 0f),
                new Vector3(0.85f, 1.05f, 0.85f), new Color(0.20f, 0.54f, 0.71f), 0.65f);
            if (!_entryProfile || !PassiveArtVisuals.TryBindGrounded(_hero, "EntryHero", 0f))
                PassiveArtVisuals.TryBind(_hero, "Hero");
            RegisterActorMotion(_hero);
            Transform blade = Shape(PrimitiveType.Cube, "Wanderer's sword", Vector3.zero,
                new Vector3(0.16f, 1.2f, 0.14f), new Color(0.83f, 0.77f, 0.59f), 0.7f);
            blade.SetParent(_hero, false);
            blade.localPosition = new Vector3(0.85f, 0.12f, 0.1f);
            blade.localRotation = Quaternion.Euler(0f, 0f, -22f);
            bool handWeapon = _entryProfile && _heroMeleeAnimations.Length > 0 && PassiveArtVisuals.TryBind(blade, "EntryWeapon");
            if (!handWeapon) PassiveArtVisuals.TryBind(blade, "Sword");
            if (_entryProfile && _heroMeleeAnimations.Length > 0)
                foreach (var socket in _hero.GetComponentsInChildren<Transform>(true))
                    if (socket.name == "EntryWeaponSocket")
                    {
                        Vector3 size = handWeapon ? Vector3.one : blade.lossyScale;
                        blade.SetParent(socket, false);
                        blade.localPosition = Vector3.zero; blade.localRotation = Quaternion.identity;
                        Vector3 parentScale = socket.lossyScale;
                        blade.localScale = new Vector3(size.x / parentScale.x, size.y / parentScale.y, size.z / parentScale.z);
                        break;
                    }
            _weaponHost=blade; foreach(Transform part in blade)_starterWeaponParts.Add(part);
            UpdateEquipmentPresentation();
            foreach (MonsterState monster in _game.Monsters) SpawnMonster(monster);
            BuildNpcVisuals();

            _camera = Camera.main;
            if (_camera == null)
            {
                GameObject camGo = new GameObject("ARPG Isometric Camera");
                camGo.tag = "MainCamera";
                _camera = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            _camera.fieldOfView = 48f;
            _camera.nearClipPlane = _entryProfile ? 0.12f : 0.3f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.055f, 0.067f, 0.09f);
            _cameraRig = _camera.GetComponent<ArenaCameraRig>();
            if (_cameraRig == null) _cameraRig = _camera.gameObject.AddComponent<ArenaCameraRig>();
            _cameraRig.Configure(_hero, _entryProfile);
            InitializeAtmosphere();
        }

        /// <summary>Lighting is presentation only, with a warm village and readable cool vault.</summary>
        private void ApplyEntryLighting()
        {
            if (!_entryProfile || _sun == null) return;
            bool warm = _game.IsVillage || _game.Progression.EntryAct == 5;
            _sun.color = warm ? new Color(1f, .91f, .76f) : new Color(.63f, .73f, 1f);
            _sun.intensity = _game.IsVillage ? 1.6f : _game.Progression.EntryAct >= 5 ? 1.2f : .9f;
            _sun.shadows = LightShadows.Soft;
        }

        /// <summary>Cache supported animator parameters once per private actor, outside per-frame allocations.</summary>
        private void RegisterActorMotion(Transform root)
        {
            if (!_entryProfile) return;
            var supported = new List<Animator>();
            foreach (var animator in root.GetComponentsInChildren<Animator>(true))
                foreach (var parameter in animator.parameters)
                    if (parameter.name == "MoveMagnitude" && parameter.type == AnimatorControllerParameterType.Float)
                    { supported.Add(animator); break; }
            _actorAnimations[root] = supported.ToArray();
            if (root == _hero)
            {
                var melee = new List<Animator>();
                foreach (var animator in root.GetComponentsInChildren<Animator>(true))
                    foreach (var parameter in animator.parameters)
                        if (parameter.name == "Melee" && parameter.type == AnimatorControllerParameterType.Trigger)
                        { melee.Add(animator); break; }
                _heroMeleeAnimations = melee.ToArray();
                foreach (var animator in _heroMeleeAnimations) EntryWeaponGrip.TryAttach(animator);
            }
        }

        /// <summary>Project accepted Core displacement into animation and facing; root motion remains disabled.</summary>
        private void AnimateActor(Transform root, long dx, long dz)
        {
            if (!_entryProfile || root == null) return;
            bool moving = dx != 0 || dz != 0;
            if (_actorAnimations.TryGetValue(root, out var animators))
                foreach (var animator in animators)
                    if (animator != null) animator.SetFloat("MoveMagnitude", moving ? 1f : 0f);
            if (moving) root.rotation = Quaternion.LookRotation(new Vector3(dx, 0, dz));
        }

        /// <summary>Track a chapter projection for cleanup and optionally bind reviewed private cosmetic art.</summary>
        private Transform ChapterShape(PrimitiveType primitive, string label, Vector3 position,
            Vector3 scale, Color color, string visualKey = null)
        {
            Transform shape = Shape(primitive, label, position, scale, color);
            _chapterScenery.Add(shape);
            string key = visualKey;
            switch (label)
            {
                case "Cracked Crypt Floor": key = "CryptFloor"; break;
                case "Pillar": key = "CryptPillar"; break;
                case "Pillar capital": key = "CryptPillarCapital"; break;
                case "Crypt Altar": key = "CryptAltar"; break;
                case "Ember_Rift_Ground": key = "EmberFloor"; break;
                case "Ember_Rift_Causeway": key = "EmberCauseway"; break;
                case "Ember_Rift_Boss_Dais": key = "EmberBossDais"; break;
                case "Ember_Rift_Gate": key = "EmberGate"; break;
            }
            if (label.StartsWith("Ember_Rift_Basalt_", StringComparison.Ordinal))
                key = "EmberBasalt";
            if (visualKey != null) FitHungerArt(shape, visualKey);
            else if (key != null) PassiveArtVisuals.TryBind(shape, key);
            return shape;
        }

        /// <summary>Only changes projection. Core collision rules remain canonical and unchanged.</summary>
        private void BuildChapterScenery()
        {
            foreach (Transform part in _chapterScenery)
                if (part != null) Destroy(part.gameObject);
            _chapterScenery.Clear();
            _woodlandChunks.Clear(); _woodlandPortals.Clear();
            _campRationVisuals.Clear();
            _returnPassage = null;
            _investigationGate = _innerGate = _cryptWarning = _cryptDebris = null;
            if (_entryProfile && _game.Progression.EntryAct >= 2 && _game.Progression.EntryAct <= 4)
                _chapterScenery.Add(OutdoorBackdrop.Create(_game.Progression.EntryAct));
            if (_game.InWoodlandRegion) { BuildWoodlandScenery(); return; }
            if (_game.InDungeon) { BuildDungeonScenery(); return; }
            if (_game.Progression.EntryAct == 7) { BuildRattlerDen(); return; }
            if (_game.Progression.EntryAct >= 5) { BuildInvestigationScenery(); return; }
            if (_game.Progression.EntryAct >= 3) { BuildHungerScenery(); if (_game.Progression.EntryAct == 4 && _game.Progression.Woodland.Active) WoodlandPortal("Wald nach Aschenzoll",-20,18); return; }
            if (_game.Progression.EntryAct > 0 && BuildEntryArt()) return;
            if (_game.Progression.EntryAct == 2) { BuildEntryVillageFallback(); return; }
            _worldPassage = ChapterShape(PrimitiveType.Cylinder, "ARE_World_Passage",
                new Vector3(ArenaSimulation.PassageXmm / 1000f, 0.02f, ArenaSimulation.PassageZmm / 1000f),
                new Vector3(3.6f, 0.02f, 3.6f), new Color(0.25f, 0.68f, 0.72f));
            _worldPassage.gameObject.SetActive(false);
            ChapterShape(PrimitiveType.Cube, "Passage_Arch", new Vector3(0f, 3.2f, 24f),
                new Vector3(5.2f, 0.55f, 0.65f), new Color(0.27f, 0.29f, 0.30f));
            foreach (int side in new[] { -1, 1 })
                ChapterShape(PrimitiveType.Cube, "Passage_Stone", new Vector3(side * 2.6f, 1.5f, 24f),
                    new Vector3(0.65f, 3f, 0.65f), new Color(0.27f, 0.29f, 0.30f));

            bool ember = _game.ChapterId == 2;
            if (_game.IsVillage) ChapterShape(PrimitiveType.Cube, "ARE_Camp_Supply", new Vector3(ArenaSimulation.CampXmm / 1000f, 0.4f, ArenaSimulation.CampZmm / 1000f),
                new Vector3(1.1f, 0.8f, 0.8f), new Color(0.44f, 0.31f, 0.18f));
            if (_game.IsVillage)
            {
                ChapterShape(PrimitiveType.Cube, "ARE_Outpost_Platform", new Vector3(-5f, -0.08f, -6f),
                    new Vector3(8f, 0.12f, 6f), new Color(0.30f, 0.26f, 0.20f));
                foreach (int side in new[] { -1, 1 })
                    ChapterShape(PrimitiveType.Cube, "ARE_Outpost_Post", new Vector3(-5f + side * 3f, 1.5f, -8.5f),
                        new Vector3(0.3f, 3f, 0.3f), new Color(0.32f, 0.24f, 0.17f));
                ChapterShape(PrimitiveType.Cube, "ARE_Outpost_Canopy", new Vector3(-5f, 3f, -8.5f),
                    new Vector3(7f, 0.14f, 2f), new Color(0.34f, 0.43f, 0.38f));
            }
            RenderSettings.ambientLight = ember ? new Color(0.45f, 0.20f, 0.14f) :
                new Color(0.27f, 0.30f, 0.39f);
            RenderSettings.fogColor = ember ? new Color(0.21f, 0.07f, 0.055f) :
                new Color(0.055f, 0.067f, 0.09f);
            RenderSettings.fogDensity = ember ? 0.022f : 0.014f;
            if (_sun != null)
            {
                _sun.color = ember ? new Color(1f, 0.52f, 0.31f) : new Color(0.65f, 0.76f, 1f);
                _sun.intensity = ember ? 1.35f : 1.15f;
            }

            if (!ember)
            {
                ChapterShape(PrimitiveType.Cube, "Cracked Crypt Floor", new Vector3(0f, -0.42f, 0f),
                    new Vector3(57f, 0.6f, 57f), new Color(0.16f, 0.18f, 0.20f));
                for (int x = -24; x <= 24; x += 8)
                    ChapterShape(PrimitiveType.Cube, "Stone inlay", new Vector3(x, -0.105f, 0f),
                        new Vector3(0.13f, 0.025f, 54f), new Color(0.24f, 0.26f, 0.28f));
                for (int z = -24; z <= 24; z += 8)
                    ChapterShape(PrimitiveType.Cube, "Stone inlay", new Vector3(0f, -0.103f, z),
                        new Vector3(54f, 0.025f, 0.13f), new Color(0.24f, 0.26f, 0.28f));
                foreach (int x in new[] { -7, 7 })
                    foreach (int z in new[] { -7, 7 })
                    {
                        ChapterShape(PrimitiveType.Cylinder, "Pillar", new Vector3(x, 2.2f, z),
                            new Vector3(2.2f, 2.2f, 2.2f), new Color(0.25f, 0.25f, 0.28f));
                        ChapterShape(PrimitiveType.Cylinder, "Pillar capital", new Vector3(x, 4.48f, z),
                            new Vector3(2.7f, 0.23f, 2.7f), new Color(0.36f, 0.34f, 0.33f));
                    }
                ChapterShape(PrimitiveType.Cylinder, "Crypt Altar", new Vector3(0f, 0.65f, 12.8f),
                    new Vector3(3f, 0.7f, 3f), new Color(0.31f, 0.18f, 0.18f));
                if (_game.IsVillage)
                {
                    BuildAschengrenzeScenery();
                    ApplyAschengrenzeArt();
                }
                return;
            }

            // Chapter II: glowing fracture lines, obsidian columns, ruined gate and boss dais.
            ChapterShape(PrimitiveType.Cube, "Ember_Rift_Ground", new Vector3(0f, -0.42f, 0f),
                new Vector3(57f, 0.6f, 57f), new Color(0.12f, 0.105f, 0.115f));
            ChapterShape(PrimitiveType.Cube, "Ember_Rift_Causeway", new Vector3(0f, -0.1f, 0f),
                new Vector3(10f, 0.12f, 52f), new Color(0.26f, 0.19f, 0.17f));
            for (int i = -3; i <= 3; i++)
            {
                ChapterShape(PrimitiveType.Cube, "Ember_Rift_Fissure_" + i,
                    new Vector3(i * 7.3f, -0.09f, 17f - (i % 2) * 9f),
                    new Vector3(0.24f, 0.055f, 13f), new Color(0.98f, 0.20f, 0.07f));
            }
            foreach (int x in new[] { -7, 7 })
                foreach (int z in new[] { -7, 7 })
                {
                    ChapterShape(PrimitiveType.Cylinder, "Ember_Rift_Basalt_" + x + "_" + z,
                        new Vector3(x, 2.5f, z), new Vector3(2.25f, 2.6f, 2.25f),
                        new Color(0.19f, 0.17f, 0.19f));
                    ChapterShape(PrimitiveType.Cube, "Ember_Rift_Glow_" + x + "_" + z,
                        new Vector3(x, 4.8f, z), new Vector3(1.9f, 0.22f, 1.9f),
                        new Color(1f, 0.31f, 0.09f));
                }
            ChapterShape(PrimitiveType.Cylinder, "Ember_Rift_Boss_Dais",
                new Vector3(0f, 0.22f, 12f), new Vector3(4.5f, 0.33f, 4.5f),
                new Color(0.38f, 0.15f, 0.13f));
            ChapterShape(PrimitiveType.Cube, "Ember_Rift_Gate",
                new Vector3(0f, 3.2f, 24f), new Vector3(7f, 0.8f, 0.9f),
                new Color(0.24f, 0.20f, 0.19f));
            ChapterShape(PrimitiveType.Cube, "Ember_Rift_Gate_Left",
                new Vector3(-3.5f, 1.4f, 24f), new Vector3(0.85f, 3.6f, 0.85f),
                new Color(0.24f, 0.20f, 0.19f));
            ChapterShape(PrimitiveType.Cube, "Ember_Rift_Gate_Right",
                new Vector3(3.5f, 1.4f, 24f), new Vector3(0.85f, 3.6f, 0.85f),
                new Color(0.24f, 0.20f, 0.19f));
        }

        /// <summary>
        /// Die Aschengrenze, first arrival. Projection-only scenery; core positions,
        /// NPC behavior, interaction, passage and save authority are never modified.
        /// All pieces are registered through ChapterShape for cleanup on zone transfer.
        /// </summary>
        private void BuildAschengrenzeScenery()
        {
            Color ashstone = new Color(0.26f, 0.25f, 0.26f);
            Color iron = new Color(0.20f, 0.21f, 0.23f);
            Color timber = new Color(0.30f, 0.19f, 0.14f);
            Color roof = new Color(0.37f, 0.22f, 0.19f);
            Color pavement = new Color(0.36f, 0.31f, 0.26f);

            // The road follows the established camp and north passage anchors.
            // No mesh/collider is permitted to decide navigation or combat here.
            ChapterShape(PrimitiveType.Cube, "Aschengrenze_Approach_Road",
                new Vector3(-2.5f, -0.085f, -7.7f), new Vector3(3.2f, 0.055f, 17f), pavement);
            ChapterShape(PrimitiveType.Cube, "Aschengrenze_North_Road",
                new Vector3(0f, -0.083f, 11f), new Vector3(2.8f, 0.06f, 25f), pavement);
            ChapterShape(PrimitiveType.Cube, "Aschengrenze_Market_Square",
                new Vector3(-6.8f, -0.067f, -10.5f), new Vector3(8f, 0.09f, 7f), ashstone);
            ChapterShape(PrimitiveType.Cube, "Aschengrenze_Supply_Hall",
                new Vector3(-12.5f, 1.4f, -8.7f), new Vector3(5.4f, 2.8f, 5f), timber);
            // An unfilled shelf illustrates the canonical missing stock (8/10 on new saves).
            // It is not evidence that any particular person committed a theft.
            ChapterShape(PrimitiveType.Cube, "Aschengrenze_Missing_Rations",
                new Vector3(-9.2f, 0.34f, -11.1f), new Vector3(1.8f, 0.25f, 0.65f), ashstone);
            for (int occupied = 0; occupied < SocialLedger.CampCapacity; occupied++)
            {
                int column = occupied % 5;
                int row = occupied / 5;
                Transform ration = ChapterShape(PrimitiveType.Cube,
                    "Aschengrenze_Stored_Ration_" + occupied,
                    new Vector3(-9.85f + column * 0.32f, 0.61f, -11.32f + row * 0.46f),
                    new Vector3(0.23f, 0.28f, 0.28f), timber);
                ration.gameObject.SetActive(occupied < _game.Progression.Social.CampProvisions);
                _campRationVisuals.Add(ration);
            }
            Transform leftRoof = ChapterShape(PrimitiveType.Cube, "Aschengrenze_Hall_Roof_Left",
                new Vector3(-13.95f, 3.15f, -8.7f), new Vector3(3.4f, 0.22f, 5.8f), roof);
            leftRoof.rotation = Quaternion.Euler(0f, 0f, 22f);
            Transform rightRoof = ChapterShape(PrimitiveType.Cube, "Aschengrenze_Hall_Roof_Right",
                new Vector3(-11.05f, 3.15f, -8.7f), new Vector3(3.4f, 0.22f, 5.8f), roof);
            rightRoof.rotation = Quaternion.Euler(0f, 0f, -22f);
            ChapterShape(PrimitiveType.Cube, "Aschengrenze_Hall_Door",
                new Vector3(-9.76f, 0.9f, -8.7f), new Vector3(0.13f, 1.8f, 1.35f), iron);

            // The original supplier's camp is beside the building, not replaced.
            for (int index = 0; index < 3; index++)
                ChapterShape(PrimitiveType.Cube, "Aschengrenze_Provision_Crate_" + index,
                    new Vector3(-7.4f + index * 0.85f, 0.37f, -7.3f),
                    new Vector3(0.72f, 0.74f, 0.72f), timber);
            ChapterShape(PrimitiveType.Cylinder, "Aschengrenze_Stone_Well",
                new Vector3(-5.8f, 0.55f, -12.4f), new Vector3(1.35f, 0.65f, 1.35f), ashstone);
            for (int stall = 0; stall < 2; stall++)
            {
                float x = -9.2f + 5.1f * stall;
                ChapterShape(PrimitiveType.Cube, "Aschengrenze_Market_Counter_" + stall,
                    new Vector3(x, 0.82f, -13f), new Vector3(2.7f, 0.9f, 0.9f), timber);
                ChapterShape(PrimitiveType.Cube, "Aschengrenze_Market_Canopy_" + stall,
                    new Vector3(x, 2.38f, -13f), new Vector3(3.2f, 0.22f, 2f),
                    stall == 0 ? roof : new Color(0.32f, 0.36f, 0.28f));
                for (int side = -1; side <= 1; side += 2)
                    ChapterShape(PrimitiveType.Cube,
                        "Aschengrenze_Market_Post_" + stall + "_" + side,
                        new Vector3(x + 1.3f * side, 1.1f, -13.6f),
                        new Vector3(0.17f, 2.2f, 0.17f), timber);
            }

            // The watch is a visible warning at the edge of the settled area.
            ChapterShape(PrimitiveType.Cube, "Aschengrenze_Watchtower",
                new Vector3(-14.5f, 3.8f, -1.8f), new Vector3(4.1f, 0.45f, 4.1f), timber);
            for (int dx = -1; dx <= 1; dx += 2)
                for (int dz = -1; dz <= 1; dz += 2)
                    ChapterShape(PrimitiveType.Cube,
                        "Aschengrenze_Watch_Post_" + dx + "_" + dz,
                        new Vector3(-14.5f + 1.7f * dx, 1.8f, -1.8f + 1.7f * dz),
                        new Vector3(0.38f, 3.6f, 0.38f), timber);
            ChapterShape(PrimitiveType.Cylinder, "Aschengrenze_Signal_Beacon",
                new Vector3(-14.5f, 4.55f, -1.8f), new Vector3(0.9f, 0.5f, 0.9f),
                new Color(0.95f, 0.45f, 0.17f));
            ChapterShape(PrimitiveType.Cube, "Aschengrenze_Watch_Fence_West",
                new Vector3(-18.5f, 1.05f, 1.2f), new Vector3(6f, 2.1f, 0.33f), iron);
            ChapterShape(PrimitiveType.Cube, "Aschengrenze_Watch_Fence_East",
                new Vector3(11.0f, 1.05f, 1.2f), new Vector3(10f, 2.1f, 0.33f), iron);

            // Existing Passage_Arch at Z=24 stays the sole real travel portal.
            foreach (int side in new[] { -1, 1 })
            {
                ChapterShape(PrimitiveType.Cube, "Aschengrenze_North_Banner_" + side,
                    new Vector3(side * 4.1f, 2.5f, 21.3f),
                    new Vector3(0.55f, 2.1f, 0.10f),
                    new Color(0.53f, 0.23f, 0.16f));
                ChapterShape(PrimitiveType.Cylinder, "Aschengrenze_North_Stone_" + side,
                    new Vector3(side * 4.1f, 0.55f, 20.6f),
                    new Vector3(1.05f, 0.55f, 1.05f), ashstone);
            }
        }

        /// <summary>
        /// Replaces blockout landmarks with reviewed passive village meshes when the private
        /// overlay is installed. Without it the complete primitive fallback stays visible.
        /// Presentation only: nothing here is read by the Core.
        /// </summary>
        private void ApplyAschengrenzeArt()
        {
            _chapterScenery.Add(new GameObject(AschengrenzeArt.BackdropLabel).transform);
            var bound = new HashSet<string>();
            foreach (Transform shape in _chapterScenery)
            {
                string key = AschengrenzeArt.KeyFor(shape.name);
                if (key != null && PassiveArtVisuals.TryBindGrounded(shape, key, AschengrenzeArt.GroundY))
                    bound.Add(shape.name);
            }
            foreach (Transform shape in _chapterScenery)
            {
                string owner = AschengrenzeArt.OwnerOf(shape.name);
                if (owner == null || !bound.Contains(owner)) continue;
                Renderer renderer = shape.GetComponent<Renderer>();
                if (renderer != null) renderer.enabled = false;
            }
        }

        /// <summary>Install privately composed real meshes; the Core still owns all walkability and resources.</summary>
        private bool BuildEntryArt()
        {
            var root = new GameObject(_game.IsVillage ? AschengrenzeArt.BackdropLabel : "AshenCrypt_Architecture");
            string key = _game.IsVillage ? "EntryVillageWorld" : "EntryCryptWorld";
            if (!PassiveArtVisuals.TryBind(root.transform, key)) { Destroy(root); return false; }
            _chapterScenery.Add(root.transform);
            var passage = new GameObject("ARE_World_Passage");
            passage.transform.position = new Vector3(0f, 1.5f, 24f);
            var light = passage.AddComponent<Light>();
            light.type = LightType.Point; light.range = 6f; light.intensity = 2.5f;
            light.color = new Color(1f, 0.64f, 0.3f);
            _worldPassage = passage.transform;
            _chapterScenery.Add(_worldPassage);
            passage.SetActive(false);
            RenderSettings.ambientLight = _game.IsVillage ? new Color(0.47f, 0.48f, 0.5f) : new Color(0.26f, 0.30f, 0.36f);
            RenderSettings.fogColor = _game.IsVillage ? new Color(0.24f, 0.28f, 0.29f) : new Color(0.045f, 0.055f, 0.07f);
            RenderSettings.fogDensity = _game.IsVillage ? 0.007f : 0.012f;
            if (!_game.IsVillage)
                foreach (int x in new[] { -7, 7 })
                    foreach (int z in new[] { -7, 7 })
                    {
                        var ember = new GameObject("Vault ember light");
                        ember.transform.position = new Vector3(x, 2.5f, z);
                        var glow = ember.AddComponent<Light>();
                        glow.type = LightType.Point; glow.color = new Color(1f, .52f, .19f);
                        glow.range = 11f; glow.intensity = 3.2f;
                        _chapterScenery.Add(ember.transform);
                    }
            if (_game.IsVillage)
            {
                for (int i = 0; i < SocialLedger.CampCapacity; i++)
                    CreateEntryRation(i, "EntryRation");
            }
            return true;
        }

        /// <summary>Visible development-only geometry follows every Core footprint; it is not finished world art.</summary>
        private void BuildEntryVillageFallback()
        {
            _worldPassage = ChapterShape(PrimitiveType.Cylinder, "ARE_World_Passage", new Vector3(0, .03f, 24),
                new Vector3(3.6f, .02f, 3.6f), new Color(.25f, .68f, .72f));
            for (int i = 0; i < EntryWorldLayout.BuildingCount; i++)
            {
                var b = EntryWorldLayout.Building(i);
                ChapterShape(PrimitiveType.Cube, "Entry_Footprint_" + b.Id,
                    new Vector3(b.X / 1000f, 1.5f, b.Z / 1000f),
                    new Vector3(b.HalfWidth * 2 / 1000f, 3f, b.HalfDepth * 2 / 1000f), new Color(.42f, .36f, .29f));
            }
            for (int i = 0; i < SocialLedger.CampCapacity; i++) CreateEntryRation(i, "EntryRation");
        }

        /// <summary>Keep real stock visible if its optional private mesh is absent; the Core owns the count.</summary>
        private Transform CreateEntryRation(int index, string visualKey)
        {
            var ration = ChapterShape(PrimitiveType.Cube, "Aschengrenze_Stored_Ration_" + index,
                new Vector3(ArenaSimulation.CampXmm / 1000f + 1.05f + index % 5 * .32f,
                    .50f, ArenaSimulation.CampZmm / 1000f + .28f + index / 5 * .46f),
                Vector3.one, new Color(.44f, .31f, .18f));
            if (!PassiveArtVisuals.TryBind(ration, visualKey)) ration.localScale = new Vector3(.23f, .28f, .28f);
            ration.gameObject.SetActive(index < _game.Progression.Social.CampProvisions);
            _campRationVisuals.Add(ration);
            return ration;
        }

        /// <summary>Build a visual for one existing Core monster and register its movement animator.</summary>
        private void SpawnMonster(MonsterState monster)
        {
            if (monster.Kind == 4 || monster.Kind == 5) return;
            PrimitiveType primitive = monster.Kind >= 2 ? PrimitiveType.Capsule : PrimitiveType.Sphere;
            Vector3 scale = monster.Kind >= 2 ? new Vector3(2.5f, 2.1f, 2.5f) :
                (monster.Kind == 1 ? new Vector3(1.8f, 1.8f, 1.8f) : Vector3.one * 1.45f);
            Color color = monster.Kind == 3 ? new Color(0.95f, 0.33f, 0.14f) :
                (monster.Kind == 2 ? new Color(0.55f, 0.13f, 0.14f) :
                (monster.Kind == 1 ? new Color(0.50f, 0.39f, 0.18f) : new Color(0.35f, 0.65f, 0.32f)));
            Transform root = Shape(primitive, "Enemy_" + monster.Id,
                new Vector3(monster.Xmm / 1000f, scale.y * 0.6f, monster.Zmm / 1000f), scale, color);
            _monsterVisuals.Add(monster.Id, root);
            if (!_entryProfile || !PassiveArtVisuals.TryBindGrounded(root, "EntryEnemy" + monster.Kind, 0f))
                PassiveArtVisuals.TryBind(root, "Enemy" + monster.Kind);
            RegisterActorMotion(root);
            if (monster.Kind == 3)
            {
                Transform crown = Shape(PrimitiveType.Cylinder, "Ember_Warden_Crown",
                    Vector3.zero, new Vector3(1.3f, 0.12f, 1.3f),
                    new Color(1f, 0.68f, 0.2f));
                crown.SetParent(root, false);
                crown.localPosition = new Vector3(0f, 0.75f, 0f);
            }
        }

        /// <summary>Read the current physical viewport once per input or drawing pass.</summary>
        /// <summary>
        /// Presentation test hook: when set, HUD/menu geometry uses this full-screen size instead of the
        /// device screen (for deterministic phone/tablet captures in batch runs). Never affects gameplay.
        /// </summary>
        public static Vector2? HudScreenOverride;
        private static float HudScreenWidth { get { return HudScreenOverride.HasValue ? HudScreenOverride.Value.x : Screen.width; } }
        private static float HudScreenHeight { get { return HudScreenOverride.HasValue ? HudScreenOverride.Value.y : Screen.height; } }

        private static ArenaHudLayout CurrentHudLayout()
        {
            if (HudScreenOverride.HasValue)
            {
                Vector2 s = HudScreenOverride.Value;
                return new ArenaHudLayout(s.x, s.y, new Rect(0f, 0f, s.x, s.y));
            }
            return new ArenaHudLayout(Screen.width, Screen.height, Screen.safeArea);
        }

        private void QueueNpcAction(NpcAction action)
        {
            NpcAgent npc = _game.NearestNpc();
            if (npc != null) _pendingNpcActions.Enqueue(new KeyValuePair<int, NpcAction>(npc.Id, action));
        }

        /// <summary>Rebuild the three existing social actors from their Core identities and persistent memories.</summary>
        private void BuildNpcVisuals()
        {
            foreach (Transform visual in _npcVisuals.Values) if (visual != null) Destroy(visual.gameObject);
            _npcVisuals.Clear();
            foreach (NpcAgent npc in _game.Npcs)
            {
                Color color = npc.Id == 0 ? new Color(0.24f, 0.55f, 0.53f) :
                    (npc.Id == 1 ? new Color(0.65f, 0.49f, 0.23f) : new Color(0.40f, 0.45f, 0.65f));
                Transform visual = Shape(PrimitiveType.Capsule, "ARE_NPC_" + npc.Id,
                    new Vector3(npc.Xmm / 1000f, 1f, npc.Zmm / 1000f), new Vector3(0.8f, 1f, 0.8f), color);
                if (!_entryProfile || !PassiveArtVisuals.TryBindGrounded(visual, "EntryNpc" + npc.Id, 0f))
                    PassiveArtVisuals.TryBind(visual, "Npc" + npc.Id);
                RegisterActorMotion(visual);
                _npcVisuals.Add(npc.Id, visual);
            }
        }

        /// <summary>Queue inputs for the authoritative fixed simulation tick.</summary>
        private void PollInputs()
        {
            ArenaHudLayout layout = CurrentHudLayout();
            if (_game.GameOver)
            {
                ReleasePresentationTouches();
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (touch.phase != TouchPhase.Began || !layout.IsWithinSafeArea(touch.position))
                        continue;
                    if (layout.HitTest(layout.ToGuiTouch(touch.position), true, false, false, false)
                        == ArenaHudControl.Restart)
                    {
                        Restart();
                        return;
                    }
                }
                if (!Application.isMobilePlatform && Input.GetMouseButtonDown(0) &&
                    layout.HitTest(layout.ToGuiTouch(Input.mousePosition), true, false, false, false) == ArenaHudControl.Restart)
                { Restart(); return; }
                if (Input.GetKeyDown(KeyCode.R)) Restart();
                return;
            }

            if (PollMenuInput()) return;
            if (PollRoadInput(layout)) { ReleasePresentationTouches(); return; }
            if (PollHungerInput(layout)) { ReleasePresentationTouches(); return; }
            if (PollRattlerInput(layout)) { ReleasePresentationTouches(); return; }
            long x = 0, z = 0;
            bool nearNpc = _game.NearestNpc() != null;
            bool hasMastery = _game.Progression.UnspentMasteryPoints > 0;
            bool canTakeRation = _game.Progression.EntryAct == 2;
            if (_cameraRig != null)
            {
                if (!Application.isMobilePlatform && Input.mouseScrollDelta.y != 0f &&
                    layout.IsWithinSafeArea(Input.mousePosition) &&
                    !layout.BlocksCameraGesture(layout.ToGuiTouch(Input.mousePosition), false,
                        nearNpc, _game.IsVillage, hasMastery, canTakeRation))
                    _cameraRig.ZoomSteps(Input.mouseScrollDelta.y);
                PollMouseOrbit(layout);
                _cameraRig.BeginTouchFrame();
            }
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 10000;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 10000;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) z += 10000;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) z -= 10000;

            bool capturedFingerPresent = false;
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                bool insideSafeArea = layout.IsWithinSafeArea(touch.position);
                Vector2 guiPoint = layout.ToGuiTouch(touch.position);
                // Only a new touch can select a button. The held movement
                // finger keeps its ownership outside MOVE and SafeArea.
                ArenaHudControl control = touch.phase == TouchPhase.Began && insideSafeArea
                    ? layout.HitTest(guiPoint, false,
                        nearNpc, _game.IsVillage, hasMastery, canTakeRation)
                    : ArenaHudControl.None;

                if (_cameraRig != null)
                    _cameraRig.ObserveTouch(touch.fingerId, touch.phase, guiPoint,
                        insideSafeArea && !layout.BlocksCameraGesture(guiPoint, false,
                            nearNpc, _game.IsVillage, hasMastery, canTakeRation));

                Vector2 direction;
                if (_movementTouch.TryMove(touch.fingerId, touch.phase, guiPoint,
                    layout, control, out direction))
                {
                    capturedFingerPresent = true;
                    x = (long)(direction.x * 10000f);
                    z = (long)(direction.y * 10000f);
                    continue;
                }

                if (touch.phase == TouchPhase.Began && insideSafeArea) RouteHudControl(control);
                if (MenuOpen) break;
            }

            // Desktop: a left click selects the same layout target a touch would (movement stays on WASD).
            if (!Application.isMobilePlatform && Input.touchCount == 0 && Input.GetMouseButtonDown(0) &&
                layout.IsWithinSafeArea(Input.mousePosition))
            {
                ArenaHudControl clicked = layout.HitTest(layout.ToGuiTouch(Input.mousePosition), false,
                    nearNpc, _game.IsVillage, hasMastery, canTakeRation);
                if (clicked != ArenaHudControl.Move) RouteHudControl(clicked);
            }

            // Release if the OS dropped the tracked finger without Ended/Canceled.
            if (_movementTouch.IsCaptured && !capturedFingerPresent) _movementTouch.Reset();
            if (_cameraRig != null) _cameraRig.EndTouchFrame();
            if (MenuOpen) { _game.SetMoveInput(0, 0); return; }

            CameraRelativeMove(ref x, ref z);
            _game.SetMoveInput(x, z);
            if (Input.GetKeyDown(KeyCode.Alpha1)) _pendingSkills.Enqueue(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) _pendingSkills.Enqueue(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) _pendingSkills.Enqueue(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) _pendingSkills.Enqueue(3);
            if (Input.GetKeyDown(KeyCode.E)) _pendingPickup = true;
            if (Input.GetKeyDown(KeyCode.F5)) _pendingMastery.Enqueue(MasteryPath.Might);
            if (Input.GetKeyDown(KeyCode.F6)) _pendingMastery.Enqueue(MasteryPath.Vitality);
            if (Input.GetKeyDown(KeyCode.F7)) _pendingMastery.Enqueue(MasteryPath.Resolve);
            if (Input.GetKeyDown(KeyCode.R)) Restart();
            if (Input.GetKeyDown(KeyCode.F)) QueueNpcAction(NpcAction.Greet);
            if (Input.GetKeyDown(KeyCode.V)) QueueNpcAction(NpcAction.Help);
            if (Input.GetKeyDown(KeyCode.B)) QueueNpcAction(NpcAction.Attack);
            if (Input.GetKeyDown(KeyCode.H)) QueueNpcAction(NpcAction.Rob);
            if (Input.GetKeyDown(KeyCode.T) && _game.Progression.EntryAct == 2) _pendingTakeRation = true;
            if (Input.GetKeyDown(KeyCode.G)) _pendingCampDonation = true;
        }

        /// <summary>One routing table for touch and mouse; Core actions are only queued, never applied here.</summary>
        private void RouteHudControl(ArenaHudControl control)
        {
            if (control >= ArenaHudControl.MenuCharacter && control <= ArenaHudControl.MenuSettings)
                OpenMenu((int)control - (int)ArenaHudControl.MenuCharacter);
            else if (control >= ArenaHudControl.CameraCloser && control <= ArenaHudControl.CameraReset)
                ApplyCameraControl((int)control - (int)ArenaHudControl.CameraCloser);
            else if (control >= ArenaHudControl.NpcGreet && control <= ArenaHudControl.NpcRob)
                QueueNpcAction((NpcAction)((int)control - (int)ArenaHudControl.NpcGreet));
            else if (control >= ArenaHudControl.Skill0 && control <= ArenaHudControl.Skill3)
                _pendingSkills.Enqueue((int)control - (int)ArenaHudControl.Skill0);
            else if (control >= ArenaHudControl.MasteryMight && control <= ArenaHudControl.MasteryResolve)
                _pendingMastery.Enqueue((MasteryPath)((int)control - (int)ArenaHudControl.MasteryMight));
            else if (control == ArenaHudControl.Donate)
                _pendingCampDonation = true;
            else if (control == ArenaHudControl.TakeRation && _game.Progression.EntryAct == 2)
                _pendingTakeRation = true;
            else if (control == ArenaHudControl.Pickup)
                _pendingPickup = true;
        }

        /// <summary>Shared button routing changes only the cosmetic camera; nothing enters the Core action queue.</summary>
        private void ApplyCameraControl(int control)
        {
            if (_cameraRig == null) return;
            if (control == 0) _cameraRig.ZoomSteps(1f);
            else if (control == 1) _cameraRig.ZoomSteps(-1f);
            else if (control == 2) _cameraRig.ResetZoom();
        }

        /// <summary>Restart the checkpoint using the shipped catalog while retaining acquired progression.</summary>
        private void Restart()
        {
            if (_storageFault) return;
            _game.Progression.Road.RestartPlayer();
            _game.Progression.Investigation.RestartPlayer();
            _game.Progression.Woodland.RestartPlayer();
            SaveCharacter();
            if (_storageFault) return;
            InstallNewRun(new ArenaSimulation(DemoSeed, _game.Progression, _game.CryptDepth, LootCatalog.Version1),
                "Crypt restarted - character progression retained.");
        }

        /// <summary>Commit and read back the next Core-approved checkpoint before switching its presentation.</summary>
        private void AdvanceCrypt()
        {
            if (_storageFault || (_game.Progression.EntryAct < 2 && !_game.CryptCleared) || !_game.AtAnyPassage)
                return;
            if (_game.GroundLoot.Count != 0)
            {
                _message = "Deine Beute liegt noch hier. Sichere sie vor dem Durchgang.";
                return;
            }
            ArenaSimulation next = _game.TryTraversePassage();
            if (next == null) return;
            // Commit and read back the new checkpoint before changing the active world.
            SaveCharacter(next.Progression);
            if (_storageFault) return;
            InstallNewRun(next, next.Progression.HungerStage > 0 ? next.HungerObjective : next.Progression.EntryAct == 2 ?
                "AKT II: Folge dem Dorfweg zu Ilyan, Mara und Dorn. Zwei Rationen fehlen." :
                "Du betrittst " + next.ChapterName + ". Der Weg fuehrt tiefer.");
            _regionFade = 1f;
            _hero.position = new Vector3(next.PlayerXmm / 1000f, 1.05f, next.PlayerZmm / 1000f);
            if (_cameraRig != null) { RefreshCameraOccluders(); _cameraRig.SnapToTarget(); }
        }

        /// <summary>Install an already-created Core world, discard queued old-world inputs and rebuild projections.</summary>
        private void InstallNewRun(ArenaSimulation next, string message)
        {
            _game = next;
            _game.Progression.Equipment.Enable(_game.Progression);
            _actorAnimations.Clear();
            RegisterActorMotion(_hero);
            _passageAnnounced = false;
            BuildChapterScenery();
            ApplyEntryLighting();
            _pendingSkills.Clear();
            _pendingMastery.Clear();
            _pendingPickup = false;
            _pendingCampDonation = false;
            _pendingTakeRation = false;
            _pendingNpcActions.Clear();
            _pendingHungerAction = -1;
            _pendingHunterAction = -1;
            ClearRoadVisuals(); ClearRattlerVisuals();
            _roadVisualAct = -1;
            _hungerJournalOpen = false;
            ReleasePresentationTouches();
            _accumulator = 0f;
            if (_bossWarning != null) Destroy(_bossWarning.gameObject);
            _bossWarning = null;
            _bossWarningVisible = false;
            foreach (Transform t in _monsterVisuals.Values) if (t != null) Destroy(t.gameObject);
            foreach (Transform t in _lootVisuals.Values) if (t != null) Destroy(t.gameObject);
            _monsterVisuals.Clear();
            _lootVisuals.Clear();
            foreach (MonsterState monster in _game.Monsters) SpawnMonster(monster);
            BuildNpcVisuals();
            _message = message + " / " + next.ChapterName;
            RebuildAtmosphere();
        }

        /// <summary>Consume ordered player requests on the established 100-ms tick, then interpolate presentation.</summary>
        private void Update()
        {
            if (_storageFault) { UpdateHud(); return; }
            _regionFade = Mathf.Max(0f, _regionFade - Time.deltaTime / 0.75f);
            PollInputs();
            // The modal menu pauses the deterministic simulation; no ticks accumulate while it is open.
            if (!MenuOpen) _accumulator += Mathf.Min(Time.deltaTime, 0.25f);
            int steps = 0;
            while (!MenuOpen && _accumulator >= TickSeconds && steps < CatchUpLimit)
            {
                _accumulator -= TickSeconds;
                steps++;
                if (_pendingHunterAction >= 0)
                {
                    var hunter = _game.InteractHunter(_pendingHunterAction == 1); _pendingHunterAction = -1;
                    _atmosphere.AcceptedSpeech(hunter);
                    _message = hunter.Accepted ? hunter.Text : "Gehe direkt zum Jaeger.";
                    if (hunter.Accepted) SaveCharacter();
                }
                if (_pendingHungerAction >= 0)
                {
                    var story = _game.ActOnHunger((HungerAction)_pendingHungerAction);
                    _pendingHungerAction = -1;
                    _message = story.Text;
                    _atmosphere.AcceptedSpeech(story);
                    if (story.Accepted) SaveCharacter();
                    if (_storageFault) break;
                }
                while (_pendingNpcActions.Count > 0)
                {
                    KeyValuePair<int, NpcAction> request = _pendingNpcActions.Dequeue();
                    SocialResult result = _game.InteractNpc(request.Key, request.Value);
                    _atmosphere.AcceptedSpeech(result);
                    if (!string.IsNullOrEmpty(result.Text)) _message = result.Text;
                    if (result.Accepted) SaveCharacter();
                    if (_storageFault) break;
                }
                if (_storageFault) break;
                if (_pendingTakeRation)
                {
                    _pendingTakeRation = false;
                    SocialResult theft = _game.TakeCampRation();
                    _atmosphere.AcceptedSpeech(theft);
                    if (!string.IsNullOrEmpty(theft.Text)) _message = theft.Text;
                    if (theft.Accepted) SaveCharacter();
                    if (_storageFault) break;
                }
                if (_pendingCampDonation)
                {
                    _pendingCampDonation = false;
                    SocialResult donation = _game.DonateCampRation();
                    _atmosphere.AcceptedSpeech(donation);
                    _message = !string.IsNullOrEmpty(donation.Text) ? donation.Text :
                        "Nur direkt bei Mara kannst du eine Lagerration abgeben.";
                    if (donation.Accepted) SaveCharacter();
                    if (_storageFault) break;
                }
                while (_pendingMastery.Count > 0)
                {
                    MasteryPath path = _pendingMastery.Dequeue();
                    bool spent = _game.Progression.TrySpendMastery(path);
                    _message = spent ? "Meisterschaft verteilt: " + MasteryName(path) : "Keine Meisterschaftspunkte frei.";
                    if (spent) SaveCharacter();
                }
                while (_pendingSkills.Count > 0)
                {
                    int slot = _pendingSkills.Dequeue();
                    System.Numerics.BigInteger previousRank = _game.Progression.PracticeLevel(slot);
                    SkillResult outcome = _game.ActivateSkill(slot);
                    _atmosphere.Skill(outcome, slot, _game);
                    if (_entryProfile && outcome.Activated && outcome.Hits > 0 && (slot == 0 || slot == 1))
                        foreach (var animator in _heroMeleeAnimations)
                            if (animator != null) { animator.ResetTrigger("Melee"); animator.SetTrigger("Melee"); }
                    _message = outcome.Message;
                    bool learned = _game.Progression.PracticeLevel(slot) > previousRank;
                    if (learned) _message += " / Durch Uebung gewachsen: Rang " + _game.Progression.PracticeLevel(slot);
                    if (outcome.Kills > 0 || learned) SaveCharacter();
                }
                if (_pendingPickup)
                {
                    bool pickedUp = _game.TryCollectLoot();
                    _message = pickedUp ? "Beute aufgenommen. Neue Waffen kannst du im Inventar vergleichen und ausrüsten." : "Keine Beute in Reichweite.";
                    if (pickedUp) SaveCharacter();
                    _pendingPickup = false;
                }
                long oldX = _game.PlayerXmm, oldZ = _game.PlayerZmm; int oldHealth = _game.PlayerHealth;
                _game.Step();
                _atmosphere.ObserveTick(_game, oldX, oldZ, oldHealth);
                if ((_game.Progression.Road.Started || _game.Progression.Investigation.Active) && _game.CurrentTick % 10 == 0) SaveCharacter();
                if ((_game.Progression.EntryAct >= 2 || _game.CryptCleared) && _game.AtAnyPassage)
                {
                    AdvanceCrypt();
                    if (_storageFault) break;
                }
            }
            UpdateRoadPresentation();
            UpdateInvestigationPresentation();
            UpdateRattlerPresentation();
            UpdatePresentation(Mathf.Clamp01(_accumulator / TickSeconds));
            UpdateHud();
            UpdateMenu();
        }

        /// <summary>Project actual game state as visuals; never create a second stock owner.</summary>
        private void UpdatePresentation(float alpha)
        {
            for (int index = 0; index < _campRationVisuals.Count; index++)
            {
                Transform ration = _campRationVisuals[index];
                bool visible = index < _game.Progression.Social.CampProvisions;
                if (ration != null && ration.gameObject.activeSelf != visible)
                    ration.gameObject.SetActive(visible);
            }
            foreach (NpcAgent npc in _game.Npcs)
            {
                Transform visual;
                if (!_npcVisuals.TryGetValue(npc.Id, out visual) || visual == null) continue;
                visual.position = new Vector3(Mathf.Lerp(npc.PreviousXmm, npc.Xmm, alpha) / 1000f,
                    npc.Memory.Health > 0 ? 1f : 0.25f, Mathf.Lerp(npc.PreviousZmm, npc.Zmm, alpha) / 1000f);
                visual.rotation = npc.Memory.Health > 0 ? Quaternion.identity : Quaternion.Euler(90f, 0f, 0f);
                if (npc.Memory.Health > 0) AnimateActor(visual, npc.Xmm - npc.PreviousXmm, npc.Zmm - npc.PreviousZmm);
            }
            UpdateDungeonPresentation();
            UpdateWoodlandPresentation();
            UpdateEquipmentPresentation();
            if (_worldPassage != null)
                _worldPassage.gameObject.SetActive((_game.Progression.EntryAct >= 2 ? _game.HungerForwardOpen : _game.CryptCleared) && _game.GroundLoot.Count == 0);
            if (_game.Progression.EntryAct < 2 && _game.CryptCleared && !_passageAnnounced)
            {
                _passageAnnounced = true;
                _message = _game.GroundLoot.Count == 0 ? "Ein Luftzug streicht durch den Nordbogen. Folge dem Weg." :
                    "Die Krypta wird still. Sichere deine Beute; der Nordbogen fuehrt weiter.";
            }
            if (_hero != null)
            {
                float x = Mathf.Lerp(_game.PrevPlayerXmm, _game.PlayerXmm, alpha) / 1000f;
                float z = Mathf.Lerp(_game.PrevPlayerZmm, _game.PlayerZmm, alpha) / 1000f;
                _hero.position = new Vector3(x, 1.05f + GroundHeight(x,z), z);
                AnimateActor(_hero, _game.PlayerXmm - _game.PrevPlayerXmm, _game.PlayerZmm - _game.PrevPlayerZmm);
            }
            UpdateHeroCameraContext();
            foreach (MonsterState enemy in _game.Monsters)
            {
                // The warning ring is presentation of real BossWindupTicks, not client-owned damage.
                if (enemy.Kind == 3 && enemy.BossWindupTicks > 0)
                {
                    if (!_bossWarningVisible)
                    {
                        if (_bossWarning == null)
                            _bossWarning = Shape(PrimitiveType.Cylinder, "Ember_Warden_Telegraph",
                                new Vector3(enemy.Xmm / 1000f, 0.025f, enemy.Zmm / 1000f),
                                new Vector3(8f, 0.015f, 8f), new Color(0.95f, 0.12f, 0.05f));
                        _bossWarningVisible = true;
                    }
                    if (_bossWarning != null)
                    {
                        _bossWarning.gameObject.SetActive(true);
                        _bossWarning.position = new Vector3(enemy.Xmm / 1000f, 0.025f, enemy.Zmm / 1000f);
                    }
                }
                else if (_bossWarning != null) _bossWarning.gameObject.SetActive(false);
                Transform visual;
                if (!_monsterVisuals.TryGetValue(enemy.Id, out visual) || visual == null) continue;
                visual.gameObject.SetActive(!enemy.IsDead);
                if (!enemy.IsDead)
                {
                    float x = Mathf.Lerp(enemy.PreviousXmm, enemy.Xmm, alpha) / 1000f;
                    float z = Mathf.Lerp(enemy.PreviousZmm, enemy.Zmm, alpha) / 1000f;
                    visual.position = new Vector3(x, visual.localScale.y * 0.6f + GroundHeight(x,z), z);
                    AnimateActor(visual, enemy.Xmm - enemy.PreviousXmm, enemy.Zmm - enemy.PreviousZmm);
                }
            }
            foreach (LootState item in _game.GroundLoot)
            {
                if (_lootVisuals.ContainsKey(item.Id)) continue;
                _lootVisuals.Add(item.Id, Shape(PrimitiveType.Sphere, "Loot_" + item.Id,
                    new Vector3(item.Xmm / 1000f, 0.55f + GroundHeight(item.Xmm/1000f,item.Zmm/1000f), item.Zmm / 1000f),
                    Vector3.one * 0.55f, new Color(0.97f, 0.76f, 0.18f), 0.8f));
            }
            var stillOnGround = new HashSet<int>();
            foreach (LootState item in _game.GroundLoot) stillOnGround.Add(item.Id);
            var removed = new List<int>();
            foreach (KeyValuePair<int, Transform> entry in _lootVisuals)
            {
                if (!stillOnGround.Contains(entry.Key))
                {
                    if (entry.Value != null) Destroy(entry.Value.gameObject);
                    removed.Add(entry.Key);
                }
                else if (entry.Value != null) entry.Value.Rotate(Vector3.up, 90f * Time.deltaTime);
            }
            foreach (int id in removed) _lootVisuals.Remove(id);
        }

        /// <summary>Describe a donation control appropriately on touch or keyboard devices.</summary>
        public static string DonationButtonLabel(bool isMobile)
        {
            return isMobile ? "RATION AN MARA" : "G: RATION AN MARA";
        }

        /// <summary>Translate a Core chapter to a display label without changing its identity or save.</summary>
        public static string DisplayChapterName(ArenaSimulation game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            return game.IsVillage ? "Die Aschengrenze" : game.ChapterName;
        }

        /// <summary>Keep defeat instructions accurate for touch and keyboard devices.</summary>
        public static string DefeatPrompt(bool isMobile)
        {
            return isMobile ? "DEFEATED\nTap RESTART" : "DEFEATED\nPress R to restart";
        }

        /// <summary>Render read-only gameplay data and queue player actions; never own game rules.</summary>
        /// <summary>
        /// Region modules (journal, road, subtitles) still draw with IMGUI on top of the uGUI HUD,
        /// using a skin generated from the same Ashen theme so they read as one interface.
        /// </summary>
        private void OnGUI()
        {
            if (_game == null || _storageFault || MenuOpen || _game.GameOver) return;
            ArenaHudLayout layout = CurrentHudLayout();
            Matrix4x4 previousMatrix = GUI.matrix;
            GUISkin previousSkin = GUI.skin;
            GUI.matrix = layout.GuiMatrix;
            GUI.skin = AshenGuiSkin();
            try
            {
                DrawRoadHud(layout);
                DrawRattlerHud(layout);
                if (DrawHungerJournal(layout)) { DrawSpeechSubtitle(layout); return; }
                DrawSpeechSubtitle(layout);
            }
            finally
            {
                GUI.matrix = previousMatrix;
                GUI.skin = previousSkin;
            }
        }

        private GUISkin _ashenSkin;

        private GUISkin AshenGuiSkin()
        {
            if (_ashenSkin != null) return _ashenSkin;
            _ashenSkin = Instantiate(GUI.skin);
            Sprite panel = AshenUiTheme.Panel, plain = AshenUiTheme.PanelPlain;
            int pb = Mathf.RoundToInt(panel.border.x), bb = Mathf.RoundToInt(plain.border.x);
            _ashenSkin.box.normal.background = panel.texture;
            _ashenSkin.box.border = new RectOffset(pb, pb, pb, pb);
            _ashenSkin.box.normal.textColor = AshenUiTheme.Parchment;
            _ashenSkin.box.fontSize = 15; _ashenSkin.box.fontStyle = FontStyle.Bold;
            _ashenSkin.box.padding = new RectOffset(14, 14, 10, 10); _ashenSkin.box.wordWrap = true;
            foreach (GUIStyle b in new[] { _ashenSkin.button })
            {
                b.normal.background = plain.texture; b.hover.background = plain.texture; b.active.background = plain.texture;
                b.border = new RectOffset(bb, bb, bb, bb);
                b.normal.textColor = AshenUiTheme.Parchment; b.hover.textColor = Color.white; b.active.textColor = AshenUiTheme.Ember;
                b.fontSize = 14; b.fontStyle = FontStyle.Bold; b.wordWrap = true;
            }
            _ashenSkin.label.normal.textColor = AshenUiTheme.Parchment;
            _labelStyle = new GUIStyle(_ashenSkin.label) { fontSize = 19, fontStyle = FontStyle.Bold };
            _smallStyle = new GUIStyle(_ashenSkin.label) { fontSize = 15, wordWrap = true };
            _smallStyle.normal.textColor = new Color(0.86f, 0.84f, 0.80f);
            return _ashenSkin;
        }
    }
}
