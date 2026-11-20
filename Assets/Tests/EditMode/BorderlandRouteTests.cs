using System.Collections.Generic;
using AreArpg.Core;
using AshenCrypt.WorldForge.Core;
using NUnit.Framework;
using UnityEngine;

namespace AreArpg.EditMode.Tests
{
    public sealed class BorderlandRouteTests
    {
        [Test]
        public void BakedRouteSpans256MetresAndConnectsEveryStoryAnchor()
        {
            var asset = Resources.Load<TextAsset>("ARE/JourneyMaps/Wald_256_731906");
            Assert.IsNotNull(asset, "WorldForge must publish the playable 256-metre route");
            var wide = ForgedWorldMap.FromBytes(asset.bytes);
            Assert.AreEqual(256000, wide.WidthMm);
            Assert.AreEqual(wide.WidthMm, wide.DepthMm);
            var old = ForgedWorldMap.FromBytes(Resources.Load<TextAsset>("ARE/JourneyMaps/Wald_v1_731904").bytes);
            var catalog = new JourneyMapCatalog(wide, old);
            Assert.AreSame(old, catalog.Resolve(old.ContentHash));
            var route = new JourneyRouteLayout(catalog.Primary, 71);
            var visited = new HashSet<int>();
            var frontier = new Queue<int>();
            int Index(long x, long z) => (int)((x-route.OriginMm)/route.CellMm) + route.Size*(int)((z-route.OriginMm)/route.CellMm);
            int start = Index(route.ReturnX, route.ReturnZ);
            visited.Add(start); frontier.Enqueue(start);
            while (frontier.Count > 0)
            {
                int n = frontier.Dequeue(), x = n % route.Size, z = n / route.Size;
                foreach (int next in new[] { n-1, n+1, n-route.Size, n+route.Size })
                {
                    if (next < 0 || next >= route.Size*route.Size || visited.Contains(next)) continue;
                    int nx = next % route.Size, nz = next / route.Size;
                    if (System.Math.Abs(x-nx)+System.Math.Abs(z-nz) != 1 || !route.IsFloor(nx,nz)) continue;
                    bool pass = true;
                    for (int i=1; i<=8 && pass; i++)
                        pass = route.CanStand(route.Centre(x)+(nx-x)*route.CellMm*i/8, route.Centre(z)+(nz-z)*route.CellMm*i/8);
                    if (pass && route.HasSight(route.Centre(x), route.Centre(z), route.Centre(nx), route.Centre(nz)))
                    { visited.Add(next); frontier.Enqueue(next); }
                }
            }
            foreach (string id in new[] { "arrival", "onward", "cave", "treasure", "rest" })
            {
                var spot = wide.FindPoi(id);
                Assert.IsTrue(visited.Contains(Index(spot.Xmm,spot.Zmm)), id);
            }
            Assert.Greater(route.ForwardZ-route.ReturnZ, 200000);
        }
    }
}
