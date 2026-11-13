using System.Collections.Generic;
using AreArpg.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    public sealed partial class ArenaRuntime
    {
        private readonly List<Transform> _woodlandChunks=new List<Transform>();
        private readonly List<Transform> _woodlandPortals=new List<Transform>();
        private readonly Transform[] _forestChestLids=new Transform[4];
        private readonly Transform[] _bearCubs=new Transform[3];
        private float GroundHeight(float x,float z) => _game.InForest?_game.Progression.Woodland.Layout.HeightMm((long)(x*1000),(long)(z*1000))/1000f:0f;
        private HungerAction[] WoodlandActions()
        {
            var w=_game.Progression.Woodland; int act=_game.Progression.EntryAct;
            if(act==2&&_game.Progression.Rattler.Reported&&!w.Active)return new[]{HungerAction.AcceptTransportLead};
            if(!w.Active)return null;
            if(act==2)
            {
                if(w.Pelts==3)return new[]{HungerAction.CraftPeltArmor};
                if(w.Choice==CubChoice.Spared&&!w.CubsReported)return new[]{HungerAction.ReportCubs};
                if(w.CubsReported&&!w.ProtectionOrganized)return new[]{HungerAction.OrganizeCubProtection};
            }
            if(act==9)
            {
                if(!w.CubsInspected)return new[]{HungerAction.InspectCubs};
                if(w.Choice==CubChoice.Undecided)return new[]{HungerAction.SpareCubs,HungerAction.SkinCubs};
            }
            if(_game.InForest)
            {
                var actions=new List<HungerAction>();
                for(int i=0;i<4;i++)if(w.Layout.HasChest(i)&&(w.OpenChests&(1<<i))==0)
                {
                    w.Layout.Chest(i,out var x,out var z);long dx=x-_game.PlayerXmm,dz=z-_game.PlayerZmm;
                    if(dx*dx+dz*dz<=2400L*2400)actions.Add((HungerAction)((int)HungerAction.ForestChest0+i));
                }
                return actions.ToArray();
            }
            return _game.InWoodlandRegion?new HungerAction[0]:null;
        }
        private void WoodlandPortal(string name,float x,float z)
        {
            var t=ChapterShape(PrimitiveType.Cylinder,"Woodland_Passage_"+name,new Vector3(x,.04f,z),new Vector3(3.6f,.02f,3.6f),new Color(.45f,.73f,.78f));
            _woodlandPortals.Add(t);
            var marker=ChapterShape(PrimitiveType.Cube,"Woodland_Sign_"+name,new Vector3(x+2,1.2f,z),new Vector3(.12f,2.4f,.12f),new Color(.27f,.21f,.15f));
            var label=new GameObject("Sign_Text").AddComponent<TextMesh>();label.transform.SetParent(marker,false);label.transform.localScale=new Vector3(1/.12f,1/2.4f,1/.12f)*.07f;label.transform.localPosition=Vector3.up*.35f;label.anchor=TextAnchor.MiddleCenter;label.text=name;label.fontSize=35;label.color=new Color(.87f,.8f,.65f);
        }
        private void BuildWoodlandScenery()
        {
            _worldPassage=_returnPassage=null;
            RenderSettings.ambientLight=new Color(.39f,.43f,.48f);RenderSettings.fogColor=new Color(.29f,.34f,.4f);RenderSettings.fogDensity=.013f;
            if(!_game.InForest) { BuildWoodlandAnchor();return; }
            var backdrop=OutdoorBackdrop.Create(8);backdrop.localScale=new Vector3(1.7f,1f,2f);_chapterScenery.Add(backdrop);
            var map=_game.Progression.Woodland.Layout;
            var ground=Resources.Load<Material>("ARE/Visuals/WoodlandGround");
            if(ground==null)ground=new Material(Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard")){color=new Color(.28f,.31f,.33f)};
            var rng=new DeterministicRng(map.Seed ^ 0xA47UL);
            // Eight metre chunks share the exact same world-space height and UV field; shared materials and distance culling.
            for(int cz=-14;cz<14;cz+=2)for(int cx=-12;cx<12;cx+=2)
            {
                var chunk=new GameObject("Forest_Chunk_"+cx+"_"+cz).transform;chunk.position=new Vector3(cx*4+4,0,cz*4+4);_chapterScenery.Add(chunk);_woodlandChunks.Add(chunk);
                var vegetation=new GameObject("Forest_Vegetation").transform;vegetation.SetParent(chunk,false);
                var verts=new Vector3[81];var uv=new Vector2[81];var indices=new int[384];int ti=0;
                for(int z=0;z<=8;z++)for(int x=0;x<=8;x++)
                {
                    float wx=cx*4+x,wz=cz*4+z;int i=z*9+x;
                    verts[i]=new Vector3(x-4,GroundHeight(wx,wz),z-4);uv[i]=new Vector2(wx*.25f,wz*.25f);
                    if(x<8&&z<8){indices[ti++]=i;indices[ti++]=i+9;indices[ti++]=i+1;indices[ti++]=i+1;indices[ti++]=i+9;indices[ti++]=i+10;}
                }
                var mesh=new Mesh{name="ForestGround_"+cx+"_"+cz};mesh.vertices=verts;mesh.uv=uv;mesh.triangles=indices;mesh.RecalculateNormals();mesh.RecalculateBounds();
                chunk.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;chunk.gameObject.AddComponent<MeshRenderer>().sharedMaterial=ground;chunk.gameObject.AddComponent<WoodlandMeshOwner>().Mesh=mesh;
                for(int z=cz;z<cz+2;z++)for(int x=cx;x<cx+2;x++)
                {
                    bool path=map.IsFloor(x,z);float wx=x*4,wz=z*4;
                    if(!path)
                    {
                        float height=rng.Next64()%4==0?2.5f+(rng.Next64()%180)/100f:6f+(rng.Next64()%240)/100f;
                        PlantWoodland(vegetation,(rng.Next64()%2==0?"WoodlandTreeA":"WoodlandTreeB"),wx,wz,height,rng);
                        PlantWoodland(vegetation,"WoodlandShrub",wx+1.1f,wz+1.1f,.8f+(rng.Next64()%50)/100f,rng);
                        // Boundary boulders visibly mark impassable thickets without blocking the open path.
                        if(map.IsFloor(x-1,z)||map.IsFloor(x+1,z)||map.IsFloor(x,z-1)||map.IsFloor(x,z+1))PlantWoodland(vegetation,"WoodlandRock",wx,wz,1.7f,rng);
                    }
                    else
                    {
                        // Low undergrowth stays out of the central combat lane.
                        if(rng.Next64()%3==0)PlantWoodland(vegetation,"WoodlandFern",wx+1.5f,wz+1.5f,.45f,rng);
                        if(rng.Next64()%5==0)PlantWoodland(vegetation,rng.Next64()%2==0?"WoodlandMushroomRed":"WoodlandMushroomMoon",wx-1.4f,wz+1.4f,.3f,rng);
                    }
                }
            }
            WoodlandPortal("Zur Mühle",0,-48);WoodlandPortal("Aschenzoll",0,48);WoodlandPortal("Bärenhöhle",-36,0);
            for(int i=0;i<4;i++)
            {
                if(!map.HasChest(i))continue;map.Chest(i,out var x,out var z);var at=new Vector3(x/1000f,GroundHeight(x/1000f,z/1000f),z/1000f);
                ChapterShape(PrimitiveType.Cube,"Woodland_Chest_"+i,at+Vector3.up*.35f,new Vector3(1.3f,.7f,.8f),new Color(.3f,.22f,.15f),"AshChest");
                _forestChestLids[i]=ChapterShape(PrimitiveType.Cube,"Woodland_Chest_Lid_"+i,at+Vector3.up*.78f,new Vector3(1.3f,.12f,.85f),new Color(.4f,.28f,.15f));
            }
        }
        private void PlantWoodland(Transform parent,string key,float x,float z,float height,DeterministicRng rng)
        {
            var t=new GameObject(key).transform;t.SetParent(parent,false);t.position=new Vector3(x,GroundHeight(x,z),z);
            if(PassiveArtVisuals.TryBind(t,key))
            {
                var renderers=t.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                float fit=height/Mathf.Max(.01f,bounds.size.y);if(key=="WoodlandRock")fit=Mathf.Min(fit,3.5f/Mathf.Max(bounds.size.x,bounds.size.z));
                t.localScale=Vector3.one*fit;
                bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                t.position+=new Vector3(x-bounds.center.x,GroundHeight(x,z)-bounds.min.y,z-bounds.center.z);t.rotation=Quaternion.Euler(0,(float)(rng.Next64()%360),0);
            }
            else
            {
                var fallback=GameObject.CreatePrimitive(key.Contains("Tree")?PrimitiveType.Cylinder:PrimitiveType.Sphere);Destroy(fallback.GetComponent<Collider>());
                fallback.transform.SetParent(t,false);fallback.transform.localScale=new Vector3(height*.25f,height*.5f,height*.25f);fallback.transform.localPosition=Vector3.up*height*.5f;
                fallback.GetComponent<Renderer>().material.color=new Color(.25f,.27f,.22f);
            }
        }
        private void BuildWoodlandAnchor()
        {
            bool cave=_game.Progression.EntryAct==9;
            var floor=ChapterShape(PrimitiveType.Cube,cave?"BearCave_Floor":"Aschenzoll_Arrival_Ground",new Vector3(0,-.15f,0),new Vector3(cave?22:30,.3f,42),new Color(.3f,.29f,.27f),cave?null:"AshStoneFloor");
            var earth=cave?Resources.Load<Material>("ARE/Visuals/WoodlandGround"):null;
            if(earth!=null)
            {
                var material=new Material(earth);material.mainTextureScale=new Vector2(5.5f,10.5f);material.color=new Color(.7f,.63f,.54f);
                floor.GetComponent<Renderer>().sharedMaterial=material;floor.gameObject.AddComponent<WoodlandMeshOwner>().Material=material;
            }
            WoodlandPortal("Zurück zum Wald",0,-18);
            if(cave)
            {
                for(int side=-1;side<=1;side+=2)for(int z=-16;z<=12;z+=4)ChapterShape(PrimitiveType.Cube,"BearCave_RockWall",new Vector3(side*11,1.8f,z),new Vector3(3,3.6f,4.4f),new Color(.22f,.24f,.27f),"AshRocks");
                ChapterShape(PrimitiveType.Cube,"BearCave_BackWall",new Vector3(0,1.7f,16),new Vector3(23,3.4f,4),new Color(.22f,.24f,.27f),"AshRocks");
                ChapterShape(PrimitiveType.Cylinder,"BearCave_Nest",new Vector3(0,.04f,5),new Vector3(4,.05f,3),new Color(.31f,.24f,.12f));
                for(int i=0;i<3;i++)
                {
                    var t=ChapterShape(PrimitiveType.Cube,"BearCub_"+i,new Vector3((i-1)*1.2f,.3f,5+(i%2)*.8f),new Vector3(.8f,.6f,1.1f),new Color(.4f,.25f,.13f));
                    if(PassiveArtVisuals.TryBindGrounded(t,"RoadBear",0)){var art=t.GetChild(t.childCount-1);art.localScale*=.32f;}
                    _bearCubs[i]=t;
                }
            }
            else
            {
                ChapterShape(PrimitiveType.Cube,"Aschenzoll_FreightShelter",new Vector3(-8,2,6),new Vector3(6,4,9),new Color(.34f,.27f,.19f),"AshRuin");
                for(int i=0;i<6;i++)ChapterShape(PrimitiveType.Cube,"Aschenzoll_FreightCrate_"+i,new Vector3(5+(i%2)*2,.55f,2+(i/2)*3),new Vector3(1.5f,1.1f,1.5f),new Color(.43f,.3f,.17f),"AshCrate");
                ChapterShape(PrimitiveType.Cube,"Aschenzoll_NoticeBoard",new Vector3(-5,1.3f,-12),new Vector3(2,2.6f,.2f),new Color(.41f,.3f,.21f));
                FreightSign("Freight_NoticeSign","Aushang",new Vector3(-5,1.2f,-12.2f));
                BuildFreightScenery();
            }
        }
        private void UpdateWoodlandPresentation()
        {
            UpdateFreightPresentation();
            foreach(var chunk in _woodlandChunks)if(chunk!=null) {var d=chunk.position-new Vector3(_game.PlayerXmm/1000f,0,_game.PlayerZmm/1000f);chunk.GetChild(0).gameObject.SetActive(d.sqrMagnitude<52*52);}
            for(int i=0;i<4;i++)if(_forestChestLids[i]!=null)_forestChestLids[i].localRotation=Quaternion.Euler((_game.Progression.Woodland.OpenChests&(1<<i))!=0?-65:0,0,0);
            for(int i=0;i<3;i++)if(_bearCubs[i]!=null)_bearCubs[i].gameObject.SetActive(_game.Progression.Woodland.Choice!=CubChoice.Skinned);
        }
    }
}
