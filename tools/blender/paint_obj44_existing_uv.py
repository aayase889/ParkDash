import bpy
import hashlib
import math
import os
import struct
import sys
from array import array
from pathlib import Path
from mathutils import Vector


args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
OUTPUT_BLEND = Path(args[0])
OUTPUT_OBJ = Path(args[1])
TEXTURE_DIR = Path(args[2])
SIZE = 512


def clamp(value, low=0.0, high=1.0):
    return max(low, min(high, value))


def mix(a, b, amount):
    amount = clamp(amount)
    return tuple(a[i] * (1.0 - amount) + b[i] * amount for i in range(3))


def scale_color(color, amount):
    return tuple(clamp(channel * amount) for channel in color)


def fingerprint(mesh):
    layer = mesh.uv_layers.active
    if not layer:
        return None
    digest = hashlib.sha256()
    for item in layer.data:
        digest.update(struct.pack("<2d", float(item.uv.x), float(item.uv.y)))
    return digest.hexdigest()


def bounds(obj):
    coords = [vertex.co for vertex in obj.data.vertices]
    minimum = Vector(tuple(min(co[axis] for co in coords) for axis in range(3)))
    maximum = Vector(tuple(max(co[axis] for co in coords) for axis in range(3)))
    return minimum, maximum


def normalized(position, object_bounds):
    minimum, maximum = object_bounds
    return tuple(
        (position[axis] - minimum[axis]) / max(maximum[axis] - minimum[axis], 1e-6)
        for axis in range(3)
    )


def noise(position):
    return (
        math.sin(position.x * 5.7 + position.y * 8.1 - position.z * 3.2) * 0.55
        + math.sin(position.x * 12.9 - position.y * 2.4 + position.z * 6.7) * 0.24
    )


def body_painter(position, normal, uv, object_bounds):
    nx, ny, nz = normalized(position, object_bounds)
    width_center = nz * 2.0 - 1.0
    base = (0.72, 0.018, 0.030)
    value = 0.70 + ny * 0.28 + max(normal.y, 0.0) * 0.12
    value -= abs(width_center) ** 1.45 * 0.09
    value -= max(0.0, 0.22 - ny) * 0.45
    value += noise(position) * 0.017
    color = scale_color(base, value)

    center_highlight = math.exp(-((width_center + 0.18) ** 2) / 0.075)
    roof_hood = clamp((normal.y - 0.05) / 0.82) * (0.18 + ny * 0.30)
    length_softness = 0.62 + 0.38 * math.exp(-((nx - 0.42) ** 2) / 0.30)
    color = mix(color, (1.0, 0.16, 0.30), center_highlight * roof_hood * length_softness)

    # The front of this model is the negative-X end. Paint reference-like headlights.
    front_surface = nx < 0.20 and normal.x < -0.14
    light_shape = ((position.y - 0.83) / 0.235) ** 2 + ((abs(position.z) - 0.66) / 0.265) ** 2
    if front_surface and light_shape <= 1.0:
        rim = clamp(light_shape)
        light_color = mix((0.86, 0.95, 1.0), (0.24, 0.46, 0.68), rim * 0.78)
        glint = ((position.y - 0.93) / 0.065) ** 2 + ((abs(position.z) - 0.59) / 0.070) ** 2
        if glint <= 1.0:
            light_color = (1.0, 1.0, 0.96)
        return light_color

    # Painted lower grille/bumper accent for readability at mobile scale.
    if front_surface and 0.45 < position.y < 0.60 and abs(position.z) < 0.48:
        return mix((0.16, 0.21, 0.34), (0.48, 0.60, 0.76), clamp((position.y - 0.45) / 0.15) * 0.55)
    if front_surface:
        color = mix(color, (1.0, 0.075, 0.045), math.exp(-(width_center**2) / 0.65) * 0.11)
    return color


def window_painter(position, normal, uv, object_bounds):
    color = (0.94, 0.92, 0.84)
    lateral = abs(position.z)
    eye = ((lateral - 0.215) / 0.155) ** 2 + ((position.y - 1.405) / 0.215) ** 2
    pupil = ((lateral - 0.225) / 0.090) ** 2 + ((position.y - 1.385) / 0.160) ** 2
    if eye <= 1.0:
        color = (0.985, 0.97, 0.91)
    if pupil <= 1.0:
        color = (0.052, 0.032, 0.028)
    catchlight = ((lateral - 0.185) / 0.036) ** 2 + ((position.y - 1.485) / 0.050) ** 2
    if catchlight <= 1.0:
        color = (1.0, 1.0, 0.985)
    edge_tint = clamp((lateral - 0.48) / 0.16) * 0.08
    return mix(color, (0.56, 0.76, 0.90), edge_tint)


def frame_painter(position, normal, uv, object_bounds):
    _, ny, _ = normalized(position, object_bounds)
    color = scale_color((0.37, 0.006, 0.014), 0.72 + ny * 0.30 + max(normal.y, 0.0) * 0.10)
    return mix(color, (0.82, 0.022, 0.036), max(normal.y, 0.0) * 0.10)


def wheel_painter(position, normal, uv, object_bounds):
    wheel_x = -0.12793 if abs(position.x + 0.12793) < abs(position.x - 1.26149) else 1.26149
    radius = math.sqrt((position.x - wheel_x) ** 2 + (position.y - 0.54022) ** 2)
    if radius < 0.215:
        radial = clamp(radius / 0.215)
        color = mix((0.67, 0.73, 0.78), (0.21, 0.29, 0.40), radial * 0.78)
        glint = clamp(normal.y * 0.55 - normal.x * 0.20)
        return mix(color, (0.94, 0.97, 0.98), glint * 0.16)
    color = (0.016, 0.022, 0.040)
    upper = clamp((position.y - 0.21755) / 0.64534) * 0.13
    variation = (math.sin(uv.x * math.tau * 13.0) * 0.5 + 0.5) * 0.015
    return scale_color(color, 0.86 + upper + variation)


def rasterize(obj, painter, output_path, image_name, fallback):
    mesh = obj.data
    uv_layer = mesh.uv_layers.active
    if not uv_layer or len(uv_layer.data) == 0:
        raise RuntimeError(f"{obj.name} has no paintable UV coordinates")
    mesh.calc_loop_triangles()
    object_bounds = bounds(obj)
    colors = [None] * (SIZE * SIZE)
    painted = set()

    for triangle in mesh.loop_triangles:
        loop_indices = triangle.loops
        uvs = [uv_layer.data[index].uv.copy() for index in loop_indices]
        screen = [(uv.x * (SIZE - 1), uv.y * (SIZE - 1)) for uv in uvs]
        min_x = max(0, int(math.floor(min(point[0] for point in screen))))
        max_x = min(SIZE - 1, int(math.ceil(max(point[0] for point in screen))))
        min_y = max(0, int(math.floor(min(point[1] for point in screen))))
        max_y = min(SIZE - 1, int(math.ceil(max(point[1] for point in screen))))
        x0, y0 = screen[0]
        x1, y1 = screen[1]
        x2, y2 = screen[2]
        denominator = (y1 - y2) * (x0 - x2) + (x2 - x1) * (y0 - y2)
        if abs(denominator) < 1e-10:
            continue
        vertex_indices = [mesh.loops[index].vertex_index for index in loop_indices]
        positions = [mesh.vertices[index].co.copy() for index in vertex_indices]
        polygon = mesh.polygons[triangle.polygon_index]
        normals = (
            [mesh.vertices[index].normal.copy() for index in vertex_indices]
            if polygon.use_smooth
            else [polygon.normal.copy(), polygon.normal.copy(), polygon.normal.copy()]
        )
        for py_int in range(min_y, max_y + 1):
            py = py_int + 0.5
            for px_int in range(min_x, max_x + 1):
                px = px_int + 0.5
                w0 = ((y1 - y2) * (px - x2) + (x2 - x1) * (py - y2)) / denominator
                w1 = ((y2 - y0) * (px - x2) + (x0 - x2) * (py - y2)) / denominator
                w2 = 1.0 - w0 - w1
                if w0 < -0.001 or w1 < -0.001 or w2 < -0.001:
                    continue
                position = positions[0] * w0 + positions[1] * w1 + positions[2] * w2
                normal = normals[0] * w0 + normals[1] * w1 + normals[2] * w2
                if normal.length_squared > 1e-12:
                    normal.normalize()
                uv = uvs[0] * w0 + uvs[1] * w1 + uvs[2] * w2
                index = py_int * SIZE + px_int
                colors[index] = painter(position, normal, uv, object_bounds)
                painted.add(index)

    frontier = set(painted)
    for _ in range(10):
        additions = {}
        for index in frontier:
            x = index % SIZE
            y = index // SIZE
            color = colors[index]
            if x > 0 and colors[index - 1] is None:
                additions[index - 1] = color
            if x + 1 < SIZE and colors[index + 1] is None:
                additions[index + 1] = color
            if y > 0 and colors[index - SIZE] is None:
                additions[index - SIZE] = color
            if y + 1 < SIZE and colors[index + SIZE] is None:
                additions[index + SIZE] = color
        for index, color in additions.items():
            colors[index] = color
        frontier = set(additions)
        if not frontier:
            break

    pixels = array("f")
    for color in colors:
        chosen = color if color is not None else fallback
        pixels.extend((clamp(chosen[0]), clamp(chosen[1]), clamp(chosen[2]), 1.0))

    image = bpy.data.images.new(image_name, width=SIZE, height=SIZE, alpha=True, float_buffer=False)
    image.colorspace_settings.name = "sRGB"
    image.alpha_mode = "STRAIGHT"
    image.pixels.foreach_set(pixels)
    image.filepath_raw = str(output_path)
    image.file_format = "PNG"
    image.save()
    image.pack()
    print(f"Painted {obj.name}: {len(painted)} pixels -> {output_path}")
    return image


def material_for(name, image, roughness):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.node_tree.nodes.clear()
    output = material.node_tree.nodes.new("ShaderNodeOutputMaterial")
    output.location = (340, 0)
    shader = material.node_tree.nodes.new("ShaderNodeBsdfPrincipled")
    shader.location = (30, 0)
    shader.inputs["Roughness"].default_value = roughness
    if "Specular IOR Level" in shader.inputs:
        shader.inputs["Specular IOR Level"].default_value = 0.28
    texture = material.node_tree.nodes.new("ShaderNodeTexImage")
    texture.location = (-260, 40)
    texture.image = image
    texture.interpolation = "Linear"
    texture.extension = "EXTEND"
    material.node_tree.links.new(texture.outputs["Color"], shader.inputs["Base Color"])
    material.node_tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return material


paint_specs = {
    "car.001": (body_painter, "car44_body_handpaint.png", "IMG_Car44_Body", (0.44, 0.008, 0.015), 0.58),
    "window.001": (window_painter, "car44_windshield_handpaint.png", "IMG_Car44_Windshield", (0.91, 0.89, 0.82), 0.48),
    "window_sides": (frame_painter, "car44_windshield_frame_handpaint.png", "IMG_Car44_Frame", (0.25, 0.004, 0.01), 0.62),
    "tires.001": (wheel_painter, "car44_wheels_handpaint.png", "IMG_Car44_Wheels", (0.016, 0.022, 0.04), 0.78),
}

TEXTURE_DIR.mkdir(parents=True, exist_ok=True)
source_hashes = {}
for object_name in paint_specs:
    obj = bpy.data.objects[object_name]
    source_hashes[object_name] = fingerprint(obj.data)
    stored = obj.get("source_uv_sha256", "")
    if stored and stored != source_hashes[object_name]:
        raise RuntimeError(f"Imported UV fingerprint mismatch on {object_name}")

for object_name, (painter, filename, image_name, fallback, roughness) in paint_specs.items():
    obj = bpy.data.objects[object_name]
    image = rasterize(obj, painter, TEXTURE_DIR / filename, image_name, fallback)
    material = material_for(f"MAT_{object_name.replace('.', '_')}_Handpaint", image, roughness)
    obj.data.materials.clear()
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.material_index = 0
    obj["handpaint_texture"] = filename

for object_name, source_hash in source_hashes.items():
    if fingerprint(bpy.data.objects[object_name].data) != source_hash:
        raise RuntimeError(f"UV map changed unexpectedly on {object_name}")

notes = bpy.data.texts.get("README_Handpaint_Car44") or bpy.data.texts.new("README_Handpaint_Car44")
notes.clear()
notes.write(
    "CarMatch Car 44 - Hand-painted Red Cartoon Style\n"
    "================================================\n"
    "Existing OBJ UV coordinates are preserved exactly.\n"
    "Four 512x512 PNG base-color textures: body, windshield, frame, and wheels.\n"
    "Headlights and lower front accent are painted directly on the body texture because they are not separate mesh objects.\n"
    "Textures are saved externally and packed into this Blender file.\n"
)

OUTPUT_BLEND.parent.mkdir(parents=True, exist_ok=True)
OUTPUT_OBJ.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT_BLEND), compress=True)

bpy.ops.wm.obj_export(
    filepath=str(OUTPUT_OBJ),
    export_selected_objects=False,
    export_uv=True,
    export_normals=True,
    export_materials=True,
    apply_modifiers=True,
    path_mode="RELATIVE",
    forward_axis="NEGATIVE_Z",
    up_axis="Y",
)

# Blender's OBJ exporter may calculate texture paths from the packed blend rather
# than the OBJ folder. Normalize map_Kd entries to the actual export texture folder.
mtl_path = OUTPUT_OBJ.with_suffix(".mtl")
if mtl_path.exists():
    texture_files = {path.name: path for path in TEXTURE_DIR.glob("*.png")}
    fixed_lines = []
    for line in mtl_path.read_text(encoding="utf-8").splitlines():
        if line.startswith("map_Kd "):
            filename = Path(line[7:].strip()).name
            texture_path = texture_files.get(filename)
            if texture_path:
                relative = os.path.relpath(texture_path, OUTPUT_OBJ.parent).replace(os.sep, "/")
                line = f"map_Kd {relative}"
        fixed_lines.append(line)
    mtl_path.write_text("\n".join(fixed_lines) + "\n", encoding="utf-8")

print(f"Saved hand-painted Blender file: {OUTPUT_BLEND}")
print(f"Exported textured OBJ: {OUTPUT_OBJ}")
