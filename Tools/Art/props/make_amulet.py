"""An amulet on a neck chain, fitted to one figure (owner brief 2026-11-24: jewellery for Mara).

The chain follows points measured on the standing figure (around the neck, down the chest), so it lies on the body in
the rest pose; the figure recipe hangs the finished model on the upper chest bone with anchor "origin". The medallion
is a worn gold disc with a raised ring, four granules and a dark polished stone. Own model, no pack content.

All input is in the figure's space as the game sees it: metres, x to the figure's right, y up, z to its front.
usage: blender --background --factory-startup --python make_amulet.py -- <fit.json> <out.fbx>
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


def finish(obj, mat, smooth=True):
    obj.data.materials.append(mat)
    for polygon in obj.data.polygons:
        polygon.use_smooth = smooth
    return obj


bpy.ops.wm.read_factory_settings(use_empty=True)
gold, stone = material("AmuletGold", fit["gold"]), material("AmuletStone", fit["stone"])
parts = []

# Chain: one closed loop through the measured right half and its mirror image, as a thin round cord.
half = [blender(p) for p in fit["chain"]]                    # nape ... bail, x >= 0 in game space
loop = half + [Vector((-p.x, p.y, p.z)) for p in reversed(half[1:-1])]
curve = bpy.data.curves.new("Chain", "CURVE")
curve.dimensions = "3D"
curve.bevel_depth, curve.bevel_resolution, curve.resolution_u = fit["cord"], 1, 3
spline = curve.splines.new("NURBS")
spline.points.add(len(loop) - 1)
for point, at in zip(spline.points, loop):
    point.co = (*at, 1.0)
spline.use_cyclic_u, spline.order_u = True, 3
chain = bpy.data.objects.new("Chain", curve)
bpy.context.scene.collection.objects.link(chain)
bpy.context.view_layer.objects.active = chain
chain.select_set(True)
bpy.ops.object.convert(target="MESH")
parts.append(finish(bpy.context.view_layer.objects.active, gold))

# Medallion, built flat (face towards -y = the figure's front), then leaned onto the chest and moved to its place.
radius, thick = fit["radius"], fit["thickness"]
lean = Matrix.Rotation(math.radians(fit["lean"]), 4, "X")     # the top leans back with the slope of the chest
place = Matrix.Translation(blender(fit["medallion"])) @ lean


def add(mesh_op, name, mat, local=Matrix.Identity(4), smooth=True, **kw):
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    mesh_op(bm, **kw)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.matrix_world = place @ local
    parts.append(finish(obj, mat, smooth))
    return obj


face = Matrix.Rotation(math.radians(90), 4, "X")              # cone axis z -> y: a disc facing front
add(bmesh.ops.create_cone, "Disc", gold, face, smooth=False, cap_ends=True, segments=24, radius1=radius, radius2=radius, depth=thick)
rim = Matrix.Translation((0, -thick * .5, 0)) @ face
for name, major, minor in (("Rim", radius * .93, thick * .42), ("Ring", radius * .56, thick * .30)):
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    segments, sides = 24, 6
    rings = []
    for i in range(segments):
        a = 2 * math.pi * i / segments
        centre = Vector((math.cos(a) * major, math.sin(a) * major, 0))
        rings.append([bm.verts.new(centre + (Vector((math.cos(a), math.sin(a), 0)) * math.cos(b) + Vector((0, 0, 1)) * math.sin(b)) * minor)
                      for b in (2 * math.pi * j / sides for j in range(sides))])
    for i in range(segments):
        for j in range(sides):
            bm.faces.new((rings[i][j], rings[(i + 1) % segments][j], rings[(i + 1) % segments][(j + 1) % sides], rings[i][(j + 1) % sides]))
    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.matrix_world = place @ rim
    parts.append(finish(obj, gold))
for i in range(4):                                             # granules between ring and rim
    a = math.radians(45 + 90 * i)
    at = Matrix.Translation((math.cos(a) * radius * .75, -thick * .5, math.sin(a) * radius * .75))
    add(bmesh.ops.create_icosphere, "Granule%d" % i, gold, at, subdivisions=1, radius=thick * .34)
dome = Matrix.Translation((0, -thick * .5, 0)) @ Matrix.Diagonal((1, .55, 1, 1))
add(bmesh.ops.create_uvsphere, "Stone", stone, dome, u_segments=12, v_segments=6, radius=radius * .46)

# Bail: the small ring the chain runs through, standing on the medallion's upper edge.
bail = Matrix.Translation((0, 0, radius + fit["cord"] * 1.6)) @ Matrix.Rotation(math.radians(90), 4, "Z") @ face
mesh = bpy.data.meshes.new("Bail")
bm = bmesh.new()
segments, sides, major, minor = 12, 5, fit["cord"] * 2.4, fit["cord"] * .9
rings = []
for i in range(segments):
    a = 2 * math.pi * i / segments
    centre = Vector((math.cos(a) * major, math.sin(a) * major, 0))
    rings.append([bm.verts.new(centre + (Vector((math.cos(a), math.sin(a), 0)) * math.cos(b) + Vector((0, 0, 1)) * math.sin(b)) * minor)
                  for b in (2 * math.pi * j / sides for j in range(sides))])
for i in range(segments):
    for j in range(sides):
        bm.faces.new((rings[i][j], rings[(i + 1) % segments][j], rings[(i + 1) % segments][(j + 1) % sides], rings[i][(j + 1) % sides]))
bm.normal_update()
bm.to_mesh(mesh)
bm.free()
obj = bpy.data.objects.new("Bail", mesh)
bpy.context.scene.collection.objects.link(obj)
obj.matrix_world = place @ bail
parts.append(finish(obj, gold))

for obj in bpy.context.scene.objects:
    obj.select_set(obj in parts)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.join()
amulet = bpy.context.view_layer.objects.active
amulet.name = amulet.data.name = fit.get("name", "Amulet")
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)   # the model's origin is the point between the feet
triangles = sum(len(p.vertices) - 2 for p in amulet.data.polygons)
bpy.ops.export_scene.fbx(filepath=out_path, use_selection=True, object_types={"MESH"}, bake_anim=False,
                         mesh_smooth_type="FACE", path_mode="STRIP", embed_textures=False)
print("AMULET %s triangles=%d" % (amulet.name, triangles))
