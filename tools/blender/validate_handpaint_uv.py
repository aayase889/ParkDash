import bpy
import hashlib
import json
import struct
import sys
from pathlib import Path


args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
source_report_path = Path(args[0])
output_path = Path(args[1])
source_report = json.loads(source_report_path.read_text(encoding="utf-8"))


def fingerprint(mesh):
    layer = mesh.uv_layers.active
    digest = hashlib.sha256()
    if not layer:
        return None, 0
    for item in layer.data:
        digest.update(struct.pack("<2d", float(item.uv.x), float(item.uv.y)))
    return digest.hexdigest(), len(layer.data)


source_objects = {entry["name"]: entry for entry in source_report["objects"]}
target_names = ["car", "window", "window.sides", "tires", "light"]
report = {
    "file": bpy.data.filepath,
    "targets": {},
    "detached_plane": {},
    "checks": {},
}

all_uv_equal = True
all_textures_valid = True
for name in target_names:
    obj = bpy.data.objects[name]
    current_hash, current_loops = fingerprint(obj.data)
    source_uv = source_objects[name]["mesh"]["uv_layers"][0]
    expected_hash = source_uv["fingerprint_sha256"]
    expected_loops = source_uv["loops"]
    material = obj.active_material
    texture_images = []
    if material and material.node_tree:
        for node in material.node_tree.nodes:
            if node.type == "TEX_IMAGE" and node.image:
                image = node.image
                filepath = Path(bpy.path.abspath(image.filepath))
                texture_images.append(
                    {
                        "name": image.name,
                        "size": list(image.size),
                        "packed": bool(image.packed_file),
                        "filepath": str(filepath),
                        "external_file_exists": filepath.exists(),
                    }
                )
    uv_equal = current_hash == expected_hash and current_loops == expected_loops
    textures_valid = bool(texture_images) and all(
        image["size"] == [512, 512] and image["packed"] and image["external_file_exists"]
        for image in texture_images
    )
    all_uv_equal = all_uv_equal and uv_equal
    all_textures_valid = all_textures_valid and textures_valid
    report["targets"][name] = {
        "source_uv_sha256": expected_hash,
        "final_uv_sha256": current_hash,
        "source_uv_loops": expected_loops,
        "final_uv_loops": current_loops,
        "uv_identical": uv_equal,
        "material": material.name if material else None,
        "textures": texture_images,
    }

plane = bpy.data.objects.get("Plane")
if plane and plane.type == "MESH":
    plane_hash, plane_loops = fingerprint(plane.data)
    report["detached_plane"] = {
        "uv_sha256": plane_hash,
        "uv_loops": plane_loops,
        "painted": bool(plane.active_material),
    }

report["checks"] = {
    "all_painted_uv_maps_byte_identical_to_source": all_uv_equal,
    "all_textures_512_png_external_and_packed": all_textures_valid,
    "five_painted_object_groups": len(report["targets"]) == 5,
    "detached_plane_left_unpainted": not report["detached_plane"].get("painted", True),
    "detached_plane_still_has_zero_uv_loops": report["detached_plane"].get("uv_loops") == 0,
}

output_path.parent.mkdir(parents=True, exist_ok=True)
output_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report, indent=2))
