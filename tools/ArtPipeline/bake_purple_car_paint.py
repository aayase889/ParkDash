"""Bake the purple car's Blender shader to a single Unity-ready paint atlas.

This is intentionally non-destructive: it creates temporary image nodes in
memory and never saves the source .blend file.
"""

from __future__ import annotations

import os
import sys

import bpy
import numpy as np


def main() -> None:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    if len(argv) != 2:
        raise RuntimeError("Usage: -- <mesh-name> <output-png>")

    mesh_name, output_path = argv
    obj = bpy.data.objects.get(mesh_name)
    if obj is None or obj.type != "MESH":
        raise RuntimeError(f"Could not find mesh '{mesh_name}'.")
    if not obj.data.uv_layers:
        raise RuntimeError(f"{mesh_name} has no UVs to bake into.")

    view_layer = bpy.context.view_layer
    for scene_object in view_layer.objects:
        scene_object.select_set(False)
    obj.select_set(True)
    view_layer.objects.active = obj
    if obj.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")

    # Unity imports the active UV layer, so bake into precisely that atlas.
    obj.data.uv_layers.active_index = obj.data.uv_layers.active_index
    size = 1024
    image = bpy.data.images.new("PurpleCar_Painted", size, size, alpha=True)
    image.colorspace_settings.name = "sRGB"

    # Cycles requires a selected Image Texture bake target in every material
    # slot on the mesh, including the glass, tires, and headlights.  Replace
    # their output temporarily with emission so we bake unlit painted color.
    for material in obj.data.materials:
        if material is None:
            continue
        material.use_nodes = True
        nodes = material.node_tree.nodes
        for node in nodes:
            node.select = False
        target = nodes.new("ShaderNodeTexImage")
        target.name = "Unity Purple Paint Bake Target"
        target.label = "Unity Purple Paint Bake Target"
        target.image = image
        target.select = True
        nodes.active = target

        output = next((node for node in nodes if node.bl_idname == "ShaderNodeOutputMaterial"), None)
        if output is None:
            continue
        for link in list(material.node_tree.links):
            if link.to_node == output and link.to_socket.name == "Surface":
                material.node_tree.links.remove(link)
        emission = nodes.new("ShaderNodeEmission")
        if material.name == "Purple Coated Glitter":
            purple_group = next((node for node in nodes if "Color Bake" in node.outputs), None)
            if purple_group is None:
                raise RuntimeError("Purple Coated Glitter is missing its Color Bake shader output.")
            material.node_tree.links.new(purple_group.outputs["Color Bake"], emission.inputs["Color"])
        elif material.name == "Black Car Paint":
            emission.inputs["Color"].default_value = (0.018, 0.028, 0.070, 1.0)
        elif material.name == "White Car Paint":
            emission.inputs["Color"].default_value = (0.90, 0.96, 1.0, 1.0)
        else:
            emission.inputs["Color"].default_value = (0.24, 0.78, 0.88, 1.0)
        material.node_tree.links.new(emission.outputs["Emission"], output.inputs["Surface"])

    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 16
    scene.render.bake.target = "IMAGE_TEXTURES"
    scene.render.bake.use_clear = True
    scene.render.bake.margin = 16
    scene.render.bake.margin_type = "EXTEND"
    bpy.ops.object.bake(type="EMIT")

    pixels = np.asarray(image.pixels[:], dtype=np.float32).reshape((-1, 4))
    print(f"PURPLE_CAR_PAINT_RANGE min={pixels.min(axis=0)} max={pixels.max(axis=0)}")

    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    image.filepath_raw = output_path
    image.file_format = "PNG"
    image.save()
    print(f"PURPLE_CAR_PAINT_BAKED mesh={obj.name} uv={obj.data.uv_layers.active.name} output={output_path}")


if __name__ == "__main__":
    main()
