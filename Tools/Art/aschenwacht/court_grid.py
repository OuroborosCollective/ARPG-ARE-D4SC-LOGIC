"""Bake where the hero may stand in the bell court from the set's own models.

usage: python3 -I court_grid.py <extracted Aschenwacht folder> <layout.json> <out folder>

Every stone, wall and figure of the layout blocks the cells its geometry covers between knee and head height;
the court's ground decides where the island ends. The result is a grid of 0.2 m cells with the hero's radius
already added, written as text (for a look) and as the rows the game's rules carry.
"""
import json, math, struct, sys, pathlib
import numpy as np

CELL, HALF, RADIUS = 0.2, 12.0, 0.5           # metres: cell, half extent of the grid, hero radius
LOW, HIGH = 0.45, 1.7                          # what stands between these heights blocks
OFFSET_X = 0.55                                # the pilgrim path runs along x = 0 in the game
N = int(round(2 * HALF / CELL))

def glb_meshes(path):
    data = pathlib.Path(path).read_bytes()
    size, kind = struct.unpack_from("<I4s", data, 12)
    g = json.loads(data[20:20 + size]); assert not g.get("extensionsRequired"), g.get("extensionsRequired")
    bin_start = 20 + size + 8
    def accessor(index):
        a = g["accessors"][index]; v = g["bufferViews"][a["bufferView"]]
        comps = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}[a["type"]]
        dtype = {5123: "<u2", 5125: "<u4", 5126: "<f4", 5121: "u1"}[a["componentType"]]
        start = bin_start + v.get("byteOffset", 0) + a.get("byteOffset", 0)
        stride = v.get("byteStride", 0)
        item = np.dtype(dtype).itemsize * comps
        if stride and stride != item:
            raw = np.frombuffer(data, dtype="u1", count=stride * a["count"], offset=start).reshape(a["count"], stride)[:, :item]
            return np.frombuffer(raw.tobytes(), dtype=dtype).reshape(a["count"], comps)
        return np.frombuffer(data, dtype=dtype, count=a["count"] * comps, offset=start).reshape(a["count"], comps)
    out = {}
    for node in g["nodes"]:
        if "mesh" not in node: continue
        name = node["name"].split(".")[0]
        if name in out: continue
        tris = []
        for prim in g["meshes"][node["mesh"]]["primitives"]:
            pos = accessor(prim["attributes"]["POSITION"]).astype(np.float64)
            idx = accessor(prim["indices"]).reshape(-1, 3) if "indices" in prim else np.arange(len(pos)).reshape(-1, 3)
            tris.append(pos[idx])
        out[name] = np.concatenate(tris)
    return out

def place(tris, piece):
    """glTF model space -> the game's world: mirror X, scale, turn about Y (Unity's sense), move."""
    p = tris.copy(); p[..., 0] *= -1
    p *= piece["scale"]
    a = math.radians(piece["yaw"]); c, s = math.cos(a), math.sin(a)
    x, z = p[..., 0].copy(), p[..., 2].copy()
    p[..., 0] = x * c + z * s + piece["x"] + OFFSET_X
    p[..., 2] = -x * s + z * c + piece["z"]
    p[..., 1] += piece["y"]
    return p

def cells(points):
    ij = np.floor((points[:, [0, 2]] + HALF) / CELL).astype(int)
    ok = (ij >= 0).all(axis=1) & (ij < N).all(axis=1)
    return ij[ok]

def sample(tris, low, high):
    """Points on the triangles, about half a cell apart, kept where their height lies in the band."""
    keep = (tris[..., 1].max(axis=1) >= low) & (tris[..., 1].min(axis=1) <= high)
    tris = tris[keep]; pts = []
    edges = np.maximum.reduce([np.linalg.norm(tris[:, 0] - tris[:, 1], axis=1), np.linalg.norm(tris[:, 1] - tris[:, 2], axis=1), np.linalg.norm(tris[:, 2] - tris[:, 0], axis=1)])
    steps = np.clip(np.ceil(edges / (CELL / 2)).astype(int), 1, 400)
    for n in np.unique(steps):
        group = tris[steps == n]
        u, v = np.meshgrid(np.arange(n + 1), np.arange(n + 1)); mask = (u + v) <= n
        u, v = u[mask] / n, v[mask] / n
        p = group[:, None, 0] * (1 - u - v)[None, :, None] + group[:, None, 1] * u[None, :, None] + group[:, None, 2] * v[None, :, None]
        pts.append(p.reshape(-1, 3))
    pts = np.concatenate(pts) if pts else np.zeros((0, 3))
    return pts[(pts[:, 1] >= low) & (pts[:, 1] <= high)]

def main():
    source, layout_path, out = pathlib.Path(sys.argv[1]), sys.argv[2], pathlib.Path(sys.argv[3])
    layout = json.load(open(layout_path))["pieces"]
    models = glb_meshes(source / "Glockenhof.glb")          # every family once, in model space as the scene carries it
    solid = np.zeros((N, N), dtype=bool); ground = np.zeros((N, N), dtype=bool)
    for piece in layout:
        name = piece["model"]
        if name.endswith("gras") or name.startswith("Pilgerweg"): continue
        world = place(models[name], piece)
        if name.startswith("Gelaende"):
            ij = cells(sample(world, -5, 5)); ground[ij[:, 1], ij[:, 0]] = True
            continue
        ij = cells(sample(world, LOW, HIGH)); solid[ij[:, 1], ij[:, 0]] = True
    reach = int(math.ceil(RADIUS / CELL))
    blocked = ~ground
    for dz in range(-reach, reach + 1):
        for dx in range(-reach, reach + 1):
            if (dx * dx + dz * dz) * CELL * CELL > (RADIUS + CELL * 0.5) ** 2: continue
            shifted = np.zeros_like(solid); src = solid[max(0, -dz):N - max(0, dz), max(0, -dx):N - max(0, dx)]
            shifted[max(0, dz):max(0, dz) + src.shape[0], max(0, dx):max(0, dx) + src.shape[1]] = src
            blocked |= shifted
    # the island's rim: keep the hero half a metre inside the ground's edge
    rim = np.zeros_like(blocked); outside = ~ground
    for dz in range(-2, 3):
        for dx in range(-2, 3):
            src = outside[max(0, -dz):N - max(0, dz), max(0, -dx):N - max(0, dx)]
            rim[max(0, dz):max(0, dz) + src.shape[0], max(0, dx):max(0, dx) + src.shape[1]] |= src
    blocked |= rim
    spawn, exit_ = (0.0, -4.0), (0.0, 8.0)
    def cell(p): return int((p[1] + HALF) / CELL), int((p[0] + HALF) / CELL)
    seen = np.zeros_like(blocked); stack = [cell(spawn)]
    while stack:
        r, c = stack.pop()
        if r < 0 or c < 0 or r >= N or c >= N or seen[r, c] or blocked[r, c]: continue
        seen[r, c] = True; stack += [(r + 1, c), (r - 1, c), (r, c + 1), (r, c - 1)]
    rows = ["".join("#" if blocked[r, c] else ("." if seen[r, c] else ":") for c in range(N)) for r in range(N)]
    out.mkdir(parents=True, exist_ok=True)
    (out / "court-grid.txt").write_text("\n".join(reversed(rows)) + "\n")      # north at the top
    json.dump({"cell": CELL, "half": HALF, "rows": ["".join("#" if blocked[r, c] or not seen[r, c] else "." for c in range(N)) for r in range(N)], "spawn": spawn, "exit": exit_},
              open(out / "court-grid.json", "w"))
    print("cells", N, "x", N, "walkable", int(seen.sum()), "spawn free", not blocked[cell(spawn)], "exit reached", bool(seen[cell(exit_)]), "unreachable free cells", int((~blocked & ~seen).sum()))

main()
