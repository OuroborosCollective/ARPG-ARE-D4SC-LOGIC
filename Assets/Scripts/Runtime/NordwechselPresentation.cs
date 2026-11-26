using System.Collections.Generic;
using AreArpg.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    public sealed partial class ArenaRuntime
    {
        private Transform _nordbogenArch;
        private Bounds[] _nordbogenCameraBounds;

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
            _nordbogenArch = AddRegionalPiece(gate, "CryptArch", x, z, 0f, 1.16f);
            if (_nordbogenArch != null) _nordbogenCameraBounds = NordbogenCameraBounds(_nordbogenArch);
            AddRegionalPiece(gate, "ShaleLedge", x - 4.65f, z + .65f, -12f, .68f);
            AddRegionalPiece(gate, "Brazier", x - 2.8f, z - 1.3f, 17f);
            BuildLayer.CreatePiece(new BuildPiece
            {
                id = "NordbogenSchwelle", key = "RoadStone", x = x, z = z, w = 3.5f, d = 1.45f
            }, gate, BuildGround, 0);
            FreightSign("Nordwechsel_Wegweiser", "Glutklamm", new Vector3(x + 3.1f, BuildGround(x + 3.1f, z - 1f) + 1.2f, z - 1f));
        }

        private static Bounds[] NordbogenCameraBounds(Transform arch)
        {
            // The FBX and its recessed stone relief each have a single renderer across the
            // opening. Their outer bounds follow the actual instance; their inner profile
            // below is measured across the three mesh LODs, in unscaled metres.
            MeshRenderer[] meshes = arch.GetComponentsInChildren<MeshRenderer>();
            Bounds shell = meshes[0].bounds;
            foreach (var mesh in meshes) shell.Encapsulate(mesh.bounds);
            var stone = new List<Bounds>(7);
            float bottom = shell.min.y;
            void AddBox(float left, float right, float top)
            {
                var box = new Bounds();
                box.SetMinMax(new Vector3(left, bottom, shell.min.z), new Vector3(right, top, shell.max.z));
                stone.Add(box);
            }
            void AddPiers(float top, float halfOpening)
            {
                Vector3 left = arch.TransformPoint(-halfOpening, top, 0f);
                Vector3 right = arch.TransformPoint(halfOpening, top, 0f);
                AddBox(shell.min.x, left.x, left.y);
                AddBox(right.x, shell.max.x, right.y);
                bottom = left.y;
            }
            AddPiers(2.05f, 1.16f);
            AddPiers(2.50f, .86f);
            AddPiers(2.86f, .53f);
            AddBox(shell.min.x, shell.max.x, shell.max.y);
            return stone.ToArray();
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
