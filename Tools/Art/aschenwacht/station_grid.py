"""Bake where the wanderer may walk at Aschenwacht (desecrated forecourt + connector + bell court) and how high the ground is.

usage: python3 -I station_grid.py <config.json>

config: {"forecourt_collision": glb, "connector": glb, "court_scene": glb, "out": folder,
         "forecourt_models": {"root": folder of part 2, "categories": [...], "extra": [model ids], "skip": [model ids]},
         "figures": [[x, z, radius], ...] (game metres, passive figures that block), "blocked_boxes": [[x0, z0, x1, z1], ...],
         "spawn": [x, z], "exit": [x, z]}

Frame of the game (Unity, metres): x = -X, y = Y, z = Z of the forecourt's glTF frame (the set turned half a turn, so that the
way from the crypt portal to the bell court leads north, away from the camera). The connector sits at glTF (0, 1.195, 46); the bell
court is docked turned half a turn at glTF (0.55, 1.195, 60.33), so that its straight path meets the connector at Z = 52.

Per cell of 0.25 m: the floor is the top of the low stack of upward faces (see below); anything between 0.40 and 1.85 m above that floor blocks;
neighbouring floors may differ by at most 0.40 m (one stair step is 0.17 m, the chapel's threshold more); only what the spawn reaches counts; the wanderer's
radius of 0.5 m is added to every obstacle. Heights are kept on a 0.5 m grid in steps of 5 cm.
"""
import csv
import json
import math
import pathlib
import struct
import sys

import numpy as np

CELL, HCELL, STEP = 0.25, 0.5, 0.05
LOW, HIGH, CLIMB, RADIUS = 0.40, 1.85, 0.40, 0.5
SKIP_FAMILIES = ("Rispengras", "Witwengras")


def quat_matrix(q):
    x, y, z, w = q
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def read_glb(path):
    data = pathlib.Path(path).read_bytes()
    size, _ = struct.unpack_from("<I4s", data, 12)
    g = json.loads(data[20:20 + size])
    if g.get("extensionsRequired"):
        sys.exit("unsupported extension in " + str(path) + ": " + str(g["extensionsRequired"]))
    binary = 20 + size + 8

    def accessor(index):
        a = g["accessors"][index]
        view = g["bufferViews"][a["bufferView"]]
        comps = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}[a["type"]]
        dtype = {5121: "u1", 5123: "<u2", 5125: "<u4", 5126: "<f4"}[a["componentType"]]
        start = binary + view.get("byteOffset", 0) + a.get("byteOffset", 0)
        item = np.dtype(dtype).itemsize * comps
        stride = view.get("byteStride", 0) or item
        raw = np.frombuffer(data, dtype="u1", count=stride * (a["count"] - 1) + item, offset=start)
        rows = np.lib.stride_tricks.as_strided(raw, shape=(a["count"], item), strides=(stride, 1))
        return np.frombuffer(rows.tobytes(), dtype=dtype).reshape(a["count"], comps)

    meshes = {}
    for index, mesh in enumerate(g.get("meshes", [])):
        tris = []
        for prim in mesh["primitives"]:
            pos = accessor(prim["attributes"]["POSITION"]).astype(np.float64)
            idx = accessor(prim["indices"]).reshape(-1, 3) if "indices" in prim else np.arange(len(pos)).reshape(-1, 3)
            tris.append(pos[idx])
        meshes[index] = np.concatenate(tris) if tris else np.zeros((0, 3, 3))
    return g, meshes


def node_world(g, meshes, skip=()):
    """Every mesh node's triangles in the file's world frame (node hierarchy applied)."""
    out = []

    def walk(index, parent):
        node = g["nodes"][index]
        if "matrix" in node:
            m = np.array(node["matrix"], dtype=np.float64).reshape(4, 4).T
        else:
            m = np.eye(4)
            m[:3, :3] = quat_matrix(node.get("rotation", [0, 0, 0, 1])) * np.array(node.get("scale", [1, 1, 1]))
            m[:3, 3] = node.get("translation", [0, 0, 0])
        world = parent @ m
        family = node.get("name", "").split(".")[0]
        if "mesh" in node and "skin" not in node and not family.startswith(skip):
            t = meshes[node["mesh"]]
            out.append((family, t @ world[:3, :3].T + world[:3, 3]))
        elif "mesh" in node and "skin" in node:
            out.append((family, meshes[node["mesh"]]))  # skinned figures: bind pose at their own origin
        for child in node.get("children", []):
            walk(child, world)

    scene = g["scenes"][g.get("scene", 0)]
    for root in scene["nodes"]:
        walk(root, np.eye(4))
    return out


def samples(tris, spacing):
    """Points on the triangles, at most `spacing` apart, with each point's triangle normal (y component, unit length)."""
    if len(tris) == 0:
        return np.zeros((0, 3)), np.zeros(0)
    a, b, c = tris[:, 0], tris[:, 1], tris[:, 2]
    n = np.cross(b - a, c - a)
    length = np.linalg.norm(n, axis=1)
    ny = np.where(length > 1e-12, n[:, 1] / np.maximum(length, 1e-12), 0.0)
    edges = np.maximum.reduce([np.linalg.norm(a - b, axis=1), np.linalg.norm(b - c, axis=1), np.linalg.norm(c - a, axis=1)])
    steps = np.clip(np.ceil(edges / spacing).astype(int), 1, 600)
    points, normals = [], []
    for k in np.unique(steps):
        pick = steps == k
        u, v = np.meshgrid(np.arange(k + 1), np.arange(k + 1))
        keep = (u + v) <= k
        u, v = u[keep] / k, v[keep] / k
        w = 1 - u - v
        p = a[pick, None] * w[None, :, None] + b[pick, None] * u[None, :, None] + c[pick, None] * v[None, :, None]
        points.append(p.reshape(-1, 3))
        normals.append(np.repeat(ny[pick], len(u)))
    return np.concatenate(points), np.concatenate(normals)


def main():
    cfg = json.loads(pathlib.Path(sys.argv[1]).read_text())
    out = pathlib.Path(cfg["out"])
    out.mkdir(parents=True, exist_ok=True)
    parts = []
    g, m = read_glb(cfg["forecourt_collision"])
    parts += [("Vorhof", t) for _, t in node_world(g, m)]
    g, m = read_glb(cfg["connector"])
    parts += [("Anschluss", t + np.array([0.0, 1.195, 46.0])) for _, t in node_world(g, m)]
    g, m = read_glb(cfg["court_scene"])
    dock = np.diag([-1.0, 1.0, -1.0])
    for family, t in node_world(g, m, SKIP_FAMILIES):
        parts.append((family, t @ dock.T + np.array([0.55, 1.195, 60.33])))
    # The forecourt's collision surface leaves out the westwork and some stonework: its real buildings, graves, sculptures,
    # liturgical pieces and trees block as well. They only ever add obstacles, never floor.
    blockers = []
    fm = cfg.get("forecourt_models")
    if fm:
        root = pathlib.Path(fm["root"])
        catalog = {c["ID"]: c for c in csv.DictReader(open(root / "Dokumentation/Modellkatalog.csv", encoding="utf-8")) if c["Eintrag"] == "Modelltyp"}
        wanted = {k for k, c in catalog.items() if c["Kategorie"] in fm["categories"]} | set(fm.get("extra", []))
        wanted -= set(fm.get("skip", []))
        convert = np.array([[1.0, 0, 0], [0, 0, 1.0], [0, -1.0, 0]])  # Blender (Z up) -> glTF (Y up)
        cache = {}
        for row in csv.DictReader(open(root / "Dokumentation/Instanzen.csv", encoding="utf-8")):
            model = row["modell"]
            if model not in wanted:
                continue
            if model not in cache:
                g, m = read_glb(root / catalog[model]["Datei"])
                cache[model] = np.concatenate([t for _, t in node_world(g, m)])
            rx, ry, rz = (float(row[k]) for k in ("rx_rad", "ry_rad", "rz_rad"))
            cx, sx_, cy, sy_, cz, sz_ = math.cos(rx), math.sin(rx), math.cos(ry), math.sin(ry), math.cos(rz), math.sin(rz)
            rot = (np.array([[cz, -sz_, 0], [sz_, cz, 0], [0, 0, 1]]) @ np.array([[cy, 0, sy_], [0, 1, 0], [-sy_, 0, cy]]) @
                   np.array([[1, 0, 0], [0, cx, -sx_], [0, sx_, cx]]))
            scale = np.diag([float(row["sx"]), float(row["sy"]), float(row["sz"])])
            place = convert @ rot @ scale @ convert.T
            move = convert @ np.array([float(row["blender_x_m"]), float(row["blender_y_m"]), float(row["blender_z_m"])])
            blockers.append(cache[model] @ place.T + move)
    # glTF frame -> game frame: x = -X. Normals keep their y, so "upward" is decided before the mirror.
    pts, nys, solid = [], [], []
    for family, t in parts:
        p, ny = samples(t, CELL * 0.4)
        p[:, 0] *= -1
        pts.append(p); nys.append(ny); solid.append(np.zeros(len(p), dtype=bool))
    for t in blockers:
        p, ny = samples(t, CELL * 0.4)
        keep = (p[:, 1] > -0.5) & (p[:, 1] < 3.6)
        p, ny = p[keep], ny[keep]
        p[:, 0] *= -1
        pts.append(p); nys.append(ny); solid.append(np.ones(len(p), dtype=bool))
    pts, nys, solid = np.concatenate(pts), np.concatenate(nys), np.concatenate(solid)
    lo = np.floor(pts[:, [0, 2]].min(axis=0) / HCELL) * HCELL - 1.0
    hi = np.ceil(pts[:, [0, 2]].max(axis=0) / HCELL) * HCELL + 1.0
    cols, rows = int(round((hi[0] - lo[0]) / CELL)), int(round((hi[1] - lo[1]) / CELL))
    ci = np.clip(((pts[:, 0] - lo[0]) / CELL).astype(int), 0, cols - 1)
    ri = np.clip(((pts[:, 2] - lo[1]) / CELL).astype(int), 0, rows - 1)
    flat = ri * cols + ci
    # The walking surface is the top of the low stack: the highest upward face that lies less than LOW above the lowest one
    # (paving on the ground, a chapel floor one threshold up), never the top of a rock, an arch or a canopy.
    lowest = np.full(rows * cols, np.inf)
    up = (nys > 0.6) & ~solid
    np.minimum.at(lowest, flat[up], pts[up, 1])
    floor = np.full(rows * cols, -np.inf)
    low_stack = up & (pts[:, 1] < np.where(np.isfinite(lowest[flat]), lowest[flat], -np.inf) + LOW)
    np.maximum.at(floor, flat[low_stack], pts[low_stack, 1])
    has_floor = np.isfinite(floor)
    floor[~has_floor] = np.inf
    rel = pts[:, 1] - np.where(has_floor[flat], floor[flat], 0.0)
    hit = has_floor[flat] & (rel > LOW) & (rel < HIGH)
    obstacle = np.zeros(rows * cols, dtype=bool)
    obstacle[flat[hit]] = True
    floor, has_floor, obstacle = floor.reshape(rows, cols), has_floor.reshape(rows, cols), obstacle.reshape(rows, cols)
    # Places that belong to no walk: behind the westwork lies the church, not the court.
    for x0, z0, x1, z1 in cfg.get("blocked_boxes", []):
        zz, xx = np.mgrid[0:rows, 0:cols]
        cxm, czm = lo[0] + (xx + .5) * CELL, lo[1] + (zz + .5) * CELL
        obstacle |= (cxm >= x0) & (cxm <= x1) & (czm >= z0) & (czm <= z1)
    for x, z, r in cfg.get("figures", []):
        zz, xx = np.mgrid[0:rows, 0:cols]
        obstacle |= ((lo[0] + (xx + .5) * CELL - x) ** 2 + (lo[1] + (zz + .5) * CELL - z) ** 2) <= r * r

    def cell(p):
        return int((p[1] - lo[1]) / CELL), int((p[0] - lo[0]) / CELL)

    def flood(free, start):
        seen = np.zeros_like(free)
        stack = [start]
        while stack:
            r, c = stack.pop()
            if seen[r, c] or not free[r, c]:
                continue
            seen[r, c] = True
            for dr, dc in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                rr, cc = r + dr, c + dc
                if 0 <= rr < rows and 0 <= cc < cols and not seen[rr, cc] and free[rr, cc] and abs(floor[rr, cc] - floor[r, c]) <= CLIMB:
                    stack.append((rr, cc))
        return seen

    spawn, exit_ = cell(cfg["spawn"]), cell(cfg["exit"])
    reach = flood(has_floor & ~obstacle, spawn)
    blocked = ~reach
    grown = blocked.copy()
    k = int(math.ceil(RADIUS / CELL))
    for dr in range(-k, k + 1):
        for dc in range(-k, k + 1):
            if dr * dr + dc * dc > (RADIUS / CELL) ** 2 + 1e-9:
                continue
            src = blocked[max(0, -dr):rows - max(0, dr), max(0, -dc):cols - max(0, dc)]
            grown[max(0, dr):max(0, dr) + src.shape[0], max(0, dc):max(0, dc) + src.shape[1]] |= src
    walk = flood(~grown, spawn)
    report = {"cols": cols, "rows": rows, "min_x": float(lo[0]), "min_z": float(lo[1]), "walkable": int(walk.sum()),
              "spawn_free": bool(walk[spawn]), "exit_reached": bool(walk[exit_])}
    # Heights: the floor where known, spread outwards into cells without one; then averaged onto the 0.5 m grid.
    known = has_floor.copy()
    h = np.where(known, floor, 0.0)
    for _ in range(40):
        if known.all():
            break
        acc = np.zeros_like(h); cnt = np.zeros_like(h)
        for dr, dc in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            src_k = np.roll(known, (dr, dc), (0, 1)); src_h = np.roll(h, (dr, dc), (0, 1))
            acc += np.where(src_k, src_h, 0); cnt += src_k
        new = ~known & (cnt > 0)
        h[new] = acc[new] / cnt[new]
        known |= new
    f = int(round(HCELL / CELL))
    hrows, hcols = rows // f, cols // f
    coarse = h[:hrows * f, :hcols * f].reshape(hrows, f, hcols, f)
    wk = walk[:hrows * f, :hcols * f].reshape(hrows, f, hcols, f)
    use = np.where(wk.any(axis=(1, 3)), (coarse * wk).sum(axis=(1, 3)) / np.maximum(wk.sum(axis=(1, 3)), 1), coarse.mean(axis=(1, 3)))
    base = math.floor(use.min() / STEP) * STEP
    levels = np.clip(np.round((use - base) / STEP).astype(int), 0, 61)
    report.update({"height_base": base, "height_max": float(use.max()), "height_rows": hrows, "height_cols": hcols, "levels_used": int(levels.max())})
    alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"
    height_rows = ["".join(alphabet[v] for v in row) for row in levels]

    def rle(row):
        runs, last, count = [], None, 0
        for v in row:
            if v == last:
                count += 1
                continue
            if last is not None:
                runs.append(str(count) + ("." if last else "#"))
            last, count = v, 1
        runs.append(str(count) + ("." if last else "#"))
        return "".join(runs)

    walk_rows = [rle(r) for r in walk]
    json.dump({"report": report, "walk": walk_rows, "heights": height_rows}, open(out / "station-grid.json", "w"))
    np.save(out / "station-walk.npy", walk)
    np.save(out / "station-floor.npy", np.where(has_floor, floor, np.nan))
    print(json.dumps(report))


main()
