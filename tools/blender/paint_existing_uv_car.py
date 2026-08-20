import bpy
import hashlib
import math
import struct
import sys
from array import array
from pathlib import Path
from mathutils import Vector


args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
OUTPUT_BLEND = Path(args[0]) if args else Path("car_handpaint_red_reference_v1.blend")
TEXTURE_DIR = Path(args[1]) if len(args) > 1 else OUTPUT_BLEND.parent / "textures"
TEXTURE_SIZE = 512


def clamp(value, low=0.0, high=1.0):
    return max(low, min(high, value))


def mix(a, b, amount):
    amount = clamp(amount)
    return tuple(a[index] * (1.0 - amount) + b[index] * amount for index in range(3))


def scale_color(color, amount):
    return tuple(clamp(channel * amount) for channel in color)


def uv_fingerprint(mesh):
    layer = mesh.uv_layers.active
    digest = hashlib.sha256()
    if not layer:
        return None
    for item in layer.data:
        digest.update(struct.pack("<2d", float(item.uv.x), float(item.uv.y)))
    return digest.hexdigest()


def bounds_for_object(obj):
    coords = [vertex.co for vertex in obj.data.vertices]
    minimum = Vector(tuple(min(co[axis] for co in coords) for axis in range(3)))
    maximum = Vector(tuple(max(co[axis] for co in coords) for axis in range(3)))
    return minimum, maximum


def normalize_position(position, bounds):
    minimum, maximum = bounds
    result = []
    for axis in range(3):
        span = max(maximum[axis] - minimum[axis], 1e-6)
        result.append((position[axis] - minimum[axis]) / span)
    return result


def handpaint_wobble(position):
    return (
        math.sin(position.x * 7.31 + position.y * 3.17 + position.z * 5.83) * 0.5
        + math.sin(position.x * 13.7 - position.y * 4.9 + position.z * 2.1) * 0.25
    )


def paint_body(position, normal, uv, bounds):
    nx, ny, nz = normalize_position(position, bounds)
    x_centered = nx * 2.0 - 1.0
    base = (0.72, 0.018, 0.030)
    shade = 0.72 + nz * 0.25 + max(normal.z, 0.0) * 0.10
    shade -= abs(x_centered) ** 1.5 * 0.10
    shade -= max(0.0, 0.24 - nz) * 0.45
    shade += handpaint_wobble(position) * 0.018
    color = scale_color(base, shade)

    # Broad painted roof/hood sheen inspired by the hot-pink reference highlight.
    center_band = math.exp(-((x_centered + 0.18) ** 2) / 0.085)
    length_band = 0.55 + 0.45 * math.exp(-((ny - 0.40) ** 2) / 0.18)
    top_facing = clamp((normal.z - 0.15) / 0.75)
    highlight = center_band * length_band * top_facing * (0.20 + nz * 0.24)
    color = mix(color, (1.0, 0.16, 0.30), highlight)

    # Warm reflected color on the front face and darker hand-painted lower edge.
    front_glow = clamp(-normal.y) * math.exp(-((x_centered) ** 2) / 0.55) * 0.12
    color = mix(color, (1.0, 0.075, 0.045), front_glow)
    if nz < 0.18:
        color = mix(color, (0.26, 0.004, 0.012), (0.18 - nz) * 1.7)
    return color


def paint_window(position, normal, uv, bounds):
    # The front windshield sits near local Y=0 and is mirrored from one modeled half.
    is_front = position.y < 0.31 and abs(normal.y) > 0.32
    if is_front:
        color = (0.93, 0.91, 0.82)
        eye_x = abs(position.x)
        eye = ((eye_x - 0.31) / 0.205) ** 2 + ((position.z - 0.77) / 0.265) ** 2
        pupil = ((eye_x - 0.325) / 0.118) ** 2 + ((position.z - 0.755) / 0.195) ** 2
        if eye <= 1.0:
            color = (0.98, 0.96, 0.89)
        if pupil <= 1.0:
            color = (0.055, 0.035, 0.030)
        catchlight = ((eye_x - 0.285) / 0.040) ** 2 + ((position.z - 0.855) / 0.055) ** 2
        if catchlight <= 1.0:
            color = (1.0, 1.0, 0.98)
        return color

    _, ny, nz = normalize_position(position, bounds)
    base = mix((0.055, 0.19, 0.31), (0.28, 0.68, 0.92), nz * 0.48)
    diagonal = clamp(1.0 - abs((uv.x * 0.82 + uv.y * 0.58) - 0.86) * 7.0)
    return mix(base, (0.72, 0.90, 1.0), diagonal * 0.18)


def paint_frame(position, normal, uv, bounds):
    _, _, nz = normalize_position(position, bounds)
    color = scale_color((0.38, 0.006, 0.015), 0.74 + nz * 0.30 + max(normal.z, 0.0) * 0.10)
    return mix(color, (0.82, 0.025, 0.040), max(normal.z, 0.0) * 0.10)


def paint_tire(position, normal, uv, bounds):
    front_y = -0.291
    rear_y = 1.030
    wheel_y = front_y if abs(position.y - front_y) < abs(position.y - rear_y) else rear_y
    radius = math.sqrt((position.y - wheel_y) ** 2 + (position.z + 0.271) ** 2)
    if radius < 0.275:
        radial = clamp(radius / 0.275)
        color = mix((0.58, 0.67, 0.74), (0.19, 0.28, 0.38), radial * 0.75)
        glint = clamp(normal.z * 0.55 + (-normal.y) * 0.25)
        return mix(color, (0.88, 0.94, 0.96), glint * 0.16)
    color = (0.018, 0.025, 0.045)
    upper_light = clamp((position.z + 0.68) / 0.82) * 0.12
    tread_variation = (math.sin(uv.x * math.tau * 15.0) * 0.5 + 0.5) * 0.018
    return scale_color(color, 0.86 + upper_light + tread_variation)


def paint_light(position, normal, uv, bounds):
    nx, ny, nz = normalize_position(position, bounds)
    color = mix((0.31, 0.55, 0.72), (0.82, 0.94, 0.98), nz * 0.65)
    glint = math.exp(-((nx - 0.30) ** 2 + (nz - 0.74) ** 2) / 0.025)
    color = mix(color, (1.0, 1.0, 0.97), glint * 0.82)
    edge = clamp(abs(normal.y) + max(normal.z, 0.0) * 0.35)
    return mix(color, (0.93, 0.98, 1.0), edge * 0.12)


def edge_function(ax, ay, bx, by, px, py):
    return (px - ax) * (by - ay) - (py - ay) * (bx - ax)


def rasterize_texture(obj, painter, output_path, image_name, fallback_color):
    mesh = obj.data
    uv_layer = mesh.uv_layers.active
    if not uv_layer or len(uv_layer.data) == 0:
        raise RuntimeError(f"Object {obj.name} does not contain paintable UV coordinates")

    mesh.calc_loop_triangles()
    bounds = bounds_for_object(obj)
    size = TEXTURE_SIZE
    colors = [None] * (size * size)
    painted = set()

    for triangle in mesh.loop_triangles:
        loop_indices = triangle.loops
        uv_coords = [uv_layer.data[loop_index].uv.copy() for loop_index in loop_indices]
        points = [(uv.x * (size - 1), uv.y * (size - 1)) for uv in uv_coords]
        min_x = max(0, int(math.floor(min(point[0] for point in points))))
        max_x = min(size - 1, int(math.ceil(max(point[0] for point in points))))
        min_y = max(0, int(math.floor(min(point[1] for point in points))))
        max_y = min(size - 1, int(math.ceil(max(point[1] for point in points))))
        x0, y0 = points[0]
        x1, y1 = points[1]
        x2, y2 = points[2]
        denominator = (y1 - y2) * (x0 - x2) + (x2 - x1) * (y0 - y2)
        if abs(denominator) < 1e-10:
            continue

        vertex_indices = [mesh.loops[index].vertex_index for index in loop_indices]
        positions = [mesh.vertices[index].co.copy() for index in vertex_indices]
        polygon = mesh.polygons[triangle.polygon_index]
        if polygon.use_smooth:
            normals = [mesh.vertices[index].normal.copy() for index in vertex_indices]
        else:
            normals = [polygon.normal.copy(), polygon.normal.copy(), polygon.normal.copy()]

        for pixel_y in range(min_y, max_y + 1):
            py = pixel_y + 0.5
            for pixel_x in range(min_x, max_x + 1):
                px = pixel_x + 0.5
                w0 = ((y1 - y2) * (px - x2) + (x2 - x1) * (py - y2)) / denominator
                w1 = ((y2 - y0) * (px - x2) + (x0 - x2) * (py - y2)) / denominator
                w2 = 1.0 - w0 - w1
                if w0 < -0.001 or w1 < -0.001 or w2 < -0.001:
                    continue
                position = positions[0] * w0 + positions[1] * w1 + positions[2] * w2
                normal = normals[0] * w0 + normals[1] * w1 + normals[2] * w2
                if normal.length_squared > 1e-12:
                    normal.normalize()
                uv = uv_coords[0] * w0 + uv_coords[1] * w1 + uv_coords[2] * w2
                index = pixel_y * size + pixel_x
                colors[index] = painter(position, normal, uv, bounds)
                painted.add(index)

    # Grow island colors into empty padding to prevent bilinear seam halos in Unity.
    frontier = set(painted)
    for _ in range(10):
        additions = {}
        for index in frontier:
            x = index % size
            y = index // size
            source_color = colors[index]
            if x > 0 and colors[index - 1] is None:
                additions[index - 1] = source_color
            if x + 1 < size and colors[index + 1] is None:
                additions[index + 1] = source_color
            if y > 0 and colors[index - size] is None:
                additions[index - size] = source_color
            if y + 1 < size and colors[index + size] is None:
                additions[index + size] = source_color
        if not additions:
            break
        for index, color in additions.items():
            colors[index] = color
        frontier = set(additions)

    pixels = array("f")
    for color in colors:
        final_color = color if color is not None else fallback_color
        pixels.extend((clamp(final_color[0]), clamp(final_color[1]), clamp(final_color[2]), 1.0))

    image = bpy.data.images.get(image_name)
    if image:
        bpy.data.images.remove(image)
    image = bpy.data.images.new(image_name, width=size, height=size, alpha=True, float_buffer=False)
    image.alpha_mode = "STRAIGHT"
    image.colorspace_settings.name = "sRGB"
    image.pixels.foreach_set(pixels)
    image.filepath_raw = str(output_path)
    image.file_format = "PNG"
    image.save()
    image.pack()
    print(f"Painted {obj.name}: {len(painted)} UV pixels -> {output_path}")
    return image


def make_texture_material(name, image, roughness=0.62, metallic=0.0):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    output.location = (330, 0)
    principled = nodes.new("ShaderNodeBsdfPrincipled")
    principled.location = (40, 0)
    principled.inputs["Roughness"].default_value = roughness
    principled.inputs["Metallic"].default_value = metallic
    if "Specular IOR Level" in principled.inputs:
        principled.inputs["Specular IOR Level"].default_value = 0.28
    texture = nodes.new("ShaderNodeTexImage")
    texture.location = (-260, 40)
    texture.image = image
    texture.interpolation = "Linear"
    texture.extension = "EXTEND"
    links.new(texture.outputs["Color"], principled.inputs["Base Color"])
    links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    return material


def assign_material(obj, material):
    obj.data.materials.clear()
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.material_index = 0


targets = {
    "car": (paint_body, "car_body_handpaint.png", "IMG_CarBody_Handpaint", (0.45, 0.01, 0.02), 0.58, 0.0),
    "window": (paint_window, "car_windows_handpaint.png", "IMG_CarWindows_Handpaint", (0.05, 0.15, 0.24), 0.48, 0.0),
    "window.sides": (paint_frame, "car_window_frames_handpaint.png", "IMG_CarWindowFrames_Handpaint", (0.24, 0.005, 0.01), 0.60, 0.0),
    "tires": (paint_tire, "car_wheels_handpaint.png", "IMG_CarWheels_Handpaint", (0.018, 0.024, 0.04), 0.78, 0.0),
    "light": (paint_light, "car_headlights_handpaint.png", "IMG_CarHeadlights_Handpaint", (0.30, 0.50, 0.66), 0.32, 0.0),
}

missing = [name for name in targets if name not in bpy.data.objects]
if missing:
    raise RuntimeError(f"Missing required car objects: {missing}")

TEXTURE_DIR.mkdir(parents=True, exist_ok=True)
source_fingerprints = {}
for name in targets:
    obj = bpy.data.objects[name]
    source_fingerprints[name] = uv_fingerprint(obj.data)
    if not source_fingerprints[name]:
        raise RuntimeError(f"Object {name} has no valid UV fingerprint")

for name, (painter, filename, image_name, fallback, roughness, metallic) in targets.items():
    obj = bpy.data.objects[name]
    image = rasterize_texture(obj, painter, TEXTURE_DIR / filename, image_name, fallback)
    material = make_texture_material(f"MAT_{name.replace('.', '_')}_Handpaint", image, roughness, metallic)
    assign_material(obj, material)
    obj["source_uv_sha256"] = source_fingerprints[name]
    obj["handpaint_texture"] = filename

for name, fingerprint in source_fingerprints.items():
    current = uv_fingerprint(bpy.data.objects[name].data)
    if current != fingerprint:
        raise RuntimeError(f"UV coordinates changed unexpectedly on {name}: {fingerprint} -> {current}")

notes = bpy.data.texts.get("README_Handpaint") or bpy.data.texts.new("README_Handpaint")
notes.clear()
notes.write(
    "Hand-painted Red Cartoon Car\n"
    "==============================\n"
    "Reference role: supplied screenshot showing a glossy red cartoon car.\n"
    "Paint target: completed left-hand car objects (car, window, window.sides, tires, light).\n"
    "The existing UV coordinates were preserved byte-for-byte; SHA-256 fingerprints are stored on each painted object.\n"
    "Texture size: 512 x 512 PNG per existing material/object group for mobile-friendly use.\n"
    "Style: saturated red body, hot-pink painted highlights, deep red edge shading, cartoon windshield eyes, blue glass, pale blue headlights, dark tires, and blue-grey hubs.\n"
    "The detached object named Plane was not painted because its UVMap contains zero UV coordinates.\n"
)

bpy.context.scene["handpaint_reference"] = "Screenshot 2026-07-17 at 12.19.30.png"
bpy.context.scene["handpaint_uv_preserved"] = True
bpy.context.scene["handpaint_texture_size"] = TEXTURE_SIZE

OUTPUT_BLEND.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT_BLEND), compress=True)
print(f"Saved hand-painted Blender file: {OUTPUT_BLEND}")
