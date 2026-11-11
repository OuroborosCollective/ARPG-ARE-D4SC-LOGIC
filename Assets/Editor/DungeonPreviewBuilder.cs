using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace AreArpg.EditorTools
{
    /// <summary>Editor-only, passive room composition. The result is never loaded by ArenaRuntime.</summary>
    public static class DungeonPreviewBuilder
    {
        private const string Output = "Assets/PrivateArt/DungeonPreviews";
        private const string Visuals = "Assets/Resources/ARE/Visuals/";
        private const string Owner = "are.dungeon-preview.v1";
        [Serializable] public sealed class Blueprint
        {
            public int version, cellMm;
            public string id;
            public Room[] rooms;
            public Door[] doors;
        }
        [Serializable] public sealed class Room { public string id; public int x, z, width, length; }
        [Serializable] public sealed class Door { public string a, b, axis; public int x, z; }
        [Serializable] public sealed class Receipt
        {
            public string schema, blueprintId, planSha256, catalogSha256, prefabSha256, prefabPath, receiptSha256;
            public int floorCount, wallCount, doorwayCount;
        }
        private sealed class Edge { public int x, z; public string axis; public readonly List<string> rooms = new List<string>(); }
        private sealed class Part { public string id, key; public int xMm, zMm; public bool quarterTurn; }
        private sealed class Plan
        {
            public Blueprint input;
            public readonly List<Part> parts = new List<Part>();
            public readonly Dictionary<string, Edge> edges = new Dictionary<string, Edge>(StringComparer.Ordinal);
            public string hash;
        }

        /// <summary>Validate the entire plan and asset graph before updating this tool's own private preview.</summary>
        public static string Build(string json)
        {
            Plan plan = Compile(json);
            var sources = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            foreach (string key in new[] { "DungeonFloor", "DungeonWall", "DungeonArch" })
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(Visuals + key + ".prefab");
                if (source == null) throw new InvalidOperationException("Missing reviewed preview asset: " + key);
                RequirePassive(source);
                var instance = UnityEngine.Object.Instantiate(source);
                try
                {
                    Bounds b = BoundsOf(instance);
                    if (Mathf.Abs(b.size.x - 4f) > .01f ||
                        (key == "DungeonFloor" ? Mathf.Abs(b.size.z - 4f) > .01f : b.size.y < 2.5f))
                        throw new InvalidOperationException("Preview requires original 4m modules: " + key);
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
                sources.Add(key, source);
            }
            string catalog = CatalogHash(sources.Values.Select(AssetDatabase.GetAssetPath).ToArray());
            string path = Output + "/" + plan.input.id + ".prefab";
            string receiptPath = Output + "/" + plan.input.id + ".receipt.json";
            if (File.Exists(path) || File.Exists(receiptPath))
            {
                if (!File.Exists(path) || !File.Exists(receiptPath))
                    throw new InvalidOperationException("Unowned or incomplete preview output: " + path);
                var previous = JsonUtility.FromJson<Receipt>(File.ReadAllText(receiptPath));
                if (previous == null || previous.schema != Owner || previous.blueprintId != plan.input.id ||
                    previous.prefabPath != path || previous.prefabSha256 != FileHash(path) ||
                    previous.receiptSha256 != ReceiptHash(previous))
                    throw new InvalidOperationException("Preview was changed externally; preserve it: " + path);
                if (previous.planSha256 == plan.hash && previous.catalogSha256 == catalog) return path;
            }
            var root = new GameObject("DungeonPreview_" + plan.input.id);
            try
            {
                foreach (var part in plan.parts)
                {
                    var holder = new GameObject(part.id);
                    holder.transform.SetParent(root.transform, false);
                    var art = UnityEngine.Object.Instantiate(sources[part.key], holder.transform, false);
                    var bounds = BoundsOf(art);
                    art.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                    holder.transform.localPosition = new Vector3(part.xMm / 1000f,
                        part.key == "DungeonFloor" ? -bounds.size.y : 0f, part.zMm / 1000f);
                    holder.transform.localRotation = Quaternion.Euler(0, part.quarterTurn ? 90 : 0, 0);
                }
                RequirePassive(root);
                if (!AssetDatabase.IsValidFolder(Output))
                    AssetDatabase.CreateFolder("Assets/PrivateArt", "DungeonPreviews");
                Publish(root, path, receiptPath, new Receipt {
                    schema = Owner, blueprintId = plan.input.id, planSha256 = plan.hash,
                    catalogSha256 = catalog, prefabPath = path,
                    floorCount = plan.parts.Count(p => p.key == "DungeonFloor"),
                    wallCount = plan.parts.Count(p => p.key == "DungeonWall"),
                    doorwayCount = plan.parts.Count(p => p.key == "DungeonArch") });
                return path;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        /// <summary>Reject ambiguous IDs, overlaps, invalid openings and disconnected room graphs using integer cells.</summary>
        private static Plan Compile(string json)
        {
            if (json == null || json.Length > 65536) throw new ArgumentException("Blueprint size limit");
            Blueprint bp = ReadBlueprint(json);
            if (bp == null || bp.version != 1 || bp.cellMm != 4000 || !ValidId(bp.id) ||
                bp.rooms == null || bp.rooms.Length == 0 || bp.rooms.Length > 16 ||
                bp.doors == null || bp.doors.Length == 0 || bp.doors.Length > 64)
                throw new ArgumentException("Invalid dungeon preview v1 blueprint");
            var plan = new Plan { input = bp };
            var rooms = new Dictionary<string, Room>(StringComparer.Ordinal);
            var cells = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in bp.rooms)
            {
                if (r == null || !ValidId(r.id) || rooms.ContainsKey(r.id) ||
                    r.x < -128 || r.x > 128 || r.z < -128 || r.z > 128 ||
                    r.width < 1 || r.width > 16 || r.length < 1 || r.length > 16)
                    throw new ArgumentException("Invalid or duplicate room");
                rooms.Add(r.id, r);
                for (int x = r.x; x < r.x + r.width; x++)
                    for (int z = r.z; z < r.z + r.length; z++)
                    {
                        if (!cells.Add(Key("cell", x, z)) || cells.Count > 256)
                            throw new ArgumentException("Overlapping rooms or floor budget exceeded");
                        plan.parts.Add(new Part { id = Key("floor", x, z), key = "DungeonFloor",
                            xMm = x * 4000 + 2000, zMm = z * 4000 + 2000 });
                    }
                for (int x = r.x; x < r.x + r.width; x++)
                { AddEdge(plan, "x", x, r.z, r.id); AddEdge(plan, "x", x, r.z + r.length, r.id); }
                for (int z = r.z; z < r.z + r.length; z++)
                { AddEdge(plan, "z", r.x, z, r.id); AddEdge(plan, "z", r.x + r.width, z, r.id); }
            }
            var openings = new HashSet<string>(StringComparer.Ordinal);
            var connected = rooms.Keys.ToDictionary(k => k, k => new List<string>(), StringComparer.Ordinal);
            foreach (var d in bp.doors)
            {
                if (d == null || (d.axis != "x" && d.axis != "z") || string.IsNullOrEmpty(d.a) ||
                    !rooms.ContainsKey(d.a)) throw new ArgumentException("Invalid doorway");
                string key = Key(d.axis, d.x, d.z);
                Edge edge;
                if (!plan.edges.TryGetValue(key, out edge) || !openings.Add(key) || !edge.rooms.Contains(d.a))
                    throw new ArgumentException("Doorway must replace a unique wall of its room");
                if (string.IsNullOrEmpty(d.b))
                {
                    if (edge.rooms.Count != 1) throw new ArgumentException("External doorway is an internal wall");
                }
                else
                {
                    if (d.a == d.b || edge.rooms.Count != 2 || !edge.rooms.Contains(d.b))
                        throw new ArgumentException("Doorway rooms must share this exact wall");
                    connected[d.a].Add(d.b); connected[d.b].Add(d.a);
                }
            }
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>(); queue.Enqueue(rooms.Keys.OrderBy(k => k, StringComparer.Ordinal).First());
            while (queue.Count > 0)
            {
                string id = queue.Dequeue();
                if (visited.Add(id)) foreach (string neighbor in connected[id]) queue.Enqueue(neighbor);
            }
            if (visited.Count != rooms.Count) throw new ArgumentException("Disconnected preview rooms");
            foreach (var entry in plan.edges.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                var edge = entry.Value;
                bool door = openings.Contains(entry.Key);
                plan.parts.Add(new Part { id = (door ? "door-" : "wall-") + entry.Key,
                    key = door ? "DungeonArch" : "DungeonWall", quarterTurn = edge.axis == "z",
                    xMm = edge.x * 4000 + (edge.axis == "x" ? 2000 : 0),
                    zMm = edge.z * 4000 + (edge.axis == "z" ? 2000 : 0) });
            }
            plan.parts.Sort((a, b) => StringComparer.Ordinal.Compare(a.id, b.id));
            var canonical = new StringBuilder(Owner + "\n" + bp.id + "\n");
            foreach (var r in rooms.Values.OrderBy(r => r.id, StringComparer.Ordinal))
                canonical.Append(r.id).Append(':').Append(Key("room", r.x, r.z)).Append(':')
                    .Append(r.width.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(r.length.ToString(CultureInfo.InvariantCulture)).Append('\n');
            foreach (var p in plan.parts)
                canonical.Append(p.id).Append(':').Append(p.key).Append(':')
                    .Append(p.xMm.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(p.zMm.ToString(CultureInfo.InvariantCulture)).Append(':').Append(p.quarterTurn ? '1' : '0').Append('\n');
            plan.hash = Hash(Encoding.UTF8.GetBytes(canonical.ToString()));
            return plan;
        }

        /// <summary>Require every field explicitly; absent coordinates must never silently become zero.</summary>
        private static Blueprint ReadBlueprint(string json)
        {
            try
            {
                using (var reader = new JsonTextReader(new StringReader(json)) { MaxDepth = 8, DateParseHandling = DateParseHandling.None })
                {
                    var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new ArgumentException("Trailing blueprint data");
                    RequireFields(root, "version:int,id:string,cellMm:int,rooms:array,doors:array");
                    foreach (var room in (JArray)root["rooms"])
                        RequireFields(room as JObject, "id:string,x:int,z:int,width:int,length:int");
                    foreach (var door in (JArray)root["doors"])
                        RequireFields(door as JObject, "a:string,b:string,axis:string,x:int,z:int");
                    return JsonUtility.FromJson<Blueprint>(root.ToString(Formatting.None));
                }
            }
            catch (JsonException error) { throw new ArgumentException("Invalid blueprint JSON", error); }
        }

        private static void RequireFields(JObject value, string contract)
        {
            string[] fields = contract.Split(',');
            if (value == null || value.Count != fields.Length) throw new ArgumentException("Unexpected blueprint fields");
            foreach (string field in fields)
            {
                string[] pair = field.Split(':'); JToken token = value[pair[0]];
                JTokenType type = pair[1] == "int" ? JTokenType.Integer : pair[1] == "array" ? JTokenType.Array : JTokenType.String;
                int integer;
                if (token == null || token.Type != type || (type == JTokenType.Integer &&
                    !int.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out integer)))
                    throw new ArgumentException("Missing or invalid blueprint field: " + pair[0]);
            }
        }

        /// <summary>Keep identity and output filenames within one portable, bounded namespace.</summary>
        private static bool ValidId(string id) { return id != null && Regex.IsMatch(id, @"\A[a-z][a-z0-9-]{0,47}\z"); }
        private static string Key(string axis, int x, int z)
        { return axis + "-" + x.ToString(CultureInfo.InvariantCulture) + "-" + z.ToString(CultureInfo.InvariantCulture); }
        /// <summary>Deduplicate shared room walls while retaining their exact adjacent room identities.</summary>
        private static void AddEdge(Plan plan, string axis, int x, int z, string room)
        {
            string key = Key(axis, x, z); Edge edge;
            if (!plan.edges.TryGetValue(key, out edge))
            { edge = new Edge { axis = axis, x = x, z = z }; plan.edges.Add(key, edge); }
            edge.rooms.Add(room);
        }
        /// <summary>Reject executable or physics components before instantiating any external prefab.</summary>
        private static void RequirePassive(GameObject root)
        {
            foreach (Component c in root.GetComponentsInChildren<Component>(true))
                if (!(c is Transform || c is MeshFilter || c is Renderer || c is LODGroup))
                    throw new InvalidOperationException("Non-passive dungeon preview asset");
        }
        /// <summary>Measure real imported renderer extents before accepting or placing a module.</summary>
        private static Bounds BoundsOf(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("Empty dungeon mesh");
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }
        /// <summary>Bind the receipt to the actual imported source files and importer metadata in stable order.</summary>
        private static string CatalogHash(string[] paths)
        {
            var files = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string p in AssetDatabase.GetDependencies(paths, true))
            {
                string physical = PhysicalPath(p);
                if (!File.Exists(physical)) throw new IOException("Missing source dependency: " + p);
                files.Add(p);
                if (File.Exists(physical + ".meta")) files.Add(p + ".meta");
            }
            var canonical = new StringBuilder();
            foreach (string f in files) canonical.Append(f).Append(':').Append(FileHash(PhysicalPath(f))).Append('\n');
            return Hash(Encoding.UTF8.GetBytes(canonical.ToString()));
        }
        private static string PhysicalPath(string assetPath)
        {
            if (!assetPath.StartsWith("Packages/", StringComparison.Ordinal)) return assetPath;
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(assetPath);
            if (package == null || !assetPath.StartsWith(package.assetPath + "/", StringComparison.Ordinal))
                throw new IOException("Cannot resolve source package: " + assetPath);
            return Path.Combine(package.resolvedPath, assetPath.Substring(package.assetPath.Length + 1));
        }
        /// <summary>Detect accidental edits to every emitted receipt field, including derived counts.</summary>
        private static string ReceiptHash(Receipt receipt)
        {
            string saved = receipt.receiptSha256;
            try { receipt.receiptSha256 = null; return Hash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(receipt))); }
            finally { receipt.receiptSha256 = saved; }
        }
        private static string FileHash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        private static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        /// <summary>Restore the previous owned files if saving or immediate readback fails.</summary>
        private static void Publish(GameObject root, string path, string receiptPath, Receipt receipt)
        {
            string[] names = { path, path + ".meta", receiptPath, receiptPath + ".meta" };
            var backup = names.ToDictionary(n => n, n => File.Exists(n) ? File.ReadAllBytes(n) : null);
            try
            {
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new IOException("Preview save failed");
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var loaded = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (loaded == null || loaded.transform.childCount != root.transform.childCount)
                    throw new IOException("Preview readback mismatch");
                RequirePassive(loaded);
                receipt.prefabSha256 = FileHash(path);
                receipt.receiptSha256 = ReceiptHash(receipt);
                File.WriteAllText(receiptPath, JsonUtility.ToJson(receipt, true));
                AssetDatabase.ImportAsset(receiptPath, ImportAssetOptions.ForceUpdate);
                if (JsonUtility.FromJson<Receipt>(File.ReadAllText(receiptPath)).prefabSha256 != FileHash(path))
                    throw new IOException("Preview receipt readback mismatch");
            }
            catch
            {
                foreach (var old in backup)
                    if (old.Value == null) { if (File.Exists(old.Key)) File.Delete(old.Key); }
                    else File.WriteAllBytes(old.Key, old.Value);
                AssetDatabase.Refresh();
                throw;
            }
        }
    }
}
