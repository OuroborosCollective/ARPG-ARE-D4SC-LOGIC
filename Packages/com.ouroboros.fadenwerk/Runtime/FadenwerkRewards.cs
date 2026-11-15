using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Fadenwerk
{
    /// <summary>
    /// Reproduzierbarer Zufall: gleicher Seed, gleiche Zahlenfolge – auf jedem Gerät.
    /// Algorithmus "fadenwerk-rng/1": SHA-256 des Seeds, erste 4 Bytes (Little Endian) als Startwert,
    /// danach mulberry32. Die Browser-Oberfläche benutzt dieselbe Rechnung für den Probe-Wurf.
    /// </summary>
    public sealed class FadenwerkRng
    {
        uint _s;

        public FadenwerkRng(string seed)
        {
            byte[] h;
            using (var sha = SHA256.Create())
                h = sha.ComputeHash(Encoding.UTF8.GetBytes(seed ?? ""));
            unchecked
            {
                _s = (uint)h[0] | ((uint)h[1] << 8) | ((uint)h[2] << 16) | ((uint)h[3] << 24);
            }
            if (_s == 0u) _s = 0x9e3779b9u;
        }

        public uint NextU32()
        {
            unchecked
            {
                _s += 0x6D2B79F5u;
                uint t = _s;
                t = (t ^ (t >> 15)) * (t | 1u);
                t ^= t + (t ^ (t >> 7)) * (t | 61u);
                return t ^ (t >> 14);
            }
        }

        /// <summary>Ganze Zahl von min bis max (beide eingeschlossen).</summary>
        public int Range(int min, int max)
        {
            if (max < min) max = min;
            uint span = (uint)(max - min) + 1u;
            return min + (int)(NextU32() % span);
        }

        /// <summary>Index nach Gewicht. Gewichte kleiner oder gleich 0 werden nie gezogen. -1, wenn nichts ziehbar ist.</summary>
        public int PickWeighted(IList<int> weights)
        {
            int total = 0;
            for (int i = 0; i < weights.Count; i++)
                if (weights[i] > 0) total += weights[i];
            if (total <= 0) return -1;
            int roll = Range(1, total);
            int last = -1;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] <= 0) continue;
                last = i;
                roll -= weights[i];
                if (roll <= 0) return i;
            }
            return last;
        }
    }

    [Serializable]
    public class RewardItem
    {
        public string itemId = "";
        public string displayName = "";
        public int quantity = 1;
        /// <summary>Das verknüpfte Unity-Asset (Prefab, ScriptableObject …) oder null.</summary>
        public UnityEngine.Object asset;
    }

    [Serializable]
    public class RewardResult
    {
        public string kitId = "";
        public string seed = "";
        public int gold;
        public int xp;
        public List<RewardItem> items = new List<RewardItem>();
    }

    public static class FadenwerkRewards
    {
        /// <summary>
        /// Würfelt ein Belohnungs-Kit aus. Reihenfolge der Züge (Vertrag "kit/1"):
        /// 1. Gold, 2. XP, 3. Menge jedes garantierten Eintrags in Listenreihenfolge,
        /// 4. je Wurf: gewichtete Auswahl unter den übrigen Einträgen, dann dessen Menge.
        /// </summary>
        public static RewardResult Roll(FadenwerkDatabase db, string kitId, string seed)
        {
            var result = new RewardResult();
            result.kitId = kitId ?? "";
            result.seed = seed ?? "";
            if (db == null) return result;
            RewardKitDef kit = db.FindKit(kitId);
            if (kit == null) return result;

            var rng = new FadenwerkRng(seed);
            result.gold = rng.Range(Math.Max(0, kit.goldMin), Math.Max(Math.Max(0, kit.goldMin), kit.goldMax));
            result.xp = rng.Range(Math.Max(0, kit.xpMin), Math.Max(Math.Max(0, kit.xpMin), kit.xpMax));

            var entries = kit.entries ?? new List<KitEntry>();
            for (int i = 0; i < entries.Count; i++)
            {
                KitEntry e = entries[i];
                if (e == null || !e.guaranteed) continue;
                Add(db, result, e, RollQuantity(rng, e));
            }

            var pool = new List<KitEntry>();
            var weights = new List<int>();
            for (int i = 0; i < entries.Count; i++)
            {
                KitEntry e = entries[i];
                if (e == null || e.guaranteed || e.weight <= 0) continue;
                pool.Add(e);
                weights.Add(e.weight);
            }
            int rolls = Math.Max(0, Math.Min(64, kit.rolls));
            for (int r = 0; r < rolls && pool.Count > 0; r++)
            {
                int idx = rng.PickWeighted(weights);
                if (idx < 0) break;
                Add(db, result, pool[idx], RollQuantity(rng, pool[idx]));
            }
            return result;
        }

        static int RollQuantity(FadenwerkRng rng, KitEntry e)
        {
            int min = Math.Max(0, e.minQuantity);
            int max = Math.Max(min, e.maxQuantity);
            return rng.Range(min, max);
        }

        static void Add(FadenwerkDatabase db, RewardResult result, KitEntry e, int quantity)
        {
            if (quantity <= 0 || string.IsNullOrEmpty(e.itemId)) return;
            for (int i = 0; i < result.items.Count; i++)
            {
                if (result.items[i].itemId == e.itemId) { result.items[i].quantity += quantity; return; }
            }
            ItemEntry item = db.FindItem(e.itemId);
            var ri = new RewardItem();
            ri.itemId = e.itemId;
            ri.displayName = item != null && !string.IsNullOrEmpty(item.displayName) ? item.displayName : e.itemId;
            ri.quantity = quantity;
            ri.asset = item != null ? item.asset : null;
            result.items.Add(ri);
        }
    }
}
