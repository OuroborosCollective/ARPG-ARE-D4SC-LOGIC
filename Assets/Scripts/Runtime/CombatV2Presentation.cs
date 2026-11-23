using AreArpg.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// Combat v2 projection (issue #127): reads what one Core tick decided — landed strikes, a raised or hit guard, a
    /// dodge, a flask that took effect — and shows it. Nothing here changes the Core; it only follows its results.
    /// </summary>
    public sealed partial class ArenaRuntime
    {
        private void PresentCombatTick(int healthBefore, int potionsBefore, int blockedBefore, bool guardBefore)
        {
            foreach (var impact in _game.Impacts)
            {
                _atmosphere.Impact(impact, _game);
                PresentImpact(impact, impact.Slot);
                _message = impact.Message;
                // The practice rank grows on the impact tick; a rank gained without a kill is saved too.
                bool learned = impact.Slot >= 0 && impact.Slot < _practiceBefore.Length &&
                    _game.Progression.PracticeLevel(impact.Slot) > _practiceBefore[impact.Slot];
                if (learned) _message += " / Durch Uebung gewachsen: Rang " + _game.Progression.PracticeLevel(impact.Slot);
                if (impact.Kills > 0 || impact.WeaponLevelUps > 0 || learned) SaveCharacter();
            }
            if (_game.Progression.Crafting.PotionCount(PotionType.Heal) < potionsBefore)
            {
                _message = "Heiltrank getrunken: +" + Mathf.Max(0, _game.PlayerHealth - healthBefore) + " Lebenspunkte.";
                PresentFlask();
                SaveCharacter(); // the flask is gone for good; the save must know
            }
            if (_game.GuardRaised && !guardBefore) PresentGuardRaised();
            if (_game.BlockedAttacks > blockedBefore) PresentBlock();
            foreach (var heal in _game.Heals)
            {
                _message = heal.Message;
                if (heal.Amount > 0) PresentHeal(heal);
                // Practice and closed wounds are kept; a landed heal is saved at once like a drunk flask.
                if (heal.Amount > 0) SaveCharacter();
            }
        }

        /// <summary>Wundschluss starts: the hero gathers the spell, a faint light marks whom it is for.</summary>
        private void PresentCastStart(HealResult cast)
        {
            if (!_entryProfile || _hero == null) return;
            EnsureCombatEffects();
            if (_guardShimmer != null)
            {
                _guardShimmer.transform.position = _hero.position + Vector3.up * 1.4f;
                _guardShimmer.Emit(_atmosphere != null && _atmosphere.ReducedEffects ? 4 : 12);
                if (cast.Target != HealTargetKind.Self)
                {
                    _guardShimmer.transform.position = new Vector3(cast.TargetXmm / 1000f, .9f, cast.TargetZmm / 1000f);
                    _guardShimmer.Emit(_atmosphere != null && _atmosphere.ReducedEffects ? 3 : 8);
                }
            }
        }

        /// <summary>Wundschluss lands: light gathers on the healed body.</summary>
        private void PresentHeal(HealResult heal)
        {
            if (!_entryProfile) return;
            EnsureCombatEffects();
            if (_guardShimmer == null) return;
            Vector3 at = heal.Target == HealTargetKind.Self && _hero != null ? _hero.position : new Vector3(heal.TargetXmm / 1000f, 0f, heal.TargetZmm / 1000f);
            _guardShimmer.transform.position = at + Vector3.up * 1.1f;
            _guardShimmer.Emit(_atmosphere != null && _atmosphere.ReducedEffects ? 10 : 30);
        }

        private void PresentGuardRaised()
        {
            if (!_entryProfile || _hero == null) return;
            _heroActions.Enqueue("Guard");
            PumpHeroActions();
        }

        private void PresentBlock()
        {
            if (!_entryProfile || _hero == null) return;
            EnsureCombatEffects();
            if (_guardShimmer != null)
            {
                _guardShimmer.transform.position = _hero.position + Vector3.up * .9f;
                _guardShimmer.Emit(_atmosphere != null && _atmosphere.ReducedEffects ? 8 : 24);
            }
            if (_cameraRig != null) _cameraRig.Shake(.04f, .12f);
        }

        private void PresentDodge()
        {
            if (!_entryProfile || _hero == null) return;
            EnsureCombatEffects();
            if (_shockwave != null)
            {
                var shape = _shockwave.shape; shape.radius = .6f;
                _shockwave.transform.position = _hero.position + Vector3.up * .1f;
                _shockwave.Emit(_atmosphere != null && _atmosphere.ReducedEffects ? 4 : 12);
            }
        }

        private void PresentFlask()
        {
            if (!_entryProfile || _hero == null) return;
            EnsureCombatEffects();
            if (_guardShimmer != null)
            {
                _guardShimmer.transform.position = _hero.position + Vector3.up * 1.1f;
                _guardShimmer.Emit(_atmosphere != null && _atmosphere.ReducedEffects ? 10 : 30);
            }
        }
    }
}
