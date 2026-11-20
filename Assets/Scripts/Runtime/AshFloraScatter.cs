using System.Collections.Generic;
using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>
    /// Where ember plants grow in the open ash field beside the ash road and around the old mill (owner direction
    /// 2026-11-20: ash snow, glowing threads and ember plants). Presentation only: the Core never reads any of
    /// this, the plants have no collision, and the same field is planted the same way on every device.
    /// </summary>
    public static class AshFloraScatter
    {
        public struct Spot
        {
            /// <summary>Key of the plant visual (private art); without the visual nothing is placed.</summary>
            public string Key;
            public Vector3 Position;
            public float Yaw;
            /// <summary>The visual is scaled to fit this height and width in metres.</summary>
            public float Height, Width;
        }

        /// <summary>The field is laid out in cells of this size; each cell holds at most one plant.</summary>
        public const float Cell = 6f;
        /// <summary>Plants stay inside the walked ground (57 m) and inside its boundary wall.</summary>
        public const float FieldEdge = 25.5f, FieldLength = 24f;
        /// <summary>Top of the outdoor ground: the plants stand on it.</summary>
        public const float GroundTop = -.1f;
        /// <summary>Beside the ash road the field starts behind the road, its roadside row and the verge.</summary>
        public const float RoadClear = 8.5f;
        /// <summary>At the old mill the shelter and its beds (left) and the mill house (right) stay clear.</summary>
        public const float MillLeftClear = 13.5f, MillRightClear = 15.5f;
        /// <summary>Margin inside every cell, so two plants never grow into each other.</summary>
        public const float Margin = 1f;
        public const string Grass = "AshFloraGrass", Moss = "AshFloraMoss", Shrub = "AshFloraShrub",
            Mushrooms = "AshFloraMushrooms", YoungMushrooms = "AshFloraMushroomsSmall", Roses = "AshFloraRoses";

        /// <summary>
        /// The plants of one region. Calm ash grass is the most common; glowing mushrooms and fire roses are the
        /// accents; about one cell in seven stays bare ash.
        /// </summary>
        public static List<Spot> Spots(bool mill)
        {
            var spots = new List<Spot>();
            int rows = Mathf.RoundToInt(2f * FieldLength / Cell);
            for (int side = -1; side <= 1; side += 2)
            {
                float inner = !mill ? RoadClear : side < 0 ? MillLeftClear : MillRightClear;
                int column = 0;
                for (float from = inner; from < FieldEdge - 2f * Margin; from += Cell, column++)
                {
                    float wide = Mathf.Min(Cell, FieldEdge - from);
                    for (int row = 0; row < rows; row++)
                    {
                        // Integer mix, no engine random: the field looks the same after every reload.
                        uint hash = CryptDungeonDressing.Hash(side * (column + 1), row, mill ? 41 : 31, 20261120UL);
                        Spot spot;
                        if (!Pick(hash % 100u, out spot)) continue;
                        float across = ((hash >> 8) % 1000u) / 999f, along = ((hash >> 18) % 1000u) / 999f;
                        spot.Position = new Vector3(side * (from + Margin + across * (wide - 2f * Margin)), GroundTop,
                            -FieldLength + row * Cell + Margin + along * (Cell - 2f * Margin));
                        spot.Yaw = (hash >> 4) % 360u;
                        spots.Add(spot);
                    }
                }
            }
            return spots;
        }

        static bool Pick(uint roll, out Spot spot)
        {
            spot = new Spot();
            if (roll < 14u) return false;
            if (roll < 42u) { spot.Key = Grass; spot.Height = .5f; spot.Width = 1.75f; }
            else if (roll < 56u) { spot.Key = YoungMushrooms; spot.Height = .5f; spot.Width = 1.7f; }
            else if (roll < 67u) { spot.Key = Mushrooms; spot.Height = .9f; spot.Width = 2.8f; }   // the old one reaches the hero's thigh
            else if (roll < 76u) { spot.Key = Roses; spot.Height = 1f; spot.Width = 1.9f; }
            else if (roll < 95u) { spot.Key = Shrub; spot.Height = .85f; spot.Width = 1.3f; }
            else { spot.Key = Moss; spot.Height = .3f; spot.Width = .9f; }
            return true;
        }
    }
}
