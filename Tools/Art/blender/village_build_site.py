"""The lot of a village house of Ashen Crypt before the house stands, built in Blender without a window.

The three houses that are built with the player's help (sick room, smithy, field barn) stand on footprints of the
same size, so one model serves all three. It is centred on the footprint; the side the door will be on is +Y.

  Base               trampled ground, stakes with a rope around three sides, foundation stones, a sawhorse
  Stock_Wood_<n>     one log of the pile (12): a lot shows as many as were delivered, at most what its house needs
  Stock_Metal_<n>    one lump of ash-iron in the crate (6)
  Stock_Herb_<n>     one bundle of ashen lilac on the rack (4)

Run:  blender --background --python Tools/Art/blender/village_build_site.py -- --output <private art folder> [--preview]
Writes BuildSite.fbx and the two palette pictures it shares with the workplaces; with --preview also
preview-BuildSite.png. Sizes are metres; +Z is up.
"""
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import village_work_props as base  # noqa: E402  (the palette, the part builder and the export)
import village_place_rooms as rooms  # noqa: E402  (the part with a ball, the merge and the preview)

HALF = 3.2
TRIANGLE_BUDGET = 3400


def build_site():
    b = rooms.Part("Base", 311)
    inner = HALF - 0.2
    # Trampled ground with a few darker patches where things were dragged.
    b.box((2 * inner, 2 * inner, 0.05), (0, 0, 0.025), "soil")
    for x, y, w, d in ((-1.2, -0.6, 2.2, 1.1), (1.4, 0.9, 1.6, 2.0), (0.2, -2.0, 2.8, 0.8)):
        b.box((w, d, 0.012), (x, y, 0.056), "char", rot=(0, 0, b.jitter(0.3)))
    # Stakes at the corners and between them; a rope runs round the west, south and east side. The door side is open.
    posts = [(sx * inner, sy * inner) for sx in (-1, 1) for sy in (-1, 1)] + [(-inner, 0), (inner, 0), (0, -inner)]
    for x, y in posts:
        b.cyl(0.055, 1.05, (x, y, 0.525), "wood_dark", sides=6, top_radius=0.04)
        b.box((0.11, 0.11, 0.05), (x, y, 0.93), "red")
    for sx in (-1, 1):
        b.box((0.025, 2 * inner, 0.025), (sx * inner, 0, 0.82), "rope")
    b.box((2 * inner, 0.025, 0.025), (0, -inner, 0.82), "rope")
    # The first foundation stones along the south and west edge.
    for i in range(7):
        b.box((0.62 + (i % 2) * 0.12, 0.42, 0.2 + (i % 3) * 0.04), (-2.5 + i * 0.82, -2.45, 0.1 + (i % 3) * 0.02), "stone" if i % 2 else "stone_dark",
              rot=(0, 0, b.jitter(0.08)))
    for i in range(4):
        b.box((0.42, 0.6 + (i % 2) * 0.1, 0.22), (-2.5, -1.6 + i * 0.8, 0.11), "stone_dark" if i % 2 else "stone", rot=(0, 0, b.jitter(0.08)))
    # A sawhorse with a squared beam, and a second beam on the ground.
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((0.07, 0.07, 0.85), (1.3 + sx * 0.55, -0.6 + sy * 0.22, 0.4), "wood", rot=(sy * 0.3, 0, 0))
        b.box((0.09, 0.6, 0.07), (1.3 + sx * 0.55, -0.6, 0.78), "wood_dark")
    b.box((2.4, 0.18, 0.18), (1.3, -0.6, 0.9), "wood_light", rot=(0, 0, 0.06))
    b.box((2.9, 0.2, 0.2), (0.4, -1.5, 0.15), "wood_light", rot=(0, 0, -0.18))
    # Bearers for the logs, a crate for the iron, a small rack for the herbs: all three near the open side.
    for x in (-2.35, -1.25):
        b.box((0.12, 1.5, 0.1), (x, 2.2, 0.1), "wood_dark")
    rooms.crate(b, 0.9, 2.3, 1.1, 0.8, 0.3)
    for sx in (-1, 1):
        b.box((0.06, 0.06, 1.25), (2.35 + sx * 0.42, 2.35, 0.625), "wood_dark")
    b.cyl(0.025, 0.95, (2.35, 2.35, 1.22), "wood", rot=(0, math.radians(90), 0), sides=6)
    parts = [b]
    # Twelve logs: five, four and three, lying across the bearers.
    layers = ((5, 0.24), (4, 0.42), (3, 0.6))
    index = 0
    for count, z in layers:
        for i in range(count):
            log = rooms.Part("Stock_Wood_%02d" % index, 320 + index)
            y = 2.2 + (i - (count - 1) / 2) * 0.21
            base.log(log, 0.1, 1.5, (-1.8, y, z), (0.0, math.radians(90), 0.0), sides=7)
            parts.append(log)
            index += 1
    for i in range(6):
        lump = rooms.Part("Stock_Metal_%02d" % i, 340 + i)
        x, y = 0.6 + (i % 3) * 0.3, 2.12 + (i // 3) * 0.34
        lump.ball(0.15, (x, y, 0.2 + (i // 3) * 0.03), "ore", squash=(1.1, 0.9, 0.75))
        lump.ball(0.06, (x + 0.04, y + 0.03, 0.3 + (i // 3) * 0.03), "iron", squash=(1, 1, 0.6))
        parts.append(lump)
    for i in range(4):
        herb = rooms.Part("Stock_Herb_%02d" % i, 350 + i)
        x = 2.05 + i * 0.2
        herb.cyl(0.012, 0.12, (x, 2.35, 1.14), "rope", sides=5)
        herb.cyl(0.03, 0.34, (x, 2.35, 0.91), "herb", sides=6, top_radius=0.1)
        herb.cyl(0.1, 0.07, (x, 2.35, 0.71), "herb", sides=6, top_radius=0.02)
        parts.append(herb)
    return parts


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--output" not in argv:
        raise SystemExit("usage: blender --background --python village_build_site.py -- --output <folder> [--preview]")
    folder = os.path.abspath(argv[argv.index("--output") + 1])
    os.makedirs(folder, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    base.write_pictures(folder)
    merged = rooms.merge(build_site())
    objects = [obj for obj, _ in merged]
    triangles = sum(count for _, count in merged)
    base.export("BuildSite", objects, folder)
    stock = {}
    for obj in objects:
        if obj.name.startswith("Stock_"):
            good = obj.name.split("_")[1]
            stock[good] = stock.get(good, 0) + 1
    print("MODEL BuildSite parts=%d triangles=%d stock=%s" % (len(objects), triangles, ",".join("%s:%d" % item for item in sorted(stock.items()))))
    if triangles > TRIANGLE_BUDGET:
        raise SystemExit("BuildSite is over its triangle budget of %d" % TRIANGLE_BUDGET)
    if "--preview" in argv:
        for obj in objects:
            obj.name = "BuildSite." + obj.name
        rooms.preview("BuildSite", objects, os.path.join(folder, "preview-BuildSite.png"), False)


if __name__ == "__main__":
    main()
