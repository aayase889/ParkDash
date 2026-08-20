"""Render close-up previews of an opened Blender car with Unity-ready maps.

This script never saves the opened .blend file. It is intended as a visual
quality gate for the derived base-color, packed metallic/smoothness, and
emission textures produced by bake_stylized_car_textures.py.
"""

from __future__ import annotations

import argparse
import math
import os
import sys

import bpy
from mathutils import Vector


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    argv = argv[argv.index("--") + 1 :] if "--" in argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--mesh", required=True)
    parser.add_argument("--base-color", required=True)
    parser.add_argument("--packed-mask", required=True)
    parser.add_argument("--emission", required=True)
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--size", type=int, default=800)
    return parser.parse_args(argv)


def load_image(path: str, color_space: str) -> bpy.types.Image:
    image = bpy.data.images.load(os.path.abspath(path), check_existing=False)
    image.colorspace_settings.name = color_space
    return image


def point_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def make_preview_material(args: argparse.Namespace) -> bpy.types.Material:
    material = bpy.data.materials.new("Stylized Car Preview")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()

    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    base = nodes.new("ShaderNodeTexImage")
    packed = nodes.new("ShaderNodeTexImage")
    emission = nodes.new("ShaderNodeTexImage")
    separate = nodes.new("ShaderNodeSeparateColor")
    invert_smoothness = nodes.new("ShaderNodeMath")
    emission_tint = nodes.new("ShaderNodeMixRGB")

    base.image = load_image(args.base_color, "sRGB")
    packed.image = load_image(args.packed_mask, "Non-Color")
    emission.image = load_image(args.emission, "sRGB")
    invert_smoothness.operation = "SUBTRACT"
    invert_smoothness.inputs[0].default_value = 1.0
    emission_tint.blend_type = "MULTIPLY"
    emission_tint.inputs[0].default_value = 1.0
    emission_tint.inputs[2].default_value = (1.55, 1.24, 0.78, 1.0)

    links.new(base.outputs["Color"], shader.inputs["Base Color"])
    links.new(packed.outputs["Color"], separate.inputs["Color"])
    links.new(separate.outputs["Red"], shader.inputs["Metallic"])
    links.new(packed.outputs["Alpha"], invert_smoothness.inputs[1])
    links.new(invert_smoothness.outputs["Value"], shader.inputs["Roughness"])
    links.new(emission.outputs["Color"], emission_tint.inputs[1])

    emission_color_input = shader.inputs.get("Emission Color") or shader.inputs.get("Emission")
    emission_strength_input = shader.inputs.get("Emission Strength")
    if emission_color_input is not None:
        links.new(emission_tint.outputs["Color"], emission_color_input)
    if emission_strength_input is not None:
        emission_strength_input.default_value = 0.65

    coat_weight = shader.inputs.get("Coat Weight") or shader.inputs.get("Clearcoat")
    coat_roughness = shader.inputs.get("Coat Roughness") or shader.inputs.get("Clearcoat Roughness")
    if coat_weight is not None:
        coat_weight.default_value = 0.18
    if coat_roughness is not None:
        coat_roughness.default_value = 0.20

    links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return material


def make_flat_material(name: str, color: tuple[float, float, float], roughness: float) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Roughness"].default_value = roughness
    return material


def add_area_light(
    scene: bpy.types.Scene,
    name: str,
    center: Vector,
    location: Vector,
    energy: float,
    color: tuple[float, float, float],
    size: float,
) -> None:
    light_data = bpy.data.lights.new(name, "AREA")
    light_data.energy = energy
    light_data.color = color
    light_data.shape = "DISK"
    light_data.size = size
    light = bpy.data.objects.new(name, light_data)
    scene.collection.objects.link(light)
    light.location = location
    point_at(light, center)


def main() -> None:
    args = parse_args()
    os.makedirs(args.output_dir, exist_ok=True)

    car = bpy.data.objects.get(args.mesh)
    if car is None or car.type != "MESH":
        raise RuntimeError(f"Mesh '{args.mesh}' was not found in the opened Blender file.")

    for other in list(bpy.context.scene.objects):
        if other != car:
            bpy.data.objects.remove(other, do_unlink=True)

    car.data.materials.clear()
    car.data.materials.append(make_preview_material(args))

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = args.size
    scene.render.resolution_y = args.size
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    scene.view_settings.look = "AgX - Medium High Contrast"

    world = scene.world or bpy.data.worlds.new("Stylized Preview World")
    scene.world = world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.055, 0.075, 0.11, 1.0)
    background.inputs["Strength"].default_value = 0.68

    points = [car.matrix_world @ Vector(corner) for corner in car.bound_box]
    minimum = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    maximum = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    center = (minimum + maximum) * 0.5
    dimensions = maximum - minimum
    span = max(dimensions)

    bpy.ops.mesh.primitive_plane_add(
        size=span * 7.0,
        location=(center.x, center.y, minimum.z - span * 0.018),
    )
    ground = bpy.context.object
    ground.name = "Stylized Preview Ground"
    ground.data.materials.append(make_flat_material("Preview Asphalt", (0.09, 0.12, 0.17), 0.88))

    add_area_light(
        scene,
        "Soft Key",
        center,
        center + Vector((-span * 1.8, -span * 1.4, span * 2.5)),
        760.0,
        (1.0, 0.80, 0.68),
        span * 2.1,
    )
    add_area_light(
        scene,
        "Soft Fill",
        center,
        center + Vector((span * 1.4, span * 0.3, span * 1.6)),
        360.0,
        (0.54, 0.70, 1.0),
        span * 2.5,
    )
    add_area_light(
        scene,
        "Soft Rim",
        center,
        center + Vector((span * 1.1, span * 1.8, span * 2.0)),
        520.0,
        (1.0, 0.46, 0.34),
        span * 1.7,
    )

    camera_data = bpy.data.cameras.new("Stylized Preview Camera")
    camera = bpy.data.objects.new("Stylized Preview Camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera.data.type = "ORTHO"
    camera.data.lens = 58

    distance = span * 3.2
    views = {
        # The exact red-car source faces local -X.
        "front_three_quarter": center + Vector((-distance, -distance * 0.72, distance * 0.72)),
        "front": center + Vector((-distance, 0.0, distance * 0.20)),
        "top_game": center + Vector((-distance * 0.52, 0.0, distance)),
    }

    for name, location in views.items():
        camera.location = location
        point_at(camera, center + Vector((0.0, 0.0, dimensions.z * 0.03)))
        camera.data.ortho_scale = span * (1.26 if name == "front" else 1.42)
        scene.render.filepath = os.path.abspath(os.path.join(args.output_dir, f"{name}.png"))
        bpy.ops.render.render(write_still=True)
        print(f"Rendered stylized preview: {scene.render.filepath}")


if __name__ == "__main__":
    main()
