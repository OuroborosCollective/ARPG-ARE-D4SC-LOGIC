using System.Collections.Generic;
using AreArpg.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    public sealed partial class ArenaRuntime
    {
        private Transform _freightCart, _freightOriginal, _freightBrake;
        private bool NearFreight(long x,long z)
        { long dx=x-_game.PlayerXmm,dz=z-_game.PlayerZmm;return dx*dx+dz*dz<=2400L*2400; }
        private HungerAction[] FreightActions()
        {
            var f=_game.Progression.Freight;
            if(_game.Progression.EntryAct==2 && f.EvidenceReady && !f.Reported)
            {
                var ilyan=_game.Npcs.Find(n=>n.Id==0);
                if(ilyan!=null && NearFreight(ilyan.Xmm,ilyan.Zmm))return new[]{HungerAction.ReportFreightEvidence};
            }
            if(!_game.InAschenzoll)return null;
            var actions=new List<HungerAction>();
            if((f.Clues&1)==0 && NearFreight(-5000,-12000))actions.Add(HungerAction.InspectFreightNotice);
            if((f.Clues&2)==0 && NearFreight(FreightState.CargoX,FreightState.StartZ))actions.Add(HungerAction.InspectFreightCage);
            if(NearFreight(-3000,-1000))
            {
                if((f.Clues&4)==0)actions.Add(HungerAction.ReadFreightManifest);
                else if(f.Clues==7 && f.Choice==FreightChoice.Undecided)
                {actions.Add(HungerAction.SecureFreight);actions.Add(HungerAction.ReleaseFreight);}
                else if(f.Choice==FreightChoice.Secured && !f.OriginalCollected)actions.Add(HungerAction.TakeFreightOriginal);
            }
            if(f.Choice==FreightChoice.Released && f.MotionTicks==FreightState.JourneyTicks && !f.ArrivalInspected && NearFreight(FreightState.CargoX,FreightState.EndZ))actions.Add(HungerAction.InspectFreightArrival);
            return actions.ToArray();
        }
        private void FreightSign(string name,string text,Vector3 at)
        {
            var post=ChapterShape(PrimitiveType.Cube,name+"_Post",at,new Vector3(.14f,2.4f,.14f),new Color(.3f,.23f,.16f));
            var label=new GameObject(name).AddComponent<TextMesh>();label.transform.position=at+new Vector3(0,1.1f,0);
            label.transform.SetParent(post,true);label.fontSize=40;label.characterSize=.022f;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;
            label.text=text;label.color=new Color(.9f,.84f,.69f);
        }
        private void BuildFreightScenery()
        {
            _chapterScenery.Add(OutdoorBackdrop.Create(10));
            var verge=ChapterShape(PrimitiveType.Cube,"Aschenzoll_ForestVerge",new Vector3(0,-.32f,0),new Vector3(57,.3f,57),new Color(.23f,.27f,.2f));
            FreightGroundMaterial(verge,"OutdoorGround",new Vector2(14,14));
            var vegetation=new GameObject("Aschenzoll_TreeLine").transform;_chapterScenery.Add(vegetation);
            var sceneryRng=new DeterministicRng(17010);
            for(int side=-1;side<=1;side+=2)for(int i=0;i<4;i++)
            {
                PlantWoodland(vegetation,i%2==0?"WoodlandTreeA":"WoodlandTreeB",side*(21+i%2*3),-17+i*12,6.5f+i%3,sceneryRng);
                PlantWoodland(vegetation,"WoodlandShrub",side*18,-13+i*10,1.1f,sceneryRng);
            }
            _freightCart=new GameObject("Aschenzoll_EmptyFreightCart").transform;_chapterScenery.Add(_freightCart);
            var wood=new Color(.34f,.24f,.15f);var iron=new Color(.25f,.28f,.3f);
            var platform=ChapterShape(PrimitiveType.Cube,"Freight_Platform",new Vector3(0,.25f,0),new Vector3(2,.5f,4),wood,"AshCrate");platform.SetParent(_freightCart,true);
            for(int side=-1;side<=1;side+=2)
            {
                for(int i=0;i<6;i++)
                {
                    var bar=ChapterShape(PrimitiveType.Cube,"Freight_CageBar_"+side+"_"+i,new Vector3(side*.9f,1.6f,-1.7f+i*.68f),new Vector3(.09f,2.2f,.09f),iron);bar.SetParent(_freightCart,true);
                    if(side==-1&&i==2)bar.localRotation=Quaternion.Euler(0,0,-17);
                }
                var rail=ChapterShape(PrimitiveType.Cube,"Freight_CageTop",new Vector3(side*.9f,2.7f,0),new Vector3(.12f,.12f,3.6f),iron);rail.SetParent(_freightCart,true);
                for(int end=-1;end<=1;end+=2)
                {
                    var wheel=ChapterShape(PrimitiveType.Cylinder,"Freight_Wheel",new Vector3(side*.95f,.3f,end*1.3f),new Vector3(.6f,.12f,.6f),iron);wheel.rotation=Quaternion.Euler(0,0,90);wheel.SetParent(_freightCart,true);
                }
            }
            for(int end=-1;end<=1;end+=2)
            {
                var beam=ChapterShape(PrimitiveType.Cube,"Freight_CageEnd",new Vector3(0,2.7f,end*1.7f),new Vector3(1.9f,.12f,.12f),iron);beam.SetParent(_freightCart,true);
                for(int i=-1;i<=1;i++)
                {var bar=ChapterShape(PrimitiveType.Cube,"Freight_EndBar",new Vector3(i*.55f,1.6f,end*1.7f),new Vector3(.08f,2.2f,.08f),iron);bar.SetParent(_freightCart,true);}
            }
            var plate=ChapterShape(PrimitiveType.Cube,"Freight_Shipment17_Plate",new Vector3(-.96f,1.25f,0),new Vector3(.03f,.45f,.7f),new Color(.73f,.66f,.48f));plate.SetParent(_freightCart,true);
            // The rails lie beyond the unchanged walk boundary: old station saves stay valid and the footpath stays open.
            ChapterShape(PrimitiveType.Cube,"Freight_TrackBed",new Vector3(15.5f,-.1f,5),new Vector3(2.6f,.2f,24),new Color(.3f,.3f,.28f),"AshStoneFloor");
            for(int side=-1;side<=1;side+=2)ChapterShape(PrimitiveType.Cube,"Freight_FixedRail",new Vector3(15.5f+side*.8f,.05f,5),new Vector3(.12f,.1f,24),iron);
            for(int i=0;i<12;i++)ChapterShape(PrimitiveType.Cube,"Freight_Sleeper",new Vector3(15.5f,0,-6+i*2),new Vector3(2.3f,.12f,.23f),wood);
            ChapterShape(PrimitiveType.Cube,"Aschenzoll_FreightDesk",new Vector3(-3,.7f,-1),new Vector3(1.6f,1.4f,1.1f),wood,"AshCrate");
            _freightOriginal=ChapterShape(PrimitiveType.Cube,"Freight_OriginalManifest",new Vector3(-3,1.43f,-1),new Vector3(.55f,.025f,.7f),new Color(.8f,.73f,.56f));
            _freightBrake=ChapterShape(PrimitiveType.Cube,"Freight_EmergencyBrake",new Vector3(-4,.8f,-1),new Vector3(.12f,1.2f,.12f),new Color(.6f,.27f,.15f));
            FreightSign("Freight_DeskSign","Frachtpult",new Vector3(-5,1.2f,-1));
            FreightSign("Freight_CageSign","Rückfracht 17",new Vector3(17.5f,1.2f,-4));
            FreightSign("Freight_UpperSign","Obere Rampe",new Vector3(17.5f,1.2f,16.5f));
            UpdateFreightPresentation();
        }
        private static void FreightGroundMaterial(Transform floor,string key,Vector2 tiling)
        {
            var surface=Resources.Load<Material>("ARE/Visuals/WorldSurfaces/"+key);
            if(surface==null)return;
            var material=new Material(surface);material.mainTextureScale=tiling;
            floor.GetComponent<Renderer>().sharedMaterial=material;
            floor.gameObject.AddComponent<WoodlandMeshOwner>().Material=material;
        }
        private void UpdateFreightPresentation()
        {
            if(!_game.InAschenzoll || _freightCart==null)return;
            var f=_game.Progression.Freight;
            _freightCart.position=new Vector3(FreightState.CargoX/1000f,0,f.CargoZ/1000f);
            if(_freightOriginal!=null)_freightOriginal.gameObject.SetActive(f.Choice!=FreightChoice.Released&&!f.OriginalCollected);
            if(_freightBrake!=null)_freightBrake.localRotation=Quaternion.Euler(0,0,f.Choice==FreightChoice.Secured?-55:0);
        }
    }
}
