"""Create the shared brown-to-amber car-eye atlas used by the mobile rig."""

from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
from PIL import Image


def smoothstep(edge0: float, edge1: float, value: np.ndarray) -> np.ndarray:
    amount = np.clip((value - edge0) / (edge1 - edge0), 0.0, 1.0)
    return amount * amount * (3.0 - 2.0 * amount)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--size", type=int, default=256)
    args = parser.parse_args()

    size = args.size
    coordinate = (np.arange(size, dtype=np.float32) + 0.5) / size * 2.0 - 1.0
    x, y = np.meshgrid(coordinate, coordinate)
    radius = np.sqrt(x * x + y * y)

    dark_rim = np.array([0.24, 0.075, 0.012], dtype=np.float32)
    brown_top = np.array([0.34, 0.095, 0.025], dtype=np.float32)
    amber_bottom = np.array([1.0, 0.55, 0.025], dtype=np.float32)
    deep_core = np.array([0.055, 0.012, 0.006], dtype=np.float32)

    vertical = smoothstep(-0.58, 0.72, y)[..., None]
    iris = brown_top * (1.0 - vertical) + amber_bottom * vertical
    radial_light = np.clip(1.08 - radius * 0.23, 0.80, 1.08)[..., None]
    iris *= radial_light

    pixels = np.broadcast_to(dark_rim, (size, size, 3)).copy()
    iris_weight = 1.0 - smoothstep(0.79, 0.87, radius)
    pixels = pixels * (1.0 - iris_weight[..., None]) + iris * iris_weight[..., None]

    # Lift the dark core toward the top so the golden lower crescent is more
    # prominent, matching the approved character-eye reference.
    core_radius = np.sqrt(x * x + (y + 0.13) * (y + 0.13))
    core_weight = 1.0 - smoothstep(0.54, 0.62, core_radius)
    pixels = pixels * (1.0 - core_weight[..., None]) + deep_core * core_weight[..., None]

    args.output.parent.mkdir(parents=True, exist_ok=True)
    # The eye disc is mounted on a tilted XZ surface whose UV V axis appears
    # inverted from the top-down game camera. Flip once here so the rendered
    # eye—not merely the source PNG—keeps its dark upper pupil and golden lower
    # crescent in every car-local orientation.
    encoded = np.flip(
        np.rint(np.clip(pixels, 0.0, 1.0) * 255.0).astype(np.uint8),
        axis=0)
    Image.fromarray(encoded, mode="RGB").save(args.output, optimize=True)
    print(f"WROTE {args.output} ({size}x{size})")


if __name__ == "__main__":
    main()
