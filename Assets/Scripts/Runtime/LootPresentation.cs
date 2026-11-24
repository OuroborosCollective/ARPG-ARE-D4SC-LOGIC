using System.Collections.Generic;
using AreArpg.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// Loot revision 2 in the scene (issue #48, owner decisions of 2026-11-15 and 2026-11-17): supply bags lie where
    /// their enemy fell and glint, weapons on the ground glint in the colour of their tier, and a legendary weapon wears
    /// an aura on the ground and in the hand (weapon or shield). The Core decides what lies where; nothing here changes it. All glints
    /// share one particle system with a fixed limit, and with reduced effects only legendary weapons keep a faint one.
    /// </summary>
    public sealed partial class ArenaRuntime
    {
        public const string SupplyVisualPrefix = "Supply_";
        public const int LootGlintLimit = 96;
        private const float LootGlintInterval = 0.3f;
        // A bag lies a step beside a weapon that fell on the same spot, well inside the pickup reach.
        private static readonly Vector3 SupplyOffset = new Vector3(.45f, 0f, -.3f);
        private static readonly Color SupplyGlint = new Color(1f, .82f, .42f, .9f);

        private readonly Dictionary<SupplyBag, Transform> _supplyVisuals = new Dictionary<SupplyBag, Transform>();
        private readonly List<SupplyBag> _supplyGone = new List<SupplyBag>();
        private ParticleSystem _lootGlint;
        private float _nextLootGlint;

        /// <summary>Show every bag the Core has on the ground, drop the ones that were taken, then let the loot glint.</summary>
        private void UpdateLootPresentation()
        {
            foreach (SupplyDrop drop in _game.GroundSupplies)
            {
                if (_supplyVisuals.TryGetValue(drop.Bag, out Transform shown) && shown != null) continue;
                float x = drop.Xmm / 1000f + SupplyOffset.x, z = drop.Zmm / 1000f + SupplyOffset.z, floor = GroundHeight(x, z);
                Transform visual = Shape(PrimitiveType.Sphere, SupplyVisualPrefix + drop.Id, new Vector3(x, floor + .2f, z),
                    Vector3.one * .38f, new Color(.62f, .47f, .26f));
                if (PassiveArtVisuals.TryBindGroundedPrefab(visual, BorderlandArt.LoadVisual("SupplySack"), floor))
                    visual.GetChild(visual.childCount - 1).name = "GroundedBag";
                _supplyVisuals[drop.Bag] = visual;
            }
            _supplyGone.Clear();
            foreach (KeyValuePair<SupplyBag, Transform> entry in _supplyVisuals)
            {
                bool lies = false;
                foreach (SupplyDrop drop in _game.GroundSupplies) if (ReferenceEquals(drop.Bag, entry.Key)) { lies = true; break; }
                if (!lies) _supplyGone.Add(entry.Key);
            }
            foreach (SupplyBag bag in _supplyGone)
            {
                if (_supplyVisuals[bag] != null) Destroy(_supplyVisuals[bag].gameObject);
                _supplyVisuals.Remove(bag);
            }
            GlintLoot();
        }

        private void ClearSupplyVisuals()
        {
            foreach (Transform visual in _supplyVisuals.Values) if (visual != null) Destroy(visual.gameObject);
            _supplyVisuals.Clear();
        }

        /// <summary>A few sparks every 0.3 s: gold over bags, the tier's colour over weapons, more for a legendary one.</summary>
        private void GlintLoot()
        {
            if (_atmosphere == null || Time.time < _nextLootGlint) return;
            _nextLootGlint = Time.time + LootGlintInterval;
            bool reduced = _atmosphere.ReducedEffects;
            if (_lootGlint == null) _lootGlint = _atmosphere.CreateEffect("Loot glint", LootGlintLimit, .1f, Color.white, 1.1f);
            if (!reduced)
                foreach (Transform bag in _supplyVisuals.Values)
                    if (bag != null) Glint(bag.position + Vector3.up * .25f, SupplyGlint, .09f, 1);
            foreach (LootState item in _game.GroundLoot)
            {
                if (item.Item == null || !_lootVisuals.TryGetValue(item.Id, out Transform visual) || visual == null) continue;
                bool legendary = item.Item.Rarity == ItemRarity.Legendary;
                if (reduced && !legendary) continue;
                Glint(visual.position + Vector3.up * .2f, AshenUiTheme.RarityColor(item.Item.Rarity), legendary ? .16f : .09f, legendary && !reduced ? 3 : 1);
            }
            // A legendary piece glints in the hand that holds it: the weapon, and a shield in the other hand.
            WeaponItem held = _game.Progression.Equipment.Weapon, shield = _game.Progression.Equipment.Shield;
            if (held != null && held.Rarity == ItemRarity.Legendary && _equippedVisual != null)
                Glint(_equippedVisual.position, AshenUiTheme.RarityColor(ItemRarity.Legendary), .12f, reduced ? 1 : 2);
            if (shield != null && shield.Rarity == ItemRarity.Legendary && _shieldVisual != null)
                Glint(_shieldVisual.position, AshenUiTheme.RarityColor(ItemRarity.Legendary), .12f, reduced ? 1 : 2);
        }

        private void Glint(Vector3 at, Color color, float size, int count)
        {
            var spark = new ParticleSystem.EmitParams { startColor = color, startSize = size };
            for (int i = 0; i < count; i++)
            {
                spark.position = at + new Vector3(Random.Range(-.28f, .28f), Random.Range(0f, .35f), Random.Range(-.28f, .28f));
                spark.velocity = new Vector3(0f, Random.Range(.25f, .6f), 0f);
                _lootGlint.Emit(spark, 1);
            }
        }

        /// <summary>What the player reads after taking something from the ground.</summary>
        private string PickupMessage()
        {
            SupplyBag bag = _game.LastSupply;
            if (bag != null)
                return "Vorratsbeutel aufgenommen: " + CoinDenominations.Format(bag.Coins) + (bag.Rations > 0 ? " und eine Ration." : ".");
            LootItem item = _game.LastLoot;
            if (item != null && item.Rarity == ItemRarity.Legendary) return "Legendäre Waffe aufgenommen! Du kannst sie im Inventar ausrüsten.";
            return "Beute aufgenommen" + (item != null ? " (" + RarityName(item.Rarity) + ")" : "") + ". Neue Waffen kannst du im Inventar vergleichen und ausrüsten.";
        }
    }
}
