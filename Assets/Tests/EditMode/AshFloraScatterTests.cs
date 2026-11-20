using System;
using System.Collections.Generic;
using System.Reflection;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEngine;

namespace AreArpg.EditMode.Tests
{
    /// <summary>
    /// Rules for the ember plants in the open ash field: where they may grow, how many, and that the field is the
    /// same on every device. The plants themselves are private art; these rules need none of it.
    /// </summary>
    public sealed class AshFloraScatterTests
    {
        static readonly string[] Known =
        {
            AshFloraScatter.Grass, AshFloraScatter.Moss, AshFloraScatter.Shrub,
            AshFloraScatter.Mushrooms, AshFloraScatter.YoungMushrooms, AshFloraScatter.Roses,
        };

        static string Print(List<AshFloraScatter.Spot> spots)
        {
            var lines = new List<string>();
            foreach (AshFloraScatter.Spot spot in spots)
                lines.Add(spot.Key + "@" + spot.Position.ToString("0.000") + "/" + spot.Yaw.ToString("0") + "/" + spot.Height.ToString("0.00") + "x" + spot.Width.ToString("0.00"));
            return string.Join("\n", lines);
        }

        [Test]
        public void TheSameFieldGrowsEveryTime()
        {
            Assert.AreEqual(Print(AshFloraScatter.Spots(false)), Print(AshFloraScatter.Spots(false)));
            Assert.AreEqual(Print(AshFloraScatter.Spots(true)), Print(AshFloraScatter.Spots(true)));
            Assert.AreNotEqual(Print(AshFloraScatter.Spots(false)), Print(AshFloraScatter.Spots(true)), "Road and mill are different fields");
        }

        [Test]
        public void PlantsKeepClearOfTheRoadTheMillAndTheEdgeOfTheGround()
        {
            foreach (AshFloraScatter.Spot spot in AshFloraScatter.Spots(false))
                Assert.GreaterOrEqual(Mathf.Abs(spot.Position.x), AshFloraScatter.RoadClear, "The ash road, its roadside row and the verge stay free");
            foreach (AshFloraScatter.Spot spot in AshFloraScatter.Spots(true))
            {
                // Shelter and beds reach to x = -10.7, the mill house and its wheel span x = 5.4 .. 14.
                Assert.IsTrue(spot.Position.x <= -AshFloraScatter.MillLeftClear || spot.Position.x >= AshFloraScatter.MillRightClear,
                    "Nothing grows through the shelter, the beds or the mill house: " + spot.Position);
            }
            foreach (bool mill in new[] { false, true })
                foreach (AshFloraScatter.Spot spot in AshFloraScatter.Spots(mill))
                {
                    // Half the plant's width is added: the whole plant stays on the 57 m ground, in front of its wall.
                    Assert.LessOrEqual(Mathf.Abs(spot.Position.x) + spot.Width * .5f, 27.5f, spot.Key);
                    Assert.LessOrEqual(Mathf.Abs(spot.Position.z) + spot.Width * .5f, 27.5f, spot.Key);
                    Assert.AreEqual(AshFloraScatter.GroundTop, spot.Position.y, 1e-6f, "Plants stand on the ground");
                    // The passages lie on the road at z = +-24: nothing within four metres of them.
                    Assert.Greater(Vector2.Distance(new Vector2(spot.Position.x, Mathf.Abs(spot.Position.z)), new Vector2(0f, 24f)), 4f);
                }
        }

        [Test]
        public void PlantsKeepTheirDistance()
        {
            foreach (bool mill in new[] { false, true })
            {
                List<AshFloraScatter.Spot> spots = AshFloraScatter.Spots(mill);
                for (int a = 0; a < spots.Count; a++)
                    for (int b = a + 1; b < spots.Count; b++)
                        Assert.GreaterOrEqual(Vector3.Distance(spots[a].Position, spots[b].Position), 2f * AshFloraScatter.Margin - 1e-3f,
                            spots[a].Key + " and " + spots[b].Key + " grow into each other");
            }
        }

        [Test]
        public void TheFieldIsPlantedButNotOvergrown()
        {
            List<AshFloraScatter.Spot> road = AshFloraScatter.Spots(false), mill = AshFloraScatter.Spots(true);
            Assert.That(road.Count, Is.InRange(30, 46), "Ash road: 48 cells, about one in seven bare");
            Assert.That(mill.Count, Is.InRange(18, 31), "Old mill: 32 cells beside shelter and house");
            foreach (List<AshFloraScatter.Spot> spots in new[] { road, mill })
            {
                int glowing = 0, calm = 0;
                foreach (AshFloraScatter.Spot spot in spots)
                {
                    CollectionAssert.Contains(Known, spot.Key);
                    Assert.That(spot.Height, Is.InRange(.25f, 1.05f), spot.Key + ": ground plants, at most hip-high: the hero stays readable");
                    Assert.That(spot.Width, Is.InRange(.8f, 3f), spot.Key);
                    Assert.That(spot.Yaw, Is.InRange(0f, 360f));
                    if (spot.Key == AshFloraScatter.Mushrooms || spot.Key == AshFloraScatter.YoungMushrooms || spot.Key == AshFloraScatter.Roses) glowing++;
                    else calm++;
                }
                Assert.GreaterOrEqual(glowing, 5, "Ember plants are the accents of the ash field");
                Assert.Greater(calm, glowing / 2, "Calm ash plants keep the glow special");
            }
            foreach (int side in new[] { -1, 1 })
            {
                bool ember = false;
                foreach (AshFloraScatter.Spot spot in road)
                    if (Mathf.Sign(spot.Position.x) == side && spot.Key != AshFloraScatter.Grass && spot.Key != AshFloraScatter.Moss && spot.Key != AshFloraScatter.Shrub) ember = true;
                Assert.IsTrue(ember, "Both sides of the ash road have ember plants");
            }
        }

        [Test]
        public void EveryPlantNamesAVisualOfTheAshenWorldSet()
        {
            Type tool = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                tool = assembly.GetType("AreArpg.EditorTools.AshenWorldArt");
                if (tool != null) break;
            }
            Assert.IsNotNull(tool, "AshenWorldArt is missing");
            var keys = new List<string>((string[])tool.GetMethod("Keys", BindingFlags.Public | BindingFlags.Static).Invoke(null, null));
            foreach (string key in Known) CollectionAssert.Contains(keys, key, "The scatter asks for a plant the art tool does not build");
        }
    }
}
