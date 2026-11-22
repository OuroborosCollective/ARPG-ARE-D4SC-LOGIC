using System;
using System.Collections.Generic;
using AreArpg.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// The village economy as the player sees it: workplaces whose stock is the Core's stock, residents who stand
    /// where the Core has them, and the village board. Nothing here owns a number. A log, a sack, a fire or a
    /// figure is shown because the Core says so, and hiding or showing one changes nothing in the Core.
    ///
    /// What is simplified on purpose: forest and mill are not walked through. A resident who leaves is seen as far
    /// as the north gate and again from there on the way home. Figures walk at their own pace along free ground,
    /// so a figure can still be on its way while the Core already counts the work as begun.
    /// </summary>
    public sealed partial class ArenaRuntime
    {
        public const string VillageWorkRoot = "ARE_VillageWork";
        public const string VillageResidentsRoot = "ARE_VillageResidents";
        public const string WorkplacePrefix = "ARE_Work_";
        public const string ResidentPrefix = "ARE_Resident_";
        public const string StockPrefix = "Stock_";
        public const string FireName = "Fire";
        /// <summary>Figures of the village residents: the reviewed villager visuals, taken in turn.</summary>
        public const int ResidentFigures = 3;
        private const float ResidentPace = 1.9f;
        /// <summary>Between two steps of work a resident is free for a moment; only a longer pause leads to the board.</summary>
        private const float IdlePause = 4f;

        /// <summary>One workplace: its Core place, its model, the ground it covers and the goods it can show.</summary>
        public sealed class VillageWorkplace
        {
            public readonly VillageSpot Spot;
            /// <summary>The house whose whole room this model is, or none for a workplace out in the village.</summary>
            public readonly VillagePlace House;
            public readonly string Key;
            /// <summary>Covered ground in the model's own frame (x to the side, z towards the worker), in metres.</summary>
            public readonly Vector2 Centre, Size;
            public readonly float Height;
            public readonly VillageGood[] Goods;
            public readonly int[] Units;
            public readonly bool Fire;

            public VillageWorkplace(VillageSpot spot, string key, float centreX, float centreZ, float width, float depth, float height, bool fire, params int[] stock)
            {
                Spot = spot; Key = key; Centre = new Vector2(centreX, centreZ); Size = new Vector2(width, depth); Height = height; Fire = fire;
                Goods = new VillageGood[stock.Length / 2]; Units = new int[stock.Length / 2];
                for (int i = 0; i < Goods.Length; i++) { Goods[i] = (VillageGood)stock[i * 2]; Units[i] = stock[i * 2 + 1]; }
            }

            /// <summary>A whole room as one model. Its ground is the room of the Core's layout, its front the door.</summary>
            public VillageWorkplace(VillagePlace house, string key, bool fire, params int[] stock)
                : this(VillageSpot.Home, key, 0f, 0f, VillagePlaceLayout.HalfWidthMm(house) / 500f, VillagePlaceLayout.HalfDepthMm(house) / 500f, 2.8f, fire, stock)
            {
                House = house;
            }
        }

        /// <summary>
        /// The workplaces built for the village (Tools/Art/blender/village_work_props.py). The store is Mara's
        /// existing camp; home and gate are places, not props. Metal and the broken axe lie in the smithy, so the
        /// bench no longer shows them. The field (Tools/Art/blender/village_place_rooms.py) lies north of its place.
        /// </summary>
        public static readonly VillageWorkplace[] VillageWorkplaces =
        {
            new VillageWorkplace(VillageSpot.Woodyard, "WorkWoodyard", 0.75f, -0.02f, 3.0f, 1.35f, 1.0f, false, (int)VillageGood.Wood, 12, (int)VillageGood.Axe, 2),
            new VillageWorkplace(VillageSpot.Workbench, "WorkBench", -0.05f, 0.05f, 2.4f, 0.85f, 0.95f, false, (int)VillageGood.WoodFrame, 2, (int)VillageGood.Bow, 2,
                (int)VillageGood.ThornBerry, 2),
            new VillageWorkplace(VillageSpot.Kitchen, "WorkKitchen", -0.55f, -0.03f, 2.66f, 1.5f, 1.8f, true, (int)VillageGood.Ration, 4, (int)VillageGood.Water, 4),
            new VillageWorkplace(VillageSpot.GameRack, "WorkGameRack", 0f, 0f, 2.2f, 0.35f, 1.9f, false, (int)VillageGood.Game, 4),
            new VillageWorkplace(VillageSpot.DryingRack, "WorkDryingRack", 0f, 0f, 3.6f, 0.9f, 1.95f, false, (int)VillageGood.Hide, 3),
            new VillageWorkplace(VillageSpot.Well, "WorkWell", -0.08f, 0f, 2.2f, 1.6f, 2.45f, false),
            new VillageWorkplace(VillageSpot.Board, "WorkBoard", 0f, 0f, 1.8f, 0.6f, 2.3f, false),
            new VillageWorkplace(VillageSpot.Field, "WorkField", 0.8f, -2.0f, 8.8f, 5.2f, 0.7f, false, (int)VillageGood.Compost, 3),
        };

        private sealed class StockDisplay
        {
            public VillageGood Good;
            public Transform[] Units;
        }

        private sealed class ResidentView
        {
            public int Ordinal;
            public Transform Visual;
            public Vector2 At;
            public bool Visible;
            /// <summary>The figure is on the way to a place where it leaves the picture (gate or house door).</summary>
            public bool Leaving;
            /// <summary>Where the figure left the picture: the gate, the home or a house it works in. It comes back the same way.</summary>
            public VillageSpot Door = VillageSpot.Home;
            public bool HasTarget;
            public VillageSpot Target;
            /// <summary>Place among the residents at the same spot, so two never stand in each other.</summary>
            public int Slot;
            /// <summary>Seconds the resident has been without work while the figure still stands at the last workplace.</summary>
            public float Pause;
            public readonly List<Vector2> Path = new List<Vector2>();
        }

        private Transform _villageWork, _villageResidents, _cookingFire;
        /// <summary>The room the village presentation was built for; none while it shows the village itself.</summary>
        private VillagePlace _shownPlace;
        private readonly Transform[] _fieldCrops = new Transform[3];
        private readonly List<Transform> _roomPatients = new List<Transform>();
        private readonly List<StockDisplay> _villageStock = new List<StockDisplay>();
        private readonly List<ResidentView> _residentViews = new List<ResidentView>();
        private readonly VillageWalkGrid _villageWalk = new VillageWalkGrid();
        private readonly int[] _villageSpotUse = new int[VillageEconomyLayout.SpotCount];
        private bool _villageBoardOpen, _villageNeedPending;
        private int _pendingVillageGift = -1;
        private int _villageScrollFinger = -1;
        private float _villageScrollY, _villageScrollLimit, _villageTouchY;

        private static Vector2 Stand(VillageSpot spot) { return new Vector2(VillageEconomyLayout.StandXmm(spot) / 1000f, VillageEconomyLayout.StandZmm(spot) / 1000f); }
        private static Vector2 Place(VillageSpot spot) { return new Vector2(VillageEconomyLayout.PlaceXmm(spot) / 1000f, VillageEconomyLayout.PlaceZmm(spot) / 1000f); }

        /// <summary>
        /// Where the n-th resident bound for a spot is seen out in the village: at the place itself, or, for a place
        /// inside a house, in front of that house's door, where the figure goes out of the picture.
        /// </summary>
        private static Vector2 VillageStand(VillageSpot spot, int slot)
        {
            VillagePlace house = VillageEconomyLayout.HouseOf(spot);
            if (house == VillagePlace.None) return Stand(spot, slot);
            Vector2 door = new Vector2(VillagePlaceLayout.DoorXmm(house), VillagePlaceLayout.DoorZmm(house)) / 1000f;
            return door + new Vector2(VillagePlaceLayout.DoorOutX(house), VillagePlaceLayout.DoorOutZ(house)) * 0.75f;
        }

        /// <summary>Where the n-th resident at a spot stands: the first at the stand point, the others beside it.</summary>
        private static Vector2 Stand(VillageSpot spot, int slot)
        {
            Vector2 stand = Stand(spot), toPlace = Place(spot) - stand;
            Vector2 side = toPlace.sqrMagnitude < 0.01f ? Vector2.right : new Vector2(-toPlace.y, toPlace.x).normalized;
            return stand + side * (((slot + 1) / 2) * 0.9f * (slot % 2 == 1 ? 1f : -1f));
        }

        private Vector3 OnGround(Vector2 at, float lift = 0f) { return new Vector3(at.x, BuildGround(at.x, at.y) + lift, at.y); }

        /// <summary>Rebuild the village work scenery for the current area; called with every scenery rebuild.</summary>
        private void BuildVillageEconomy()
        {
            ClearVillageEconomy();
            if (_game == null || !_game.IsVillage) return;
            // Not chapter scenery: racks, tripod and board are low and open, and the camera must not pull in
            // for them as it does for houses and trees. Both roots are removed here on every rebuild.
            _villageWork = new GameObject(VillageWorkRoot).transform;
            _shownPlace = _game.InsidePlace;
            if (_shownPlace != VillagePlace.None)
            {
                // Inside a house: its room with everything in it. The room stands whether or not anybody works yet.
                BuildVillageRoom(_shownPlace);
                UpdateVillageEconomy(0f);
                return;
            }
            foreach (VillageWorkplace workplace in VillageWorkplaces)
                if (workplace.Spot == VillageSpot.Board) BuildWorkplace(workplace);
            BuildHouseSigns();
            BuildVillageSites();
            _villageNeedPending = true;
            UpdateVillageEconomy(0f);
        }

        private void OpenVillageWorkplaces()
        {
            _villageResidents = new GameObject(VillageResidentsRoot).transform;
            if (_shownPlace != VillagePlace.None) return;
            foreach (VillageWorkplace workplace in VillageWorkplaces)
                if (workplace.Spot != VillageSpot.Board) BuildWorkplace(workplace);
            BuildVillageWalkGrid();
        }

        private void ClearVillageEconomy()
        {
            if (_villageWork != null) Destroy(_villageWork.gameObject);
            if (_villageResidents != null)
            {
                foreach (ResidentView view in _residentViews) if (view.Visual != null) _actorAnimations.Remove(view.Visual);
                Destroy(_villageResidents.gameObject);
            }
            _villageWork = _villageResidents = _cookingFire = null;
            ClearVillageSites();
            _shownPlace = VillagePlace.None;
            Array.Clear(_fieldCrops, 0, _fieldCrops.Length);
            _roomPatients.Clear();
            _villageStock.Clear();
            _residentViews.Clear();
            SetVillageBoardOpen(false);
            _villageNeedPending = false;
            _pendingVillageGift = -1;
        }

        /// <summary>A workplace faces the point its worker stands at; the model's front is its +Z side.</summary>
        private static Quaternion WorkplaceFacing(VillageSpot spot)
        {
            Vector2 toWorker = Stand(spot) - Place(spot);
            return toWorker.sqrMagnitude < 0.0001f ? Quaternion.identity : Quaternion.LookRotation(new Vector3(toWorker.x, 0f, toWorker.y));
        }

        private void BuildWorkplace(VillageWorkplace workplace)
        {
            var host = new GameObject(WorkplacePrefix + workplace.Spot).transform;
            host.SetParent(_villageWork, false);
            host.position = OnGround(Place(workplace.Spot));
            host.rotation = WorkplaceFacing(workplace.Spot);
            GameObject prefab = Resources.Load<GameObject>("ARE/Visuals/" + workplace.Key);
            bool art = prefab != null && PassiveArtVisuals.TryBindPrefab(host, prefab);
            if (!art)
            {
                if (workplace.Spot == VillageSpot.Field) BuildFieldBlockout(host, workplace);
                else
                {
                // No private art: a plain block of the same footprint, with one small cube per unit of stock.
                Transform block = Shape(PrimitiveType.Cube, "Platzhalter_" + workplace.Key, Vector3.zero,
                    new Vector3(workplace.Size.x, Mathf.Min(workplace.Height, 1f), workplace.Size.y), new Color(0.40f, 0.29f, 0.19f));
                block.SetParent(host, false);
                block.localPosition = new Vector3(workplace.Centre.x, Mathf.Min(workplace.Height, 1f) * 0.5f, workplace.Centre.y);
                block.localRotation = Quaternion.identity;
                }
            }
            BindWorkplaceStock(host, workplace, art);
            if (workplace.Spot == VillageSpot.Field) FindFieldCrops(host);
        }

        /// <summary>
        /// Find (or, without art, make) the parts that stand for the stock of a workplace or a room, and its fire.
        /// A stock part that no good of this place claims stays hidden: nothing is shown that nobody counts.
        /// </summary>
        private void BindWorkplaceStock(Transform host, VillageWorkplace workplace, bool art)
        {
            int slot = 0, slots = 0;
            foreach (int units in workplace.Units) slots += units;
            for (int g = 0; g < workplace.Goods.Length; g++)
            {
                var display = new StockDisplay { Good = workplace.Goods[g] };
                var found = new List<Transform>();
                if (art)
                {
                    string prefix = StockPrefix + workplace.Goods[g] + "_";
                    foreach (Transform part in host.GetComponentsInChildren<Transform>(true))
                        if (part.name.StartsWith(prefix, StringComparison.Ordinal)) found.Add(part);
                    found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                }
                else
                {
                    for (int i = 0; i < workplace.Units[g]; i++, slot++)
                    {
                        Transform unit = Shape(PrimitiveType.Cube, StockPrefix + workplace.Goods[g] + "_" + i.ToString("00"), Vector3.zero,
                            Vector3.one * 0.2f, StockColour(workplace.Goods[g]));
                        unit.SetParent(host, false);
                        unit.localPosition = new Vector3(workplace.Centre.x - workplace.Size.x * 0.5f + (slot + 0.5f) * workplace.Size.x / slots,
                            Mathf.Min(workplace.Height, 1f) + 0.1f, workplace.Centre.y);
                        unit.localRotation = Quaternion.identity;
                        found.Add(unit);
                    }
                }
                display.Units = found.ToArray();
                foreach (Transform unit in display.Units) unit.gameObject.SetActive(false);
                _villageStock.Add(display);
            }
            if (art)
                foreach (Transform part in host.GetComponentsInChildren<Transform>(true))
                    if (part.name.StartsWith(StockPrefix, StringComparison.Ordinal) && part.gameObject.activeSelf &&
                        Array.IndexOf(workplace.Goods, StockGood(part.name)) < 0) part.gameObject.SetActive(false);
            if (!workplace.Fire) return;
            if (art)
            {
                foreach (Transform part in host.GetComponentsInChildren<Transform>(true))
                    if (part.name == FireName) _cookingFire = part;
            }
            else
            {
                _cookingFire = Shape(PrimitiveType.Cube, FireName, Vector3.zero, new Vector3(0.4f, 0.4f, 0.4f), new Color(1f, 0.5f, 0.12f));
                _cookingFire.SetParent(host, false);
                _cookingFire.localPosition = new Vector3(0f, Mathf.Min(workplace.Height, 1f) + 0.4f, 0f);
            }
            if (_cookingFire != null) _cookingFire.gameObject.SetActive(false);
        }

        /// <summary>The good a stock part stands for, from its name "Stock_&lt;Good&gt;_&lt;n&gt;"; -1 for an unknown name.</summary>
        private static VillageGood StockGood(string part)
        {
            int end = part.IndexOf('_', StockPrefix.Length);
            string name = end < 0 ? part.Substring(StockPrefix.Length) : part.Substring(StockPrefix.Length, end - StockPrefix.Length);
            return Enum.TryParse(name, out VillageGood good) && Enum.IsDefined(typeof(VillageGood), good) ? good : (VillageGood)(-1);
        }

        private static Color StockColour(VillageGood good)
        {
            switch (good)
            {
                case VillageGood.Wood: case VillageGood.WoodFrame: case VillageGood.Bow: return new Color(0.58f, 0.45f, 0.31f);
                case VillageGood.Axe: case VillageGood.BrokenAxe: case VillageGood.Metal: return new Color(0.34f, 0.35f, 0.38f);
                case VillageGood.Game: case VillageGood.Hide: return new Color(0.50f, 0.36f, 0.24f);
                case VillageGood.Water: return new Color(0.20f, 0.31f, 0.38f);
                case VillageGood.ThornBerry: return new Color(0.56f, 0.10f, 0.15f);
                case VillageGood.Ore: case VillageGood.Stone: return new Color(0.37f, 0.30f, 0.27f);
                case VillageGood.Fitting: return new Color(0.34f, 0.35f, 0.38f);
                case VillageGood.Grain: case VillageGood.SeedGrain: case VillageGood.Straw: return new Color(0.74f, 0.63f, 0.36f);
                case VillageGood.Flour: case VillageGood.Bandage: return new Color(0.86f, 0.83f, 0.76f);
                case VillageGood.Herb: return new Color(0.34f, 0.45f, 0.29f);
                case VillageGood.Leather: case VillageGood.Compost: return new Color(0.36f, 0.25f, 0.17f);
                default: return new Color(0.60f, 0.55f, 0.45f);
            }
        }

        /// <summary>Free ground for the figures: no building, no workplace, none of the village's standing people and things.</summary>
        private void BuildVillageWalkGrid()
        {
            _villageWalk.Reset();
            foreach (VillageWorkplace workplace in VillageWorkplaces)
            {
                Quaternion facing = WorkplaceFacing(workplace.Spot);
                Vector3 centre = OnGround(Place(workplace.Spot)) + facing * new Vector3(workplace.Centre.x, 0f, workplace.Centre.y);
                Vector3 side = facing * Vector3.right;
                _villageWalk.BlockBox(new Vector2(centre.x, centre.z), new Vector2(side.x, side.z), workplace.Size.x * 0.5f + 0.35f, workplace.Size.y * 0.5f + 0.35f);
            }
            // Mara's camp with its crates and the three villagers' usual places.
            _villageWalk.BlockDisc(new Vector2(ArenaSimulation.CampXmm / 1000f, ArenaSimulation.CampZmm / 1000f), 1.0f);
            _villageWalk.BlockDisc(new Vector2(-2.9f, -5.5f), 1.1f);
            foreach (NpcAgent npc in _game.Npcs) _villageWalk.BlockDisc(new Vector2(npc.Xmm / 1000f, npc.Zmm / 1000f), 0.8f);
            if (_buildLayer != null)
                foreach (Transform piece in _buildLayer)
                    _villageWalk.BlockDisc(new Vector2(piece.position.x, piece.position.z), 0.9f);
        }

        /// <summary>Show the Core's stock and people. Seconds only move figures; they never reach the Core.</summary>
        private void UpdateVillageEconomy(float seconds)
        {
            if (_game == null) return;
            VillageEconomyState economy = _game.Progression.Economy;
            bool wanted = _game.IsVillage;
            if (wanted != (_villageWork != null) || (wanted && _shownPlace != _game.InsidePlace))
            {
                if (wanted) BuildVillageEconomy();
                else ClearVillageEconomy();
                return;
            }
            if (!wanted) return;
            // The lots are there from the first visit on, before anybody has moved in.
            if (_shownPlace == VillagePlace.None) UpdateVillageSites();
            if (!economy.Started) return;
            if (_villageResidents == null) OpenVillageWorkplaces();
            // Checked against the Core every frame: whatever else touched a unit, the next frame shows the truth again.
            foreach (StockDisplay display in _villageStock)
            {
                int count = economy.Commons(display.Good);
                for (int i = 0; i < display.Units.Length; i++)
                    if (display.Units[i] != null && display.Units[i].gameObject.activeSelf != i < count) display.Units[i].gameObject.SetActive(i < count);
            }
            // The fire burns where somebody is at it: out in the village the cooking fire, in a room its forge or hearth.
            bool cooking = false; int lying = 0;
            foreach (VillageResidentState resident in economy.Residents)
            {
                VillageSpot at;
                if (!_game.SeenInVillage(resident, out at) || resident.Walking) continue;
                if (_shownPlace == VillagePlace.None)
                    cooking |= resident.Activity == VillageActivity.Working && at == VillageSpot.Kitchen;
                else if (VillageEconomyLayout.HouseOf(at) == _shownPlace)
                {
                    cooking = true;
                    if (resident.Activity == VillageActivity.Recovering) lying++;
                }
            }
            if (_cookingFire != null && _cookingFire.gameObject.activeSelf != cooking) _cookingFire.gameObject.SetActive(cooking);
            for (int i = 0; i < _roomPatients.Count; i++)
                if (_roomPatients[i] != null && _roomPatients[i].gameObject.activeSelf != i < lying) _roomPatients[i].gameObject.SetActive(i < lying);
            UpdateFieldCrops(economy);

            if (_villageNeedPending && seconds > 0f)
            {
                // Once per arrival: what the village lacks most is said aloud, the rest stands on the board.
                _villageNeedPending = false;
                List<string> needs = VillageBoard.LocalNeeds(_game.Progression);
                if (needs.Count > 0) _message += " / Dorftafel: " + needs[0];
            }
            Array.Clear(_villageSpotUse, 0, _villageSpotUse.Length);
            for (int i = _residentViews.Count; i < economy.Residents.Count; i++) _residentViews.Add(CreateResidentView(economy.Residents[i]));
            Array.Clear(_villageSpotUse, 0, _villageSpotUse.Length);
            for (int i = 0; i < _residentViews.Count && i < economy.Residents.Count; i++)
            {
                if (_shownPlace == VillagePlace.None) StepResidentView(_residentViews[i], economy.Residents[i], seconds);
                else StepRoomResident(_residentViews[i], economy.Residents[i], seconds);
            }
        }

        private ResidentView CreateResidentView(VillageResidentState resident)
        {
            var view = new ResidentView { Ordinal = resident.Ordinal };
            int figure = (resident.Ordinal - VillageServicesState.BasePopulation) % ResidentFigures;
            if (figure < 0) figure += ResidentFigures;
            Color colour = figure == 0 ? new Color(0.62f, 0.46f, 0.32f) : figure == 1 ? new Color(0.50f, 0.36f, 0.30f) : new Color(0.40f, 0.44f, 0.34f);
            view.Visual = Shape(PrimitiveType.Capsule, ResidentPrefix + resident.Ordinal, new Vector3(0f, 1f, 0f), new Vector3(0.8f, 1f, 0.8f), colour);
            view.Visual.SetParent(_villageResidents, true);
            PassiveArtVisuals.TryBindGrounded(view.Visual, "Villager" + figure, 0f);
            var shared = _entryProfile ? Resources.Load<RuntimeAnimatorController>("ARE/Visuals/EntryMelee") : null;
            foreach (Animator animator in view.Visual.GetComponentsInChildren<Animator>(true))
            {
                if (shared != null && PassiveArtVisuals.NeedsSharedLocomotion(animator)) animator.runtimeAnimatorController = shared;
                animator.applyRootMotion = false;
            }
            RegisterActorMotion(view.Visual);
            // A game that starts or loads with the village already at work shows everybody in place at once.
            VillageSpot spot;
            bool seen = _game.SeenInVillage(resident, out spot);
            if (_shownPlace != VillagePlace.None)
            {
                // Inside a room: whoever stands at work here is at their place; everybody else is not in this room.
                view.Visible = seen && VillageEconomyLayout.HouseOf(spot) == _shownPlace && resident.Activity != VillageActivity.Recovering;
                view.Slot = view.Visible ? _villageSpotUse[(int)spot]++ : 0;
                view.At = view.Visible ? Stand(spot, view.Slot) : RoomDoorInside(_shownPlace);
                view.Target = spot; view.HasTarget = view.Visible;
                PlaceResident(view, Vector2.zero);
                if (view.Visible) FaceWorkplace(view, spot);
                view.Visual.gameObject.SetActive(view.Visible);
                return view;
            }
            // Out in the village a resident is seen at the places under the open sky; in a house, behind its door.
            view.Visible = seen && VillageEconomyLayout.HouseOf(spot) == VillagePlace.None;
            if (!seen) spot = resident.Walking || resident.Site != VillageSite.Village ? VillageSpot.Gate : VillageSpot.Home;
            view.Door = spot;
            view.Slot = _villageSpotUse[(int)spot]++;
            view.At = VillageStand(spot, view.Slot);
            view.Target = spot; view.HasTarget = view.Visible;
            PlaceResident(view, Vector2.zero);
            if (view.Visible) FaceWorkplace(view, spot);
            view.Visual.gameObject.SetActive(view.Visible);
            return view;
        }

        private void PlaceResident(ResidentView view, Vector2 moved)
        {
            view.Visual.position = OnGround(view.At, 1f);
            AnimateActor(view.Visual, (long)(moved.x * 1000f), (long)(moved.y * 1000f));
        }

        private void FaceWorkplace(ResidentView view, VillageSpot spot)
        {
            Vector2 look = Place(spot) - view.At;
            // At the house door and at the gate there is nothing to look at: turn towards the village.
            if (look.sqrMagnitude < 0.01f) look = -view.At;
            if (look.sqrMagnitude > 0.0001f) view.Visual.rotation = Quaternion.LookRotation(new Vector3(look.x, 0f, look.y));
        }

        /// <summary>
        /// Move one figure towards where the Core has its resident. A resident who is not seen any more (away,
        /// asleep) still walks the visible rest of the way and then leaves the picture.
        /// </summary>
        private void StepResidentView(ResidentView view, VillageResidentState resident, float seconds)
        {
            VillageSpot spot;
            bool seen = _game.SeenInVillage(resident, out spot);
            // At work or in care inside a house the resident is behind its door: the figure walks there and is gone.
            bool indoors = seen && VillageEconomyLayout.HouseOf(spot) != VillagePlace.None;
            if (!seen || indoors)
            {
                // Indoors to sleep, otherwise out through the gate.
                if (!seen) spot = resident.Site == VillageSite.Village && !resident.Walking ? VillageSpot.Home : VillageSpot.Gate;
                if (!view.Visible) { view.Door = spot; return; }
                seen = false;
            }
            else if (!view.Visible)
            {
                // Back in the picture the way the figure left it: through the gate, or out of a house.
                view.At = VillageStand(view.Door, 0);
                view.Visible = true; view.HasTarget = false;
                view.Visual.gameObject.SetActive(true);
            }
            if (seen && spot == VillageSpot.Board && view.HasTarget && !view.Leaving && view.Target != VillageSpot.Board)
            {
                view.Pause += Mathf.Max(0f, seconds);
                if (view.Pause < IdlePause) spot = view.Target;
            }
            else view.Pause = 0f;
            int slot = _villageSpotUse[(int)spot]++;
            if (!view.HasTarget || view.Target != spot || view.Slot != slot || view.Leaving == seen)
            {
                view.Target = spot; view.Slot = slot; view.HasTarget = true; view.Leaving = !seen;
                _villageWalk.Find(view.At, VillageStand(spot, slot), view.Path);
            }
            Vector2 before = view.At;
            float budget = resident.Walking && resident.Activity == VillageActivity.Resting ? 0f : ResidentPace * Mathf.Max(0f, seconds);
            while (view.Path.Count > 0 && budget > 0f)
            {
                Vector2 step = view.Path[0] - view.At;
                float length = step.magnitude;
                if (length <= budget) { view.At = view.Path[0]; view.Path.RemoveAt(0); budget -= length; }
                else { view.At += step / length * budget; budget = 0f; }
            }
            bool arrived = view.Path.Count == 0;
            PlaceResident(view, arrived ? Vector2.zero : view.At - before);
            if (!arrived) return;
            if (view.Leaving)
            {
                view.Visible = false; view.HasTarget = false; view.Leaving = false;
                view.Door = spot;
                view.Visual.gameObject.SetActive(false);
            }
            else FaceWorkplace(view, spot);
        }

        // ---------------------------------------------------------------- the village board

        /// <summary>The button that opens the board, offered while the player stands at it.</summary>
        public static Rect VillageBoardToggleRect(ArenaHudLayout layout) { return layout.ContextActionRect; }

        /// <summary>One layout for drawing and for touch: the board, its close button and up to six hand-over buttons.</summary>
        public static Rect VillageBoardPanel(ArenaHudLayout layout)
        {
            float height = Mathf.Min(600f, layout.Height - 58f);
            return new Rect((layout.Width - 600f) * .5f, (layout.Height - height) * .5f, 600f, height);
        }

        public static Rect VillageBoardCloseRect(ArenaHudLayout layout)
        { Rect panel = VillageBoardPanel(layout); return new Rect(panel.xMax - 132f, panel.y + 8f, 120f, 30f); }

        public static Rect VillageGiftRect(ArenaHudLayout layout, int index)
        {
            Rect panel = VillageBoardPanel(layout);
            return new Rect(panel.x + 14f + (index % 3) * 192f, panel.yMax - 92f + (index / 3) * 42f, 188f, 38f);
        }

        public static Rect VillageBoardTextRect(ArenaHudLayout layout, int giftCount)
        {
            Rect panel = VillageBoardPanel(layout);
            float footerTop = giftCount > 0 ? VillageGiftRect(layout, 0).yMin : panel.yMax - 54f;
            return Rect.MinMaxRect(panel.x + 16f, panel.y + 46f, panel.xMax - 16f, footerTop - 6f);
        }

        /// <summary>What the player could hand over right now: goods the pack holds and the village works with.</summary>
        private List<VillageGood> VillageGifts()
        {
            var gifts = new List<VillageGood>();
            if (!_game.Progression.Economy.Started) return gifts;
            foreach (VillageGood good in VillageBoard.GiftGoods)
                if (_game.Progression.Crafting.VillageGiftCount(good) > 0) gifts.Add(good);
            return gifts;
        }

        private void SetVillageBoardOpen(bool open)
        {
            _villageBoardOpen = open;
            _villageScrollFinger = -1;
            _villageScrollY = _villageScrollLimit = 0f;
        }

        /// <summary>
        /// A reading gesture ends with its finger. A finger can vanish without an end or cancel phase - the app
        /// was paused, the system took the touch - and would otherwise keep the board for itself: no later tap
        /// could close it or hand anything over. Called on every input frame, and when the app leaves the screen.
        /// </summary>
        private void ReleaseVillageBoardScroll(bool unlessFingerDown)
        {
            if (_villageScrollFinger < 0) return;
            if (unlessFingerDown)
                for (int i = 0; i < Input.touchCount; i++)
                    if (Input.GetTouch(i).fingerId == _villageScrollFinger) return;
            _villageScrollFinger = -1;
        }

        // A gesture belongs to the region in which it began, even if the finger crosses a gift button.
        private bool VillageBoardTouch(ArenaHudLayout layout, int finger, TouchPhase phase, Vector2 point)
        {
            if (!_villageBoardOpen)
            {
                if (phase != TouchPhase.Began || !VillageBoardToggleRect(layout).Contains(point)) return false;
                SetVillageBoardOpen(true);
                return true;
            }
            if (_villageScrollFinger >= 0)
            {
                if (finger != _villageScrollFinger) return true;
                if (phase == TouchPhase.Moved)
                {
                    _villageScrollY = Mathf.Clamp(_villageScrollY + _villageTouchY - point.y, 0f, _villageScrollLimit);
                    _villageTouchY = point.y;
                }
                if (phase == TouchPhase.Ended || phase == TouchPhase.Canceled) _villageScrollFinger = -1;
                return true;
            }
            if (phase != TouchPhase.Began) return true;
            if (VillageBoardCloseRect(layout).Contains(point)) { SetVillageBoardOpen(false); return true; }
            List<VillageGood> gifts = VillageGifts();
            if (VillageBoardTextRect(layout, gifts.Count).Contains(point))
            {
                _villageScrollFinger = finger;
                _villageTouchY = point.y;
                return true;
            }
            for (int i = 0; i < gifts.Count; i++)
                if (VillageGiftRect(layout, i).Contains(point)) { _pendingVillageGift = (int)gifts[i]; break; }
            return true;
        }

        /// <summary>The board is read standing at it; the Core checks the distance again when goods are handed over.</summary>
        private bool PollVillageBoardInput(ArenaHudLayout layout)
        {
            if (!_game.VillageBoardInReach || _hungerJournalOpen) { if (_villageBoardOpen) SetVillageBoardOpen(false); return false; }
            ReleaseVillageBoardScroll(true);
            bool consumed = false;
            if (Input.GetKeyDown(KeyCode.N) || (_villageBoardOpen && Input.GetKeyDown(KeyCode.Escape)))
            { SetVillageBoardOpen(!_villageBoardOpen); consumed = true; }
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.fingerId != _villageScrollFinger && !layout.IsWithinSafeArea(touch.position)) continue;
                consumed |= VillageBoardTouch(layout, touch.fingerId, touch.phase, layout.ToGuiTouch(touch.position));
            }
            if (!consumed && !_villageBoardOpen) return false;
            _game.SetMoveInput(0, 0);
            ReleasePresentationTouches();
            return true;
        }

        /// <summary>Hand one unit over on the game tick; the Core decides and the save follows an accepted hand-over.</summary>
        private void ProcessVillageGift()
        {
            if (_pendingVillageGift < 0) return;
            var good = (VillageGood)_pendingVillageGift;
            _pendingVillageGift = -1;
            SocialResult result = _game.GiveToVillage(good, 1);
            if (!string.IsNullOrEmpty(result.Text)) _message = result.Text;
            if (result.Accepted) SaveCharacter();
        }

        /// <summary>The text of the board, read from the Core at the moment it is drawn.</summary>
        private string VillageBoardText()
        {
            CharacterProgression p = _game.Progression;
            var text = new System.Text.StringBuilder();
            foreach (string line in VillageBoard.StockLines(p)) text.AppendLine(line);
            text.AppendLine();
            foreach (VillageResidentState resident in p.Economy.Residents) text.AppendLine(VillageBoard.ResidentNotice(resident));
            List<string> needs = VillageBoard.LocalNeeds(p);
            text.AppendLine();
            foreach (string need in needs) text.AppendLine(need);
            return text.ToString();
        }

        private void DrawBoardText(Rect viewport, bool touch)
        {
            var body = new GUIContent(VillageBoardText());
            const float gutter = 20f;
            float width = viewport.width - gutter;
            float height = _smallStyle.CalcHeight(body, width);
            _villageScrollLimit = Mathf.Max(0f, height - viewport.height);
            _villageScrollY = Mathf.Clamp(_villageScrollY, 0f, _villageScrollLimit);
            if (_villageScrollLimit > 0f)
            {
                Rect rail = new Rect(viewport.xMax - 15f, viewport.y, 15f, viewport.height);
                if (touch)
                {
                    float thumb = Mathf.Max(18f, rail.height * rail.height / height);
                    GUI.Box(rail, GUIContent.none, GUI.skin.verticalScrollbar);
                    GUI.Box(new Rect(rail.x, rail.y + (rail.height - thumb) * _villageScrollY / _villageScrollLimit,
                        rail.width, thumb), GUIContent.none, GUI.skin.verticalScrollbarThumb);
                }
                else
                {
                    Event input = Event.current;
                    if (input.type == EventType.ScrollWheel && viewport.Contains(input.mousePosition))
                    {
                        _villageScrollY = Mathf.Clamp(_villageScrollY + input.delta.y * 20f, 0f, _villageScrollLimit);
                        input.Use();
                    }
                    _villageScrollY = GUI.VerticalScrollbar(rail, _villageScrollY, viewport.height, 0f, height);
                }
            }
            GUI.BeginGroup(new Rect(viewport.x, viewport.y, width, viewport.height));
            GUI.Label(new Rect(0f, -_villageScrollY, width, height), body, _smallStyle);
            GUI.EndGroup();
        }

        /// <summary>Draw the board button while the player stands at the board, and the board itself when opened.</summary>
        private bool DrawVillageBoard(ArenaHudLayout layout)
        {
            if (!_game.VillageBoardInReach || _hungerJournalOpen) return false;
            bool touch = Application.isMobilePlatform;
            if (!_villageBoardOpen)
            {
                if (touch) GUI.Box(VillageBoardToggleRect(layout), "DORFTAFEL LESEN");
                else if (GUI.Button(VillageBoardToggleRect(layout), "N: DORFTAFEL LESEN")) SetVillageBoardOpen(true);
                return false;
            }
            Rect panel = VillageBoardPanel(layout);
            GUI.Box(panel, "");
            GUI.Label(new Rect(panel.x + 16f, panel.y + 10f, panel.width - 160f, 26f), "DORFTAFEL VON ASCHENGRENZE", _labelStyle);
            if (touch) GUI.Box(VillageBoardCloseRect(layout), "SCHLIESSEN");
            else if (GUI.Button(VillageBoardCloseRect(layout), "SCHLIESSEN (N)")) SetVillageBoardOpen(false);
            List<VillageGood> gifts = VillageGifts();
            DrawBoardText(VillageBoardTextRect(layout, gifts.Count), touch);
            if (gifts.Count == 0)
                GUI.Label(new Rect(panel.x + 16f, panel.yMax - 54f, panel.width - 32f, 46f),
                    _game.Progression.Economy.Started ? "Hier kannst du Holz, Dornbeeren, Ascheisen, Heilkräuter, Holzrahmen oder einen Bogen abgeben."
                        : "Sobald jemand einzieht, kann das Dorf Material für seine Werkstätten annehmen.", _smallStyle);
            for (int i = 0; i < gifts.Count; i++)
            {
                string label = "ABGEBEN: " + VillageEconomyRules.GoodName(gifts[i]) + " (du hast " + _game.Progression.Crafting.VillageGiftCount(gifts[i]) + ")";
                if (touch) GUI.Box(VillageGiftRect(layout, i), label);
                else if (GUI.Button(VillageGiftRect(layout, i), label)) _pendingVillageGift = (int)gifts[i];
            }
            return true;
        }
    }

    /// <summary>
    /// Free ground of the village for walking figures, on a one-metre grid. Presentation only: the Core's own
    /// movement rules never read it, and a figure that finds no way walks straight.
    /// </summary>
    public sealed class VillageWalkGrid
    {
        public const int Half = 27, Side = Half * 2 + 1;
        private readonly bool[] _blocked = new bool[Side * Side];
        private readonly float[] _cost = new float[Side * Side];
        private readonly int[] _from = new int[Side * Side];
        private readonly List<int> _open = new List<int>();

        private static int Cell(float metres) { return Mathf.Clamp(Mathf.RoundToInt(metres), -Half, Half) + Half; }
        private static int Index(int x, int z) { return z * Side + x; }
        private static Vector2 Centre(int index) { return new Vector2(index % Side - Half, index / Side - Half); }

        /// <summary>Start again from the village's buildings, which nobody walks through.</summary>
        public void Reset()
        {
            for (int z = 0; z < Side; z++)
                for (int x = 0; x < Side; x++)
                    _blocked[Index(x, z)] = !EntryWorldLayout.CanStand((x - Half) * 1000L, (z - Half) * 1000L);
        }

        public bool Blocked(Vector2 at) { return _blocked[Index(Cell(at.x), Cell(at.y))]; }

        public void BlockDisc(Vector2 centre, float radius)
        {
            for (int z = Cell(centre.y - radius); z <= Cell(centre.y + radius); z++)
                for (int x = Cell(centre.x - radius); x <= Cell(centre.x + radius); x++)
                    if ((new Vector2(x - Half, z - Half) - centre).sqrMagnitude <= radius * radius) _blocked[Index(x, z)] = true;
        }

        /// <summary>A turned rectangle: side is its local x direction on the ground.</summary>
        public void BlockBox(Vector2 centre, Vector2 side, float halfWidth, float halfDepth)
        {
            side.Normalize();
            var front = new Vector2(-side.y, side.x);
            float reach = Mathf.Sqrt(halfWidth * halfWidth + halfDepth * halfDepth);
            for (int z = Cell(centre.y - reach); z <= Cell(centre.y + reach); z++)
                for (int x = Cell(centre.x - reach); x <= Cell(centre.x + reach); x++)
                {
                    Vector2 offset = new Vector2(x - Half, z - Half) - centre;
                    if (Mathf.Abs(Vector2.Dot(offset, side)) <= halfWidth && Mathf.Abs(Vector2.Dot(offset, front)) <= halfDepth) _blocked[Index(x, z)] = true;
                }
        }

        private bool Clear(Vector2 a, Vector2 b, int start, int goal)
        {
            float length = (b - a).magnitude;
            int samples = Mathf.Max(1, Mathf.CeilToInt(length / 0.3f));
            for (int i = 0; i <= samples; i++)
            {
                Vector2 point = Vector2.Lerp(a, b, i / (float)samples);
                int index = Index(Cell(point.x), Cell(point.y));
                if (_blocked[index] && index != start && index != goal) return false;
            }
            return true;
        }

        /// <summary>
        /// The way from one point to another as a short list of corners, ending at the target itself. The start and
        /// the target may lie on blocked ground (a worker stands close to the workplace). Without a way the list
        /// holds the target alone, and false is returned.
        /// </summary>
        public bool Find(Vector2 from, Vector2 to, List<Vector2> path)
        {
            path.Clear();
            int start = Index(Cell(from.x), Cell(from.y)), goal = Index(Cell(to.x), Cell(to.y));
            if (start == goal || Clear(from, to, start, goal)) { path.Add(to); return true; }
            for (int i = 0; i < _cost.Length; i++) { _cost[i] = float.MaxValue; _from[i] = -1; }
            _open.Clear();
            _cost[start] = 0f; _open.Add(start);
            bool found = false;
            while (_open.Count > 0)
            {
                // The grid is small (55 x 55): the nearest open cell is found by looking at all of them.
                int best = 0;
                float bestScore = float.MaxValue;
                for (int i = 0; i < _open.Count; i++)
                {
                    float score = _cost[_open[i]] + (Centre(_open[i]) - Centre(goal)).magnitude;
                    if (score < bestScore) { bestScore = score; best = i; }
                }
                int current = _open[best];
                _open[best] = _open[_open.Count - 1]; _open.RemoveAt(_open.Count - 1);
                if (current == goal) { found = true; break; }
                int cx = current % Side, cz = current / Side;
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int nx = cx + dx, nz = cz + dz;
                        if (nx < 0 || nz < 0 || nx >= Side || nz >= Side) continue;
                        int next = Index(nx, nz);
                        if (_blocked[next] && next != goal) continue;
                        // No squeezing diagonally between two blocked cells.
                        if (dx != 0 && dz != 0 && (_blocked[Index(cx + dx, cz)] || _blocked[Index(cx, cz + dz)])) continue;
                        float cost = _cost[current] + (dx != 0 && dz != 0 ? 1.4142f : 1f);
                        if (cost >= _cost[next]) continue;
                        if (_cost[next] == float.MaxValue) _open.Add(next);
                        _cost[next] = cost; _from[next] = current;
                    }
            }
            if (!found) { path.Add(to); return false; }
            var cells = new List<Vector2>();
            for (int at = goal; at != start && at >= 0; at = _from[at]) cells.Add(Centre(at));
            cells.Reverse();
            cells[cells.Count - 1] = to;
            // Keep only the corners: from each point walk straight to the farthest one that is in clear view.
            Vector2 standing = from;
            int reached = -1;
            while (reached < cells.Count - 1)
            {
                int farthest = reached + 1;
                for (int candidate = cells.Count - 1; candidate > reached + 1; candidate--)
                    if (Clear(standing, cells[candidate], start, goal)) { farthest = candidate; break; }
                standing = cells[farthest]; reached = farthest;
                path.Add(standing);
            }
            return true;
        }
    }
}
