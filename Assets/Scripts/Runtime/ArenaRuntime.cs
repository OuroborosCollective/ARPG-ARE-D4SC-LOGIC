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
        private readonly Dictionary<int, Transform> _monsterVisuals = new Dictionary<int, Transform>();
        private readonly Dictionary<int, Transform> _lootVisuals = new Dictionary<int, Transform>();
        private readonly Queue<int> _pendingSkills = new Queue<int>();
        private bool _pendingPickup;
        private readonly Queue<MasteryPath> _pendingMastery = new Queue<MasteryPath>();
        private bool _storageFault;
        private string _message = "Explore the crypt. Survive the guardian.";
        private GUIStyle _labelStyle;
        private GUIStyle _smallStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallInAnyScene()
        {
            if (FindObjectOfType<ArenaRuntime>() != null) return;
            new GameObject("ARE_ARPG_Runtime").AddComponent<ArenaRuntime>();
        }

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
            _game = new ArenaSimulation(DemoSeed, saved);
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
            if (_storageFault || _game == null) return;
            string primary = PlayerPrefs.GetString(CharacterSaveKey, "");
            CharacterProgression previous;
            if (CharacterProgression.TryLoad(primary, out previous))
                PlayerPrefs.SetString(CharacterBackupKey, primary);
            string next = _game.Progression.SaveString();
            PlayerPrefs.SetString(CharacterSaveKey, next);
            PlayerPrefs.Save();
            CharacterProgression verified;
            if (!CharacterProgression.TryLoad(PlayerPrefs.GetString(CharacterSaveKey, ""), out verified) ||
                verified.StableHash != _game.Progression.StableHash)
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

        private void BuildWorld()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.27f, 0.30f, 0.39f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.055f, 0.067f, 0.09f);
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.014f;
            Shape(PrimitiveType.Cube, "Cracked Crypt Floor", new Vector3(0f, -0.42f, 0f),
                new Vector3(57f, 0.6f, 57f), new Color(0.16f, 0.18f, 0.20f));
            for (int x = -24; x <= 24; x += 8)
                Shape(PrimitiveType.Cube, "Stone inlay", new Vector3(x, -0.105f, 0f),
                    new Vector3(0.13f, 0.025f, 54f), new Color(0.24f, 0.26f, 0.28f));
            for (int z = -24; z <= 24; z += 8)
                Shape(PrimitiveType.Cube, "Stone inlay", new Vector3(0f, -0.103f, z),
                    new Vector3(54f, 0.025f, 0.13f), new Color(0.24f, 0.26f, 0.28f));
            foreach (int x in new[] { -7, 7 })
                foreach (int z in new[] { -7, 7 })
                {
                    Shape(PrimitiveType.Cylinder, "Pillar", new Vector3(x, 2.2f, z),
                        new Vector3(2.2f, 2.2f, 2.2f), new Color(0.25f, 0.25f, 0.28f));
                    Shape(PrimitiveType.Cylinder, "Pillar capital", new Vector3(x, 4.48f, z),
                        new Vector3(2.7f, 0.23f, 2.7f), new Color(0.36f, 0.34f, 0.33f));
                }
            Shape(PrimitiveType.Cylinder, "Crypt Altar", new Vector3(0f, 0.65f, 12.8f),
                new Vector3(3f, 0.7f, 3f), new Color(0.31f, 0.18f, 0.18f));

            var lightGo = new GameObject("ARE Moonlight");
            Light sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.65f, 0.76f, 1f);
            sun.intensity = 1.15f;
            sun.transform.rotation = Quaternion.Euler(50f, -28f, 0f);

            _hero = Shape(PrimitiveType.Capsule, "Wanderer", new Vector3(0f, 1.05f, 0f),
                new Vector3(0.85f, 1.05f, 0.85f), new Color(0.20f, 0.54f, 0.71f), 0.65f);
            Transform blade = Shape(PrimitiveType.Cube, "Wanderer's sword", Vector3.zero,
                new Vector3(0.16f, 1.2f, 0.14f), new Color(0.83f, 0.77f, 0.59f), 0.7f);
            blade.SetParent(_hero, false);
            blade.localPosition = new Vector3(0.85f, 0.12f, 0.1f);
            blade.localRotation = Quaternion.Euler(0f, 0f, -22f);
            foreach (MonsterState monster in _game.Monsters) SpawnMonster(monster);

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

        private void SpawnMonster(MonsterState monster)
        {
            PrimitiveType primitive = monster.Kind == 2 ? PrimitiveType.Capsule : PrimitiveType.Sphere;
            Vector3 scale = monster.Kind == 2 ? new Vector3(2.5f, 2.1f, 2.5f) :
                (monster.Kind == 1 ? new Vector3(1.8f, 1.8f, 1.8f) : Vector3.one * 1.45f);
            Color color = monster.Kind == 2 ? new Color(0.55f, 0.13f, 0.14f) :
                (monster.Kind == 1 ? new Color(0.50f, 0.39f, 0.18f) : new Color(0.35f, 0.65f, 0.32f));
            Transform root = Shape(primitive, "Enemy_" + monster.Id,
                new Vector3(monster.Xmm / 1000f, scale.y * 0.6f, monster.Zmm / 1000f), scale, color);
            _monsterVisuals.Add(monster.Id, root);
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
                    bool foundSkill = false;
                    for (int ability = 0; ability < 4; ability++)
                        if (SkillRect(ability).Contains(guiPos))
                        {
                            _pendingSkills.Enqueue(ability);
                            foundSkill = true;
                            break;
                        }
                    if (!foundSkill && PickupRect().Contains(guiPos)) _pendingPickup = true;
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
            if (Input.GetKeyDown(KeyCode.N) && _game.CryptCleared) AdvanceCrypt();
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
            if (_storageFault || !_game.CryptCleared)
                return;
            if (_game.GroundLoot.Count != 0)
            {
                _message = "Pick up all loot before entering the next crypt.";
                return;
            }
            SaveCharacter();
            if (_storageFault) return;
            ArenaSimulation next = _game.TryAdvanceCrypt();
            if (next == null) return;
            InstallNewRun(next, "Crypt depth " + next.CryptDepth + " - enemies grow stronger.");
        }

        private void InstallNewRun(ArenaSimulation next, string message)
        {
            _game = next;
            _pendingSkills.Clear();
            _pendingMastery.Clear();
            _pendingPickup = false;
            _accumulator = 0f;
            foreach (Transform t in _monsterVisuals.Values) if (t != null) Destroy(t.gameObject);
            foreach (Transform t in _lootVisuals.Values) if (t != null) Destroy(t.gameObject);
            _monsterVisuals.Clear();
            _lootVisuals.Clear();
            foreach (MonsterState monster in _game.Monsters) SpawnMonster(monster);
            _message = message;
        }

        private void Update()
        {
            if (_storageFault) return;
            PollInputs();
            _accumulator += Mathf.Min(Time.deltaTime, 0.25f);
            int steps = 0;
            while (_accumulator >= TickSeconds && steps < CatchUpLimit)
            {
                _accumulator -= TickSeconds;
                steps++;
                while (_pendingMastery.Count > 0)
                {
                    MasteryPath path = _pendingMastery.Dequeue();
                    bool spent = _game.Progression.TrySpendMastery(path);
                    _message = spent ? "Mastery allocated: " + path : "No mastery points available";
                    if (spent) SaveCharacter();
                }
                while (_pendingSkills.Count > 0)
                {
                    SkillResult outcome = _game.ActivateSkill(_pendingSkills.Dequeue());
                    _message = outcome.Message;
                    if (outcome.Kills > 0) SaveCharacter();
                }
                if (_pendingPickup)
                {
                    bool pickedUp = _game.TryCollectLoot();
                    _message = pickedUp ? "Equipment upgraded!" : "No loot nearby";
                    if (pickedUp) SaveCharacter();
                    _pendingPickup = false;
                }
                _game.Step();
            }
            UpdatePresentation(Mathf.Clamp01(_accumulator / TickSeconds));
        }

        private void UpdatePresentation(float alpha)
        {
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
            GUI.Label(new Rect(20, 15, 460, 28), "ARE / ASHEN CRYPT  |  ALPHA VERTICAL SLICE", _labelStyle);
            GUI.Label(new Rect(20, 43, 460, 24),
                "HP " + _game.PlayerHealth + "/" + _game.MaxPlayerHealth +
                "    Focus " + _game.Resource + "/100" + "    Weapon +" + _game.WeaponBonus, _smallStyle);
            GUI.Label(new Rect(20, 67, 460, 24),
                "Crypt " + _game.CryptDepth + "   Kills " + _game.Kills +
                "   Loot " + _game.LootCollected + "   Tick " + _game.CurrentTick, _smallStyle);
            GUI.Label(new Rect(20, 92, 540, 24),
                "LV " + _game.Progression.Level + "   Next " + _game.Progression.ExperienceToNextLevel +
                " XP   Mastery " + _game.Progression.UnspentMasteryPoints, _smallStyle);
            GUI.Label(new Rect(20, 115, 540, 24), _message, _smallStyle);
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

            string[] labels = { "1 STRIKE", "2 HEAVY", "3 GUARD", "4 RUIN" };
            for (int i = 0; i < labels.Length; i++)
            {
                Rect rect = SkillRect(i);
                string title = labels[i] + (_game.Cooldowns[i] > 0 ? " (" + _game.Cooldowns[i] + ")" : "");
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
            else if (_game.CryptCleared)
            {
                Rect victoryRect = new Rect(Screen.width * 0.24f, Screen.height * 0.32f,
                    Screen.width * 0.52f, 126f);
                GUI.Box(victoryRect, "CRYPT CLEARED!\nDepth " + _game.CryptDepth);
                if (_game.GroundLoot.Count != 0)
                    GUI.Label(new Rect(victoryRect.x + 12f, victoryRect.y + 55f,
                        victoryRect.width - 24f, 32f), "Collect remaining loot before continuing.");
                else if (GUI.Button(new Rect(victoryRect.x + 12f, victoryRect.y + 72f,
                    victoryRect.width - 24f, 45f), "NEXT CRYPT  (N)"))
                    AdvanceCrypt();
            }
        }
    }
}
