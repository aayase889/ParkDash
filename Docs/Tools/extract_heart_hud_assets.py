#!/usr/bin/env python3
"""Extract the approved heart preview into transparent Unity HUD textures."""

from __future__ import annotations

import argparse
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter


PREVIEW_CROPS = {
    "heart_full": (112, 178, 636, 650),
    "heart_stale": (662, 178, 1190, 650),
    "heart_broken_borderless": (1200, 178, 1750, 650),
}


def connected_components(mask: np.ndarray) -> list[np.ndarray]:
    height, width = mask.shape
    visited = np.zeros_like(mask, dtype=bool)
    components: list[np.ndarray] = []

    for y in range(height):
        for x in range(width):
            if not mask[y, x] or visited[y, x]:
                continue

            pixels: list[tuple[int, int]] = []
            queue: deque[tuple[int, int]] = deque([(y, x)])
            visited[y, x] = True
            while queue:
                current_y, current_x = queue.popleft()
                pixels.append((current_y, current_x))
                for next_y, next_x in (
                    (current_y - 1, current_x),
                    (current_y + 1, current_x),
                    (current_y, current_x - 1),
                    (current_y, current_x + 1),
                ):
                    if (
                        0 <= next_y < height
                        and 0 <= next_x < width
                        and mask[next_y, next_x]
                        and not visited[next_y, next_x]
                    ):
                        visited[next_y, next_x] = True
                        queue.append((next_y, next_x))

            component = np.zeros_like(mask, dtype=bool)
            component[tuple(np.array(pixels).T)] = True
            components.append(component)

    components.sort(key=np.count_nonzero, reverse=True)
    return components


def fill_enclosed_holes(mask: np.ndarray) -> np.ndarray:
    height, width = mask.shape
    exterior = np.zeros_like(mask, dtype=bool)
    queue: deque[tuple[int, int]] = deque()

    for x in range(width):
        if not mask[0, x]:
            queue.append((0, x))
        if not mask[height - 1, x]:
            queue.append((height - 1, x))
    for y in range(height):
        if not mask[y, 0]:
            queue.append((y, 0))
        if not mask[y, width - 1]:
            queue.append((y, width - 1))

    while queue:
        y, x = queue.popleft()
        if exterior[y, x] or mask[y, x]:
            continue
        exterior[y, x] = True
        for next_y, next_x in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
            if 0 <= next_y < height and 0 <= next_x < width:
                queue.append((next_y, next_x))

    return mask | (~mask & ~exterior)


def red_surface_mask(rgb: np.ndarray, stale: bool) -> np.ndarray:
    red = rgb[:, :, 0].astype(np.int16)
    green = rgb[:, :, 1].astype(np.int16)
    blue = rgb[:, :, 2].astype(np.int16)
    if stale:
        candidate = (red > 62) & (red - green > 14) & (red - blue > 1)
    else:
        candidate = (red > 58) & (red - green > 22) & (red - blue > 8)

    components = connected_components(candidate)
    if not components:
        raise RuntimeError("Could not isolate a heart surface from the preview.")
    return fill_enclosed_holes(components[0])


def bordered_heart(preview: Image.Image, crop: tuple[int, int, int, int], stale: bool) -> Image.Image:
    source = preview.crop(crop).convert("RGBA")
    rgb = np.asarray(source)[:, :, :3]
    surface = red_surface_mask(rgb, stale)

    # Expand by the approved artwork's navy rim width. The underlying preview
    # pixels are retained, so the bevel and highlight remain exactly as shown.
    surface_image = Image.fromarray((surface.astype(np.uint8) * 255), mode="L")
    alpha = surface_image.filter(ImageFilter.MaxFilter(41))
    alpha = alpha.filter(ImageFilter.GaussianBlur(0.85))

    rgba = np.asarray(source).copy()
    red = rgba[:, :, 0].astype(np.int16)
    green = rgba[:, :, 1].astype(np.int16)
    blue = rgba[:, :, 2].astype(np.int16)
    rim = (np.asarray(alpha) > 2) & ~surface
    background_texture = rim & ((blue - red < 34) | (blue - green < 12))
    rgba[background_texture, :3] = np.array([7, 29, 62], dtype=np.uint8)

    cleaned = Image.fromarray(rgba, mode="RGBA")
    cleaned.putalpha(alpha)
    return cleaned


def borderless_broken_heart(preview: Image.Image, crop: tuple[int, int, int, int]) -> Image.Image:
    source = preview.crop(crop).convert("RGBA")
    rgb = np.asarray(source)[:, :, :3]
    red = rgb[:, :, 0].astype(np.int16)
    green = rgb[:, :, 1].astype(np.int16)
    blue = rgb[:, :, 2].astype(np.int16)

    candidate = (red > 54) & (red - green > 20) & (red - blue > 8)
    components = connected_components(candidate)
    large_components = [component for component in components if np.count_nonzero(component) > 8000]
    if len(large_components) < 2:
        raise RuntimeError("Could not isolate both halves of the broken heart.")

    # Keep only the red halves. The navy outer rim and navy crack are omitted,
    # while enclosed glossy highlights remain part of the red artwork.
    red_halves = fill_enclosed_holes(large_components[0] | large_components[1])
    alpha = Image.fromarray((red_halves.astype(np.uint8) * 255), mode="L")
    alpha = alpha.filter(ImageFilter.GaussianBlur(0.65))
    source.putalpha(alpha)
    return source


def normalize_to_square(image: Image.Image, output_size: int = 512) -> Image.Image:
    alpha = image.getchannel("A")
    bounds = alpha.getbbox()
    if bounds is None:
        raise RuntimeError("Generated heart texture has no visible pixels.")

    visible = image.crop(bounds)
    padding = max(12, int(max(visible.size) * 0.075))
    square_edge = max(visible.size) + padding * 2
    square = Image.new("RGBA", (square_edge, square_edge), (0, 0, 0, 0))
    offset = ((square_edge - visible.width) // 2, (square_edge - visible.height) // 2)
    square.alpha_composite(visible, offset)
    return square.resize((output_size, output_size), Image.Resampling.LANCZOS)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    args = parser.parse_args()

    preview = Image.open(args.preview).convert("RGB")
    args.output_dir.mkdir(parents=True, exist_ok=True)

    assets = {
        "heart_full": bordered_heart(preview, PREVIEW_CROPS["heart_full"], stale=False),
        "heart_stale": bordered_heart(preview, PREVIEW_CROPS["heart_stale"], stale=True),
        "heart_broken_borderless": borderless_broken_heart(
            preview, PREVIEW_CROPS["heart_broken_borderless"]
        ),
    }

    for name, asset in assets.items():
        normalized = normalize_to_square(asset)
        normalized.save(args.output_dir / f"{name}.png", optimize=True)


if __name__ == "__main__":
    main()
