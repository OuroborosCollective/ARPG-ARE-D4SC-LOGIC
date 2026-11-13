using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AshenCrypt.MemoryForge
{
    public sealed class ControllerMigration
    {
        public ulong Sequence { get; }
        public string NpcId { get; }
        public ulong Tick { get; }
        public ControllerArtifact From { get; }
        public ControllerArtifact To { get; }
        public string FromHash=>From.Hash;
        public string ToHash=>To.Hash;
        public string RegressionHash { get; }
        public string Checksum { get; }
        internal ControllerMigration(ulong sequence,string id,ulong tick,ControllerArtifact from,ControllerArtifact to,string regression)
        {
            Sequence=sequence;NpcId=ContractText.Id(id);Tick=tick;From=from;To=to;RegressionHash=regression;
            Checksum=ContractText.Hash("AshenCrypt.ControllerMigration.v1|"+ContractText.Number(sequence)+"|"+id+"|"+ContractText.Number(tick)+"|"+from.Hash+"|"+to.Hash+"|"+regression);
        }
    }
    public sealed partial class MemoryArchive
    {
        private readonly List<ControllerMigration> _migrations=new List<ControllerMigration>();
        public IReadOnlyList<ControllerMigration> Migrations=>_migrations.AsReadOnly();
        /// <summary>Explicit host command. Only an admitted exact target with the same archetype contract is eligible.</summary>
        public bool TryMigrate(string id,ControllerArtifact target,NpcArchetypeControllerRegistry registry,ulong tick,out ControllerMigration receipt)
        {
            receipt=null;var mind=Get(id);
            if(mind==null||target==null||registry==null||mind.Controller.Hash==target.Hash||mind.Controller.Archetype.Canonical!=target.Archetype.Canonical||
                !registry.TryRestore(target.ArchetypeId,target.ControllerId,target.Version,target.Hash,out var approved)||
                !registry.TryRestore(mind.Controller.ArchetypeId,mind.Controller.ControllerId,mind.Controller.Version,mind.Controller.Hash,out _)||
                tick<mind.DecisionState.LastTick||_events.Count>0&&tick<_events.Last().Tick||_acquisitions.Any(k=>k.LearnedTick>tick)||_migrations.Count>0&&tick<_migrations.Last().Tick)return false;
            receipt=new ControllerMigration(checked((ulong)_migrations.Count+1),id,tick,mind.Controller,approved,registry.Admission(approved).EvidenceHash);
            _migrations.Add(receipt);mind.MigrateTo(approved,tick);return true;
        }
        private void WriteMigrations(BinaryWriter writer)
        {
            writer.Write(_migrations.Count);
            foreach(var m in _migrations)
            {
                writer.Write(m.Sequence);Write(writer,m.NpcId);writer.Write(m.Tick);
                foreach(var controller in new[]{m.From,m.To})
                {Write(writer,controller.ArchetypeId);Write(writer,controller.ControllerId);writer.Write(controller.Version);Write(writer,controller.Hash);}
                Write(writer,m.RegressionHash);Write(writer,m.Checksum);
            }
        }
        private bool ReadMigrations(BinaryReader reader,NpcArchetypeControllerRegistry registry)
        {
            int count=Count(reader);if(count==0)return false;
            var last=new Dictionary<string,ControllerArtifact>(StringComparer.Ordinal);
            for(int i=0;i<count;i++)
            {
                ulong sequence=reader.ReadUInt64();string id=Read(reader);ulong tick=reader.ReadUInt64();
                var bindings=new ControllerArtifact[2];
                for(int j=0;j<2;j++)if(!registry.TryRestore(Read(reader),Read(reader),reader.ReadInt32(),Read(reader),out bindings[j]))return false;
                string regression=Read(reader),checksum=Read(reader);var from=bindings[0];var to=bindings[1];var mind=Get(id);
                if(sequence!=(ulong)i+1||mind==null||from.Hash==to.Hash||from.Archetype.Canonical!=to.Archetype.Canonical||regression!=registry.Admission(to).EvidenceHash||
                    _migrations.Count>0&&tick<_migrations.Last().Tick||last.TryGetValue(id,out var prior)&&prior.Hash!=from.Hash)return false;
                var receipt=new ControllerMigration(sequence,id,tick,from,to,regression);if(checksum!=receipt.Checksum)return false;
                _migrations.Add(receipt);last[id]=to;
            }
            foreach(var entry in last)
            {
                var mind=Get(entry.Key);var latest=_migrations.Last(m=>m.NpcId==entry.Key);
                if(mind.Controller.Hash!=entry.Value.Hash||mind.DecisionState.LastDecision!=null&&mind.DecisionState.LastTick<latest.Tick)return false;
                if(mind.DecisionState.LastDecision==null)mind.DecisionState.LastTick=latest.Tick;
            }
            return true;
        }
    }
}
