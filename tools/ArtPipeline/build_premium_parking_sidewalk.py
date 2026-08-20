#!/usr/bin/env python3
"""Build the clean 2-5 space parking-sidewalk sprites from one master.

The master is an orthographic, chroma-keyed ImageGen source.  This builder
locks the output canvas, live-game alignment, and straight rectangular opening,
then changes capacity only through the transparent center and bottom pavement.
"""

from pathlib import Path

from PIL import Image


PROJECT = Path(__file__).resolve().parents[2]
SOURCE = PROJECT / "ArtSource/Environment/PremiumParkingSidewalk_Source_Cutout.png"
OUTPUT = PROJECT / "Assets/Resources/Environment"

CANVAS_HEIGHT = 543
BASE_WIDTH = 766
SUBJECT_BOX = (10, 21, 756, 497)
# The three-space master has a 546-pixel opening: exactly three 182-pixel
# bays. Grow or shrink only that transparent center so every capacity keeps
# the same bay width, including the two outer bays beside the sidewalks.
SIDEWALK_PIXEL_WIDTH = 220
BAY_PIXEL_WIDTH = 182
TARGET_WIDTHS = {
    capacity: SIDEWALK_PIXEL_WIDTH + capacity * BAY_PIXEL_WIDTH
    for capacity in range(2, 6)
}


def splice_center(base: Image.Image, target_width: int) -> Image.Image:
    if target_width == base.width:
        return base.copy()

    difference = target_width - base.width
    if difference < 0:
        removed = -difference
        left_cut = (base.width - removed) // 2
        right_cut = left_cut + removed
        result = Image.new("RGBA", (target_width, base.height))
        result.alpha_composite(base.crop((0, 0, left_cut, base.height)), (0, 0))
        result.alpha_composite(base.crop((right_cut, 0, base.width, base.height)), (left_cut, 0))
        return result

    split = base.width // 2
    sample_left = max(0, split - difference // 2)
    sample = base.crop((sample_left, 0, sample_left + difference, base.height))
    result = Image.new("RGBA", (target_width, base.height))
    result.alpha_composite(base.crop((0, 0, split, base.height)), (0, 0))
    result.alpha_composite(sample, (split, 0))
    result.alpha_composite(base.crop((split, 0, base.width, base.height)), (split + difference, 0))
    return result


def remove_edge_halo(image: Image.Image) -> None:
    pixels = image.load()
    alpha = image.getchannel("A")
    bounds = alpha.getbbox()
    if bounds is None:
        return
    left, top, right, bottom = bounds

    # Image generation deliberately used no cast shadow. Chroma antialiasing
    # can still leave a one-pixel dark fringe at the outermost silhouette;
    # remove only those boundary pixels, never the authored grout or curb.
    for y in range(top, bottom):
        for x in (left, right - 1):
            r, g, b, a = pixels[x, y]
            if a < 245 and max(r, g, b) < 95:
                pixels[x, y] = (r, g, b, 0)
    for x in range(left, right):
        for y in (top, bottom - 1):
            r, g, b, a = pixels[x, y]
            if a < 245 and max(r, g, b) < 95:
                pixels[x, y] = (r, g, b, 0)

    # Remove the dark one-pixel contour sometimes introduced between a keyed
    # silhouette and transparency. This includes the old "black line" failure
    # mode on the parking-facing curb, while leaving the bright antialiasing and
    # all interior grout intact.
    original_alpha = image.getchannel("A")
    to_clear = []
    for y in range(1, image.height - 1):
        for x in range(1, image.width - 1):
            r, g, b, a = pixels[x, y]
            if a <= 20 or max(r, g, b) >= 85:
                continue
            if any(
                original_alpha.getpixel((x + dx, y + dy)) <= 20
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))
            ):
                to_clear.append((x, y))
    for x, y in to_clear:
        r, g, b, _ = pixels[x, y]
        pixels[x, y] = (r, g, b, 0)


def validate(image: Image.Image, capacity: int) -> None:
    expected = (TARGET_WIDTHS[capacity], CANVAS_HEIGHT)
    if image.size != expected:
        raise RuntimeError(f"Capacity {capacity}: expected {expected}, got {image.size}")
    if image.mode != "RGBA":
        raise RuntimeError(f"Capacity {capacity}: expected RGBA, got {image.mode}")

    alpha = image.getchannel("A")
    if alpha.getpixel((image.width // 2, 100)) != 0:
        raise RuntimeError(f"Capacity {capacity}: parking opening is not transparent")
    if alpha.getpixel((0, 0)) != 0 or alpha.getpixel((image.width - 1, 0)) != 0:
        raise RuntimeError(f"Capacity {capacity}: canvas corners are not transparent")


def main() -> None:
    source = Image.open(SOURCE).convert("RGBA")
    bounds = source.getchannel("A").getbbox()
    if bounds is None:
        raise RuntimeError("Master source has no visible pixels")

    subject = source.crop(bounds).resize(
        (SUBJECT_BOX[2] - SUBJECT_BOX[0], SUBJECT_BOX[3] - SUBJECT_BOX[1]),
        Image.Resampling.LANCZOS,
    )
    base = Image.new("RGBA", (BASE_WIDTH, CANVAS_HEIGHT))
    base.alpha_composite(subject, SUBJECT_BOX[:2])

    OUTPUT.mkdir(parents=True, exist_ok=True)
    for capacity, width in TARGET_WIDTHS.items():
        image = splice_center(base, width)
        remove_edge_halo(image)
        validate(image, capacity)
        image.save(OUTPUT / f"ApprovedParkingSidewalk_{capacity}.png", optimize=True)


if __name__ == "__main__":
    main()
