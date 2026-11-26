"""Native Blender readback: blender -b -P verify_blender.py -- <model_dir> <mesh_report.json> <result.json>."""
import hashlib
import json
from pathlib import Path
import struct
import sys
import tempfile

import bpy


def require(test, message):
    if not test:
        raise ValueError(message)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def source_bindings(path):
    raw = path.read_bytes()
    magic, version, size, n, section = struct.unpack_from("<4sIII4s", raw)
    require(magic == b"glTF" and version == 2 and size == len(raw) and section == b"JSON", str(path))
    model = json.loads(raw[20:20+n])
    length, section = struct.unpack_from("<I4s", raw, 20+n)
    buffer = memoryview(raw)[28+n:]
    require(section == b"BIN\x00" and len(buffer) == length, str(path))
    images = {}
    for item in model["images"]:
        view = model["bufferViews"][item["bufferView"]]
        start, count = view.get("byteOffset", 0), view["byteLength"]
        content = bytes(buffer[start:start+count])
        require(content[:8] == b"\x89PNG\r\n\x1a\n" and len(content) == count, item["name"])
        images[item["name"]] = content
    require(set(images) == {"earth", "rock", "masonry", "timber"}, f"{path}: embedded tile set")
    used = {}
    for mesh in model["meshes"]:
        for primitive in mesh["primitives"]:
            material = model["materials"][primitive["material"]]
            tex = material["pbrMetallicRoughness"].get("baseColorTexture")
            names = set()
            if tex:
                src = model["textures"][tex["index"]]["source"]
                names.add(model["images"][src]["name"])
            used[material["name"]] = names
    return sha(raw), images, used


def verify_one(path, row):
    file_sha, embedded, expected = source_bindings(path)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    result = bpy.ops.import_scene.gltf(filepath=str(path.resolve()), import_pack_images=True)
    require("FINISHED" in result, str(path))
    objects = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    triangles = 0
    used = {}
    imported = set()
    for obj in objects:
        mesh = obj.data
        mesh.calc_loop_triangles()
        triangles += len(mesh.loop_triangles)
        require(len(mesh.uv_layers) and len(mesh.color_attributes), f"{path}: missing UV/colour layer")
        for slot in {p.material_index for p in mesh.polygons}:
            material = mesh.materials[slot]
            material_images = [n.image for n in material.node_tree.nodes if n.type == "TEX_IMAGE"]
            used[material.name] = {im.name for im in material_images}
            for im in material_images:
                require(tuple(im.size) == (512,512) and im.packed_file, f"{path}: {im.name} import")
                require(sha(im.packed_file.data) == sha(embedded[im.name]), f"{path}: {im.name} differs")
                imported.add(im.name)
    require(triangles == row["triangles"] and used == expected, f"{path}: triangle count/material bindings")
    # The importer's default intentionally skips materials not used on any surface.
    # Exercise Blender's own decoder on these exact embedded PNGs as separate images.
    missing = set(embedded)-imported
    with tempfile.TemporaryDirectory(prefix="horizon-glb-readback-") as directory:
        for name in sorted(missing):
            file = Path(directory)/(name+".png")
            file.write_bytes(embedded[name])
            image = bpy.data.images.load(str(file), check_existing=False)
            require(tuple(image.size) == (512,512) and len(image.pixels) == 512*512*4,
                    f"{path}: unused {name} cannot be decoded")
            image.pack()
            require(sha(image.packed_file.data) == sha(embedded[name]), f"{path}: {name} packed bytes")
    return {"id": row["id"], "glb_sha256": file_sha, "imported_triangles": triangles,
            "mesh_objects": len(objects), "uv_and_vertex_colours": True,
            "packed_images_used": sorted(imported), "unbound_images_decoded": sorted(missing),
            "embedded_images_sha256": {k:sha(data) for k,data in embedded.items()}}


models, report, destination = map(Path, sys.argv[sys.argv.index("--")+1:])
observed = [verify_one(models/(r["id"]+".glb"), r) for r in json.loads(report.read_text())]
destination.write_text(json.dumps({"status":"passed", "validator":f"Blender {bpy.app.version_string} native glTF and image import",
                                   "meshCount":len(observed), "views":observed}, indent=2)+"\n")
print("BLENDER VERIFIED", len(observed), "real GLB imports, material assignments and all embedded PNG decodes")
