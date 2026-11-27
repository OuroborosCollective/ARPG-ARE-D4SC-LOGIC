using System.Collections.Generic;
using AreArpg.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    public sealed partial class ArenaRuntime
    {
        private readonly Transform[] _dungeonLids = new Transform[2];
        private Transform _dungeonGate;
        private readonly List<Light> _dungeonLights = new List<Light>();
        private bool _dungeonDressed;
        private int _dungeonFlames;

        private HungerAction[] DungeonActions()
        {
            var actions=new List<HungerAction>(); var d=_game.Progression.Dungeon;
            for(int i=0;i<2;i++)
                if(d.Current.Layout.HasChest(i) && (d.Current.OpenChests&(1<<i))==0)
                {
                    d.Current.Layout.Chest(i,out var x,out var z);
                    long dx=x-_game.PlayerXmm,dz=z-_game.PlayerZmm;
                    if(dx*dx+dz*dz<=ArenaSimulation.NpcInteractionRadiusMm*ArenaSimulation.NpcInteractionRadiusMm)
                        actions.Add(i==0?HungerAction.OpenWestChest:HungerAction.OpenEastChest);
                }
            if(d.Depth==2 && !_game.Progression.Investigation.TraceFound) actions.Add(HungerAction.InspectCryptTrace);
            else if(d.Depth==2 && !_game.Progression.Rattler.Active) actions.Add(HungerAction.OpenInnerDoor);
            else if(d.Depth==3 && _game.Progression.Rattler.Creature.IsDead && !_game.Progression.Rattler.BrandExamined) actions.Add(HungerAction.InspectRoyalBrand);
            return actions.ToArray();
        }

        private void BuildDungeonScenery()
        {
            var d=_game.Progression.Dungeon; var map=d.Current.Layout;
            // The reworked crypt set (purchased dark fantasy pack) is private art. Without it every step below
            // falls back to the previous look, so a build without the pack still shows a complete dungeon.
            bool dressed=PassiveArtVisuals.Has("DungeonCorner");
            _dungeonDressed=dressed; _dungeonFlames=0; _dungeonLights.Clear();
            DungeonAir(dressed);
            _worldPassage=null;
            ulong seed=d.Seed^((ulong)d.Depth<<40);
            // Tiles with a chest or a room centre stay free of props; a wall that carries a shrine stays plain.
            var reserved=new HashSet<long>(); var calm=new HashSet<long>();
            for(int i=0;i<2;i++) if(map.HasChest(i)) { map.Chest(i,out var cx,out var cz); reserved.Add(DungeonTile(cx,cz)); }
            var shrines=new int[map.RoomCount][];
            for(int i=0;i<map.RoomCount;i++)
            {
                map.Room(i,out var rx,out var rz); reserved.Add(DungeonTile(rx,rz));
                if(!dressed || !PassiveArtVisuals.Has("DungeonShrine")) continue;
                int roomX=Mathf.RoundToInt(rx/4000f),roomZ=Mathf.RoundToInt(rz/4000f);
                if(!CryptDungeonDressing.ShrineSpot(map.IsFloor,roomX,roomZ,out int sx,out int sz,out int shrineSide)) continue;
                // A shrine belongs to its room: a wall far across an open hall is not this room's wall.
                if(Mathf.Abs(sx-roomX)+Mathf.Abs(sz-roomZ)>CryptDungeonDressing.ShrineReach) continue;
                // Never in front of a passage frame at the two ends of the middle lane.
                if(sx==0 && shrineSide>=CryptDungeonDressing.South) continue;
                shrines[i]=new[]{sx,sz,shrineSide}; calm.Add(DungeonTileKey(sx,sz)*4+shrineSide);
            }
            for(int i=0;i<map.RoomCount;i++)
            {
                map.Room(i,out var x,out var z);
                var lampAt=new Vector3(x/1000f,2.6f,z/1000f); float brightness=1.2f;
                // A light needs a source the player can see: a lantern hangs where the room light always was.
                if(dressed)
                {
                    var lantern=DungeonModule("Dungeon_Lantern_"+i,"DungeonLantern",new Vector3(x/1000f,0,z/1000f),0,false,true);
                    if(lantern!=null) { DungeonFlames(lantern); lampAt.y=CryptDungeonDressing.LanternHeight; brightness=DungeonCandleLight; }
                    if(shrines[i]!=null) DungeonShrine(map,i,shrines[i]);
                }
                var lamp=new GameObject("Dungeon_Room_Light_"+i); lamp.transform.position=lampAt;
                var light=lamp.AddComponent<Light>(); light.color=dressed?new Color(1,.62f,.30f):new Color(1,.69f,.35f); light.intensity=brightness; light.range=dressed?14:10; light.shadows=LightShadows.None;
                _chapterScenery.Add(lamp.transform);
                _dungeonLights.Add(light);
            }
            DungeonTiles("Dungeon_",-6,6,map.IsFloor,index=>index*CryptDungeonDressing.TileMetres,seed,reserved,calm,dressed,d.Depth);
            for(int i=0;i<2;i++)
            {
                _dungeonLids[i]=null; if(!map.HasChest(i)) continue;
                map.Chest(i,out var x,out var z); var at=new Vector3(x/1000f,.35f,z/1000f);
                ChapterShape(PrimitiveType.Cube,"Dungeon_Chest_"+i,at,new Vector3(1.3f,.7f,.8f),new Color(.34f,.23f,.12f),"AshCrate");
                _dungeonLids[i]=ChapterShape(PrimitiveType.Cube,"Dungeon_Chest_Lid_"+i,at+Vector3.up*.45f,new Vector3(1.4f,.18f,.9f),new Color(.45f,.31f,.15f),"MillWood");
                for(int band=-1;band<=1;band+=2) ChapterShape(PrimitiveType.Cube,"Dungeon_Chest_Band",at+new Vector3(band*.38f,.02f,0),new Vector3(.09f,.76f,.84f),new Color(.65f,.48f,.22f));
            }
            _returnPassage=ChapterShape(PrimitiveType.Cylinder,"ARE_Return_Passage",new Vector3(0,.035f,-24),new Vector3(3.6f,.02f,3.6f),new Color(.35f,.68f,.85f));
            if(d.Depth<3) _worldPassage=ChapterShape(PrimitiveType.Cylinder,"ARE_World_Passage",new Vector3(0,.035f,24),new Vector3(3.6f,.02f,3.6f),new Color(.9f,.65f,.23f));
            if(dressed && PassiveArtVisuals.Has("DungeonPortal"))
            {
                // Passage frames stand against the end walls of the middle lane; the glowing discs stay the walkable marks.
                DungeonPortal("Dungeon_Portal_Return",new Vector3(0,0,-25.35f),0);
                if(d.Depth<3) DungeonPortal("Dungeon_Portal_World",new Vector3(0,0,25.35f),180);
            }
            _dungeonGate=ChapterShape(PrimitiveType.Cube,"Dungeon_Descent_Gate",new Vector3(0,1.4f,22),new Vector3(3.9f,2.8f,.35f),new Color(.31f,.32f,.35f),
                dressed && PassiveArtVisuals.Has("DungeonGate")?"DungeonGate":"CryptDoor");
            if(d.Depth==1)
            {
                _cryptWarning=ChapterShape(PrimitiveType.Cylinder,"Dungeon_Fall_Warning",new Vector3(0,.04f,-3),new Vector3(4.8f,.02f,4.8f),new Color(.9f,.18f,.08f));
                _cryptDebris=ChapterShape(PrimitiveType.Cube,"Dungeon_Fallen_Debris",new Vector3(0,.06f,-3),new Vector3(2,.12f,1.5f),new Color(.3f,.29f,.27f),"AshStoneFloor");
                UpdateInvestigationPresentation();
            }
            if(d.Depth==2)
            {
                ClueMark("Dungeon_Inner_Door_Clue",0,15);
                ChapterShape(PrimitiveType.Cube,"Dungeon_Fresh_Bandage",new Vector3(0,.08f,15),new Vector3(.8f,.12f,.3f),new Color(.8f,.72f,.5f));
            }
            AnimateDungeonLights();
            UpdateDungeonPresentation();
        }

        /// <summary>
        /// One visual module. Art of the reworked set is authored in place (pivot on the ground, walls on their
        /// border line) and must not be re-centred: relief in front of a wall would otherwise push the wall back and
        /// break the wall line. The previous art has arbitrary pivots and is centred by its bounds as before.
        /// Returns the bound module, or null when the blockout stands in.
        /// </summary>
        private Transform DungeonModule(string label,string key,Vector3 at,float yaw,bool floor,bool authored,Texture2D masonry=null)
        {
            var holder=new GameObject(label).transform;
            if(PassiveArtVisuals.TryBind(holder,key))
            {
                if(authored) holder.position=at;
                else
                {
                    var art=holder.GetChild(0); var renderers=art.GetComponentsInChildren<Renderer>();
                    var bounds=renderers[0].bounds; foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    art.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                    holder.position=at+Vector3.down*(floor?bounds.size.y:0);
                }
                ApplyCryptMasonry(holder, masonry, floor);
                holder.rotation=Quaternion.Euler(0,yaw,0);
                _chapterScenery.Add(holder);
                return holder;
            }
            Destroy(holder.gameObject);
            // Pillars, props, shrines and frames are dressing: without their art nothing stands in.
            if(authored && !floor && !key.StartsWith("DungeonWall",System.StringComparison.Ordinal)) return null;
            var shape=ChapterShape(PrimitiveType.Cube,label,at+Vector3.up*(floor?-.08f:1.5f),
                floor?new Vector3(4,.16f,4):new Vector3(4,3,.3f),new Color(.27f,.29f,.31f),floor?"AshStoneFloor":"AshStoneWall");
            ApplyCryptMasonry(shape, masonry, floor);
            shape.rotation=Quaternion.Euler(0,yaw,0);
            return null;
        }

        /// <summary>
        /// Floors, walls, corner pillars and props of one tiled floor. The procedural crypt and the journey caves
        /// share the four-metre grid: tile indexes run from min to max and centre turns an index into metres.
        /// Walls of the reworked set face their room and vary by tile and seed; the middle lane (passages, gate,
        /// story marks) and the walls named in calm stay plain, tiles named in reserved stay free of props.
        /// </summary>
        private void DungeonTiles(string prefix,int min,int max,System.Func<int,int,bool> isFloor,System.Func<int,float> centre,ulong seed,
            HashSet<long> reserved,HashSet<long> calm,bool dressed,int cryptDepth=0)
        {
            string stone = cryptDepth == 1 ? "Moss" : cryptDepth == 2 ? "Limestone" : cryptDepth == 3 ? "Slate" : null;
            Texture2D wallTexture = stone == null ? null : Resources.Load<Texture2D>("ARE/Visuals/CryptMasonry/" + stone);
            Texture2D floorTexture = cryptDepth == 3 ? Resources.Load<Texture2D>("ARE/Visuals/CryptMasonry/Marble") : null;
            int decorated=0; while(dressed && decorated<3 && PassiveArtVisuals.Has("DungeonWall"+(char)('B'+decorated))) decorated++;
            int props=0; while(dressed && props<16 && PassiveArtVisuals.Has("DungeonProp"+props)) props++;
            float half=CryptDungeonDressing.TileMetres*.5f;
            for(int z=min;z<=max;z++) for(int x=min;x<=max;x++)
            {
                if(!isFloor(x,z)) continue;
                var tile=new Vector3(centre(x),0,centre(z));
                DungeonModule(prefix+"Floor_"+x+"_"+z,"DungeonFloor",tile,0,true,dressed,floorTexture);
                for(int side=CryptDungeonDressing.West;side<=CryptDungeonDressing.North;side++)
                {
                    Vector3 inward=CryptDungeonDressing.Inward(side);
                    if(isFloor(x-Mathf.RoundToInt(inward.x),z-Mathf.RoundToInt(inward.z))) continue;
                    bool quiet=Mathf.Abs(tile.x)<half || (calm!=null && calm.Contains(DungeonTileKey(x,z)*4+side));
                    int variant=quiet?0:CryptDungeonDressing.WallVariant(CryptDungeonDressing.Hash(x,z,side,seed),decorated);
                    var wall=DungeonModule(prefix+"Wall_"+"WESN"[side]+"_"+x+"_"+z,variant==0?"DungeonWall":"DungeonWall"+(char)('A'+variant),
                        tile-inward*half,dressed?CryptDungeonDressing.WallYaw(side):side<CryptDungeonDressing.South?90:0,false,dressed,wallTexture);
                    if(!dressed) continue;
                    if(variant!=0) { DungeonFlames(wall); continue; }
                    if(quiet || (reserved!=null && reserved.Contains(DungeonTileKey(x,z)))) continue;
                    uint roll=CryptDungeonDressing.Hash(x,z,side+16,seed);
                    int prop=CryptDungeonDressing.PropIndex(roll,props);
                    if(prop<0) continue;
                    DungeonFlames(DungeonModule(prefix+"Prop_"+"WESN"[side]+"_"+x+"_"+z,"DungeonProp"+prop,
                        tile+CryptDungeonDressing.PropOffset(side,roll),CryptDungeonDressing.PropYaw(side,roll),false,true));
                }
            }
            if(!dressed) return;
            for(int z=min;z<=max+1;z++) for(int x=min;x<=max+1;x++)
                if(CryptDungeonDressing.CornerPillar(isFloor(x-1,z-1),isFloor(x,z-1),isFloor(x-1,z),isFloor(x,z)))
                    DungeonModule(prefix+"Corner_"+x+"_"+z,"DungeonCorner",new Vector3(centre(x)-half,0,centre(z)-half),0,false,true);
        }

        private static void ApplyCryptMasonry(Transform module, Texture2D texture, bool floor)
        {
            if (texture == null) return;
            string surface = floor ? "Floor_Metric" : "Wall_Basic_Metric";
            var block = new MaterialPropertyBlock();
            foreach (var renderer in module.GetComponentsInChildren<MeshRenderer>())
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    var material = slots[i];
                    if (material == null || material.name != surface || !material.HasProperty("_BaseMap")) continue;
                    renderer.GetPropertyBlock(block, i);
                    block.SetTexture("_BaseMap", texture);
                    renderer.SetPropertyBlock(block, i);
                }
            }
        }

        private const float DungeonCandleLight=24f;

        /// <summary>
        /// Air of a tiled dungeon. The stone of the reworked set is dark slate: it needs more cool fill light than the
        /// previous pale blocks to stay readable on a phone, while the candles keep the warm accents.
        /// </summary>
        private static void DungeonAir(bool dressed)
        {
            RenderSettings.ambientLight=dressed?new Color(.44f,.50f,.64f):new Color(.30f,.32f,.36f);
            RenderSettings.fogColor=dressed?new Color(.05f,.07f,.11f):new Color(.07f,.09f,.12f);
            RenderSettings.fogDensity=dressed?.011f:.009f;
        }

        /// <summary>A passage frame against the end wall of the middle lane; its veil lights the stone around it.</summary>
        private void DungeonPortal(string label,Vector3 at,float yaw)
        {
            var frame=DungeonModule(label,"DungeonPortal",at,yaw,false,true);
            if(frame==null) return;
            var glow=new GameObject(label+"_Light"); glow.transform.SetParent(frame,false); glow.transform.localPosition=new Vector3(0,2.5f,.9f);
            var light=glow.AddComponent<Light>(); light.type=LightType.Point; light.color=new Color(.35f,1f,.85f); light.intensity=5f; light.range=8f; light.shadows=LightShadows.None;
            _dungeonLights.Add(light);
        }

        private static long DungeonTileKey(int x,int z) { return ((long)x<<32)^(uint)z; }
        private static long DungeonTile(long xmm,long zmm) { return DungeonTileKey(Mathf.RoundToInt(xmm/4000f),Mathf.RoundToInt(zmm/4000f)); }

        /// <summary>A candle shrine at the wall chosen for a room; decoration only, the room light hangs in the lantern.</summary>
        private void DungeonShrine(CryptDungeonLayout map,int room,int[] spot)
        {
            Vector3 inward=CryptDungeonDressing.Inward(spot[2]);
            var at=new Vector3(spot[0]*4,0,spot[1]*4)-inward*(CryptDungeonDressing.TileMetres*.5f-.75f);
            for(int i=0;i<2;i++)
                if(map.HasChest(i)) { map.Chest(i,out var cx,out var cz); if((new Vector3(cx/1000f,0,cz/1000f)-at).sqrMagnitude<2.6f*2.6f) return; }
            DungeonFlames(DungeonModule("Dungeon_Shrine_"+room,"DungeonShrine",at,CryptDungeonDressing.WallYaw(spot[2]),false,true));
        }

        /// <summary>
        /// Candle flames on the marker nodes of a visual: the game's own flame quad, no particle system and no
        /// extra light. The budget keeps a floor full of candles cheap on phones.
        /// </summary>
        private void DungeonFlames(Transform module)
        {
            if(module==null || !TryFlameAssets(out var material,out var mesh)) return;
            int budget=_atmosphere!=null && _atmosphere.ReducedEffects?CryptDungeonDressing.ReducedFlameBudget:CryptDungeonDressing.FlameBudget;
            foreach(Transform marker in module.GetComponentsInChildren<Transform>(true))
            {
                bool lantern=marker.name==CryptDungeonDressing.LanternMarker;
                if(!lantern && marker.name!=CryptDungeonDressing.FlameMarker) continue;
                // A room's lantern always burns; candles stop at the budget.
                if(!lantern && _dungeonFlames>=budget) continue;
                var flame=new GameObject("Dungeon_Flame"); flame.transform.SetParent(module,false);
                flame.transform.position=marker.position; flame.transform.rotation=Quaternion.identity;
                flame.transform.localScale=lantern?new Vector3(.7f,.85f,1f):new Vector3(.3f,.44f,1f);
                flame.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=flame.AddComponent<MeshRenderer>(); renderer.sharedMaterial=material;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows=false;
                // No per-flame property block: the shader takes each flame's phase from its position, so all flames batch.
                _dungeonFlames++;
            }
        }

        // Emissive lanterns and portal frames stay visible outside the closest-light budget.
        // Stable list order breaks equal distances and reproduces the same set on reload.
        private void AnimateDungeonLights()
        {
            if (!_game.InDungeon || _dungeonLights.Count == 0) return;
            bool reduced = _atmosphere != null && _atmosphere.ReducedEffects;
            int budget = reduced ? VillageLightReducedBudget : VillageLightBudget;
            Vector3 player = new Vector3(_game.PlayerXmm * .001f, 0, _game.PlayerZmm * .001f);
            float phase = (_game.CurrentTick % 3000UL) * .1f;
            for (int i = 0; i < _dungeonLights.Count; i++)
            {
                var light = _dungeonLights[i];
                if (light == null) continue;
                float distance = (light.transform.position - player).sqrMagnitude;
                int rank = 0;
                for (int j = 0; j < _dungeonLights.Count && rank < budget; j++)
                {
                    var other = _dungeonLights[j];
                    if (other == null || j == i) continue;
                    float otherDistance = (other.transform.position - player).sqrMagnitude;
                    if (otherDistance < distance || (otherDistance == distance && j < i)) rank++;
                }
                light.enabled = rank < budget;
                if (!_dungeonDressed || !light.enabled || light.range < 10f) continue;
                light.intensity = reduced ? DungeonCandleLight : DungeonCandleLight *
                    (.88f + .12f * Mathf.PerlinNoise(phase * 2.7f, i * 3.1f) + .035f * Mathf.Sin(phase * 13f + i));
            }
        }

        private void UpdateDungeonPresentation()
        {
            if(!_game.InDungeon) return;
            if(_dungeonGate!=null) _dungeonGate.gameObject.SetActive(!_game.DungeonForwardOpen);
            for(int i=0;i<2;i++) if(_dungeonLids[i]!=null)
                _dungeonLids[i].localRotation=Quaternion.Euler((_game.Progression.Dungeon.Current.OpenChests&(1<<i))!=0?-65:0,0,0);
        }
    }
}
