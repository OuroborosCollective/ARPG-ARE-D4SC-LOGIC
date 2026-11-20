#!/usr/bin/env python3
"""Textures for the ember mushrooms of Ashen Crypt ("Glutpilze"): caps of charred crust whose cracks glow.

Owner direction 2026-11-20: grey ash becomes a beautiful dark fantasy world when ember plants bloom in it. The
caps are painted like the ground they grow on: a dark crust, glowing cracks, ash that has settled on top.

Writes into the output folder:
  EmberCap_Albedo.png    atlas (sRGB): cap top, gills, stem
  EmberCap_Emission.png  the glow of the same parts (sRGB)
  EmberGlow.png          soft round glow for the light a cluster throws on the ash (white, falloff in alpha)

Layout in texture coordinates (v runs upwards), shared with the mesh builder in AshenWorldArt.cs:
  cap top   disc around (0.25, 0.75), radius 0.235; the apex is the centre, the rim the edge
  gills     disc around (0.75, 0.75), radius 0.235; the stem is the centre, the rim the edge
  stem      u 0.02..0.48 once around the stem, v 0.02..0.48 from the foot to the cap

Needs Python 3 with NumPy and Pillow, and generate_ashen_ground.py next to it. Example:
  python3 Tools/Art/generate_ember_flora.py --output <private art folder>
"""
import argparse
import os

import numpy as np
from PIL import Image

from generate_ashen_ground import gradient, noise, smoothstep, warp

CAP = (0.25, 0.75)
GILLS = (0.75, 0.75)
DISC = 0.235
STEM = (0.02, 0.48)


def crack_distance(size, seed, low, high, sway):
    """Distance in pixels to the cracks between the plates of the crust (zero lines of a bent noise field)."""
    field = warp(noise(size, seed, 1.0, low, high),
                 sway * noise(size, seed + 1, 1.2, 4.0, 14.0), sway * noise(size, seed + 2, 1.2, 4.0, 14.0))
    dx, dy = gradient(field)
    steep = np.sqrt(dx * dx + dy * dy)
    return np.abs(field) / np.maximum(steep, 0.35 * np.median(steep))


def mix(a, b, t):
    return a * (1 - t[..., None]) + b * t[..., None]


def build(size, seed):
    rows, cols = np.meshgrid(np.arange(size), np.arange(size), indexing="ij")
    u = (cols + 0.5) / size
    v = 1.0 - (rows + 0.5) / size
    albedo = np.zeros((size, size, 3))
    glow = np.zeros((size, size, 3))
    unit = size / 512.0
    fine = noise(size, seed + 9, 0.4, 60.0, None)

    # Cap top: plates of charred crust, cracks that glow hotter towards the rim, ash settled on the apex.
    x, y = (u - CAP[0]) / DISC, (v - CAP[1]) / DISC
    r = np.minimum(np.sqrt(x * x + y * y), 1.0)       # beyond the rim the edge colour continues (no bleeding)
    plates = crack_distance(size, seed + 10, 5.0, 12.0, 5.0 * unit)
    hairs = crack_distance(size, seed + 20, 14.0, 30.0, 2.5 * unit)
    width = 2.1 * unit
    crack = np.maximum(np.exp(-(plates / width) ** 2), 0.45 * np.exp(-(hairs / (width * 0.45)) ** 2))
    halo = np.maximum(np.exp(-(plates / (width * 3.2)) ** 2), 0.5 * np.exp(-(hairs / (width * 1.6)) ** 2))
    dust = smoothstep(0.50, 0.12, r + 0.16 * noise(size, seed + 30, 1.3, 5.0, 26.0))   # ash on the apex
    heat = (0.22 + 0.78 * smoothstep(0.15, 0.95, r)) * (1 - 0.92 * dust)
    rim = smoothstep(0.78, 1.0, r)
    crust = mix(np.array([0.165, 0.020, 0.017]), np.array([0.40, 0.034, 0.02]), smoothstep(0.35, 1.0, r))
    crust = crust * (0.8 + 0.2 * smoothstep(0.0, 9.0 * unit, plates))[..., None] * (1 + 0.16 * fine)[..., None]
    crust = mix(crust, np.array([0.045, 0.02, 0.018]), 0.8 * halo)
    crust = mix(crust, np.array([0.30, 0.05, 0.02]), crack * heat)
    ash = (0.40 + 0.05 * fine)[..., None] * np.array([0.97, 1.0, 1.05])
    top = mix(crust, ash, 0.88 * dust)
    pores = smoothstep(2.35, 2.9, noise(size, seed + 40, 0.3, 30.0, 80.0)) * smoothstep(0.3, 0.5, r) * (1 - dust)
    top_glow = (np.array([0.55, 0.016, 0.003]) * (0.5 * halo * heat)[..., None]
                + np.array([1.0, 0.085, 0.007]) * (crack * heat)[..., None]
                + np.array([0.85, 0.045, 0.005]) * (0.55 * rim * rim)[..., None]
                + np.array([1.0, 0.16, 0.014]) * (0.45 * smoothstep(0.93, 1.0, r) + pores)[..., None])
    part = (u < 0.5) & (v >= 0.5)
    albedo[part], glow[part] = top[part], top_glow[part]

    # Gills: thin lamellae from the stem to the rim, glowing between them, strongest under the rim.
    x, y = (u - GILLS[0]) / DISC, (v - GILLS[1]) / DISC
    r = np.minimum(np.sqrt(x * x + y * y), 1.0)
    angle = np.arctan2(y, x)
    lamella = 0.5 + 0.5 * np.cos(46.0 * angle + 0.9 * np.sin(8.0 * r))
    between = lamella ** 3
    under = mix(np.array([0.09, 0.028, 0.02]), np.array([0.46, 0.13, 0.045]), between) * smoothstep(0.08, 0.3, r)[..., None]
    under_glow = np.array([1.0, 0.10, 0.008]) * ((0.22 + 0.78 * between) * (0.3 + 0.7 * smoothstep(0.15, 0.95, r)))[..., None]
    part = (u >= 0.5) & (v >= 0.5)
    albedo[part], glow[part] = under[part], under_glow[part]

    # Stem: pale as bone ash at the foot, fibres running up, warming and glowing towards the cap. It closes
    # around the stem without a seam: the fibres are periodic across the strip.
    around = np.clip((u - STEM[0]) / (STEM[1] - STEM[0]), 0.0, 1.0)
    up = np.clip((v - STEM[0]) / (STEM[1] - STEM[0]), 0.0, 1.0)
    strip = noise(256, seed + 50, 0.8, 10.0, 60.0, squeeze=5.0)       # fibres from the foot to the cap
    fibres = strip[np.clip((up * 255).astype(int), 0, 255), (around * 256).astype(int) % 256]
    bone = (0.47 + 0.07 * fibres)[..., None] * np.array([1.0, 0.94, 0.86])
    bone = bone * (0.55 + 0.45 * smoothstep(0.0, 0.3, up))[..., None]                      # soot at the foot
    stem = mix(bone, np.array([0.42, 0.12, 0.06]), 0.75 * smoothstep(0.55, 1.0, up))
    stem_glow = np.array([0.95, 0.07, 0.007]) * (smoothstep(0.4, 1.0, up) ** 2 * (0.55 + 0.45 * smoothstep(-0.5, 1.5, fibres)))[..., None]
    part = (u < 0.5) & (v < 0.5)
    albedo[part], glow[part] = stem[part], stem_glow[part]

    albedo[(u >= 0.5) & (v < 0.5)] = np.array([0.05, 0.03, 0.03])     # unused quarter
    return np.clip(albedo, 0, 1), np.clip(glow, 0, 1)


def pool(size):
    """Round falloff for the light on the ground: bright in the middle, gone well before the edge."""
    rows, cols = np.meshgrid(np.arange(size), np.arange(size), indexing="ij")
    r = np.sqrt(((cols + 0.5) / size - 0.5) ** 2 + ((rows + 0.5) / size - 0.5) ** 2) * 2.0
    return np.clip(1.0 - r, 0.0, 1.0) ** 2.2


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--output", required=True)
    parser.add_argument("--size", type=int, default=512, choices=[256, 512, 1024])
    parser.add_argument("--seed", type=int, default=771)
    args = parser.parse_args()
    albedo, glow = build(args.size, args.seed)
    os.makedirs(args.output, exist_ok=True)
    to_srgb = lambda linear: np.where(linear <= 0.0031308, linear * 12.92, 1.055 * np.power(np.maximum(linear, 1e-9), 1 / 2.4) - 0.055)
    for name, pixels in (("EmberCap_Albedo", albedo), ("EmberCap_Emission", glow)):
        Image.fromarray((to_srgb(pixels) * 255 + 0.5).astype(np.uint8), "RGB").save(os.path.join(args.output, name + ".png"), optimize=True)
        print("%s.png %dx%d mean %s" % (name, args.size, args.size, np.round(pixels.reshape(-1, 3).mean(axis=0), 3).tolist()))
    falloff = pool(128)
    rgba = np.concatenate([np.full((128, 128, 3), 255, dtype=np.uint8), (falloff * 255 + 0.5).astype(np.uint8)[..., None]], axis=-1)
    Image.fromarray(rgba, "RGBA").save(os.path.join(args.output, "EmberGlow.png"), optimize=True)
    print("EmberGlow.png 128x128 edge alpha %d centre alpha %d" % (rgba[0, 64, 3], rgba[64, 64, 3]))


if __name__ == "__main__":
    main()
