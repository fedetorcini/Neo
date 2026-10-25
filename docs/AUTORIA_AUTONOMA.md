# Autoría autónoma de mapas en Unity

## Objetivo del autor — 25 de octubre de 2026

Codex debe aprender del nivel real y construir, inspeccionar y corregir mapas en Unity sin que el autor tenga que pintar por él, proporcionar cada captura o dirigir cada colocación. Los mocks son referencias; la entrega de autoría es una escena persistida y revisada en el Editor.

## Convención confirmada por el autor

Regla de autoría para las próximas sesiones, confirmada explícitamente el 25 de octubre de 2026:

| Capa | Responsabilidad |
| --- | --- |
| Colliders | Todos los bloqueos estáticos, con bloques pintados debajo de las bases de árboles, piedras y demás obstáculos |
| Terrain | Terreno base que puede pisar el jugador |
| Terrain LV1 | Terreno también pisable, superpuesto a otro terreno para componer capas |
| Foreground | Elementos que se dibujan por encima del jugador |

Toda nueva colisión estática se resuelve en Colliders. Terrain LV1 no es foreground ni una altura física automática. Mantener la física separada del dibujo: una huella bloqueada se representa en Colliders, aunque tenga arte sobre el terreno. Esta regla sustituye la receta anterior de tres Tilemaps. La tabla de uso observado de abajo documenta el estado inspeccionado, no una redefinición de estas responsabilidades.

## Referencia inspeccionada antes de la aclaración

Proyecto: Neo. Escena abierta: `Assets/Scenes/DemoLevel1.unity`. Se consultó el estado vivo del Editor mediante Unity CLI/Pipeline; había cambios sin guardar. La continuación se guardó como copia, conservando la escena abierta y su estado pendiente.

La versión actual tiene cuatro Tilemaps bajo Grid. Esta observación actualiza la receta anterior de tres capas; conservar lo construido por el autor, sin ejecutar una migración para imponer la estructura anterior.

| Tilemap | Sorting layer / order | Uso observado |
| --- | --- | --- |
| Terrain | Terrain / -10000 | Suelo, senderos, escalera, roca, agua y bases de árboles |
| Terrain LV1 | Terrain / -9999 | Superposición de flores, vegetación y piezas de roca; no es exclusivamente altura |
| Foreground | Foreground / 0 | Piezas superiores de árboles |
| Colliders | Collider / 0 | Tiene TilemapCollider2D; estaba vacío al inspeccionarlo |

La prioridad entre estos Tilemaps no sustituye el diseño de elevación ni define transitabilidad. La continuación visual todavía no está validada como recorrido jugable.

## Vocabulario comprobado

- Sendero: reutilizar `Assets/Sprites/AniamtedTiles/PathTile.asset`, preservando sus reglas.
- Pasto: hay al menos tres referencias usadas como suelo: `Assets/Pallete/Pallete1_1.asset`, `Assets/Resources/Demo/Art/NeoGrassA.asset` y `Assets/Pallete/Tilesets/safari zzone part 1_178.asset`. No identificar suelo solo por el nombre de una referencia.
- Árbol observado: base de dos celdas con `Pallete1_486.asset` y `Pallete1_487.asset`; fila media `treeForeground_2.asset`, `treeForeground_3.asset`; fila superior `treeForeground_0.asset`, `treeForeground_1.asset`. Las piezas están en `Assets/Pallete/`. Componer en Tilemaps, no como objetos por árbol.
- Flores: `Assets/Sprites/AniamtedTiles/AnimatedFlowers.asset` en la superposición, sin sustituir el suelo.
- Escalera observada en Terrain: columnas 13–14, filas -5 y -4, tile `Pallete1_207`. La salida norte atraviesa suelo safari en filas -3 a -1; incluir esas celdas al conectar un sendero.
- El catálogo de referencias, cantidades y coordenadas de ejemplo está en `design/catalogo_autoria_20261025.json`. Es un inventario observado, no una aprobación semántica automática de todos los sprites.

## Procedimiento para próximas sesiones

1. Consultar el Editor y confirmar proyecto, escena, Play y cambios pendientes. La CLI local se encuentra en `C:/Users/fedet/AppData/Local/Unity/bin/unity.exe`; comprobar su disponibilidad en cada entorno.
2. Inspeccionar Tilemaps y referencias reales antes de modificar. Capturar ejemplos completos, con posiciones, transformaciones y colores si los tienen. Confirmar que troncos, copas y bordes forman la figura esperada.
3. Definir entrada, recorrido, lugar de interés y salida. Conservar el trabajo manual; usar una copia revisable si se está explorando una composición.
4. Pintar en Edit con assets existentes y una lista explícita de piezas. Comprobar ocupación antes de superponer árboles o modificar suelo. Evitar decoración aleatoria sin función espacial.
5. Revisar el dibujo compuesto y sus capas desde Unity. Preferir una escena de preview con solo el Grid para las capturas: abrir dos escenas completas aditivamente ejecuta componentes ExecuteInEditMode, incluso fuera de Play. Aislar el renderizado y no guardar cambios temporales de cámara o layers.
6. Corregir cortes, esquinas, superposiciones y escalas detectados visualmente antes de entregar.
7. Pintar y comprobar colisiones como un paso explícito. Añadir interacción y navegación solo después de conocer el recorrido; validar en Play antes de llamar al nivel jugable.
8. Registrar la escena, evidencia, fallos encontrados y pendientes para reutilizar el aprendizaje. No depender de que el autor explique otra vez la organización.

## Primera prueba autónoma

Se creó `Assets/Scenes/Reference/DemoLevel1_ClaroNorte.unity` desde el estado vivo del autor. Se extendió el sendero desde la escalera a un claro norte, con 13 árboles compuestos y 7 celdas de flores. La primera captura reveló seis celdas omitidas de pasto safari: se corrigió la conexión en la copia y se volvió a renderizar.

Evidencia: `design/evidencias/claro-norte-unity.png`, render de los Tilemaps reales mediante una cámara temporal de Unity, sin generación de imágenes. Es una prueba pequeña de autoría visual, no el mapa final. No se añadieron encuentros ni se validaron física, navegación o jugabilidad; Colliders sigue vacío como en la referencia.

La revisión de consola detectó avisos de AstarPath duplicado al abrir la copia aditivamente, porque usa ExecuteInEditMode. Se cerró la copia y se restableció explícitamente AstarPath.active al único componente de la escena original. Se comprobó al finalizar: una escena abierta, un AstarPath, referencia activa válida y cambios pendientes del autor conservados. Los avisos históricos no se borraron. Para siguientes pruebas no cargar copias completas aditivamente: usar preview de Grid o un Editor aislado. Esta sesión demuestra la autoría y revisión visual, no una validación limpia de gameplay.

## Colisiones indicadas por el autor

El autor confirmó que las bases de árboles y piedras deben llevar bloques cuadrados por debajo, pintados en Colliders. Las copas y flores no amplían la huella. Guardado en `Assets/Scenes/Reference/DemoLevel1_ClaroNorte.unity`:

- 44 celdas de base de árbol (22 árboles, dos celdas por base).
- 93 celdas de piedra y paredes rocosas, incluidas las nueve celdas de pared junto a la laguna (`Pallete1_380`). No deducir materiales solo por el número de sprite: contrastar el resultado con el render.
- 137 bloques en total con `BoundaryCell`, collider de tipo Grid, TilemapCollider2D activo y no trigger, en la layer física Collider. Su sorting layer Collider se dibuja debajo del terreno; el rojo de la evidencia es solo una superposición temporal para revisión.
- Los límites de NeoGridBoard se ampliaron al terreno actual: origen (-17,-12), tamaño (64,45), para no rechazar por límites antiguos los pasos hacia el claro. NeoGridBoard ya consume walls=Colliders para movimiento y navegación.

Se comprobó la presencia de forma física en el centro de los 137 bloques y su correspondencia con StaticBlocked; las 115 celdas de PathTile y escalera Pallete1_207 siguen libres de bloqueo estático y de esa forma física. Los actores no arrancan en una huella estática. Esto verifica la configuración en Edit; no constituye una partida recorrida en Play ni valida las mecánicas de agua.

Receta de autoría reutilizable: `tools/paint_static_colliders.cs.txt` (C# para eval del Editor, restringido a esta escena). Informe: `design/colliders_claro_norte_20261025.json`. Evidencia: `design/evidencias/claro-norte-colliders.png`. La escena original estaba guardada al comenzar este paso; se abrió la copia en modo Single, evitando cargar dos AstarPath. La copia queda abierta y guardada.
