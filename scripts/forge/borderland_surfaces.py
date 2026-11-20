"""Bake the original seamless ash/ember mask and the matching Blender trim-atlas emission.
Usage: python borderland_surfaces.py Assets/AshenCrypt/BorderlandArt (NumPy, Pillow).
R = basalt grain, G = windborne ash, B = narrow hot fissures. All noise is offline.
"""
import argparse
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter


def smooth_field(side, cells, rng):
    lattice = rng.uniform(-1, 1, (cells, cells))
    axis = np.arange(side) * cells / side
    i = axis.astype(int)
    t = axis - i
    t = t * t * (3 - 2 * t)
    x, y = np.meshgrid(i, i)
    u, v = np.meshgrid(t, t)
    dx, dy = (x + 1) % cells, (y + 1) % cells
    return ((1 - v) * ((1 - u) * lattice[y, x] + u * lattice[y, dx]) +
            v * ((1 - u) * lattice[dy, x] + u * lattice[dy, dx]))


def paint_soil(folder):
    rng = np.random.default_rng(842011)
    side = 1024
    field = lambda n: smooth_field(side, n, rng)
    large, mid, rough, flecks = field(3), field(12), field(45), field(88)
    y, x = np.indices((side, side))
    drifts = np.sin((x * 2 + y * 3) * math.tau / side + mid * 1.4 + large * 2)
    powder = .52 + .37 * large + .24 * mid + .09 * rough + .07 * drifts + .04 * flecks
    # Make the project's original slate artwork periodic without blurring strata or grain.
    # Remove the smooth component induced by the opposite-edge discontinuities.
    original = Path(__file__).parent / "textures" / "ash-slate-source.jpg"
    tile = np.asarray(Image.open(original).convert("L"), dtype=float) / 255.0
    assert tile.shape == (side, side)
    edge = np.zeros_like(tile)
    edge[0] = tile[-1] - tile[0]
    edge[-1] = -edge[0]
    edge[:,0] += tile[:,-1] - tile[:,0]
    edge[:,-1] -= tile[:,-1] - tile[:,0]
    k = 2 * np.cos(math.tau*np.arange(side)/side)
    laplace = k[None,:] + k[:,None] - 4
    laplace[0,0] = 1
    smooth = np.fft.fft2(edge) / laplace
    smooth[0,0] = 0
    periodic = tile - np.fft.ifft2(smooth).real
    low,high = np.percentile(periodic, (2,98))
    grain = .10 + .80*np.clip((periodic-low)/(high-low),0,1)
    # Sparse branching networks wrap across tile edges; lengths and angles are continuous.
    fissures = Image.new("L", (side, side))
    draw = ImageDraw.Draw(fissures)

    def crack(px, py, angle, count, width, branch=False):
        for step in range(count):
            length = rng.uniform(3, 9)
            angle += rng.uniform(-.59, .59)
            nx, ny = px + length * math.cos(angle), py + length * math.sin(angle)
            for ox in (-side, 0, side):
                for oy in (-side, 0, side):
                    draw.line((round(px + ox), round(py + oy), round(nx + ox), round(ny + oy)),
                              width=width, fill=int(rng.uniform(155, 255)))
            if not branch and step in (5, 13, 23):
                crack(nx, ny, angle + rng.choice((-1, 1)) * .89, int(rng.integers(4, 12)), 1, True)
            px, py = nx, ny

    for _ in range(9):
        crack(rng.uniform(0, side), rng.uniform(0, side), rng.uniform(0, math.tau), int(rng.integers(14, 38)), 3)
    heat = np.asarray(fissures, dtype=float) / 255
    shoulder = np.asarray(fissures.filter(ImageFilter.GaussianBlur(2.1)), dtype=float) / 255
    heat = np.minimum(1, heat + .24 * shoulder)
    mask = np.stack((grain - .42 * shoulder, powder - .68 * shoulder, heat), axis=2)
    Image.fromarray(np.rint(np.clip(mask, 0, 1) * 255).astype('uint8')).save(folder / "AshSnowMask.png")


def paint_emission(folder):
    rgb = np.zeros((256, 256, 3), dtype='uint8')
    # PNG rows descend; UV tiles 12 (gills) and 14 (charcoal embers) are uppermost.
    y, x = np.indices((64, 64))
    folds = (.22 + .78 * np.maximum(0, np.cos(x * .61 + np.sin(y * .13))) ** 3)
    veins = np.clip(.5 + .4 * np.sin(x * .27 + np.cos(y * .32)) - .3 * np.cos(y * .29), 0, 1)
    for col, mask, color in ((0, folds, [235, 94, 46]), (2, veins, [243, 109, 41])):
        rgb[:64, col * 64:(col + 1) * 64] = (mask[..., None] * color).astype('uint8')
    Image.fromarray(rgb).save(folder / "BorderlandEmission.png")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument('output', type=Path)
    target = parser.parse_args().output
    target.mkdir(parents=True, exist_ok=True)
    paint_soil(target)
    paint_emission(target)
