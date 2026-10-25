# Orillas animadas (playas, lagos)

`NeoShoreTile` es un tile de agua que arma su propia costa. Se pinta **solo el agua**; cada casilla toma una banda según su distancia a tierra (0 = orilla, la última = agua abierta) y un borde hacia la banda menos profunda. Los bordes se componen por cuartos de casilla, así existen todas las esquinas, también las internas. Si se agranda o se borra agua, la orilla se recalcula sola.

Cada estilo de costa es un asset distinto del mismo tile, con su propia plantilla:

| Asset | Plantilla | Bandas | Uso |
| --- | --- | --- | --- |
| `Assets/Sprites/Shore/PlayaArena.asset` | `playa_arena.png` | 3: orilla con arena mojada y espuma, agua media, profunda | Playa de Bosque del Cauce |
| `Assets/Sprites/Shore/LagoPasto.asset` | `lago_pasto.png` | 2: orilla con espuma suave, profunda | Laguna del Bosque, orilla oeste |

El borde exterior de la orilla es transparente: muestra lo que haya debajo en Terrain (arena, pasto, roca). Por eso un mismo estilo sirve en distintos contextos.

## Pintar una playa

1. En la capa **Terrain**, pintar el suelo de la costa (arena, pasto…) también debajo de donde irá el agua.
2. En la capa **Water**, pintar el agua con el asset del estilo (arrastrarlo a una Tile Palette).
3. **Neo › Tiles › Sincronizar colliders de orillas**: bloquea en Colliders las bandas a partir de `walkableBands` (por defecto 1: solo la primera banda, donde todavía hay arena, se pisa) y abre las de orilla. `NeoShoreTile.Walkable` y `BandAt` sirven a otras herramientas que necesiten la misma regla.

El tile no agrega colisiones: el bloqueo sigue viviendo solo en Colliders, como en el resto del mapa.

## Crear un estilo nuevo

1. Agregar una entrada en `STYLES` de `tools/generate_shore_template.py` (colores, ancho de espuma, cuántas bandas, `inset` = píxeles de suelo visibles antes del agua) y ejecutar `python tools/generate_shore_template.py <estilo>`.
2. En Unity, clic derecho sobre el PNG → **Create › Neo › Tiles › Orilla desde esta plantilla**. Crea el asset y lo hornea.

La plantilla también se puede dibujar o retocar a mano. Layout por banda (filas) y frame (columnas): un bloque de 2 × 3 casillas de 16 px —arriba a la izquierda una vista previa, arriba a la derecha las cuatro esquinas internas, abajo una isla de 2 × 2 con esquinas externas, bordes y centro—. Los patrones deben repetirse cada 16 px para que las casillas encajen. Bandas y frames se deducen del tamaño de la imagen.

Después de cambiar una plantilla: botón **Hornear plantilla** en el Inspector del asset, o **Neo › Tiles › Hornear todas las orillas**. El horneado guarda la hoja y los sprites dentro del asset.

## Bosque del Cauce

La playa junto a Psyduck (x 46–56, y 13–20) reemplazó 86 casillas pintadas a mano con tiles estáticos de Pallete1 por `PlayaArena` sobre arena. Colliders: 26 casillas de la banda profunda; las dos bandas de orilla se caminan. Revisado con una captura de Edit; la animación (4 frames, 3 por segundo) se ve en Play.

## Reacción al pisar

Los assets de orilla están en el estilo *Water* de `NeoReactiveTiles` (Grid de Bosque del Cauce). Cada llegada de un `NeoGridMotor` —jugador o Pokémon, en todas las casillas de su huella— dispara una salpicadura: un anillo de espuma grueso y un segundo anillo que se abre detrás. El agua vecina reacciona más suave, y la superficie conserva sus ondas ambientales esporádicas.
