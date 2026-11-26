"""Blender (headless): the bell court with every piece reduced as for the tablet, rendered for a look.
usage: blender --background --factory-startup -noaudio --threads 3 --python preview.py -- <Glockenhof.glb> <out.png>"""
import bpy, sys, random, math, bmesh
from mathutils import Vector
glb, out = sys.argv[sys.argv.index("--") + 1:][:2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)
targets = {"Chorkapelle": (14000, 0), "Glockenturm": (10000, 0), "Schiefergrat": (1800, 0), "Schieferfindling": (1300, 0), "Rispengras": (700, 0.2),
           "Witwengras": (380, 0.3), "Glockenwacht": (14000, 0), "Dochtpilger": (12000, 0)}
done, total_before, total_after = set(), 0, 0
def tris(mesh):
    mesh.calc_loop_triangles(); return len(mesh.loop_triangles)
for obj in [o for o in bpy.data.objects if o.type == "MESH"]:
    family = obj.name.split(".")[0]
    total_before += tris(obj.data)
    if obj.data.name not in done and family in targets:
        done.add(obj.data.name)
        target, thin = targets[family]
        if thin:
            mesh = bmesh.new(); mesh.from_mesh(obj.data); mesh.verts.ensure_lookup_table()
            seen, islands = set(), []
            for vert in mesh.verts:
                if vert.index in seen: continue
                stack, island = [vert], []; seen.add(vert.index)
                while stack:
                    current = stack.pop(); island.append(current)
                    for edge in current.link_edges:
                        other = edge.other_vert(current)
                        if other.index not in seen: seen.add(other.index); stack.append(other)
                islands.append(island)
            pick = random.Random(9147)
            drop = [island for island in islands if pick.random() > thin]
            bmesh.ops.delete(mesh, geom=[v for island in drop for v in island], context="VERTS")
            mesh.to_mesh(obj.data); mesh.free()
        now = tris(obj.data)
        if now > target * 1.05:
            bpy.ops.object.select_all(action="DESELECT"); obj.select_set(True); bpy.context.view_layer.objects.active = obj
            users = obj.data.users
            reduce = obj.modifiers.new("reduce", "DECIMATE"); reduce.decimate_type = "COLLAPSE"; reduce.ratio = max(0.01, target / now); reduce.use_collapse_triangulate = True
            reduced = bpy.data.meshes.new_from_object(obj.evaluated_get(bpy.context.evaluated_depsgraph_get()))
            obj.modifiers.remove(reduce)
            old = obj.data
            for other in bpy.data.objects:
                if other.type == "MESH" and other.data == old: other.data = reduced
            reduced.name = old.name; done.add(reduced.name)
for obj in [o for o in bpy.data.objects if o.type == "MESH"]: total_after += tris(obj.data)
print("TRIANGLES before", total_before, "after", total_after)
scene = bpy.context.scene
scene.render.engine = "CYCLES"; scene.cycles.device = "CPU"; scene.cycles.samples = 36; scene.cycles.use_denoising = True
scene.render.resolution_x, scene.render.resolution_y = 1200, 760
scene.view_settings.view_transform = "Filmic" if "Filmic" in [i.identifier for i in type(scene.view_settings).bl_rna.properties["view_transform"].enum_items] else scene.view_settings.view_transform
world = bpy.data.worlds.new("sky"); scene.world = world; world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.20, 0.23, 0.28, 1); world.node_tree.nodes["Background"].inputs[1].default_value = 0.9
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); scene.collection.objects.link(sun)
sun.data.energy = 3.2; sun.data.angle = math.radians(6); sun.rotation_euler = (math.radians(52), 0, math.radians(38))
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); scene.collection.objects.link(cam); scene.camera = cam
cam.data.lens = 38
# Blender space: glTF (x, y, z) -> (x, -z, y). The owner's overview looks from the path's side, a little from the right.
eye, focus = Vector((13.5, -19.0, 12.5)), Vector((-0.6, 0.6, 1.6))
cam.location = eye; cam.rotation_euler = (focus - eye).to_track_quat("-Z", "Y").to_euler()
scene.render.filepath = out
bpy.ops.render.render(write_still=True)
