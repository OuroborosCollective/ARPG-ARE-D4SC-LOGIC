using System;
using System.Globalization;
using System.Linq;
using System.Text;
using AshenCrypt.MemoryForge;

namespace AreArpg.Core
{
    /// <summary>Owned by the existing SocialLedger. No independent clock or world simulation.</summary>
    public sealed class NpcForgeState
    {
        internal static readonly NpcArchetypeControllerRegistry Registry=ForgeDefaults.CreateRegistry();
        public MemoryArchive Archive { get; private set; }
        public ulong ProcessedTicks { get; internal set; }
        public static System.Collections.Generic.IReadOnlyList<ControllerArtifact> ControllerCatalog => Registry.Versions;
        public static ControllerAdmission Regression(ControllerArtifact controller) => Registry.Admission(controller);
        internal string Checkpoint="-";
        internal Func<string> Capture;
        public static string Identity(int id) => id==0?"ilyan":id==1?"mara":id==2?"dorn":throw new ArgumentOutOfRangeException(nameof(id));
        internal NpcForgeState()
        {
            Archive=new MemoryArchive();
            Archive.Spawn("ilyan","traveler",Registry);Archive.Spawn("mara","merchant",Registry);Archive.Spawn("dorn","guard",Registry);
        }
        internal NpcMind Ensure(string id,string archetype) => Archive.Get(id)??Archive.Spawn(id,archetype,Registry);
        internal void Record(int id,NpcAction action,bool victimAware,int[] witnesses,string context)
        {
            var kind=action==NpcAction.Help?MemoryKind.Help:action==NpcAction.Greet?MemoryKind.Greeting:action==NpcAction.Attack?MemoryKind.Assault:MemoryKind.Theft;
            var impact=action==NpcAction.Help?new RelationshipChange(trust:1200,gratitude:2000,familiarity:500):
                action==NpcAction.Greet?new RelationshipChange(familiarity:50):new RelationshipChange(trust:-2000,fear:1500,mistrust:2000,hostility:2500,familiarity:500);
            var ev=Archive.Append(kind,"player",Identity(id),ProcessedTicks,action==NpcAction.Greet?100:action==NpcAction.Help?5000:8000,false,context,impact);
            if(victimAware)Archive.Learn(Identity(id),ev.Id,KnowledgeOrigin.Self,Identity(id),ProcessedTicks);
            foreach(int witness in witnesses)if(witness!=id)Archive.Learn(Identity(witness),ev.Id,KnowledgeOrigin.Witnessed,Identity(witness),ProcessedTicks,6000);
        }
        internal string Token() => "1:"+ProcessedTicks.ToString(CultureInfo.InvariantCulture)+":"+Archive.Save()+":"+(Capture?.Invoke()??Checkpoint);
        internal static bool TryRead(string token,out NpcForgeState state)
        {
            state=null;var f=token.Split(':');
            if(f.Length!=4||f[0]!="1"||!ulong.TryParse(f[1],NumberStyles.None,CultureInfo.InvariantCulture,out var ticks)||ticks.ToString(CultureInfo.InvariantCulture)!=f[1]||
                !MemoryArchive.TryLoad(f[2],Registry,out var archive))return false;
            if(archive.Minds.Count<3||archive.Minds.Count>5||archive.Minds.Any(m=>m.Id!="ilyan"&&m.Id!="mara"&&m.Id!="dorn"&&m.Id!="hunter"&&m.Id!="road.traveler")||
                archive.Get("road.traveler")!=null&&archive.Get("road.traveler").Controller.ArchetypeId!="traveler"||
                archive.Get("hunter")!=null&&archive.Get("hunter").Controller.ArchetypeId!="companion"||archive.Get("ilyan")?.Controller.ArchetypeId!="traveler"||archive.Get("mara")?.Controller.ArchetypeId!="merchant"||archive.Get("dorn")?.Controller.ArchetypeId!="guard"||
                archive.Acquisitions.Any(k=>k.LearnedTick>ticks)||archive.Events.Any(e=>e.Tick>ticks)||archive.Migrations.Any(m=>m.Tick>ticks)||archive.Minds.Any(m=>m.DecisionState.LastTick>ticks))return false;
            if(!ForgeVillageCheckpoint.TryRead(f[3],out _))return false;
            state=new NpcForgeState{ProcessedTicks=ticks,Archive=archive,Checkpoint=f[3]};return true;
        }
    }
    public sealed partial class SocialLedger
    {
        public NpcForgeState Forge { get; internal set; }
        /// <summary>Explicit feature migration; the old BigInteger social accounts remain intact.</summary>
        public void EnableForge(){if(Forge==null)Forge=new NpcForgeState();}
    }
    public sealed partial class ArenaSimulation
    {
        public bool TryMigrateNpcController(string identity,string controllerId,int version,string hash)
        {
            var forge=Progression.Social.Forge;var mind=forge?.Archive.Get(identity);
            return !GameOver&&mind!=null&&NpcForgeState.Registry.TryRestore(mind.Controller.ArchetypeId,controllerId,version,hash,out var target)&&forge.Archive.TryMigrate(identity,target,NpcForgeState.Registry,forge.ProcessedTicks,out _);
        }
    }
    public sealed partial class CharacterProgression
    {
        private string ForgePayload()
        {
            string body=EquipmentPayload();if(Social.Forge==null)return body;
            string legacy=body+"|"+HashText(body).ToString("X16",CultureInfo.InvariantCulture);
            return "AC17|"+Convert.ToBase64String(Encoding.UTF8.GetBytes(legacy))+"|"+Convert.ToBase64String(Encoding.UTF8.GetBytes(Social.Forge.Token()));
        }
        private static bool TryLoadForge(string saved,out CharacterProgression progress)
        {
            progress=null;if(saved.Length>48*1024*1024)return false;
            var f=saved.Split('|');if(f.Length!=4||f[3]!=HashText(string.Join("|",f,0,3)).ToString("X16",CultureInfo.InvariantCulture))return false;
            try
            {
                var innerBytes=Convert.FromBase64String(f[1]);var forgeBytes=Convert.FromBase64String(f[2]);
                if(Convert.ToBase64String(innerBytes)!=f[1]||Convert.ToBase64String(forgeBytes)!=f[2])return false;
                var utf8=new UTF8Encoding(false,true);string inner=utf8.GetString(innerBytes);
                if(inner.StartsWith("AC17|",StringComparison.Ordinal)||inner.StartsWith("AC18|",StringComparison.Ordinal)||!TryLoad(inner,out var p)||!NpcForgeState.TryRead(utf8.GetString(forgeBytes),out var forge))return false;
                if(!ForgeVillageCheckpoint.TryRead(forge.Checkpoint,out var checkpoint)||checkpoint!=null&&checkpoint.Health>CombatRules.MaximumHealth(p))return false;
                p.Social.Forge=forge;progress=p;return true;
            }
            catch(Exception e) when(e is FormatException||e is ArgumentException||e is OverflowException){return false;}
        }
    }
}
