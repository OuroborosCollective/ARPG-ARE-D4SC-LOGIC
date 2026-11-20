using System;
using System.Collections.Generic;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;

namespace AreArpg.EditMode.Tests
{
    /// <summary>
    /// Rules that dress the procedural crypt (owner task 2026-11-19/20: rework the dungeon environment). They are
    /// presentation only and must be stable: the same saved floor looks the same after every reload.
    /// </summary>
    public sealed class CryptDungeonDressingTests
    {
        static readonly int[] Sides = { CryptDungeonDressing.West, CryptDungeonDressing.East, CryptDungeonDressing.South, CryptDungeonDressing.North };

        [Test]
        public void EveryWallFacesTheTileItEncloses()
        {
            foreach (int side in Sides)
            {
                Vector3 facing = Quaternion.Euler(0f, CryptDungeonDressing.WallYaw(side), 0f) * Vector3.forward;
                Assert.Greater(Vector3.Dot(facing, CryptDungeonDressing.Inward(side)), .999f, "side " + side + ": the decorated face must look into the room");
                Vector3 centre = CryptDungeonDressing.WallCentre(2, -3, side);
                Vector3 tile = new Vector3(2 * CryptDungeonDressing.TileMetres, 0f, -3 * CryptDungeonDressing.TileMetres);
                Assert.Less((centre + CryptDungeonDressing.Inward(side) * (CryptDungeonDressing.TileMetres * .5f) - tile).magnitude, .0001f, "side " + side + ": the wall stands on the tile border");
            }
        }

        [Test]
        public void APillarStandsExactlyWhereTheWallLineTurns()
        {
            for (int mask = 0; mask < 16; mask++)
            {
                bool southWest = (mask & 1) != 0, southEast = (mask & 2) != 0, northWest = (mask & 4) != 0, northEast = (mask & 8) != 0;
                int floors = (southWest ? 1 : 0) + (southEast ? 1 : 0) + (northWest ? 1 : 0) + (northEast ? 1 : 0);
                bool diagonal = floors == 2 && southWest == northEast;
                bool expected = floors == 1 || floors == 3 || diagonal;
                Assert.AreEqual(expected, CryptDungeonDressing.CornerPillar(southWest, southEast, northWest, northEast), "floors around the corner: mask " + mask);
            }
            Assert.IsFalse(CryptDungeonDressing.CornerPillar(true, true, false, false), "A straight wall needs no pillar");
            Assert.IsFalse(CryptDungeonDressing.CornerPillar(true, true, true, true), "Open floor needs no pillar");
        }

        [Test]
        public void TheSameSeedDressesAFloorTheSameWayAndAnotherSeedDiffers()
        {
            const ulong first = 0x1234ABCDul, second = 0x1234ABCEul;
            int walls = 0, different = 0;
            var seen = new int[4];
            for (int z = -6; z <= 6; z++)
                for (int x = -6; x <= 6; x++)
                    foreach (int side in Sides)
                    {
                        uint hash = CryptDungeonDressing.Hash(x, z, side, first);
                        Assert.AreEqual(hash, CryptDungeonDressing.Hash(x, z, side, first), "The dressing of a saved floor never changes");
                        int variant = CryptDungeonDressing.WallVariant(hash, 3);
                        seen[variant]++; walls++;
                        if (variant != CryptDungeonDressing.WallVariant(CryptDungeonDressing.Hash(x, z, side, second), 3)) different++;
                    }
            Assert.Greater(different, walls / 5, "Another floor seed gives another floor");
            Assert.That(seen[0] / (float)walls, Is.InRange(.45f, .70f), "Most walls stay plain so that the decorated ones are landmarks");
            for (int variant = 1; variant <= 3; variant++) Assert.Greater(seen[variant], 0, "Variant " + variant + " appears");
            Assert.Less(seen[3], seen[1], "The skull relief is rarer than the arch niche");
        }

        [Test]
        public void MissingVariantsFallBackWithoutLeavingTheSet()
        {
            for (int decorated = 0; decorated <= 3; decorated++)
                for (uint hash = 0; hash < 400; hash++)
                    Assert.That(CryptDungeonDressing.WallVariant(hash * 2654435761u, decorated), Is.InRange(0, decorated), "available decorated walls: " + decorated);
        }

        [Test]
        public void PropsLieAtTheFootOfTheirWallAndLeaveTheMiddleOfTheTileFree()
        {
            float half = CryptDungeonDressing.TileMetres * .5f;
            int placed = 0, rolls = 0;
            for (uint i = 0; i < 2000; i++)
            {
                uint hash = i * 2246822519u + 17u;
                foreach (int side in Sides)
                {
                    Vector3 offset = CryptDungeonDressing.PropOffset(side, hash);
                    Vector3 inward = CryptDungeonDressing.Inward(side);
                    Assert.AreEqual(-(half - CryptDungeonDressing.PropInset), Vector3.Dot(offset, inward), .0001f, "A prop keeps its distance from the wall line");
                    float slide = (offset - inward * Vector3.Dot(offset, inward)).magnitude;
                    Assert.LessOrEqual(slide, CryptDungeonDressing.PropSlide + .0001f, "A prop stays clear of the corner pillars");
                    Assert.Less(Mathf.Max(Mathf.Abs(offset.x), Mathf.Abs(offset.z)), half, "A prop stays inside its tile");
                    Assert.GreaterOrEqual(offset.magnitude, 1f, "The walked middle of a tile stays free");
                    Assert.AreEqual(0f, offset.y, "Props stand on the floor");
                    float turn = Mathf.DeltaAngle(CryptDungeonDressing.WallYaw(side), CryptDungeonDressing.PropYaw(side, hash));
                    Assert.LessOrEqual(Mathf.Abs(turn), 30.001f, "A prop faces the room");
                }
                int prop = CryptDungeonDressing.PropIndex(hash, 8);
                rolls++;
                if (prop >= 0) { placed++; Assert.Less(prop, 8); }
                Assert.AreEqual(-1, CryptDungeonDressing.PropIndex(hash, 0), "No prop art, no prop");
            }
            Assert.That(placed / (float)rolls, Is.InRange(.2f, .4f), "About three walls in ten carry a prop");
        }

        [Test]
        public void AShrineStandsAtTheNearestWallOfItsRoom()
        {
            // A room of 6 x 3 tiles: x from -2 to 3, z from -1 to 1.
            Func<int, int, bool> room = (x, z) => x >= -2 && x <= 3 && z >= -1 && z <= 1;
            int tileX, tileZ, side;
            Assert.IsTrue(CryptDungeonDressing.ShrineSpot(room, 0, 0, out tileX, out tileZ, out side));
            Assert.AreEqual(CryptDungeonDressing.South, side, "South and north are one step away; the first nearest wall wins");
            Assert.AreEqual(0, tileX); Assert.AreEqual(-1, tileZ);
            Assert.IsTrue(room(tileX, tileZ), "The shrine stands on a floor tile");
            Vector3 outward = -CryptDungeonDressing.Inward(side);
            Assert.IsFalse(room(tileX + Mathf.RoundToInt(outward.x), tileZ + Mathf.RoundToInt(outward.z)), "There is a wall behind the shrine");

            Assert.IsTrue(CryptDungeonDressing.ShrineSpot(room, -2, 1, out tileX, out tileZ, out side));
            Assert.AreEqual(CryptDungeonDressing.West, side, "A room centre beside the west wall uses that wall");
            Assert.AreEqual(-2, tileX); Assert.AreEqual(1, tileZ);

            Assert.IsFalse(CryptDungeonDressing.ShrineSpot(room, 9, 9, out tileX, out tileZ, out side), "No floor, no shrine");
            Assert.IsFalse(CryptDungeonDressing.ShrineSpot((x, z) => true, 0, 0, out tileX, out tileZ, out side), "Endless floor has no wall to stand at");
        }
    }
}
