using System.Collections.Generic;
using AreArpg.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace AreArpg.UnityClient
{
    /// <summary>Static road and refuge dressing. Only the Core supplies solid shelter footprints.</summary>
    public sealed class RefugeScenery : MonoBehaviour
    {
        private readonly List<Material> _materials = new List<Material>();
        private Mesh _canopy;

        public static Transform Create(bool mill)
        {
            var root = new GameObject(mill ? "OldMill_Refuge" : "AshRoad_Verges");
            var scenery = root.AddComponent<RefugeScenery>();
            scenery.AddRoadside(mill);
            if (mill) scenery.AddShelter();
            return root.transform;
        }

        private Material Surface(string key, Color fallback)
        {
            var existing = Resources.Load<Material>("ARE/Visuals/WorldSurfaces/" + key);
            if (existing != null) return existing;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", fallback); material.SetFloat("_Smoothness", .05f);
            _materials.Add(material); return material;
        }

        private Transform Shape(string name, PrimitiveType type, Vector3 position, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(type); part.name = name;
            part.transform.SetParent(transform, false); part.transform.localPosition = position; part.transform.localScale = size;
            var collider = part.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }

        private void AddShelter()
        {
            Material wood = Surface("MillWood", new Color(.28f, .20f, .13f));
            Material cloth = Surface("RefugeCanvas", new Color(.52f, .43f, .29f));
            Material linen = Surface("RefugeLinen", new Color(.73f, .69f, .56f));
            Material board = Surface("MillBoard", new Color(.30f, .22f, .14f));
            float width = ArenaSimulation.MillShelterPostHalfWidthMm * .002f;
            for (int i = 0; i < 4; i++)
            {
                ArenaSimulation.MillShelterPost(i, out long x, out long z);
                float height = i < 2 ? 3.4f : 3.0f;
                Shape("OldMill_Shelter_Post_" + i, PrimitiveType.Cube,
                    new Vector3(x * .001f, height * .5f, z * .001f), new Vector3(width, height, width), wood);
            }
            Shape("OldMill_Shelter_FrontBeam", PrimitiveType.Cube, new Vector3(-6.5f, 3.4f, 2.9f), new Vector3(8.3f, .15f, .18f), wood);
            Shape("OldMill_Shelter_BackBeam", PrimitiveType.Cube, new Vector3(-6.5f, 3f, 7.2f), new Vector3(8.3f, .15f, .18f), wood);
            AddCanopy(cloth);

            // Flat boards connect the existing supply platform to the bed approach without inventing an obstacle.
            for (int i = 0; i < 7; i++)
                Shape("OldMill_Refuge_Walkboard_" + i, PrimitiveType.Cube, new Vector3(-5.1f, -.035f, .25f + i * .46f),
                    new Vector3(2.7f, .07f, .41f), board);
            for (int i = 0; i < 3; i++)
                Shape("OldMill_Bed_Linen_" + i, PrimitiveType.Cube, new Vector3(-4 - i * 2.5f, .31f, 5),
                    new Vector3(1.25f, .02f, 2.14f), linen);
            Shape("OldMill_Folded_Linen", PrimitiveType.Cube, new Vector3(-4.15f, .64f, -2.1f), new Vector3(.6f, .08f, .35f), linen);
            for (int i = 0; i < 3; i++)
            {
                var roll = Shape("OldMill_Dressing_Roll_" + i, PrimitiveType.Cylinder,
                    new Vector3(-4.35f + i * .22f, .76f, -2.08f), new Vector3(.14f, .13f, .14f), linen);
                roll.localRotation = Quaternion.Euler(90, 0, 0);
            }
            // Sealed small food parcels sit on the same existing supply crate, not a new stock ledger.
            Material parcel = Surface("RefugeParcel", new Color(.36f, .29f, .18f));
            Shape("OldMill_Supply_Parcel", PrimitiveType.Cube, new Vector3(-3.65f, .69f, -1.9f), new Vector3(.24f, .18f, .3f), parcel);
        }

        private void AddCanopy(Material material)
        {
            const int columns = 8, rows = 4;
            var vertices = new Vector3[(columns + 1) * (rows + 1)];
            var uv = new Vector2[vertices.Length]; var triangles = new List<int>();
            for (int row = 0; row <= rows; row++)
                for (int column = 0; column <= columns; column++)
                {
                    float u = column / (float)columns, v = row / (float)rows;
                    int n = row * (columns + 1) + column;
                    vertices[n] = new Vector3(-10.9f + u * 8.8f, 3.52f - v * .46f - Mathf.Sin(u * Mathf.PI * 4) * .045f,
                        2.5f + v * 5.1f);
                    uv[n] = new Vector2(u, v);
                    if (column == columns || row == rows) continue;
                    triangles.AddRange(new[] { n, n + columns + 1, n + 1, n + 1, n + columns + 1, n + columns + 2 });
                }
            _canopy = new Mesh { name = "Refuge canvas", vertices = vertices, uv = uv, triangles = triangles.ToArray() };
            _canopy.RecalculateNormals(); _canopy.RecalculateBounds();
            // Only a private instance is made double-sided; imported material assets remain untouched.
            var canvas = new Material(material); canvas.SetFloat("_Cull", 0); _materials.Add(canvas);
            var roof = new GameObject("OldMill_Shelter_Canvas"); roof.transform.SetParent(transform, false);
            roof.AddComponent<MeshFilter>().sharedMesh = _canopy;
            roof.AddComponent<MeshRenderer>().sharedMaterial = canvas;
        }

        private void AddRoadside(bool mill)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 6; i++)
                {
                    float x = side * (5.4f + i % 3 * .75f);
                    if (mill && side < 0) x = -15f - i % 2;
                    BindClump("Route_Grass_" + side + "_" + i, "AshGrass", new Vector3(x, -.08f, -20 + i * 8), .65f, 2f);
                }
                for (int i = 0; i < 3; i++)
                    BindClump("Route_OuterRocks_" + side + "_" + i, "AshRocks", new Vector3(side * 31, -.1f, -18 + i * 17), 3.2f, 4f);
            }
        }

        private void BindClump(string name, string key, Vector3 position, float height, float width)
        {
            var host = new GameObject(name); host.transform.SetParent(transform, false);
            if (!PassiveArtVisuals.TryBindGrounded(host.transform, key, 0)) { Destroy(host); return; }
            var renderers = host.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Destroy(host); return; }
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            if (bounds.size.y <= 0 || Mathf.Max(bounds.size.x, bounds.size.z) <= 0) { Destroy(host); return; }
            host.transform.localScale = Vector3.one * Mathf.Min(height / bounds.size.y, width / Mathf.Max(bounds.size.x, bounds.size.z));
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) { bounds.Encapsulate(renderer.bounds); renderer.shadowCastingMode = ShadowCastingMode.Off; }
            host.transform.position += position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }

        private void OnDestroy()
        {
            if (_canopy != null) Destroy(_canopy);
            foreach (var material in _materials) if (material != null) Destroy(material);
        }
    }
}
