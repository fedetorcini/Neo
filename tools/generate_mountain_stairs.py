"""Genera las piezas de la escalera tallada en la roca (EscaleraMontana).

Uso:  python tools/generate_mountain_stairs.py

Produce Assets/Sprites/AniamtedTiles/MountainStairs.png: 4 x 4 casillas de 16 x 16.
La escalera se pinta dentro de una cresta de Mountain y se lee así: los escalones suben por la
cara de la roca (la fila de abajo) y arriba sigue un sendero tallado en la cima, a la altura del
terreno del otro lado. Las mejillas del corte usan la misma textura de la roca, así la cresta
sigue de corrido a ambos lados.

    filas:     0 arriba (T)   1 medio (I)   2 cara con escalones (F)   3 cresta de una fila (TF)
    columnas:  0 borde oeste  1 centro      2 borde este               3 escalera de una columna

Las texturas salen de Pallete1.png (roca 105/113/121, arena 289); se puede retocar en Aseprite
respetando la grilla.
"""
import re
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SHEETS = ROOT / "Assets/Sprites/AniamtedTiles"
OUT = SHEETS / "MountainStairs.png"
T = 16


def rgba(h):
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), 255)


# Rock (Mountain) and stone (Pallete1_207) colours.
ROCK_MID, ROCK_DARK, FACE_DARK, OUTLINE = rgba("b89088"), rgba("987070"), rgba("604050"), rgba("808088")
STONE = [rgba("f8f8f8"), rgba("d0d0d0"), rgba("b8b8b8"), rgba("808088"), rgba("686870")]
SAND_SHADE, SAND_DEEP = rgba("d0b068"), rgba("c89850")
DARKER = {STONE[0]: STONE[1], STONE[1]: STONE[2], STONE[2]: STONE[3], STONE[3]: STONE[4], STONE[4]: STONE[4]}


def palette_sprites():
    """Pallete1 sprites by name, cropped with the rects of its .meta (y counts from the bottom)."""
    meta = (SHEETS / "Pallete1.png.meta").read_text()
    img = Image.open(SHEETS / "Pallete1.png").convert("RGBA")
    out = {}
    for m in re.finditer(r"name: (\S+)\n\s+rect:\n.*?x: (\d+)\n\s+y: (\d+)\n\s+width: (\d+)\n\s+height: (\d+)", meta, re.S):
        name, x, y, w, h = m.group(1), *map(int, m.groups()[1:])
        out[name] = img.crop((x, img.height - y - h, x + w, img.height - y))
    return out


# Stone steps for one 16 px face: back shadow, tread with a bright nose, riser; two steps per cell.
STEP_ROWS = [4, 1, 1, 1, 0, 3, 3, 3, 2, 1, 1, 1, 0, 3, 3, 3]


def piece(src, row, col):
    left, right = col in (0, 3), col in (2, 3)
    face = row in (2, 3)
    rock = src["Pallete1_" + ("121" if face else "105" if row == 0 else "113")]
    sand = src["Pallete1_289"]
    im = Image.new("RGBA", (T, T))
    x0, x1 = (4 if left else 0), (11 if right else 15)
    for y in range(T):
        for x in range(T):
            if (left and x < x0) or (right and x > x1):
                c = rock.getpixel((x, y))  # same x as the neighbouring rock, so it continues seamlessly
            elif face:
                c = STONE[STEP_ROWS[y]]
                if y == T - 1:
                    c = OUTLINE  # foot of the stairs on the ground below
                elif left and x - x0 < 2:
                    c = DARKER[c]  # shadow cast by the west wall
            else:
                c = sand.getpixel((x, y))
                if left and x - x0 < 2:
                    c = SAND_DEEP if x == x0 else SAND_SHADE
            im.putpixel((x, y), c)
        # Edges of the cut: the west wall is in shade, the east one catches the light.
        if left:
            im.putpixel((x0 - 1, y), FACE_DARK if face else ROCK_DARK)
        if right:
            im.putpixel((x1 + 1, y), ROCK_DARK if face else ROCK_MID)
    if face and row == 2:
        # The carved path above ends in a lip over the first step.
        for x in range(x0, x1 + 1):
            im.putpixel((x, 0), SAND_DEEP)
    return im


def main():
    src = palette_sprites()
    sheet = Image.new("RGBA", (4 * T, 4 * T))
    for row in range(4):
        for col in range(4):
            sheet.paste(piece(src, row, col), (col * T, row * T))
    sheet.save(OUT)
    print("Escrito", OUT.relative_to(ROOT))


if __name__ == "__main__":
    main()
