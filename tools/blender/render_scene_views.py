import bpy
import math
import sys
from pathlib import Path
from mathutils import Vector


args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
output_dir = Path(args[0]) if args else Path("renders")
output_dir.mkdir(parents=True, exist_ok=True)

scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.render.resolution_x = 700
scene.render.resolution_y = 700
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = True
scene.display.shading.light = "STUDIO"
scene.display.shading.studio_light = "rim.sl"
scene.display.shading.color_type = "MATERIAL"
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.display.shading.cavity_type = "BOTH"
scene.display.shading.curvature_ridge_factor = 1.5
scene.display.shading.curvature_valley_factor = 1.0
scene.display.shading.show_specular_highlight = True
scene.view_settings.look = "AgX - Medium High Contrast"

mesh_objects = [obj for obj in scene.objects if obj.type == "MESH" and not obj.hide_render]
world_points = [obj.matrix_world @ Vector(corner) for obj in mesh_objects for corner in obj.bound_box]
minimum = Vector((min(point.x for point in world_points), min(point.y for point in world_points), min(point.z for point in world_points)))
maximum = Vector((max(point.x for point in world_points), max(point.y for point in world_points), max(point.z for point in world_points)))
center = (minimum + maximum) * 0.5
size = maximum - minimum

camera_data = bpy.data.cameras.new("InspectionCamera")
camera = bpy.data.objects.new("InspectionCamera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera
camera.data.type = "ORTHO"
camera.data.lens = 55


def aim_camera(location, target):
    camera.location = Vector(location)
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()


distance = max(size) * 3.2
views = {
    "front_three_quarter": (center.x + distance * 0.70, center.y - distance, center.z + distance * 0.55),
    "front": (center.x, center.y - distance, center.z + distance * 0.10),
    "side": (center.x + distance, center.y, center.z + distance * 0.08),
    "rear_three_quarter": (center.x - distance * 0.70, center.y + distance, center.z + distance * 0.50),
    "top": (center.x, center.y, center.z + distance),
}

for name, location in views.items():
    camera.data.ortho_scale = max(size.x, size.y, size.z) * (1.28 if name != "side" else 1.22)
    aim_camera(location, center + Vector((0.0, 0.0, size.z * 0.02)))
    scene.render.filepath = str(output_dir / f"{name}.png")
    bpy.ops.render.render(write_still=True)
    print(f"Rendered {scene.render.filepath}")
