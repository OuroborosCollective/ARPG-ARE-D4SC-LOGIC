"""Check actual WFH1 support contacts, sills and line of sight against its emitted terrain.

Axes in the evidence are east, north, up. Positive sole clearance means open air.
Face indices are zero-based positions in the decoded WFH1 stream. Exit 1 on a failed
contact. An archived report without face ranges requires its original --legacy-source.
"""
import argparse
from collections import Counter
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import struct
import sys

import numpy as np

from qa_map import decode_glb


# Independent contact areas taken from the physical load-bearing elements. No roofs,
# waterwheels, arch spans, hanging bells, scales, or elevated timber floors are feet.
# x, y, width, depth are in asset coordinates. Most plinths have a zero underside.
def slabs(kind, legacy=False):
    if kind == "village" and not legacy:
        return [("west_house", -2.06, 1.65, 6.01, 7.11, -.13, -.16),
                ("east_house", 3.36, 1.87, 4.11, 5.80, .13, -.16),
                ("south_house", -.73, -2.66, 4.42, 3.80, .07, -.16)]
    if kind == "village":
        return [("west_house", -2, 1.60, 5.90, 6.95),
                ("east_house", 3.30, 1.88, 4, 5.66),
                ("south_house", -.72, -2.64, 4.22, 3.64)]
    if kind == "mill":
        return [("mill_house", .84, 1.12, 8.57, 6.70),
                ("west_headrace_foot", -8.55, -2.42, 1.02, 1.34, 0., .02 if legacy else -.14),
                ("east_headrace_foot", -6.44, -2.42, 1.02, 1.34, 0., .02 if legacy else -.14)]
    if kind == "granary":
        return [(f"stilt_{i}_{j}", x, y, 1.24, 1.21)
                for i, x in enumerate((-3.13, 0, 3.13))
                for j, y in enumerate((-2.52, 2.52))]
    if kind == "aqueduct":
        return [(f"pier_{i+1}", (i-2)*4.12, 0, 1.69, 2.51) for i in range(5)]
    fixtures = {"bell": ("tower", 0, 0, 7.35, 7.35),
                "kiln": ("furnace", -.48, .85, 11, 6.95),
                "toll": ("weighhouse", -.76, 1.04, 10.02, 6.70),
                "abbey": ("nave", 0, .84, 11.62, 12.64),
                "road_shrine": ("shrine_stair", 0, .25, 5.49, 5.49*.79, 0., .01 if legacy else -.08),
                "mill_vault": ("cistern_base", 0, 1.10, 10.20, 9.80)}
    return [fixtures[kind]] if kind in fixtures else []


def need(condition, message):
    if not condition:
        raise ValueError(message)


def rnd(value):
    return round(float(value), 6)


def filehash(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def horizontal(p, mark):
    yaw = math.radians(mark["bearing"])
    twist = math.radians(mark["turn"] - mark["bearing"])
    s, c = math.sin(twist), math.cos(twist)
    center = mark["distance"] * np.asarray([math.sin(yaw), math.cos(yaw)])
    return np.asarray(p) @ (mark["scale"] * np.array([[c, s], [-s, c]])) + center


def read_native(path):
    raw = path.read_bytes()
    magic, n = struct.unpack_from("<4sI", raw)
    need(magic == b"WFH1" and n > 0 and len(raw) == 8 + 48*n, str(path))
    records = np.frombuffer(raw, offset=8, dtype=[("pos", "<f4", 3), ("rgba", "u1", 4)])
    xyz = records["pos"].reshape(n, 3, 3)
    colors = records["rgba"].reshape(n, 3, 4)
    need(np.isfinite(xyz).all() and (colors[:, :, 3] == colors[:, :1, 3]).all(),
         f"{path}: invalid triangle or material zone")
    return xyz, colors, hashlib.sha256(raw).hexdigest()


class Rays:
    """Barycentric vertical ray and finite segment tests on real exported faces."""
    def __init__(self, triangles, start=0):
        self.t = np.asarray(triangles, dtype=np.float64)
        self.start = int(start)
        self.a = self.t[:, 0]
        self.e = self.t[:, 1] - self.a
        self.f = self.t[:, 2] - self.a
        self.normals = np.cross(self.e, self.f)
        self.det = self.normals[:, 2]
        self.low = self.t[:, :, :2].min(axis=1)
        self.high = self.t[:, :, :2].max(axis=1)

    def hits(self, point):
        x, y = point
        keep = ((self.low[:, 0] <= x+1e-5) & (self.high[:, 0] >= x-1e-5) &
                (self.low[:, 1] <= y+1e-5) & (self.high[:, 1] >= y-1e-5) &
                (np.abs(self.det) > 1e-10))
        idx = np.flatnonzero(keep)
        dx = x - self.a[idx, 0]
        dy = y - self.a[idx, 1]
        u = (dx*self.f[idx, 1]-dy*self.f[idx, 0])/self.det[idx]
        v = (self.e[idx, 0]*dy-self.e[idx, 1]*dx)/self.det[idx]
        inside = (u >= -1e-5) & (v >= -1e-5) & (u+v <= 1.00001)
        idx, u, v = idx[inside], u[inside], v[inside]
        z = self.a[idx, 2] + u*self.e[idx, 2] + v*self.f[idx, 2]
        return np.stack((idx+self.start, z), axis=1)

    def footprint_extrema(self, corners):
        """Clip the real terrain triangles to a convex footing and return both extrema."""
        lower, upper = corners.min(axis=0), corners.max(axis=0)
        possible = np.flatnonzero(np.all((self.low <= upper) & (self.high >= lower), axis=1))
        low = (math.inf, None)
        high = (-math.inf, None)
        for idx in possible:
            poly = self.t[idx].tolist()
            for a, b in zip(corners, np.roll(corners, -1, axis=0)):
                if not poly:
                    break
                edge = b-a

                def signed(point):
                    return edge[0]*(point[1]-a[1])-edge[1]*(point[0]-a[0])

                clipped = []
                first = poly[-1]
                df = signed(first)
                for last in poly:
                    dl = signed(last)
                    if (df < 0) != (dl < 0):
                        factor = df/(df-dl)
                        clipped.append([first[k]+factor*(last[k]-first[k]) for k in range(3)])
                    if dl >= 0:
                        clipped.append(last)
                    first, df = last, dl
                poly = clipped
            for point in poly:
                if point[2] < low[0]:
                    low = (point[2], point[:2])
                if point[2] > high[0]:
                    high = (point[2], point[:2])
        if low[1] is None:
            raise ValueError(f"No emitted ground below footing {corners}")
        return low[1], high[1]

    def component_ids(self):
        """Unite triangles sharing real edges. Adjacent but independent shells retain separate interiors."""
        if hasattr(self, "_components"):
            return self._components
        _, vertices = np.unique(np.round(self.t.reshape(-1, 3), 5), axis=0, return_inverse=True)
        vertices = vertices.reshape(-1, 3)
        pairs = np.stack((vertices, np.roll(vertices, -1, axis=1)), axis=-1).reshape(-1, 2)
        pairs.sort(axis=1)
        order = np.lexsort((pairs[:, 1], pairs[:, 0]))
        equal = np.all(pairs[order[1:]] == pairs[order[:-1]], axis=1)
        parent = list(range(len(self.t)))

        def root(index):
            while parent[index] != index:
                parent[index] = parent[parent[index]]
                index = parent[index]
            return index

        for a, b in zip(order[:-1][equal]//3, order[1:][equal]//3):
            ra, rb = root(a), root(b)
            if ra != rb:
                parent[rb] = ra
        self._components = np.asarray([root(i) for i in range(len(self.t))])
        return self._components

    def emitted_mark(self, point, tolerance):
        """Locate even an uncapped rim/steep face by its triangle plane and barycentrics."""
        keep = np.all((self.low-tolerance <= point[:2]) & (point[:2] <= self.high+tolerance), axis=1)
        ids = np.flatnonzero(keep)
        if not len(ids):
            return None
        normal = self.normals[ids]
        n2 = np.einsum("ij,ij->i", normal, normal)
        delta = point - self.a[ids]
        raw = np.einsum("ij,ij->i", delta, normal)
        plane = np.abs(raw)/np.sqrt(n2)
        projected = point - normal*(raw/n2)[:, None]
        # Signed areas work even when a triangle projects to one line on XY.
        relative = projected - self.a[ids]
        u = np.einsum("ij,ij->i", np.cross(relative, self.f[ids]), normal)/n2
        v = np.einsum("ij,ij->i", np.cross(self.e[ids], relative), normal)/n2
        inside = (u >= -2.e-4) & (v >= -2.e-4) & (u+v <= 1.0002) & (plane <= tolerance)
        sel = np.flatnonzero(inside)
        if not len(sel):
            return None
        k = sel[plane[sel].argmin()]
        return int(ids[k])+self.start, float(projected[k, 2])

    def obstruction(self, eye, target):
        """Nearest true terrain hit strictly between the camera and target."""
        ray = np.asarray(target) - eye
        length = np.linalg.norm(ray)
        subset = np.flatnonzero(np.all((np.minimum(eye[:2], target[:2]) <= self.high) &
                                       (np.maximum(eye[:2], target[:2]) >= self.low), axis=1))
        e, f = self.e[subset], self.f[subset]
        p = np.cross(ray, f)
        det = np.einsum("ij,ij->i", e, p)
        valid = np.abs(det) > 1e-8
        s = eye - self.a[subset]
        q = np.cross(s, e)
        inv = np.divide(1.0, det, out=np.zeros_like(det), where=valid)
        u = inv*np.einsum("ij,ij->i", s, p)
        v = inv*(q @ ray)
        t = inv*np.einsum("ij,ij->i", f, q)
        mask = valid & (u >= -1e-8) & (v >= -1e-8) & (u+v <= 1.00000001)
        mask &= (t > .015/length) & (t < 1 - .10/length)
        if not mask.any():
            return None
        idx = np.flatnonzero(mask)
        j = idx[np.argmin(t[idx])]
        return {"face": int(subset[j])+self.start, "aheadOfTargetM": rnd((1-t[j])*length)}


def terrain_hit(surface, point):
    hits = surface.hits(point)
    if not len(hits):
        raise ValueError(f"No emitted terrain at {point}")
    return hits[hits[:, 1].argmax()]


def _contact(surface, member, mark, local, mode, label, world_xy=None):
    lx, ly, lz = local
    xy = horizontal([lx, ly], mark) if world_xy is None else np.asarray(world_xy)
    terrain_face, terrain_z = terrain_hit(surface, xy)
    hits = member.hits(xy)
    scale = mark["scale"]
    hint = mark["originZ"] + lz*scale
    out = {"type": mode, "name": label, "xy": [rnd(n) for n in xy],
           "terrainFace": int(terrain_face), "terrainZ": rnd(terrain_z)}
    if mode == "slab":
        if not len(hits):
            return dict(out, issue="no_member_face_at_support")
        # Choose the physical underside closest to the specified course. Other
        # independently founded parts can overlap the same footprint in projection.
        member_face, z = hits[abs(hits[:, 1]-hint).argmin()]
    else:
        on_face = member.emitted_mark(np.r_[xy, hint], .002*scale+.00003)
        if on_face is None:
            if len(hits):
                face, z = hits[abs(hits[:, 1]-hint).argmin()]
                out.update(nearestMemberFace=int(face), nearestMemberZ=rnd(z))
            return dict(out, issue="sill_face_missing" if mode == "sill" else "support_face_missing")
        member_face, z = on_face
    clearance = z-terrain_z
    out.update(memberFace=int(member_face), memberZ=rnd(z), clearanceM=rnd(clearance))
    if mode == "sill":
        out["issue"] = "sill_clipped" if clearance < .04*scale-1e-4 else None
    else:
        # Negative nominal Z is an intentional buried root or structural footing.
        allowed_embed = max(0., -lz)*scale + .12*scale + .035
        if clearance > .03*scale+.0001:
            out["issue"] = "floating"
        elif clearance < -allowed_embed-1e-4:
            out["issue"] = "buried"
        else:
            out["issue"] = None
    return out


def _joint_ray(member, xy, lower_z, upper_z):
    hits = member.hits(xy)
    if not len(hits):
        return upper_z-lower_z, lower_z, upper_z, ()
    component = member.component_ids()
    by_shell = {}
    for rawface, z in hits:
        face = int(rawface)
        idx = face-member.start
        count = 1 if member.det[idx] < 0 else -1
        by_shell.setdefault(component[idx], []).append((z, count, face))
    occupied = []
    event_faces = []
    for events in by_shell.values():
        events.sort()
        levels = []
        for z, sign, face in events:
            if lower_z <= z <= upper_z:
                event_faces.append(face)
            # A pair of triangles covering one plane can intersect on a shared diagonal.
            if levels and abs(levels[-1][0]-z) < 2e-5:
                levels[-1][1].add(sign)
            else:
                levels.append((z,{sign}))
        depth = 0
        for (z, signs), (next_z, _) in zip(levels, levels[1:]):
            depth += sum(signs)
            if depth and z < upper_z and next_z > lower_z:
                occupied.append((max(z,lower_z), min(next_z,upper_z)))
    # A one-sided open wall/cap contributes no solid interval of its own and
    # cannot cancel the volume of another independent overlapping member.
    cursor = lower_z
    gaps = []
    for begin, end in sorted(occupied):
        if begin > cursor:
            gaps.append((begin-cursor, cursor, begin))
        cursor = max(end, cursor)
    if cursor < upper_z:
        gaps.append((upper_z-cursor, cursor, upper_z))
    return (*max(gaps, default=(0., lower_z, lower_z)), event_faces)


def _interface(member, mark, probe):
    name, x, y, below, above = probe
    scale = mark["scale"]
    xy = horizontal([x,y], mark)
    lower_z, upper_z = [mark["originZ"] + v*scale for v in (below, above)]
    # Tiny ray offsets avoid counting both triangles on shared face diagonals.
    # A median guards against the one diagonal a probe may coincidentally follow.
    shifts = np.asarray(((.00061,.00037),(-.00042,.00053),(.00041,-.00063)))*scale
    candidates = sorted((_joint_ray(member, xy+q, lower_z, upper_z) for q in shifts), key=lambda r:r[0])
    gap, a, b, faces = candidates[1]
    return {"type": "structure_joint", "name": name, "xy": [rnd(v) for v in xy],
            "interval": [rnd(lower_z), rnd(upper_z)], "maxEmptyM": rnd(gap),
            "emptyInterval": [rnd(a),rnd(b)] if gap > 0 else None, "crossingFaces": faces,
            "issue": "open_joint" if gap > .01*scale+.0001 else None}


def sight_lines(rec, mark, member, surface):
    # Matched to the default proof camera by render_views: 12 m forward,
    # 3.6 m or 5 m eye height. Turn toward each motif, including the reverse view.
    cave = rec["style"] in {"crypt", "nest", "bear", "rift", "vault"}
    bearing = math.radians(mark["bearing"])
    eye = np.array([12*math.sin(bearing), 12*math.cos(bearing), 3.6 if cave else 5.0])
    vertices = member.t.reshape(-1, 3)
    highest = vertices[vertices[:, 2].argmax()]
    base = horizontal([0, 0], mark)
    targets = {"upperSilhouette": highest, "localDatumPlus1m": np.r_[base, mark["originZ"]+1.]}
    sight = {}
    for name, target in targets.items():
        hit = surface.obstruction(eye, target)
        ground = terrain_hit(surface, target[:2])[1]
        sight[name] = {"target": [rnd(n) for n in target], "terrainAtTargetZ": rnd(ground),
                       "targetInsideTerrain": bool(target[2] < ground-.02),
                       "nearestBlockingTerrain": hit}
    return sight


def _prove_glb(path, xyz, rgba, tiles):
    groups, _ = decode_glb(path, tiles)
    native_material = rgba[:, 0, 3]
    need(set(np.unique(native_material)) == set(groups), f"{path}: material identity")
    for tag, (imported, rgb) in groups.items():
        keep = native_material == tag
        need(np.array_equal(imported, xyz[keep]), f"{path}: geometry differs for material {tag}")
        need(np.array_equal(rgb, rgba[keep, :, :3]), f"{path}: colors differ for material {tag}")
    return filehash(path)


def verify_scene(rec, metadata, mesh_path, glb_path=None, tiles=None, anchors=None):
    """Accept decoded provenance only for partitioning; ray tests read real output bytes."""
    xyz, rgba, sha = read_native(Path(mesh_path))
    need(metadata["sha256"] == sha and metadata["triangles"] == len(xyz), "WFH1 receipt mismatch")
    art = xyz[:, :, [0, 2, 1]].astype(float)
    n, all_land = metadata["surfaceTriangles"], metadata["terrainTriangles"]
    need(0 < n <= all_land < len(art), "wrong terrain partition")
    surface = Rays(art[:n])
    scene = {"id": rec["id"], "planned": rec["planned"], "wfhSHA256": sha,
             "surfaceTriangles": n, "terrainTriangles": all_land, "triangles": len(xyz), "objects": []}
    if glb_path:
        scene["bitExactGlbSHA256"] = _prove_glb(Path(glb_path), xyz, rgba, tiles)
    entries = metadata["objects"]
    authored = rec["landmarks"] + rec["dressing"]
    need(len(entries) == len(authored), "catalog or provenance object count")
    offset = all_land
    for mark, placement in zip(entries, authored):
        for k, v in placement.items():
            original = mark.get("originalDistance", mark["distance"]) if k == "distance" else mark[k]
            need(original == v, f"{rec['id']}: object order/transform mismatch at {k}")
        first, count = mark["firstTriangle"], mark["triangleCount"]
        need(first == offset and 0 < count <= len(art)-first, "wrong triangle partition")
        offset += count
        member = Rays(art[first:offset], first)
        kind = mark["kind"]
        probes = []
        for label, x, y, w, d, *profile in slabs(kind, legacy=metadata.get("originalLayout",False)):
            angle, z = profile if profile else (0., 0.)
            c, s = math.cos(angle), math.sin(angle)
            for i in (-1, 0, 1):
                for j in (-1, 0, 1):
                    u, v = .475*i*w, .475*j*d
                    probes.append(_contact(surface, member, mark, (x+c*u-s*v, y+s*u+c*v, z),
                                           "slab", f"{label}[{i},{j}]"))
            # Terrain peaks/ravines can sit inside the raster. A linear height on
            # each clipped triangle attains its extrema on this polygon's vertices.
            box = np.array([[-w,-d],[w,-d],[w,d],[-w,d]])*.475
            corners = horizontal(box @ np.array([[c,s],[-s,c]])+[x,y], mark)
            extrema = surface.footprint_extrema(corners)
            for tag, pt in zip(("terrainLow","terrainHigh"), extrema):
                probes.append(_contact(surface, member, mark, (x,y,z), "slab",
                                       f"{label}[{tag}]", world_xy=pt))
        if anchors is not None:
            for mode, method in (("support", anchors.support_probes), ("sill", anchors.sill_probes)):
                for label, x, y, z in method(kind):
                    probes.append(_contact(surface, member, mark, (x, y, z), mode, label))
            if hasattr(anchors, "interface_probes"):
                for probe in anchors.interface_probes(kind):
                    probes.append(_interface(member, mark, probe))
        # Measure a real lowest near-horizontal face even on a natural/wall-mounted
        # assembly without a semantically defined structural probe. This observation
        # is not a verdict on whether that particular rock or suspended prop needs a plinth.
        centroids = member.t.mean(axis=1)
        selectable = np.flatnonzero(abs(member.det) > 1.e-6)
        j = selectable[centroids[selectable, 2].argmin()]
        at = centroids[j]
        ground_face, gz = terrain_hit(surface, at[:2])
        direction_delta = (mark["bearing"] - rec["bearing"] + 180) % 360 - 180
        obj = {"kind": kind, "bearing": mark["bearing"] % 360, "originZ": rnd(mark["originZ"]),
               "degreesFromMainView": rnd(direction_delta), "firstTriangle": first, "triangles": count,
               "lowestFaceSample": {"face": first+int(j), "xyz": [rnd(v) for v in at],
                                    "terrainFace": int(ground_face), "terrainZ": rnd(gz),
                                    "clearanceM": rnd(at[2]-gz)},
               "sightLines": sight_lines(rec, mark, member, surface), "probes": probes}
        scene["objects"].append(obj)
    need(offset == len(art), "incomplete object partition")
    return scene


def require_scene_contacts(rec, metadata, mesh_path):
    """Fail the asset build on real stream contacts; GLB is verified in the full CLI."""
    import ground_anchors
    scene = verify_scene(rec, metadata, mesh_path, anchors=ground_anchors)
    result = summarize([scene])
    if result["issues"]:
        failed = [(o["kind"], p["name"], p["issue"], p.get("clearanceM",p.get("maxEmptyM")))
                  for o in scene["objects"] for p in o["probes"] if p["issue"]]
        raise ValueError(f"{rec['id']} failed {result['issues']}: {failed[:10]}")
    return result["counts"]


def legacy_layout(rec, metadata, source, object_cache, mesh_path):
    # Original format had no part table. Prove the recovered ranges by exact
    # comparison of every released terrain/object POSITION value to its source.
    import build
    import landscape
    b = build.Builder()
    seed = int.from_bytes(hashlib.sha256(("weltferne/1/"+rec["id"]).encode()).digest()[:4], "little")
    landscape.terrain(b, rec["style"], seed)
    xyz, _, _ = read_native(mesh_path)
    native = xyz[:, :, [0, 2, 1]]
    count = len(b.tris)
    need(np.array_equal(np.asarray(b.tris, dtype="<f4"), native[:count]), "released terrain provenance")
    data = dict(metadata, originalLayout=True, surfaceTriangles=b.landscape_surface_count, terrainTriangles=count, objects=[])
    for placement in rec["landmarks"] + rec["dressing"]:
        kind = placement["kind"]
        if kind not in object_cache:
            object_cache[kind] = build.make_landmark(kind)
        b.append(object_cache[kind], **placement)
        end = len(b.tris)
        need(np.array_equal(np.asarray(b.tris[count:end], dtype="<f4"), native[count:end]),
             f"{rec['id']}: {kind} archived member provenance")
        data["objects"].append(dict(placement, firstTriangle=count, triangleCount=end-count, originZ=0))
        count = end
    need(count == len(native), "released stream did not match archived source")
    return data


def summarize(scenes):
    counters = Counter()
    kinds = Counter()
    issues = Counter()
    worst = {}
    for sc in scenes:
        for ob in sc["objects"]:
            counters["objects"] += 1
            kinds[ob["kind"]] += 1
            counters["outside35Degrees"] += abs(ob["degreesFromMainView"]) > 35
            upper = ob["sightLines"]["upperSilhouette"]
            counters["roofTargetUnderLocalTerrain"] += upper["targetInsideTerrain"]
            counters["roofTargetTerrainOccluded"] += upper["nearestBlockingTerrain"] is not None
            for probe in ob["probes"]:
                mode = probe["type"]
                counters[mode] += 1
                if probe["issue"]:
                    issues[probe["issue"]] += 1
                if "clearanceM" not in probe:
                    continue
                value = probe["clearanceM"]
                for key, sign in ((mode+"Highest", 1.), (mode+"Lowest", -1.)):
                    if key not in worst or value*sign > worst[key]["clearanceM"]*sign:
                        worst[key] = {"scene": sc["id"], "kind": ob["kind"], "bearing": ob["bearing"],
                                      "probe": probe["name"], "clearanceM": value}
    return {"scenes": len(scenes), "runtimeScenes": sum(not s["planned"] for s in scenes),
            "portableGeometryCompared": sum("bitExactGlbSHA256" in s for s in scenes),
            "counts": dict(sorted(counters.items())), "placementKinds": dict(sorted(kinds.items())),
            "issues": dict(sorted(issues.items())), "extrema": worst}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--catalog", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--meshes", type=Path, required=True)
    parser.add_argument("--studies", type=Path)
    parser.add_argument("--models", type=Path)
    parser.add_argument("--textures", type=Path)
    parser.add_argument("--legacy-source", type=Path)
    parser.add_argument("--anchors", type=Path, default=Path(__file__).with_name("ground_anchors.py"))
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--ids", nargs="+")
    args = parser.parse_args()
    if args.models and not args.textures:
        parser.error("--models needs --textures to check the embedded materials")
    authored = json.loads(args.catalog.read_text())
    metadata = {r["id"]: r for r in json.loads(args.report.read_text())}
    anchors = None
    if args.anchors.is_file():
        spec = importlib.util.spec_from_file_location("grounding_qa_probes", args.anchors)
        anchors = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(anchors)
    if args.legacy_source:
        sys.path.insert(0, str(args.legacy_source.resolve()))
    else:
        need(anchors is not None, "current assets require their ground_anchors.py for complete coverage")
    tiles = ({p.stem: filehash(p) for p in args.textures.glob("*.png")} if args.textures else None)
    cache = {}
    scenes = []
    for rec in authored:
        key = rec["id"]
        if args.ids and key not in args.ids:
            continue
        mesh_dir = args.studies if rec["planned"] and args.studies else args.meshes
        path = mesh_dir / (key+".bytes")
        info = metadata[key]
        if "objects" not in info:
            need(args.legacy_source is not None, "original artifacts require their original --legacy-source")
            info = legacy_layout(rec, info, args.legacy_source, cache, path)
        glb = args.models/(key+".glb") if args.models else None
        scenes.append(verify_scene(rec, info, path, glb, tiles, anchors))
    need(scenes and (not args.ids or set(args.ids) == {s["id"] for s in scenes}), "missing selected scenes")
    summary = summarize(scenes)
    evidence = {"format": "ash-crypt-ground-contact-v1", "summary": summary,
                "units": "metres; XY east/north; Z up; clearance = member underside or sill minus terrain",
                "catalogSHA256": filehash(args.catalog), "reportSHA256": filehash(args.report),
                "groundAnchorsSHA256": filehash(args.anchors) if anchors else None,
                "proofCamera": "diagnostic, 12 m towards each object, 5 m outdoors / 3.6 m caves; terrain-only sight rays",
                "scenes": scenes}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(evidence, ensure_ascii=False, separators=(",", ":"))+"\n")
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    return 1 if summary["issues"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
