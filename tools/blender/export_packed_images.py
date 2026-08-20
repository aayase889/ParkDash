"""Export packed bitmap images from an opened Blender file.

Usage:
    Blender --background source.blend --python export_packed_images.py -- output_folder

The opened Blender file is never saved or changed on disk.
"""

from __future__ import annotations

import os
import re
import sys
from pathlib import Path

import bpy


def safe_filename(name: str) -> str:
    stem, extension = os.path.splitext(name)
    stem = re.sub(r"[^A-Za-z0-9_.-]+", "_", stem).strip("._") or "image"
    extension = extension.lower()
    if extension not in {".png", ".tga", ".jpg", ".jpeg", ".exr", ".tif", ".tiff"}:
        extension = ".png"
    return f"{stem}{extension}"


args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
output_dir = Path(args[0] if args else "packed_images").resolve()
output_dir.mkdir(parents=True, exist_ok=True)

used_names: set[str] = set()
for image in bpy.data.images:
    if image.source != "FILE" or image.packed_file is None:
        continue

    filename = safe_filename(image.name)
    stem, extension = os.path.splitext(filename)
    suffix = 2
    while filename.lower() in used_names:
        filename = f"{stem}_{suffix}{extension}"
        suffix += 1
    used_names.add(filename.lower())

    output_path = output_dir / filename
    original_raw_path = image.filepath_raw
    original_format = image.file_format
    image.filepath_raw = str(output_path)
    if extension == ".png":
        image.file_format = "PNG"
    elif extension == ".tga":
        image.file_format = "TARGA"
    image.save()
    image.filepath_raw = original_raw_path
    image.file_format = original_format
    print(f"Exported packed image: {output_path}")
