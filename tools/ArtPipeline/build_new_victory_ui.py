#!/usr/bin/env python3
"""Build exact, runtime-ready layers from the approved generated Win UI art."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter


def soft_white_alpha(image: Image.Image) -> np.ndarray:
    rgba = np.asarray(image.convert("RGBA"), dtype=np.float32)
    rgb = rgba[..., :3]
    chroma = rgb.max(axis=2) - rgb.min(axis=2)
    brightness = rgb.mean(axis=2)
    # Remove only the neutral matte connected to the image edge. A global
    # "remove white" operation also erases the panel's cream frame and was the
    # cause of the dark pinholes visible over the game background.
    candidate = ((brightness > 213.0) & (chroma < 25.0)).astype(np.uint8) * 255
    # Use a deterministic scanline flood from the edge so the internal cream
    # frame remains opaque while the connected exterior matte is removed.
    exterior = np.zeros(candidate.shape, dtype=bool)
    stack: list[tuple[int, int]] = [(0, 0)]
    max_y, max_x = candidate.shape
    while stack:
        seed_x, seed_y = stack.pop()
        if seed_x < 0 or seed_x >= max_x or seed_y < 0 or seed_y >= max_y:
            continue
        if exterior[seed_y, seed_x] or candidate[seed_y, seed_x] == 0:
            continue
        left = seed_x
        while left > 0 and candidate[seed_y, left - 1] != 0 and not exterior[seed_y, left - 1]:
            left -= 1
        right = seed_x
        while right + 1 < max_x and candidate[seed_y, right + 1] != 0 and not exterior[seed_y, right + 1]:
            right += 1
        exterior[seed_y, left:right + 1] = True
        if seed_y > 0:
            for x in range(left, right + 1):
                if candidate[seed_y - 1, x] != 0 and not exterior[seed_y - 1, x]:
                    stack.append((x, seed_y - 1))
        if seed_y + 1 < max_y:
            for x in range(left, right + 1):
                if candidate[seed_y + 1, x] != 0 and not exterior[seed_y + 1, x]:
                    stack.append((x, seed_y + 1))
    object_mask = Image.fromarray((~exterior).astype(np.uint8) * 255, "L")
    object_mask = object_mask.filter(ImageFilter.GaussianBlur(0.65))
    cleaned = np.asarray(object_mask, dtype=np.float32) / 255.0
    cleaned *= rgba[..., 3] / 255.0
    return cleaned


def alpha_bbox(alpha: np.ndarray, threshold: int = 8) -> tuple[int, int, int, int]:
    ys, xs = np.nonzero(alpha > threshold)
    return int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1


def crop_alpha_layer(
    source: Image.Image,
    box: tuple[int, int, int, int],
    mask: np.ndarray,
    output: Path,
) -> dict[str, int]:
    rgba = np.asarray(source.convert("RGBA")).copy()
    rgba[..., 3] = mask
    x0, y0, x1, y1 = box
    cropped = Image.fromarray(rgba[y0:y1, x0:x1], "RGBA")
    cropped.save(output)
    return {"x": x0, "y": y0, "width": x1 - x0, "height": y1 - y0}


def build_panel(source_path: Path, output_path: Path) -> None:
    source = Image.open(source_path).convert("RGBA")
    alpha = soft_white_alpha(source)
    rgba = np.asarray(source).copy()
    rgba[..., 3] = np.clip(alpha * 255.0, 0, 255).astype(np.uint8)
    Image.fromarray(rgba, "RGBA").save(output_path)


def build_button_layers(source_path: Path, output_dir: Path) -> dict:
    source = Image.open(source_path).convert("RGBA")
    rgba = np.asarray(source)
    height, width = rgba.shape[:2]
    yy, xx = np.mgrid[:height, :width]

    # Exact approved-source bounds. The alpha masks keep the original control
    # pixels, while omitting the surrounding blue panel completely.
    specs = {
        "ContinueButton": (270, 942, 894, 1225),
        "CloseButton": (883, 103, 1107, 327),
    }
    result: dict[str, dict[str, int]] = {}
    for name, box in specs.items():
        x0, y0, x1, y1 = box
        region = np.zeros((height, width), dtype=bool)
        region[y0:y1, x0:x1] = True
        if name == "ContinueButton":
            rf = rgba[..., 0].astype(np.float32)
            gf = rgba[..., 1].astype(np.float32)
            bf = rgba[..., 2].astype(np.float32)
            panel_blue = (bf > gf * 1.05) & (bf > rf * 1.18) & (bf > 72.0)
            foreground = region & ~panel_blue
            # The control is the only non-blue object in this source region.
            # Fill each occupied scanline between its first and last rim pixel;
            # this retains the supplied anti-aliased lettering and highlight.
            solid = np.zeros_like(foreground)
            for y in range(y0, y1):
                xs = np.nonzero(foreground[y, x0:x1])[0]
                if xs.size > 0:
                    solid[y, x0 + int(xs.min()):x0 + int(xs.max()) + 1] = True
            mask_image = Image.fromarray(solid.astype(np.uint8) * 255, "L")
            mask_array = np.asarray(mask_image.filter(ImageFilter.GaussianBlur(0.6)))
        else:
            # The red control itself is a 140-pixel circle centred at (979,207).
            # The nearby orange/blue rail belongs to the panel and must not
            # scale when X is pressed.
            center_x, center_y, radius = 979.0, 207.0, 65.0
            signed_distance = np.sqrt((xx - center_x) ** 2 + (yy - center_y) ** 2) - radius
            analytic_alpha = np.clip(0.5 - signed_distance, 0.0, 1.0)
            mask_array = np.clip(analytic_alpha * region * 255.0, 0, 255).astype(np.uint8)
        actual_box = alpha_bbox(mask_array)
        # Transparent texels use a neutral rim color, preventing blue/black
        # fringe during the press-scale animation's bilinear filtering.
        button_rgba = rgba.copy()
        button_rgba[mask_array <= 8, :3] = 255
        result[name] = crop_alpha_layer(
            Image.fromarray(button_rgba, "RGBA"),
            actual_box,
            mask_array,
            output_dir / f"{name}.png",
        )
    return result


def build_perfect_layers(source_path: Path, output_dir: Path) -> dict:
    source = Image.open(source_path).convert("RGBA")
    rgba = np.asarray(source)
    height, width = rgba.shape[:2]
    yy, xx = np.mgrid[:height, :width]

    # Text occupies this exact source region. Color-based seeds isolate the
    # golden fill and brown extrusion; a short dilation captures the white rim.
    region = (xx >= 430) & (xx <= 1490) & (yy >= 510) & (yy <= 750)
    r, g, b = rgba[..., 0], rgba[..., 1], rgba[..., 2]
    rf = r.astype(np.float32)
    gf = g.astype(np.float32)
    bf = b.astype(np.float32)
    gold_or_brown = region & (rf > gf * 1.12) & (rf > bf * 1.28) & (gf > bf * 0.82)
    seed = Image.fromarray((gold_or_brown.astype(np.uint8) * 255), "L")
    expanded = np.asarray(seed.filter(ImageFilter.MaxFilter(35))) > 0
    warm_white = region & (r > 205) & (g > 174) & (b > 151) & (rf - bf > 18)
    text_mask = expanded & (gold_or_brown | warm_white)
    text_mask_image = Image.fromarray((text_mask.astype(np.uint8) * 255), "L")
    text_mask_image = text_mask_image.filter(ImageFilter.MaxFilter(3))
    text_mask = np.asarray(text_mask_image)

    columns = np.nonzero((text_mask > 8).any(axis=0))[0]
    x0, x1 = int(columns.min()), int(columns.max()) + 1
    # Exact character intervals measured from the supplied art.
    cuts = [x0, 645, 760, 880, 995, 1110, 1230, 1335, x1]
    names = ["P", "E", "R", "F", "E2", "C", "T", "Bang"]
    # Sample corresponding clean blue-panel pixels and remove the blue ground
    # that is baked between/inside letters while preserving warm art shadows.
    # The approved source and clean plate do not share perfectly identical blue
    # pixels. Fill every non-text pixel with transparent white so bilinear UI
    # filtering can never reveal a blue rectangle around a moving letter.
    layer_rgba = rgba.copy()
    layer_rgba[text_mask <= 8, :3] = 255
    source_for_layers = Image.fromarray(layer_rgba, "RGBA")
    letters: list[dict] = []
    for index, name in enumerate(names):
        local = np.zeros_like(text_mask, dtype=np.uint8)
        local[:, cuts[index]:cuts[index + 1]] = text_mask[:, cuts[index]:cuts[index + 1]]
        if not (local > 8).any():
            raise RuntimeError(f"Could not isolate PERFECT letter {name}")
        box = alpha_bbox(local)
        record = crop_alpha_layer(
            source_for_layers,
            box,
            local,
            output_dir / f"Perfect{name}.png",
        )
        record["name"] = name
        letters.append(record)

    # Untouched final word guarantees a pixel-perfect settled frame.
    word_box = alpha_bbox(text_mask)
    word = crop_alpha_layer(
        source_for_layers,
        word_box,
        text_mask,
        output_dir / "PerfectWord.png",
    )
    return {
        "source_width": width,
        "source_height": height,
        "word": word,
        "letters": letters,
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("approved_ui", type=Path)
    parser.add_argument("clean_panel", type=Path)
    parser.add_argument("output_directory", type=Path)
    parser.add_argument("--source-copy-directory", type=Path)
    parser.add_argument("--buttonless-panel", type=Path)
    args = parser.parse_args()
    args.output_directory.mkdir(parents=True, exist_ok=True)

    panel_source = args.buttonless_panel if args.buttonless_panel is not None else args.clean_panel
    build_panel(panel_source, args.output_directory / "VictoryPanel.png")
    manifest = build_perfect_layers(args.approved_ui, args.output_directory)
    manifest["buttons"] = build_button_layers(args.clean_panel, args.output_directory)
    (args.output_directory / "VictoryUiLayers.json").write_text(
        json.dumps(manifest, indent=2) + "\n",
        encoding="utf-8",
    )
    if args.source_copy_directory is not None:
        args.source_copy_directory.mkdir(parents=True, exist_ok=True)
        Image.open(args.approved_ui).save(
            args.source_copy_directory / "ApprovedVictoryUiSource.png")
        Image.open(args.clean_panel).save(
            args.source_copy_directory / "CleanVictoryPanelSource.png")
        if args.buttonless_panel is not None:
            Image.open(args.buttonless_panel).save(
                args.source_copy_directory / "ButtonlessVictoryPanelSource.png")


if __name__ == "__main__":
    main()
