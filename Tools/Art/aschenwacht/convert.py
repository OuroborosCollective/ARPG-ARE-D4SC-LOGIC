"""Blender (headless): one GLB of the Aschenwacht set -> FBX files Unity reads.

usage: blender --background --factory-startup -noaudio --python convert.py -- '<json>'
json: {"glb": source, "name": "Glockenturm", "out": folder,
       "object": optional name of the one object to take from a scene file,
       "triangles": target for the game mesh (0 keeps the source), "thin": share of loose parts a plant keeps (0..1),
       "takes": ["Ruhe", "Gehen"] for a figure with clips}

Writes <out>/Full/<name>.fbx (the source geometry, nothing left out), <out>/Game/<name>.fbx (reduced for the tablet,
same materials and UVs) and, for a figure, <out>/Game/<name>@<take>.fbx with one clip each. Materials keep their
names; pictures are not embedded (Unity builds the materials from the set's PNG originals). Prints "RESULT {json}".
"""
import bpy
import json
import os
import random
import sys

config = json.loads(sys.argv[sys.argv.index("--") + 1])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=config["glb"])
scene = bpy.context.scene

if config.get("object"):
    for obj in list(bpy.data.objects):
        if obj.name != config["object"]:
            bpy.data.objects.remove(obj, do_unlink=True)

armature = next((obj for obj in bpy.data.objects if obj.type == "ARMATURE"), None)
# Blender's glTF import adds a small sphere as the bones' display shape: it is no part of the model.
for obj in list(bpy.data.objects):
    if armature and obj.type == "MESH" and obj.parent is None and not obj.vertex_groups and obj.name.startswith("Icosphere"):
        bpy.data.objects.remove(obj, do_unlink=True)
meshes = [obj for obj in bpy.data.objects if obj.type == "MESH"]
if not meshes:
    sys.exit("no mesh in " + config["glb"])
for material in bpy.data.materials:
    material.name = material.name.split(".")[0]


def triangles(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def bounds():
    low, high = [1e9] * 3, [-1e9] * 3
    for obj in meshes:
        for corner in obj.bound_box:
            world = obj.matrix_world @ __import__("mathutils").Vector(corner)
            for axis in range(3):
                low[axis] = min(low[axis], world[axis]); high[axis] = max(high[axis], world[axis])
    return [round(high[axis] - low[axis], 3) for axis in range(3)]


def select():
    bpy.ops.object.select_all(action="DESELECT")
    for obj in meshes + ([armature] if armature else []):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = armature or meshes[0]


common = dict(use_selection=True, object_types={"ARMATURE", "MESH"}, add_leaf_bones=False, path_mode="STRIP",
              embed_textures=False, mesh_smooth_type="OFF")
result = {"name": config["name"], "meshes": [[obj.name, len(obj.vertex_groups), obj.parent.name if obj.parent else None, obj.parent_bone] for obj in meshes],
           "materials": sorted({slot.material.name for obj in meshes for slot in obj.material_slots if slot.material}),
          "size": bounds(), "full": sum(triangles(obj) for obj in meshes), "bones": len(armature.data.bones) if armature else 0, "takes": {}}

if armature:
    armature.animation_data_create()
    armature.animation_data.action = None
    for bone in armature.pose.bones:
        bone.matrix_basis.identity()
for folder in ("Full", "Game"):
    os.makedirs(os.path.join(config["out"], folder), exist_ok=True)
select()
bpy.ops.export_scene.fbx(filepath=os.path.join(config["out"], "Full", config["name"] + ".fbx"), bake_anim=False, **common)

# The game mesh: a plant keeps a share of its loose parts (whole blades, not shredded ones), then every mesh is
# collapsed to its share of the triangle target. UVs, material slots and skin weights stay.
target = config.get("triangles", 0)
for obj in meshes:
    if config.get("thin", 0) > 0:
        import bmesh
        mesh = bmesh.new(); mesh.from_mesh(obj.data); mesh.verts.ensure_lookup_table()
        seen, islands = set(), []
        for vert in mesh.verts:
            if vert.index in seen:
                continue
            stack, island = [vert], []
            seen.add(vert.index)
            while stack:
                current = stack.pop(); island.append(current)
                for edge in current.link_edges:
                    other = edge.other_vert(current)
                    if other.index not in seen:
                        seen.add(other.index); stack.append(other)
            islands.append(island)
        pick = random.Random(9147)
        drop = [island for island in islands if pick.random() > config["thin"]]
        if len(drop) < len(islands):
            bmesh.ops.delete(mesh, geom=[vert for island in drop for vert in island], context="VERTS")
        mesh.to_mesh(obj.data); mesh.free()
        result["parts"] = [len(islands), len(islands) - len(drop)]
before_total = sum(triangles(obj) for obj in meshes)
for obj in meshes:
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    now = triangles(obj)
    share = target * now / max(1, before_total) if target else 0
    if target and now > share * 1.05:
        reduce = obj.modifiers.new("reduce", "DECIMATE")
        reduce.decimate_type = "COLLAPSE"
        reduce.ratio = max(0.01, share / now)
        reduce.use_collapse_triangulate = True
        if armature:
            bpy.ops.object.modifier_move_to_index(modifier=reduce.name, index=0)  # before the armature deforms it
        bpy.ops.object.modifier_apply(modifier=reduce.name)
    if armature and obj.vertex_groups:
        bpy.ops.object.vertex_group_limit_total(limit=4)
        bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    bpy.ops.object.shade_smooth()
result["game"] = sum(triangles(obj) for obj in meshes)
select()
bpy.ops.export_scene.fbx(filepath=os.path.join(config["out"], "Game", config["name"] + ".fbx"), bake_anim=False, **common)

for take in config.get("takes", []):
    action = bpy.data.actions.get(take)
    if action is None:
        sys.exit("clip missing: " + take)
    armature.animation_data.action = action
    if getattr(action, "slots", None):
        armature.animation_data.action_slot = action.slots[0]
    scene.frame_start, scene.frame_end = int(action.frame_range[0]), int(action.frame_range[1])
    select()
    bpy.ops.export_scene.fbx(filepath=os.path.join(config["out"], "Game", config["name"] + "@" + take + ".fbx"), bake_anim=True,
                             bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
                             bake_anim_simplify_factor=0.0, **common)
    result["takes"][take] = round((action.frame_range[1] - action.frame_range[0]) / scene.render.fps, 2)
print("RESULT " + json.dumps(result))
