"""Independent readback of the authored map, Unity triangle streams and editable GLBs."""
import argparse
import hashlib
import json
import re
import struct
from pathlib import Path

import numpy as np


AREAS = {
    "old_crypt", "forecourt", "kiln", "bellcourt", "village", "ash_road", "mill",
    "granary", "hidden_crypt", "nest", "west_forest", "bear", "freight", "forest_road",
    "side_1", "side_2", "side_3", "side_4", "under_1", "under_2", "under_3",
}
STUDIES = {"king", "guardian"}
MAP_PLACE_NAMES = {
    "Die alte Krypta", "Aschenwacht · Der entweihte Vorhof", "Der Brennerhof",
    "Aschenwacht · Der Glockenhof", "Die Aschengrenze", "Die Aschestraße · Akt 3",
    "Die alte Mühle · Akt 4", "Der alte Speicher · Akt 5", "Die verborgene Krypta · Akt 6",
    "Das Rattlernest · Akt 7", "Der Wald nach Aschenzoll", "Die Bärenhöhle",
    "Aschenzoll · Der Frachtplatz", "Waldweg jenseits von Aschenzoll", "Die Seitenhöhle",
    "Krypta unter der Mühle",
}
# Wayfinding bearings follow the respective local level, including its return from
# a side location. The schematic does not imply opposite bearings on each side.
DIRECTIONS = {
    "old_crypt": [("crypt_arch", 0)],
    "forecourt": [("bell", 0), ("kiln", 90)],
    "kiln": [("bell", 180)],
    "bellcourt": [("village", 0)],
    "village": [("road_shrine", 0)],
    "ash_road": [("mill", 0), ("village", 180)],
    "mill": [("granary", 0), ("dead_oak", 312), ("road_shrine", 180)],
    "granary": [("crypt_crown", 0), ("sealed_arch", 0), ("mill", 180)],
    "hidden_crypt": [("nest", 0), ("sealed_arch", 180)],
    "nest": [("crypt_arch", 180)],
    "west_forest": [("toll", 0), ("bear_den", 270), ("mill", 180)],
    "bear": [("bear_den", 180)],
    "freight": [("road_shrine", 0), ("dead_oak", 180)],
    "forest_road": [("side_rift", 270), ("cairn", 90), ("toll", 180)],
}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def decode_glb(path, tiles):
    container = path.read_bytes()
    magic, version, length = struct.unpack_from("<4s2I", container)
    require(magic == b"glTF" and version == 2 and length == len(container), path)
    json_size, json_tag = struct.unpack_from("<I4s", container, 12)
    require(json_tag == b"JSON", path)
    description = json.loads(container[20:20 + json_size])
    bin_size, bin_tag = struct.unpack_from("<I4s", container, 20 + json_size)
    buffer = memoryview(container)[28 + json_size:]
    require(bin_tag == b"BIN\x00" and len(buffer) == bin_size, path)
    primitives = description["meshes"][0]["primitives"]

    def read(accessor_index):
        ac = description["accessors"][accessor_index]
        view = description["bufferViews"][ac["bufferView"]]
        dt = np.dtype({5121: "u1", 5123: "<u2", 5125: "<u4", 5126: "<f4"}[ac["componentType"]])
        width = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}[ac["type"]]
        stride = view.get("byteStride", width * dt.itemsize)
        return np.ndarray((ac["count"], width), dtype=dt, buffer=buffer,
                          offset=view.get("byteOffset", 0) + ac.get("byteOffset", 0),
                          strides=(stride, dt.itemsize))

    decoded_images = {}
    for image in description.get("images", []):
        view = description["bufferViews"][image["bufferView"]]
        start, length = view.get("byteOffset", 0), view["byteLength"]
        require(length > 16 and start + length <= len(buffer), f"{path}: embedded image extent")
        signature = bytes(buffer[start:start + 8])
        require(signature == b"\x89PNG\x0d\x0a\x1a\x0a" or signature[:2] == b"\xff\xd8",
                f"{path}: embedded image signature")
        decoded_images[image["name"]] = hashlib.sha256(buffer[start:start + length]).hexdigest()
    require(decoded_images == tiles, f"{path}: game and portable colour maps differ")
    for material in description["materials"]:
        require("KHR_materials_unlit" in material.get("extensions", {}), f"{path}: material type")
        texture = material["pbrMetallicRoughness"].get("baseColorTexture")
        if texture:
            binding = description["textures"][texture["index"]]
            sampler = description["samplers"][binding["sampler"]]
            require(description["images"][binding["source"]]["name"] == material["name"],
                    f"{path}: wrong texture bound for {material['name']}")
            require(sampler.get("wrapS", 10497) == 10497 and sampler.get("wrapT", 10497) == 10497 and
                    sampler.get("minFilter") in (9984, 9985, 9986, 9987), f"{path}: tile sampler")
    groups = {}
    for prim in primitives:
        indices = read(prim["indices"]).ravel()
        xyz = read(prim["attributes"]["POSITION"])[indices]
        xyz[:, 2] *= -1  # glTF forward is -Z, the Unity facing is +Z.
        color = read(prim["attributes"]["COLOR_0"])[indices]
        linear = color[:, :3].astype(np.float64) / np.iinfo(color.dtype).max
        srgb = np.where(linear <= .0031308, linear * 12.92, 1.055 * linear ** (1 / 2.4) - .055)
        if "TEXCOORD_0" in prim["attributes"]:
            uv = read(prim["attributes"]["TEXCOORD_0"])[indices]
            require(np.isfinite(uv).all() and (np.ptp(uv, axis=0) > 0).any(), f"{path}: texture coordinates")
        key = prim.get("material", 0)
        require(key not in groups, f"{path}: duplicate material primitive")
        groups[key] = (xyz.reshape(-1, 3, 3),
                       np.rint(np.clip(srgb, 0, 1) * 255).astype(np.uint8).reshape(-1, 3, 3))
    return groups, len(description.get("images", []))


def triangle_clearance(xz):
    q = np.roll(xz, -1, axis=1)
    d = q - xz
    length_squared = np.sum(d * d, axis=2)
    factor = np.divide(-np.sum(xz * d, axis=2), length_squared,
                       out=np.zeros_like(length_squared), where=length_squared > 0)
    closest = xz + np.clip(factor, 0, 1)[:, :, None] * d
    distance = np.sqrt(np.min(np.sum(closest * closest, axis=2), axis=1))
    cross = xz[:, :, 0] * q[:, :, 1] - xz[:, :, 1] * q[:, :, 0]
    inside = (cross.min(axis=1) > 1e-8) | (cross.max(axis=1) < -1e-8)
    return 0.0 if inside.any() else float(distance.min())


def verify_asset(key, mesh_path, glb_path, tiles):
    raw = mesh_path.read_bytes()
    magic, count = struct.unpack_from("<4sI", raw)
    require(magic == b"WFH1" and count > 0 and len(raw) == 8 + 48 * count, mesh_path)
    vertices = np.frombuffer(raw, offset=8, dtype=[("xyz", "<f4", 3), ("rgba", "u1", 4)])
    tri = vertices["xyz"].reshape(-1, 3, 3)
    colors = vertices["rgba"]
    require(np.isfinite(tri).all(), key)
    area = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0])
    require(np.all(np.linalg.norm(area, axis=1) > 0), f"{key}: collapsed triangle")
    require(len(np.unique(colors[:, :3], axis=0)) > 12, f"{key}: missing baked colour")
    tags = colors[:, 3].reshape(-1, 3)
    require(np.all(tags == tags[:, :1]), f"{key}: inconsistent material within triangle")
    classes = tags[:, 0]
    groups, image_count = decode_glb(glb_path, tiles)
    if np.all(classes == 255) and len(groups) == 1:  # archive version with unclassified vertex colours
        classes = np.zeros_like(classes) + next(iter(groups))
    require(set(classes.tolist()) == set(groups), f"{key}: GLB and game material groups differ")
    for material, (imported_xyz, imported_rgb) in groups.items():
        keep = classes == material
        require(np.array_equal(tri[keep], imported_xyz), f"{key}: GLB and game geometry differ in class {material}")
        require(np.array_equal(colors[:, :3].reshape(-1, 3, 3)[keep], imported_rgb),
                f"{key}: GLB and game colour differ in class {material}")
    clearance = triangle_clearance(tri[:, :, (0, 2)].astype(np.float64))
    require(clearance > 30, f"{key}: scenery enters center ({clearance:.2f}m)")
    bearings = np.arctan2(tri[:, :, 0], tri[:, :, 2])
    bins = np.unique(np.floor((bearings + np.pi) / (2 * np.pi) * 32).astype(int) % 32)
    require(len(bins) == 32, f"{key}: missing angular sector")
    return {"id": key, "triangles": count, "bytes": len(raw), "material_groups": len(groups),
            "embedded_images": image_count,
            "inner_distance_m": round(clearance, 3), "sha256": hashlib.sha256(raw).hexdigest()}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    here = Path(__file__).resolve().parent
    parser.add_argument("--art", type=Path, default=here)
    parser.add_argument("--resources", type=Path, default=here.parent / "ashen/Assets/Resources/Weltferne")
    parser.add_argument("--map-html", type=Path, help="Published world map source dist/index.html")
    parser.add_argument("--renders", action="store_true", help="Verify all perspective renders and their input receipt")
    args = parser.parse_args()
    if args.map_html:
        html = args.map_html.read_text(encoding="utf-8")
        places = re.findall(r'<li class="area (\w+)">.*?<span class="area-name">([^<]+)<', html, re.S)
        require(len(places) == 18, "published map place count")
        require({name for status, name in places if status != "planned"} == MAP_PLACE_NAMES, "published map sites")
        require({name for status, name in places if status == "planned"} ==
                {"Der König von Immergrün", "Der Sockel des Glutwächters"}, "future map places")
        require(html.count('class="path one"') == 2 and html.count('class="path two"') == 13,
                "published one-way and two-way edges")
    tiles = {p.stem: hashlib.sha256(p.read_bytes()).hexdigest() for p in (args.resources / "Detail").glob("*.png")}
    require(set(tiles) == {"masonry", "rock", "earth", "timber"}, "game material tile coverage")
    plates = json.loads((args.art / "catalog.json").read_text(encoding="utf-8"))
    require(len(plates) == len(AREAS | STUDIES), "duplicate/missing plates")
    require({p["id"] for p in plates if not p["planned"]} == AREAS, "active area coverage")
    require({p["id"] for p in plates if p["planned"]} == STUDIES, "planned area coverage")
    require({p.stem for p in args.resources.glob("*.bytes")} == AREAS, "runtime resources")
    require({p.stem for p in (args.art / "studies").glob("*.bytes")} == STUDIES, "concept resources")
    rows = []
    directional_checks = 0
    for p in plates:
        key = p["id"]
        for kind, expected in DIRECTIONS.get(key, ()):
            angles = [a["bearing"] for a in p["landmarks"] if a["kind"] == kind]
            require(any(abs((a - expected + 180) % 360 - 180) <= 25 for a in angles),
                    f"{key}: missing {kind} towards {expected} degrees")
            directional_checks += 1
        folder = args.art / "studies" if p["planned"] else args.resources
        rows.append(verify_asset(key, folder / f"{key}.bytes", args.art / "models" / f"{key}.glb", tiles))
    require(len({r["sha256"] for r in rows}) == len(rows), "duplicate exported plate")
    if args.renders:
        from PIL import Image
        receipt = json.loads((args.art / "render_receipt.json").read_text(encoding="utf-8"))
        actual = hashlib.sha256((args.art / "catalog.json").read_bytes()).hexdigest()
        require(receipt["catalogSha256"] == actual, "source catalogue changed after the logged render")
        hashes = {r["id"]: r["sha256"] for r in rows}
        require({v["id"] for v in receipt["views"]} == set(hashes), "missing rendered locations")
        for view in receipt["views"]:
            image = args.art / "views" / (view["view"] + ".jpg")
            require(hashes[view["id"]] == view["meshSha256"] and
                    hashlib.sha256(image.read_bytes()).hexdigest() == view["imageSha256"], view["view"])
            with Image.open(image) as picture:
                require(picture.size == tuple(view["dimensions"]), view["view"])
                picture.verify()
    print(json.dumps({"status": "passed", "mapped_areas": 16, "live_variants": len(AREAS),
                      "isolated_studies": len(STUDIES), "bearing_checks": directional_checks, "map_hashes": tiles,
                      "assets": rows}, indent=2))


if __name__ == "__main__":
    main()
