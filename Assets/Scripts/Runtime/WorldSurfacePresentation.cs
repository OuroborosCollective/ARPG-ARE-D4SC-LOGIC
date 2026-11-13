using System;
using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>Optional private materials for named scenery; never actors, UI or gameplay indicators.</summary>
    public static class WorldSurfacePresentation
    {
        /// <summary>Resolve only the explicitly reviewed primitive scenery surfaces.</summary>
        public static string KeyFor(string label)
        {
            switch (label)
            {
                case "AshRoad_Ground": case "OldMill_Ground": return "OutdoorGround";
                case "OldStore_Ground": case "HiddenCrypt_Ground": return "CryptSubfloor";
                case "OldMill_Wheel": return "MillWood";
                case "AshRoad_Trail_Marker": return "WeatheredPost";
                case "Grass verge": return "GrassVerge";
                case "Mill wooden board": return "MillBoard";
                case "Mill shallow puddle": return "MillWater";
                case "OldStore_SealedGate": return "StoreDoor";
                case "HiddenCrypt_InnerDoor": return "CryptDoor";
                case "Ember_Rift_Ground": return "BasaltGround";
                case "Ember_Rift_Causeway": return "BasaltRoad";
                case "Ember_Rift_Boss_Dais": case "Ember_Rift_Gate":
                case "Ember_Rift_Gate_Left": case "Ember_Rift_Gate_Right": return "BasaltStructure";
            }
            if (label != null && label.StartsWith("Ember_Rift_Fissure_", StringComparison.Ordinal))
                return "LavaFissure";
            if (label != null && label.StartsWith("OldMill_Bed_", StringComparison.Ordinal))
                return "BedWood";
            if (label != null && label.StartsWith("Ember_Rift_Basalt_", StringComparison.Ordinal))
                return "BasaltStructure";
            return null;
        }

        /// <summary>Use a persistent imported material if present; a missing private overlay preserves existing art.</summary>
        public static bool TryApply(Renderer renderer, string label)
        {
            string key = KeyFor(label);
            if (renderer == null || !renderer.enabled || key == null) return false;
            Material material = Resources.Load<Material>("ARE/Visuals/WorldSurfaces/" + key);
            if (material == null) return false;
            renderer.sharedMaterial = material;
            return true;
        }
    }
}
