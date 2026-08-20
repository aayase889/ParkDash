"""Build Blue-Car-compatible red and green paint atlases.

The Blue and Yellow source atlases share one UV layout. Their per-pixel
difference is therefore a reliable paint mask: areas that do not change are
glass, tires, trim, lights, or empty UV space and must remain untouched.

Red starts from the Blue atlas. Green starts from the Yellow atlas. Only the
paint mask is hue-remapped, so both variants retain the source artwork's
shading and all neutral details.
"""

from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
from PIL import Image


def fixed_hue_rgb(value: np.ndarray, saturation: np.ndarray, hue: float) -> np.ndarray:
    chroma = value * saturation
    hue_sector = hue * 6.0
    secondary = chroma * (1.0 - abs(hue_sector % 2.0 - 1.0))
    offset = value - chroma

    sector = int(hue_sector) % 6
    if sector == 0:
        channels = (chroma, secondary, np.zeros_like(chroma))
    elif sector == 1:
        channels = (secondary, chroma, np.zeros_like(chroma))
    elif sector == 2:
        channels = (np.zeros_like(chroma), chroma, secondary)
    elif sector == 3:
        channels = (np.zeros_like(chroma), secondary, chroma)
    elif sector == 4:
        channels = (secondary, np.zeros_like(chroma), chroma)
    else:
        channels = (chroma, np.zeros_like(chroma), secondary)

    return np.stack(channels, axis=2) + offset[..., None]


def recolor_paint(source: np.ndarray, comparison: np.ndarray, hue: float) -> np.ndarray:
    source_rgb = source[..., :3]
    comparison_rgb = comparison[..., :3]
    difference = np.max(np.abs(source_rgb - comparison_rgb), axis=2)

    value = np.max(source_rgb, axis=2)
    minimum = np.min(source_rgb, axis=2)
    saturation = np.divide(
        value - minimum,
        value,
        out=np.zeros_like(value),
        where=value > 1.0e-6,
    )
    recolored = fixed_hue_rgb(value, saturation, hue)

    # Feather only the atlas edge pixels. The interiors differ strongly while
    # identical neutral art has a zero difference and remains byte-for-byte
    # sourced from its requested atlas.
    paint_weight = np.clip((difference - 0.015) / 0.085, 0.0, 1.0)
    result = source.copy()
    result[..., :3] = (
        source_rgb * (1.0 - paint_weight[..., None])
        + recolored * paint_weight[..., None]
    )
    return np.clip(result, 0.0, 1.0)


def load_rgba(path: Path) -> np.ndarray:
    return np.asarray(Image.open(path).convert("RGBA"), dtype=np.float32) / 255.0


def save_rgba(path: Path, pixels: np.ndarray) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    encoded = np.rint(pixels * 255.0).astype(np.uint8)
    Image.fromarray(encoded, mode="RGBA").save(path, optimize=True)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--blue", type=Path, required=True)
    parser.add_argument("--yellow", type=Path, required=True)
    parser.add_argument("--red-output", type=Path, required=True)
    parser.add_argument("--green-output", type=Path, required=True)
    args = parser.parse_args()

    blue = load_rgba(args.blue)
    yellow = load_rgba(args.yellow)
    if blue.shape != yellow.shape:
        raise RuntimeError(f"Atlas sizes differ: blue={blue.shape}, yellow={yellow.shape}")

    # Slightly warm red avoids a magenta cast; the green sits near a clean,
    # saturated toy-car green while retaining the Yellow atlas's value range.
    save_rgba(args.red_output, recolor_paint(blue, yellow, hue=0.995))
    save_rgba(args.green_output, recolor_paint(yellow, blue, hue=0.340))
    print(f"WROTE {args.red_output}")
    print(f"WROTE {args.green_output}")


if __name__ == "__main__":
    main()
