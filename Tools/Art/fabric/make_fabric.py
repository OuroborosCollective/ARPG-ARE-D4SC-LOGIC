#!/usr/bin/env python3
"""Fabric height maps for the story figures (owner 2026-11-23: "grob mächtiges Leinen").

Writes tileable grey height maps (white = raised thread, black = gap between threads) that the BoZo figure baker lays
over chosen outfit parts, as weave in the colours and as relief in the normal map. Deterministic: same seed, same file.
usage: python3 -I make_fabric.py <output-folder>
"""
import sys
import numpy as np
from PIL import Image


def smooth_noise(n, scale, seed):
    """Tileable value noise: a coarse random grid, smoothly interpolated and wrapped at the edges."""
    grid = np.random.default_rng(seed).random((scale, scale))
    t = (np.arange(n) / n) * scale
    i0 = np.floor(t).astype(int) % scale
    i1 = (i0 + 1) % scale
    f = t - np.floor(t)
    f = f * f * (3 - 2 * f)
    fy, fx = f[:, None], f[None, :]
    a, b = grid[np.ix_(i0, i0)], grid[np.ix_(i0, i1)]
    c, d = grid[np.ix_(i1, i0)], grid[np.ix_(i1, i1)]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def linen(n=512, threads=40, seed=20261123):
    """Coarse heavy linen: plain weave of thick threads that vary in width, with slubs, fibre noise and grime."""
    rng = np.random.default_rng(seed)
    cell = n / threads
    y, x = np.mgrid[0:n, 0:n].astype(np.float64)
    col, row = x / cell, y / cell
    ci, ri = np.floor(col).astype(int) % threads, np.floor(row).astype(int) % threads
    fx, fy = col - np.floor(col) - 0.5, row - np.floor(row) - 0.5
    warp = (0.60 + 0.30 * rng.random(threads))[ci] * (0.82 + 0.40 * smooth_noise(n, 20, seed + 1))
    weft = (0.60 + 0.30 * rng.random(threads))[ri] * (0.82 + 0.40 * smooth_noise(n, 20, seed + 2).T)
    across_warp = np.clip(1 - (np.abs(fx) / (0.5 * warp)) ** 2, 0, 1)
    across_weft = np.clip(1 - (np.abs(fy) / (0.5 * weft)) ** 2, 0, 1)
    over = (ci + ri) % 2 == 0
    top = np.where(over, across_warp * (0.72 + 0.28 * np.cos(2 * np.pi * fy)), across_weft * (0.72 + 0.28 * np.cos(2 * np.pi * fx)))
    under = np.where(over, across_weft, across_warp) * 0.42
    height = np.maximum(top, under)
    height *= 0.86 + 0.16 * smooth_noise(n, 128, seed + 3) + 0.10 * smooth_noise(n, 64, seed + 4)
    grime = smooth_noise(n, 6, seed + 5)
    height -= 0.10 * np.clip((grime - 0.62) / 0.2, 0, 1)
    return np.clip(height, 0, 1)


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    h = linen()
    Image.fromarray((h * 255 + 0.5).astype(np.uint8), "L").save(out.rstrip("/") + "/AC_LinenCoarse.png", optimize=True)


if __name__ == "__main__":
    main()
