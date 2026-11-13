using System.Collections.Generic;
using AreArpg.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    public sealed partial class ArenaRuntime
    {
        private readonly Transform[] _dungeonLids = new Transform[2];
        private Transform _dungeonGate;

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
            RenderSettings.ambientLight=new Color(.30f,.32f,.36f);
            RenderSettings.fogColor=new Color(.07f,.09f,.12f); RenderSettings.fogDensity=.009f;
            _worldPassage=null;
            for(int z=-6;z<=6;z++) for(int x=-6;x<=6;x++)
            {
                if(!map.IsFloor(x,z)) continue;
                DungeonModule("Dungeon_Floor_"+x+"_"+z,"DungeonFloor",new Vector3(x*4,0,z*4),false,true);
                if(!map.IsFloor(x-1,z)) DungeonModule("Dungeon_Wall_W_"+x+"_"+z,"DungeonWall",new Vector3(x*4-2,0,z*4),true,false);
                if(!map.IsFloor(x+1,z)) DungeonModule("Dungeon_Wall_E_"+x+"_"+z,"DungeonWall",new Vector3(x*4+2,0,z*4),true,false);
                if(!map.IsFloor(x,z-1)) DungeonModule("Dungeon_Wall_S_"+x+"_"+z,"DungeonWall",new Vector3(x*4,0,z*4-2),false,false);
                if(!map.IsFloor(x,z+1)) DungeonModule("Dungeon_Wall_N_"+x+"_"+z,"DungeonWall",new Vector3(x*4,0,z*4+2),false,false);
            }
            for(int i=0;i<map.RoomCount;i++)
            {
                map.Room(i,out var x,out var z);
                var lamp=new GameObject("Dungeon_Room_Light_"+i); lamp.transform.position=new Vector3(x/1000f,2.6f,z/1000f);
                var light=lamp.AddComponent<Light>(); light.color=new Color(1,.69f,.35f); light.intensity=1.2f; light.range=10; light.shadows=LightShadows.None;
                _chapterScenery.Add(lamp.transform);
            }
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
            _dungeonGate=ChapterShape(PrimitiveType.Cube,"Dungeon_Descent_Gate",new Vector3(0,1.4f,22),new Vector3(3.9f,2.8f,.35f),new Color(.31f,.32f,.35f),"CryptDoor");
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
            UpdateDungeonPresentation();
        }

        private void DungeonModule(string label,string key,Vector3 at,bool turn,bool floor)
        {
            var holder=new GameObject(label).transform;
            if(PassiveArtVisuals.TryBind(holder,key))
            {
                var art=holder.GetChild(0); var renderers=art.GetComponentsInChildren<Renderer>();
                var bounds=renderers[0].bounds; foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                art.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                holder.position=at+Vector3.down*(floor?bounds.size.y:0); holder.rotation=Quaternion.Euler(0,turn?90:0,0);
                _chapterScenery.Add(holder);
            }
            else
            {
                Destroy(holder.gameObject);
                var shape=ChapterShape(PrimitiveType.Cube,label,at+Vector3.up*(floor?-.08f:1.5f),
                    floor?new Vector3(4,.16f,4):new Vector3(4,3,.3f),new Color(.27f,.29f,.31f),floor?"AshStoneFloor":"AshStoneWall");
                shape.rotation=Quaternion.Euler(0,turn?90:0,0);
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
