using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AreArpg.Core;
using AreArpg.UnityClient;
using Fadenwerk.EditorTools;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Bau-Modus: browser page and API on the existing Fadenwerk server (same token, same owner-authenticated proxy).
/// Page: "/?bau". API: "/api/x/…". Placed pieces are passive scenery; Core rules are never touched.
/// Effect chain per change: validated request → plan file (new revision) → live projection in the open editor or
/// the running game → readback of the realised piece count in Unity → response.
/// </summary>
[InitializeOnLoad]
public static class BauModusBridge
{
    public const string PageKey = "bau";
    const string PagePath = "Assets/Editor/BauModus/Web/BauModus.html";
    static readonly string FullPagePath;
    static readonly Dictionary<string, EntryDto> Measured = new Dictionary<string, EntryDto>();

    static BauModusBridge()
    {
        FullPagePath = Path.GetFullPath(PagePath);
        FadenwerkAuthoringBridge.ExtensionPageKey = PageKey;
        FadenwerkAuthoringBridge.ExtensionPage = () => File.ReadAllBytes(FullPagePath);
        FadenwerkAuthoringBridge.Extension = Handle;
    }

    [MenuItem("Tools/Ashen Crypt/Bau-Modus/Browser-Oberfläche starten")]
    public static void StartServer()
    {
        if (!FadenwerkAuthoringBridge.ServerRunning) FadenwerkAuthoringBridge.StartServer();
        Debug.Log("[Bau-Modus] Browser-Oberfläche: https://arelogic.space/fadenwerk/?bau (oder lokal http://127.0.0.1:<Fadenwerk-Port>/?bau)");
    }

    [MenuItem("Tools/Ashen Crypt/Bau-Modus/Vorschau aus der Szene entfernen")]
    public static void HidePreview() { BauModusPreview.Hide(); }

    // ------------------------------------------------------------------ DTOs (JsonUtility)

    [Serializable] class Req
    {
        public string area = "", key = "", id = "";
        public int revision = -1, view, count, seed;
        public float x, z, rot, scale = 1f, w, d, radius;
        public bool clear, real, focus;
    }
    [Serializable] class AreaDto { public string key, label; public bool previewWorld, hasPlan; public float extent; public int pieces; }
    [Serializable] class StateDto
    {
        public bool ok = true, playing, compiling, previewVisible;
        public string liveArea = "", previewArea = "", unity = "", project = "";
        public int livePieces, liveRevision, previewPieces;
        public AreaDto[] areas;
    }
    [Serializable] class CategoryDto { public string key, label, icon; }
    [Serializable] class EntryDto { public string key, label, cat, kind, color; public float w, d, h, ox, oz; public bool art; }
    [Serializable] class CatalogDto { public bool ok = true; public CategoryDto[] categories; public EntryDto[] entries; public string[] forest; }
    [Serializable] class ReservedDto { public string id; public float x, z, hw, hd; }
    [Serializable] class LiveDto { public string mode = "", note = ""; public int inUnity, revision; public bool shown; }
    [Serializable] class LayoutDto
    {
        public bool ok = true;
        public string error = "", area = "", message = "";
        public int revision;
        public bool canUndo;
        public BuildPiece[] pieces;
        public string[] ids;
        public LiveDto live;
        public ReservedDto[] reserved;
    }
    [Serializable] class GhostDto { public bool ok = true, shown; public string mode = ""; }

    // ------------------------------------------------------------------ routing

    static FadenwerkExtensionReply Handle(string method, string path, string body, IDictionary<string, string> query)
    {
        if (method == "GET")
        {
            switch (path)
            {
                case "state": return Json(State());
                case "catalog": return Json(Catalog());
                case "layout": return Json(LayoutReply(BauModusStore.Load(Arg(query, "area")), null, ""));
                case "spriteinfo": return Json(BauModusRender.SpriteMeta(Arg(query, "key"), true));
                case "sprite": return FadenwerkExtensionReply.Bytes(BauModusRender.SpritePng(Arg(query, "key")), "image/png");
                case "map/info": return MapInfo(query);
                case "map/image": return MapImage(query);
            }
            return FadenwerkExtensionReply.Error("Unbekannter Bau-Befehl: " + path, 404);
        }
        if (method != "POST") return FadenwerkExtensionReply.Error("Methode nicht erlaubt", 405);
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return FadenwerkExtensionReply.Error("Unity kompiliert gerade – gleich noch einmal versuchen.", 503);
        Req q;
        try { q = string.IsNullOrEmpty(body) ? new Req() : JsonUtility.FromJson<Req>(body); }
        catch (Exception e) { return FadenwerkExtensionReply.Error("Ungültige Daten: " + e.Message); }
        if (q == null) q = new Req();
        BauModusStore.Area(q.area);
        switch (path)
        {
            case "preview": return Preview(q);
            case "ghost": return Ghost(q);
            case "focus": Focus(q.x, q.z, q.view, 26f); return Json(new GhostDto { shown = true, mode = Mode() });
            case "map/render": return MapRender(q);
            case "place": return Change(q, Place);
            case "update": return Change(q, Update);
            case "remove": return Change(q, Remove);
            case "forest": return Change(q, Forest);
            case "undo": return Undo(q);
        }
        return FadenwerkExtensionReply.Error("Unbekannter Bau-Befehl: " + path, 404);
    }

    static string Arg(IDictionary<string, string> query, string name)
    {
        string value;
        return query != null && query.TryGetValue(name, out value) ? value ?? "" : "";
    }

    static FadenwerkExtensionReply Json(object dto) { return FadenwerkExtensionReply.Json(JsonUtility.ToJson(dto)); }

    static string Mode() { return EditorApplication.isPlaying ? "play" : "edit"; }

    // ------------------------------------------------------------------ reads

    static StateDto State()
    {
        var areas = new List<AreaDto>();
        foreach (BuildArea area in BuildAreas.All)
        {
            BuildLayoutData data = null;
            try { data = BauModusStore.Load(area.Key); } catch (InvalidOperationException) { }
            areas.Add(new AreaDto
            {
                key = area.Key, label = area.Label, previewWorld = !string.IsNullOrEmpty(area.PreviewWorld) && Resources.Load<GameObject>(BuildCatalog.PrefabRoot + area.PreviewWorld) != null,
                extent = area.Extent, hasPlan = File.Exists(BauModusStore.AssetPath(area.Key)), pieces = data != null ? data.pieces.Count : 0
            });
        }
        return new StateDto
        {
            playing = EditorApplication.isPlaying, compiling = EditorApplication.isCompiling,
            liveArea = EditorApplication.isPlaying ? BuildLayerLive.ActiveArea ?? "" : "",
            livePieces = BuildLayerLive.ActivePieces, liveRevision = BuildLayerLive.ActiveRevision,
            previewArea = BauModusPreview.Visible ? BauModusPreview.Area : "", previewVisible = BauModusPreview.Visible,
            previewPieces = BauModusPreview.Pieces, unity = Application.unityVersion, project = Application.productName,
            areas = areas.ToArray()
        };
    }

    static CatalogDto Catalog()
    {
        var categories = new List<CategoryDto>();
        foreach (BuildCategory c in BuildCatalog.Categories) categories.Add(new CategoryDto { key = c.Key, label = c.Label, icon = c.Icon });
        var entries = new List<EntryDto>();
        foreach (BuildCatalogEntry entry in BuildCatalog.All) entries.Add(Measure(entry));
        return new CatalogDto { categories = categories.ToArray(), entries = entries.ToArray(), forest = BuildCatalog.ForestKeys };
    }

    /// <summary>Footprint from the real prefab (renderer bounds of the uninstantiated asset), else the catalog blockout.</summary>
    static EntryDto Measure(BuildCatalogEntry entry)
    {
        EntryDto dto;
        if (Measured.TryGetValue(entry.Key, out dto)) return dto;
        dto = new EntryDto
        {
            key = entry.Key, label = entry.Label, cat = entry.Category, kind = entry.Kind == BuildKind.Surface ? "surface" : "prefab",
            w = entry.Width, d = entry.Depth, h = entry.Height, color = "#" + ColorUtility.ToHtmlStringRGB(entry.Fallback)
        };
        if (entry.Kind == BuildKind.Surface) dto.art = Resources.Load<Material>(entry.ResourcePath) != null;
        else
        {
            GameObject prefab = Resources.Load<GameObject>(entry.ResourcePath);
            dto.art = prefab != null;
            if (prefab != null)
            {
                bool any = false;
                Bounds b = new Bounds();
                foreach (Renderer r in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer) continue;
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
                if (any && b.size.x > 0.05f && b.size.z > 0.05f && b.size.x < 200f && b.size.z < 200f)
                {
                    Vector3 root = prefab.transform.position;
                    dto.w = Round(b.size.x); dto.d = Round(b.size.z); dto.h = Round(b.size.y);
                    dto.ox = Round(b.center.x - root.x); dto.oz = Round(b.center.z - root.z);
                }
            }
        }
        Measured[entry.Key] = dto;
        return dto;
    }

    static float Round(float v) { return Mathf.Round(v * 100f) / 100f; }

    static LayoutDto LayoutReply(BuildLayoutData data, LiveDto live, string message)
    {
        var reply = new LayoutDto
        {
            area = data.area, revision = data.revision, pieces = data.pieces.ToArray(), message = message ?? "",
            canUndo = BauModusStore.CanUndo(data), live = live ?? CurrentLive(data)
        };
        if (data.area == "aschengrenze")
        {
            // Canonical Core building footprints (collision and sight), shown so builders see what the rules own.
            var reserved = new List<ReservedDto>();
            for (int i = 0; i < EntryWorldLayout.BuildingCount; i++)
            {
                EntryWorldLayout.Footprint f = EntryWorldLayout.Building(i);
                reserved.Add(new ReservedDto { id = f.Id, x = f.X / 1000f, z = f.Z / 1000f, hw = f.HalfWidth / 1000f, hd = f.HalfDepth / 1000f });
            }
            reply.reserved = reserved.ToArray();
        }
        else reply.reserved = new ReservedDto[0];
        return reply;
    }

    static LiveDto CurrentLive(BuildLayoutData data)
    {
        if (EditorApplication.isPlaying)
        {
            bool here = BuildLayerLive.ActiveArea == data.area;
            return new LiveDto
            {
                mode = "play", shown = here, inUnity = here ? BuildLayerLive.ActivePieces : 0, revision = here ? BuildLayerLive.ActiveRevision : 0,
                note = here ? "" : "Das laufende Spiel zeigt gerade " + AreaLabel(BuildLayerLive.ActiveArea) + "."
            };
        }
        bool shown = BauModusPreview.Visible && BauModusPreview.Area == data.area;
        return new LiveDto
        {
            mode = "edit", shown = shown, inUnity = shown ? BauModusPreview.Pieces : 0, revision = data.revision,
            note = shown ? "" : "Vorschau in Unity ist für ein anderes Gebiet geöffnet."
        };
    }

    static string AreaLabel(string key)
    {
        BuildArea area = BuildAreas.Find(key);
        return area != null ? "„" + area.Label + "“" : "kein Baugebiet (Dungeon/Reise)";
    }

    // ------------------------------------------------------------------ live

    static FadenwerkExtensionReply Preview(Req q)
    {
        BuildLayoutData data = BauModusStore.Load(q.area);
        if (!EditorApplication.isPlaying)
        {
            BauModusPreview.Show(q.area);
            if (q.focus) Focus(0f, 0f, q.view, 70f);
        }
        return Json(LayoutReply(data, null, ""));
    }

    static FadenwerkExtensionReply Ghost(Req q)
    {
        BuildPiece piece = null;
        if (!q.clear)
        {
            string error;
            piece = BuildLayoutRules.Prepare(BauModusStore.Area(q.area), "b0", q.key, q.x, q.z, q.rot, q.scale, q.w, q.d, out error);
            if (piece == null) throw new InvalidOperationException(error);
        }
        bool shown;
        if (EditorApplication.isPlaying)
        {
            BuildLayerLive.PublishGhost(q.area, piece);
            shown = BuildLayerLive.GhostVisible;
        }
        else
        {
            if (piece != null && (!BauModusPreview.Visible || BauModusPreview.Area != q.area)) BauModusPreview.Show(q.area);
            BauModusPreview.Ghost(piece);
            shown = BauModusPreview.GhostVisible;
        }
        if (piece != null && q.focus) Focus(piece.x, piece.z, q.view, 22f);
        return Json(new GhostDto { shown = shown, mode = Mode() });
    }

    static LiveDto ApplyLive(BuildLayoutData data)
    {
        if (EditorApplication.isPlaying)
        {
            BuildLayerLive.PublishGhost(data.area, null);
            BuildLayerLive.Publish(data.area, data);
        }
        else
        {
            BauModusPreview.Ghost(null);
            BauModusPreview.Refresh(data);
        }
        return CurrentLive(data);
    }

    /// <summary>Move the Scene view onto the change so the live editor shows it immediately.</summary>
    static void Focus(float x, float z, int view, float size)
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null && SceneView.sceneViews.Count > 0) sceneView = SceneView.sceneViews[0] as SceneView;
        if (sceneView == null) return;
        float yaw = 45f + 90f * (((view % 4) + 4) % 4);
        sceneView.LookAt(new Vector3(x, 1f, z), Quaternion.Euler(38f, yaw, 0f), size, false, true);
        sceneView.Repaint();
    }

    // ------------------------------------------------------------------ changes

    delegate string Mutation(Req q, BuildArea area, BuildLayoutData next, List<string> ids);

    static FadenwerkExtensionReply Change(Req q, Mutation mutation)
    {
        BuildArea area = BauModusStore.Area(q.area);
        BuildLayoutData current = BauModusStore.Load(q.area);
        if (q.revision != current.revision) return Conflict(current);
        BuildLayoutData next = current.Clone();
        var ids = new List<string>();
        string message = mutation(q, area, next, ids);
        BuildLayoutData saved = BauModusStore.Commit(current, next);
        LiveDto live = ApplyLive(saved);
        if (q.focus && ids.Count > 0)
        {
            BuildPiece focus = saved.Find(ids[0]);
            if (focus != null) Focus(focus.x, focus.z, q.view, 24f);
        }
        LayoutDto reply = LayoutReply(saved, live, message);
        reply.ids = ids.ToArray();
        return Json(reply);
    }

    static string Place(Req q, BuildArea area, BuildLayoutData next, List<string> ids)
    {
        if (next.pieces.Count >= BuildLayoutRules.MaxPieces) throw new InvalidOperationException("Das Baugebiet ist voll (" + BuildLayoutRules.MaxPieces + " Teile).");
        string error;
        BuildPiece piece = BuildLayoutRules.Prepare(area, BuildLayoutRules.NextId(next), q.key, q.x, q.z, q.rot, q.scale, q.w, q.d, out error);
        if (piece == null) throw new InvalidOperationException(error);
        next.pieces.Add(piece);
        ids.Add(piece.id);
        return BuildCatalog.Find(piece.key).Label + " gebaut";
    }

    static string Update(Req q, BuildArea area, BuildLayoutData next, List<string> ids)
    {
        BuildPiece old = next.Find(q.id);
        if (old == null) throw new InvalidOperationException("Dieses Teil gibt es nicht mehr.");
        string error;
        BuildPiece piece = BuildLayoutRules.Prepare(area, old.id, old.key, q.x, q.z, q.rot, q.scale, q.w, q.d, out error);
        if (piece == null) throw new InvalidOperationException(error);
        next.pieces[next.pieces.IndexOf(old)] = piece;
        ids.Add(piece.id);
        return BuildCatalog.Find(piece.key).Label + " geändert";
    }

    static string Remove(Req q, BuildArea area, BuildLayoutData next, List<string> ids)
    {
        BuildPiece old = next.Find(q.id);
        if (old == null) throw new InvalidOperationException("Dieses Teil gibt es nicht mehr.");
        next.pieces.Remove(old);
        return BuildCatalog.Find(old.key).Label + " abgerissen";
    }

    static string Forest(Req q, BuildArea area, BuildLayoutData next, List<string> ids)
    {
        List<BuildPiece> trees = BuildLayoutRules.Forest(q.x, q.z, q.radius, q.count, unchecked((uint)q.seed));
        int planted = 0;
        foreach (BuildPiece tree in trees)
        {
            if (next.pieces.Count >= BuildLayoutRules.MaxPieces) break;
            string error;
            BuildPiece piece = BuildLayoutRules.Prepare(area, BuildLayoutRules.NextId(next), tree.key, tree.x, tree.z, tree.rot, tree.scale, 0f, 0f, out error);
            if (piece == null) continue; // outside the building site
            next.pieces.Add(piece);
            ids.Add(piece.id);
            planted++;
        }
        if (planted == 0) throw new InvalidOperationException("Hier passt kein Wald hin (außerhalb des Baugebiets oder Gebiet voll).");
        return "Wald gepflanzt: " + planted.ToString(CultureInfo.InvariantCulture) + " Pflanzen";
    }

    static FadenwerkExtensionReply Conflict(BuildLayoutData current)
    {
        LayoutDto reply = LayoutReply(current, null, "");
        reply.ok = false;
        reply.error = "Der Bauplan oder sein Verlauf hat sich geändert (Revision " + current.revision + "). Die Ansicht ist jetzt aktuell.";
        return FadenwerkExtensionReply.Json(JsonUtility.ToJson(reply), 409);
    }

    static FadenwerkExtensionReply Undo(Req q)
    {
        BuildLayoutData current = BauModusStore.Load(q.area);
        if (q.revision != current.revision) return Conflict(current);
        if (!BauModusStore.TryUndo(current, out BuildLayoutData restored)) return Conflict(current);
        LiveDto live = ApplyLive(restored);
        return Json(LayoutReply(restored, live, "Rückgängig gemacht"));
    }

    // ------------------------------------------------------------------ maps

    static FadenwerkExtensionReply MapRender(Req q)
    {
        var hide = new List<Transform>();
        if (EditorApplication.isPlaying)
        {
            if (BuildLayerLive.ActiveArea != q.area)
                throw new InvalidOperationException("Das laufende Spiel zeigt gerade " + AreaLabel(BuildLayerLive.ActiveArea) +
                    ". Für dieses Gebiet: im Spiel dorthin reisen oder den Play-Modus beenden.");
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.parent == null && (t.name.StartsWith(BuildLayer.RootPrefix, StringComparison.Ordinal) || t.name == BuildLayer.GhostName)) hide.Add(t);
        }
        else
        {
            if (!BauModusPreview.Visible || BauModusPreview.Area != q.area) BauModusPreview.Show(q.area);
            hide.Add(BauModusPreview.Layer);
            hide.Add(BauModusPreview.GhostRoot);
        }
        return Json(BauModusRender.RenderMap(q.area, q.view, q.real, hide));
    }

    static FadenwerkExtensionReply MapInfo(IDictionary<string, string> query)
    {
        string area = Arg(query, "area");
        BauModusStore.Area(area);
        int view; int.TryParse(Arg(query, "view"), NumberStyles.Integer, CultureInfo.InvariantCulture, out view);
        BauModusRender.MapInfo info = BauModusRender.MapMeta(area, view, Arg(query, "real") == "1");
        if (info == null) return FadenwerkExtensionReply.Error("Noch keine Karte für dieses Gebiet.", 404);
        return Json(info);
    }

    static FadenwerkExtensionReply MapImage(IDictionary<string, string> query)
    {
        string area = Arg(query, "area");
        BauModusStore.Area(area);
        int view; int.TryParse(Arg(query, "view"), NumberStyles.Integer, CultureInfo.InvariantCulture, out view);
        string path = BauModusRender.MapPath(area, ((view % 4) + 4) % 4, Arg(query, "real") == "1", "jpg");
        if (!File.Exists(path)) return FadenwerkExtensionReply.Error("Noch keine Karte für dieses Gebiet.", 404);
        return FadenwerkExtensionReply.Bytes(File.ReadAllBytes(path), "image/jpeg");
    }
}
