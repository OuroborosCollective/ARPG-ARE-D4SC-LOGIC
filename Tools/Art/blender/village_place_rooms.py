"""Rooms of the village houses of Ashen Crypt, the field and the house signs, built in Blender without a window.

Three houses of Aschengrenze can be entered: the sick room, the smithy and the field barn. Each room is one model
file whose measures are those of the game's rules (VillagePlaceLayout): the room is centred on the origin, its door
is in the middle of the north wall (+Y), where the camera sees it, and every piece of furniture stands where the
rules have its block. The south wall is the one the camera looks in through.

  Shell              floor, walls, timber, the door in the north wall. The walls are seen from inside only, so the camera can look in.
  Furniture          everything that holds no goods
  Stock_<Good>_<n>   one unit of a good, in the order the room fills up (as in village_work_props.py)
  Fire               the fire of the forge or the hearth; shown while somebody is at work or in care here
  Lamp_<n>           the flame of a lamp; the game hangs a light at each
  Patient_<n>        somebody lying in the n-th bed of the sick room

  RoomSickRoom   three beds, hearth, herb rack, table, shelf, basin; herbs (6), dressings (4), leather (3)
  RoomSmithy     forge, bellows, anvil, quench barrel, bench, tool rack, bins; ore (6), metal (4), fittings (4), a broken axe (1)
  RoomBarn       grain (8), flour (4), straw (6), seed grain (3); tools and a hand cart
  WorkField      the field in the open: Crop_Sown, Crop_Grown and Crop_Ripe are its three states; compost (3)
  SignSickRoom, SignSmithy, SignBarn   the signs that hang at the three house doors

Run:  blender --background --python Tools/Art/blender/village_place_rooms.py -- --output <private art folder> [--preview]
Writes <Name>.fbx for each model and the two palette pictures it shares with the workplaces; with --preview also
preview-<Name>.png. Sizes are metres; +Z is up. The export turns the models for Unity as for the workplaces: the
side that is -Y here is the front (+Z) there, so the game turns a room's front to the south.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import village_work_props as base  # noqa: E402  (the palette, the part builder and the export)

ROOM_TRIANGLE_BUDGET = 9000
WALL = 2.8


class Part(base.Part):
    """The workplace part builder with two more shapes: a wall seen from one side, and a ball."""

    def plane(self, corners, colour, toward):
        verts = [self.bm.verts.new(c) for c in corners]
        face = self.bm.faces.new(verts)
        self.bm.normal_update()
        if face.normal.dot(Vector(toward) - face.calc_center_median()) < 0:
            face.normal_flip()
        face[self.colour] = base.NAMES.index(colour)
        return face

    def blade(self, radius, height, at, colour, top_radius=0.0):
        """A three-sided stalk or spike without end caps, standing on z: six triangles, or three for a spike."""
        made = bmesh.ops.create_cone(self.bm, cap_ends=False, segments=3, radius1=radius, radius2=top_radius, depth=height)
        verts = made["verts"]
        self._place(verts, (at[0], at[1], at[2] + height / 2), (0, 0, self.rng.uniform(0, 2.0)))
        faces = list({f for v in verts for f in v.link_faces})
        self.bm.normal_update()
        self._paint(faces, colour)
        return faces

    def ball(self, radius, at, colour, squash=(1.0, 1.0, 1.0), detail=1):
        made = bmesh.ops.create_icosphere(self.bm, subdivisions=detail, radius=radius)
        verts = made["verts"]
        bmesh.ops.scale(self.bm, vec=Vector(squash), verts=verts)
        self._place(verts, at, (0, 0, 0))
        faces = list({f for v in verts for f in v.link_faces})
        self.bm.normal_update()
        self._paint(faces, colour)
        return faces


# ---------------------------------------------------------------- shapes many rooms share

def band(part, hw, hd, wall, lo, hi, z0, z1, colour, inset=0.02):
    """A flat band on the inside of a wall ('N', 'S', 'E' or 'W'), from lo to hi along it and between two heights.
    Like the walls themselves it is seen from inside the room only."""
    if wall in ("N", "S"):
        y = (hd - inset) if wall == "N" else (-hd + inset)
        corners = [(lo, y, z0), (hi, y, z0), (hi, y, z1), (lo, y, z1)]
    else:
        x = (hw - inset) if wall == "E" else (-hw + inset)
        corners = [(x, lo, z0), (x, hi, z0), (x, hi, z1), (x, lo, z1)]
    part.plane(corners, colour, (0, 0, (z0 + z1) / 2))


def shell(part, hw, hd, door=0.8):
    """Floor of boards, four walls seen from inside, a timber frame painted onto them and the door in the north wall."""
    part.box((2 * hw, 2 * hd, 0.1), (0, 0, -0.05), "floor_dark", top="floor")
    y = -hd + 0.4
    while y < hd - 0.05:
        part.box((2 * hw - 0.02, 0.014, 0.006), (0, y, 0.003), "floor_dark")
        y += 0.4
    middle = (0, 0, WALL / 2)
    part.plane([(-hw, hd, 0), (hw, hd, 0), (hw, hd, WALL), (-hw, hd, WALL)], "plaster", middle)
    part.plane([(hw, -hd, 0), (hw, hd, 0), (hw, hd, WALL), (hw, -hd, WALL)], "plaster", middle)
    part.plane([(-hw, -hd, 0), (-hw, hd, 0), (-hw, hd, WALL), (-hw, -hd, WALL)], "plaster", middle)
    part.plane([(-hw, -hd, 0), (hw, -hd, 0), (hw, -hd, WALL), (-hw, -hd, WALL)], "plaster", middle)
    # Everything on the walls is flat and faces inward, so that nothing of a wall stands between the camera and the room.
    for wall, half in (("N", hw), ("S", hw), ("E", hd), ("W", hd)):
        runs = [(-half, half)] if wall != "N" else [(-half, -door), (door, half)]
        for lo, hi in runs:
            band(part, hw, hd, wall, lo, hi, 0.2, 0.95, "plaster_dark", inset=0.008)
            band(part, hw, hd, wall, lo, hi, 0.0, 0.2, "timber", inset=0.016)
            band(part, hw, hd, wall, lo, hi, 0.93, 1.0, "timber", inset=0.016)
        band(part, hw, hd, wall, -half, half, WALL - 0.16, WALL, "timber", inset=0.016)
        for end in (-half, half):
            band(part, hw, hd, wall, min(end, end - 0.16 * (1 if end > 0 else -1)), max(end, end - 0.16 * (1 if end > 0 else -1)), 0.0, WALL, "timber", inset=0.024)
        steps = max(1, int(half // 1.1))
        for i in range(1, 2 * steps):
            at = -half + i * half / steps
            if wall == "N" and abs(at) < door + 0.3:
                continue
            band(part, hw, hd, wall, at - 0.06, at + 0.06, 0.0, WALL, "timber", inset=0.024)
    # The door in the north wall, shut and flat like everything else on the walls: boards, three iron bands, a ring.
    # Whoever walks into it is let out. Frame and lintel lie over its edges; a worn mat lies in front of it.
    band(part, hw, hd, "N", -door, door, 0.0, 2.1, "wood", inset=0.02)
    board = -door + 0.27
    while board < door - 0.05:
        band(part, hw, hd, "N", board - 0.006, board + 0.006, 0.0, 2.1, "wood_dark", inset=0.024)
        board += 0.27
    for z in (0.35, 1.0, 1.65):
        band(part, hw, hd, "N", -door, door, z - 0.045, z + 0.045, "iron_dark", inset=0.027)
    band(part, hw, hd, "N", door - 0.28, door - 0.16, 1.02, 1.14, "iron", inset=0.031)
    for sx in (-1, 1):
        band(part, hw, hd, "N", sx * door + min(0, sx * 0.13), sx * door + max(0, sx * 0.13), 0.0, 2.23, "wood_dark", inset=0.03)
    band(part, hw, hd, "N", -door - 0.13, door + 0.13, 2.1, 2.26, "wood_dark", inset=0.03)
    part.box((1.3, 0.7, 0.02), (0, hd - 0.6, 0.01), "sack")


def window(part, hw, hd, wall, along, width=0.9):
    """A small window painted flat onto the inside of a wall: frame, dull panes, a cross bar and a sill."""
    band(part, hw, hd, wall, along - width / 2, along + width / 2, 1.3, 2.1, "timber", inset=0.03)
    band(part, hw, hd, wall, along - width / 2 + 0.07, along + width / 2 - 0.07, 1.37, 2.03, "glass", inset=0.036)
    band(part, hw, hd, wall, along - 0.025, along + 0.025, 1.37, 2.03, "timber", inset=0.04)
    band(part, hw, hd, wall, along - width / 2 + 0.07, along + width / 2 - 0.07, 1.68, 1.72, "timber", inset=0.04)
    band(part, hw, hd, wall, along - width / 2 - 0.05, along + width / 2 + 0.05, 1.24, 1.3, "wood_dark", inset=0.045)


def lamp(name, at, toward):
    """A lantern on a wall arm; its flame is a part of its own so that the game can hang a light there."""
    arm = Part("Furniture", 5)
    x, y, z = at
    dx, dy = toward
    arm.box((0.05 + abs(dx) * 0.3, 0.05 + abs(dy) * 0.3, 0.05), (x + dx * 0.15, y + dy * 0.15, z + 0.2), "iron_dark")
    arm.box((0.03, 0.03, 0.14), (x + dx * 0.3, y + dy * 0.3, z + 0.12), "iron_dark")
    arm.box((0.16, 0.16, 0.03), (x + dx * 0.3, y + dy * 0.3, z - 0.11), "iron_dark")
    arm.box((0.18, 0.18, 0.03), (x + dx * 0.3, y + dy * 0.3, z + 0.05), "iron_dark", taper=0.5)
    for sx in (-1, 1):
        for sy in (-1, 1):
            arm.box((0.018, 0.018, 0.16), (x + dx * 0.3 + sx * 0.07, y + dy * 0.3 + sy * 0.07, z - 0.03), "iron_dark")
    flame = Part(name, 6)
    flame.cyl(0.035, 0.11, (x + dx * 0.3, y + dy * 0.3, z - 0.04), "flame_core", sides=6, top_radius=0.006)
    return arm, flame


def table(part, cx, cy, w, d, h=0.78, colour="wood", legs="wood_dark"):
    part.box((w, d, 0.06), (cx, cy, h - 0.03), colour, top="wood_light")
    for sx in (-1, 1):
        for sy in (-1, 1):
            part.box((0.08, 0.08, h - 0.06), (cx + sx * (w / 2 - 0.09), cy + sy * (d / 2 - 0.09), (h - 0.06) / 2), legs)
    part.box((w - 0.2, 0.05, 0.08), (cx, cy - d / 2 + 0.09, h - 0.14), legs)
    part.box((w - 0.2, 0.05, 0.08), (cx, cy + d / 2 - 0.09, h - 0.14), legs)


def crate(part, cx, cy, w, d, h, colour="wood", rim="wood_dark"):
    """An open bin: four board walls on a bottom."""
    part.box((w, d, 0.04), (cx, cy, 0.06), colour)
    for sx in (-1, 1):
        part.box((0.04, d, h), (cx + sx * (w / 2 - 0.02), cy, h / 2 + 0.04), colour)
    for sy in (-1, 1):
        part.box((w, 0.04, h), (cx, cy + sy * (d / 2 - 0.02), h / 2 + 0.04), colour)
    for sx in (-1, 1):
        for sy in (-1, 1):
            part.box((0.07, 0.07, h + 0.06), (cx + sx * (w / 2 - 0.02), cy + sy * (d / 2 - 0.02), h / 2 + 0.04), rim)


def barrel(part, cx, cy, radius, height, water=False):
    part.cyl(radius, height, (cx, cy, height / 2), "wood", sides=10, ends="wood_dark")
    for z in (height * 0.22, height * 0.78):
        part.cyl(radius + 0.012, 0.05, (cx, cy, z), "iron_dark", sides=10)
    if water:
        part.cyl(radius - 0.04, 0.02, (cx, cy, height - 0.03), "water", sides=10)


def sack(part, cx, cy, z, radius, height, colour="sack", lean=0.0, turn=0.0):
    # A filled sack standing on z, tied at the neck.
    part.cyl(radius, height, (cx, cy, z + height / 2), colour, rot=(lean, 0, turn), sides=7, top_radius=radius * 0.72)
    part.cyl(radius * 0.3, 0.09, (cx, cy, z + height + 0.03), "rope", sides=5, top_radius=radius * 0.42)


def bale(part, cx, cy, z, turn=0.0):
    part.box((0.95, 0.5, 0.42), (cx, cy, z + 0.21), "straw", rot=(0, 0, turn))
    for offset in (-0.25, 0.25):
        part.box((0.025, 0.52, 0.44), (cx + math.cos(turn) * offset, cy + math.sin(turn) * offset, z + 0.21), "rope", rot=(0, 0, turn))


# ---------------------------------------------------------------- the sick room

def bed(part, cx, cy):
    """A cot with its head to the west: frame, straw mattress, pillow and a folded blanket at the foot."""
    part.box((2.0, 0.92, 0.10), (cx, cy, 0.34), "wood_dark")
    for sx in (-1, 1):
        for sy in (-1, 1):
            part.box((0.09, 0.09, 0.34 if sx > 0 else 0.62), (cx + sx * 0.95, cy + sy * 0.41, 0.17 if sx > 0 else 0.31), "wood_dark")
    part.box((0.07, 0.92, 0.26), (cx - 0.97, cy, 0.52), "wood")
    part.box((1.9, 0.84, 0.13), (cx + 0.02, cy, 0.455), "linen")
    part.box((0.42, 0.62, 0.09), (cx - 0.68, cy, 0.565), "bandage", taper=0.85)
    part.box((0.5, 0.8, 0.07), (cx + 0.68, cy, 0.555), "blanket")


def patient(index, cx, cy):
    """Somebody under a blanket: a head on the pillow, shoulders, and the long hump of a body."""
    part = Part("Patient_%d" % index, 40 + index)
    part.ball(0.115, (cx - 0.66, cy, 0.70), "skin", squash=(1.0, 0.92, 0.95))
    part.ball(0.118, (cx - 0.705, cy, 0.715), "fur" if index != 1 else "bark", squash=(0.8, 0.95, 0.9))
    part.box((0.5, 0.62, 0.17), (cx - 0.30, cy, 0.60), "blanket", taper=0.82)
    part.box((1.02, 0.5, 0.15), (cx + 0.36, cy, 0.585), "blanket", taper=0.78)
    part.box((0.2, 0.3, 0.2), (cx + 0.82, cy, 0.60), "blanket", taper=0.7)
    return part


def hearth(cx, cy, width=1.8):
    """A stone fireplace against the north wall; the fire is a part of its own."""
    furn = Part("Furniture", 21)
    furn.box((width, 0.9, 0.34), (cx, cy, 0.17), "stone", top="stone_dark")
    for sx in (-1, 1):
        furn.box((0.26, 0.9, 1.25), (cx + sx * (width / 2 - 0.13), cy, 0.96), "stone")
    furn.box((width, 0.16, 1.25), (cx, cy + 0.37, 0.96), "stone_dark")
    furn.box((width, 0.9, 0.2), (cx, cy, 1.68), "stone")
    furn.box((width - 0.2, 0.86, 0.95), (cx, cy + 0.02, 2.25), "stone", taper=0.55)
    furn.box((0.5, 0.5, 0.3), (cx, cy + 0.15, 2.66), "stone_dark")
    for i, turn in enumerate((0.3, -0.5, 1.2)):
        base.log(furn, 0.055, 0.55, (cx - 0.15 + i * 0.14, cy - 0.05, 0.40 + (i % 2) * 0.05), (math.radians(90), 0, turn), sides=6)
    furn.cyl(0.3, 0.05, (cx, cy - 0.02, 0.365), "char", sides=8)
    fire = Part("Fire", 22)
    for i, (dx, dy, h) in enumerate(((0.0, 0.0, 0.5), (-0.17, 0.05, 0.36), (0.16, -0.04, 0.38), (0.05, 0.1, 0.3))):
        fire.cyl(0.1 if i == 0 else 0.075, h, (cx + dx, cy - 0.03 + dy, 0.42 + h / 2), "flame", sides=6, top_radius=0.008)
    fire.cyl(0.05, 0.26, (cx, cy - 0.03, 0.52), "flame_core", sides=6, top_radius=0.006)
    fire.cyl(0.24, 0.035, (cx, cy - 0.02, 0.405), "hot", sides=8)
    return furn, fire


def sick_room():
    hw, hd = 4.0, 3.5
    walls = Part("Shell", 11)
    shell(walls, hw, hd)
    window(walls, hw, hd, "W", -3.0)
    window(walls, hw, hd, "E", -1.2)
    furn = Part("Furniture", 12)
    parts = []
    for index, y in enumerate((2.2, 0.4, -1.4)):
        bed(furn, -2.9, y)
        parts.append(patient(index, -2.9, y))
        # A stool beside every bed, for whoever sits with the sick.
        furn.cyl(0.17, 0.04, (-1.55, y - 0.25, 0.44), "wood", sides=8)
        for a in range(3):
            furn.box((0.04, 0.04, 0.42), (-1.55 + math.cos(a * 2.1) * 0.11, y - 0.25 + math.sin(a * 2.1) * 0.11, 0.21), "wood_dark")
    # Hearth and herb rack stand at the north wall, to either side of the door.
    stove, fire = hearth(2.15, 3.05)
    # Herb rack: a pole on two arms, the bundles hang from it.
    rack = -2.05
    for sx in (-1, 1):
        furn.box((0.05, 0.3, 0.05), (rack + sx * 0.72, 3.33, 1.95), "wood_dark")
    furn.cyl(0.025, 1.6, (rack, 3.2, 1.95), "wood", rot=(0, math.radians(90), 0), sides=6)
    for i in range(6):
        herb = Part("Stock_Herb_%02d" % i, 60 + i)
        x = rack - 0.65 + i * 0.26
        herb.cyl(0.012, 0.12, (x, 3.2, 1.87), "rope", sides=5)
        herb.cyl(0.03, 0.34, (x, 3.2, 1.64), "herb", sides=6, top_radius=0.105)
        herb.cyl(0.105, 0.07, (x, 3.2, 1.44), "herb", sides=6, top_radius=0.02)
        parts.append(herb)
    # The carer's table along the east wall: mortar, bowl, candle, and the rolled dressings.
    ty = 0.0
    table(furn, 3.3, ty, 1.3, 2.1)
    furn.cyl(0.11, 0.12, (3.45, ty + 0.7, 0.84), "stone", sides=8, top_radius=0.14)
    furn.cyl(0.025, 0.2, (3.48, ty + 0.72, 0.93), "wood_dark", rot=(0.5, 0.2, 0), sides=5)
    furn.cyl(0.13, 0.06, (3.05, ty + 0.75, 0.81), "wood_light", sides=8, top_radius=0.16)
    furn.cyl(0.03, 0.16, (3.6, ty - 0.8, 0.86), "string", sides=6)
    furn.box((0.5, 0.36, 0.015), (3.15, ty - 0.5, 0.79), "linen")
    for i in range(4):
        roll = Part("Stock_Bandage_%02d" % i, 70 + i)
        roll.cyl(0.055, 0.16, (3.1 + (i % 2) * 0.2, ty - 0.08 + (i // 2) * 0.2, 0.835), "bandage", rot=(math.radians(90), 0, 0.2 * i), sides=8, ends="linen")
        parts.append(roll)
    # Shelf in the north-east corner: jars above, the leather for dressings below.
    for z in (0.35, 0.9, 1.45, 2.0):
        furn.box((0.7, 1.3, 0.04), (3.62, 2.7, z), "wood")
    for sy in (-1, 1):
        furn.box((0.7, 0.05, 2.05), (3.62, 2.7 + sy * 0.66, 1.025), "wood_dark")
    for i, colour in enumerate(("glass", "stone", "copper", "glass", "stone")):
        furn.cyl(0.07, 0.17, (3.6, 2.2 + i * 0.25, 1.555), colour, sides=7)
        furn.cyl(0.045, 0.03, (3.6, 2.2 + i * 0.25, 1.655), "wood_dark", sides=6)
    for i, colour in enumerate(("stone", "glass", "copper")):
        furn.cyl(0.085, 0.2, (3.6, 2.3 + i * 0.36, 2.12), colour, sides=7)
    for i in range(3):
        leather = Part("Stock_Leather_%02d" % i, 80 + i)
        leather.cyl(0.075, 0.42, (3.6, 2.3 + i * 0.36, 0.995), "leather", rot=(0, math.radians(90), 0), sides=8, ends="hide")
        parts.append(leather)
    # Wash tub with a towel over its rim.
    barrel(furn, 3.3, -2.4, 0.4, 0.55, water=True)
    furn.box((0.3, 0.06, 0.34), (3.3, -2.78, 0.42), "linen")
    lamps = []
    for name, at, toward in (("Lamp_0", (-3.93, 1.3, 1.95), (1, 0)), ("Lamp_1", (3.93, -1.1, 1.95), (-1, 0))):
        arm, flame = lamp(name, at, toward)
        lamps.extend((arm, flame))
    return [walls, furn, stove, fire] + parts + lamps


# ---------------------------------------------------------------- the smithy

def anvil(part, cx, cy):
    part.cyl(0.3, 0.5, (cx, cy, 0.25), "bark", sides=9, ends="cut")
    part.box((0.3, 0.22, 0.1), (cx, cy, 0.55), "iron_dark")
    part.box((0.16, 0.14, 0.14), (cx, cy, 0.67), "iron_dark")
    part.box((0.5, 0.2, 0.12), (cx, cy, 0.8), "iron", top="ingot")
    part.cyl(0.085, 0.3, (cx, cy + 0.38, 0.8), "iron", rot=(math.radians(-90), 0, 0), sides=7, top_radius=0.012)
    # The hammer rests on the stump's edge.
    part.box((0.035, 0.34, 0.035), (cx + 0.2, cy - 0.05, 0.535), "wood", rot=(0, 0, 0.5))
    part.box((0.12, 0.06, 0.06), (cx + 0.28, cy + 0.09, 0.535), "iron_dark", rot=(0, 0, 0.5))


def smithy():
    hw, hd = 4.5, 4.0
    walls = Part("Shell", 111)
    shell(walls, hw, hd)
    window(walls, hw, hd, "W", -3.0)
    window(walls, hw, hd, "E", -1.8)
    furn = Part("Furniture", 112)
    parts = []
    # The forge at the north wall, west of the door: a stone table with the fire bed, a hood and a chimney breast above.
    fx = -2.45
    furn.box((2.3, 1.2, 0.8), (fx, 3.4, 0.4), "stone", top="stone_dark")
    furn.box((2.3, 0.18, 1.1), (fx, 3.91, 1.35), "stone_dark")
    for sx in (-1, 1):
        furn.box((0.2, 1.0, 0.5), (fx + sx * 1.05, 3.45, 1.05), "stone")
    furn.box((2.4, 1.25, 0.9), (fx, 3.38, 2.3), "stone", taper=0.5)
    furn.box((0.7, 0.7, 0.25), (fx, 3.55, 2.68), "stone_dark")
    furn.cyl(0.42, 0.06, (fx, 3.3, 0.82), "coal", sides=9)
    for i in range(7):
        furn.ball(0.07, (fx - 0.3 + i * 0.1, 3.22 + (i % 3) * 0.08, 0.87), "coal", squash=(1, 1, 0.7))
    fire = Part("Fire", 113)
    fire.cyl(0.36, 0.04, (fx, 3.3, 0.875), "hot", sides=9)
    for i, (dx, dy, h) in enumerate(((0.0, 0.0, 0.42), (-0.16, 0.06, 0.3), (0.15, -0.05, 0.32), (0.04, 0.12, 0.26), (-0.06, -0.12, 0.22))):
        fire.cyl(0.085 if i == 0 else 0.06, h, (fx + dx, 3.3 + dy, 0.9 + h / 2), "flame", sides=6, top_radius=0.006)
    fire.cyl(0.04, 0.2, (fx, 3.3, 0.98), "flame_core", sides=6, top_radius=0.005)
    # A bar of iron lies in the fire while it burns.
    fire.box((0.55, 0.04, 0.04), (fx + 0.25, 3.12, 0.9), "hot", rot=(0, 0, 0.5))
    # Bellows in the corner west of the forge: two boards, a leather bag between them, the nose towards the fire.
    bx = -4.05
    furn.box((0.5, 0.9, 0.5), (bx, 3.35, 0.25), "wood_dark")
    furn.box((0.6, 0.95, 0.05), (bx, 3.3, 0.54), "wood")
    furn.box((0.56, 0.9, 0.26), (bx, 3.3, 0.7), "leather", taper=0.8)
    furn.box((0.6, 0.95, 0.05), (bx, 3.3, 0.86), "wood")
    furn.cyl(0.05, 0.3, (bx + 0.42, 3.3, 0.72), "iron_dark", rot=(0, math.radians(90), 0), sides=6, top_radius=0.03)
    furn.box((0.05, 0.05, 1.3), (bx - 0.15, 2.85, 1.2), "wood_dark")
    furn.box((0.05, 0.9, 0.05), (bx - 0.15, 2.5, 1.85), "wood", rot=(0.25, 0, 0))
    # The anvil stands in front of the forge, the quench barrel between it and the west wall.
    anvil(furn, -2.2, 1.1)
    barrel(furn, -3.9, 1.3, 0.38, 0.75, water=True)
    # Bench along the east wall with a leg vice; the finished fittings lie on it.
    by = 0.8
    table(furn, 4.0, by, 0.9, 2.5, h=0.88)
    furn.box((0.14, 0.1, 0.3), (3.62, by - 0.9, 0.98), "iron_dark")
    furn.box((0.14, 0.1, 0.3), (3.62, by - 1.02, 0.98), "iron")
    furn.cyl(0.02, 0.3, (3.62, by - 0.96, 1.02), "iron_dark", rot=(math.radians(90), 0, 0), sides=5)
    for i in range(4):
        fitting = Part("Stock_Fitting_%02d" % i, 130 + i)
        y = by - 0.25 + i * 0.36
        fitting.box((0.3, 0.07, 0.02), (4.0, y, 0.89), "iron")
        fitting.box((0.07, 0.2, 0.02), (4.11, y + 0.07, 0.89), "iron")
        fitting.cyl(0.022, 0.03, (3.9, y, 0.905), "iron_dark", sides=5)
        parts.append(fitting)
    # Tool rack on the west wall: tongs and hammers hang on a board; a broken axe waits below.
    ry = -1.3
    furn.box((0.05, 1.9, 0.5), (-4.42, ry, 1.55), "wood")
    for i, y in enumerate((ry - 0.7, ry - 0.35, ry, ry + 0.35, ry + 0.7)):
        furn.box((0.04, 0.03, 0.42), (-4.36, y, 1.5), "iron_dark", rot=(0, 0.06 * (i - 2), 0))
        if i % 2 == 0:
            furn.box((0.05, 0.11, 0.07), (-4.36, y, 1.72), "iron")
        else:
            furn.box((0.04, 0.03, 0.42), (-4.36, y + 0.05, 1.5), "iron_dark", rot=(0, -0.1, 0))
    furn.box((0.3, 1.9, 0.05), (-4.3, ry, 0.5), "wood")
    for sy in (-1, 1):
        furn.box((0.3, 0.05, 0.5), (-4.3, ry + sy * 0.9, 0.25), "wood_dark")
    broken = Part("Stock_BrokenAxe_00", 140)
    broken.box((0.045, 0.045, 0.5), (-4.3, ry - 0.2, 0.78), "wood", rot=(0.5, 0, 0))
    broken.box((0.045, 0.2, 0.045), (-4.3, ry + 0.25, 0.55), "wood", rot=(0, 0, 0.2))
    broken.box((0.05, 0.2, 0.15), (-4.3, ry - 0.38, 0.95), "iron", rot=(0.5, 0, 0), taper=0.6)
    parts.append(broken)
    # East of the door at the north wall: the ore bin, and beside it the table with the ingots. Coal in the south-west corner.
    ox = 2.05
    crate(furn, ox, 3.4, 1.5, 1.0, 0.5)
    for i in range(6):
        ore = Part("Stock_Ore_%02d" % i, 150 + i)
        ore.ball(0.17, (ox - 0.45 + (i % 3) * 0.43, 3.2 + (i // 3) * 0.38, 0.3 + (i // 3) * 0.12 + (i % 2) * 0.04), "ore", squash=(1.1, 0.9, 0.75))
        ore.ball(0.07, (ox - 0.4 + (i % 3) * 0.43, 3.24 + (i // 3) * 0.38, 0.4 + (i // 3) * 0.12), "copper", squash=(1, 1, 0.6))
        parts.append(ore)
    ix = 3.65
    table(furn, ix, 3.4, 1.5, 1.0, h=0.6)
    for i in range(4):
        ingot = Part("Stock_Metal_%02d" % i, 160 + i)
        ingot.box((0.34, 0.13, 0.09), (ix - 0.4 + (i % 2) * 0.5, 3.25 + (i // 2) * 0.3, 0.645), "ingot", taper=0.8)
        parts.append(ingot)
    cx, cy = -3.3, -3.2
    crate(furn, cx, cy, 1.1, 1.1, 0.4)
    for i in range(9):
        furn.ball(0.13, (cx - 0.3 + (i % 3) * 0.3, cy - 0.3 + (i // 3) * 0.3, 0.42 + ((i * 7) % 3) * 0.04), "coal", squash=(1, 1, 0.8))
    lamps = []
    for name, at, toward in (("Lamp_0", (4.43, -2.6, 1.95), (-1, 0)), ("Lamp_1", (-4.43, 0.3, 1.95), (1, 0))):
        arm, flame = lamp(name, at, toward)
        lamps.extend((arm, flame))
    return [walls, furn, fire] + parts + lamps


# ---------------------------------------------------------------- the field barn

def barn():
    hw, hd = 5.0, 4.0
    walls = Part("Shell", 211)
    shell(walls, hw, hd)
    window(walls, hw, hd, "E", -1.0)
    furn = Part("Furniture", 212)
    parts = []
    # Grain in sacks on a low pallet in the north-west corner: five stand, three lie on top.
    furn.box((2.3, 1.9, 0.12), (-3.7, 2.9, 0.06), "wood_dark")
    for i in range(8):
        grain = Part("Stock_Grain_%02d" % i, 220 + i)
        if i < 5:
            sack(grain, -4.5 + (i % 3) * 0.72 + (0.36 if i >= 3 else 0), 2.5 + (i // 3) * 0.75, 0.12, 0.3, 0.72, turn=i * 0.7)
        else:
            j = i - 5
            grain.cyl(0.27, 0.8, (-4.3 + j * 0.62, 2.9, 1.03), "sack", rot=(math.radians(90), 0, 0.15 * (j - 1)), sides=7, top_radius=0.2)
        parts.append(grain)
    # Flour comes back from the mill in pale sacks; they stand on a bench at the north wall, east of the door.
    flx = 2.15
    table(furn, flx, 3.4, 1.7, 1.0, h=0.5)
    for i in range(4):
        flour = Part("Stock_Flour_%02d" % i, 230 + i)
        sack(flour, flx - 0.6 + i * 0.4, 3.4, 0.5, 0.19, 0.5, colour="linen", turn=i)
        parts.append(flour)
    # Straw bales against the east wall, two layers.
    sy0 = 0.2
    for i in range(6):
        straw = Part("Stock_Straw_%02d" % i, 240 + i)
        if i < 4:
            bale(straw, 3.25 + (i % 2) * 0.98, sy0 + (i // 2) * 1.1, 0.0, turn=math.radians(90) if i % 2 else 0.0)
        else:
            bale(straw, 3.7, sy0 + 0.05 + (i - 4) * 1.1, 0.42, turn=0.1 * (i - 4))
        parts.append(straw)
    furn.box((0.9, 0.5, 0.05), (3.7, sy0 + 2.2, 0.03), "straw")
    # Seed bin at the west wall: a lidless chest with the small seed bags.
    crate(furn, -4.4, -0.3, 1.0, 1.3, 0.6)
    for i in range(3):
        seed = Part("Stock_SeedGrain_%02d" % i, 250 + i)
        sack(seed, -4.4, -0.7 + i * 0.4, 0.3, 0.16, 0.42, colour="cloth", turn=i * 1.3)
        parts.append(seed)
    # Tools on the west wall: pitchfork, rake and flail.
    furn.box((0.05, 1.5, 0.12), (-4.93, -2.4, 1.7), "wood_dark")
    furn.cyl(0.02, 1.7, (-4.85, -2.9, 1.0), "wood", sides=5)
    for k in (-1, 0, 1):
        furn.cyl(0.012, 0.3, (-4.85, -2.9 + k * 0.07, 1.98), "iron_dark", sides=4)
    furn.cyl(0.02, 1.7, (-4.85, -2.4, 1.0), "wood", sides=5)
    furn.box((0.04, 0.5, 0.04), (-4.85, -2.4, 1.86), "wood_dark")
    for k in range(6):
        furn.cyl(0.01, 0.12, (-4.85, -2.62 + k * 0.088, 1.8), "wood_dark", sides=4)
    furn.cyl(0.02, 1.3, (-4.85, -1.9, 1.05), "wood", sides=5)
    furn.cyl(0.03, 0.6, (-4.8, -1.9, 1.5), "wood_dark", rot=(0.5, 0, 0), sides=5)
    # A hand cart in the south-east corner.
    furn.box((1.5, 0.9, 0.06), (2.6, -2.3, 0.55), "wood")
    for sy in (-1, 1):
        furn.box((1.5, 0.05, 0.3), (2.6, -2.3 + sy * 0.43, 0.72), "wood")
        furn.cyl(0.36, 0.07, (2.75, -2.3 + sy * 0.52, 0.36), "wood_dark", rot=(math.radians(90), 0, 0), sides=10, ends="wood")
        furn.box((1.1, 0.05, 0.05), (1.5, -2.3 + sy * 0.3, 0.6), "wood_dark", rot=(0, 0.12, 0))
    furn.box((0.05, 0.9, 0.3), (3.33, -2.3, 0.72), "wood")
    furn.box((0.08, 0.08, 0.5), (1.95, -2.3, 0.25), "wood_dark")
    lamps = []
    for name, at, toward in (("Lamp_0", (-4.93, 1.2, 1.95), (1, 0)), ("Lamp_1", (4.93, -2.9, 1.95), (-1, 0))):
        arm, flame = lamp(name, at, toward)
        lamps.extend((arm, flame))
    return [walls, furn] + parts + lamps


# ---------------------------------------------------------------- the field

def field():
    """Seven by five metres of ridged soil; the worker stands in front of it (-Y). Three crops for its three states."""
    soil = Part("Base", 311)
    soil.box((7.2, 5.2, 0.12), (0, 2.0, 0.06), "soil")
    rows = [-0.1 + i * 0.6 for i in range(8)]
    for y in rows:
        soil.box((6.8, 0.34, 0.14), (0, y, 0.15), "soil", taper=0.55)
    # A low wattle fence on three sides and a scarecrow in the far corner.
    for i in range(13):
        x = -3.6 + i * 0.6
        soil.box((0.06, 0.06, 0.62), (x, 4.6, 0.31), "wood_dark")
    for sx in (-1, 1):
        for i in range(9):
            soil.box((0.06, 0.06, 0.62), (sx * 3.6, -0.4 + i * 0.625, 0.31), "wood_dark")
        for z in (0.2, 0.42):
            soil.box((0.035, 5.0, 0.07), (sx * 3.6, 2.1, z), "bark")
    for z in (0.2, 0.42):
        soil.box((7.2, 0.035, 0.07), (0, 4.6, z), "bark")
    soil.box((0.07, 0.07, 1.9), (2.9, 3.9, 0.95), "wood_dark")
    soil.box((1.2, 0.06, 0.06), (2.9, 3.9, 1.45), "wood_dark")
    soil.box((0.5, 0.2, 0.6), (2.9, 3.9, 1.3), "cloth", taper=0.8)
    soil.ball(0.16, (2.9, 3.9, 1.82), "sack")
    soil.cyl(0.26, 0.05, (2.9, 3.9, 1.96), "straw", sides=8, top_radius=0.2)
    # The compost bay on the left, three boards high.
    for sy in (-1, 1):
        soil.box((1.5, 0.05, 0.6), (-4.45, 0.4 + sy * 0.75, 0.3), "wood_dark")
    soil.box((0.05, 1.5, 0.6), (-5.2, 0.4, 0.3), "wood_dark")
    parts = []
    for i in range(3):
        heap = Part("Stock_Compost_%02d" % i, 320 + i)
        heap.ball(0.42, (-4.75 + i * 0.36, 0.4 + (i % 2) * 0.2 - 0.1, 0.27), "compost", squash=(1.0, 1.0, 0.62))
        heap.ball(0.16, (-4.7 + i * 0.36, 0.5, 0.49), "straw", squash=(1.2, 1.0, 0.4))
        parts.append(heap)
    sown = Part("Crop_Sown", 331)
    grown = Part("Crop_Grown", 332)
    ripe = Part("Crop_Ripe", 333)
    for r, y in enumerate(rows):
        for i in range(13):
            x = -3.1 + i * 0.5 + (0.25 if r % 2 else 0.0)
            if x > 3.3:
                continue
            sown.blade(0.04, 0.11, (x, y, 0.21), "sprout")
            grown.blade(0.06, 0.44, (x, y, 0.21), "sprout", top_radius=0.015)
            ripe.blade(0.035, 0.68, (x, y, 0.21), "wheat", top_radius=0.022)
            ripe.blade(0.065, 0.22, (x, y, 0.86), "wheat")
    return [soil] + parts + [sown, grown, ripe]


# ---------------------------------------------------------------- the house signs

def sign(kind):
    """A board on an iron arm beside the door: an anvil for the smithy, a herb sprig for the sick room, a sheaf for the barn."""
    part = Part("Base", 400 + len(kind))
    part.box((0.09, 0.09, 2.6), (0, 0, 1.3), "wood_dark")
    part.box((0.9, 0.05, 0.05), (0.42, 0, 2.45), "iron_dark")
    part.box((0.04, 0.04, 0.5), (0.2, 0, 2.2), "iron_dark", rot=(0, 0.75, 0))
    for x in (0.25, 0.75):
        part.box((0.02, 0.02, 0.16), (x, 0, 2.34), "iron_dark")
    part.box((0.7, 0.05, 0.56), (0.5, 0, 1.98), "wood", taper=0.96)
    part.box((0.74, 0.07, 0.04), (0.5, 0, 2.26), "wood_dark")
    for side in (-1, 1):
        y = side * 0.032
        if kind == "Smithy":
            part.box((0.34, 0.012, 0.07), (0.5, y, 2.07), "iron_dark")
            part.box((0.14, 0.012, 0.1), (0.5, y, 1.99), "iron_dark")
            part.box((0.26, 0.012, 0.07), (0.5, y, 1.9), "iron_dark")
            part.box((0.12, 0.012, 0.05), (0.7, y, 2.07), "iron_dark", taper=0.3)
        elif kind == "SickRoom":
            part.box((0.03, 0.012, 0.36), (0.5, y, 1.97), "herb")
            for k, (dx, dz, turn) in enumerate(((-0.08, 0.1, 0.7), (0.08, 0.06, -0.7), (-0.08, -0.02, 0.7), (0.08, -0.06, -0.7), (0.0, 0.17, 0.0))):
                part.box((0.06, 0.012, 0.13), (0.5 + dx, y, 1.99 + dz), "herb", rot=(0, turn, 0), taper=0.4)
            part.box((0.26, 0.012, 0.06), (0.5, y, 1.78), "bandage")
        else:
            for k in range(5):
                part.box((0.03, 0.012, 0.34), (0.5 + (k - 2) * 0.05, y, 1.96), "wheat", rot=(0, (k - 2) * 0.16, 0))
                part.box((0.05, 0.012, 0.1), (0.5 + (k - 2) * 0.085, y, 2.14), "wheat", rot=(0, (k - 2) * 0.16, 0), taper=0.4)
            part.box((0.2, 0.012, 0.04), (0.5, y, 1.9), "rope")
    return [part]


MODELS = (("RoomSickRoom", sick_room, ROOM_TRIANGLE_BUDGET), ("RoomSmithy", smithy, ROOM_TRIANGLE_BUDGET), ("RoomBarn", barn, ROOM_TRIANGLE_BUDGET),
          ("WorkField", field, 3400), ("SignSickRoom", lambda: sign("SickRoom"), 600), ("SignSmithy", lambda: sign("Smithy"), 600),
          ("SignBarn", lambda: sign("Barn"), 600))


def merge(parts):
    """Parts of the same name (several builders add to 'Furniture') become one object."""
    by_name = {}
    order = []
    for part in parts:
        obj, triangles = part.finish()
        if part.name in by_name:
            first, count = by_name[part.name]
            bpy.ops.object.select_all(action="DESELECT")
            obj.select_set(True)
            first.select_set(True)
            bpy.context.view_layer.objects.active = first
            bpy.ops.object.join()
            by_name[part.name] = (first, count + triangles)
        else:
            obj.name = part.name
            by_name[part.name] = (obj, triangles)
            order.append(part.name)
    return [by_name[name] for name in order]


def see_through_from_behind():
    """In the game a wall is seen from inside only. The preview material does the same, so the pictures show the room."""
    mat = base.material()
    tree = mat.node_tree
    if tree.nodes.get("Behind"):
        return
    shader = tree.nodes["Principled BSDF"]
    out = next(node for node in tree.nodes if node.type == "OUTPUT_MATERIAL")
    geometry = tree.nodes.new("ShaderNodeNewGeometry")
    clear = tree.nodes.new("ShaderNodeBsdfTransparent")
    mix = tree.nodes.new("ShaderNodeMixShader")
    mix.name = "Behind"
    tree.links.new(geometry.outputs["Backfacing"], mix.inputs["Fac"])
    tree.links.new(shader.outputs["BSDF"], mix.inputs[1])
    tree.links.new(clear.outputs["BSDF"], mix.inputs[2])
    tree.links.new(mix.outputs["Shader"], out.inputs["Surface"])


def preview(name, objects, path, room):
    """One picture per model: a room from over its south wall with its own warm light, the others in daylight."""
    scene = bpy.context.scene
    for obj in scene.objects:
        obj.hide_render = obj not in objects
    extra = []
    world = bpy.data.worlds.get("Sky") or bpy.data.worlds.new("Sky")
    world.use_nodes = True
    background = world.node_tree.nodes["Background"]
    if room:
        background.inputs["Color"].default_value = (0.02, 0.02, 0.025, 1.0)
        background.inputs["Strength"].default_value = 1.0
        for obj in objects:
            if ".Lamp_" in obj.name or obj.name.endswith(".Fire"):
                light = bpy.data.objects.new("PreviewLight", bpy.data.lights.new("PreviewLight", "POINT"))
                centre = sum((Vector(corner) for corner in obj.bound_box), Vector()) / 8
                light.location = (centre.x * 0.9, centre.y * 0.9, centre.z + 0.15)
                light.data.energy = 260 if obj.name == "Fire" else 170
                light.data.color = (1.0, 0.62, 0.34)
                light.data.shadow_soft_size = 0.25
                scene.collection.objects.link(light)
                extra.append(light)
        fill = bpy.data.objects.new("PreviewFill", bpy.data.lights.new("PreviewFill", "POINT"))
        fill.location = (0, -1.0, 4.5)
        fill.data.energy = 220
        fill.data.color = (0.72, 0.78, 1.0)
        scene.collection.objects.link(fill)
        extra.append(fill)
        location, tilt, lens = (0.0, -10.2, 8.2), 52, 30
    else:
        background.inputs["Color"].default_value = (0.36, 0.40, 0.46, 1.0)
        background.inputs["Strength"].default_value = 0.9
        sun = bpy.data.objects.new("PreviewSun", bpy.data.lights.new("PreviewSun", "SUN"))
        sun.data.energy = 3.0
        sun.rotation_euler = (math.radians(52), 0.0, math.radians(35))
        scene.collection.objects.link(sun)
        bpy.ops.mesh.primitive_plane_add(size=40, location=(0, 0, -0.045))
        ground = bpy.context.active_object
        ash = bpy.data.materials.get("PreviewAsh") or bpy.data.materials.new("PreviewAsh")
        ash.diffuse_color = (0.42, 0.42, 0.44, 1.0)
        ground.data.materials.append(ash)
        extra.extend((sun, ground))
        if name.startswith("Sign"):
            location, tilt, lens = (0.5, -4.2, 2.2), 87, 50
        else:
            location, tilt, lens = (-0.6, -6.6, 5.4), 56, 32
    scene.world = world
    camera = bpy.data.objects.new("PreviewCamera", bpy.data.cameras.new("PreviewCamera"))
    camera.location = location
    camera.rotation_euler = (math.radians(tilt), 0.0, 0.0)
    camera.data.lens = lens
    scene.collection.objects.link(camera)
    scene.camera = camera
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 32
    scene.cycles.use_denoising = False
    scene.render.resolution_x, scene.render.resolution_y = 1120, 700
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    for obj in extra + [camera]:
        bpy.data.objects.remove(obj, do_unlink=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--output" not in argv:
        raise SystemExit("usage: blender --background --python village_place_rooms.py -- --output <folder> [--preview]")
    folder = os.path.abspath(argv[argv.index("--output") + 1])
    os.makedirs(folder, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    base.write_pictures(folder)
    built = []
    for name, build, budget in MODELS:
        merged = merge(build())
        objects = [obj for obj, _ in merged]
        triangles = sum(count for _, count in merged)
        base.export(name, objects, folder)
        stock = {}
        for obj in objects:
            if obj.name.startswith("Stock_"):
                good = obj.name.split("_")[1]
                stock[good] = stock.get(good, 0) + 1
        others = sorted(obj.name for obj in objects if not obj.name.startswith("Stock_"))
        print("MODEL %s parts=%d triangles=%d stock=%s other=%s" % (name, len(objects), triangles,
              ",".join("%s:%d" % item for item in sorted(stock.items())) or "-", ",".join(others)))
        if triangles > budget:
            raise SystemExit("%s is over its triangle budget of %d" % (name, budget))
        for obj in objects:
            obj.name = name + "." + obj.name
        built.append((name, objects))
    if "--preview" in argv:
        see_through_from_behind()
        for name, objects in built:
            preview(name, objects, os.path.join(folder, "preview-%s.png" % name), name.startswith("Room"))


if __name__ == "__main__":
    main()
