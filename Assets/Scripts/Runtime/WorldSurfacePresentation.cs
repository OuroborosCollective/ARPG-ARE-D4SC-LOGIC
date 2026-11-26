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
                case "Aschengrenze_FallbackSoil": return "VillageAsh";
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

        public static Material AshSnow => Resources.Load<Material>("ARE/Borderland/AshSnow");

        // Only the installed village's ground receives its original tiling on the shared ash material.
        internal static void DressVillageSoil(MeshRenderer floor)
        {
            var ash = Resources.Load<Material>("ARE/Borderland/VillageAsh");
            if (floor == null || ash == null) return;
            var original = floor.sharedMaterial;
            if (original != null && original.HasProperty("_BaseMap") && original.GetTexture("_BaseMap") is Texture soil)
            {
                Vector2 scale = original.GetTextureScale("_BaseMap");
                Vector2 offset = original.GetTextureOffset("_BaseMap");
                var bindings = new MaterialPropertyBlock();
                floor.GetPropertyBlock(bindings);
                bindings.SetTexture("_VillageEarth", soil);
                bindings.SetVector("_VillageEarth_ST", new Vector4(scale.x, scale.y, offset.x, offset.y));
                floor.SetPropertyBlock(bindings);
            }
            floor.sharedMaterial = ash;
        }

        // Reuse the art director's private ash islands and vein painting if installed. The public
        // mobile material has its own seamless mask for checkouts without this optional overlay.
        internal static void BindAshPaint()
        {
            var source = Resources.Load<Material>("ARE/Visuals/WorldSurfaces/OutdoorGround");
            Texture colour = source != null && source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : null;
            Texture veins = source != null && source.HasProperty("_EmissionMap") ? source.GetTexture("_EmissionMap") : null;
            if (colour == null || veins == null || colour.name != "AshGround_Albedo" || veins.name != "AshGround_Emission")
            {
                ReleaseAshPaint();
                return;
            }
            Shader.SetGlobalTexture("_AREAshPainting", colour);
            Shader.SetGlobalTexture("_AREAshVeins", veins);
            Shader.EnableKeyword("ARE_BORDERLAND_PAINT");
        }

        internal static void ReleaseAshPaint()
        {
            Shader.DisableKeyword("ARE_BORDERLAND_PAINT");
            Shader.SetGlobalTexture("_AREAshPainting", null);
            Shader.SetGlobalTexture("_AREAshVeins", null);
        }

        /// <summary>Keep authored regional surfaces first, with the bundled slate for the Ember Rift blockout.</summary>
        public static bool TryApply(Renderer renderer, string label)
        {
            string key = KeyFor(label);
            if (renderer == null || !renderer.enabled || key == null) return false;
            Material material = key switch
            {
                "VillageAsh" => Resources.Load<Material>("ARE/Borderland/VillageAsh"),
                "OutdoorGround" or "GrassVerge" => AshSnow ?? Resources.Load<Material>("ARE/Visuals/WorldSurfaces/" + key),
                _ => Resources.Load<Material>("ARE/Visuals/WorldSurfaces/" + key)
            };
            if (material == null && (key is "BasaltGround" or "BasaltRoad" or "BasaltStructure"))
            {
                material = Resources.Load<Material>("ARE/Borderland/RoughSlate");
                if (material != null)
                {
                    // The quarry floor is coarse, the warm causeway cut finer, and the
                    // pillar faces are lighter. All three sample the same world-space rock texture.
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    Vector4 tone = key switch
                    {
                        "BasaltGround" => new Vector4(.22f, .26f, .31f, 1f),
                        "BasaltRoad" => new Vector4(.43f, .39f, .35f, 1f),
                        _ => new Vector4(.57f, .52f, .49f, 1f)
                    };
                    block.SetVector("_StoneTint", tone);
                    block.SetFloat("_Metres", key == "BasaltGround" ? 5.4f : key == "BasaltRoad" ? 1.65f : 2.8f);
                    renderer.SetPropertyBlock(block);
                }
            }
            if (material == null) return false;
            renderer.sharedMaterial = material;
            return true;
        }
    }
}
