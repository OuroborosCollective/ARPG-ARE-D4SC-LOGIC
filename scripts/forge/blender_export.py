"""Convert a self-contained model inside the World Forge worker's filesystem sandbox."""
import json
from pathlib import Path
import struct
import sys
from urllib.parse import unquote, urlsplit

import bpy


def finished(result, action):
    if result != {'FINISHED'}:
        raise ValueError(f"{action} did not finish: {result}")


def glb_document(path):
    with path.open("rb") as stream:
        magic, version, length, json_length, kind = struct.unpack("<4sIII4s", stream.read(20))
        if (magic != b"glTF" or version != 2 or kind != b"JSON" or length != path.stat().st_size
                or json_length > length - 20):
            raise ValueError("Expected a complete GLB 2.0 document")
        return json.loads(stream.read(json_length))


def require_embedded_glb(document):
    for resource in document.get("buffers", []) + document.get("images", []):
        uri = resource.get("uri")
        if uri is not None and not uri.startswith("data:"):
            raise ValueError("GLB has an external resource. Embed all buffers and images before uploading.")


def load_source(source):
    if source.suffix.lower() == ".blend":
        finished(bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False, use_scripts=False), "Open .blend")
        for library in bpy.data.libraries:
            if not library.packed_file:
                raise ValueError(f"External Blender library '{library.name}' must be packed before uploading.")
    else:
        finished(bpy.ops.wm.read_factory_settings(use_empty=True), "Clear startup scene")
        if source.suffix.lower() == ".fbx":
            finished(bpy.ops.import_scene.fbx(filepath=str(source), use_image_search=False), "Import FBX")
        elif source.suffix.lower() == ".glb":
            require_embedded_glb(glb_document(source))
            # Bone-display objects belong to the editor; FBX would serialize them as model meshes.
            finished(bpy.ops.import_scene.gltf(filepath=str(source), disable_bone_shape=True), "Import GLB")
        else:
            raise ValueError("Expected .blend, .fbx or .glb input")


def check_scene():
    for number, image in enumerate(bpy.data.images):
        if image.source == 'VIEWER':
            continue
        if image.source == 'GENERATED':
            image.pack()
        if image.source not in ('FILE', 'GENERATED'):
            raise ValueError(f"Image '{image.name}' requires a static packed texture for exchange.")
        if not image.packed_file:
            raise ValueError(f"Missing embedded image '{image.name}'. Pack external image resources before uploading.")
        if not all(image.size):
            raise ValueError(f"Packed image '{image.name}' could not be decoded.")
        # FBX COPY deduplicates by destination basename, even for packed images.
        # Give each in-memory image its own path without reloading the datablock.
        suffix = Path(image.filepath).suffix.lower()
        if suffix not in bpy.path.extensions_image:
            suffix = "." + image.file_format.lower()
        image.filepath_raw = f"//texture-{number}{suffix}"
    instances = bpy.context.evaluated_depsgraph_get().object_instances
    if not any(i.object.type == 'MESH' and len(i.object.data.polygons) for i in instances):
        raise ValueError("The active scene contains no mesh with faces.")


def check_geometry(document):
    if not any(primitive.get("mode", 4) == 4 and "POSITION" in primitive.get("attributes", {})
               for mesh in document.get("meshes", []) for primitive in mesh["primitives"]):
        raise ValueError("Exporter produced no triangle mesh.")


def export_models(source, output):
    arguments = sys.argv[:sys.argv.index("--")]
    if "--disable-autoexec" not in arguments or bpy.context.preferences.filepaths.use_scripts_auto_execute:
        raise ValueError("Worker must start Blender with --disable-autoexec.")
    output.mkdir(parents=True, exist_ok=True)
    if any(output.iterdir()):
        raise ValueError("Expected an empty output directory.")
    load_source(source)
    check_scene()
    fbx = output / "model.fbx"
    finished(bpy.ops.export_scene.fbx(filepath=str(fbx), path_mode='COPY', embed_textures=True,
                                      add_leaf_bones=False, bake_anim=True), "Export FBX")
    if fbx.stat().st_size == 0:
        raise ValueError("FBX export produced an empty file.")
    glb = output / "model.glb"
    gltf = output / "model.gltf"
    options = dict(use_active_scene=True, export_cameras=True, export_lights=True,
                   export_animations=True)
    finished(bpy.ops.export_scene.gltf(filepath=str(glb), export_format='GLB', **options), "Export GLB")
    binary_document = glb_document(glb)
    require_embedded_glb(binary_document)
    check_geometry(binary_document)
    finished(bpy.ops.export_scene.gltf(filepath=str(gltf), export_format='GLTF_SEPARATE',
                                       export_texture_dir="textures", **options), "Export glTF")
    document = json.loads(gltf.read_text(encoding="utf-8"))
    check_geometry(document)
    for resource in document["buffers"] + document.get("images", []):
        if "bufferView" in resource and "uri" not in resource:
            continue  # Embedded image bytes share an already validated glTF buffer.
        if not isinstance(resource.get("uri"), str):
            raise ValueError("glTF resource has neither a local URI nor an embedded buffer view.")
        uri = urlsplit(resource["uri"])
        relative = Path(unquote(uri.path))
        file = (output / relative).resolve()
        if (uri.scheme or uri.netloc or uri.query or uri.fragment or relative.is_absolute() or not file.is_relative_to(output)
                or not file.is_file() or file.stat().st_size == 0):
            raise ValueError("glTF references a missing or non-portable resource.")


if __name__ == "__main__":
    source_arg, output_arg = sys.argv[sys.argv.index("--") + 1:]
    export_models(Path(source_arg).resolve(strict=True), Path(output_arg).resolve())
