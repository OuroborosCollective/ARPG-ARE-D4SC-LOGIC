"""Workplaces for the village economy of Ashen Crypt, built in Blender without a window.

The residents of Aschengrenze fell trees, build frames and bows, hunt, cook and hand rations to Mara. This script
builds the places that work is seen at. Every workplace is one model file with named parts:

  Base               the furniture itself: it holds no goods
  Stock_<Good>_<n>   one unit of a good, in the order the place fills up
  Fire               the flames of the cooking fire

The game shows exactly as many Stock parts as the village really owns of that good, so nothing in these models is a
stock of its own. The goods are named as in the game's rules (VillageGood).

  WorkWoodyard    stump and stacking frame; wood (12), axes (2)
  WorkBench       carpenter's bench; wooden frames (2), bows (2), a broken axe (1), metal (2), thorn berries (2)
  WorkGameRack    pole on forked posts; game (4)
  WorkDryingRack  three hanging frames; hides (3)
  WorkKitchen     fire ring, tripod, pot and a low table; rations (4), water (4), the fire
  WorkWell        the village well on the market square; the water itself is not counted here
  WorkBoard       the village board, where the village writes what it has and what it lacks

All parts take their colour from one small palette picture; a second picture marks what glows (the fire).

Run:  blender --background --python Tools/Art/blender/village_work_props.py -- --output <private art folder> [--preview]
Writes <Name>.fbx for each workplace, VillageWorkPalette.png, VillageWorkGlow.png and, with --preview, preview.png.
Sizes are metres; +Z is up and the side a worker stands at is -Y. The export turns the models for Unity.
"""
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

PALETTE = [
    ("bark", (0.21, 0.15, 0.11)), ("wood_dark", (0.30, 0.21, 0.14)), ("wood", (0.44, 0.32, 0.21)), ("wood_light", (0.58, 0.45, 0.31)),
    ("cut", (0.74, 0.62, 0.44)), ("iron", (0.31, 0.32, 0.34)), ("iron_dark", (0.17, 0.18, 0.20)), ("rope", (0.56, 0.48, 0.34)),
    ("parchment", (0.83, 0.77, 0.63)), ("ink", (0.13, 0.11, 0.10)), ("hide", (0.67, 0.53, 0.39)), ("fur", (0.34, 0.25, 0.18)),
    ("ash", (0.80, 0.80, 0.83)), ("red", (0.47, 0.15, 0.13)), ("stone", (0.40, 0.40, 0.42)), ("stone_dark", (0.28, 0.28, 0.30)),
    ("feather", (0.52, 0.50, 0.47)), ("string", (0.86, 0.82, 0.72)), ("char", (0.09, 0.08, 0.08)), ("cloth", (0.60, 0.55, 0.45)),
    ("berry", (0.56, 0.10, 0.15)), ("water", (0.20, 0.31, 0.38)), ("stew", (0.50, 0.36, 0.22)),
    ("flame", (1.00, 0.52, 0.12)), ("flame_core", (1.00, 0.84, 0.42)), ("ember", (0.82, 0.22, 0.06)),
]
GLOWING = ("flame", "flame_core", "ember")
CELLS = 8
NAMES = [name for name, _ in PALETTE]
TRIANGLE_BUDGET = 3500


def cell_uv(name):
    index = NAMES.index(name)
    return ((index % CELLS + 0.5) / CELLS, (index // CELLS + 0.5) / CELLS)


class Part:
    """One named part of a workplace: shapes are added to a single mesh; every face remembers its palette colour."""

    def __init__(self, name, seed):
        self.name = name
        self.bm = bmesh.new()
        self.colour = self.bm.faces.layers.int.new("colour")
        self.rng = random.Random(seed)

    def _paint(self, faces, colour, top=None):
        for face in faces:
            face[self.colour] = NAMES.index(colour)
        if top:
            for face in faces:
                if face.normal.z > 0.9:
                    face[self.colour] = NAMES.index(top)

    def box(self, size, at, colour, rot=(0, 0, 0), top=None, taper=1.0):
        """A box; taper below 1 narrows its top (for axe heads, posts and roofs)."""
        made = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = made["verts"]
        for v in verts:
            if v.co.z > 0:
                v.co.x *= taper
                v.co.y *= taper
        bmesh.ops.scale(self.bm, vec=Vector(size), verts=verts)
        self._place(verts, at, rot)
        faces = list({f for v in verts for f in v.link_faces})
        self.bm.normal_update()
        self._paint(faces, colour, top)
        return faces

    def cyl(self, radius, length, at, colour, rot=(0, 0, 0), sides=8, ends=None, top_radius=None, top=None):
        """A cylinder along Z before turning; ends take their own colour (cut wood)."""
        made = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=sides, radius1=radius,
                                     radius2=radius if top_radius is None else top_radius, depth=length)
        verts = made["verts"]
        faces = list({f for v in verts for f in v.link_faces})
        self.bm.normal_update()
        caps = [f for f in faces if abs(f.normal.z) > 0.9]
        self._place(verts, at, rot)
        self.bm.normal_update()
        self._paint(faces, colour, top)
        if ends:
            for face in caps:
                face[self.colour] = NAMES.index(ends)
        return faces

    def sheet(self, points, at, colour, rot=(0, 0, 0), thick=0.012, rim=None):
        """A thin irregular plate from an outline in its own XY plane (hides, parchments)."""
        lower = [self.bm.verts.new((x, y, -thick / 2)) for x, y in points]
        upper = [self.bm.verts.new((x, y, thick / 2)) for x, y in points]
        faces = [self.bm.faces.new(upper), self.bm.faces.new(list(reversed(lower)))]
        n = len(points)
        for i in range(n):
            faces.append(self.bm.faces.new((lower[i], lower[(i + 1) % n], upper[(i + 1) % n], upper[i])))
        self._place(lower + upper, at, rot)
        self.bm.normal_update()
        self._paint(faces, colour)
        if rim:
            for face in faces[2:]:
                face[self.colour] = NAMES.index(rim)
        return faces

    def _place(self, verts, at, rot):
        matrix = Matrix.Translation(Vector(at)) @ Matrix.Rotation(rot[2], 4, "Z") @ Matrix.Rotation(rot[1], 4, "Y") @ Matrix.Rotation(rot[0], 4, "X")
        bmesh.ops.transform(self.bm, matrix=matrix, verts=verts)

    def jitter(self, amount):
        return self.rng.uniform(-amount, amount)

    def finish(self):
        """Triangulate, mark hard edges, lay every face onto its palette cell and make the object."""
        bmesh.ops.triangulate(self.bm, faces=self.bm.faces[:])
        self.bm.normal_update()
        uv = self.bm.loops.layers.uv.new("palette")
        for face in self.bm.faces:
            face.smooth = True
            u, v = cell_uv(NAMES[face[self.colour]])
            for loop in face.loops:
                loop[uv].uv = (u, v)
        for edge in self.bm.edges:
            if len(edge.link_faces) == 2 and edge.calc_face_angle(0.0) > math.radians(38):
                edge.smooth = False
        mesh = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(mesh)
        triangles = len(self.bm.faces)
        self.bm.free()
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        obj.data.materials.append(material())
        return obj, triangles


def material():
    mat = bpy.data.materials.get("VillageWork")
    if mat:
        return mat
    mat = bpy.data.materials.new("VillageWork")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    shader = nodes["Principled BSDF"]
    shader.inputs["Roughness"].default_value = 0.9
    for picture, socket in (("VillageWorkPalette", "Base Color"), ("VillageWorkGlow", "Emission Color")):
        texture = nodes.new("ShaderNodeTexImage")
        texture.image = bpy.data.images.get(picture)
        texture.interpolation = "Closest"
        mat.node_tree.links.new(texture.outputs["Color"], shader.inputs[socket])
    shader.inputs["Emission Strength"].default_value = 3.0
    return mat


def log(part, radius, length, at, rot, sides=8):
    part.cyl(radius, length, at, "bark", rot=rot, sides=sides, ends="cut")


# ---------------------------------------------------------------- the wood yard

PILE_X = -1.45


def woodyard():
    parts = []
    b = Part("Base", 11)
    # The stump everything is split on, a little wider at the foot, with the pale cut on top.
    b.cyl(0.42, 0.52, (0, 0, 0.26), "bark", sides=12, ends="cut", top_radius=0.37)
    # Chips and offcuts around it: what is left over, not firewood.
    for x, y, yaw in ((0.58, 0.20, 40), (0.66, -0.30, -25), (-0.40, 0.52, 15), (0.20, 0.64, 70), (0.44, -0.52, 110)):
        b.box((0.16, 0.05, 0.035), (x, y, 0.018), "cut", rot=(0.0, 0.0, math.radians(yaw + b.jitter(10))))
    # The stacking frame: two bearers on the ground and two stakes at each end.
    for y in (-0.30, 0.30):
        b.cyl(0.05, 1.56, (PILE_X, y, 0.05), "bark", rot=(0.0, math.radians(90), 0.0), sides=6, ends="cut")
    for x in (PILE_X - 0.72, PILE_X + 0.72):
        for y in (-0.38, 0.38):
            b.box((0.06, 0.06, 0.96), (x, y, 0.48), "wood_dark", rot=(b.jitter(0.04), b.jitter(0.04), 0.0), taper=0.7)
    b.box((0.30, 0.22, 0.012), (0.02, 0.03, 0.526), "ash", rot=(0.0, 0.0, math.radians(20)))
    parts.append(b.finish())

    # The pile fills from the bottom row up.
    number = 0
    for row, count in enumerate((5, 4, 3)):
        for i in range(count):
            p = Part("Stock_Wood_%02d" % number, 100 + number)
            x = PILE_X + (i - (count - 1) / 2.0) * 0.235 + p.jitter(0.012)
            log(p, 0.115, 1.0 + p.jitter(0.06), (x, p.jitter(0.03), 0.215 + row * 0.20), (math.radians(90), 0.0, p.jitter(0.04)))
            parts.append(p.finish())
            number += 1

    # One axe stands in the stump, the other leans head down against the stacking frame.
    p = Part("Stock_Axe_0", 150)
    p.box((0.045, 0.035, 0.74), (0.02, -0.02, 0.84), "wood_light", rot=(0.0, math.radians(24), math.radians(15)))
    p.box((0.20, 0.03, 0.13), (0.11, 0.0, 0.60), "iron", rot=(0.0, math.radians(24), math.radians(15)), taper=0.35)
    parts.append(p.finish())
    p = Part("Stock_Axe_1", 151)
    p.box((0.045, 0.035, 0.74), (PILE_X + 0.90, -0.52, 0.40), "wood_light", rot=(math.radians(-10), math.radians(-13), 0.0))
    p.box((0.20, 0.03, 0.13), (PILE_X + 0.99, -0.585, 0.075), "iron", rot=(math.radians(-10), math.radians(-13), 0.0), taper=0.35)
    parts.append(p.finish())
    return parts


# ---------------------------------------------------------------- the carpenter's bench

def workbench():
    parts = []
    b = Part("Base", 23)
    b.box((1.9, 0.72, 0.09), (0, 0, 0.86), "wood", top="wood_light")
    for x in (-0.84, 0.84):
        for y in (-0.28, 0.28):
            b.box((0.10, 0.10, 0.82), (x, y, 0.41), "wood_dark")
        b.box((0.07, 0.56, 0.07), (x, 0, 0.26), "wood_dark")
    b.box((1.64, 0.07, 0.07), (0, 0, 0.26), "wood_dark")
    # A vise at the left end, and the bench's own mallet.
    b.box((0.10, 0.34, 0.16), (-1.0, 0.0, 0.83), "wood_dark")
    b.cyl(0.02, 0.26, (-1.12, 0.0, 0.83), "iron", rot=(0.0, math.radians(90), 0.0), sides=6)
    b.cyl(0.05, 0.16, (-0.74, 0.20, 0.955), "wood_dark", rot=(math.radians(90), 0.0, math.radians(30)), sides=8)
    b.box((0.03, 0.03, 0.30), (-0.65, 0.27, 0.925), "wood_light", rot=(math.radians(90), 0.0, math.radians(-60)))
    b.box((0.36, 0.26, 0.012), (0.66, 0.20, 0.911), "ash")
    parts.append(b.finish())

    # Wooden frames lean against the right end.
    for k, lean in enumerate((0.26, 0.36)):
        p = Part("Stock_WoodFrame_%d" % k, 200 + k)
        x = 1.06 + k * 0.10
        for z in (0.10, 0.74):
            p.box((0.05, 0.62, 0.05), (x + (z - 0.42) * lean * 0.5, 0.0, z), "wood_light", rot=(0.0, math.radians(lean * 40), 0.0))
        for y in (-0.29, 0.29):
            p.box((0.05, 0.05, 0.68), (x, y, 0.42), "wood_light", rot=(0.0, math.radians(lean * 40), 0.0))
        parts.append(p.finish())

    # The first bow leans strung against the front, the second lies on the bench.
    p = Part("Stock_Bow_0", 210)
    for i in range(7):
        t = (i - 3) / 3.0
        p.box((0.035, 0.03, 0.22), (0.62 + 0.13 * (1 - t * t), -0.43, 0.72 + t * 0.62), "wood", rot=(0.0, math.radians(-22 * t), 0.0))
    p.cyl(0.006, 1.34, (0.62, -0.43, 0.72), "string", sides=4)
    parts.append(p.finish())
    p = Part("Stock_Bow_1", 211)
    for i in range(6):
        t = (i - 2.5) / 2.5
        p.box((0.24, 0.035, 0.03), (0.35 + t * 0.58, -0.10 + 0.10 * t * t, 0.925), "wood", rot=(0.0, 0.0, math.radians(18 * t)))
    p.cyl(0.006, 1.22, (0.35, 0.01, 0.925), "string", rot=(0.0, math.radians(90), 0.0), sides=4)
    parts.append(p.finish())

    # A broken axe waits for metal: the head with the stump of its helve, and the piece that split off.
    p = Part("Stock_BrokenAxe_0", 220)
    p.box((0.20, 0.13, 0.035), (-0.44, 0.10, 0.923), "iron", rot=(0.0, 0.0, math.radians(8)))
    p.box((0.30, 0.045, 0.035), (-0.20, 0.13, 0.923), "wood_light", rot=(0.0, 0.0, math.radians(8)))
    p.box((0.34, 0.045, 0.035), (0.14, 0.24, 0.923), "wood_light", rot=(0.0, 0.0, math.radians(-22)))
    p.box((0.03, 0.05, 0.037), (-0.045, 0.152, 0.923), "cut", rot=(0.0, 0.0, math.radians(8)))
    parts.append(p.finish())

    # Metal bars and bowls of thorn berries along the front edge.
    for k in range(2):
        p = Part("Stock_Metal_%d" % k, 230 + k)
        p.box((0.22, 0.075, 0.05), (-0.42 + k * 0.03, -0.27 + k * 0.095, 0.93), "iron", rot=(0.0, 0.0, math.radians(4 - k * 9)), taper=0.85)
        parts.append(p.finish())
    for k in range(2):
        p = Part("Stock_ThornBerry_%d" % k, 240 + k)
        x, y = -0.14 + k * 0.21, -0.25
        p.cyl(0.085, 0.05, (x, y, 0.93), "wood", sides=8, top_radius=0.10, top="berry")
        for i in range(5):
            angle = i / 5.0 * math.tau
            p.box((0.035, 0.035, 0.035), (x + math.cos(angle) * 0.045, y + math.sin(angle) * 0.045, 0.965), "berry", rot=(0.4, 0.3, angle))
        parts.append(p.finish())
    return parts


# ---------------------------------------------------------------- game and hides

def hide_outline(rng, width, height):
    """A pelt: a rounded body with the four legs pulled out to the corners, the neck up and the tail down."""
    points = []
    for i in range(16):
        angle = i / 16.0 * math.tau
        stretch = 0.82 + rng.uniform(-0.03, 0.03)
        if i in (2, 6, 10, 14):
            stretch = 1.34                      # legs, laced to the corners of the frame
        elif i in (4, 12):
            stretch = 1.05                      # neck and tail
        points.append((math.cos(angle) * width / 2 * stretch, math.sin(angle) * height / 2 * stretch))
    return points


def drying_rack():
    parts = []
    b = Part("Base", 37)
    for x in (-1.66, 1.66):
        for lean in (-1, 1):
            b.box((0.07, 0.07, 1.95), (x, lean * 0.22, 0.95), "wood_dark", rot=(math.radians(-13 * lean), 0.0, 0.0), taper=0.8)
    b.cyl(0.045, 3.6, (0, 0, 1.86), "bark", rot=(0.0, math.radians(90), 0.0), sides=6, ends="cut")
    frames = (-1.04, 0.0, 1.04)
    for x in frames:
        # A frame of four slats hangs from the beam; a hide is laced into it.
        for z in (0.42, 1.52):
            b.box((0.94, 0.04, 0.04), (x, 0, z), "wood")
        for dx in (-0.47, 0.47):
            b.box((0.04, 0.04, 1.14), (x + dx, 0, 0.97), "wood")
        for dx in (-0.3, 0.3):
            b.cyl(0.008, 0.30, (x + dx, 0, 1.69), "rope", sides=4)
    b.box((3.5, 0.10, 0.015), (0, 0, 1.912), "ash")
    parts.append(b.finish())
    for k, x in enumerate(frames):
        p = Part("Stock_Hide_%d" % k, 300 + k)
        p.sheet(hide_outline(p.rng, 0.66, 0.84), (x, 0, 0.97), "hide", rot=(math.radians(90), 0.0, 0.0), thick=0.02, rim="fur")
        for i in range(8):
            angle = i / 8.0 * math.tau + 0.2
            sx, sz = math.cos(angle) * 0.40, math.sin(angle) * 0.50
            p.cyl(0.006, 0.16, (x + sx, 0, 0.97 + sz), "rope", rot=(0.0, angle + math.radians(90), 0.0), sides=4)
        parts.append(p.finish())
    return parts


def game_rack():
    parts = []
    b = Part("Base", 67)
    for x in (-0.95, 0.95):
        b.box((0.08, 0.08, 1.75), (x, 0, 0.875), "wood_dark", taper=0.8)
        for side in (-1, 1):
            b.box((0.05, 0.05, 0.3), (x + side * 0.07, 0, 1.82), "wood_dark", rot=(0.0, math.radians(28 * side), 0.0))
    b.cyl(0.04, 2.2, (0, 0, 1.78), "bark", rot=(0.0, math.radians(90), 0.0), sides=6, ends="cut")
    b.box((2.1, 0.09, 0.015), (0, 0, 1.826), "ash")
    parts.append(b.finish())
    # Hares and birds hang head down from cords.
    hung = ((-0.63, 0.10, "fur", 0.46), (-0.21, 0.08, "feather", 0.32), (0.21, 0.11, "fur", 0.50), (0.63, 0.085, "feather", 0.34))
    for k, (x, body, colour, length) in enumerate(hung):
        p = Part("Stock_Game_%d" % k, 400 + k)
        p.cyl(0.006, 0.26, (x, 0, 1.63), "rope", sides=4)
        p.cyl(body, length, (x, 0, 1.50 - length / 2), colour, sides=6, top_radius=body * 0.45)
        p.cyl(body * 0.62, body * 1.3, (x, 0, 1.50 - length - body * 0.5), colour, sides=6, top_radius=body * 0.5)
        if colour == "fur":
            for ear in (-1, 1):
                p.box((0.02, 0.035, 0.14), (x + ear * 0.03, 0, 1.50 - length - body * 1.3), "fur", rot=(0.0, math.radians(12 * ear), 0.0), taper=0.4)
        else:
            for wing in (-1, 1):
                p.box((0.02, 0.16, 0.24), (x + wing * 0.085, 0, 1.50 - length * 0.55), "feather", rot=(0.0, math.radians(-10 * wing), 0.0), taper=0.5)
        parts.append(p.finish())
    return parts


# ---------------------------------------------------------------- the kitchen

def kitchen():
    parts = []
    b = Part("Base", 71)
    # The fire ring: stones around a bed of ash, with charred wood in it.
    for i in range(9):
        angle = i / 9.0 * math.tau + b.jitter(0.08)
        b.box((0.26 + b.jitter(0.04), 0.19 + b.jitter(0.03), 0.15 + b.jitter(0.03)), (math.cos(angle) * 0.52, math.sin(angle) * 0.52, 0.07),
              "stone" if i % 3 else "stone_dark", rot=(b.jitter(0.12), b.jitter(0.12), angle + math.radians(90)), taper=0.8)
    b.cyl(0.44, 0.03, (0, 0, 0.015), "ash", sides=10)
    for i in range(3):
        angle = i / 3.0 * math.tau + 0.5
        b.cyl(0.045, 0.52, (math.cos(angle) * 0.10, math.sin(angle) * 0.10, 0.09), "char", rot=(math.radians(78), 0.0, angle), sides=6)
    # A tripod of three poles holds the pot on a chain.
    lean = math.atan(0.75 / 1.75)
    for i in range(3):
        angle = math.radians(90 + i * 120)
        b.box((0.055, 0.055, 1.95), (math.cos(angle) * 0.375, math.sin(angle) * 0.375, 0.875), "wood_dark", rot=(0.0, -lean, angle), taper=0.8)
    b.cyl(0.07, 0.07, (0, 0, 1.74), "rope", sides=6)
    b.cyl(0.012, 0.74, (0, 0, 1.35), "iron_dark", sides=4)
    b.cyl(0.21, 0.26, (0, 0, 0.82), "iron_dark", sides=10, top_radius=0.24, top="stew")
    b.cyl(0.25, 0.03, (0, 0, 0.955), "iron", sides=10)
    b.cyl(0.19, 0.012, (0, 0, 0.972), "stew", sides=10)
    # The low table the cook works at.
    b.box((1.20, 0.52, 0.06), (1.28, 0, 0.62), "wood", top="wood_light")
    for x in (0.76, 1.80):
        for y in (-0.20, 0.20):
            b.box((0.07, 0.07, 0.59), (x, y, 0.295), "wood_dark")
    b.box((0.22, 0.14, 0.012), (1.74, 0.16, 0.656), "ash")
    parts.append(b.finish())

    # Rations wrapped in cloth lie on the table until somebody carries them to Mara.
    for k in range(4):
        p = Part("Stock_Ration_%d" % k, 500 + k)
        x = 0.86 + k * 0.255
        p.box((0.21, 0.26, 0.13), (x, -0.04 + p.jitter(0.03), 0.715), "cloth", rot=(0.0, 0.0, p.jitter(0.12)), taper=0.86)
        p.box((0.215, 0.03, 0.135), (x, -0.04, 0.715), "rope")
        p.box((0.03, 0.265, 0.135), (x, -0.04, 0.715), "rope")
        parts.append(p.finish())
    # Buckets of water stand in front of the table.
    for k in range(4):
        p = Part("Stock_Water_%d" % k, 510 + k)
        x = 0.84 + k * 0.30
        p.cyl(0.11, 0.26, (x, -0.56, 0.13), "wood", sides=8, top_radius=0.135, top="water")
        p.cyl(0.127, 0.03, (x, -0.56, 0.20), "iron_dark", sides=8)
        p.cyl(0.118, 0.03, (x, -0.56, 0.07), "iron_dark", sides=8)
        parts.append(p.finish())

    # The fire itself: only shown while somebody cooks.
    p = Part("Fire", 520)
    p.cyl(0.30, 0.03, (0, 0, 0.045), "ember", sides=8)
    flames = ((0.0, 0.0, 0.17, 0.58, "flame"), (0.13, 0.05, 0.11, 0.40, "flame"), (-0.11, 0.09, 0.10, 0.36, "flame"),
              (-0.03, -0.14, 0.11, 0.42, "flame"), (0.0, 0.0, 0.09, 0.36, "flame_core"), (0.08, -0.06, 0.06, 0.25, "flame_core"))
    for x, y, radius, height, colour in flames:
        p.cyl(radius, height, (x, y, 0.06 + height / 2), colour, rot=(p.jitter(0.10), p.jitter(0.10), p.jitter(1.5)), sides=5, top_radius=0.012)
    parts.append(p.finish())
    return parts


# ---------------------------------------------------------------- the well

def well():
    b = Part("Base", 83)
    # Two courses of stones around the shaft, and the dark water below the rim.
    for course in range(2):
        for i in range(10):
            angle = (i + course * 0.5) / 10.0 * math.tau
            b.box((0.46 + b.jitter(0.03), 0.24, 0.36), (math.cos(angle) * 0.64, math.sin(angle) * 0.64, 0.18 + course * 0.36),
                  "stone" if (i + course) % 3 else "stone_dark", rot=(0.0, 0.0, angle + math.radians(90)), taper=0.94)
    b.cyl(0.53, 0.04, (0, 0, 0.42), "water", sides=10)
    b.cyl(0.78, 0.05, (0, 0, 0.745), "stone_dark", sides=10, top="ash")
    b.cyl(0.52, 0.06, (0, 0, 0.75), "char", sides=10)
    # Two posts carry the windlass and a small roof.
    for x in (-0.80, 0.80):
        b.box((0.11, 0.11, 2.15), (x, 0, 1.075), "wood_dark", taper=0.85)
    b.cyl(0.07, 1.78, (0, 0, 1.62), "bark", rot=(0.0, math.radians(90), 0.0), sides=8, ends="cut")
    b.box((0.04, 0.04, 0.30), (0.98, 0, 1.50), "iron_dark")
    b.box((0.20, 0.04, 0.04), (1.06, 0, 1.36), "iron_dark")
    for turn in range(5):
        b.cyl(0.085, 0.03, (-0.22 + turn * 0.045, 0, 1.62), "rope", rot=(0.0, math.radians(90), 0.0), sides=8)
    # The bucket hangs over the shaft.
    b.cyl(0.008, 0.52, (-0.10, 0, 1.30), "rope", sides=4)
    b.cyl(0.11, 0.24, (-0.10, 0, 0.94), "wood", sides=8, top_radius=0.135, top="water")
    b.cyl(0.127, 0.03, (-0.10, 0, 1.00), "iron_dark", sides=8)
    for side in (-1, 1):
        b.box((1.96, 0.62, 0.04), (0, side * 0.27, 2.25), "wood_dark", rot=(math.radians(-24 * side), 0.0, 0.0), top="ash")
    b.box((1.98, 0.06, 0.06), (0, 0, 2.40), "wood")
    return [b.finish()]


# ---------------------------------------------------------------- the village board

def village_board():
    b = Part("Base", 41)
    for x in (-0.72, 0.72):
        b.box((0.11, 0.11, 2.15), (x, 0, 1.075), "wood_dark", taper=0.85)
    for i in range(5):
        b.box((1.36, 0.04, 0.19), (b.jitter(0.008), 0, 0.92 + i * 0.20), "wood" if i % 2 else "wood_dark")
    # A small roof keeps the ash off the notes.
    for side in (-1, 1):
        b.box((1.78, 0.36, 0.035), (0, side * 0.15, 2.22), "wood_dark", rot=(math.radians(-24 * side), 0.0, 0.0), top="ash")
    b.box((1.80, 0.05, 0.05), (0, 0, 2.30), "wood")
    # Notes: what the village has and what it lacks.
    notes = ((-0.36, 1.50, 0.34, 0.42, 4), (0.12, 1.56, 0.30, 0.30, -6), (0.40, 1.20, 0.26, 0.36, 3), (-0.20, 1.06, 0.40, 0.24, -2))
    for x, z, w, h, turn in notes:
        b.box((w, 0.008, h), (x, -0.027, z), "parchment", rot=(0.0, math.radians(turn), 0.0))
        for line in range(3):
            b.box((w * 0.66, 0.004, 0.018), (x - w * 0.06, -0.033, z + h * 0.24 - line * h * 0.2), "ink", rot=(0.0, math.radians(turn), 0.0))
        b.cyl(0.012, 0.02, (x, -0.035, z + h * 0.42), "iron_dark", rot=(math.radians(90), 0.0, 0.0), sides=5)
    # A tally stick hangs at the post and a red ribbon marks the urgent note.
    b.box((0.035, 0.02, 0.42), (0.86, -0.02, 1.30), "wood_light", rot=(0.0, math.radians(6), 0.0))
    b.box((0.05, 0.006, 0.20), (-0.36, -0.036, 1.22), "red", rot=(0.0, math.radians(8), 0.0))
    return [b.finish()]


WORKPLACES = (("WorkWoodyard", woodyard), ("WorkBench", workbench), ("WorkGameRack", game_rack), ("WorkDryingRack", drying_rack),
              ("WorkKitchen", kitchen), ("WorkWell", well), ("WorkBoard", village_board))


def write_pictures(folder):
    size = CELLS * 8
    for picture, glow in (("VillageWorkPalette", False), ("VillageWorkGlow", True)):
        image = bpy.data.images.new(picture, size, size, alpha=False)
        pixels = [0.0, 0.0, 0.0, 1.0] * (size * size) if glow else [0.3, 0.3, 0.3, 1.0] * (size * size)
        for index, (name, colour) in enumerate(PALETTE):
            if glow and name not in GLOWING:
                continue
            cx, cy = index % CELLS, index // CELLS
            # The picture is an sRGB picture: the colours above are written as they are meant to be seen.
            for y in range(8):
                for x in range(8):
                    at = ((cy * 8 + y) * size + cx * 8 + x) * 4
                    pixels[at:at + 3] = colour
        image.pixels = pixels
        image.filepath_raw = os.path.join(folder, picture + ".png")
        image.file_format = "PNG"
        image.save()


def export(name, objects, folder):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(filepath=os.path.join(folder, name + ".fbx"), use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             mesh_smooth_type="EDGE", use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False, path_mode="STRIP")


def preview(groups, path):
    """A quick look at all workplaces, fully stocked: sun, soft sky, seen from above like in the game."""
    scene = bpy.context.scene
    for i, objects in enumerate(groups):
        for obj in objects:
            obj.location = ((i % 4 - 1.5) * 4.6, -(i // 4) * 4.6, 0.0)
    bpy.ops.mesh.primitive_plane_add(size=44, location=(0, -1.5, 0))
    ground = bpy.context.active_object
    ash = bpy.data.materials.new("PreviewAsh")
    ash.diffuse_color = (0.42, 0.42, 0.44, 1.0)
    ground.data.materials.append(ash)
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sun.data.energy = 3.0
    sun.rotation_euler = (math.radians(52), 0.0, math.radians(35))
    scene.collection.objects.link(sun)
    world = bpy.data.worlds.new("Sky")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.36, 0.40, 0.46, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.9
    scene.world = world
    camera = bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera"))
    camera.location = (0.0, -15.6, 9.6)
    camera.rotation_euler = (math.radians(57), 0.0, 0.0)
    camera.data.lens = 34
    scene.collection.objects.link(camera)
    scene.camera = camera
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 24
    scene.cycles.use_denoising = False
    scene.render.resolution_x, scene.render.resolution_y = 1280, 720
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--output" not in argv:
        raise SystemExit("usage: blender --background --python village_work_props.py -- --output <folder> [--preview]")
    folder = os.path.abspath(argv[argv.index("--output") + 1])
    os.makedirs(folder, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    write_pictures(folder)
    groups = []
    for name, build in WORKPLACES:
        built = build()
        objects = [obj for obj, _ in built]
        triangles = sum(count for _, count in built)
        export(name, objects, folder)
        stock = {}
        for obj in objects:
            if obj.name.startswith("Stock_"):
                good = obj.name.split("_")[1]
                stock[good] = stock.get(good, 0) + 1
        print("WORKPLACE %s parts=%d triangles=%d stock=%s" % (name, len(objects), triangles, ",".join("%s:%d" % item for item in sorted(stock.items())) or "-"))
        if triangles > TRIANGLE_BUDGET:
            raise SystemExit("%s is over the triangle budget" % name)
        # Part names repeat from workplace to workplace; free them for the next one.
        for obj in objects:
            obj.name = name + "." + obj.name
        groups.append(objects)
    if "--preview" in argv:
        preview(groups, os.path.join(folder, "preview.png"))


main()
