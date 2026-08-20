#!/usr/bin/env python3
"""Build deterministic UI animation layers from the approved Park Dash logo."""

from __future__ import annotations

import argparse
from collections import deque
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter


PARK_BOXES = {
    "P": (950, 255, 1295, 670),
    "A": (1215, 235, 1555, 665),
    "R": (1480, 240, 1845, 670),
    "K": (1750, 270, 2125, 685),
}
DASH_BOXES = {
    "D": (745, 625, 1215, 1135),
    "A": (1115, 610, 1565, 1135),
    "S": (1465, 615, 1905, 1135),
    "H": (1790, 620, 2285, 1140),
}
OUTPUT_SCALE = 0.5


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("input", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--badge-source", type=Path)
    return parser.parse_args()


def dilate(mask: np.ndarray, diameter: int) -> np.ndarray:
    if diameter % 2 == 0:
        diameter += 1
    image = Image.fromarray((mask.astype(np.uint8) * 255), mode="L")
    return np.asarray(image.filter(ImageFilter.MaxFilter(diameter))) > 0


def soften(mask: np.ndarray, radius: float = 1.2) -> np.ndarray:
    image = Image.fromarray((mask.astype(np.uint8) * 255), mode="L")
    return np.asarray(image.filter(ImageFilter.GaussianBlur(radius)), dtype=np.float32) / 255.0


def select_center_component(seed: np.ndarray) -> np.ndarray:
    ys, xs = np.nonzero(seed)
    if len(xs) == 0:
        return seed
    center_x = (seed.shape[1] - 1) * 0.5
    center_y = (seed.shape[0] - 1) * 0.5
    nearest = np.argmin((xs - center_x) ** 2 + (ys - center_y) ** 2)
    start_x = int(xs[nearest])
    start_y = int(ys[nearest])
    selected = np.zeros_like(seed, dtype=bool)
    selected[start_y, start_x] = True
    pending: deque[tuple[int, int]] = deque([(start_x, start_y)])
    while pending:
        x, y = pending.popleft()
        for next_x, next_y in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if next_x < 0 or next_x >= seed.shape[1]:
                continue
            if next_y < 0 or next_y >= seed.shape[0]:
                continue
            if selected[next_y, next_x] or not seed[next_y, next_x]:
                continue
            selected[next_y, next_x] = True
            pending.append((next_x, next_y))
    return selected


def make_component_mask(
    rgba: np.ndarray,
    bounds: tuple[int, int, int, int],
    kind: str,
) -> np.ndarray:
    height, width = rgba.shape[:2]
    x0, y0, x1, y1 = bounds
    x0, y0 = max(0, x0), max(0, y0)
    x1, y1 = min(width, x1), min(height, y1)
    crop = rgba[y0:y1, x0:x1]
    red = crop[..., 0].astype(np.int16)
    green = crop[..., 1].astype(np.int16)
    blue = crop[..., 2].astype(np.int16)
    alpha = crop[..., 3] > 8

    if kind == "park":
        neutral_range = np.maximum.reduce([red, green, blue]) - np.minimum.reduce(
            [red, green, blue]
        )
        seed = (
            alpha
            & (red > 130)
            & (green > 125)
            & (blue > 115)
            & (neutral_range < 88)
        )
        seed = select_center_component(seed)
        near = dilate(seed, 105)
        blue_outline = (
            alpha
            & (blue > 62)
            & (blue > red + 20)
            & (green < 76)
            & (red < 102)
        )
        local_mask = seed | (near & blue_outline)
    else:
        seed = alpha & (red > 175) & (green > 75) & (blue < 125)
        seed = select_center_component(seed)
        near = dilate(seed, 105)
        purple_shadow = (
            alpha
            & (blue > 48)
            & (blue > green + 10)
            & (red < 125)
            & (green < 64)
        )
        orange_extrusion = (
            alpha
            & (red > 90)
            & (red > blue + 25)
            & (green < 92)
            & (blue < 82)
        )
        local_mask = seed | (near & (purple_shadow | orange_extrusion))

    result = np.zeros((height, width), dtype=bool)
    result[y0:y1, x0:x1] = local_mask
    return result


def save_component(
    rgba: np.ndarray,
    mask: np.ndarray,
    output: Path,
) -> dict[str, float]:
    visible = mask & (rgba[..., 3] > 0)
    ys, xs = np.nonzero(visible)
    if len(xs) == 0:
        raise RuntimeError(f"No pixels selected for {output.name}")

    padding = 20
    x0 = max(0, int(xs.min()) - padding)
    y0 = max(0, int(ys.min()) - padding)
    x1 = min(rgba.shape[1], int(xs.max()) + padding + 1)
    y1 = min(rgba.shape[0], int(ys.max()) + padding + 1)
    component = rgba[y0:y1, x0:x1].copy()
    component_alpha = soften(mask[y0:y1, x0:x1])
    component[..., 3] = np.clip(
        component[..., 3].astype(np.float32) * component_alpha,
        0,
        255,
    ).astype(np.uint8)

    image = Image.fromarray(component, mode="RGBA")
    image = image.resize(
        (
            max(1, round(image.width * OUTPUT_SCALE)),
            max(1, round(image.height * OUTPUT_SCALE)),
        ),
        Image.Resampling.LANCZOS,
    )
    image.save(output, optimize=True)
    return {
        "x": x0 / rgba.shape[1],
        "y": y0 / rgba.shape[0],
        "width": (x1 - x0) / rgba.shape[1],
        "height": (y1 - y0) / rgba.shape[0],
    }


def build_badge(rgba: np.ndarray, text_mask: np.ndarray) -> np.ndarray:
    badge = rgba.copy()
    height, width = rgba.shape[:2]
    yy, xx = np.mgrid[0:height, 0:width]
    normalized_y = yy / max(1, height - 1)
    normalized_x = np.abs(xx / max(1, width - 1) - 0.5) * 2.0

    top = np.array([43.0, 102.0, 181.0])
    middle = np.array([25.0, 147.0, 219.0])
    bottom = np.array([21.0, 91.0, 165.0])
    first_half = np.clip(normalized_y / 0.55, 0.0, 1.0)[..., None]
    second_half = np.clip((normalized_y - 0.55) / 0.45, 0.0, 1.0)[..., None]
    blue_fill = top * (1.0 - first_half) + middle * first_half
    lower_fill = middle * (1.0 - second_half) + bottom * second_half
    blue_fill = np.where((normalized_y <= 0.55)[..., None], blue_fill, lower_fill)
    blue_fill *= (1.0 - normalized_x[..., None] * 0.10)

    removal = dilate(text_mask, 111)
    # The construction badge is visible for only the first few tenths of a
    # second, but it must read cleanly. Fill the two complete word bands so no
    # letter highlights survive the extraction matte.
    removal[205:710, 875:2165] = True
    removal[585:1165, 690:2340] = True
    badge[removal, :3] = np.clip(blue_fill[removal], 0, 255).astype(np.uint8)
    badge[removal, 3] = 255
    return badge


def prepare_generated_badge(path: Path, size: tuple[int, int]) -> Image.Image:
    image = Image.open(path).convert("RGBA")
    rgba = np.asarray(image).copy()
    rgb = rgba[..., :3].astype(np.int16)
    lightest = rgb.min(axis=2)
    chroma = rgb.max(axis=2) - lightest
    # Image generation supplied a near-white checkerboard instead of native
    # alpha. Only neutral, very light exterior pixels are removed; saturated
    # yellow highlights and the soft dark badge shadow remain untouched.
    exterior = (lightest > 224) & (chroma < 18)
    matte = Image.fromarray((~exterior).astype(np.uint8) * 255, mode="L")
    matte = matte.filter(ImageFilter.GaussianBlur(0.65))
    rgba[..., 3] = np.asarray(matte)
    prepared = Image.fromarray(rgba, mode="RGBA")
    return prepared.resize(size, Image.Resampling.LANCZOS)


def main() -> None:
    args = parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    source = Image.open(args.input).convert("RGBA")
    rgba = np.asarray(source).copy()

    manifest: dict[str, object] = {
        "sourceWidth": source.width,
        "sourceHeight": source.height,
        "layers": {},
    }
    text_mask = np.zeros((source.height, source.width), dtype=bool)
    for letter, bounds in PARK_BOXES.items():
        mask = make_component_mask(rgba, bounds, "park")
        text_mask |= mask
        manifest["layers"][f"Park{letter}"] = save_component(
            rgba,
            mask,
            args.output / f"ParkDashPark{letter}.png",
        )

    dash_mask = np.zeros_like(text_mask)
    for bounds in DASH_BOXES.values():
        mask = make_component_mask(rgba, bounds, "dash")
        dash_mask |= mask
    yy, xx = np.mgrid[0:source.height, 0:source.width]
    red = rgba[..., 0].astype(np.int16)
    green = rgba[..., 1].astype(np.int16)
    blue = rgba[..., 2].astype(np.int16)
    exposed_badge_red = (
        (red > 120)
        & (red > green + 60)
        & (green < 96)
        & (blue < 90)
        & ((yy < 690) | (xx < 800) | (xx > 2220))
    )
    dash_mask &= ~exposed_badge_red
    text_mask |= dash_mask
    manifest["layers"]["Dash"] = save_component(
        rgba,
        dash_mask,
        args.output / "ParkDashDash.png",
    )

    final_logo = source.resize(
        (
            round(source.width * OUTPUT_SCALE),
            round(source.height * OUTPUT_SCALE),
        ),
        Image.Resampling.LANCZOS,
    )
    final_logo.save(args.output / "ParkDashLogo.png", optimize=True)

    if args.badge_source is not None:
        badge = prepare_generated_badge(args.badge_source, final_logo.size)
    else:
        badge = Image.fromarray(build_badge(rgba, text_mask), mode="RGBA")
        badge = badge.resize(final_logo.size, Image.Resampling.LANCZOS)
    badge.save(args.output / "ParkDashBadge.png", optimize=True)

    (args.output / "ParkDashVictoryLayers.json").write_text(
        json.dumps(manifest, indent=2) + "\n",
        encoding="utf-8",
    )
    print(json.dumps(manifest, indent=2))


if __name__ == "__main__":
    main()
