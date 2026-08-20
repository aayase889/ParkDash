import bpy
import math
import sys
from pathlib import Path
from mathutils import Vector


args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
output_dir = Path(args[0]) if args else Path("textured_previews")
output_dir.mkdir(parents=True, exist_ok=True)

target_names = (
    {name.strip() for name in args[1].split(",") if name.strip()}
    if len(args) > 1
    else {"car", "window", "window.sides", "tires", "light"}
)
targets = [bpy.data.objects[name] for name in target_names if name in bpy.data.objects]
if len(targets) != len(target_names):
    raise RuntimeError(f"Missing target objects: {sorted(target_names - set(obj.name for obj in targets))}")

for obj in bpy.context.scene.objects:
    if obj.type == "MESH" and obj.name not in target_names:
        obj.hide_render = True

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 800
scene.render.resolution_y = 800
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.render.image_settings.color_mode = "RGBA"
scene.render.image_settings.color_depth = "8"
scene.view_settings.look = "AgX - Medium High Contrast"

world = scene.world or bpy.data.worlds.new("HandpaintPreviewWorld")
scene.world = world
world.use_nodes = True
world_nodes = world.node_tree.nodes
background = world_nodes.get("Background")
background.inputs["Color"].default_value = (0.035, 0.050, 0.085, 1.0)
background.inputs["Strength"].default_value = 0.45

world_points = [obj.matrix_world @ Vector(corner) for obj in targets for corner in obj.bound_box]
minimum = Vector(tuple(min(point[axis] for point in world_points) for axis in range(3)))
maximum = Vector(tuple(max(point[axis] for point in world_points) for axis in range(3)))
center = (minimum + maximum) * 0.5
size = maximum - minimum


def make_material(name, color, roughness=0.82):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    principled = material.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = (*color, 1.0)
    principled.inputs["Roughness"].default_value = roughness
    return material


bpy.ops.mesh.primitive_plane_add(size=max(size) * 7.0, location=(center.x, center.y, minimum.z - 0.015))
ground = bpy.context.object
ground.name = "PreviewGround"
ground.data.materials.append(make_material("PreviewGroundMaterial", (0.10, 0.13, 0.18), 0.90))


def point_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def area_light(name, location, energy, color, area_size):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.color = color
    data.shape = "DISK"
    data.size = area_size
    obj = bpy.data.objects.new(name, data)
    scene.collection.objects.link(obj)
    obj.location = location
    point_at(obj, center)
    return obj


span = max(size)
area_light("KeyLight", center + Vector((span * 1.7, -span * 2.0, span * 2.4)), 650.0, (1.0, 0.72, 0.64), span * 1.9)
area_light("FillLight", center + Vector((-span * 2.0, -span * 0.7, span * 1.3)), 430.0, (0.52, 0.70, 1.0), span * 2.3)
area_light("RimLight", center + Vector((span * 0.8, span * 2.0, span * 2.0)), 720.0, (1.0, 0.35, 0.48), span * 1.7)

camera_data = bpy.data.cameras.new("HandpaintPreviewCamera")
camera = bpy.data.objects.new("HandpaintPreviewCamera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera
camera.data.type = "ORTHO"
camera.data.lens = 55
camera.data.ortho_scale = max(size.x, size.y, size.z) * 1.30

distance = span * 3.4
views = {
    "front_three_quarter": center + Vector((distance * 0.72, -distance, distance * 0.58)),
    "front": center + Vector((0.0, -distance, distance * 0.13)),
    "side": center + Vector((distance, 0.0, distance * 0.10)),
    "rear_three_quarter": center + Vector((-distance * 0.72, distance, distance * 0.55)),
    "top": center + Vector((0.0, 0.0, distance)),
}

for name, location in views.items():
    camera.location = location
    point_at(camera, center + Vector((0.0, 0.0, size.z * 0.02)))
    camera.data.ortho_scale = span * (1.30 if name != "side" else 1.24)
    scene.render.filepath = str(output_dir / f"{name}.png")
    bpy.ops.render.render(write_still=True)
    print(f"Rendered textured preview: {scene.render.filepath}")
