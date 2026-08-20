import bpy
import bmesh
import json
import sys
from pathlib import Path


def mesh_stats(obj):
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    boundary_edges = sum(1 for edge in bm.edges if edge.is_boundary)
    non_manifold_edges = sum(1 for edge in bm.edges if not edge.is_manifold)
    bm.free()

    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    eval_mesh = evaluated.to_mesh()
    eval_mesh.calc_loop_triangles()
    triangles = len(eval_mesh.loop_triangles)
    evaluated.to_mesh_clear()

    return {
        "vertices": len(mesh.vertices),
        "edges": len(mesh.edges),
        "polygons": len(mesh.polygons),
        "triangles_evaluated": triangles,
        "boundary_edges": boundary_edges,
        "non_manifold_edges": non_manifold_edges,
        "modifiers": [modifier.type for modifier in obj.modifiers],
        "materials": [slot.material.name if slot.material else None for slot in obj.material_slots],
    }


report = {
    "file": bpy.data.filepath,
    "scene": bpy.context.scene.name,
    "unit_system": bpy.context.scene.unit_settings.system,
    "objects": [],
}

for obj in sorted(bpy.context.scene.objects, key=lambda item: item.name.lower()):
    entry = {
        "name": obj.name,
        "type": obj.type,
        "location": [round(value, 5) for value in obj.location],
        "rotation_euler": [round(value, 5) for value in obj.rotation_euler],
        "scale": [round(value, 5) for value in obj.scale],
        "dimensions": [round(value, 5) for value in obj.dimensions],
        "parent": obj.parent.name if obj.parent else None,
        "hidden_render": obj.hide_render,
    }
    if obj.type == "MESH":
        entry["mesh"] = mesh_stats(obj)
    report["objects"].append(entry)

script_args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
output_path = Path(script_args[0]) if script_args else Path("car_scene_report.json")
output_path.parent.mkdir(parents=True, exist_ok=True)
output_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report, indent=2))
