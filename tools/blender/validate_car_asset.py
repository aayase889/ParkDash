import bpy
import bmesh
import json
import sys
from pathlib import Path


report = {
    "file": bpy.data.filepath,
    "mesh_objects": 0,
    "vertices": 0,
    "polygons": 0,
    "evaluated_triangles": 0,
    "boundary_edges": 0,
    "non_manifold_edges": 0,
    "uv_layers": 0,
    "objects_with_modifiers": [],
    "objects_with_non_unit_scale": [],
    "wheel_objects": [],
}

depsgraph = bpy.context.evaluated_depsgraph_get()
for obj in bpy.context.scene.objects:
    if obj.type != "MESH":
        continue
    report["mesh_objects"] += 1
    report["vertices"] += len(obj.data.vertices)
    report["polygons"] += len(obj.data.polygons)
    report["uv_layers"] += len(obj.data.uv_layers)
    if obj.modifiers:
        report["objects_with_modifiers"].append(obj.name)
    if any(abs(value - 1.0) > 1e-6 for value in obj.scale):
        report["objects_with_non_unit_scale"].append(obj.name)

    bm = bmesh.new()
    bm.from_mesh(obj.data)
    report["boundary_edges"] += sum(1 for edge in bm.edges if edge.is_boundary)
    report["non_manifold_edges"] += sum(1 for edge in bm.edges if not edge.is_manifold)
    bm.free()

    evaluated = obj.evaluated_get(depsgraph)
    evaluated_mesh = evaluated.to_mesh()
    evaluated_mesh.calc_loop_triangles()
    report["evaluated_triangles"] += len(evaluated_mesh.loop_triangles)
    evaluated.to_mesh_clear()

    if obj.name.startswith("Wheel_"):
        report["wheel_objects"].append(
            {
                "name": obj.name,
                "location": [round(value, 5) for value in obj.location],
                "rotation": [round(value, 5) for value in obj.rotation_euler],
                "scale": [round(value, 5) for value in obj.scale],
            }
        )

report["checks"] = {
    "mobile_budget_under_4000_triangles": report["evaluated_triangles"] < 4000,
    "all_meshes_closed_manifold": report["boundary_edges"] == 0 and report["non_manifold_edges"] == 0,
    "no_uv_maps": report["uv_layers"] == 0,
    "no_live_modifiers": not report["objects_with_modifiers"],
    "unit_scale_on_mesh_objects": not report["objects_with_non_unit_scale"],
    "four_separate_wheels": len(report["wheel_objects"]) == 4,
}

args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
output_path = Path(args[0]) if args else Path("car_asset_validation.json")
output_path.parent.mkdir(parents=True, exist_ok=True)
output_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report, indent=2))
