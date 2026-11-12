using System.Collections.Generic;
using AreArpg.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    public sealed partial class ArenaRuntime
    {
        private bool CameraDangerNear(long x, long z)
        {
            long dx = x - _game.PlayerXmm, dz = z - _game.PlayerZmm;
            return dx * dx + dz * dz <= 14000L * 14000L;
        }

        private readonly List<Renderer> _occluderScan = new List<Renderer>();
        private readonly List<Bounds> _occluderBoxes = new List<Bounds>();
        private float _occluderRefresh;
        private Vector2 _orbitMouse;
        private bool _orbitMouseHeld;

        /// <summary>
        /// "Up" on the stick or keyboard means away from the camera. The rotated direction is an
        /// ordinary move input; the Core still decides every step.
        /// </summary>
        private void CameraRelativeMove(ref long x, ref long z)
        {
            if (_cameraRig == null || (x == 0 && z == 0)) return;
            float yaw = _cameraRig.Yaw * Mathf.Deg2Rad, sin = Mathf.Sin(yaw), cos = Mathf.Cos(yaw);
            float worldX = x * cos + z * sin, worldZ = z * cos - x * sin;
            // The Core clamps each axis before normalising; keep the turned direction inside that range.
            float scale = Mathf.Min(1f, 10000f / Mathf.Max(Mathf.Abs(worldX), Mathf.Abs(worldZ), 1f));
            x = (long)Mathf.Round(worldX * scale); z = (long)Mathf.Round(worldZ * scale);
        }

        /// <summary>Desktop: hold the right mouse button on free world space and drag to turn the view.</summary>
        private void PollMouseOrbit(ArenaHudLayout layout)
        {
            if (Application.isMobilePlatform || !Input.GetMouseButton(1)) { _orbitMouseHeld = false; return; }
            Vector2 point = layout.ToGuiTouch(Input.mousePosition);
            if (_orbitMouseHeld) _cameraRig.Orbit(point - _orbitMouse);
            _orbitMouse = point; _orbitMouseHeld = true;
        }

        /// <summary>
        /// Tell the camera which tall scenery could hide the hero. Renderer boxes only: no physics,
        /// no Core data. Floors, low props and region-sized shells are left out.
        /// </summary>
        private void RefreshCameraOccluders()
        {
            if (_cameraRig == null) return;
            _occluderBoxes.Clear();
            foreach (Transform part in _chapterScenery)
            {
                if (part == null || !part.gameObject.activeInHierarchy) continue;
                part.GetComponentsInChildren(false, _occluderScan);
                foreach (Renderer renderer in _occluderScan)
                {
                    if (!renderer.enabled || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
                    Bounds box = renderer.bounds;
                    if (box.max.y < 1.5f || box.size.y < 1f) continue;
                    if (box.size.x > 30f || box.size.z > 30f) continue;
                    // Most tall props are round (pillars, trunks, towers); trim the box corners.
                    box.extents = new Vector3(box.extents.x * .8f, box.extents.y, box.extents.z * .8f);
                    _occluderBoxes.Add(box);
                }
            }
            _occluderScan.Clear();
            _cameraRig.SetOccluders(_occluderBoxes.ToArray());
        }

        /// <summary>Reads existing actors; neither camera context nor interpolation writes the Core.</summary>
        private void UpdateHeroCameraContext()
        {
            if (_cameraRig == null || _hero == null) return;
            _occluderRefresh -= Time.unscaledDeltaTime;
            if (_occluderRefresh <= 0f) { _occluderRefresh = 1.5f; RefreshCameraOccluders(); }
            bool moving = _game.PlayerXmm != _game.PrevPlayerXmm || _game.PlayerZmm != _game.PrevPlayerZmm;
            bool danger = _game.GameOver;
            foreach (var enemy in _game.Monsters)
                if (!enemy.IsDead && CameraDangerNear(enemy.Xmm, enemy.Zmm)) { danger = true; break; }
            var road = _game.Progression.Road;
            if (_game.Progression.EntryAct == 3 && road.Started && !road.Bear.IsDead && CameraDangerNear(road.Bear.Xmm, road.Bear.Zmm)) danger = true;
            foreach (var npc in _game.Npcs)
                if (npc.Memory.Health > 0 && npc.Memory.Disposition(npc.Personality) == NpcDisposition.Hostile && CameraDangerNear(npc.Xmm, npc.Zmm)) danger = true;
            bool speaking = _atmosphere != null && _atmosphere.Subtitle.Length > 0;
            Vector3 speaker = _hero.position;
            if (speaking)
            {
                int id = _atmosphere.SpeakerId;
                if (id == 3) speaker = new Vector3(HunterEncounter.HunterX / 1000f, 1, HunterEncounter.HunterZ / 1000f);
                else if (_npcVisuals.TryGetValue(id, out var actor) && actor != null) speaker = actor.position;
            }
            _cameraRig.SetContext(moving, danger, speaking, speaker);
        }
    }
}
