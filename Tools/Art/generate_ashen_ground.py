#!/usr/bin/env python3
"""Ashen ground textures for the overworld of Ashen Crypt: ash snow with glowing ember veins.

Owner direction 2026-11-20: not a world drowned in fog and ash haze, but ash snow balanced by red glowing threads
in the ash and fiery plants. This tool paints that ground. It writes four tileable maps:

  <name>_Albedo.png    pale ash snow in drifts; scorched earth where the embers melted the ash away; charred crust
                       along the veins (sRGB)
  <name>_Normal.png    tangent-space normals: drifts, wind ripples, the rim of the scorched islands, veins as
                       grooves (linear)
  <name>_Emission.png  the veins only: deep red halo, red body, orange seam where they burn hottest (sRGB)
  <name>_Detail.png    fine ash grain for close views. It multiplies the colour; mid grey changes nothing (linear)

All fields are built from periodic spectral noise, so every map repeats without a seam. The veins are the zero
lines of smooth noise fields: they meander and branch like cracks instead of running as straight strokes. They
gather in scorched islands, so calm ash snow remains between them. Sizes are given in metres and centimetres: the
same seed paints the same ground at any resolution.

Needs Python 3 with NumPy and Pillow. Example:
  python3 Tools/Art/generate_ashen_ground.py --output <private art folder> --name AshGround --size 2048 --metres 16
"""
import argparse
import os

import numpy as np
from PIL import Image


def noise(size, seed, beta, low=0.0, high=None, squeeze=1.0):
    """Periodic noise: random phases, amplitude 1/f^beta, kept between the wave numbers low and high.

    squeeze > 1 stretches the pattern from the top to the bottom of the picture, like ripples lying across the wind.
    """
    rng = np.random.default_rng(seed)
    fx = np.fft.fftfreq(size)[:, None] * size * squeeze
    fy = np.fft.fftfreq(size)[None, :] * size
    f = np.sqrt(fx * fx + fy * fy)
    f[0, 0] = 1.0
    amplitude = f ** -beta
    if low > 0:
        amplitude *= 1.0 - np.exp(-(f / low) ** 2)
    if high:
        amplitude *= np.exp(-(f / high) ** 2)
    amplitude[0, 0] = 0.0
    field = np.fft.ifft2(amplitude * np.exp(2j * np.pi * rng.random((size, size)))).real
    field -= field.mean()
    return field / (field.std() + 1e-9)


def gradient(field):
    """Central differences with wrap-around, so the edges continue into the next tile."""
    dx = (np.roll(field, -1, axis=1) - np.roll(field, 1, axis=1)) * 0.5
    dy = (np.roll(field, -1, axis=0) - np.roll(field, 1, axis=0)) * 0.5
    return dx, dy


def smoothstep(edge0, edge1, x):
    t = np.clip((x - edge0) / (edge1 - edge0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def warp(field, shift_x, shift_y):
    """Sample a periodic field at shifted places (bilinear, wrap-around): bends its lines without tearing them."""
    size = field.shape[0]
    rows, cols = np.meshgrid(np.arange(size, dtype=np.float64), np.arange(size, dtype=np.float64), indexing="ij")
    r = (rows + shift_y) % size
    c = (cols + shift_x) % size
    r0 = np.floor(r).astype(np.int64) % size
    c0 = np.floor(c).astype(np.int64) % size
    fr = r - np.floor(r)
    fc = c - np.floor(c)
    r1 = (r0 + 1) % size
    c1 = (c0 + 1) % size
    return (field[r0, c0] * (1 - fr) * (1 - fc) + field[r0, c1] * (1 - fr) * fc
            + field[r1, c0] * fr * (1 - fc) + field[r1, c1] * fr * fc)


def vein_distance(size, seed, low, high, k, ppm):
    """Distance in pixels to the nearest zero line of a smooth noise field (first-order estimate).

    The zero lines of a smooth field are long and meander. Bending the field - a wide sway and a small tremble -
    makes them run like cracks without breaking them into loops, which adding fine noise would do.
    """
    field = noise(size, seed, 1.0, low, high)
    sway, tremble = 0.09 * ppm, 0.022 * ppm      # 9 cm and about 2 cm
    field = warp(field,
                 sway * noise(size, seed + 100, 1.2, 3.0 * k, 10.0 * k) + tremble * noise(size, seed + 102, 1.0, 12.0 * k, 35.0 * k),
                 sway * noise(size, seed + 101, 1.2, 3.0 * k, 10.0 * k) + tremble * noise(size, seed + 103, 1.0, 12.0 * k, 35.0 * k))
    dx, dy = gradient(field)
    steep = np.sqrt(dx * dx + dy * dy)
    return np.abs(field) / np.maximum(steep, 0.35 * np.median(steep))   # a floor keeps junctions from swelling


def build(size, seed, metres, vein_width_cm, vein_amount):
    ppm = size / metres    # pixels per metre
    k = metres / 8.0       # wave numbers below are written for an 8 m tile; k keeps their size in the world
    top = size * 0.45      # nothing finer than the picture can hold
    drifts = noise(size, seed + 1, 1.9, 0.0, 9.0 * k)                                 # wind-blown dunes, metres wide
    ripples = noise(size, seed + 2, 1.0, 30.0 * k, min(110.0 * k, top), squeeze=2.2)   # ripples across the wind
    grain = noise(size, seed + 3, 0.2, min(180.0 * k, top * 0.8), None)                # ash grain
    flecks = noise(size, seed + 4, 0.0, min(260.0 * k, top * 0.9), None)
    tone = noise(size, seed + 5, 2.2, 0.0, 4.0 * k)                                    # slow change of cool and warm ash

    # Scorched islands: where the embers gather, the ash snow has melted away and dark earth shows. Torn edges.
    patches = noise(size, seed + 13, 1.6, 2.0 * k, 6.5 * k)
    torn = patches + 0.22 * noise(size, seed + 17, 1.0, 16.0 * k, 60.0 * k)
    scorch = smoothstep(0.86 - vein_amount, 1.16 - vein_amount, torn)
    island = smoothstep(0.55 - vein_amount, 1.0 - vein_amount, patches)      # inside an island
    near = smoothstep(0.2 - vein_amount, 0.8 - vein_amount, patches)         # an island and its rim
    reach = smoothstep(-0.5 - vein_amount, 0.4 - vein_amount, patches)       # how far the long veins run into the ash

    # Three families of veins. Long ones, about a metre apart, leave the islands and run on through the ash snow
    # as thin glowing threads; branches and hairlines stay with the islands.
    main = vein_distance(size, seed + 11, 1.5 * k, 5.0 * k, k, ppm)
    side = vein_distance(size, seed + 12, 4.0 * k, 11.0 * k, k, ppm)
    hair = vein_distance(size, seed + 16, 8.0 * k, 22.0 * k, k, ppm)
    width = vein_width_cm / 100.0 * ppm * (0.7 + 0.6 * smoothstep(-1.2, 1.4, noise(size, seed + 14, 1.5, 0.0, 14.0 * k)))
    heat = 0.3 + 0.7 * smoothstep(-0.9, 0.9, noise(size, seed + 15, 1.4, 0.0, 22.0 * k))  # hotter and cooler stretches
    main_width = width * (0.55 + 0.45 * island)                              # thinner out in the snow
    main_mask = np.maximum(island, 0.85 * reach)
    core = np.maximum.reduce([np.exp(-(main / main_width) ** 2) * main_mask,
                              np.exp(-(side / (width * 0.6)) ** 2) * island,
                              0.8 * np.exp(-(hair / (width * 0.38)) ** 2) * near])
    halo = np.maximum.reduce([np.exp(-(main / (main_width * (2.2 + 1.2 * island))) ** 2) * main_mask,
                              np.exp(-(side / (width * 1.9)) ** 2) * island,
                              0.6 * np.exp(-(hair / (width * 1.2)) ** 2) * near])
    hot = np.exp(-(main / (width * 0.4)) ** 2) * island * heat
    heat = heat * (0.6 + 0.4 * island)                                       # veins cool down away from the islands

    # Albedo, as linear reflectance. Ash snow is a pale cool grey; scorched earth a warm charcoal.
    light = 0.43 + 0.05 * drifts + 0.008 * ripples + 0.014 * grain
    warm = smoothstep(-1.0, 1.0, tone)[..., None]
    snow = light[..., None] * (np.array([0.95, 0.99, 1.075]) * (1 - warm) + np.array([0.995, 0.995, 1.01]) * warm)   # cold ash, never sand
    snow *= (1.0 - 0.2 * smoothstep(2.0, 2.7, flecks))[..., None]                 # soot flecks
    snow += (0.06 * smoothstep(2.2, 3.0, -flecks))[..., None]                     # pale flakes
    earth = (0.06 + 0.012 * grain + 0.01 * ripples)[..., None] * np.array([1.1, 0.97, 0.9])
    earth += (0.028 * smoothstep(0.2, 1.9, noise(size, seed + 18, 1.2, 12.0 * k, 60.0 * k)))[..., None]             # ash dust
    ground = snow * (1 - scorch[..., None]) + earth * scorch[..., None]
    crust = np.array([0.043, 0.035, 0.033])
    ember = np.array([0.28, 0.045, 0.02])
    albedo = ground * (1 - 0.86 * halo[..., None]) + crust * (0.86 * halo[..., None])
    albedo = albedo * (1 - core[..., None]) + ember * core[..., None]

    # Emission, linear. Red needs very little green: the screen curve lifts every mid value towards yellow.
    emission = (np.array([0.50, 0.014, 0.003]) * (0.36 * halo * heat)[..., None]
                + np.array([1.0, 0.075, 0.007]) * (core * heat)[..., None]
                + np.array([0.9, 0.15, 0.012]) * hot[..., None])

    # Normal map from slopes. Broad fields are set to a slope that reads under a low sun; the sparse ones (island
    # rims, veins) keep their own steepness.
    def slope(field):
        dx, dy = gradient(field)
        rms = np.sqrt((dx * dx + dy * dy).mean()) + 1e-9
        return dx / rms, dy / rms
    gx = np.zeros((size, size))
    gy = np.zeros((size, size))
    for field, weight in ((drifts, 0.12), (ripples, 0.07), (grain, 0.035)):
        dx, dy = slope(field)
        gx += weight * dx * (1 - 0.5 * scorch)
        gy += weight * dy * (1 - 0.5 * scorch)
    for field, weight in ((-scorch, 9.0), (-halo, 4.5), (core, 1.2)):
        dx, dy = gradient(field)
        gx += weight * dx
        gy += weight * dy
    normal = np.stack([-gx, gy, np.ones_like(gx)], axis=-1)   # image rows run downwards, texture v runs upwards
    normal /= np.linalg.norm(normal, axis=-1, keepdims=True)
    return np.clip(albedo, 0, 1), normal * 0.5 + 0.5, np.clip(emission, 0, 1), core, scorch


def detail(size, seed):
    """Fine grain around mid grey (0.5 changes nothing): powdery ash with a few dark and pale specks."""
    speck = noise(size, seed + 33, 0.0, size * 0.3, None)
    field = (0.5 + 0.085 * noise(size, seed + 31, 0.3, size * 0.12, None) + 0.05 * noise(size, seed + 32, 1.2, 6.0, 40.0)
             - 0.16 * smoothstep(2.3, 3.0, speck) + 0.12 * smoothstep(2.4, 3.1, -speck))
    return np.clip(field - field.mean() + 0.5, 0.18, 0.82)


def seam(image):
    """Mean difference across the wrap-around edge compared with neighbouring columns inside the picture."""
    a = image.astype(np.float32)
    edge = np.abs(a[:, 0] - a[:, -1]).mean() + np.abs(a[0] - a[-1]).mean()
    inside = np.abs(a[:, 1] - a[:, 0]).mean() + np.abs(a[1] - a[0]).mean()
    return edge / (inside + 1e-6)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--output", required=True)
    parser.add_argument("--name", default="AshGround")
    parser.add_argument("--size", type=int, default=2048, choices=[512, 1024, 2048, 4096])
    parser.add_argument("--metres", type=float, default=16.0, help="edge of the ground one repeat covers")
    parser.add_argument("--seed", type=int, default=462)
    parser.add_argument("--vein-width", type=float, default=2.4, help="half width of a vein in centimetres")
    parser.add_argument("--vein-amount", type=float, default=0.0, help="-0.3 few veins ... 0.3 many veins")
    parser.add_argument("--detail-size", type=int, default=512, choices=[256, 512, 1024])
    args = parser.parse_args()
    if not -0.3 <= args.vein_amount <= 0.3 or not 1.0 <= args.vein_width <= 8.0 or not 4.0 <= args.metres <= 32.0:
        parser.error("vein-amount must be -0.3..0.3, vein-width 1..8 cm and metres 4..32")

    albedo, normal, emission, core, scorch = build(args.size, args.seed, args.metres, args.vein_width, args.vein_amount)
    os.makedirs(args.output, exist_ok=True)
    to_srgb = lambda linear: np.where(linear <= 0.0031308, linear * 12.92, 1.055 * np.power(np.maximum(linear, 1e-9), 1 / 2.4) - 0.055)
    grain = detail(args.detail_size, args.seed)
    pictures = {
        "Albedo": (to_srgb(albedo) * 255 + 0.5).astype(np.uint8),
        "Normal": (normal * 255 + 0.5).astype(np.uint8),
        "Emission": (to_srgb(emission) * 255 + 0.5).astype(np.uint8),
        "Detail": np.repeat((grain * 255 + 0.5).astype(np.uint8)[..., None], 3, axis=-1),
    }
    for kind, pixels in pictures.items():
        picture = Image.fromarray(pixels, "RGB")
        picture.save(os.path.join(args.output, "%s_%s.png" % (args.name, kind)), optimize=True)
        ratio = seam(pixels)
        if ratio > 1.6:
            raise SystemExit("%s does not tile: edge/inside difference %.2f" % (kind, ratio))
        print("%s_%s.png %dx%d seam %.2f" % (args.name, kind, picture.width, picture.height, ratio))
    calm = pictures["Albedo"][scorch < 0.02]
    print("scorched islands %.1f %%, veins %.1f %% of the ground; mean ash snow %s; detail mean %.3f" % (
        100.0 * float((scorch > 0.5).mean()), 100.0 * float((core > 0.2).mean()),
        np.round(calm.mean(axis=0)).astype(int).tolist(), float(grain.mean())))


if __name__ == "__main__":
    main()
