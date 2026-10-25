"""Read Unity scene YAML and compare semantic tile references by layer/cell."""
import argparse
import collections
import json
import re
from pathlib import Path

import yaml


def scene_maps(path):
    text = Path(path).read_text(encoding="utf-8-sig")
    chunks = re.split(r"^--- !u!\d+ &(-?\d+).*\n", text, flags=re.M)
    objects = {}
    for index in range(1, len(chunks), 2):
        objects[int(chunks[index])] = yaml.load(chunks[index + 1], Loader=yaml.CSafeLoader)
    result = {}
    for document in objects.values():
        if not document or "Tilemap" not in document:
            continue
        data = document["Tilemap"]
        name = objects[data["m_GameObject"]["fileID"]]["GameObject"]["m_Name"]
        cells = {}
        assets = data.get("m_TileAssetArray", [])
        matrices = data.get("m_TileMatrixArray", [])
        colors = data.get("m_TileColorArray", [])
        for entry in data.get("m_Tiles", []):
            position, tile = entry["first"], entry["second"]
            coordinate = tuple(position[key] for key in ("x", "y", "z"))
            asset = assets[tile["m_TileIndex"]]["m_Data"]
            matrix = matrices[tile["m_TileMatrixIndex"]]["m_Data"]
            color = colors[tile["m_TileColorIndex"]]["m_Data"]
            cells[coordinate] = (asset.get("guid", ""), asset["fileID"], tuple(matrix.values()), tuple(color.values()))
        result[name] = cells
    return result


def compare(before, after, names):
    report = {}
    for layer in sorted(before.keys() | after.keys()):
        a, b = before.get(layer, {}), after.get(layer, {})
        changes = []
        for cell in sorted(a.keys() | b.keys()):
            if a.get(cell) == b.get(cell):
                continue
            old, new = a.get(cell), b.get(cell)
            changes.append({"cell": cell, "before": names.get(old[0], old[0]) if old else None,
                            "after": names.get(new[0], new[0]) if new else None,
                            "kind": "added" if old is None else "removed" if new is None else "replaced" if old[:2] != new[:2] else "transform_or_color"})
        pairs = collections.Counter((c["before"], c["after"]) for c in changes)
        report[layer] = {"before_cells": len(a), "after_cells": len(b),
                         "changes": len(changes), "change_types": dict(collections.Counter(c["kind"] for c in changes)),
                         "transitions": [{"before": old, "after": new, "count": count} for (old, new), count in pairs.most_common()],
                         "cells": changes}
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("before")
    parser.add_argument("after")
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    names = {}
    for meta in Path("Assets").rglob("*.meta"):
        match = re.search(r"^guid: (\w+)$", meta.read_text(encoding="utf-8-sig", errors="replace"), re.M)
        if match:
            names[match[1]] = meta.as_posix()[:-5]
    report = compare(scene_maps(args.before), scene_maps(args.after), names)
    Path(args.output).write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    for layer, data in report.items():
        print(layer, json.dumps({k: v for k, v in data.items() if k not in ("cells", "transitions")}, ensure_ascii=False))
        print(json.dumps(data["transitions"][:12], ensure_ascii=False))
