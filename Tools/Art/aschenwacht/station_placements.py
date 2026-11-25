"""Write the placement table of Aschenwacht for the Unity builder (Assets/Editor/AschenwachtCourtBuilder.cs).

usage: python3 -I station_placements.py <part 2 root> <Glockenhof.glb> <out.tsv>

Every row places one model under a parent transform in the game frame (x = -X, y = Y, z = Z of the forecourt's glTF frame).
The model itself is the reduced FBX as Unity imports it: its vertices arrive as (X, Y, -Z) of the model's glTF frame (M), so a
parent rotation R_u = F * R_glTF * M^-1 with F = diag(-1, 1, 1) places it exactly as the source scene does.
Columns: group, set (I or II), model, px, py, pz, qx, qy, qz, qw, sx, sy, sz.
"""
import csv
import json
import math
import pathlib
import struct
import sys

import numpy as np

F = np.diag([-1.0, 1.0, 1.0])
M_INV = np.diag([1.0, 1.0, -1.0])
C = np.array([[1.0, 0, 0], [0, 0, 1.0], [0, -1.0, 0]])          # Blender (Z up) -> glTF (Y up)
DOCK_R, DOCK_T = np.diag([-1.0, 1.0, -1.0]), np.array([0.55, 1.195, 60.33])  # the bell court, turned half a turn
# Owner decision 2026-11-25: the side gate to the Brennerhof stands in the forecourt's east wall; that wall piece gives way to it
# (the runtime puts up the gate's arch, KilnYard.SideGateXmm/Zmm).
SIDE_GATE = (40.8, 3.0)


def quaternion(m):
    """Unit quaternion (x, y, z, w) of a proper rotation matrix (column-vector convention, as Unity's Matrix4x4.Rotate)."""
    t = m[0, 0] + m[1, 1] + m[2, 2]
    if t > 0:
        s = math.sqrt(t + 1.0) * 2
        q = ((m[2, 1] - m[1, 2]) / s, (m[0, 2] - m[2, 0]) / s, (m[1, 0] - m[0, 1]) / s, 0.25 * s)
    elif m[0, 0] > m[1, 1] and m[0, 0] > m[2, 2]:
        s = math.sqrt(1.0 + m[0, 0] - m[1, 1] - m[2, 2]) * 2
        q = (0.25 * s, (m[0, 1] + m[1, 0]) / s, (m[0, 2] + m[2, 0]) / s, (m[2, 1] - m[1, 2]) / s)
    elif m[1, 1] > m[2, 2]:
        s = math.sqrt(1.0 + m[1, 1] - m[0, 0] - m[2, 2]) * 2
        q = ((m[0, 1] + m[1, 0]) / s, 0.25 * s, (m[1, 2] + m[2, 1]) / s, (m[0, 2] - m[2, 0]) / s)
    else:
        s = math.sqrt(1.0 + m[2, 2] - m[0, 0] - m[1, 1]) * 2
        q = ((m[0, 2] + m[2, 0]) / s, (m[1, 2] + m[2, 1]) / s, 0.25 * s, (m[1, 0] - m[0, 1]) / s)
    n = math.sqrt(sum(v * v for v in q))
    return tuple(v / n for v in q)


def quat_matrix(q):
    x, y, z, w = q
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def row(group, part, model, position, rotation, scale):
    if abs(np.linalg.det(rotation) - 1.0) > 1e-4:
        sys.exit("not a rotation for " + model)
    q = quaternion(rotation)
    return [group, part, model] + ["%.5f" % v for v in position] + ["%.6f" % v for v in q] + ["%.5f" % v for v in scale]


def main():
    root, court, out = pathlib.Path(sys.argv[1]), sys.argv[2], sys.argv[3]
    rows = []
    for r in csv.DictReader(open(root / "Dokumentation/Instanzen.csv", encoding="utf-8")):
        rx, ry, rz = (float(r[k]) for k in ("rx_rad", "ry_rad", "rz_rad"))
        cx, sx, cy, sy, cz, sz = math.cos(rx), math.sin(rx), math.cos(ry), math.sin(ry), math.cos(rz), math.sin(rz)
        rot_b = (np.array([[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]]) @ np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]]) @
                 np.array([[1, 0, 0], [0, cx, -sx], [0, sx, cx]]))
        t_b = np.array([float(r["blender_x_m"]), float(r["blender_y_m"]), float(r["blender_z_m"])])
        at = F @ C @ t_b
        if r["gruppe"] == "Umfriedung" and abs(at[0] - SIDE_GATE[0]) < 0.3 and abs(at[2] - SIDE_GATE[1]) < 0.3:
            continue
        rows.append(row(r["gruppe"], "II", r["modell"], F @ C @ t_b, F @ C @ rot_b @ C.T @ M_INV,
                        (float(r["sx"]), float(r["sz"]), float(r["sy"]))))
    # The terraced floor carries every other placement; the table of placements leaves it out, the scene has it at the origin.
    rows.append(row("Boden", "II", "Hofboden_84x92", np.zeros(3), F @ np.eye(3) @ M_INV, (1, 1, 1)))
    rows.append(row("Anschluss", "II", "Anschluss_Glockenhof", F @ np.array([0.0, 1.195, 46.0]), F @ np.eye(3) @ M_INV, (1, 1, 1)))
    rows.append(row("Fernkulisse", "II", "Fernkulisse", np.zeros(3), F @ np.eye(3) @ M_INV, (1, 1, 1)))
    # The Aschbeter keeps his place on the procession path but faces the crypt portal he guards.
    rows.append(row("Figuren", "II", "Aschbeter", F @ np.array([0.0, 0.06, 8.0]), np.eye(3), (1, 1, 1)))
    data = pathlib.Path(court).read_bytes()
    size, _ = struct.unpack_from("<I4s", data, 12)
    g = json.loads(data[20:20 + size])
    for node in g["nodes"]:
        if "mesh" not in node:
            continue
        family = node["name"].split(".")[0]
        s = node.get("scale", [1, 1, 1])
        if max(s) - min(s) > 1e-5:
            sys.exit("non-uniform scale in the bell court: " + node["name"])
        t = np.array(node.get("translation", [0, 0, 0]), dtype=float)
        rq = quat_matrix(node.get("rotation", [0, 0, 0, 1]))
        rows.append(row("Glockenhof", "I", family, F @ (DOCK_R @ t + DOCK_T), F @ DOCK_R @ rq @ M_INV, (s[0], s[0], s[0])))
    with open(out, "w", encoding="utf-8", newline="") as f:
        f.write("group\tset\tmodel\tpx\tpy\tpz\tqx\tqy\tqz\tqw\tsx\tsy\tsz\n")
        for r in rows:
            f.write("\t".join(r) + "\n")
    print("placements", len(rows))


main()
