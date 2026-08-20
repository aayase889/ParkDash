import bpy
import math
import sys
from pathlib import Path
from mathutils import Vector


args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
output_dir = Path(args[0]) if args else Path("preview")
output_dir.mkdir(parents=True, exist_ok=True)

target_names = {"car.001", "window.001", "window_sides", "tires.001"}
targets = [bpy.data.objects[name] for name in target_names]
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 800
scene.render.resolution_y = 800
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.view_settings.look = "AgX - Medium High Contrast"

world = scene.world or bpy.data.worlds.new("Car44PreviewWorld")
scene.world = world
world.use_nodes = True
background = world.node_tree.nodes.get("Background")
background.inputs["Color"].default_value = (0.035, 0.050, 0.085, 1.0)
background.inputs["Strength"].default_value = 0.45

points = [obj.matrix_world @ Vector(corner) for obj in targets for corner in obj.bound_box]
minimum = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
maximum = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
center = (minimum + maximum) * 0.5
size = maximum - minimum
span = max(size)


def point_at(obj, target, up="Y"):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", up).to_euler()


def simple_material(name, color, roughness):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Roughness"].default_value = roughness
    return material


# Model is intentionally Y-up; make an XZ ground plane without rotating the car.
bpy.ops.mesh.primitive_plane_add(size=span * 7.0, location=(center.x, minimum.y - 0.018, center.z), rotation=(math.pi / 2.0, 0.0, 0.0))
ground = bpy.context.object
ground.name = "PreviewGround_YUp"
ground.data.materials.append(simple_material("PreviewGroundMaterial_YUp", (0.10, 0.13, 0.18), 0.90))


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


area_light("KeyLight", center + Vector((-span * 1.8, span * 2.0, -span * 1.5)), 700.0, (1.0, 0.70, 0.62), span * 1.9)
area_light("FillLight", center + Vector((-span * 0.4, span * 1.0, span * 2.1)), 450.0, (0.50, 0.70, 1.0), span * 2.3)
area_light("RimLight", center + Vector((span * 1.8, span * 2.0, span * 1.3)), 760.0, (1.0, 0.34, 0.48), span * 1.7)

camera_data = bpy.data.cameras.new("Car44PreviewCamera")
camera = bpy.data.objects.new("Car44PreviewCamera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera
camera.data.type = "ORTHO"
camera.data.lens = 55

distance = span * 3.4
views = {
    "front_three_quarter": center + Vector((-distance, distance * 0.55, -distance * 0.72)),
    "front": center + Vector((-distance, distance * 0.10, 0.0)),
    "side": center + Vector((0.0, distance * 0.10, -distance)),
    "rear_three_quarter": center + Vector((distance, distance * 0.52, distance * 0.72)),
    "top": center + Vector((-distance * 0.02, distance, distance * 0.02)),
}

for name, location in views.items():
    camera.location = location
    point_at(camera, center + Vector((0.0, size.y * 0.03, 0.0)), up="Y" if name != "top" else "Z")
    camera.data.ortho_scale = span * (1.34 if name != "side" else 1.26)
    scene.render.filepath = str(output_dir / f"{name}.png")
    bpy.ops.render.render(write_still=True)
    print(f"Rendered textured Y-up preview: {scene.render.filepath}")
