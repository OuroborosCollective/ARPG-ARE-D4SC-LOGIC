using System;
using AreArpg.Core;
using AshenCrypt.WorldForge.Core;
using NUnit.Framework;
using UnityEngine;

namespace AreArpg.EditMode.Tests
{
    public sealed class JourneyMapEditModeTests
    {
        [Test]
        public void ActualAuthoredMapsBindByHashAndSupportAllCaveFloorBounds()
        {
            var a=Resources.Load<TextAsset>("ARE/JourneyMaps/Wald_v1_731904");var b=Resources.Load<TextAsset>("ARE/JourneyMaps/Wald_v1_731905");
            Assert.IsNotNull(a,"Real native World Forge export is required");Assert.IsNotNull(b);
            var map=ForgedWorldMap.FromBytes(a.bytes);var other=ForgedWorldMap.FromBytes(b.bytes);
            Assert.AreEqual(129,map.NodesX);Assert.AreEqual(129,map.NodesZ);Assert.AreEqual(128000,map.WidthMm);
            Assert.AreNotEqual(map.ContentHash,other.ContentHash);CollectionAssert.AreEqual(a.bytes,map.ToBytes());
            var catalog=new JourneyMapCatalog(map,other);Assert.AreSame(map,catalog.Resolve(map.ContentHash));Assert.IsNull(catalog.Resolve(0));
            for(ulong seed=0;seed<8;seed++)
            {
                var route=new JourneyRouteLayout(map,seed);
                Assert.IsTrue(route.CanStand(route.ReturnX,route.ReturnZ));Assert.IsTrue(route.CanStand(route.CaveX,route.CaveZ));
                Assert.AreEqual(route.LayoutHash,new JourneyRouteLayout(map,seed).LayoutHash);
                Assert.AreEqual(map.HeightMm(route.CaveX,route.CaveZ),route.HeightMm(route.CaveX,route.CaveZ));
                foreach(int count in new[]{3,6})for(int d=1;d<=count;d++)
                {var cave=new JourneyCaveLayout(seed,d,count);Assert.IsTrue(cave.CanStand(cave.ReturnX,cave.ReturnZ));Assert.IsTrue(cave.CanStand(cave.ForwardX,cave.ForwardZ));}
            }
            Assert.Throws<ArgumentOutOfRangeException>(()=>new JourneyCaveLayout(1,1,2));Assert.Throws<ArgumentOutOfRangeException>(()=>new JourneyCaveLayout(1,1,7));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new CryptDungeonLayout(1,4),"Legacy story crypt retains exactly three floors");
        }
    }
}
