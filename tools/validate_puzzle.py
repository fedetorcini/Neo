"""Validate a discrete push puzzle. Does not validate Unity physics or two actors."""
import argparse
from collections import deque
import json
from pathlib import Path

DIRECTIONS = {"U": (0, -1), "R": (1, 0), "D": (0, 1), "L": (-1, 0)}


def parse(data):
    rows = data["rows"]
    if not rows or not rows[0] or any(len(row) != len(rows[0]) for row in rows):
        raise ValueError("El plano debe ser rectangular y no vacío.")
    if data.get("goal") != "reach_exit":
        raise ValueError("Objetivo no soportado: se requiere reach_exit.")
    floor, rocks, actors, exits = set(), set(), [], set()
    for y, row in enumerate(rows):
        for x, char in enumerate(row):
            if char not in "# MRE":
                raise ValueError(f"Símbolo desconocido: {char!r}")
            if char != "#":
                floor.add((x, y))
            if char == "R":
                rocks.add((x, y))
            elif char == "M":
                actors.append((x, y))
            elif char == "E":
                exits.add((x, y))
    if len(actors) != 1 or not exits:
        raise ValueError("Se requiere un Machop y al menos una salida.")
    return floor, exits, (actors[0], frozenset(rocks))


def step(state, direction, floor):
    actor, rocks = state
    dx, dy = DIRECTIONS[direction]
    destination = (actor[0] + dx, actor[1] + dy)
    if destination not in floor:
        return None
    if destination in rocks:
        beyond = (destination[0] + dx, destination[1] + dy)
        if beyond not in floor or beyond in rocks:
            return None
        rocks = (rocks - {destination}) | {beyond}
    return destination, frozenset(rocks)


def validate(data, limit=200000):
    floor, exits, start = parse(data)
    reference = "".join(data.get("reference_moves", "").split()).upper()
    if reference:
        state = start
        for index, direction in enumerate(reference, 1):
            if direction not in DIRECTIONS:
                raise ValueError(f"Dirección inválida en paso {index}.")
            state = step(state, direction, floor)
            if state is None:
                raise ValueError(f"La solución de referencia se bloquea en paso {index}.")
        if state[0] not in exits:
            raise ValueError("La referencia no llega a la salida.")
    queue = deque([start])
    previous = {start: None}
    while queue:
        state = queue.popleft()
        if state[0] in exits:
            moves = []
            while previous[state] is not None:
                state, direction = previous[state]
                moves.append(direction)
            return {"solvable": True, "moves": "".join(reversed(moves)),
                    "states_examined": len(previous), "reference_valid": bool(reference)}
        for direction in DIRECTIONS:
            next_state = step(state, direction, floor)
            if next_state is None or next_state in previous:
                continue
            if len(previous) >= limit:
                raise ValueError("Límite de estados alcanzado; resultado inconcluso.")
            previous[next_state] = (state, direction)
            queue.append(next_state)
    return {"solvable": False, "states_examined": len(previous), "reference_valid": False}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("plan", type=Path)
    args = parser.parse_args()
    try:
        result = validate(json.loads(args.plan.read_text(encoding="utf-8-sig")))
        print(json.dumps(result, ensure_ascii=False))
        raise SystemExit(0 if result["solvable"] else 1)
    except (ValueError, KeyError, TypeError, OSError) as error:
        parser.exit(2, f"Plano inválido: {error}\n")
