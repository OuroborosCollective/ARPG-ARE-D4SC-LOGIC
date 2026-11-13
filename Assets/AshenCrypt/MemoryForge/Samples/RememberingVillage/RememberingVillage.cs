using System;
using System.Collections.Generic;
using System.Linq;
using AshenCrypt.MemoryForge.Integration;
using UnityEngine;

namespace AshenCrypt.MemoryForge.Samples
{
    /// <summary>Original standalone sample projection. The sample host supplies exactly 100-ms steps.</summary>
    public sealed class RememberingVillage : MonoBehaviour
    {
        public const string SaveKey="AshenCrypt.MemoryForge.Sample.v1";
        public DemoWorld World { get; private set; }
        public bool Running=true;
        public Shader BuiltinLitShader,BuiltinUnlitShader,UniversalLitShader,UniversalUnlitShader;
        public Camera SampleCamera { get; private set; }
        private Transform _player,_nara,_guard;
        private TextMesh _naraLabel,_guardLabel;
        private LineRenderer _sight;
        private readonly List<Material> _materials=new List<Material>();
        private float _accumulator;
        private int _selected,_tab;
        private string _note="Approach Nara, give her food, then leave and return. Her next greeting will remember your help.";
        private GUIStyle _title,_label,_muted,_small,_button,_heading;
        private static readonly Color Ink=new Color(.042f,.068f,.09f),Panel=new Color(.073f,.11f,.14f),Cream=new Color(.93f,.9f,.82f),Muted=new Color(.57f,.65f,.67f),Copper=new Color(.91f,.62f,.35f),Teal=new Color(.35f,.79f,.72f);
        private void Awake()
        {
            World=new DemoWorld();
            var cameraObject=new GameObject("Memory Forge Demo Camera",typeof(Camera));cameraObject.transform.SetParent(transform,false);SampleCamera=cameraObject.GetComponent<Camera>();cameraObject.tag="MainCamera";
            cameraObject.transform.position=new Vector3(8,12,-13);cameraObject.transform.LookAt(new Vector3(0,.4f,0));SampleCamera.orthographic=true;SampleCamera.orthographicSize=8;SampleCamera.clearFlags=CameraClearFlags.SolidColor;SampleCamera.backgroundColor=Ink;SampleCamera.rect=new Rect(.015f,.28f,.57f,.56f);
            var lightObject=new GameObject("Example Key Light",typeof(Light));lightObject.transform.SetParent(transform,false);var light=lightObject.GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.color=new Color(1,.88f,.72f);lightObject.transform.rotation=Quaternion.Euler(48,-35,0);
            Shape(PrimitiveType.Cube,"Courtyard",new Vector3(0,-.14f,0),new Vector3(14,.2f,12),new Color(.075f,.13f,.15f));
            for(int side=-1;side<=1;side+=2)for(int z=-5;z<=5;z++)Shape(PrimitiveType.Cube,"Paving",new Vector3(side*2.2f,-.02f,z),new Vector3(1.6f,.08f,.88f),new Color(.13f,.22f,.24f));
            Shape(PrimitiveType.Cube,"Canonical Sight Wall",new Vector3(0,1.1f,1.6f),new Vector3(.7f,2.2f,3.4f),new Color(.24f,.29f,.31f));
            for(int z=0;z<=3;z++)Shape(PrimitiveType.Cube,"Wall Cap",new Vector3(0,2.23f,z+.1f),new Vector3(.9f,.14f,.78f),new Color(.4f,.37f,.3f));
            _player=Pawn("Player",Cream);_nara=Pawn("Nara",Teal);_guard=Pawn("Watch",Copper);
            _nara.gameObject.AddComponent<ForgeNpcView>();_guard.gameObject.AddComponent<ForgeNpcView>();
            _naraLabel=Label(_nara,"NARA");_guardLabel=Label(_guard,"WATCH");
            var sightObject=new GameObject("Actual Sight Query",typeof(LineRenderer));sightObject.transform.SetParent(transform,false);_sight=sightObject.GetComponent<LineRenderer>();_sight.positionCount=2;_sight.startWidth=_sight.endWidth=.035f;_sight.sharedMaterial=Material(Color.white,true);
            BindViews();Project();
        }
        private Material Material(Color color,bool unlit=false)
        {
            bool universal=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline!=null&&UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.GetType().Name.Contains("Universal");
            var shader=universal?(unlit?UniversalUnlitShader:UniversalLitShader):(unlit?BuiltinUnlitShader:BuiltinLitShader);
            if(shader==null)shader=Shader.Find(universal?(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit"):(unlit?"Unlit/Color":"Standard"));
            if(shader==null)throw new InvalidOperationException("The example requires its serialized Built-in or URP shader references. Open the included scene or configure these four fields on a custom sample component.");
            var material=new Material(shader){color=color};_materials.Add(material);return material;
        }
        private Transform Shape(PrimitiveType type,string title,Vector3 position,Vector3 scale,Color color,Transform parent=null)
        {
            var go=GameObject.CreatePrimitive(type);go.name=title;go.transform.SetParent(parent??transform,false);go.transform.localPosition=position;go.transform.localScale=scale;
            var collider=go.GetComponent<Collider>();if(collider!=null){collider.enabled=false;Destroy(collider);}go.GetComponent<Renderer>().sharedMaterial=Material(color);return go.transform;
        }
        private Transform Pawn(string name,Color color)
        {
            var root=new GameObject(name).transform;root.SetParent(transform,false);
            Shape(PrimitiveType.Cylinder,"Identity Ring",new Vector3(0,.03f,0),new Vector3(1,.03f,1),color,root);
            Shape(PrimitiveType.Capsule,"Coat",new Vector3(0,.8f,0),new Vector3(.58f,.65f,.52f),color,root);
            Shape(PrimitiveType.Sphere,"Head",new Vector3(0,1.6f,0),Vector3.one*.42f,new Color(.82f,.72f,.58f),root);
            Shape(PrimitiveType.Cube,"Facing",new Vector3(0,1.59f,.22f),new Vector3(.17f,.09f,.1f),new Color(.18f,.23f,.25f),root);
            return root;
        }
        private TextMesh Label(Transform parent,string text)
        {
            var go=new GameObject("Name",typeof(TextMesh));go.transform.SetParent(parent,false);go.transform.localPosition=new Vector3(0,2.1f,0);
            var label=go.GetComponent<TextMesh>();label.text=text;label.characterSize=.065f;label.fontSize=40;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=Cream;return label;
        }
        private void BindViews(){_nara.GetComponent<ForgeNpcView>().Bind(World.Archive.Get("nara"));_guard.GetComponent<ForgeNpcView>().Bind(World.Archive.Get("watch"));}
        private void Update()
        {
            if(Running)_accumulator+=Mathf.Min(Time.deltaTime,.25f);
            int count=0;while(_accumulator>=.1f&&count++<3){_accumulator-=.1f;World.Step();}
            Project();
        }
        public void Project()
        {
            _player.position=new Vector3(World.PlayerX/1000f,0,World.PlayerZ/1000f);_nara.position=new Vector3(World.NaraX/1000f,0,World.NaraZ/1000f);_guard.position=new Vector3(World.GuardX/1000f,0,World.GuardZ/1000f);
            foreach(var actor in new[]{_nara,_guard}){var d=_player.position-actor.position;if(d.sqrMagnitude>.01f)actor.rotation=Quaternion.LookRotation(d);}
            foreach(var label in new[]{_naraLabel,_guardLabel})label.transform.rotation=SampleCamera.transform.rotation;
            _sight.SetPosition(0,_guard.position+Vector3.up*1.6f);_sight.SetPosition(1,_player.position+Vector3.up*1.6f);
            _sight.startColor=_sight.endColor=World.GuardCanSeePlayer?Teal:new Color(.67f,.27f,.22f,.7f);
        }
        public void Execute(string command)
        {
            switch(command)
            {
                case "approach":World.Approach();_note="Walking to Nara. Contact and visibility are checked by the sample host.";break;
                case "leave":World.Leave();_note="Leaving the courtyard. Nara keeps her own history.";break;
                case "food":_note=World.GiveFood()?"One real ration given. Nara remembers your help; trust and gratitude increased.":"Come within reach. Nara must need food and you must have a ration.";break;
                case "greet":_note=World.Greet();break;
                case "witness":World.MoveWitnessToCourtyard();_note="The watch walks around the wall. The sight line shows what can actually be witnessed.";break;
                case "rob":_note=World.Rob()?"The purse changed hands once. Only a witness with a clear sight line learned about the theft.":"Come within reach. An empty purse cannot be taken twice.";break;
                case "save":PlayerPrefs.SetString(SaveKey,World.Save());PlayerPrefs.Save();_note="Saved: host state, individual memories, exact controller versions and decision continuity.";break;
                case "load":if(DemoWorld.TryLoad(PlayerPrefs.GetString(SaveKey,""),out var loaded)){World=loaded;BindViews();_accumulator=0;_note="Restored the actual saved state. No relationship or reward was applied twice.";}else _note="No valid sample save. The current world has been kept.";break;
                case "reset":World=new DemoWorld();BindViews();_accumulator=0;_note="Fresh scenario. Your saved snapshot is still available through Load.";break;
                case "step":World.Step();break;
                case "pause":Running=!Running;break;
            }
            Project();
        }
        private void Styles()
        {
            if(_title!=null)return;
            _title=new GUIStyle(GUI.skin.label){fontSize=38,fontStyle=FontStyle.Bold,normal={textColor=Cream}};
            _heading=new GUIStyle(GUI.skin.label){fontSize=22,fontStyle=FontStyle.Bold,normal={textColor=Cream}};
            _label=new GUIStyle(GUI.skin.label){fontSize=16,wordWrap=true,richText=true,normal={textColor=Cream}};
            _muted=new GUIStyle(_label){normal={textColor=Muted}};_small=new GUIStyle(_muted){fontSize=12};
            _button=new GUIStyle(GUI.skin.button){fontSize=14,fontStyle=FontStyle.Bold,padding=new RectOffset(10,10,8,8)};
        }
        private static void Fill(Rect rect,Color color){var old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;}
        private void Button(Rect rect,string label,string command,bool enabled=true)
        {bool was=GUI.enabled;GUI.enabled=enabled;if(GUI.Button(rect,label,_button))Execute(command);GUI.enabled=was;}
        private void OnGUI()
        {
            Styles();var old=GUI.matrix;GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1280f,Screen.height/720f,1));
            Fill(new Rect(0,0,1280,108),Ink);Fill(new Rect(24,25,5,63),Copper);
            GUI.Label(new Rect(43,17,730,52),"MEMORY FORGE",_title);
            GUI.Label(new Rect(45,72,700,24),"ACTIONS LEAVE MEMORIES.   •   LOCAL, DETERMINISTIC NPC AI",_small);
            GUI.Label(new Rect(1010,29,245,25),"UNITY 6  /  LIVE EXAMPLE",_small);
            GUI.Label(new Rect(1010,55,245,28),"TICK "+World.Tick+"   ·   "+(Running?"RUNNING":"PAUSED"),_label);
            GUI.Label(new Rect(24,115,700,24),"01   THE COURTYARD",_small);
            GUI.Label(new Rect(24,484,710,30),"RATIONS  "+World.Provisions+"      COINS  "+World.Coins+"      HEALTH  "+World.PlayerHealth+"      WITNESS  "+(World.GuardCanSeePlayer?"CLEAR SIGHT":"OCCLUDED"),_small);
            float x=24;string[] names={"APPROACH NARA","GIVE FOOD","GREET","LEAVE"},commands={"approach","food","greet","leave"};
            for(int i=0;i<4;i++)Button(new Rect(x+i*182,526,174,38),names[i],commands[i],World.PlayerHealth>0);
            Button(new Rect(24,572,356,36),"MOVE WATCH INTO VIEW","witness");Button(new Rect(388,572,174,36),"TAKE THE PURSE","rob",World.PlayerHealth>0);Button(new Rect(570,572,174,36),Running?"PAUSE":"RESUME","pause");
            GUI.Label(new Rect(24,615,720,32),"Try the theft before and after the watch moves. The wall changes who knows.",_small);
            string[] footer={"SAVE","LOAD","RESET SCENARIO","STEP 100 MS"},actions={"save","load","reset","step"};for(int i=0;i<4;i++)Button(new Rect(24+i*182,661,174,34),footer[i],actions[i]);
            Fill(new Rect(770,118,486,578),Panel);Fill(new Rect(770,118,3,578),Teal);
            if(GUI.Button(new Rect(791,135,213,34),"NARA  /  TRAVELER",_button))_selected=0;
            if(GUI.Button(new Rect(1014,135,220,34),"WATCH  /  GUARD",_button))_selected=1;
            var mind=World.Archive.Get(_selected==0?"nara":"watch");var relationship=mind.RelationshipTo("player");var decision=mind.DecisionState.LastDecision;
            GUI.Label(new Rect(793,181,440,33),decision==null?"Waiting for first tick":decision.Action.ToString().ToUpperInvariant(),_heading);
            GUI.Label(new Rect(793,215,440,42),"Trust "+relationship.Trust+"  ·  Gratitude "+relationship.Gratitude+"\nHostility "+relationship.Hostility+"  ·  Fear "+relationship.Fear,_label);
            GUI.Label(new Rect(793,266,440,20),mind.Controller.ControllerId+" / v"+mind.Controller.Version+" / "+mind.Controller.Hash.Substring(0,12),_small);
            if(GUI.Button(new Rect(793,299,215,30),"PERSONAL MEMORY",_button))_tab=0;
            if(GUI.Button(new Rect(1018,299,215,30),"WHY THIS ACTION?",_button))_tab=1;
            float y=344;
            if(_tab==0)
            {
                if(mind.Known.Count==0)GUI.Label(new Rect(793,y,440,90),"No personal events yet.\nThis NPC does not automatically know what happened to someone else.",_muted);
                foreach(var memory in mind.Known.Reverse().Take(3))
                {GUI.Label(new Rect(793,y,440,24),"#"+memory.Event.Id+"   "+memory.Event.Kind+"  ·  "+memory.Event.Context,_label);GUI.Label(new Rect(793,y+25,440,40),memory.Origin+" · source "+memory.Source+" · tick "+memory.Event.Tick+"\n"+(memory.IsFact?"Direct knowledge":"Unconfirmed information"),_small);y+=78;}
            }
            else if(decision!=null)
            {
                GUI.Label(new Rect(793,y,440,25),decision.Reason,_small);y+=30;
                foreach(var score in decision.Priorities.Where(p=>p.Legal).OrderByDescending(p=>p.Value).Take(5))
                {GUI.Label(new Rect(793,y,280,24),score.Action.ToString(),_label);GUI.Label(new Rect(1115,y,120,24),score.Value.ToString(),_label);y+=29;}
                GUI.Label(new Rect(793,y+5,440,45),"Hard world permissions run first. The NPC Inspector exposes the exact weighted contributions.",_small);
            }
            Fill(new Rect(792,591,442,1),new Color(.2f,.3f,.32f));GUI.Label(new Rect(793,605,440,76),_note,_label);
            GUI.matrix=old;
        }
        private void OnDestroy(){foreach(var material in _materials)if(material!=null)Destroy(material);}
    }
}
