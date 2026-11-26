using AreArpg.Core;

namespace AreArpg.UnityClient
{
    internal enum MeleeForm { None, Sword, SwordShield, GreatAxe }

    // Transient player input, like the touch queue. Only ActivateSkill can spend resources or start a strike.
    internal sealed class StandingMeleeChain
    {
        internal const int FollowUpWindow = 2, HoldTicks = 3;
        private ArenaSimulation _world;
        private WeaponItem _weapon, _shield;
        private ulong _sequence, _expires;
        private int _slot = -1, _stage = -1, _buffer = -1;

        internal static MeleeForm Form(WeaponEquipment gear)
        {
            var w = gear.Weapon?.Definition;
            // The starting hand is represented by the visible starter sword.
            if (w == null || (w.Family == WeaponFamily.Sword && !w.TwoHanded))
                return gear.Shield == null ? MeleeForm.Sword : MeleeForm.SwordShield;
            return w.Family == WeaponFamily.Axe && w.TwoHanded ? MeleeForm.GreatAxe : MeleeForm.None;
        }

        internal void Clear() { _stage = _slot = _buffer = -1; }

        internal void Observe(ArenaSimulation w, bool interrupted)
        {
            var gear = w.Progression.Equipment;
            if (_world != w || _weapon != gear.Weapon || _shield != gear.Shield || interrupted ||
                _sequence != w.ActionSequence || w.CurrentTick > _expires || w.GameOver || w.GuardHeld)
                Clear();
            _world = w; _weapon = gear.Weapon; _shield = gear.Shield;
        }

        internal int Accept(ArenaSimulation w, int slot, bool moving)
        {
            var form = Form(w.Progression.Equipment);
            if (!w.CombatV2 || slot > 1 || form == MeleeForm.None) { Clear(); return -1; }
            int count = form == MeleeForm.Sword ? 4 : 3;
            _stage = !moving && _slot == slot ? (_stage + 1) % count : 0;
            _slot = moving ? -1 : slot;
            _sequence = w.ActionSequence;
            _expires = w.CurrentTick + (ulong)(w.CurrentActionTiming.Total + HoldTicks);
            _buffer = -1;
            return _stage;
        }

        internal bool Buffer(ArenaSimulation w, int slot)
        {
            if (slot != _slot || _stage < 0 || w.ActionPhase != CombatPhase.Recovery ||
                w.ActionTick < w.CurrentActionTiming.Total - FollowUpWindow) return false;
            _buffer = slot;
            return true;
        }

        internal int TakeReady(ArenaSimulation w)
        {
            if (_buffer < 0 || w.ActionKind != CombatActionKind.None || w.Cooldowns[_buffer] > 0) return -1;
            int slot = _buffer; _buffer = -1; return slot;
        }
    }
}
