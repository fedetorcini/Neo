"""Genera plantillas animadas para NeoShoreTile (agua con orilla).

Uso:  python tools/generate_shore_template.py            # todos los estilos
      python tools/generate_shore_template.py playa_arena

Cada estilo produce Assets/Sprites/Shore/<estilo>.png. Layout por banda (filas) y frame
(columnas): un bloque de 2 x 3 casillas de 16 px.
    (0,0) vista previa      (1,0) esquinas internas, un cuarto por esquina
    filas 1-2: una isla de 2 x 2 casillas: esquinas externas, bordes y centro.
Todo patrón se repite cada 16 px, así los cuartos encajan entre casillas vecinas.
La plantilla se puede retocar a mano (Aseprite) respetando el layout; luego se hornea en Unity.
"""
import math
import sys
from pathlib import Path

from PIL import Image

TILE = 16


def rgba(hex_color, alpha=255):
    h = hex_color.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), alpha)


# Una banda: inset = píxeles de la casilla que deja ver lo de abajo antes del agua.
STYLES = {
    # Playa del bosque: arena mojada, espuma que va y viene, orilla clara, agua media y profunda.
    "playa_arena": {
        "frames": 4,
        "bands": [
            {"inset": 4, "fill": "#98a0b8", "sparkle": "#a8c0e0", "rim": ("#c89850", 150), "foam": "#f0f4f8", "foam_width": 2.0},
            {"inset": 2, "fill": "#7888c8", "sparkle": "#98a0b8"},
            {"inset": 2, "fill": "#5068d0", "waves": "#6880d0"},
        ],
    },
    # Lago entre pasto: sin arena mojada, espuma suave, dos bandas.
    "lago_pasto": {
        "frames": 4,
        "bands": [
            {"inset": 3, "fill": "#88a8d0", "sparkle": "#b0c8e8", "foam": "#d8e8f0", "foam_width": 1.2},
            {"inset": 2, "fill": "#5068d0", "waves": "#6880d0"},
        ],
    },
}


def signed_depth(x, y, inset):
    """Píxeles hacia adentro del agua desde su borde (negativo = fuera), en coordenadas del bloque."""
    cx, cy = x + 0.5, y + 0.5
    if y < TILE:
        if x < TILE:
            return 99.0  # Vista previa: todo agua.
        lx, ly = cx - TILE, cy
        corner = (0 if lx < 8 else TILE, 0 if ly < 8 else TILE)
        # Esquina interna: la tierra es solo la casilla diagonal; el agua la rodea en un cuarto de círculo.
        return math.hypot(lx - corner[0], ly - corner[1]) - inset
    ix, iy = cx, cy - TILE
    size = 2 * TILE
    radius = inset + 2
    lo, hi = inset + radius, size - inset - radius
    nx, ny = min(max(ix, lo), hi), min(max(iy, lo), hi)
    if (nx, ny) == (ix, iy):
        return radius + min(ix - lo, hi - ix, iy - lo, hi - iy)
    return radius - math.hypot(ix - nx, iy - ny)


def wave_pixels(frame, frames):
    """Pequeños arcos que avanzan en diagonal; período de 16 px."""
    shift = round(frame * TILE / frames)
    arc = [(0, 1), (1, 0), (2, 0), (3, 1)]
    pts = set()
    for row, offset in ((3, 0), (11, 8)):
        for dx, dy in arc:
            pts.add(((offset + dx + shift) % TILE, (row + dy) % TILE))
    return pts


def sparkle_pixels(frame, frames):
    pts = set()
    for i, (x, y) in enumerate(((2, 5), (9, 1), (13, 9), (5, 13))):
        if (frame + i) % frames < max(1, frames // 2):
            pts.add((x, y))
    return pts


def colour(band, depth, x, y, frame, frames):
    """Color de un píxel de una banda, o None si deja ver lo de abajo."""
    edge = band.get("rim")
    if depth < 0:
        if edge and depth >= -2:
            return rgba(edge[0], edge[1])
        return None
    foam = band.get("foam")
    if foam:
        # La espuma late con los frames y viaja a lo largo de la costa.
        phase = 2 * math.pi * (frame / frames + ((x + y) % TILE) / TILE)
        width = band["foam_width"] * (0.6 + 0.4 * math.sin(phase))
        if depth < width:
            return rgba(foam)
    elif depth < 1:
        return rgba(band["fill"]) if (x + y) % 2 == 0 else None  # Borde tramado hacia la banda anterior.
    px, py = x % TILE, y % TILE
    if "waves" in band and (px, py) in wave_pixels(frame, frames):
        return rgba(band["waves"])
    if "sparkle" in band and (px, py) in sparkle_pixels(frame, frames):
        return rgba(band["sparkle"])
    return rgba(band["fill"])


def generate(name, style):
    frames, bands = style["frames"], style["bands"]
    block_w, block_h = 2 * TILE, 3 * TILE
    image = Image.new("RGBA", (frames * block_w, len(bands) * block_h), (0, 0, 0, 0))
    pixels = image.load()
    for b, band in enumerate(bands):
        for f in range(frames):
            for y in range(block_h):
                for x in range(block_w):
                    c = colour(band, signed_depth(x, y, band["inset"]), x, y, f, frames)
                    if c:
                        pixels[f * block_w + x, b * block_h + y] = c
    out = Path(__file__).resolve().parent.parent / "Assets" / "Sprites" / "Shore" / f"{name}.png"
    out.parent.mkdir(parents=True, exist_ok=True)
    image.save(out)
    print(f"{out}  ({len(bands)} bandas, {frames} frames)")


if __name__ == "__main__":
    for style_name in sys.argv[1:] or STYLES:
        generate(style_name, STYLES[style_name])
