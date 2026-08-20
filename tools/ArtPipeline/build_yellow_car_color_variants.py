"""Create color variants from the authored Yellow Car paint atlas.

The Yellow and Blue source atlases use the same UV layout. Their per-pixel
difference is therefore an exact paint mask: shared glass, tires, trim,
headlights, and empty UV space remain sourced byte-for-byte from the Yellow
atlas, while only the painted panels receive a new hue.
"""

from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
from PIL import Image


TARGET_HUES = {
    "red": 0.995,
    "green": 0.340,
    "blue": 0.575,
    "purple": 0.765,
}


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


def build_paint_weight(yellow: np.ndarray, blue: np.ndarray) -> np.ndarray:
    difference = np.max(np.abs(yellow[..., :3] - blue[..., :3]), axis=2)
    # Feather only antialiased UV-island edges. Identical neutral pixels stay
    # at zero, while painted panel interiors reach a full replacement weight.
    return np.clip((difference - 0.015) / 0.085, 0.0, 1.0)


def recolor_yellow_atlas(
    yellow: np.ndarray,
    paint_weight: np.ndarray,
    target_hue: float,
) -> np.ndarray:
    source_rgb = yellow[..., :3]
    value = np.max(source_rgb, axis=2)
    minimum = np.min(source_rgb, axis=2)
    saturation = np.divide(
        value - minimum,
        value,
        out=np.zeros_like(value),
        where=value > 1.0e-6,
    )
    recolored = fixed_hue_rgb(value, saturation, target_hue)

    result = yellow.copy()
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
    parser.add_argument("--yellow", type=Path, required=True)
    parser.add_argument("--blue-reference", type=Path, required=True)
    parser.add_argument("--output-directory", type=Path, required=True)
    args = parser.parse_args()

    yellow = load_rgba(args.yellow)
    blue = load_rgba(args.blue_reference)
    if yellow.shape != blue.shape:
        raise RuntimeError(f"Atlas sizes differ: yellow={yellow.shape}, blue={blue.shape}")

    paint_weight = build_paint_weight(yellow, blue)
    for color_name, hue in TARGET_HUES.items():
        output_path = args.output_directory / f"Material-color-{color_name}.png"
        save_rgba(output_path, recolor_yellow_atlas(yellow, paint_weight, hue))
        print(f"WROTE {output_path}")


if __name__ == "__main__":
    main()
