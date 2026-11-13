using System;
using System.IO;
using System.Linq;
using System.Text;

namespace AshenCrypt.MemoryForge
{
    public sealed partial class MemoryArchive
    {
        /// <summary>Canonical, checksummed archive. The host embeds this token into its existing save owner.</summary>
        public string Save()
        {
            using(var stream=new MemoryStream())
            {
                using(var w=new BinaryWriter(stream,Encoding.UTF8,true))
                {
                    w.Write(1);w.Write(_events.Count);
                    foreach(var e in _events)
                    {
                        w.Write(e.Id);w.Write((byte)e.Kind);Write(w,e.Actor);Write(w,e.Target);w.Write(e.Tick);w.Write(e.Importance);w.Write(e.Essential);Write(w,e.Context);
                        for(int i=0;i<8;i++)w.Write(e.Impact[(RelationshipAxis)i]);Write(w,e.Checksum);
                    }
                    w.Write(_minds.Count);
                    foreach(var entry in _minds)
                    {
                        var m=entry.Value;Write(w,m.Id);Write(w,m.Controller.ArchetypeId);Write(w,m.Controller.ControllerId);w.Write(m.Controller.Version);Write(w,m.Controller.Hash);
                        // Decision continuity is completed in the gameplay adapter integration, before enabling this save in production.
                        if(m.DecisionState.LastDecision!=null)throw new InvalidOperationException("Decision-state persistence is not yet enabled");
                    }
                    w.Write(_acquisitions.Count);
                    foreach(var k in _acquisitions)
                    {
                        w.Write(k.Sequence);w.Write(k.Event.Id);Write(w,k.Knower);w.Write((byte)k.Origin);Write(w,k.Source);w.Write(k.SourceSequence);w.Write(k.LearnedTick);w.Write(k.PersonalImpact);w.Write(k.SocialCloseness);Write(w,k.Checksum);
                    }
                }
                string payload=Convert.ToBase64String(stream.ToArray());return "NMF1."+payload+"."+ContractText.Hash(payload);
            }
        }
        private static void Write(BinaryWriter writer,string value)
        {var bytes=Encoding.UTF8.GetBytes(value);writer.Write(bytes.Length);writer.Write(bytes);}
        private static string Read(BinaryReader reader)
        {
            int length=reader.ReadInt32();if(length<0||length>4096||length>reader.BaseStream.Length-reader.BaseStream.Position)throw new InvalidDataException("Invalid string length");
            return new UTF8Encoding(false,true).GetString(reader.ReadBytes(length));
        }
        private static int Count(BinaryReader reader)
        {int n=reader.ReadInt32();if(n<0||n>reader.BaseStream.Length-reader.BaseStream.Position)throw new InvalidDataException("Invalid record count");return n;}
        public static bool TryLoad(string token,NpcArchetypeControllerRegistry registry,out MemoryArchive archive,int maximumBytes=16*1024*1024)
        {
            archive=null;if(string.IsNullOrEmpty(token)||maximumBytes<1||token.Length>(long)maximumBytes*2)return false;
            var fields=token.Split('.');if(fields.Length!=3||fields[0]!="NMF1"||fields[2]!=ContractText.Hash(fields[1]))return false;
            try
            {
                var bytes=Convert.FromBase64String(fields[1]);if(bytes.Length>maximumBytes||Convert.ToBase64String(bytes)!=fields[1])return false;
                using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,new UTF8Encoding(false,true)))
                {
                    if(reader.ReadInt32()!=1)return false;var a=new MemoryArchive();int events=Count(reader);
                    for(int i=0;i<events;i++)
                    {
                        ulong id=reader.ReadUInt64();var kind=(MemoryKind)reader.ReadByte();string actor=Read(reader),target=Read(reader);ulong tick=reader.ReadUInt64();int importance=reader.ReadInt32();byte essential=reader.ReadByte();if(essential>1)return false;string context=Read(reader);
                        var impact=new int[8];for(int j=0;j<8;j++)impact[j]=reader.ReadInt32();string hash=Read(reader);
                        var e=a.Append(kind,actor,target,tick,importance,essential==1,context,new RelationshipChange(impact[0],impact[1],impact[2],impact[3],impact[4],impact[5],impact[6],impact[7]));
                        if(e.Id!=id||e.Checksum!=hash)return false;
                    }
                    int minds=Count(reader);string previous=null;
                    for(int i=0;i<minds;i++)
                    {
                        string id=Read(reader),archetype=Read(reader),controllerId=Read(reader);int version=reader.ReadInt32();string hash=Read(reader);
                        if(previous!=null&&StringComparer.Ordinal.Compare(previous,id)>=0||!registry.TryRestore(archetype,controllerId,version,hash,out var controller))return false;
                        a._minds.Add(id,new NpcMind(id,controller));previous=id;
                    }
                    int acquisitions=Count(reader);
                    for(int i=0;i<acquisitions;i++)
                    {
                        ulong sequence=reader.ReadUInt64(),eventId=reader.ReadUInt64();string knower=Read(reader);var origin=(KnowledgeOrigin)reader.ReadByte();string source=Read(reader);ulong sourceSequence=reader.ReadUInt64(),tick=reader.ReadUInt64();int personal=reader.ReadInt32(),social=reader.ReadInt32();string hash=Read(reader);
                        bool accepted=origin==KnowledgeOrigin.Reported?a.Report(source,knower,eventId,tick,personal,social):a.Learn(knower,eventId,origin,source,tick,personal,social);
                        if(!accepted)return false;var known=a._acquisitions.Last();if(known.Sequence!=sequence||known.SourceSequence!=sourceSequence||known.Checksum!=hash)return false;
                    }
                    if(stream.Position!=stream.Length||a.Save()!=token)return false;archive=a;return true;
                }
            }
            catch(Exception e) when(e is ArgumentException||e is IOException||e is OverflowException||e is FormatException||e is InvalidOperationException){return false;}
        }
    }
}
