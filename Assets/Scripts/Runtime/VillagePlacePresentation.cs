using System;
using System.Collections.Generic;
using AreArpg.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// The houses of Aschengrenze that can be entered, as the player sees them. Out in the village a sign hangs at
    /// each of their doors. Walking into a door takes the village down and puts up the room behind it; walking out
    /// does the reverse. The Core decides where the player is, where the walls and the furniture stand, what lies in
    /// stock and who is at work or in care; nothing here owns any of it.
    ///
    /// A room is built at the Core's place for it, far outside the village square. Its walls are seen from inside
    /// only, so the camera may stand outside them and still look in. Light comes from the room's lamps and its fire.
    /// </summary>
    public sealed partial class ArenaRuntime
    {
        public const string VillageRoomRoot = "ARE_VillageRoom";
        public const string HouseSignPrefix = "ARE_HouseSign_";
        public const string LampPrefix = "Lamp_", PatientPrefix = "Patient_", CropPrefix = "Crop_";
        /// <summary>Region number the atmosphere gets inside a house: no ash in the air, boards under the feet.</summary>
        public const int VillageRoomAtmosphere = AtmosphereSurfaces.VillageRoom;
        private const float RoomWall = 2.8f;
        private bool _villageKept, _villageKeptFog;
        private CameraClearFlags _villageKeptClear;
        private Color _villageKeptBackground;
        private static readonly Color RoomAmbient = new Color(0.33f, 0.285f, 0.25f), RoomVoid = new Color(0.016f, 0.014f, 0.015f);

        /// <summary>
        /// The rooms (Tools/Art/blender/village_place_rooms.py), in the order of <see cref="VillagePlace"/>. A room
        /// is one model; like a workplace it shows the goods that lie in it and the fire somebody is at.
        /// </summary>
        public static readonly VillageWorkplace[] VillageRooms =
        {
            new VillageWorkplace(VillagePlace.SickRoom, "RoomSickRoom", true, (int)VillageGood.Herb, 6, (int)VillageGood.Bandage, 4, (int)VillageGood.Leather, 3),
            new VillageWorkplace(VillagePlace.Smithy, "RoomSmithy", true, (int)VillageGood.Ore, 6, (int)VillageGood.Metal, 4, (int)VillageGood.Fitting, 4,
                (int)VillageGood.BrokenAxe, 1),
            new VillageWorkplace(VillagePlace.Barn, "RoomBarn", false, (int)VillageGood.Grain, 8, (int)VillageGood.Flour, 4, (int)VillageGood.Straw, 6,
                (int)VillageGood.SeedGrain, 3),
        };

        /// <summary>The signs at the house doors, in the order of <see cref="VillagePlace"/>.</summary>
        public static readonly string[] HouseSignKeys = { "SignSickRoom", "SignSmithy", "SignBarn" };
        /// <summary>How many beds of the sick room can show somebody lying in them.</summary>
        public const int SickBeds = 3;

        public static VillageWorkplace RoomOf(VillagePlace place) { return VillageRooms[(int)place - 1]; }

        private static Vector3 RoomCentre(VillagePlace place)
        { return new Vector3(VillagePlaceLayout.RoomXmm(place) / 1000f, 0f, VillagePlaceLayout.RoomZmm / 1000f); }

        /// <summary>Just inside the door of a room: where a figure comes into the picture or goes out of it.</summary>
        private static Vector2 RoomDoorInside(VillagePlace place)
        {
            return new Vector2(VillagePlaceLayout.RoomDoorXmm(place) - VillagePlaceLayout.RoomOutX * 700,
                VillagePlaceLayout.RoomDoorZmm(place) - VillagePlaceLayout.RoomOutZ * 700) / 1000f;
        }

        // ---------------------------------------------------------------- through the door

        /// <summary>
        /// Let the player through a house door they walk into, in or out. True when the player changed sides: the
        /// remaining ticks of this frame belong to the side that was just left.
        /// </summary>
        private bool StepVillageDoors()
        {
            VillagePlace into = _game.WalkingIntoHouse, from = _game.InsidePlace;
            bool passed = into != VillagePlace.None ? _game.TryEnterPlace() : _game.WalkingOutOfRoom && _game.TryLeavePlace();
            if (!passed) return false;
            ShowVillagePlace(from);
            return true;
        }

        /// <summary>Put up what the player now stands in, the room or the village, and place hero and camera in it.</summary>
        private void ShowVillagePlace(VillagePlace left)
        {
            VillagePlace now = _game.InsidePlace;
            // What the room takes away from the picture of the village is kept, and put back on the way out even
            // where no sky is drawn that would set it again.
            if (now != VillagePlace.None && left == VillagePlace.None)
            {
                _villageKept = true; _villageKeptFog = RenderSettings.fog;
                if (_camera != null) { _villageKeptClear = _camera.clearFlags; _villageKeptBackground = _camera.backgroundColor; }
            }
            BuildChapterScenery();
            BuildPlacedLayer();
            BuildVillageEconomy();
            if (_atmosphere != null) _atmosphere.EnterRegion(now != VillagePlace.None ? VillageRoomAtmosphere : _game.CurrentAreaId);
            if (now == VillagePlace.None)
            {
                // The torches are chapter scenery of the village; the room took them down.
                if (_game.IsVillage) BuildVillageLights();
                if (_villageKept)
                {
                    RenderSettings.fog = _villageKeptFog;
                    if (_camera != null) { _camera.clearFlags = _villageKeptClear; _camera.backgroundColor = _villageKeptBackground; }
                    _villageKept = false;
                }
                ApplyEntryLighting();
            }
            _regionFade = 1f;
            float x = _game.PlayerXmm / 1000f, z = _game.PlayerZmm / 1000f;
            _hero.position = new Vector3(x, 1.05f + GroundHeight(x, z) + WalkSurfaceHeight(x, z), z);
            // One looks the way one came: into the room away from its door, out of the house away from its door.
            Vector3 look = now != VillagePlace.None ? new Vector3(-VillagePlaceLayout.RoomOutX, 0f, -VillagePlaceLayout.RoomOutZ) :
                left == VillagePlace.None ? Vector3.forward : new Vector3(VillagePlaceLayout.DoorOutX(left), 0f, VillagePlaceLayout.DoorOutZ(left));
            _hero.rotation = Quaternion.LookRotation(look);
            if (_cameraRig != null) { _cameraRig.ClearTouches(); RefreshCameraOccluders(); _cameraRig.SnapToTarget(); }
            _message = now != VillagePlace.None ? "Du betrittst: " + VillagePlaceLayout.Name(now) + "." : "Du trittst wieder hinaus ins Dorf.";
        }

        /// <summary>What the door in front of the player is, for the hint above the controls. Null away from doors.</summary>
        public string VillageDoorHint
        {
            get
            {
                if (_game == null) return null;
                VillagePlace door = _game.DoorInReach;
                if (door != VillagePlace.None)
                    return _game.DoorsBarred ? "TÜR: " + VillagePlaceLayout.Name(door).ToUpperInvariant() + " · BLEIBT DIR VERSCHLOSSEN" :
                        "TÜR: " + VillagePlaceLayout.Name(door).ToUpperInvariant() + " · HINEINGEHEN";
                return _game.AtRoomDoor ? "TÜR: HINAUS INS DORF" : null;
            }
        }

        private void DrawVillageDoorHint(ArenaHudLayout layout)
        {
            if (_villageBoardOpen || _hungerJournalOpen) return;
            string hint = VillageDoorHint;
            if (hint != null) GUI.Box(layout.ContextActionRect, hint);
        }

        // ---------------------------------------------------------------- the room

        /// <summary>The surroundings of a room: no sky, no weather, no village lights. Called instead of the village scenery.</summary>
        private void BuildVillageRoomScenery()
        {
            _villageTorches.Clear();
            RenderSettings.fog = false;
        }

        /// <summary>
        /// Light of a room, set every frame after the sky had its turn: a dim warm ambience, hardly any light from
        /// above, darkness around the walls. Out in the village the sky sets its own light again on the next frame.
        /// </summary>
        private void RefreshVillageRoomLight()
        {
            if (_game == null || _game.InsidePlace == VillagePlace.None) return;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = RoomAmbient;
            ApplyFlatWorldSkyProbe();
            RenderSettings.fog = false;
            if (_sun != null)
            {
                _sun.intensity = 0.14f;
                _sun.color = new Color(0.72f, 0.78f, 1f);
                _sun.shadows = LightShadows.None;
            }
            if (_camera != null) { _camera.clearFlags = CameraClearFlags.SolidColor; _camera.backgroundColor = RoomVoid; }
        }

        private void BuildVillageRoom(VillagePlace place)
        {
            VillageWorkplace room = RoomOf(place);
            var host = new GameObject(VillageRoomRoot).transform;
            host.SetParent(_villageWork, false);
            host.position = RoomCentre(place);
            // The model is built looking in over its south wall, and the export turns that side to the model's +Z:
            // half a turn puts the south wall to the south again and the door in the north wall, as the rules have it.
            host.rotation = Quaternion.LookRotation(Vector3.back);
            GameObject prefab = Resources.Load<GameObject>("ARE/Visuals/" + room.Key);
            bool art = prefab != null && PassiveArtVisuals.TryBindPrefab(host, prefab);
            if (!art) BuildRoomBlockout(host, place);
            BindWorkplaceStock(host, room, art);
            var patients = new List<Transform>();
            foreach (Transform part in host.GetComponentsInChildren<Transform>(true))
            {
                if (part.name.StartsWith(PatientPrefix, StringComparison.Ordinal)) patients.Add(part);
                else if (part.name.StartsWith(LampPrefix, StringComparison.Ordinal)) AddRoomLight(part, new Color(1f, 0.69f, 0.42f), 3.4f, 9.5f);
            }
            patients.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            foreach (Transform patient in patients) patient.gameObject.SetActive(false);
            _roomPatients.AddRange(patients);
            // The fire's light is part of the fire: it burns and goes out with it.
            if (_cookingFire != null) AddRoomLight(_cookingFire, new Color(1f, 0.52f, 0.22f), 3.4f, 9f);
        }

        private static void AddRoomLight(Transform at, Color colour, float intensity, float range)
        {
            var renderer = at.GetComponentInChildren<Renderer>(true);
            var lamp = new GameObject("Licht");
            lamp.transform.SetParent(at, false);
            lamp.transform.position = renderer != null ? renderer.bounds.center + Vector3.up * 0.12f : at.position + Vector3.up * 2f;
            var light = lamp.AddComponent<Light>();
            light.type = LightType.Point; light.color = colour; light.intensity = intensity; light.range = range;
            light.shadows = LightShadows.None;
        }

        /// <summary>Without the private art: floor, three walls and a block for every piece of furniture, as the Core has them.</summary>
        private void BuildRoomBlockout(Transform host, VillagePlace place)
        {
            Vector3 centre = RoomCentre(place);
            float hw = VillagePlaceLayout.HalfWidthMm(place) / 1000f, hd = VillagePlaceLayout.HalfDepthMm(place) / 1000f;
            var floor = new Color(0.37f, 0.28f, 0.20f); var wall = new Color(0.60f, 0.55f, 0.47f); var wood = new Color(0.40f, 0.29f, 0.19f);
            Keep(host, Shape(PrimitiveType.Cube, "Raum_Boden", centre + new Vector3(0f, -0.05f, 0f), new Vector3(hw * 2f, 0.1f, hd * 2f), floor));
            // No wall in the south: there the camera stands. The door is in the north wall.
            Keep(host, Shape(PrimitiveType.Cube, "Raum_Wand_Nord", centre + new Vector3(0f, RoomWall * 0.5f, hd + 0.05f), new Vector3(hw * 2f + 0.2f, RoomWall, 0.1f), wall));
            Keep(host, Shape(PrimitiveType.Cube, "Raum_Tuer", new Vector3(VillagePlaceLayout.RoomDoorXmm(place) / 1000f, 1.05f, VillagePlaceLayout.RoomDoorZmm(place) / 1000f),
                new Vector3(VillagePlaceLayout.DoorHalfWidthMm / 500f, 2.1f, 0.12f), wood));
            foreach (int side in new[] { -1, 1 })
                Keep(host, Shape(PrimitiveType.Cube, side < 0 ? "Raum_Wand_West" : "Raum_Wand_Ost", centre + new Vector3(side * (hw + 0.05f), RoomWall * 0.5f, 0f),
                    new Vector3(0.1f, RoomWall, hd * 2f), wall));
            for (int i = 0; i < VillagePlaceLayout.FurnitureCount(place); i++)
            {
                VillagePlaceLayout.Furniture piece = VillagePlaceLayout.FurnitureAt(place, i);
                Keep(host, Shape(PrimitiveType.Cube, "Moebel_" + piece.Id, centre + new Vector3(piece.X / 1000f, 0.45f, piece.Z / 1000f),
                    new Vector3(piece.HalfWidth / 500f, 0.9f, piece.HalfDepth / 500f), wood));
            }
            foreach (int side in new[] { -1, 1 })
            {
                var lamp = new GameObject(LampPrefix + (side < 0 ? "0" : "1")).transform;
                lamp.SetParent(host, false);
                lamp.position = centre + new Vector3(side * (hw - 0.4f), 2f, 0f);
            }
            if (place == VillagePlace.SickRoom)
                for (int bed = 0; bed < SickBeds; bed++)
                {
                    VillagePlaceLayout.Furniture cot = VillagePlaceLayout.Find(place, "bed" + bed);
                    Keep(host, Shape(PrimitiveType.Cube, PatientPrefix + bed, centre + new Vector3(cot.X / 1000f, 1.05f, cot.Z / 1000f),
                        new Vector3(1.5f, 0.3f, 0.6f), new Color(0.34f, 0.38f, 0.44f)));
                }
        }

        private static void Keep(Transform host, Transform part) { part.SetParent(host, true); }

        // ---------------------------------------------------------------- people in the room

        /// <summary>
        /// Move one figure inside the shown room. Whoever is at work here walks from the door to the workplace;
        /// whoever stops or leaves walks back to the door and is gone. Somebody in care does not stand: a bed shows them.
        /// </summary>
        private void StepRoomResident(ResidentView view, VillageResidentState resident, float seconds)
        {
            VillageSpot spot;
            bool here = _game.SeenInVillage(resident, out spot) && VillageEconomyLayout.HouseOf(spot) == _shownPlace &&
                resident.Activity != VillageActivity.Recovering;
            if (!here && !view.Visible) return;
            int slot = here ? _villageSpotUse[(int)spot]++ : 0;
            if (here && !view.Visible)
            {
                view.At = RoomDoorInside(_shownPlace);
                view.Visible = true; view.HasTarget = false;
                view.Visual.gameObject.SetActive(true);
            }
            if (!view.HasTarget || view.Leaving == here || (here && (view.Target != spot || view.Slot != slot)))
            {
                view.Target = spot; view.Slot = slot; view.HasTarget = true; view.Leaving = !here;
                FindRoomWay(_shownPlace, view.At, here ? Stand(spot, slot) : RoomDoorInside(_shownPlace), view.Path);
            }
            Vector2 before = view.At;
            float budget = ResidentPace * Mathf.Max(0f, seconds);
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
                view.Visual.gameObject.SetActive(false);
            }
            else FaceWorkplace(view, spot);
        }

        private const float RoomCell = 0.25f;
        private static readonly int[] RoomStepX = { 1, -1, 0, 0 }, RoomStepZ = { 0, 0, 1, -1 };

        /// <summary>
        /// A way through a room on the ground the Core lets a body stand on, a quarter of a metre at a time. Without
        /// one (a point inside furniture) the figure walks straight.
        /// </summary>
        public static void FindRoomWay(VillagePlace place, Vector2 from, Vector2 to, List<Vector2> way)
        {
            way.Clear();
            float originX = VillagePlaceLayout.RoomXmm(place) / 1000f - VillagePlaceLayout.HalfWidthMm(place) / 1000f;
            float originZ = VillagePlaceLayout.RoomZmm / 1000f - VillagePlaceLayout.HalfDepthMm(place) / 1000f;
            int width = Mathf.CeilToInt(VillagePlaceLayout.HalfWidthMm(place) / 500f / RoomCell) + 1;
            int depth = Mathf.CeilToInt(VillagePlaceLayout.HalfDepthMm(place) / 500f / RoomCell) + 1;
            Func<Vector2, int> cell = p => Mathf.Clamp(Mathf.RoundToInt((p.x - originX) / RoomCell), 0, width - 1) +
                Mathf.Clamp(Mathf.RoundToInt((p.y - originZ) / RoomCell), 0, depth - 1) * width;
            Func<int, Vector2> point = c => new Vector2(originX + (c % width) * RoomCell, originZ + (c / width) * RoomCell);
            int start = cell(from), goal = cell(to);
            var previous = new int[width * depth];
            for (int i = 0; i < previous.Length; i++) previous[i] = -1;
            var open = new Queue<int>();
            open.Enqueue(start); previous[start] = start;
            while (open.Count > 0 && previous[goal] < 0)
            {
                int at = open.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    int x = at % width + RoomStepX[d], z = at / width + RoomStepZ[d];
                    if (x < 0 || z < 0 || x >= width || z >= depth) continue;
                    int next = x + z * width;
                    if (previous[next] >= 0) continue;
                    Vector2 p = point(next);
                    if (next != goal && !VillagePlaceLayout.CanStandInRoom(place, (long)Mathf.Round(p.x * 1000f), (long)Mathf.Round(p.y * 1000f))) continue;
                    previous[next] = at; open.Enqueue(next);
                }
            }
            if (previous[goal] >= 0)
                for (int at = goal; at != start; at = previous[at]) way.Add(point(at));
            way.Reverse();
            // Corners of the grid are cut where the next but one point lies on the same line.
            for (int i = way.Count - 2; i >= 1; i--)
                if (Mathf.Abs(Vector2.Dot((way[i] - way[i - 1]).normalized, (way[i + 1] - way[i]).normalized) - 1f) < 0.001f) way.RemoveAt(i);
            way.Add(to);
        }

        // ---------------------------------------------------------------- the signs at the doors

        private void BuildHouseSigns()
        {
            for (int i = 1; i <= VillagePlaceLayout.PlaceCount; i++)
            {
                var place = (VillagePlace)i;
                // A lot has no door and no sign yet.
                if (!_game.Progression.Houses.Built(place)) continue;
                var door = new Vector2(VillagePlaceLayout.DoorXmm(place), VillagePlaceLayout.DoorZmm(place)) / 1000f;
                var outward = new Vector2(VillagePlaceLayout.DoorOutX(place), VillagePlaceLayout.DoorOutZ(place));
                var along = new Vector2(outward.y, -outward.x);
                var host = new GameObject(HouseSignPrefix + place).transform;
                host.SetParent(_villageWork, false);
                host.position = OnGround(door + along * 1.3f + outward * 0.25f);
                // The model's arm points to its -X; turned so, the board hangs out over the way to the door.
                host.rotation = Quaternion.LookRotation(new Vector3(outward.y, 0f, -outward.x));
                GameObject prefab = Resources.Load<GameObject>("ARE/Visuals/" + HouseSignKeys[i - 1]);
                if (prefab != null && PassiveArtVisuals.TryBindPrefab(host, prefab)) continue;
                Color colour = place == VillagePlace.Smithy ? new Color(0.31f, 0.32f, 0.34f) : place == VillagePlace.SickRoom ? new Color(0.34f, 0.45f, 0.29f) : new Color(0.82f, 0.67f, 0.30f);
                Transform post = Shape(PrimitiveType.Cube, "Schild_Pfosten", Vector3.zero, new Vector3(0.1f, 2.6f, 0.1f), new Color(0.30f, 0.21f, 0.14f));
                post.SetParent(host, false); post.localPosition = new Vector3(0f, 1.3f, 0f); post.localRotation = Quaternion.identity;
                Transform board = Shape(PrimitiveType.Cube, "Schild_Brett", Vector3.zero, new Vector3(0.7f, 0.55f, 0.06f), colour);
                board.SetParent(host, false); board.localPosition = new Vector3(-0.5f, 1.98f, 0f); board.localRotation = Quaternion.identity;
            }
        }

        // ---------------------------------------------------------------- the field

        /// <summary>Without the private art: a dark bed and one block per state of the crop.</summary>
        private void BuildFieldBlockout(Transform host, VillageWorkplace workplace)
        {
            Transform soil = Shape(PrimitiveType.Cube, "Platzhalter_" + workplace.Key, Vector3.zero, new Vector3(7.2f, 0.16f, 5.2f), new Color(0.19f, 0.15f, 0.12f));
            soil.SetParent(host, false); soil.localPosition = new Vector3(0f, 0.08f, -2.0f); soil.localRotation = Quaternion.identity;
            string[] names = { "Sown", "Grown", "Ripe" };
            float[] heights = { 0.1f, 0.42f, 0.9f };
            Color[] colours = { new Color(0.38f, 0.52f, 0.26f), new Color(0.38f, 0.52f, 0.26f), new Color(0.82f, 0.67f, 0.30f) };
            for (int i = 0; i < names.Length; i++)
            {
                Transform crop = Shape(PrimitiveType.Cube, CropPrefix + names[i], Vector3.zero, new Vector3(6.6f, heights[i], 4.6f), colours[i]);
                crop.SetParent(host, false); crop.localPosition = new Vector3(0f, 0.16f + heights[i] * 0.5f, -2.0f); crop.localRotation = Quaternion.identity;
            }
        }

        private void FindFieldCrops(Transform host)
        {
            string[] names = { CropPrefix + "Sown", CropPrefix + "Grown", CropPrefix + "Ripe" };
            foreach (Transform part in host.GetComponentsInChildren<Transform>(true))
            {
                int state = Array.IndexOf(names, part.name);
                if (state < 0) continue;
                _fieldCrops[state] = part;
                part.gameObject.SetActive(false);
            }
        }

        /// <summary>What stands on the field: nothing, the young crop, the grown crop, or the ripe grain waiting for the harvest.</summary>
        public static int FieldCropState(VillageEconomyState economy)
        {
            if (economy.Source(VillageSource.RipePlot) > 0) return 3;
            int left = economy.Source(VillageSource.CropTicks);
            if (left <= 0) return 0;
            return left > VillageEconomyRules.CropGrowthTicks / 2 ? 1 : 2;
        }

        private void UpdateFieldCrops(VillageEconomyState economy)
        {
            int state = FieldCropState(economy);
            for (int i = 0; i < _fieldCrops.Length; i++)
                if (_fieldCrops[i] != null && _fieldCrops[i].gameObject.activeSelf != (state == i + 1)) _fieldCrops[i].gameObject.SetActive(state == i + 1);
        }
    }
}
