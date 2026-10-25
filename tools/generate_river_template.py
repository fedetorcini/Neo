"""Genera la plantilla de materiales de NeoRiverTile (río con niveles y cascadas).

Uso:  python tools/generate_river_template.py

Produce Assets/Sprites/River/rio_cauce.png: una columna de casillas de 16 x 16, una por material.
Cada material es un patrón que se repite cada 16 px en ambos ejes; el tile lo desplaza para animar
(la corriente, la caída del agua), así que la plantilla no tiene frames. Se puede retocar a mano
en Aseprite respetando el orden de las filas y que los bordes encajen.

    0 agua profunda      1 agua baja (lecho con piedras)   2 roca de orilla
    3 lámina de cascada  4 espuma (alfa = densidad)          5 pared de roca junto a cascadas
"""
import random
from pathlib import Path

from PIL import Image

TILE = 16
ROWS = ["agua", "bajo", "orilla", "cascada", "espuma", "pared"]


def rgba(hex_color, alpha=255):
    h = hex_color.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), alpha)


class Tile:
    """Una casilla con escritura envuelta: lo que sale por un borde entra por el opuesto."""

    def __init__(self, fill):
        self.px = [[rgba(fill) for _ in range(TILE)] for _ in range(TILE)]

    def put(self, x, y, c):
        self.px[y % TILE][x % TILE] = rgba(*c) if isinstance(c, tuple) else rgba(c)

    def get(self, x, y):
        return self.px[y % TILE][x % TILE]


def agua():
    # Ondas cortas en arco, como el río actual, sin dirección marcada: el tile las hace correr.
    t = Tile("#3060b0")
    arcs = [(1, 2), (9, 5), (4, 10), (12, 13)]
    for ox, oy in arcs:
        for dx, dy in ((0, 1), (1, 0), (2, 0), (3, 0), (4, 1)):
            t.put(ox + dx, oy + dy, "#4078b8")
        t.put(ox + 2, oy, "#5078c8")
    for x, y in ((7, 1), (14, 8), (2, 14)):
        t.put(x, y, "#6088d0")
    for x, y in ((6, 7), (15, 3), (10, 11)):
        t.put(x, y, "#2d58a4")
    return t


def bajo():
    # Agua clara sobre un lecho de piedritas redondeadas.
    t = Tile("#4c84c8")
    for ox, oy, r in ((2, 3, 2), (10, 1, 1), (7, 9, 2), (13, 11, 1), (1, 12, 1), (12, 6, 1)):
        for dy in range(-r, r + 1):
            for dx in range(-r - 1, r + 2):
                if (dx / (r + 1.2)) ** 2 + (dy / (r + 0.4)) ** 2 <= 1:
                    t.put(ox + dx, oy + dy, "#6a9cd8")
        for dx in range(-r, r + 1):
            t.put(ox + dx, oy + r + 1, "#3c70b8")  # sombra bajo la piedra
        t.put(ox - r + 1, oy - r, "#8cb4e4")  # brillo arriba a la izquierda
    for x, y in ((5, 6), (15, 15), (9, 14)):
        t.put(x, y, "#a8c8ec")
    return t


def orilla():
    # Roca de la orilla, en los marrones del tileset.
    t = Tile("#805858")
    rng = random.Random(7)
    stones = [(2, 2), (8, 1), (13, 4), (5, 7), (11, 9), (1, 11), (7, 13), (14, 14)]
    for ox, oy in stones:
        w, h = rng.choice(((3, 2), (2, 2), (3, 3)))
        for dy in range(h):
            for dx in range(w):
                t.put(ox + dx, oy + dy, "#987070")
        t.put(ox, oy, "#b89088")
        for dx in range(w + 1):
            t.put(ox + dx, oy + h, "#604050")
        t.put(ox + w, oy + h - 1, "#604050")
    for x, y in ((4, 4), (10, 6), (15, 10), (3, 15)):
        t.put(x, y, "#d8b0a0")
    return t


def cascada():
    # Lámina de agua cayendo: vetas verticales claras y blancas sobre celeste, surcos oscuros.
    t = Tile("#88a0d8")
    rng = random.Random(3)
    for x in range(TILE):
        kind = x % 4
        if kind == 1:
            for y in range(TILE):
                t.put(x, y, "#a8c0e0")
        elif kind == 3 and x % 8 == 3:
            for y in range(TILE):
                t.put(x, y, "#6880d0")
        # Gotas largas blancas que, al desplazarse, se leen como agua cayendo.
        start, length = rng.randrange(TILE), rng.choice((3, 4, 5, 6))
        for y in range(length):
            t.put(x, start + y, "#d8e0e8" if y < length - 1 else "#a8c0e0")
        if kind == 1:
            t.put(x, start + 1, "#f8fcff")
    return t


def espuma():
    # Espuma: el alfa es la densidad (el tile muestra un píxel si su densidad supera un umbral).
    t = Tile("#ffffff")
    rng = random.Random(11)
    dens = [[0.0] * TILE for _ in range(TILE)]
    for _ in range(9):
        cx, cy, r = rng.randrange(TILE), rng.randrange(TILE), rng.uniform(2.0, 4.0)
        for y in range(TILE):
            for x in range(TILE):
                dx = min(abs(x - cx), TILE - abs(x - cx))
                dy = min(abs(y - cy), TILE - abs(y - cy))
                d = (dx * dx + dy * dy) ** 0.5
                dens[y][x] = max(dens[y][x], 1 - d / r)
    for y in range(TILE):
        for x in range(TILE):
            a = max(0.08, min(1.0, dens[y][x] + rng.uniform(-0.1, 0.1)))
            colour = "#f8fcff" if a > 0.7 else "#d8e0e8" if a > 0.4 else "#a8c0e0"
            t.put(x, y, (colour, int(a * 255)))
    return t


def pared():
    # Pared de roca que enmarca la cascada: estratos verticales como los acantilados del bosque.
    t = Tile("#805858")
    for x in range(TILE):
        if x % 5 == 0:
            for y in range(TILE):
                t.put(x, y, "#604050")
        elif x % 5 == 1:
            for y in range(TILE):
                t.put(x, y, "#987070")
    for x, y in ((2, 3), (3, 4), (7, 9), (8, 10), (12, 1), (13, 13), (12, 14)):
        t.put(x, y, "#b89088")
    for y in (6, 13):
        for x in range(1, 5):
            t.put(x + (y // 6) * 6, y, "#604050")
    return t


if __name__ == "__main__":
    makers = {"agua": agua, "bajo": bajo, "orilla": orilla, "cascada": cascada, "espuma": espuma, "pared": pared}
    image = Image.new("RGBA", (TILE, TILE * len(ROWS)), (0, 0, 0, 0))
    for row, name in enumerate(ROWS):
        tile = makers[name]()
        for y in range(TILE):
            for x in range(TILE):
                image.putpixel((x, row * TILE + y), tile.get(x, y))
    out = Path(__file__).resolve().parent.parent / "Assets" / "Sprites" / "River" / "rio_cauce.png"
    out.parent.mkdir(parents=True, exist_ok=True)
    image.save(out)
    print(f"{out}  ({len(ROWS)} materiales)")
