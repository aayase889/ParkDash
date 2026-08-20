"""Write material colors, shader inputs, texture nodes, and links to JSON."""

from __future__ import annotations

import json
import sys
from pathlib import Path

import bpy


def serializable_value(value):
    if isinstance(value, (bool, int, float, str)):
        return value
    try:
        return [round(float(component), 6) for component in value]
    except (TypeError, ValueError):
        return str(value)


materials = []
for material in sorted(bpy.data.materials, key=lambda item: item.name.lower()):
    entry = {
        "name": material.name,
        "diffuse_color": [round(float(value), 6) for value in material.diffuse_color],
        "metallic": round(float(material.metallic), 6),
        "roughness": round(float(material.roughness), 6),
        "use_nodes": bool(material.use_nodes),
        "nodes": [],
        "links": [],
    }
    if material.use_nodes and material.node_tree:
        for node in material.node_tree.nodes:
            node_entry = {
                "name": node.name,
                "type": node.type,
                "inputs": {},
            }
            for socket in node.inputs:
                if socket.is_linked:
                    continue
                try:
                    node_entry["inputs"][socket.name] = serializable_value(socket.default_value)
                except AttributeError:
                    pass
            if node.type == "TEX_IMAGE" and node.image:
                node_entry["image"] = {
                    "name": node.image.name,
                    "filepath": bpy.path.abspath(node.image.filepath),
                    "size": list(node.image.size),
                    "packed": bool(node.image.packed_file),
                    "color_space": node.image.colorspace_settings.name,
                }
            entry["nodes"].append(node_entry)
        for link in material.node_tree.links:
            entry["links"].append(
                {
                    "from_node": link.from_node.name,
                    "from_socket": link.from_socket.name,
                    "to_node": link.to_node.name,
                    "to_socket": link.to_socket.name,
                }
            )
    materials.append(entry)

report = {"file": bpy.data.filepath, "materials": materials}
args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
output_path = Path(args[0] if args else "material_nodes.json").resolve()
output_path.parent.mkdir(parents=True, exist_ok=True)
output_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report, indent=2))
