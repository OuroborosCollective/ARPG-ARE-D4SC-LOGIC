"""Blender fixture modes: create/create-missing/create-empty SOURCE.blend, create-rig SOURCE.glb,
or verify/verify-rig OUTPUT for the corresponding conversion."""
import json
from pathlib import Path
import shutil
import sys
import tempfile
from urllib.parse import unquote

import bpy
from mathutils import Vector


def finished(result):
    if result != {'FINISHED'}:
        raise RuntimeError(f"Blender operator returned {result}")


SWATCHES = {
    "ForgePigment": [(1, 0, 0), (0, 1, 0), (0, 0, 1), (1, 1, 0)],
    "ForgeTrim": [(0, 1, 1), (1, 0, 1), (0, 0, 0), (1, 1, 1)],
}


def add_material(cube, name, pack):
    # Different source directories deliberately contain the same basename.
    with tempfile.TemporaryDirectory() as temp:
        image_path = Path(temp) / "ForgeSwatches.png"
        generated = bpy.data.images.new("Swatches", width=2, height=2)
        generated.pixels[:] = [value for rgb in SWATCHES[name] for value in (*rgb, 1)]
        generated.filepath_raw = str(image_path)
        generated.file_format = 'PNG'
        generated.save()
        bpy.data.images.remove(generated)
        image = bpy.data.images.load(str(image_path))
        if pack:
            image.pack()

        material = bpy.data.materials.new(name)
        material.use_nodes = True
        texture = material.node_tree.nodes.new("ShaderNodeTexImage")
        texture.image = image
        shader = material.node_tree.nodes.get("Principled BSDF")
        material.node_tree.links.new(texture.outputs['Color'], shader.inputs['Base Color'])
        cube.data.materials.append(material)


def create(path, mode):
    path.parent.mkdir(parents=True, exist_ok=True)
    finished(bpy.ops.wm.read_factory_settings(use_empty=True))
    if mode == "create-empty":
        finished(bpy.ops.object.camera_add())
    else:
        parent = bpy.data.objects.new("ForgeRoot", None)
        bpy.context.collection.objects.link(parent)
        for frame, x in ((1, 2), (4, 2), (16, 5)):
            parent.location = (x, -1, 4)
            parent.keyframe_insert(data_path="location", frame=frame)
        parent.animation_data.action.name = "ForgeTravel"
        bpy.context.scene.frame_set(1)
        finished(bpy.ops.mesh.primitive_cube_add())
        cube = bpy.context.object
        cube.name = "ForgeBlock"
        cube.parent = parent
        cube.location = (1, 2, 3)
        cube.scale = (0.5, 1.5, 2.5)

        for material_name in SWATCHES:
            add_material(cube, material_name, mode == "create")
        for polygon in cube.data.polygons:
            polygon.material_index = polygon.index % 2

        # If Blender executes a registered source script, the world-bounds assertion fails.
        script = bpy.data.texts.new("fixture_pose.py")
        script.write("import bpy\nbpy.data.objects['ForgeBlock'].scale.x = 10\n")
        script.use_module = True
    finished(bpy.ops.wm.save_as_mainfile(filepath=str(path)))


def create_rig(path):
    path.parent.mkdir(parents=True, exist_ok=True)
    finished(bpy.ops.wm.read_factory_settings(use_empty=True))
    finished(bpy.ops.object.armature_add(enter_editmode=True))
    rig = bpy.context.object
    rig.name = "ForgeRig"
    base = rig.data.edit_bones[0]
    base.name = "Base"
    base.head, base.tail = (0, 0, -1), (0, 0, 0)
    tip = rig.data.edit_bones.new("Tip")
    tip.head, tip.tail, tip.parent = (0, 0, 0), (0, 0, 1), base
    finished(bpy.ops.object.mode_set(mode='OBJECT'))

    finished(bpy.ops.mesh.primitive_cube_add())
    skin = bpy.context.object
    skin.name = "ForgeSkin"
    skin.parent = rig
    modifier = skin.modifiers.new("Skin", 'ARMATURE')
    modifier.object = rig
    for name in ("Base", "Tip"):
        group = skin.vertex_groups.new(name=name)
        indices = [v.index for v in skin.data.vertices if (v.co.z > 0) == (name == "Tip")]
        group.add(indices, 1.0, 'REPLACE')

    tip_pose = rig.pose.bones["Tip"]
    for frame, x in ((1, 0), (4, 0), (16, 1.5)):
        tip_pose.location.x = x
        tip_pose.keyframe_insert(data_path="location", frame=frame)
    rig.animation_data.action.name = "ForgeBend"
    bpy.context.scene.frame_set(1)
    finished(bpy.ops.export_scene.gltf(filepath=str(path), export_format='GLB', export_animations=True))


def check_rig(label):
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
    assert len(meshes) == 1, (label, [mesh.name for mesh in meshes])
    skin = meshes[0]
    rig = skin.find_armature()
    assert skin.name == "ForgeSkin" and rig and rig.pose.bones.get("Tip"), label
    assert bpy.data.actions, (label, "missing skeletal clip")

    spans = []
    base_positions = []
    for frame in range(1, 25):
        bpy.context.scene.frame_set(frame)
        evaluated = skin.evaluated_get(bpy.context.evaluated_depsgraph_get())
        coords = [evaluated.matrix_world @ vertex.co for vertex in evaluated.data.vertices]
        top = [v.x for v in coords if v.z > 0]
        bottom = [v.x for v in coords if v.z < 0]
        assert top and bottom, (label, frame)
        # Bounds centers survive vertex splitting at UV seams and triangulation.
        base = (min(bottom) + max(bottom)) / 2
        base_positions.append(base)
        spans.append((min(top) + max(top)) / 2 - base)
    assert max(base_positions) - min(base_positions) < 0.001, (label, base_positions)
    assert abs(max(spans) - min(spans) - 1.5) < 0.01, (label, spans)
    print(f"PASS {label}: one skinned mesh, armature, fixed base and 1.5-unit tip deformation")


def check_scene(label):
    mesh_objects = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
    assert len(mesh_objects) == 1, (label, mesh_objects)
    block = mesh_objects[0]
    assert block.name == "ForgeBlock" and block.parent and block.parent.name == "ForgeRoot", label
    assert block.data.polygons and block.data.uv_layers.active, label
    bpy.context.scene.frame_set(1)
    coords = [block.matrix_world @ Vector(corner) for corner in block.bound_box]
    for axis, (low, high) in enumerate(((2.5, 3.5), (-0.5, 2.5), (4.5, 9.5))):
        values = [p[axis] for p in coords]
        assert abs(min(values) - low) < 0.001 and abs(max(values) - high) < 0.001, (label, axis, values)

    assert bpy.data.actions, (label, "missing clip")
    x_values = []
    for frame in range(1, 25):
        bpy.context.scene.frame_set(frame)
        x_values.append(block.matrix_world.translation.x)
    assert abs(max(x_values) - min(x_values) - 3) < 0.01, (label, "travel", x_values)
    used = {block.data.materials[face.material_index].name for face in block.data.polygons}
    assert used == set(SWATCHES), (label, used)
    for material in block.data.materials:
        shader = next(node for node in material.node_tree.nodes if node.type == 'BSDF_PRINCIPLED')
        texture = shader.inputs['Base Color'].links[0].from_node
        assert texture.type == 'TEX_IMAGE' and tuple(texture.image.size) == (2, 2), (label, material.name)
        pixels = list(texture.image.pixels[:])
        colors = {tuple(round(c, 2) for c in pixels[i:i+3]) for i in range(0, 16, 4)}
        assert colors == set(SWATCHES[material.name]), (label, material.name, colors)
    print(f"PASS {label}: hierarchy, world bounds, 3-unit animated travel, UV, both assigned materials, eight original texture swatches")


def verify(directory, check):
    for suffix in (".fbx", ".glb", ".gltf"):
        # Give each importer only the files its format advertises.
        with tempfile.TemporaryDirectory() as temp:
            model = Path(temp) / ("model" + suffix)
            shutil.copyfile(directory / model.name, model)
            if suffix == ".gltf":
                manifest = json.loads(model.read_text(encoding="utf-8"))
                for item in manifest["buffers"] + manifest.get("images", []):
                    relative = Path(unquote(item["uri"]))
                    assert not relative.is_absolute() and ".." not in relative.parts, relative
                    target = model.parent / relative
                    target.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copyfile(directory / relative, target)
            finished(bpy.ops.wm.read_factory_settings(use_empty=True))
            if suffix == ".fbx":
                finished(bpy.ops.import_scene.fbx(filepath=str(model), use_image_search=False))
            else:
                finished(bpy.ops.import_scene.gltf(filepath=str(model), disable_bone_shape=True))
            check(model.name)


if __name__ == "__main__":
    mode, argument = sys.argv[sys.argv.index("--") + 1:]
    path = Path(argument).resolve()
    if mode == "verify":
        verify(path, check_scene)
    elif mode == "create-rig":
        create_rig(path)
    elif mode == "verify-rig":
        verify(path, check_rig)
    elif mode in ("create", "create-missing", "create-empty"):
        create(path, mode)
    else:
        raise ValueError(f"unknown fixture mode: {mode}")
