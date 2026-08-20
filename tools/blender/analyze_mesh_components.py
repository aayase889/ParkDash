import bpy
import json
import sys
from collections import deque
from pathlib import Path


def components_for_mesh(obj):
    mesh = obj.data
    adjacency = [set() for _ in mesh.vertices]
    polygons_by_vertex = [[] for _ in mesh.vertices]
    for edge in mesh.edges:
        a, b = edge.vertices
        adjacency[a].add(b)
        adjacency[b].add(a)
    for polygon in mesh.polygons:
        for vertex_index in polygon.vertices:
            polygons_by_vertex[vertex_index].append(polygon.index)

    unseen = set(range(len(mesh.vertices)))
    components = []
    while unseen:
        start = unseen.pop()
        queue = deque([start])
        vertex_indices = {start}
        while queue:
            current = queue.popleft()
            for neighbor in adjacency[current]:
                if neighbor in unseen:
                    unseen.remove(neighbor)
                    vertex_indices.add(neighbor)
                    queue.append(neighbor)

        polygon_indices = set()
        for vertex_index in vertex_indices:
            polygon_indices.update(polygons_by_vertex[vertex_index])
        coords = [mesh.vertices[index].co for index in vertex_indices]
        minimum = [min(co[axis] for co in coords) for axis in range(3)]
        maximum = [max(co[axis] for co in coords) for axis in range(3)]
        components.append(
            {
                "vertices": len(vertex_indices),
                "polygons": len(polygon_indices),
                "min": [round(value, 5) for value in minimum],
                "max": [round(value, 5) for value in maximum],
                "center": [round((minimum[i] + maximum[i]) * 0.5, 5) for i in range(3)],
            }
        )
    return sorted(components, key=lambda item: item["vertices"], reverse=True)


report = {}
for obj in bpy.context.scene.objects:
    if obj.type == "MESH":
        report[obj.name] = components_for_mesh(obj)

args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
output_path = Path(args[0]) if args else Path("mesh_components.json")
output_path.parent.mkdir(parents=True, exist_ok=True)
output_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report, indent=2))
