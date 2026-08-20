import bpy
import math
import sys
from pathlib import Path


OUTPUT_PATH = Path(sys.argv[sys.argv.index("--") + 1]) if "--" in sys.argv else Path("rounded_compact_variant.blend")


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in list(bpy.data.collections):
        if collection.name != bpy.context.scene.collection.name:
            bpy.data.collections.remove(collection)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.cameras, bpy.data.lights, bpy.data.materials):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def material(name, color, metallic=0.0, roughness=0.55):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    principled = mat.node_tree.nodes.get("Principled BSDF")
    if principled:
        principled.inputs["Base Color"].default_value = (*color, 1.0)
        principled.inputs["Metallic"].default_value = metallic
        principled.inputs["Roughness"].default_value = roughness
    return mat


def link_object(obj, collection):
    for current_collection in list(obj.users_collection):
        current_collection.objects.unlink(obj)
    collection.objects.link(obj)


def validate_mesh(obj):
    if obj.type != "MESH":
        return
    obj.data.validate(verbose=True)
    obj.data.update()


def rounded_box(name, location, dimensions, bevel_width, bevel_segments, mat, collection):
    bpy.ops.mesh.primitive_cube_add(location=location, calc_uvs=False)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bevel = obj.modifiers.new("LowPolyBevel", "BEVEL")
    bevel.width = bevel_width
    bevel.segments = bevel_segments
    bevel.limit_method = "ANGLE"
    bevel.harden_normals = True
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    obj.data.materials.append(mat)
    for polygon in obj.data.polygons:
        polygon.use_smooth = polygon.area < 0.20
    link_object(obj, collection)
    validate_mesh(obj)
    return obj


def make_loft(name, sections, mat, collection):
    vertices = []
    faces = []
    ring_size = 8

    for y, half_width, bottom, top in sections:
        height = top - bottom
        ring = [
            (-half_width * 0.78, y, bottom),
            (-half_width, y, bottom + height * 0.18),
            (-half_width, y, bottom + height * 0.66),
            (-half_width * 0.72, y, top),
            (half_width * 0.72, y, top),
            (half_width, y, bottom + height * 0.66),
            (half_width, y, bottom + height * 0.18),
            (half_width * 0.78, y, bottom),
        ]
        vertices.extend(ring)

    for section_index in range(len(sections) - 1):
        start = section_index * ring_size
        next_start = (section_index + 1) * ring_size
        for ring_index in range(ring_size):
            next_ring_index = (ring_index + 1) % ring_size
            faces.append((start + ring_index, start + next_ring_index, next_start + next_ring_index, next_start + ring_index))

    faces.append(tuple(reversed(range(ring_size))))
    final_start = (len(sections) - 1) * ring_size
    faces.append(tuple(final_start + index for index in range(ring_size)))

    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    for polygon in mesh.polygons:
        polygon.use_smooth = len(polygon.vertices) == 4
    validate_mesh(obj)
    return obj


def cabin_half_width(y):
    points = [(-0.20, 0.86), (0.05, 0.93), (0.55, 0.96), (1.25, 0.92), (1.48, 0.82)]
    if y <= points[0][0]:
        return points[0][1]
    if y >= points[-1][0]:
        return points[-1][1]
    for (y0, width0), (y1, width1) in zip(points, points[1:]):
        if y0 <= y <= y1:
            factor = (y - y0) / (y1 - y0)
            return width0 + (width1 - width0) * factor
    return points[-1][1]


def cabin_dimensions_at(y):
    points = [
        (-0.20, 0.86, 0.35, 1.08),
        (0.05, 0.93, 0.35, 1.20),
        (0.55, 0.96, 0.36, 1.27),
        (1.25, 0.92, 0.36, 1.20),
        (1.48, 0.82, 0.37, 1.05),
    ]
    if y <= points[0][0]:
        return points[0][1:]
    if y >= points[-1][0]:
        return points[-1][1:]
    for a, b in zip(points, points[1:]):
        if a[0] <= y <= b[0]:
            factor = (y - a[0]) / (b[0] - a[0])
            return tuple(a[index] + (b[index] - a[index]) * factor for index in range(1, 4))
    return points[-1][1:]


def cabin_surface_half_width(y, z):
    width, bottom, top = cabin_dimensions_at(y)
    height = max(top - bottom, 0.001)
    relative_height = (z - bottom) / height
    if relative_height <= 0.18:
        factor = max(0.0, relative_height / 0.18)
        return width * (0.78 + 0.22 * factor)
    if relative_height <= 0.66:
        return width
    factor = min(1.0, (relative_height - 0.66) / 0.34)
    return width * (1.0 - 0.28 * factor)


def solid_panel(name, front_vertices, extrusion, mat, collection, bevel_width=0.012):
    count = len(front_vertices)
    back_vertices = [
        (vertex[0] + extrusion[0], vertex[1] + extrusion[1], vertex[2] + extrusion[2])
        for vertex in front_vertices
    ]
    vertices = list(front_vertices) + back_vertices
    faces = [tuple(range(count)), tuple(reversed(range(count, count * 2)))]
    for index in range(count):
        next_index = (index + 1) % count
        faces.append((index, next_index, count + next_index, count + index))

    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    validate_mesh(obj)

    if bevel_width > 0:
        bevel = obj.modifiers.new("PanelEdgeBevel", "BEVEL")
        bevel.width = bevel_width
        bevel.segments = 1
        bevel.limit_method = "ANGLE"
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=bevel.name)
        validate_mesh(obj)
    return obj


def append_rounded_cylinder(vertices, faces, material_indices, axis, radius, depth, segments, center, material_index, rounded=False):
    start = len(vertices)
    if rounded:
        positions = (-depth * 0.5, -depth * 0.36, depth * 0.36, depth * 0.5)
        radii = (radius * 0.84, radius, radius, radius * 0.84)
    else:
        positions = (-depth * 0.5, depth * 0.5)
        radii = (radius, radius)

    for position, ring_radius in zip(positions, radii):
        for index in range(segments):
            angle = math.tau * index / segments
            c = math.cos(angle) * ring_radius
            s = math.sin(angle) * ring_radius
            if axis == "X":
                vertices.append((center[0] + position, center[1] + c, center[2] + s))
            elif axis == "Y":
                vertices.append((center[0] + c, center[1] + position, center[2] + s))
            else:
                vertices.append((center[0] + c, center[1] + s, center[2] + position))

    ring_count = len(positions)
    for ring in range(ring_count - 1):
        a_start = start + ring * segments
        b_start = start + (ring + 1) * segments
        for index in range(segments):
            next_index = (index + 1) % segments
            faces.append((a_start + index, a_start + next_index, b_start + next_index, b_start + index))
            material_indices.append(material_index)

    faces.append(tuple(reversed(tuple(start + index for index in range(segments)))))
    material_indices.append(material_index)
    last_start = start + (ring_count - 1) * segments
    faces.append(tuple(last_start + index for index in range(segments)))
    material_indices.append(material_index)


def wheel(name, location, tire_mat, rim_mat, collection):
    vertices = []
    faces = []
    material_indices = []
    append_rounded_cylinder(vertices, faces, material_indices, "X", 0.41, 0.28, 14, (0.0, 0.0, 0.0), 0, rounded=True)
    append_rounded_cylinder(vertices, faces, material_indices, "X", 0.22, 0.305, 12, (0.0, 0.0, 0.0), 1, rounded=False)
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(tire_mat)
    mesh.materials.append(rim_mat)
    for polygon, material_index in zip(mesh.polygons, material_indices):
        polygon.material_index = material_index
        polygon.use_smooth = len(polygon.vertices) == 4
    obj = bpy.data.objects.new(name, mesh)
    obj.location = location
    collection.objects.link(obj)
    validate_mesh(obj)
    return obj


def cylinder_object(name, location, axis, radius, depth, segments, mats, component_specs, collection):
    vertices = []
    faces = []
    material_indices = []
    for component in component_specs:
        append_rounded_cylinder(
            vertices,
            faces,
            material_indices,
            axis,
            component["radius"],
            component["depth"],
            segments,
            component.get("center", (0.0, 0.0, 0.0)),
            component["material_index"],
            rounded=component.get("rounded", False),
        )
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    for mat in mats:
        mesh.materials.append(mat)
    for polygon, material_index in zip(mesh.polygons, material_indices):
        polygon.material_index = material_index
        polygon.use_smooth = len(polygon.vertices) == 4
    obj = bpy.data.objects.new(name, mesh)
    obj.location = location
    collection.objects.link(obj)
    validate_mesh(obj)
    return obj


def fender_arch(name, x_center, wheel_y, wheel_z, outward_sign, mat, collection):
    segments = 8
    outer_radius = 0.485
    inner_radius = 0.430
    half_depth = 0.030
    x_values = (x_center - half_depth, x_center + half_depth)
    vertices = []
    for x in x_values:
        for radius in (outer_radius, inner_radius):
            for index in range(segments + 1):
                angle = math.pi * index / segments
                vertices.append((x, wheel_y + math.cos(angle) * radius, wheel_z + math.sin(angle) * radius))

    stride = segments + 1
    faces = []
    for side in range(2):
        base = side * stride * 2
        for index in range(segments):
            faces.append((base + index, base + index + 1, base + stride + index + 1, base + stride + index))
    for radius_index in range(2):
        base_a = radius_index * stride
        base_b = stride * 2 + radius_index * stride
        for index in range(segments):
            faces.append((base_a + index, base_b + index, base_b + index + 1, base_a + index + 1))
    faces.append((0, stride, stride * 3, stride * 2))
    faces.append((segments, stride + segments, stride * 3 + segments, stride * 2 + segments))

    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(mat)
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    validate_mesh(obj)
    return obj


def assign_parent(objects, parent):
    for obj in objects:
        obj.parent = parent


clear_scene()
scene = bpy.context.scene
scene.name = "RoundedCompactCar"
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0
scene.render.engine = "BLENDER_WORKBENCH"
scene.render.resolution_x = 700
scene.render.resolution_y = 700
scene.render.resolution_percentage = 100

car_collection = bpy.data.collections.new("CarVariant_RoundedCompact")
scene.collection.children.link(car_collection)

body_mat = material("MAT_BodyPaint", (0.10, 0.52, 0.50), metallic=0.0, roughness=0.48)
glass_mat = material("MAT_GlassPlaceholder", (0.035, 0.08, 0.12), metallic=0.0, roughness=0.25)
tire_mat = material("MAT_Tire", (0.025, 0.03, 0.04), metallic=0.0, roughness=0.82)
rim_mat = material("MAT_WheelHub", (0.58, 0.62, 0.64), metallic=0.15, roughness=0.38)
lamp_mat = material("MAT_Headlight", (1.0, 0.72, 0.22), metallic=0.0, roughness=0.32)
tail_mat = material("MAT_TailLight", (0.72, 0.055, 0.025), metallic=0.0, roughness=0.38)
bumper_mat = material("MAT_Bumper", (0.90, 0.48, 0.15), metallic=0.0, roughness=0.52)
dark_mat = material("MAT_Grille", (0.045, 0.055, 0.065), metallic=0.0, roughness=0.72)

root = bpy.data.objects.new("CarVariant_RoundedCompact_ROOT", None)
root.empty_display_type = "CUBE"
root.empty_display_size = 0.22
root["forward_axis"] = "-Y"
root["up_axis"] = "+Z"
root["design_note"] = "Rounded compact variation preserving the source car scale, wheelbase, and chunky style."
root["uv_unwrapped"] = False
car_collection.objects.link(root)

parts = []
parts.append(rounded_box("Body_Lower", (0.0, 0.28, 0.02), (1.98, 2.68, 0.78), 0.14, 2, body_mat, car_collection))

cabin_sections = [
    (-0.20, 0.86, 0.35, 1.08),
    (0.05, 0.93, 0.35, 1.20),
    (0.55, 0.96, 0.36, 1.27),
    (1.25, 0.92, 0.36, 1.20),
    (1.48, 0.82, 0.37, 1.05),
]
parts.append(make_loft("Body_Cabin", cabin_sections, body_mat, car_collection))

# Front and rear glass sit just outside the cabin caps, with no UVs or textures.
front_glass = [
    (-0.68, -0.214, 0.48),
    (0.68, -0.214, 0.48),
    (0.75, -0.214, 0.58),
    (0.61, -0.214, 1.035),
    (-0.61, -0.214, 1.035),
    (-0.75, -0.214, 0.58),
]
parts.append(solid_panel("Window_Front", front_glass, (0.0, -0.028, 0.0), glass_mat, car_collection))

rear_glass = [
    (-0.61, 1.494, 0.49),
    (-0.55, 1.494, 0.96),
    (-0.46, 1.494, 1.015),
    (0.46, 1.494, 1.015),
    (0.55, 1.494, 0.96),
    (0.61, 1.494, 0.49),
]
parts.append(solid_panel("Window_Rear", rear_glass, (0.0, 0.028, 0.0), glass_mat, car_collection))

side_window_profiles = {
    "Front": [(-0.06, 0.48), (0.53, 0.48), (0.58, 0.55), (0.51, 0.90), (0.04, 0.90), (-0.06, 0.82)],
    "Rear": [(0.69, 0.48), (1.28, 0.48), (1.34, 0.55), (1.27, 0.88), (0.75, 0.91), (0.68, 0.84)],
}
for side_name, sign in (("L", -1.0), ("R", 1.0)):
    for window_name, profile in side_window_profiles.items():
        window_vertices = [
            (sign * (cabin_half_width(y) + 0.014), y, z)
            for y, z in profile
        ]
        parts.append(
            solid_panel(
                f"Window_Side_{window_name}_{side_name}",
                window_vertices,
                (sign * 0.026, 0.0, 0.0),
                glass_mat,
                car_collection,
            )
        )

wheel_locations = {
    "Wheel_FL": (-1.02, -0.34, -0.28),
    "Wheel_FR": (1.02, -0.34, -0.28),
    "Wheel_RL": (-1.02, 1.02, -0.28),
    "Wheel_RR": (1.02, 1.02, -0.28),
}
for wheel_name, location in wheel_locations.items():
    parts.append(wheel(wheel_name, location, tire_mat, rim_mat, car_collection))

for x_sign, side_name in ((-1.0, "L"), (1.0, "R")):
    arch_x = x_sign * 1.005
    parts.append(fender_arch(f"Fender_Front_{side_name}", arch_x, -0.34, -0.28, x_sign, body_mat, car_collection))
    parts.append(fender_arch(f"Fender_Rear_{side_name}", arch_x, 1.02, -0.28, x_sign, body_mat, car_collection))

for x, side_name in ((-0.67, "L"), (0.67, "R")):
    parts.append(
        cylinder_object(
            f"Headlight_{side_name}",
            (x, -1.115, 0.19),
            "Y",
            0.20,
            0.10,
            12,
            [body_mat, lamp_mat],
            [
                {"radius": 0.225, "depth": 0.075, "material_index": 0},
                {"radius": 0.175, "depth": 0.112, "material_index": 1, "center": (0.0, -0.020, 0.0)},
            ],
            car_collection,
        )
    )
    parts.append(
        cylinder_object(
            f"TailLight_{side_name}",
            (x, 1.636, 0.12),
            "Y",
            0.14,
            0.075,
            10,
            [tail_mat],
            [{"radius": 0.14, "depth": 0.075, "material_index": 0}],
            car_collection,
        )
    )

parts.append(rounded_box("Bumper_Front", (0.0, -1.135, -0.17), (1.84, 0.18, 0.18), 0.07, 1, bumper_mat, car_collection))
parts.append(rounded_box("Bumper_Rear", (0.0, 1.645, -0.16), (1.76, 0.16, 0.17), 0.065, 1, bumper_mat, car_collection))
parts.append(rounded_box("Grille_Front", (0.0, -1.073, -0.005), (0.88, 0.035, 0.17), 0.045, 1, dark_mat, car_collection))

# Small mirrors add the attached-reference character without changing the main silhouette.
for x, side_name in ((-1.035, "L"), (1.035, "R")):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=0.15, location=(x, -0.02, 0.73))
    mirror = bpy.context.object
    mirror.name = f"Mirror_{side_name}"
    mirror.scale = (0.72, 0.90, 0.78)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    mirror.data.materials.append(body_mat)
    for polygon in mirror.data.polygons:
        polygon.use_smooth = True
    link_object(mirror, car_collection)
    validate_mesh(mirror)
    parts.append(mirror)

assign_parent(parts, root)

# Keep all object transforms clean and avoid accidental animation/import data.
for obj in parts:
    obj.rotation_euler = (0.0, 0.0, 0.0)
    obj.scale = (1.0, 1.0, 1.0)
    obj.animation_data_clear()
    if obj.type == "MESH":
        for uv_layer in list(obj.data.uv_layers):
            obj.data.uv_layers.remove(uv_layer)

notes = bpy.data.texts.new("README_CarVariant")
notes.write(
    "Rounded Compact Car Variant\n"
    "================================\n"
    "Designed as a restrained variation of carmatchcar3.\n"
    "Preserves the source car's overall scale, wheelbase, wheel size, chunky proportions, -Y forward axis, and +Z up axis.\n"
    "Geometry is intentionally low-poly for a mobile Unity game.\n"
    "Wheels are separate objects with origins at their centers.\n"
    "No texture images and no UV maps are included. Materials are simple viewport placeholders only.\n"
)

scene["source_file"] = "carmatchcar3 copy.blend"
scene["target_platform"] = "Mobile / Unity"
scene["variation_style"] = "Rounded compact, close to original"

OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT_PATH), compress=True)
print(f"Saved rounded compact car variant to: {OUTPUT_PATH}")
