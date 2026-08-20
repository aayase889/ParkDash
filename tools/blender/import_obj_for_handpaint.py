import bpy
import hashlib
import json
import struct
import sys
from pathlib import Path


args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
input_obj = Path(args[0])
output_blend = Path(args[1])
output_report = Path(args[2])

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)

bpy.ops.wm.obj_import(filepath=str(input_obj), forward_axis="NEGATIVE_Z", up_axis="Y")


def uv_fingerprint(mesh):
    layer = mesh.uv_layers.active
    digest = hashlib.sha256()
    if not layer:
        return None, 0, None
    for item in layer.data:
        digest.update(struct.pack("<2d", float(item.uv.x), float(item.uv.y)))
    u_values = [item.uv.x for item in layer.data]
    v_values = [item.uv.y for item in layer.data]
    bounds = [min(u_values), min(v_values), max(u_values), max(v_values)] if u_values else None
    return digest.hexdigest(), len(layer.data), bounds


report = {"source_obj": str(input_obj), "objects": []}
for obj in sorted(bpy.context.scene.objects, key=lambda item: item.name.lower()):
    if obj.type != "MESH":
        continue
    fingerprint, loops, uv_bounds = uv_fingerprint(obj.data)
    obj["source_uv_sha256"] = fingerprint or ""
    obj.data.calc_loop_triangles()
    report["objects"].append(
        {
            "name": obj.name,
            "vertices": len(obj.data.vertices),
            "polygons": len(obj.data.polygons),
            "triangles": len(obj.data.loop_triangles),
            "dimensions": [round(value, 6) for value in obj.dimensions],
            "location": [round(value, 6) for value in obj.location],
            "uv_layer": obj.data.uv_layers.active.name if obj.data.uv_layers.active else None,
            "uv_loops": loops,
            "uv_bounds": [round(value, 6) for value in uv_bounds] if uv_bounds else None,
            "uv_sha256": fingerprint,
            "materials": [
                {
                    "name": slot.material.name if slot.material else None,
                    "images": [
                        {
                            "name": node.image.name,
                            "filepath": bpy.path.abspath(node.image.filepath),
                            "size": list(node.image.size),
                        }
                        for node in (slot.material.node_tree.nodes if slot.material and slot.material.node_tree else [])
                        if node.type == "TEX_IMAGE" and node.image
                    ],
                }
                for slot in obj.material_slots
            ],
        }
    )

output_blend.parent.mkdir(parents=True, exist_ok=True)
output_report.parent.mkdir(parents=True, exist_ok=True)
output_report.write_text(json.dumps(report, indent=2), encoding="utf-8")
bpy.ops.wm.save_as_mainfile(filepath=str(output_blend), compress=True)
print(json.dumps(report, indent=2))
print(f"Saved imported OBJ scene: {output_blend}")
