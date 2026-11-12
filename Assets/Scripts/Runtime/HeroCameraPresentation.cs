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

        /// <summary>Reads existing actors; neither camera context nor interpolation writes the Core.</summary>
        private void UpdateHeroCameraContext()
        {
            if (_cameraRig == null || _hero == null) return;
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
