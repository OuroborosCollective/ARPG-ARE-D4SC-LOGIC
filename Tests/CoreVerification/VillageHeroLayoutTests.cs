using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using AreArpg.Core;

internal static class VillageHeroLayoutTests
{
    // Tests the actual visual-authoring plan against the unchanged canonical Core footprints.
    // This does NOT assert that the Unity renderers were already imported or displayed.
    internal static void Run(Action<bool, string> require)
    {
        DirectoryInfo root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md")))
            root = root.Parent;
        require(root != null, "village-hero-project-root-present");
        string path = Path.Combine(root.FullName, "docs", "visual-pass-1", "village-hero-layout.json");
        require(File.Exists(path), "village-hero-plan-exists");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        var plan = document.RootElement;
        require(plan.GetProperty("schema").GetString() == "ashen-crypt.visual-pass-1-village-hero.v1",
            "village-hero-schema");
        require(plan.GetProperty("visualOnly").GetBoolean(), "village-hero-is-presentation-only");
        var placements = plan.GetProperty("placements");
        require(placements.GetArrayLength() == 8, "village-hero-eight-curated-placements");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        int portals = 0;
        foreach (var entry in placements.EnumerateArray())
        {
            string id = entry.GetProperty("id").GetString();
            string sourceKey = entry.GetProperty("sourceKey").GetString();
            require(ids.Add(id) && !string.IsNullOrWhiteSpace(id), "village-hero-id-" + id);
            require(sourceKey.StartsWith("hero.", StringComparison.Ordinal) &&
                    !sourceKey.Contains("/") && !sourceKey.Contains(".."),
                    "village-hero-private-catalog-key-" + id);
            keys.Add(sourceKey);
            long x = entry.GetProperty("xMm").GetInt64();
            long z = entry.GetProperty("zMm").GetInt64();
            long width = entry.GetProperty("maxWidthMm").GetInt64();
            long height = entry.GetProperty("maxHeightMm").GetInt64();
            long depth = entry.GetProperty("maxDepthMm").GetInt64();
            long hw = width / 2, hd = depth / 2;
            int yaw = entry.GetProperty("yawQuarterTurns").GetInt32();
            bool portal = entry.GetProperty("passageArch").GetBoolean();
            require(width > 0 && width <= 7000 && depth > 0 && depth <= 5500 &&
                    height > 0 && height <= 6500 && width % 2 == 0 && depth % 2 == 0 &&
                    yaw >= 0 && yaw <= 3 && Math.Abs(x) <= 35000 && Math.Abs(z) <= 35000,
                    "village-hero-bounded-millimetre-geometry-" + id);
            if (portal)
            {
                portals++;
                require(id == "north-gate" && x == 0 && z == 24000 &&
                        width == 6400 && depth == 2000 && yaw == 1, "village-hero-one-gateway-at-core-passage-and-front-orientation");
            }
            else
            {
                require(Math.Abs(x) - hw >= 3500 || z - hd >= 24000 || z + hd <= -24000,
                    "village-hero-central-path-remains-readable-" + id);
            }
            for (int i = 0; i < EntryWorldLayout.BuildingCount; i++)
            {
                var b = EntryWorldLayout.Building(i);
                // 500-mm visual buffer around canonical blocking footprints, no gameplay edits.
                require(Math.Abs(x - b.X) >= hw + b.HalfWidth + 500 ||
                        Math.Abs(z - b.Z) >= hd + b.HalfDepth + 500,
                        "village-hero-clear-of-core-footprint-" + id + "-" + b.Id);
            }
        }
        require(portals == 1 && keys.Count == 5,
            "village-hero-single-gate-five-distinct-licensed-prop-types");
        // No arbitrary world time or Core mutation is needed for this static check.
    }
}
