#!/usr/bin/env python3
"""Build deterministic box-break atlas assets from the approved box artwork."""

from __future__ import annotations

import json
import math
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Assets/Resources/CarPrototype/Mechanics/MysteryBox.png"
OUTPUT_DIRECTORY = ROOT / "Assets/Resources/CarPrototype/Mechanics/RevealVfx"
ATLAS_PATH = OUTPUT_DIRECTORY / "PremiumMysteryBoxFragments.png"
MANIFEST_PATH = OUTPUT_DIRECTORY / "PremiumMysteryBoxFragments.json"
QUESTION_PATH = OUTPUT_DIRECTORY / "PremiumMysteryQuestion.png"

CELL_SIZE = 256
ATLAS_COLUMNS = 4
ATLAS_ROWS = 4


FRAGMENTS = (
    ("Upper Left Bolted Board", 0.30, 0.17),
    ("Upper Right Bolted Board", 0.70, 0.17),
    ("Lower Left L Board", 0.27, 0.62),
    ("Lower Right Rail", 0.75, 0.66),
    ("Lower Front Plate", 0.50, 0.88),
    ("Mid Right Bolted Block", 0.72, 0.43),
    ("Right Horizontal Slat", 0.80, 0.53),
    ("Upper Right Wedge", 0.63, 0.29),
    ("Left Middle Wedge", 0.27, 0.40),
    ("Left Upper Chip", 0.39, 0.29),
    ("Left Lower Chip", 0.39, 0.62),
    ("Right Small Chip", 0.65, 0.61),
    ("Lower Center Chip", 0.54, 0.74),
    ("Central Upper Chip", 0.50, 0.43),
)


def connected_components(mask: np.ndarray) -> list[list[tuple[int, int]]]:
    height, width = mask.shape
    seen = np.zeros_like(mask, dtype=bool)
    components: list[list[tuple[int, int]]] = []
    for y in range(height):
        for x in range(width):
            if not mask[y, x] or seen[y, x]:
                continue
            points: list[tuple[int, int]] = []
            queue = deque([(x, y)])
            seen[y, x] = True
            while queue:
                px, py = queue.popleft()
                points.append((px, py))
                for ny in range(max(0, py - 1), min(height, py + 2)):
                    for nx in range(max(0, px - 1), min(width, px + 2)):
                        if mask[ny, nx] and not seen[ny, nx]:
                            seen[ny, nx] = True
                            queue.append((nx, ny))
            components.append(points)
    components.sort(key=len, reverse=True)
    return components


def build_question_mask(source: np.ndarray) -> np.ndarray:
    height, width, _ = source.shape
    r = source[:, :, 0].astype(np.float32)
    g = source[:, :, 1].astype(np.float32)
    b = source[:, :, 2].astype(np.float32)
    a = source[:, :, 3]
    yy, xx = np.mgrid[0:height, 0:width]

    central_region = (
        (xx > width * 0.35)
        & (xx < width * 0.65)
        & (yy > height * 0.20)
        & (yy < height * 0.52)
    )
    pale_gold = (
        (a > 16)
        & (r > 205)
        & (g > 105)
        & (g > b * 1.03)
        & (r < g * 2.25)
    )
    candidates = pale_gold & central_region
    components = connected_components(candidates)
    selected = np.zeros_like(candidates)
    accepted = 0
    for component in components:
        if len(component) < 180:
            continue
        xs = np.fromiter((point[0] for point in component), dtype=np.int32)
        ys = np.fromiter((point[1] for point in component), dtype=np.int32)
        center_x = float(xs.mean()) / width
        center_y = float(ys.mean()) / height
        if 0.37 <= center_x <= 0.63 and 0.22 <= center_y <= 0.51:
            selected[ys, xs] = True
            accepted += 1
            if accepted == 2:
                break
    if accepted < 2:
        raise RuntimeError("Could not isolate both parts of the mystery question mark.")

    selected_image = Image.fromarray((selected.astype(np.uint8) * 255), mode="L")
    selected_image = selected_image.filter(ImageFilter.MaxFilter(9))
    return np.array(selected_image) > 0


def build_question_texture(source: np.ndarray, question_mask: np.ndarray) -> None:
    ys, xs = np.where(question_mask)
    margin = 28
    left = max(0, int(xs.min()) - margin)
    top = max(0, int(ys.min()) - margin)
    right = min(source.shape[1], int(xs.max()) + margin + 1)
    bottom = min(source.shape[0], int(ys.max()) + margin + 1)

    cropped_source = source[top:bottom, left:right].copy()
    cropped_mask = question_mask[top:bottom, left:right]
    front_alpha = Image.fromarray((cropped_mask.astype(np.uint8) * 255), mode="L")
    front_alpha = front_alpha.filter(ImageFilter.GaussianBlur(0.7))

    shadow_alpha = front_alpha.filter(ImageFilter.MaxFilter(11)).filter(
        ImageFilter.GaussianBlur(1.3)
    )
    canvas_width = cropped_source.shape[1] + 18
    canvas_height = cropped_source.shape[0] + 22
    canvas = Image.new("RGBA", (canvas_width, canvas_height), (0, 0, 0, 0))
    extrusion = Image.new("RGBA", front_alpha.size, (50, 70, 100, 218))
    extrusion.putalpha(shadow_alpha.point(lambda value: int(value * 0.84)))
    canvas.alpha_composite(extrusion, (12, 15))

    # The detached mark in the reference flashes almost white while retaining
    # a warm, embossed edge. Lift the approved gold artwork toward cream but
    # preserve its original highlights and shading.
    cream = np.array([255.0, 239.0, 205.0], dtype=np.float32)
    lifted_front = cropped_source.copy()
    lifted_front[:, :, :3] = np.clip(
        cropped_source[:, :, :3].astype(np.float32) * 0.42 + cream * 0.58,
        0,
        255,
    ).astype(np.uint8)
    front = Image.fromarray(lifted_front, mode="RGBA")
    front.putalpha(front_alpha)
    canvas.alpha_composite(front, (3, 3))
    canvas.save(QUESTION_PATH)


def build_fragment_atlas(source: np.ndarray, question_mask: np.ndarray) -> list[dict]:
    height, width, _ = source.shape
    opaque = source[:, :, 3] > 8
    # Remove the complete embossed question (face, orange rim, and cast
    # shadow) from the wood pieces. The tighter mask above remains useful for
    # building the clean standalone mark; this broader mask prevents a second
    # orange question silhouette from travelling with the debris.
    exclusion_image = Image.fromarray(
        (question_mask.astype(np.uint8) * 255),
        mode="L",
    ).filter(ImageFilter.MaxFilter(31))
    question_exclusion = np.array(exclusion_image) > 0
    fracture_pixels = opaque & ~question_exclusion
    yy, xx = np.mgrid[0:height, 0:width]

    # Slightly perturb the coordinate field so Voronoi boundaries look like
    # authored splinters rather than perfectly straight computer cuts.
    warped_x = xx + 7.0 * np.sin(yy * 0.027) + 3.0 * np.sin(yy * 0.071)
    warped_y = yy + 6.0 * np.sin(xx * 0.023) + 2.5 * np.cos(xx * 0.067)
    normalized_x = warped_x / width
    normalized_y = warped_y / height

    distances = []
    for _, seed_x, seed_y in FRAGMENTS:
        distances.append((normalized_x - seed_x) ** 2 + (normalized_y - seed_y) ** 2)
    assignments = np.argmin(np.stack(distances, axis=0), axis=0)

    atlas = Image.new(
        "RGBA",
        (CELL_SIZE * ATLAS_COLUMNS, CELL_SIZE * ATLAS_ROWS),
        (0, 0, 0, 0),
    )
    manifest: list[dict] = []
    for index, (name, _, _) in enumerate(FRAGMENTS):
        region = fracture_pixels & (assignments == index)
        if not region.any():
            raise RuntimeError(f"Fragment {index} ({name}) contains no pixels.")

        region_image = Image.fromarray((region.astype(np.uint8) * 255), mode="L")
        # One-pixel overlap prevents visible cracks on the impact frame while
        # the flash is replacing the intact box with the individual bodies.
        region_image = region_image.filter(ImageFilter.MaxFilter(3))
        region = np.array(region_image) > 0
        ys, xs = np.where(region)
        left = int(xs.min())
        top = int(ys.min())
        right = int(xs.max()) + 1
        bottom = int(ys.max()) + 1

        fragment_pixels = source[top:bottom, left:right].copy()
        fragment_pixels[:, :, 3] = np.where(
            region[top:bottom, left:right],
            fragment_pixels[:, :, 3],
            0,
        )
        fragment = Image.fromarray(fragment_pixels, mode="RGBA")
        fragment = fragment.resize((CELL_SIZE - 8, CELL_SIZE - 8), Image.Resampling.LANCZOS)

        column = index % ATLAS_COLUMNS
        row = index // ATLAS_COLUMNS
        atlas.alpha_composite(fragment, (column * CELL_SIZE + 4, row * CELL_SIZE + 4))
        manifest.append(
            {
                "name": name,
                "atlasIndex": index,
                "centerX": ((left + right) * 0.5 / width) - 0.5,
                "centerY": 0.5 - ((top + bottom) * 0.5 / height),
                "width": (right - left) / width,
                "height": (bottom - top) / height,
                "uvX": column / ATLAS_COLUMNS,
                "uvY": 1.0 - ((row + 1) / ATLAS_ROWS),
                "uvWidth": 1.0 / ATLAS_COLUMNS,
                "uvHeight": 1.0 / ATLAS_ROWS,
            }
        )

    atlas.save(ATLAS_PATH)
    return manifest


def main() -> None:
    OUTPUT_DIRECTORY.mkdir(parents=True, exist_ok=True)
    source_image = Image.open(SOURCE).convert("RGBA")
    source = np.array(source_image)
    question_mask = build_question_mask(source)
    build_question_texture(source, question_mask)
    manifest = build_fragment_atlas(source, question_mask)
    MANIFEST_PATH.write_text(
        json.dumps({"fragments": manifest}, indent=2) + "\n",
        encoding="utf-8",
    )
    print(f"Built {len(manifest)} fragments: {ATLAS_PATH}")
    print(f"Built question mark: {QUESTION_PATH}")
    print(f"Wrote manifest: {MANIFEST_PATH}")


if __name__ == "__main__":
    main()
