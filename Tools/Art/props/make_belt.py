"""A slung leather belt with a brass buckle, fitted to one figure (owner brief 2026-11-24: Mara carries pouches and flasks).

The band follows a closed loop measured around the standing figure's hips, so it lies on the body in the rest pose;
the figure recipe hangs the finished model on the hip bone with anchor "origin". Own model, no pack content.

All input is in the figure's space as the game sees it: metres, x to the figure's right, y up, z to its front.
usage: blender --background --factory-startup --python make_belt.py -- <fit.json> <out.fbx>
"""
import bpy, bmesh, json, math, sys
from mathutils import Matrix, Vector

fit_path, out_path = sys.argv[sys.argv.index("--") + 1:][:2]
fit = json.load(open(fit_path, encoding="utf-8"))


def blender(p):
    """Game space (x right, y up, z front) to Blender (z up, -y front); the FBX export turns it back."""
    return Vector((-p[0], -p[2], p[1]))


def material(name, colour):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*colour, 1.0)
    return m


bpy.ops.wm.read_factory_settings(use_empty=True)
leather, brass = material("BeltLeather", fit["leather"]), material("BeltBrass", fit["brass"])
parts = []

# Band: the loop as a closed curve, drawn out upright to the belt's height and rounded at the edges.
loop = [blender(p) for p in fit["loop"]]
curve = bpy.data.curves.new("Band", "CURVE")
curve.dimensions, curve.twist_mode = "3D", "Z_UP"
curve.extrude, curve.bevel_depth, curve.bevel_resolution, curve.resolution_u = fit["height"] * .5 - fit["thickness"] * .5, fit["thickness"] * .5, 1, 2
spline = curve.splines.new("NURBS")
spline.points.add(len(loop) - 1)
for point, at in zip(spline.points, loop):
    point.co = (*at, 1.0)
spline.use_cyclic_u, spline.order_u = True, 3
band = bpy.data.objects.new("Band", curve)
bpy.context.scene.collection.objects.link(band)
bpy.context.view_layer.objects.active = band
band.select_set(True)
bpy.ops.object.convert(target="MESH")
band = bpy.context.view_layer.objects.active
band.data.materials.append(leather)
for polygon in band.data.polygons:
    polygon.use_smooth = True
parts.append(band)

# Buckle: a brass frame with a prong, lying on the band at one point of the loop and turned to face outwards.
at = blender(fit["buckle"])
centre = sum(loop, Vector()) / len(loop)
out = Vector((at.x - centre.x, at.y - centre.y, 0)).normalized()
turn = Matrix.Rotation(math.atan2(out.y, out.x) - math.pi * .5, 4, "Z")   # local +y -> outwards
frame_w, frame_h, bar = fit["height"] * 1.25, fit["height"] * 1.45, fit["thickness"] * .9
place = Matrix.Translation(at + out * (fit["thickness"] * .9)) @ turn
boxes = [((0, 0, frame_h * .5), (frame_w + bar, bar, bar)), ((0, 0, -frame_h * .5), (frame_w + bar, bar, bar)),
         ((frame_w * .5, 0, 0), (bar, bar, frame_h)), ((-frame_w * .5, 0, 0), (bar, bar, frame_h)),
         ((0, bar * .4, 0), (frame_w + bar, bar * .7, bar * .7))]            # the last one is the prong across the frame
for index, (where, size) in enumerate(boxes):
    mesh = bpy.data.meshes.new("Buckle%d" % index)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.bevel(bm, geom=bm.edges[:], offset=.18, segments=1, affect="EDGES")
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(mesh.name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.matrix_world = place @ Matrix.Translation(where) @ Matrix.Diagonal((*size, 1))
    obj.data.materials.append(brass)
    parts.append(obj)

for obj in bpy.context.scene.objects:
    obj.select_set(obj in parts)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.join()
belt = bpy.context.view_layer.objects.active
belt.name = belt.data.name = fit.get("name", "Belt")
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)   # the model's origin is the point between the feet
triangles = sum(len(p.vertices) - 2 for p in belt.data.polygons)
bpy.ops.export_scene.fbx(filepath=out_path, use_selection=True, object_types={"MESH"}, bake_anim=False,
                         mesh_smooth_type="FACE", path_mode="STRIP", embed_textures=False)
print("BELT %s triangles=%d" % (belt.name, triangles))
