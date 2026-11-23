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
                if (impact.Kills > 0 || impact.WeaponLevelUps > 0) SaveCharacter();
            }
            if (_game.Progression.Crafting.PotionCount(PotionType.Heal) < potionsBefore)
            {
                _message = "Heiltrank getrunken: +" + Mathf.Max(0, _game.PlayerHealth - healthBefore) + " Lebenspunkte.";
                PresentFlask();
                SaveCharacter(); // the flask is gone for good; the save must know
            }
            if (_game.GuardRaised && !guardBefore) PresentGuardRaised();
            if (_game.BlockedAttacks > blockedBefore) PresentBlock();
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
