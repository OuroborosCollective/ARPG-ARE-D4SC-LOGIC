using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshenCrypt.MemoryForge.Integration
{
    [Serializable] public sealed class ForgeRegistryEntry
    { public ForgeControllerAsset Controller; public bool ForNewSpawns; }
    [CreateAssetMenu(menuName="Memory Forge/Controller registry",fileName="ForgeRegistry")]
    public sealed class ForgeRegistryAsset : ScriptableObject
    {
        public List<ForgeRegistryEntry> Entries=new List<ForgeRegistryEntry>();
        /// <summary>Build once per host session, share artifacts, keep all historical entries for restore.</summary>
        public NpcArchetypeControllerRegistry Build()
        {
            var registry=new NpcArchetypeControllerRegistry();var selected=new HashSet<string>(StringComparer.Ordinal);
            foreach(var entry in Entries)
            {
                if(entry==null||entry.Controller==null)throw new InvalidOperationException("Registry entry has no controller");
                if(!entry.Controller.TryGetApproved(out var artifact,out var receipt,out var message))throw new InvalidOperationException(message);
                if(entry.ForNewSpawns&&!selected.Add(artifact.ArchetypeId))throw new InvalidOperationException("Choose exactly one new-spawn controller per archetype");
                registry.Register(artifact,entry.Controller.ReviewedHash,receipt,entry.ForNewSpawns);
            }
            if(!selected.Contains("fallback"))throw new InvalidOperationException("An admitted fallback is required");
            return registry;
        }
    }
}
