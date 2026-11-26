"""Bake where the wanderer may walk in the Brennerhof (Aschenwacht's side yard) and how high the ground is.

usage: python3 -I kiln_grid.py <bake folder> <spawn x> <spawn z> <exit x> <exit z>   (metres, the yard's own frame)

Input, written in the editor from the pack's own scene (private pipeline): scene-dump.json indexes the scene's colliders,
colliders.bin holds their world triangles (float32, nine per triangle). The game uses the yard's frame unchanged: x east,
z north, the south gate in the south.

The gate of the kiln passage is left out: the walk map treats the passage as open, the rules (KilnYard.InGate) close it until
the bolt is drawn. Triggers and the walker's own controller are left out too.

Per cell of 0.25 m, as for Aschenwacht (Tools/Art/aschenwacht/station_grid.py): the floor is the top of the low stack of
upward faces; anything between 0.40 and 1.85 m above that floor blocks; neighbouring floors may differ by at most 0.40 m;
only what the spawn reaches counts, and only within the yard and its approach (KEEP); the wanderer's radius of 0.45 m (the rules' PlayerRadiusMm) is
added to every obstacle. Heights are kept on a 0.5 m
grid in steps of 5 cm. Writes kiln-grid.json (same layout as station-grid.json) into the bake folder.
"""
import json
import math
import pathlib
import sys

import numpy as np

CELL, HCELL, STEP = 0.25, 0.5, 0.05
LOW, HIGH, CLIMB, RADIUS = 0.40, 1.85, 0.40, 0.45  # the rules' PlayerRadiusMm
OPEN_ROLES = ("Ofengitter",)
# The pack's terrain reaches 134 x 133 m into the hills. The wanderer keeps to the yard inside its walls (side walls at x = +-13,
# south wall at z = -12, north wall at z = 32, the walls themselves still block) and to the paved approach before the south
# gate, where Aschenwacht's side gate lets him in; the hills and the abbey stay horizon. Boxes: (x0, z0, x1, z1) in metres.
KEEP = ((-12.8, -17.5, 12.8, 33.2), (-6.0, -26.0, 6.0, -17.0))


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
    bake = pathlib.Path(sys.argv[1])
    spawn_m = (float(sys.argv[2]), float(sys.argv[3]))
    exit_m = (float(sys.argv[4]), float(sys.argv[5]))
    dump = json.loads((bake / "scene-dump.json").read_text(encoding="utf-8"))
    raw = np.fromfile(bake / "colliders.bin", dtype="<f4").astype(np.float64).reshape(-1, 3, 3)
    parts, left_out = [], []
    for c in dump["colliders"]:
        if c["trigger"] or not c["enabled"] or c["role"] in OPEN_ROLES:
            left_out.append(c["path"])
            continue
        parts.append(raw[c["start"]:c["start"] + c["count"]])
    tris = np.concatenate(parts)
    # Colliders may be one-sided and wound either way: a face counts as level by the size of its normal's y, not its sign.
    # A ceiling never becomes floor, because the floor is taken from the low stack above the lowest level face of a cell.
    pts, nys = samples(tris, CELL * 0.4)
    nys = np.abs(nys)
    lo = np.floor(pts[:, [0, 2]].min(axis=0) / HCELL) * HCELL - 1.0
    hi = np.ceil(pts[:, [0, 2]].max(axis=0) / HCELL) * HCELL + 1.0
    cols, rows = int(round((hi[0] - lo[0]) / CELL)), int(round((hi[1] - lo[1]) / CELL))
    ci = np.clip(((pts[:, 0] - lo[0]) / CELL).astype(int), 0, cols - 1)
    ri = np.clip(((pts[:, 2] - lo[1]) / CELL).astype(int), 0, rows - 1)
    flat = ri * cols + ci
    lowest = np.full(rows * cols, np.inf)
    up = nys > 0.6
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
    zz, xx = np.mgrid[0:rows, 0:cols]
    cx, cz = lo[0] + (xx + .5) * CELL, lo[1] + (zz + .5) * CELL
    inside = np.zeros_like(obstacle)
    for x0, z0, x1, z1 in KEEP:
        inside |= (cx >= x0) & (cx <= x1) & (cz >= z0) & (cz <= z1)
    obstacle |= ~inside

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

    spawn, exit_ = cell(spawn_m), cell(exit_m)
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
              "spawn_free": bool(walk[spawn]), "exit_reached": bool(walk[exit_]), "left_out": left_out}
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

    json.dump({"report": report, "walk": [rle(r) for r in walk], "heights": height_rows}, open(bake / "kiln-grid.json", "w"))
    np.save(bake / "kiln-walk.npy", walk)
    np.save(bake / "kiln-floor.npy", np.where(has_floor, floor, np.nan))
    print(json.dumps({k: v for k, v in report.items() if k != "left_out"}), "left out:", len(left_out))


main()
