using AreArpg.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    public sealed partial class ArenaRuntime
    {
        private void BuildNordbogen()
        {
            float x = ArenaSimulation.NordwechselXmm * .001f;
            float z = ArenaSimulation.NordwechselZmm * .001f;
            var gate = new GameObject("Aschenzoll_Nordbogen").transform;
            gate.position = new Vector3(x, BuildGround(x, z), z);
            _chapterScenery.Add(gate);
            _woodlandPortals.Add(gate);

            // The pointed arch has an open centre and a ground-level pivot. The render subtree
            // retains its three authored LODs while the Core owns the passage through it.
            AddRegionalPiece(gate, "CryptArch", x, z, 0f, 1.16f);
            AddRegionalPiece(gate, "ShaleLedge", x - 4.65f, z + .65f, -12f, .68f);
            AddRegionalPiece(gate, "Brazier", x - 2.8f, z - 1.3f, 17f);
            BuildLayer.CreatePiece(new BuildPiece
            {
                id = "NordbogenSchwelle", key = "RoadStone", x = x, z = z, w = 3.5f, d = 1.45f
            }, gate, BuildGround, 0);
            FreightSign("Nordwechsel_Wegweiser", "Glutklamm", new Vector3(x + 3.1f, BuildGround(x + 3.1f, z - 1f) + 1.2f, z - 1f));
        }

        private void BuildEmberRiftRim()
        {
            var rim = new GameObject("Ember_Rift_Cliff_Rim").transform;
            _chapterScenery.Add(rim);
            float edge = ArenaSimulation.ArenaLimitMm * .001f;

            // Leave the existing basalt pillars, causeway and ritual dais inside the arena.
            // The cleft slate silhouettes sit outside the walk limit, with a sightline through the north gate.
            AddRegionalPiece(rim, "ShaleLedge", -14.5f, edge + 4.5f, -6f, 2.1f);
            AddRegionalPiece(rim, "ShaleLedge", 12f, edge + 4.15f, 9f, 1.7f);
            AddRegionalPiece(rim, "ShaleLedge", edge + 4.15f, 8f, 86f, 2.1f);
            AddRegionalPiece(rim, "ShaleLedge", -edge - 4f, -2f, 96f, 1.9f);
            rim.position += Vector3.down * .28f; // sink the toes into the surrounding basalt floor
        }
    }
}
