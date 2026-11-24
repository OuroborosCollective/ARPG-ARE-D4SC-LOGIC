"""Derive the village's dark, worn textures from private source textures.

usage: python3 -I retexture.py <recipe.json> <sources.json> <output folder> [id ...]

The recipe (in the repository) says how each surface looks: size, grade, what happens to which hues, whether
a normal map is made from the brightness. The sources file (private, never in the repository) says where the
licensed or generated source picture of each surface lies. Outputs are private as well.

Grade of a surface, applied in this order:
  hues        list of {"from", "to" (degrees), "saturation", "gain", "tint", "toward" [r,g,b], "amount"}: only pixels
              whose hue lies in the range (soft edges of 12 degrees, weighted by how colourful the pixel is)
  saturation  1 keeps the colour, 0 is grey
  gain        brightness factor
  contrast    spread around the picture's own mean brightness
  tint        [r, g, b] factors
  ash         0..1: ash settles on the bright, flat parts (for surfaces that only face upwards)
  normalFromLuma  strength of a normal map derived from the brightness (tileable)
A surface may list "regions": {"rect": [x, y, width, height] as fractions from the top left, plus a grade of its own};
inside its rectangle a region replaces the surface's grade (the tiles of an atlas: roof, plaster, timber, stone).
"""
import json
import pathlib
import sys

import numpy as np
from PIL import Image

Image.MAX_IMAGE_PIXELS = None
ASH = np.array([0.56, 0.585, 0.61], dtype=np.float32)


def luma(rgb):
    return rgb[..., 0] * 0.2126 + rgb[..., 1] * 0.7152 + rgb[..., 2] * 0.0722


def hue_and_chroma(rgb):
    high, low = rgb.max(axis=-1), rgb.min(axis=-1)
    chroma = high - low
    safe = np.where(chroma > 1e-5, chroma, 1.0)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    hue = np.where(high == r, ((g - b) / safe) % 6.0, np.where(high == g, (b - r) / safe + 2.0, (r - g) / safe + 4.0)) * 60.0
    return np.where(chroma > 1e-5, hue, 0.0), chroma / np.maximum(high, 1e-5)


def in_range(hue, start, end, soft=12.0):
    """1 inside [start, end] on the colour wheel, falling to 0 over `soft` degrees outside it."""
    span = (end - start) % 360.0
    along = (hue - start) % 360.0
    outside = np.where(along <= span, 0.0, np.minimum(along - span, 360.0 - along))
    return np.clip(1.0 - outside / soft, 0.0, 1.0)


def saturate(rgb, amount):
    grey = luma(rgb)[..., None]
    return grey + (rgb - grey) * amount


def grade(rgb, spec):
    rgb = rgb.copy()
    for rule in spec.get("hues", []):
        hue, colourful = hue_and_chroma(rgb)
        weight = (in_range(hue, rule["from"], rule["to"]) * np.clip(colourful * 4.0, 0.0, 1.0))[..., None]
        changed = saturate(rgb, rule.get("saturation", 1.0)) * rule.get("gain", 1.0) * np.array(rule.get("tint", [1, 1, 1]), dtype=np.float32)
        if "toward" in rule:
            target = luma(changed)[..., None] / max(float(luma(np.array(rule["toward"], dtype=np.float32))), 1e-3) * np.array(rule["toward"], dtype=np.float32)
            changed = changed + (target - changed) * rule.get("amount", 1.0)
        rgb = rgb + (changed - rgb) * weight
    rgb = saturate(rgb, spec.get("saturation", 1.0)) * spec.get("gain", 1.0)
    if "contrast" in spec:
        mean = float(luma(rgb).mean())
        rgb = mean + (rgb - mean) * spec["contrast"]
    rgb = rgb * np.array(spec.get("tint", [1, 1, 1]), dtype=np.float32)
    if spec.get("ash", 0) > 0:
        bright = luma(rgb)
        low, high = np.percentile(bright, 35), np.percentile(bright, 92)
        settle = np.clip((bright - low) / max(high - low, 1e-4), 0.0, 1.0)[..., None] * spec["ash"]
        rgb = rgb + (ASH * (0.75 + 0.5 * settle) - rgb) * settle
    return np.clip(rgb, 0.0, 1.0)


def normal_from_luma(rgb, strength):
    height = luma(rgb)
    for _ in range(2):  # a small tileable blur keeps single-pixel noise out of the slopes
        height = (height + np.roll(height, 1, 0) + np.roll(height, -1, 0) + np.roll(height, 1, 1) + np.roll(height, -1, 1)) / 5.0
    dx = (np.roll(height, -1, 1) - np.roll(height, 1, 1)) * strength * height.shape[1] / 256.0
    dy = (np.roll(height, 1, 0) - np.roll(height, -1, 0)) * strength * height.shape[0] / 256.0
    normal = np.stack([-dx, -dy, np.ones_like(height)], axis=-1)
    normal /= np.linalg.norm(normal, axis=-1, keepdims=True)
    return normal * 0.5 + 0.5


def to_linear(srgb):
    return np.where(srgb <= 0.04045, srgb / 12.92, ((srgb + 0.055) / 1.055) ** 2.4)


def to_srgb(linear):
    return np.where(linear <= 0.0031308, linear * 12.92, 1.055 * np.maximum(linear, 0.0) ** (1 / 2.4) - 0.055)


def main():
    recipe = json.loads(pathlib.Path(sys.argv[1]).read_text(encoding="utf-8"))
    sources = json.loads(pathlib.Path(sys.argv[2]).read_text(encoding="utf-8"))
    out = pathlib.Path(sys.argv[3])
    wanted = set(sys.argv[4:])
    out.mkdir(parents=True, exist_ok=True)
    for spec in recipe["surfaces"]:
        if wanted and spec["id"] not in wanted:
            continue
        if spec["source"] not in sources:
            sys.exit("no private source for " + spec["source"])
        image = Image.open(sources[spec["source"]])
        alpha = image.getchannel("A") if image.mode in ("RGBA", "LA") else None
        image = image.convert("RGB")
        size = spec.get("size", 1024)
        if max(image.size) > size:
            scale = size / max(image.size)
            target = (max(1, round(image.width * scale)), max(1, round(image.height * scale)))
            image = image.resize(target, Image.LANCZOS)
            alpha = alpha.resize(target, Image.LANCZOS) if alpha is not None else None
        source = to_linear(np.asarray(image, dtype=np.float32) / 255.0)
        rgb = grade(source, spec)
        for region in spec.get("regions", []):  # parts of an atlas with a look of their own, cut from the source picture
            x, y, w, h = region["rect"]
            rows = slice(round(y * source.shape[0]), round((y + h) * source.shape[0]))
            columns = slice(round(x * source.shape[1]), round((x + w) * source.shape[1]))
            rgb[rows, columns] = grade(source[rows, columns], region)
        result = Image.fromarray((to_srgb(rgb) * 255.0 + 0.5).astype(np.uint8), "RGB")
        if alpha is not None and spec.get("keepAlpha", True):
            result.putalpha(alpha)
        result.save(out / (spec["id"] + ".png"))
        if spec.get("normalFromLuma", 0) > 0:
            normal = normal_from_luma(rgb, spec["normalFromLuma"])
            Image.fromarray((normal * 255.0 + 0.5).astype(np.uint8), "RGB").save(out / (spec["id"] + "_normal.png"))
        print(spec["id"], result.size, "mean", round(float(luma(rgb).mean()), 3))


if __name__ == "__main__":
    main()
