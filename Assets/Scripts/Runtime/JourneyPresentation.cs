using System;
using System.Collections.Generic;
using AreArpg.Core;
using AshenCrypt.WorldForge.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    public sealed partial class ArenaRuntime
    {
        private JourneyMapCatalog _journeyMaps;
        private Transform _journeyGate;
        private readonly Transform[] _journeyLids=new Transform[2];
        private static JourneyMapCatalog LoadJourneyMaps()
        {
            var primary=Resources.Load<TextAsset>("ARE/JourneyMaps/Wald_v1_731904");
            if(primary==null)return null;
            var maps=new List<ForgedWorldMap>{ForgedWorldMap.FromBytes(primary.bytes)};
            // Retain explicitly named historical maps when a new authored version is added.
            var alternative=Resources.Load<TextAsset>("ARE/JourneyMaps/Wald_v1_731905");
            if(alternative!=null)maps.Add(ForgedWorldMap.FromBytes(alternative.bytes));
            return new JourneyMapCatalog(maps.ToArray());
        }
        private HungerAction[] JourneyActions()
        {
            var actions=new List<HungerAction>();var f=_game.Progression.Journey.Current;
            for(int i=0;i<f.Geometry.ChestCount;i++)
            {
                f.Geometry.Chest(i,out var x,out var z);
                long dx=x-_game.PlayerXmm,dz=z-_game.PlayerZmm;
                if((f.OpenChests&(1<<i))==0&&dx*dx+dz*dz<=ArenaSimulation.NpcInteractionRadiusMm*ArenaSimulation.NpcInteractionRadiusMm)
                    actions.Add(i==0?HungerAction.OpenWestChest:HungerAction.OpenEastChest);
            }
            return actions.ToArray();
        }
        private void BuildJourneyScenery()
        {
            _worldPassage=_returnPassage=_journeyGate=null;for(int i=0;i<_journeyLids.Length;i++)_journeyLids[i]=null;
            if(_game.InJourneyCave)BuildJourneyCave();else BuildJourneyWoodland();
            var g=_game.JourneyGeometry;
            for(int i=0;i<g.ChestCount;i++)
            {
                g.Chest(i,out var x,out var z);var at=new Vector3(x/1000f,GroundHeight(x/1000f,z/1000f),z/1000f);
                var chest=new GameObject("Journey_Chest_"+i).transform;chest.position=at;_chapterScenery.Add(chest);
                if(!PassiveArtVisuals.TryBindGrounded(chest,"AshChest",at.y))
                    ChapterShape(PrimitiveType.Cube,"Journey_Chest_Fallback_"+i,at+Vector3.up*.35f,new Vector3(1.3f,.7f,.8f),new Color(.34f,.23f,.12f),"AshCrate");
                _journeyLids[i]=ChapterShape(PrimitiveType.Cube,"Journey_Chest_Seal_"+i,at+Vector3.up*.8f,new Vector3(.6f,.08f,.45f),new Color(.75f,.57f,.22f));
            }
            UpdateJourneyPresentation();
        }
        private void BuildJourneyWoodland()
        {
            var geometry=(JourneyRouteLayout)_game.JourneyGeometry;var map=geometry.Map;
            RenderSettings.ambientLight=new Color(.40f,.44f,.47f);RenderSettings.fogColor=new Color(.29f,.34f,.4f);RenderSettings.fogDensity=.010f;
            var ground=Resources.Load<Material>("ARE/Visuals/WoodlandGround")??WoodlandFallbackMaterial();
            var chunks=new Dictionary<(int,int),Transform>();
            // All 16,641 authored integer height nodes are projected without Unity terrain collision authority.
            for(int cz=0;cz<8;cz++)for(int cx=0;cx<8;cx++)
            {
                float ox=-64+cx*16,oz=-64+cz*16;
                var chunk=new GameObject("Journey_Terrain_"+cx+"_"+cz).transform;chunk.position=new Vector3(ox+8,0,oz+8);_chapterScenery.Add(chunk);_woodlandChunks.Add(chunk);
                var vegetation=new GameObject("Journey_Vegetation").transform;vegetation.SetParent(chunk,false);chunks[(cx,cz)]=vegetation;
                var vertices=new Vector3[17*17];var uv=new Vector2[vertices.Length];var triangles=new int[16*16*6];int ti=0;
                for(int z=0;z<=16;z++)for(int x=0;x<=16;x++)
                {
                    int i=z*17+x;vertices[i]=new Vector3(x-8,GroundHeight(ox+x,oz+z),z-8);uv[i]=new Vector2((ox+x)*.25f,(oz+z)*.25f);
                    if(x<16&&z<16){triangles[ti++]=i;triangles[ti++]=i+17;triangles[ti++]=i+1;triangles[ti++]=i+1;triangles[ti++]=i+17;triangles[ti++]=i+18;}
                }
                var mesh=new Mesh{name="Journey_Authored_Surface_"+cx+"_"+cz,vertices=vertices,uv=uv,triangles=triangles};mesh.RecalculateNormals();mesh.RecalculateBounds();
                chunk.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;chunk.gameObject.AddComponent<MeshRenderer>().sharedMaterial=ground;chunk.gameObject.AddComponent<WoodlandMeshOwner>().Mesh=mesh;
            }
            Transform Chunk(float x,float z)=>chunks[(Mathf.Clamp((int)((x+64)/16),0,7),Mathf.Clamp((int)((z+64)/16),0,7))];
            var decoration=new DeterministicRng(_game.Progression.Journey.RouteSeed^0x4445434F52UL);
            foreach(var obstacle in map.Obstacles)
            {
                float x=obstacle.Xmm/1000f,z=obstacle.Zmm/1000f;
                PlantWoodland(Chunk(x,z),decoration.Next64()%2==0?"WoodlandTreeA":"WoodlandTreeB",x,z,5f+(decoration.Next64()%175)/100f,decoration);
            }
            // Rocks and thickets mark the actual generated route boundary. Bounds stay inside the blocked cell.
            for(int z=1;z<geometry.Size-1;z++)for(int x=1;x<geometry.Size-1;x++)
                if(!geometry.IsFloor(x,z)&&(geometry.IsFloor(x-1,z)||geometry.IsFloor(x+1,z)||geometry.IsFloor(x,z-1)||geometry.IsFloor(x,z+1)))
                {
                    float wx=geometry.Centre(x)/1000f,wz=geometry.Centre(z)/1000f;
                    JourneyBoundary(Chunk(wx,wz),wx,wz,decoration);
                }
            // A separate bounded cosmetic stream scatters low plants beyond the clear walking lane.
            var undergrowth=new DeterministicRng(_game.Progression.Journey.RouteSeed^0x554E44455247524FUL);
            int planted=0;
            for(int attempt=0;attempt<1200&&planted<160;attempt++)
            {
                int x=2+(int)(undergrowth.Next64()%61),z=2+(int)(undergrowth.Next64()%61);
                bool clear=true,nearRoute=false;
                for(int dz=-2;dz<=2;dz++)for(int dx=-2;dx<=2;dx++)if(geometry.IsFloor(x+dx,z+dz))
                {nearRoute=true;if(Math.Abs(dx)<=1&&Math.Abs(dz)<=1)clear=false;}
                if(!clear||!nearRoute)continue;
                long wx=geometry.Centre(x)+(long)(undergrowth.Next64()%801)-400,wz=geometry.Centre(z)+(long)(undergrowth.Next64()%801)-400;
                if(!map.CanStand(wx,wz,450))continue;
                int kind=planted%3;string key=kind==0?"WoodlandMushroomRed":kind==1?"WoodlandFern":"WoodlandShrub";
                PlantWoodland(Chunk(wx/1000f,wz/1000f),key,wx/1000f,wz/1000f,kind==0?.25f:kind==1?.45f:.7f,undergrowth);planted++;
            }
            WoodlandPortal("Zurück nach Aschenzoll",geometry.ReturnX/1000f,geometry.ReturnZ/1000f);
            WoodlandPortal("Seitenhöhle · vier Ebenen",geometry.CaveX/1000f,geometry.CaveZ/1000f);
            FreightSign("Journey_Onward_Sign","Weg zum nächsten Handelsposten",new Vector3(geometry.ForwardX/1000f,GroundHeight(geometry.ForwardX/1000f,geometry.ForwardZ/1000f)+1.5f,geometry.ForwardZ/1000f));
        }
        private void JourneyBoundary(Transform parent,float x,float z,DeterministicRng rng)
        {
            var t=new GameObject("Journey_Route_Thicket").transform;t.SetParent(parent,false);t.position=new Vector3(x,GroundHeight(x,z),z);
            if(PassiveArtVisuals.TryBind(t,rng.Next64()%3==0?"WoodlandShrub":"WoodlandRock"))
            {
                var renderers=t.GetComponentsInChildren<Renderer>();var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);
                t.localScale=Vector3.one*Mathf.Min(1.8f/Mathf.Max(.1f,Mathf.Max(b.size.x,b.size.z)),1.6f/Mathf.Max(.1f,b.size.y));
                b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);t.position+=new Vector3(x-b.center.x,GroundHeight(x,z)-b.min.y,z-b.center.z);
            }
            else
            {
                var rock=GameObject.CreatePrimitive(PrimitiveType.Cube);Destroy(rock.GetComponent<Collider>());rock.transform.SetParent(t,false);rock.transform.localPosition=Vector3.up*.6f;rock.transform.localScale=new Vector3(1.85f,1.2f,1.85f);rock.GetComponent<Renderer>().sharedMaterial=WoodlandFallbackMaterial();
            }
        }
        private void BuildJourneyCave()
        {
            var map=_game.JourneyGeometry;
            // Same reworked set as the crypt when its private art is present; otherwise the previous look.
            bool dressed=PassiveArtVisuals.Has("DungeonCorner");
            _dungeonDressed=dressed;_dungeonFlames=0;_dungeonLights.Clear();
            DungeonAir(dressed);
            // Tiles of the passages and chests stay free of props.
            var reserved=new HashSet<long>{JourneyTile(map,map.ReturnX,map.ReturnZ),JourneyTile(map,map.ForwardX,map.ForwardZ)};
            for(int i=0;i<map.ChestCount;i++){map.Chest(i,out var cx,out var cz);reserved.Add(JourneyTile(map,cx,cz));}
            DungeonTiles("Journey_Cave_",0,map.Size-1,map.IsFloor,index=>map.Centre(index)/1000f,map.LayoutHash,reserved,null,dressed);
            _returnPassage=ChapterShape(PrimitiveType.Cylinder,"Journey_Return_Passage",new Vector3(map.ReturnX/1000f,.035f,map.ReturnZ/1000f),new Vector3(3.6f,.02f,3.6f),new Color(.35f,.68f,.85f));
            if(_game.Progression.Journey.Depth<_game.Progression.Journey.FloorCount)
            {
                _worldPassage=ChapterShape(PrimitiveType.Cylinder,"Journey_Descent_Passage",new Vector3(map.ForwardX/1000f,.035f,map.ForwardZ/1000f),new Vector3(3.6f,.02f,3.6f),new Color(.9f,.65f,.23f));
                _journeyGate=ChapterShape(PrimitiveType.Cube,"Journey_Descent_Gate",new Vector3(0,1.4f,22),new Vector3(3.9f,2.8f,.35f),new Color(.31f,.32f,.35f),
                    dressed&&PassiveArtVisuals.Has("DungeonGate")?"DungeonGate":"CryptDoor");
            }
        }
        private static long JourneyTile(JourneyGeometry map,long xmm,long zmm)
        {
            return DungeonTileKey(Mathf.RoundToInt((xmm-map.OriginMm)/(float)map.CellMm),Mathf.RoundToInt((zmm-map.OriginMm)/(float)map.CellMm));
        }
        private void UpdateJourneyPresentation()
        {
            if(!_game.InJourney)return;
            if(_journeyGate!=null)_journeyGate.gameObject.SetActive(!_game.JourneyForwardOpen);
            for(int i=0;i<_journeyLids.Length;i++)if(_journeyLids[i]!=null)_journeyLids[i].gameObject.SetActive((_game.Progression.Journey.Current.OpenChests&(1<<i))==0);
        }
    }
}
