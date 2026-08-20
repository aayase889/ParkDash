"""Bake non-destructive stylized texture maps from an opened Blender car file.

Run with Blender, after the .blend path:

    Blender --background car.blend --python bake_stylized_car_textures.py -- \
        --mesh car_mesh \
        --base-color path/to/hand-painted.png \
        --output-dir path/to/output

The original .blend file and source texture are never saved or overwritten.
"""

from __future__ import annotations

import argparse
import os
import sys

import bpy
import numpy as np


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    argv = argv[argv.index("--") + 1 :] if "--" in argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--mesh", required=True)
    parser.add_argument("--base-color", required=True)
    parser.add_argument("--metallic")
    parser.add_argument("--roughness")
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--bake-output-dir")
    parser.add_argument("--prefix", default="Material")
    parser.add_argument("--size", type=int, default=1024)
    parser.add_argument("--samples", type=int, default=64)
    return parser.parse_args(argv)


def select_only(obj: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def make_image(name: str, size: int, color_space: str, fill: tuple[float, float, float, float]) -> bpy.types.Image:
    image = bpy.data.images.new(
        name,
        width=size,
        height=size,
        alpha=True,
        float_buffer=False,
        is_data=color_space == "Non-Color",
    )
    image.generated_color = fill
    image.colorspace_settings.name = color_space
    image.pixels.foreach_set(np.tile(np.asarray(fill, dtype=np.float32), size * size))
    image.update()
    return image


def save_image(image: bpy.types.Image, path: str) -> None:
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()


def replace_material_with_bake_target(
    obj: bpy.types.Object,
    image: bpy.types.Image,
    emission_from_pointiness: bool,
) -> None:
    material = bpy.data.materials.new(f"{image.name} Bake Material")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()

    output = nodes.new("ShaderNodeOutputMaterial")
    image_node = nodes.new("ShaderNodeTexImage")
    image_node.image = image
    image_node.select = True
    nodes.active = image_node

    if emission_from_pointiness:
        geometry = nodes.new("ShaderNodeNewGeometry")
        ramp = nodes.new("ShaderNodeValToRGB")
        ramp.color_ramp.interpolation = "EASE"
        ramp.color_ramp.elements[0].position = 0.34
        ramp.color_ramp.elements[0].color = (0.12, 0.12, 0.12, 1.0)
        ramp.color_ramp.elements[1].position = 0.66
        ramp.color_ramp.elements[1].color = (0.88, 0.88, 0.88, 1.0)
        midpoint = ramp.color_ramp.elements.new(0.50)
        midpoint.color = (0.50, 0.50, 0.50, 1.0)
        emission = nodes.new("ShaderNodeEmission")
        emission.inputs["Strength"].default_value = 1.0
        links.new(geometry.outputs["Pointiness"], ramp.inputs["Fac"])
        links.new(ramp.outputs["Color"], emission.inputs["Color"])
        links.new(emission.outputs["Emission"], output.inputs["Surface"])
    else:
        principled = nodes.new("ShaderNodeBsdfPrincipled")
        principled.inputs["Base Color"].default_value = (0.8, 0.8, 0.8, 1.0)
        principled.inputs["Roughness"].default_value = 0.55
        links.new(principled.outputs["BSDF"], output.inputs["Surface"])

    obj.data.materials.clear()
    obj.data.materials.append(material)


def bake_map(
    obj: bpy.types.Object,
    name: str,
    output_path: str,
    size: int,
    bake_type: str,
    emission_from_pointiness: bool,
) -> bpy.types.Image:
    fill = (0.5, 0.5, 0.5, 1.0) if emission_from_pointiness else (1.0, 1.0, 1.0, 1.0)
    image = make_image(name, size, "Non-Color", fill)
    replace_material_with_bake_target(obj, image, emission_from_pointiness)
    select_only(obj)
    bpy.ops.object.bake(type=bake_type)
    save_image(image, output_path)
    return image


def image_array(path: str, size: int, color_space: str) -> np.ndarray:
    image = bpy.data.images.load(path, check_existing=False)
    image.colorspace_settings.name = color_space
    if tuple(image.size) != (size, size):
        image.scale(size, size)
    pixels = np.empty(size * size * 4, dtype=np.float32)
    image.pixels.foreach_get(pixels)
    return pixels.reshape((size, size, 4))


def baked_array(image: bpy.types.Image, size: int) -> np.ndarray:
    pixels = np.empty(size * size * 4, dtype=np.float32)
    image.pixels.foreach_get(pixels)
    return pixels.reshape((size, size, 4))


def write_array(
    name: str,
    pixels: np.ndarray,
    path: str,
    color_space: str,
) -> None:
    height, width, _ = pixels.shape
    image = make_image(name, width, color_space, (0.0, 0.0, 0.0, 1.0))
    if height != width:
        image.scale(width, height)
    image.pixels.foreach_set(np.clip(pixels, 0.0, 1.0).astype(np.float32).ravel())
    image.update()
    save_image(image, path)


def create_stylized_maps(args: argparse.Namespace, ao_image: bpy.types.Image, curvature_image: bpy.types.Image) -> None:
    size = args.size
    base = image_array(args.base_color, size, "sRGB")
    ao = baked_array(ao_image, size)[..., 0]
    curvature = baked_array(curvature_image, size)[..., 0]

    rgb = base[..., :3].copy()
    background = (
        np.all(base[..., :3] > 0.995, axis=2)
        & (ao < 0.002)
        & (curvature < 0.002)
    )
    # Keep the artist's brushwork dominant: AO only strengthens contact areas,
    # while curvature adds a small toy-like highlight along convex edges.
    ao_factor = 1.0 - 0.24 * (1.0 - ao)
    rgb *= ao_factor[..., None]
    curvature_signed = np.clip((curvature - 0.5) * 2.0, -1.0, 1.0)
    convex = np.maximum(curvature_signed, 0.0)[..., None]
    concave = np.maximum(-curvature_signed, 0.0)[..., None]
    rgb += (1.0 - rgb) * convex * 0.11
    rgb *= 1.0 - concave * 0.07
    rgb[background] = base[..., :3][background]

    stylized = np.concatenate((np.clip(rgb, 0.0, 1.0), base[..., 3:4]), axis=2)
    write_array(
        f"{args.prefix} Stylized Base Color",
        stylized,
        os.path.join(args.output_dir, f"{args.prefix}-color-stylized.png"),
        "sRGB",
    )

    if args.metallic:
        metallic_source = image_array(args.metallic, size, "Non-Color")[..., :3].mean(axis=2)
    else:
        metallic_source = np.zeros((size, size), dtype=np.float32)
    if args.roughness:
        roughness = image_array(args.roughness, size, "Non-Color")[..., :3].mean(axis=2)
    else:
        roughness = np.full((size, size), 0.45, dtype=np.float32)

    # Red body paint receives a glossy clear-coat look. Existing authored
    # roughness still distinguishes windows, tires, and painted details.
    red_body = (
        (base[..., 0] > 0.28)
        & (base[..., 0] > base[..., 1] * 1.35)
        & (base[..., 0] > base[..., 2] * 1.20)
    )
    used_uv = ~background
    smoothness = np.clip(1.0 - roughness * 0.86, 0.06, 0.90)
    smoothness = np.where(red_body & used_uv, np.maximum(smoothness, 0.72), smoothness)
    smoothness = np.where(used_uv, smoothness, 0.0)
    metallic = np.where(used_uv, np.clip(metallic_source, 0.0, 0.85), 0.0)

    packed = np.zeros((size, size, 4), dtype=np.float32)
    packed[..., 0] = metallic
    packed[..., 1] = ao
    packed[..., 2] = curvature
    packed[..., 3] = smoothness
    write_array(
        f"{args.prefix} Metallic Smoothness Mask",
        packed,
        os.path.join(args.output_dir, f"{args.prefix}-metallic-smoothness.png"),
        "Non-Color",
    )

    # Extract only the already-painted pale-blue front headlights. Restricting
    # this to the front region avoids turning windshield highlights into lamps.
    y, x = np.mgrid[0:size, 0:size]
    pale_blue = (
        (base[..., 2] > 0.38)
        & (base[..., 2] > base[..., 0] * 1.06)
        & (base[..., 1] > base[..., 0] * 0.92)
    )
    front_region = (
        (x > size * 0.10)
        & (x < size * 0.42)
        & (y < size * 0.19)
    )
    emission_mask = pale_blue & front_region & used_uv
    emission = np.zeros((size, size, 4), dtype=np.float32)
    emission[..., 0] = emission_mask.astype(np.float32) * 1.0
    emission[..., 1] = emission_mask.astype(np.float32) * 0.82
    emission[..., 2] = emission_mask.astype(np.float32) * 0.56
    emission[..., 3] = 1.0
    write_array(
        f"{args.prefix} Headlight Emission",
        emission,
        os.path.join(args.output_dir, f"{args.prefix}-emission.png"),
        "sRGB",
    )


def main() -> None:
    args = parse_args()
    os.makedirs(args.output_dir, exist_ok=True)
    bake_output_dir = args.bake_output_dir or args.output_dir
    os.makedirs(bake_output_dir, exist_ok=True)

    obj = bpy.data.objects.get(args.mesh)
    if obj is None or obj.type != "MESH":
        raise RuntimeError(f"Mesh '{args.mesh}' was not found in the opened Blender file.")
    if not obj.data.uv_layers:
        raise RuntimeError(f"Mesh '{args.mesh}' has no UV map.")

    for other in list(bpy.context.scene.objects):
        if other != obj:
            bpy.data.objects.remove(other, do_unlink=True)

    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = args.samples
    scene.render.bake.use_clear = True
    scene.render.bake.margin = 16
    scene.render.bake.margin_type = "EXTEND"
    scene.render.bake.target = "IMAGE_TEXTURES"

    ao_image = bake_map(
        obj,
        f"{args.prefix} AO Bake",
        os.path.join(bake_output_dir, f"{args.prefix}-ao.png"),
        args.size,
        "AO",
        False,
    )
    curvature_image = bake_map(
        obj,
        f"{args.prefix} Curvature Bake",
        os.path.join(bake_output_dir, f"{args.prefix}-curvature.png"),
        args.size,
        "EMIT",
        True,
    )
    create_stylized_maps(args, ao_image, curvature_image)
    print(
        f"STYLIZED_CAR_BAKE_COMPLETE mesh={obj.name} size={args.size} "
        f"output={args.output_dir} bake_output={bake_output_dir}"
    )


if __name__ == "__main__":
    main()
