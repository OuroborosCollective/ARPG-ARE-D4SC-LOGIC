using System;
using System.Collections.Generic;
using AreArpg.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>Presentation and input only. The Unity physics engine never decides gameplay outcomes.</summary>
    public sealed class ArenaRuntime : MonoBehaviour
    {
        private const ulong DemoSeed = 0xA11CE2026UL;
        private const string CharacterSaveKey = "ARE_ARPG_character_ac1";
        private const string CharacterBackupKey = "ARE_ARPG_character_ac1_backup";
        private const int CatchUpLimit = 5;
        private const float TickSeconds = ArenaSimulation.TickMs / 1000f;

        private ArenaSimulation _game;
        private float _accumulator;
        private Transform _hero;
        private Transform _cameraTarget;
        private Camera _camera;
        private readonly List<Transform> _chapterScenery = new List<Transform>();
        // Read-only projection index; SocialLedger exclusively owns the stock.
        private readonly List<Transform> _campRationVisuals = new List<Transform>();
        private Light _sun;
        private Transform _bossWarning;
        private bool _bossWarningVisible;
        private readonly Dictionary<int, Transform> _monsterVisuals = new Dictionary<int, Transform>();
        private readonly Dictionary<int, Transform> _lootVisuals = new Dictionary<int, Transform>();
        private readonly Dictionary<int, Transform> _npcVisuals = new Dictionary<int, Transform>();
        private readonly Queue<KeyValuePair<int, NpcAction>> _pendingNpcActions = new Queue<KeyValuePair<int, NpcAction>>();
        private readonly Queue<int> _pendingSkills = new Queue<int>();
        private bool _pendingPickup;
        private bool _pendingCampDonation;
        private readonly Queue<MasteryPath> _pendingMastery = new Queue<MasteryPath>();
        private bool _storageFault;
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
            if (_game.ChapterId == 1)
                _message = "Aschengrenze: Lager, Wache und Nordpass. Deine Taten bleiben in Erinnerung.";
            BuildWorld();
        }

        private static bool TryLoadCharacter(out CharacterProgression saved)
        {
            saved = null;
            string primary = PlayerPrefs.GetString(CharacterSaveKey, "");
            string backup = PlayerPrefs.GetString(CharacterBackupKey, "");
            if (string.IsNullOrEmpty(primary) && string.IsNullOrEmpty(backup))
            {
                saved = new CharacterProgression();
                return true;
            }
            if (CharacterProgression.TryLoad(primary, out saved)) return true;
            if (CharacterProgression.TryLoad(backup, out saved)) return true;
            saved = null;
            return false;
        }

        private void SaveCharacter()
        {
            if (_game != null) SaveCharacter(_game.Progression);
        }

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
            if (paused) SaveCharacter();
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
            if (renderer != null) renderer.sharedMaterial = Material(color, metallic);
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

            _hero = Shape(PrimitiveType.Capsule, "Wanderer", new Vector3(0f, 1.05f, 0f),
                new Vector3(0.85f, 1.05f, 0.85f), new Color(0.20f, 0.54f, 0.71f), 0.65f);
            PassiveArtVisuals.TryBind(_hero, "Hero");
            Transform blade = Shape(PrimitiveType.Cube, "Wanderer's sword", Vector3.zero,
                new Vector3(0.16f, 1.2f, 0.14f), new Color(0.83f, 0.77f, 0.59f), 0.7f);
            blade.SetParent(_hero, false);
            blade.localPosition = new Vector3(0.85f, 0.12f, 0.1f);
            blade.localRotation = Quaternion.Euler(0f, 0f, -22f);
            PassiveArtVisuals.TryBind(blade, "Sword");
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
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.055f, 0.067f, 0.09f);
            _camera.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
            _camera.transform.position = new Vector3(0f, 17f, -18f);
            _cameraTarget = _hero;
        }

        private Transform ChapterShape(PrimitiveType primitive, string label, Vector3 position,
            Vector3 scale, Color color)
        {
            Transform shape = Shape(primitive, label, position, scale, color);
            _chapterScenery.Add(shape);
            string key = null;
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
            if (key != null) PassiveArtVisuals.TryBind(shape, key);
            return shape;
        }

        /// <summary>Only changes projection. Core collision rules remain canonical and unchanged.</summary>
        private void BuildChapterScenery()
        {
            foreach (Transform part in _chapterScenery)
                if (part != null) Destroy(part.gameObject);
            _chapterScenery.Clear();
            _campRationVisuals.Clear();
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
            ChapterShape(PrimitiveType.Cube, "ARE_Camp_Supply", new Vector3(ArenaSimulation.CampXmm / 1000f, 0.4f, ArenaSimulation.CampZmm / 1000f),
                new Vector3(1.1f, 0.8f, 0.8f), new Color(0.44f, 0.31f, 0.18f));
            if (!ember)
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
                BuildAschengrenzeScenery();
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

        private void SpawnMonster(MonsterState monster)
        {
            PrimitiveType primitive = monster.Kind >= 2 ? PrimitiveType.Capsule : PrimitiveType.Sphere;
            Vector3 scale = monster.Kind >= 2 ? new Vector3(2.5f, 2.1f, 2.5f) :
                (monster.Kind == 1 ? new Vector3(1.8f, 1.8f, 1.8f) : Vector3.one * 1.45f);
            Color color = monster.Kind == 3 ? new Color(0.95f, 0.33f, 0.14f) :
                (monster.Kind == 2 ? new Color(0.55f, 0.13f, 0.14f) :
                (monster.Kind == 1 ? new Color(0.50f, 0.39f, 0.18f) : new Color(0.35f, 0.65f, 0.32f)));
            Transform root = Shape(primitive, "Enemy_" + monster.Id,
                new Vector3(monster.Xmm / 1000f, scale.y * 0.6f, monster.Zmm / 1000f), scale, color);
            _monsterVisuals.Add(monster.Id, root);
            PassiveArtVisuals.TryBind(root, "Enemy" + monster.Kind);
            if (monster.Kind == 3)
            {
                Transform crown = Shape(PrimitiveType.Cylinder, "Ember_Warden_Crown",
                    Vector3.zero, new Vector3(1.3f, 0.12f, 1.3f),
                    new Color(1f, 0.68f, 0.2f));
                crown.SetParent(root, false);
                crown.localPosition = new Vector3(0f, 0.75f, 0f);
            }
        }

        private static Rect SkillRect(int i)
        {
            float width = Mathf.Clamp(Screen.width * 0.125f, 74f, 145f);
            float height = Mathf.Clamp(Screen.height * 0.13f, 48f, 67f);
            int col = i % 2;
            int row = i / 2;
            return new Rect(Screen.width - (2 - col) * (width + 10f) - 12f,
                Screen.height - (2 - row) * (height + 9f) - 16f, width, height);
        }

        private static Rect PickupRect()
        {
            return new Rect(Screen.width * 0.43f, Screen.height - 72f, Screen.width * 0.16f, 52f);
        }

        private static Rect NpcActionRect(int action)
        {
            float width = Mathf.Min(300f, Screen.width - 24f);
            return new Rect(Screen.width - width - 10f + (action % 2) * (width / 2f),
                266f + (action / 2) * 42f, width / 2f - 5f, 38f);
        }

        private void QueueNpcAction(NpcAction action)
        {
            NpcAgent npc = _game.NearestNpc();
            if (npc != null) _pendingNpcActions.Enqueue(new KeyValuePair<int, NpcAction>(npc.Id, action));
        }

        private bool QueueNpcTouch(Vector2 position)
        {
            if (_game.NearestNpc() == null) return false;
            for (int i = 0; i < 4; i++)
                if (NpcActionRect(i).Contains(position)) { QueueNpcAction((NpcAction)i); return true; }
            return false;
        }

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
                PassiveArtVisuals.TryBind(visual, "Npc" + npc.Id);
                _npcVisuals.Add(npc.Id, visual);
            }
        }

        /// <summary>One touch/desktop donation region; the Core validates the queued request.</summary>
        private static Rect SupplyDonationRect()
        {
            return new Rect(12f, 249f, Mathf.Min(290f, Screen.width * 0.40f), 37f);
        }

        /// <summary>Queue inputs for the authoritative fixed simulation tick.</summary>
        private void PollInputs()
        {
            long x = 0, z = 0;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 10000;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 10000;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) z += 10000;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) z -= 10000;

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                Vector2 pos = touch.position; // Input touch y grows from the bottom.
                if (pos.x < Screen.width * 0.34f && pos.y < Screen.height * 0.46f &&
                    touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled)
                {
                    Vector2 center = new Vector2(110f, 110f);
                    Vector2 direction = Vector2.ClampMagnitude((pos - center) / 75f, 1f);
                    x = (long)(direction.x * 10000f);
                    z = (long)(direction.y * 10000f);
                }
                else if (touch.phase == TouchPhase.Began)
                {
                    Vector2 guiPos = new Vector2(pos.x, Screen.height - pos.y);
                    bool handledByUi = QueueNpcTouch(guiPos);
                    for (int ability = 0; ability < 4 && !handledByUi; ability++)
                        if (SkillRect(ability).Contains(guiPos))
                        {
                            _pendingSkills.Enqueue(ability);
                            handledByUi = true;
                            break;
                        }
                    if (!handledByUi && _game.ChapterId == 1 &&
                        SupplyDonationRect().Contains(guiPos))
                    {
                        _pendingCampDonation = true;
                        handledByUi = true;
                    }
                    if (!handledByUi && PickupRect().Contains(guiPos)) _pendingPickup = true;
                }
            }
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
            if (Input.GetKeyDown(KeyCode.G)) _pendingCampDonation = true;
        }

        private void Restart()
        {
            if (_storageFault) return;
            SaveCharacter();
            if (_storageFault) return;
            InstallNewRun(new ArenaSimulation(DemoSeed, _game.Progression, _game.CryptDepth),
                "Crypt restarted - character progression retained.");
        }

        private void AdvanceCrypt()
        {
            if (_storageFault || !_game.CryptCleared || !_game.AtWorldPassage)
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
            InstallNewRun(next, "Du betrittst " + next.ChapterName + ". Der Weg fuehrt tiefer.");
            _regionFade = 1f;
            _hero.position = new Vector3(next.PlayerXmm / 1000f, 1.05f, next.PlayerZmm / 1000f);
            if (_camera != null)
                _camera.transform.position = _hero.position - Quaternion.Euler(52f, 0f, 0f) * Vector3.forward * 22f;
        }

        private void InstallNewRun(ArenaSimulation next, string message)
        {
            _game = next;
            _passageAnnounced = false;
            BuildChapterScenery();
            _pendingSkills.Clear();
            _pendingMastery.Clear();
            _pendingPickup = false;
            _pendingCampDonation = false;
            _pendingNpcActions.Clear();
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
        }

        private void Update()
        {
            if (_storageFault) return;
            _regionFade = Mathf.Max(0f, _regionFade - Time.deltaTime / 0.75f);
            PollInputs();
            _accumulator += Mathf.Min(Time.deltaTime, 0.25f);
            int steps = 0;
            while (_accumulator >= TickSeconds && steps < CatchUpLimit)
            {
                _accumulator -= TickSeconds;
                steps++;
                while (_pendingNpcActions.Count > 0)
                {
                    KeyValuePair<int, NpcAction> request = _pendingNpcActions.Dequeue();
                    SocialResult result = _game.InteractNpc(request.Key, request.Value);
                    if (!string.IsNullOrEmpty(result.Text)) _message = result.Text;
                    if (result.Accepted) SaveCharacter();
                    if (_storageFault) break;
                }
                if (_storageFault) break;
                if (_pendingCampDonation)
                {
                    _pendingCampDonation = false;
                    SocialResult donation = _game.DonateCampRation();
                    _message = !string.IsNullOrEmpty(donation.Text) ? donation.Text :
                        "Nur direkt bei Mara kannst du eine Lagerration abgeben.";
                    if (donation.Accepted) SaveCharacter();
                    if (_storageFault) break;
                }
                while (_pendingMastery.Count > 0)
                {
                    MasteryPath path = _pendingMastery.Dequeue();
                    bool spent = _game.Progression.TrySpendMastery(path);
                    _message = spent ? "Mastery allocated: " + path : "No mastery points available";
                    if (spent) SaveCharacter();
                }
                while (_pendingSkills.Count > 0)
                {
                    int slot = _pendingSkills.Dequeue();
                    System.Numerics.BigInteger previousRank = _game.Progression.PracticeLevel(slot);
                    SkillResult outcome = _game.ActivateSkill(slot);
                    _message = outcome.Message;
                    bool learned = _game.Progression.PracticeLevel(slot) > previousRank;
                    if (learned) _message += " / Durch Uebung gewachsen: Rang " + _game.Progression.PracticeLevel(slot);
                    if (outcome.Kills > 0 || learned) SaveCharacter();
                }
                if (_pendingPickup)
                {
                    bool pickedUp = _game.TryCollectLoot();
                    _message = pickedUp ? "Equipment upgraded!" : "No loot nearby";
                    if (pickedUp) SaveCharacter();
                    _pendingPickup = false;
                }
                _game.Step();
                if (_game.CryptCleared && _game.AtWorldPassage)
                {
                    AdvanceCrypt();
                    if (_storageFault) break;
                }
            }
            UpdatePresentation(Mathf.Clamp01(_accumulator / TickSeconds));
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
            }
            if (_worldPassage != null)
                _worldPassage.gameObject.SetActive(_game.CryptCleared && _game.GroundLoot.Count == 0);
            if (_game.CryptCleared && !_passageAnnounced)
            {
                _passageAnnounced = true;
                _message = _game.GroundLoot.Count == 0 ? "Ein Luftzug streicht durch den Nordbogen. Folge dem Weg." :
                    "Die Krypta wird still. Sichere deine Beute; der Nordbogen fuehrt weiter.";
            }
            if (_hero != null)
            {
                float x = Mathf.Lerp(_game.PrevPlayerXmm, _game.PlayerXmm, alpha) / 1000f;
                float z = Mathf.Lerp(_game.PrevPlayerZmm, _game.PlayerZmm, alpha) / 1000f;
                _hero.position = new Vector3(x, 1.05f, z);
                if (_camera != null)
                {
                    Quaternion rotation = Quaternion.Euler(52f, 0f, 0f);
                    Vector3 desired = _hero.position - rotation * Vector3.forward * 22f;
                    _camera.transform.position = Vector3.Lerp(_camera.transform.position, desired,
                        1f - Mathf.Exp(-8f * Time.deltaTime));
                    _camera.transform.rotation = rotation;
                }
            }
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
                    visual.position = new Vector3(x, visual.localScale.y * 0.6f, z);
                }
            }
            foreach (LootState item in _game.GroundLoot)
            {
                if (_lootVisuals.ContainsKey(item.Id)) continue;
                _lootVisuals.Add(item.Id, Shape(PrimitiveType.Sphere, "Loot_" + item.Id,
                    new Vector3(item.Xmm / 1000f, 0.55f, item.Zmm / 1000f),
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
            return game.ChapterId == 1 ? "Die Aschengrenze" : game.ChapterName;
        }

        /// <summary>Render read-only gameplay data and queue player actions; never own game rules.</summary>
        private void OnGUI()
        {
            if (_game == null) return;
            if (_storageFault)
            {
                GUI.Box(new Rect(16, 16, Screen.width - 32, 116),
                    "SAVE VERIFICATION FAILED\nYour character was not overwritten.\nRestore a valid save before playing.");
                return;
            }
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold };
                _labelStyle.normal.textColor = new Color(0.96f, 0.92f, 0.82f);
                _smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
                _smallStyle.normal.textColor = new Color(0.86f, 0.88f, 0.92f);
            }
            GUI.Box(new Rect(8, 8, Mathf.Min(600, Screen.width * 0.80f), 132), "");
            GUI.Label(new Rect(20, 15, 540, 28),
                _game.ChapterId == 1 ? "ARE / DIE ASCHENGRENZE" : "ARE / ASHEN CRYPT", _labelStyle);
            GUI.Label(new Rect(20, 43, 460, 24),
                "HP " + _game.PlayerHealth + "/" + _game.MaxPlayerHealth +
                "    Focus " + _game.Resource + "/100" + "    Weapon +" + _game.WeaponBonus, _smallStyle);
            GUI.Label(new Rect(20, 67, 460, 24),
                DisplayChapterName(_game) + "  | Weg in die Tiefe: " + _game.CryptDepth +
                "   Beute " + _game.LootCollected, _smallStyle);
            GUI.Label(new Rect(20, 92, 540, 24),
                "LV " + _game.Progression.Level + "   Next " + _game.Progression.ExperienceToNextLevel +
                " XP   Mastery " + _game.Progression.UnspentMasteryPoints, _smallStyle);
            GUI.Label(new Rect(20, 115, 540, 24), _message, _smallStyle);
            if (_game.ChapterId == 1)
            {
                int missing = SocialLedger.CampCapacity - _game.Progression.Social.CampProvisions;
                GUI.Box(new Rect(8f, 148f, Mathf.Min(310f, Screen.width * 0.43f), 145f), "");
                GUI.Label(new Rect(17f, 155f, 285f, 24f), "DIE GESTOHLENEN VORRAETE", _smallStyle);
                GUI.Label(new Rect(17f, 181f, 285f, 25f),
                    "Maras Lager: " + _game.Progression.Social.CampProvisions + "/" + SocialLedger.CampCapacity + "  Fehlbestand: " + missing, _smallStyle);
                GUI.Label(new Rect(17f, 205f, 285f, 43f),
                    missing > 0 ? "Ilyan hungert. Niemand kennt den Taeter." :
                    "Das Lager ist versorgt. Die Schuldfrage bleibt offen.", _smallStyle);
                if (Application.isMobilePlatform) GUI.Box(SupplyDonationRect(), DonationButtonLabel(true));
                else if (GUI.Button(SupplyDonationRect(), DonationButtonLabel(false)))
                    _pendingCampDonation = true;
            }
            NpcAgent nearby = _game.NearestNpc();
            if (nearby != null)
            {
                float width = Mathf.Min(300f, Screen.width - 24f);
                float left = Screen.width - width - 10f;
                NpcDisposition mood = nearby.Memory.Disposition(nearby.Personality);
                string moodText = mood == NpcDisposition.Friendly ? "freundlich" :
                    (mood == NpcDisposition.Wary ? "misstrauisch" : (mood == NpcDisposition.Hostile ? "feindselig" : "neutral"));
                GUI.Box(new Rect(left - 5f, 144f, width + 5f, 210f), "");
                string characterName = nearby.Id == 0 ? "Ilyan (Pfadleser)" :
                    (nearby.Id == 1 ? "Mara (Versorgerin)" : "Dorn (Wache)");
                GUI.Label(new Rect(left, 148f, width, 25f), characterName + " / " + moodText, _labelStyle);
                GUI.Label(new Rect(left, 177f, width, 26f), "Gute Taten +" + nearby.Memory.PositivePoints + "  Uebergriffe -" + nearby.Memory.NegativePoints, _smallStyle);
                GUI.Label(new Rect(left, 205f, width, 26f), "Hunger " + nearby.Memory.Hunger + "  Erschoepfung " + nearby.Memory.Fatigue, _smallStyle);
                GUI.Label(new Rect(left, 231f, width, 28f), "Proviant " + _game.Progression.Social.Provisions + "  Muenzen " + _game.Progression.Social.Coins, _smallStyle);
                string[] actions = { "F GRUESSEN", "V VERSORGEN", "B ANGREIFEN", "H AUSRAUBEN" };
                Color previousColor = GUI.color;
                for (int i = 0; i < actions.Length; i++)
                {
                    GUI.color = i >= 2 ? new Color(1f, 0.68f, 0.62f) : previousColor;
                    if (Application.isMobilePlatform) GUI.Box(NpcActionRect(i), actions[i]);
                    else if (GUI.Button(NpcActionRect(i), actions[i])) QueueNpcAction((NpcAction)i);
                }
                GUI.color = previousColor;
            }
            GUI.Box(new Rect(25, Screen.height - 194, 170, 170), "MOVE\nWASD / TOUCH");
            if (_game.Progression.UnspentMasteryPoints > 0)
            {
                float buttonWidth = Mathf.Clamp(Screen.width * 0.13f, 72f, 125f);
                string[] masteryLabels = { "+ MIGHT", "+ VITALITY", "+ RESOLVE" };
                for (int i = 0; i < masteryLabels.Length; i++)
                {
                    Rect masteryRect = new Rect(12 + i * (buttonWidth + 5), 146, buttonWidth, 42);
                    if (GUI.Button(masteryRect, masteryLabels[i]))
                        _pendingMastery.Enqueue((MasteryPath)i);
                }
            }

            string[] labels = { "1 KLINGE", "2 WUCHT", "3 SCHUTZ", "4 RUIN" };
            for (int i = 0; i < labels.Length; i++)
            {
                Rect rect = SkillRect(i);
                string title = labels[i] + " R" + _game.Progression.PracticeLevel(i) +
                    "\n" + _game.Progression.PracticeExperience(i) + " XP" +
                    (_game.Cooldowns[i] > 0 ? " (" + _game.Cooldowns[i] + ")" : "");
                if (Application.isMobilePlatform) GUI.Box(rect, title);
                else if (GUI.Button(rect, title)) _pendingSkills.Enqueue(i);
            }
            if (Application.isMobilePlatform) GUI.Box(PickupRect(), "PICK UP");
            else if (GUI.Button(PickupRect(), "PICK UP (E)")) _pendingPickup = true;

            if (_game.GameOver)
            {
                Rect rect = new Rect(Screen.width * 0.3f, Screen.height * 0.38f,
                    Screen.width * 0.4f, 90f);
                GUI.Box(rect, "DEFEATED\nPress R to restart");
                if (GUI.Button(new Rect(rect.x, rect.y + 90, rect.width, 40), "RESTART")) Restart();
            }
            if (_regionFade > 0f)
            {
                Color previous = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, _regionFade);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = previous;
            }
        }
    }
}


