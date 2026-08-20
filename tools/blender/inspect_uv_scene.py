import bpy
import hashlib
import json
import struct
import sys
from pathlib import Path


def uv_fingerprint(mesh, layer):
    digest = hashlib.sha256()
    for item in layer.data:
        digest.update(struct.pack("<2d", float(item.uv.x), float(item.uv.y)))
    return digest.hexdigest()


def material_report(mat):
    images = []
    if mat and mat.use_nodes and mat.node_tree:
        for node in mat.node_tree.nodes:
            if node.type == "TEX_IMAGE" and node.image:
                images.append(
                    {
                        "name": node.image.name,
                        "filepath": bpy.path.abspath(node.image.filepath),
                        "size": list(node.image.size),
                        "packed": bool(node.image.packed_file),
                    }
                )
    return {"name": mat.name if mat else None, "images": images}


report = {
    "file": bpy.data.filepath,
    "scene": bpy.context.scene.name,
    "objects": [],
}

for obj in sorted(bpy.context.scene.objects, key=lambda item: item.name.lower()):
    entry = {
        "name": obj.name,
        "type": obj.type,
        "location": [round(value, 6) for value in obj.location],
        "rotation": [round(value, 6) for value in obj.rotation_euler],
        "scale": [round(value, 6) for value in obj.scale],
        "dimensions": [round(value, 6) for value in obj.dimensions],
    }
    if obj.type == "MESH":
        mesh = obj.data
        uv_layers = []
        for layer in mesh.uv_layers:
            if layer.data:
                u_values = [item.uv.x for item in layer.data]
                v_values = [item.uv.y for item in layer.data]
                bounds = [min(u_values), min(v_values), max(u_values), max(v_values)]
            else:
                bounds = [0.0, 0.0, 0.0, 0.0]
            uv_layers.append(
                {
                    "name": layer.name,
                    "active": layer == mesh.uv_layers.active,
                    "loops": len(layer.data),
                    "bounds": [round(value, 6) for value in bounds],
                    "fingerprint_sha256": uv_fingerprint(mesh, layer),
                }
            )
        mesh.calc_loop_triangles()
        entry["mesh"] = {
            "vertices": len(mesh.vertices),
            "polygons": len(mesh.polygons),
            "triangles": len(mesh.loop_triangles),
            "uv_layers": uv_layers,
            "materials": [material_report(slot.material) for slot in obj.material_slots],
            "material_indices": sorted(set(polygon.material_index for polygon in mesh.polygons)),
            "attributes": [
                {
                    "name": attribute.name,
                    "domain": attribute.domain,
                    "data_type": attribute.data_type,
                    "length": len(attribute.data),
                }
                for attribute in mesh.attributes
            ],
            "modifiers": [
                {
                    "name": modifier.name,
                    "type": modifier.type,
                    "show_viewport": modifier.show_viewport,
                    "show_render": modifier.show_render,
                }
                for modifier in obj.modifiers
            ],
        }
    report["objects"].append(entry)

args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
output_path = Path(args[0]) if args else Path("uv_scene_report.json")
output_path.parent.mkdir(parents=True, exist_ok=True)
output_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report, indent=2))
