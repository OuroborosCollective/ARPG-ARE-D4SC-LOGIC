using System;
using System.Collections.Generic;
using AreArpg.Core;
using UnityEngine;
using Input = AreArpg.UnityClient.PlayerDevice;

namespace AreArpg.UnityClient
{
    public sealed partial class ArenaRuntime
    {
        private enum WorldTargetKind { Story, Choice, Npc, Vase, CourtTrace, Relic, Loot, Supplies, KilnGate, Hunter, Orsel, Board, Site, HouseDoor }
        private sealed class WorldTarget
        {
            public WorldTargetKind Kind;
            public int Id;
            public string Verb;
            public Vector3 Anchor;
            public Vector2 Point;
            public Rect Hit;
            public List<HungerAction> Actions;
            public LootState Loot;
            public SupplyDrop Supply;
        }

        private readonly List<WorldTarget> _worldTargets = new List<WorldTarget>();
        private readonly HashSet<(WorldTargetKind, int)> _seenInteractions = new HashSet<(WorldTargetKind, int)>();
        private WorldTarget _worldConversation;
        private Action _pendingWorldInteraction;
        private Texture2D _worldRing;
        private Vector3 _interactionGlance;
        private float _interactionGlanceSeconds;

        private static string WorldNpcName(int id) =>
            id == 0 ? "Ilyan" : id == SocialLedger.MaraNpcId ? "Mara" : id == 2 ? "Dorn" : "Jäger";

        private void ResetWorldInteractions()
        {
            _worldConversation = null;
            _pendingWorldInteraction = null;
            _worldTargets.Clear();
            _seenInteractions.Clear();
            _interactionGlanceSeconds = 0;
        }

        private void AddWorldTarget(WorldTargetKind kind, int id, long x, long z, string verb, float height = .65f,
            LootState loot = null, SupplyDrop supply = null)
        {
            float wx = x * .001f, wz = z * .001f;
            _worldTargets.Add(new WorldTarget
            {
                Kind = kind, Id = id, Verb = verb, Loot = loot, Supply = supply,
                Anchor = new Vector3(wx, GroundHeight(wx, wz) + height, wz)
            });
        }

        private static bool IsWorldChoice(HungerAction action) =>
            action == HungerAction.OpenDungeonSeal || action == HungerAction.BreakDungeonSeal ||
            action == HungerAction.SpareCubs || action == HungerAction.SkinCubs ||
            action == HungerAction.SecureFreight || action == HungerAction.ReleaseFreight;

        private static string WorldStoryVerb(HungerAction action)
        {
            switch (action)
            {
                case HungerAction.OpenWestChest: case HungerAction.OpenEastChest:
                case HungerAction.ForestChest0: case HungerAction.ForestChest1:
                case HungerAction.ForestChest2: case HungerAction.ForestChest3:
                case HungerAction.OpenInnerDoor: return "Öffnen";
                case HungerAction.BeginDungeonRun: return "Absteigen";
                case HungerAction.TakeFreightOriginal: return "Mitnehmen";
                case HungerAction.InspectFreightNotice: case HungerAction.ReadFreightManifest: return "Lesen";
                default: return "Untersuchen";
            }
        }

        private void RefreshWorldTargets(ArenaHudLayout layout)
        {
            _worldTargets.Clear();
            if (!ImguiLayerVisible || _camera == null) return;
            foreach (var npc in _game.Npcs)
                if (_game.CanTalkToWorldNpc(npc.Id) && _npcVisuals.TryGetValue(npc.Id, out var actor) && actor != null)
                {
                    AddWorldTarget(WorldTargetKind.Npc, npc.Id, npc.Xmm, npc.Zmm, "Sprechen");
                    _worldTargets[_worldTargets.Count - 1].Anchor = actor.position + Vector3.up * 1.35f;
                }
            for (int i = 0; i <= (int)HungerAction.BreakKilnVase; i++)
            {
                var action = (HungerAction)i;
                if (!_game.TryGetWorldStorySite(action, out long x, out long z, out int speaker)) continue;
                if (speaker >= 0)
                {
                    var npcTarget = _worldTargets.Find(t => t.Kind == WorldTargetKind.Npc && t.Id == speaker);
                    if (npcTarget == null) continue;
                    if (npcTarget.Actions == null) npcTarget.Actions = new List<HungerAction>();
                    npcTarget.Actions.Add(action);
                }
                else if (IsWorldChoice(action))
                {
                    int group = action <= HungerAction.BreakDungeonSeal ? 0 : action <= HungerAction.SkinCubs ? 1 : 2;
                    var target = _worldTargets.Find(t => t.Kind == WorldTargetKind.Choice && t.Id == group);
                    if (target == null)
                    {
                        AddWorldTarget(WorldTargetKind.Choice, group, x, z, "Entscheiden");
                        target = _worldTargets[_worldTargets.Count - 1];
                        target.Actions = new List<HungerAction>();
                    }
                    target.Actions.Add(action);
                }
                else AddWorldTarget(WorldTargetKind.Story, i, x, z, WorldStoryVerb(action));
            }
            if (_game.InCourt)
                foreach (var trace in CourtLore.Traces)
                    if (_game.CanReadWorldTrace(trace))
                        AddWorldTarget(WorldTargetKind.CourtTrace, trace.Id, trace.Xmm, trace.Zmm, "Untersuchen");
            if (_game.InKilnYard)
            {
                if (_game.NearKilnGate)
                    AddWorldTarget(WorldTargetKind.KilnGate, 0, KilnYard.GateXmm, KilnYard.GateZmm,
                        KilnYard.OnLatchSide(_game.PlayerXmm, _game.PlayerZmm) ? "Öffnen" : "Untersuchen", 1.35f);
                foreach (var vase in KilnYard.Vases)
                    if (_game.CanBreakWorldVase(vase.Id))
                        AddWorldTarget(WorldTargetKind.Vase, vase.Id, vase.Xmm, vase.Zmm, "Zerschlagen");
            }
            if (_game.OrselInReach && _orselFigure != null && _game.TryGetOrselTeachingSpot(out long ox, out long oz))
            {
                AddWorldTarget(WorldTargetKind.Orsel, 0, ox, oz, "Sprechen");
                _worldTargets[_worldTargets.Count - 1].Anchor = _orselFigure.position + Vector3.up * 1.35f;
            }
            if (_game.VillageBoardInReach && _game.InsidePlace == VillagePlace.None)
                AddWorldTarget(WorldTargetKind.Board, 0, VillageEconomyLayout.PlaceXmm(VillageSpot.Board),
                    VillageEconomyLayout.PlaceZmm(VillageSpot.Board), "Lesen", 1.3f);
            var site = _game.SiteInReach;
            if (site != VillagePlace.None)
                AddWorldTarget(WorldTargetKind.Site, (int)site, VillageBuildRules.AskerXmm(site), VillageBuildRules.AskerZmm(site), "Sprechen", 1.3f);
            var door = _game.DoorInReach;
            if (door != VillagePlace.None && !_game.DoorsBarred)
                AddWorldTarget(WorldTargetKind.HouseDoor, (int)door, VillagePlaceLayout.DoorXmm(door), VillagePlaceLayout.DoorZmm(door), "Betreten", 1.3f);
            if (_game.AtRoomDoor)
                AddWorldTarget(WorldTargetKind.HouseDoor, 0, VillagePlaceLayout.RoomDoorXmm(_game.InsidePlace),
                    VillagePlaceLayout.RoomDoorZmm(_game.InsidePlace), "Verlassen", 1.3f);

            var road = _game.Progression.Road;
            if (_game.Progression.EntryAct == 3 && !_game.Progression.Companion.HasJoined)
            {
                long dx = _game.PlayerXmm - HunterEncounter.HunterX, dz = _game.PlayerZmm - HunterEncounter.HunterZ;
                if (dx * dx + dz * dz <= 2400L * 2400 && (road.Rescued && !road.FavorClaimed || road.HunterHealth == 0 && road.Purse > 0))
                    AddWorldTarget(WorldTargetKind.Hunter, 3, HunterEncounter.HunterX, HunterEncounter.HunterZ,
                        road.HunterHealth > 0 ? "Sprechen" : "Untersuchen", road.HunterHealth > 0 ? 1.35f : .45f);
            }
            foreach (var relic in RegionalRelics.All)
                if (_game.TryGetWorldRelic(relic, out long rx, out long rz))
                    AddWorldTarget(WorldTargetKind.Relic, (int)relic, rx, rz, "Mitnehmen");
            foreach (var loot in _game.GroundLoot)
                if (_game.CanTakeWorldLoot(loot))
                    AddWorldTarget(WorldTargetKind.Loot, loot.Id, loot.Xmm, loot.Zmm, "Sammeln", .45f, loot: loot);
            foreach (var bag in _game.Progression.Supplies.Ground)
                if (_game.CanTakeWorldSupply(bag))
                    AddWorldTarget(WorldTargetKind.Supplies, bag.Id, bag.Xmm, bag.Zmm, "Sammeln", .45f, supply: bag);

            // Clustered targets keep independent touch areas. The ring stays at the physical anchor.
            for (int i = 0; i < _worldTargets.Count; i++)
            {
                var target = _worldTargets[i];
                Vector3 screen = _camera.WorldToScreenPoint(target.Anchor);
                if (screen.z <= 0 || !layout.IsWithinSafeArea(screen)) continue;
                target.Point = layout.ToGuiTouch(screen);
                target.Hit = PlaceWorldLabel(layout, target.Point, i);
            }
        }

        private Rect PlaceWorldLabel(ArenaHudLayout layout, Vector2 point, int index)
        {
            bool nearNpc = _game.NearestNpc() != null;
            bool camp = _game.CampPanelVisible;
            bool mastery = _game.Progression.UnspentMasteryPoints > 0;
            bool takeRation = _game.Progression.EntryAct == 2 && camp;
            bool donate = _game.CanDonateCampRation;
            bool Blocks(Vector2 p) => layout.BlocksCameraGesture(p, false, nearNpc, camp, mastery, takeRation, donate);
            foreach (int dy in WorldLabelRows)
                foreach (int dx in WorldLabelColumns)
                {
                    float x = Mathf.Clamp(point.x - 55f + dx, 4f, layout.Width - 114f);
                    float y = Mathf.Clamp(point.y + dy, 4f, layout.Height - 52f);
                    var label = new Rect(x, y, 110f, 48f);
                    if (Blocks(label.center) || Blocks(label.min) || Blocks(label.max) ||
                        Blocks(new Vector2(label.xMin, label.yMax)) || Blocks(new Vector2(label.xMax, label.yMin))) continue;
                    bool occupied = false;
                    for (int i = 0; i < index; i++)
                        if (_worldTargets[i].Hit.Overlaps(label)) { occupied = true; break; }
                    if (!occupied) return label;
                }
            return default;
        }

        private static readonly int[] WorldLabelRows = { -74, 26, -124, 76, -174, 126 };
        private static readonly int[] WorldLabelColumns = { 0, -114, 114, -228, 228 };

        private bool TryTapWorldTarget(Vector2 screen, Vector2 point)
        {
            if (_worldConversation != null) return true;
            if (_camera == null) return false;
            foreach (var target in _worldTargets)
                if (target.Hit.width > 0 && target.Hit.Contains(point))
                    return ActivateWorldTarget(target);
            WorldTarget ring = null;
            float ringDistance = 26f * 26f;
            foreach (var target in _worldTargets)
            {
                if (target.Hit.width <= 0) continue;
                float distance = (point - target.Point).sqrMagnitude;
                if (distance >= ringDistance) continue;
                ring = target; ringDistance = distance;
            }
            if (ring != null) return ActivateWorldTarget(ring);
            Ray ray = _camera.ScreenPointToRay(screen);
            WorldTarget direct = null;
            float closest = float.MaxValue;
            foreach (var target in _worldTargets)
            {
                if (target.Hit.width <= 0 || target.Kind != WorldTargetKind.Vase) continue;
                var vase = _kilnVases[target.Id];
                if (vase == null || !vase.gameObject.activeInHierarchy) continue;
                foreach (var renderer in vase.GetComponentsInChildren<Renderer>())
                    if (renderer.enabled && renderer.bounds.IntersectRay(ray, out float depth) && depth < closest)
                    { direct = target; closest = depth; }
            }
            return direct != null && ActivateWorldTarget(direct);
        }

        private bool ActivateWorldTarget(WorldTarget target)
        {
            if (_pendingWorldInteraction != null) return true;
            if (target.Kind == WorldTargetKind.Npc || target.Kind == WorldTargetKind.Choice ||
                target.Kind == WorldTargetKind.Hunter || target.Kind == WorldTargetKind.Orsel)
            {
                _worldConversation = target;
                ReleasePresentationTouches();
                _game.SetMoveInput(0, 0);
                _moveWorldX = _moveWorldZ = 0;
                return true;
            }
            _pendingWorldInteraction = () => ExecuteWorldTarget(target);
            return true;
        }

        private void ExecuteWorldTarget(WorldTarget target)
        {
            switch (target.Kind)
            {
                case WorldTargetKind.Story: ExecuteWorldStory((HungerAction)target.Id); break;
                case WorldTargetKind.CourtTrace: ApplyWorldResult(_game.ReadWorldTrace(target.Id)); break;
                case WorldTargetKind.KilnGate: ApplyWorldResult(_game.ActOnHunger(HungerAction.OpenKilnGate)); break;
                case WorldTargetKind.Vase: ApplyWorldResult(_game.BreakWorldVase(target.Id)); break;
                case WorldTargetKind.Relic: ApplyWorldResult(TakeRegionalRelic((RegionalRelic)target.Id)); break;
                case WorldTargetKind.Board: if (_game.VillageBoardInReach) SetVillageBoardOpen(true); break;
                case WorldTargetKind.Site: if (_game.SiteInReach == (VillagePlace)target.Id) _sitePanelOpen = true; break;
                case WorldTargetKind.HouseDoor:
                    var from = _game.InsidePlace;
                    if (target.Id == 0 ? _game.TryLeavePlace() :
                        _game.DoorInReach == (VillagePlace)target.Id && _game.TryEnterPlace())
                    { ResetWorldInteractions(); ShowVillagePlace(from); }
                    break;
                case WorldTargetKind.Loot: ProcessPickup(() => _game.TryTakeWorldLoot(target.Loot)); break;
                case WorldTargetKind.Supplies: ProcessPickup(() => _game.TryTakeWorldSupply(target.Supply)); break;
            }
        }

        private void ExecuteWorldStory(HungerAction action)
        {
            if (!_game.TryGetWorldStorySite(action, out _, out _, out _))
            { _message = "Dieses Ziel ist nicht mehr verfügbar."; return; }
            string binding = AuthoredHungerBinding.ActionId(action);
            var result = binding == null ? _game.ActOnHunger(action) : AuthoredHungerBinding.Execute(_game, binding);
            ApplyWorldResult(result, PresentedStoryResult(action, result));
        }

        private void ApplyWorldResult(SocialResult result, string text = null)
        {
            _message = text ?? result.Text;
            if (string.IsNullOrEmpty(_message)) _message = "Dieses Ziel ist nicht mehr verfügbar.";
            _atmosphere.AcceptedSpeech(result);
            if (result.Accepted) SaveCharacter();
        }

        private readonly List<(string Label, Action Execute)> _worldAnswers = new List<(string, Action)>();

        private string WorldDialogueLine(WorldTarget target)
        {
            if (target.Kind == WorldTargetKind.Orsel) return _game.OrselCourseText;
            if (target.Kind == WorldTargetKind.Hunter)
                return _game.Progression.Road.HunterHealth > 0 ? "Du hast mir das Leben gerettet. Ich stehe in deiner Schuld." :
                    "Beim toten Jäger liegt ein Münzbeutel.";
            if (target.Kind == WorldTargetKind.Choice)
                return target.Id == 0 ? "Das Siegel hält einen alten Mechanismus. Wie willst du den Zugang öffnen?" :
                    target.Id == 1 ? "Drei Junge drängen sich im Nest zusammen. Was willst du tun?" :
                    "Der Frachtbeleg ist geprüft. Wie soll es mit der Sendung weitergehen?";
            if (target.Id == SocialLedger.MaraNpcId)
            {
                var social = _game.Progression.Social;
                if (social.CampRefilled) return "Dank deiner Rationen konnten wir das Lager wieder auffüllen.";
                int missing = SocialLedger.CampCapacity - social.CampProvisions;
                if (missing == 1) return "Uns fehlt noch eine Ration.";
                return missing > 1 ? "Uns fehlen noch " + missing + " Rationen." :
                    "Die Vorräte sind im Lager. Was führt dich zu mir?";
            }
            if (target.Actions != null && target.Actions.Contains(HungerAction.HearIlyan))
                return "Du wolltest mich sprechen? Hier können wir leise reden.";
            return target.Actions != null ? "Du bist zurück. Was hast du herausgefunden?" : "Was führt dich zu mir?";
        }

        private void RefreshWorldAnswers()
        {
            _worldAnswers.Clear();
            var target = _worldConversation;
            if (target.Actions != null)
                foreach (var action in target.Actions)
                    if (_game.TryGetWorldStorySite(action, out _, out _, out int owner) &&
                        (target.Kind != WorldTargetKind.Npc || owner == target.Id))
                        _worldAnswers.Add((HungerActionLabel(action), () => ExecuteWorldStory(action)));
            if (target.Kind == WorldTargetKind.Npc)
            {
                int id = target.Id;
                if (id == SocialLedger.MaraNpcId && _game.CanDonateCampRation)
                    _worldAnswers.Add(("Eine Ration übergeben", () =>
                    { if (_game.CanTalkToWorldNpc(id)) ApplyWorldResult(_game.DonateCampRation()); }));
                _worldAnswers.Add(("Grüßen", () =>
                {
                    if (!_game.CanTalkToWorldNpc(id)) return;
                    var result = _game.InteractNpc(id, NpcAction.Greet);
                    ApplyWorldResult(result, PresentedNpcResult(result));
                }));
            }
            if (target.Kind == WorldTargetKind.Orsel)
                for (int choice = 1; choice <= OrselLessonChoice; choice++)
                {
                    string label = OrselChoiceLabel(choice);
                    if (label == null) continue;
                    int selected = choice;
                    _worldAnswers.Add((label, () => ApplyWorldResult(selected == OrselLessonChoice ?
                        _game.LearnWundschluss() : _game.HelpOrsel((HealingCoursePath)selected))));
                }
            if (target.Kind == WorldTargetKind.Hunter)
            {
                bool corpse = _game.Progression.Road.HunterHealth == 0;
                _worldAnswers.Add((corpse ? "Den Münzbeutel nehmen" : "Für den Münzbeutel danken",
                    () => ApplyWorldResult(_game.InteractHunter(corpse))));
            }
            _worldAnswers.Add(("Verlassen", null));
        }

        private static Rect WorldConversationPanel(ArenaHudLayout layout, int count)
        {
            float width = Mathf.Min(700f, layout.Width - 32f);
            return new Rect((layout.Width - width) * .5f, layout.Height - 110f - count * 48f, width, 94f + count * 48f);
        }

        private static Rect WorldAnswerRect(Rect panel, int index) =>
            new Rect(panel.x + 16f, panel.y + 88f + index * 48f, panel.width - 32f, 44f);

        private void SelectWorldAnswer(int index)
        {
            _pendingWorldInteraction = _worldAnswers[index].Execute;
            _worldConversation = null;
            _worldAnswers.Clear();
        }

        private bool PollWorldConversation(ArenaHudLayout layout)
        {
            if (_worldConversation == null) return false;
            if (Input.GetKeyDown(KeyCode.Escape) ||
                _worldConversation.Kind == WorldTargetKind.Npc && !_game.CanTalkToWorldNpc(_worldConversation.Id) ||
                _worldConversation.Kind == WorldTargetKind.Orsel && !_game.OrselInReach)
            { _worldConversation = null; return true; }
            RefreshWorldAnswers();
            Rect panel = WorldConversationPanel(layout, _worldAnswers.Count);
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began || !layout.IsWithinSafeArea(touch.position)) continue;
                Vector2 point = layout.ToGuiTouch(touch.position);
                for (int j = 0; j < _worldAnswers.Count; j++)
                    if (WorldAnswerRect(panel, j).Contains(point)) { SelectWorldAnswer(j); return true; }
            }
            _game.SetMoveInput(0, 0);
            ReleasePresentationTouches();
            return true;
        }

        private bool DrawWorldConversation(ArenaHudLayout layout)
        {
            if (_worldConversation == null) return false;
            RefreshWorldAnswers();
            Rect panel = WorldConversationPanel(layout, _worldAnswers.Count);
            bool npc = _worldConversation.Kind == WorldTargetKind.Npc || _worldConversation.Kind == WorldTargetKind.Hunter;
            GUI.Box(panel, npc ? WorldNpcName(_worldConversation.Id) :
                _worldConversation.Kind == WorldTargetKind.Orsel ? "Orsel" : "Entscheidung");
            GUI.Label(new Rect(panel.x + 16, panel.y + 31, panel.width - 32, 53), WorldDialogueLine(_worldConversation), _smallStyle);
            for (int i = 0; i < _worldAnswers.Count; i++)
            {
                Rect rect = WorldAnswerRect(panel, i);
                if (Application.isMobilePlatform) GUI.Box(rect, _worldAnswers[i].Label);
                else if (GUI.Button(rect, _worldAnswers[i].Label)) { SelectWorldAnswer(i); break; }
            }
            return true;
        }

        private void DrawWorldTargets()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (_worldRing == null)
            {
                _worldRing = new Texture2D(64, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                var pixels = new Color32[64 * 64];
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        float radius = new Vector2(x - 31.5f, y - 31.5f).magnitude;
                        if (radius >= 28f && radius <= 31f) pixels[y * 64 + x] = new Color32(255, 224, 147, 255);
                        else if (radius < 28f) pixels[y * 64 + x] = new Color32(21, 15, 11, 190);
                    }
                _worldRing.SetPixels32(pixels);
                _worldRing.Apply(false, true);
            }
            foreach (var target in _worldTargets)
            {
                if (target.Hit.width <= 0) continue;
                DrawWorldMarkerStem(target);
                GUI.DrawTexture(new Rect(target.Point.x - 22f, target.Point.y - 22f, 44f, 44f), _worldRing);
                GUI.Box(target.Hit, target.Verb);
            }
        }

        private static void DrawWorldMarkerStem(WorldTarget target)
        {
            Vector2 end = new Vector2(Mathf.Clamp(target.Point.x, target.Hit.xMin, target.Hit.xMax),
                Mathf.Clamp(target.Point.y, target.Hit.yMin, target.Hit.yMax));
            Vector2 delta = end - target.Point;
            if (delta.sqrMagnitude <= 22f * 22f) return;
            Matrix4x4 matrix = GUI.matrix;
            Color color = GUI.color;
            GUI.color = new Color(1f, .78f, .48f, .8f);
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, target.Point);
            GUI.DrawTexture(new Rect(target.Point.x, target.Point.y - 1f, delta.magnitude, 2f), Texture2D.whiteTexture);
            GUI.matrix = matrix;
            GUI.color = color;
        }

        private void UpdateWorldCamera()
        {
            if (_cameraRig == null) return;
            if (_worldConversation != null)
            {
                Vector3 focus = _worldConversation.Anchor;
                if (_worldConversation.Kind == WorldTargetKind.Npc &&
                    _npcVisuals.TryGetValue(_worldConversation.Id, out var speaker) && speaker != null)
                    focus = speaker.position + Vector3.up * 1.35f;
                _cameraRig.SetInteractionFocus(focus, true);
                return;
            }
            WorldTarget fresh = null;
            foreach (var target in _worldTargets)
                if (_seenInteractions.Add((target.Kind, target.Id))) fresh = target;
            if (_worldTargets.Count == 1 && fresh != null)
            { _interactionGlance = fresh.Anchor; _interactionGlanceSeconds = .7f; }
            if (_worldTargets.Count != 1 || MenuOpen || _hungerJournalOpen || _sceneActive || _game.GameOver)
                _interactionGlanceSeconds = 0;
            _interactionGlanceSeconds = Mathf.Max(0, _interactionGlanceSeconds - Time.unscaledDeltaTime);
            _cameraRig.SetInteractionFocus(_interactionGlanceSeconds > 0 ? _interactionGlance : (Vector3?)null, false);
        }
    }
}
