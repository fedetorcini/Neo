# Cómo diseñaremos y comprobaremos Neo

> Convención vigente confirmada por el autor el 25 de octubre: cuatro capas, con colisión estática en Colliders, terreno pisable en Terrain y Terrain LV1, y dibujo por encima del jugador en Foreground. La inspección viva y las pruebas están en [AUTORIA_AUTONOMA.md](AUTORIA_AUTONOMA.md).

## 1. De historia a lugar

Antes de pintar, describir para cada espacio: qué ocurrió, qué quiere el jugador, qué acción aprende, qué impide avanzar, qué cambia al resolverlo y qué se ve al entrar. Dibujar conexiones y distancias, no una fila de salas idénticas.

Recorrido propuesto: puesto del guardabosques → paso estrecho por el desprendimiento → control de acceso de mantenimiento (una placa) → cámara de mantenimiento (dos placas) → claro donde se refugió Pikachu → explanada del Electrode → campamento. Las placas pertenecen a instalaciones reconocibles; se necesitan palancas/compuertas coherentes antes de vestir esas salas. Evitar introducir incendio sin función narrativa.

El primer plano, [design/paso_01.json](design/paso_01.json), es una maqueta mecánica de una sala, aún sin medir cámara, energía o duración. No sustituye el mapa final. Incluye una solución de referencia: Machop llega debajo de la roca, la empuja al hueco superior y libera el paso. El guardabosques se sitúa en un ensanchamiento anterior, sin bloquear la ruta. El humano espera en una celda lateral segura y luego puede atravesar el paso.

## 2. Paleta semántica antes de decoración

Registrar por recurso: archivo y GUID, nombre exacto del subasset, estado representado, tamaño en píxeles, PPU, pivote, animación/dirección, huella en celdas y orden de dibujo. Ver la hoja completa y los recortes antes de elegir. En una previsualización de recortes, convertir el origen inferior izquierdo de Unity al origen superior izquierdo de la imagen.

| Familia | Uso permitido | Pendiente de comprobar |
| --- | --- | --- |
| `NeoGrassA` | Suelo transitable | Unión con caminos y escala |
| `NeoForestPath` | Sendero; su apariencia no añade bloqueo | Bordes y vecinos del RuleTile |
| `NeoTrees/tree_0..7` | Ninguno aprobado automáticamente | Separar normal, fuego y otros estados por inspección; seleccionar por identidad explícita |
| `MovableRock` | Roca móvil del puzzle | Diferenciarla visualmente del límite rocoso fijo |
| Machop | Compañero que empuja | Prefab y clips reales; no reutilizar Haunter cambiando el nombre |

No usar el índice de una hoja como significado. Si falta un recurso, dejarlo identificado en la maqueta y registrar la carencia. No inventar una equivalencia visual definitiva.

## 3. Un contrato espacial

### Estructura de escena confirmada por el autor

Usar la organización actual de cuatro capas, confirmada por el autor el 25 de octubre de 2026:

```text
Grid
├── Colliders   Tilemap + TilemapRenderer + TilemapCollider2D
├── Terrain     Tilemap + TilemapRenderer
├── Terrain LV1 Tilemap + TilemapRenderer
└── Foreground  Tilemap + TilemapRenderer
```

`Terrain` contiene el terreno base pisable. `Terrain LV1` también es pisable y permite dibujar terreno sobre otro terreno; no implica por sí solo una altura física distinta. `Foreground` se dibuja por encima del jugador, como las copas. Toda colisión estática se pinta mediante bloques en `Colliders`, debajo de las huellas de árboles, piedras y otros obstáculos; no añadir colliders a las capas de dibujo. Inspeccionar y reutilizar las sorting layers y máscaras del prototipo; no confundir una sorting layer con una layer de física.

Los elementos estáticos se componen con los tiles existentes en las capas correspondientes, con su huella de bloqueo en Colliders cuando haga falta. No se crea un objeto individual por árbol ni un Tilemap distinto para cada decoración. Personajes, rocas móviles y puertas mantienen objetos propios por su comportamiento.

Terrain y Terrain LV1 expresan suelo pisable. La navegación toma los bloqueos de Colliders y de los obstáculos dinámicos dentro del recorrido diseñado, sin inferir una segunda máscara física del dibujo. No llenar todo el exterior del recorrido de colliders como sustituto de diseñar el contorno.

**Comprobación de autoría:** inspeccionar las cuatro capas por separado, compararlas con sus vistas de referencia y volver a mostrar la composición completa. Verificar que el actor pise Terrain y Terrain LV1, quede detrás de Foreground y choque con las huellas de Colliders. La visibilidad de Scene View es una herramienta de inspección, no un estado de gameplay.

**Estado de adaptación:** la convención de cuatro capas está incorporada al flujo y a la skill. No ejecutar generadores ni migraciones que impongan la receta antigua de tres capas; inspeccionar sus resultados y adaptarlos antes de usarlos sobre el nivel actual. No se modifican escenas por actualizar esta documentación.

Una celda mide una unidad y su centro es `(x + 0.5, y + 0.5)`. Un desplazamiento manual cruza una sola arista cardinal. Reservar destino al iniciar el movimiento; al llegar, confirmar la ocupación. Al cancelar/reiniciar, liberar reservas. Elegir y documentar cómo pasan los actores entre sí; no dejarlo a máscaras accidentales.

| Elemento | Humano / Machop / Pikachu | Navegación y dibujo |
| --- | --- | --- |
| Suelo | Pasan | Transitable |
| Roca fija, tronco, pared | Bloquean | Huella explícita; la copa se dibuja encima sin ampliar esa huella |
| Roca móvil | Bloquea; solo Machop controlado empuja | Reserva origen/destino durante el empuje; sin cadenas ni tirar |
| Puerta cerrada / abierta | Bloquea / deja pasar | Estado visible y ocupación cambian juntos; resuelta queda abierta |
| Placa | Pasan | Detecta ocupación estable; no es una pared |
| Integrantes del equipo | Pueden cruzarse entre sí | El humano no recibe daño durante control del compañero; una roca no puede ocupar la celda de un actor |

Movimiento y planificador deben compartir las mismas restricciones físicas. En la estructura del autor, Colliders define las restricciones fijas; rocas y puertas aportan las dinámicas. Las reservas temporales complementan ese estado durante movimientos. Evitar dos mapas de obstáculos mantenidos a mano. A* existente es el candidato inicial para seguimiento y regreso; comprobar límites y capacidad de actualización antes de decidir sustituirlo. El movimiento manual no necesita calcular una ruta.

Empuje/puerta: validar → reservar → animar → confirmar estado → sincronizar física → actualizar navegación afectada → invalidar rutas antiguas → emitir evento local. Nunca aceptar un callback de ruta perteneciente a un estado de control o revisión de mapa anterior. Recalcular por cambio de destino, bloqueo o revisión del mapa, con frecuencia medida. No confundir mover el grafo, pedir una ruta y actualizar obstáculos.

## 4. Autoría y revisión visual

Construir una escena que se vea y pueda editarse fuera de Play con `Grid/Colliders`, `Grid/Terrain`, `Grid/Terrain LV1` y `Grid/Foreground`. Mantener los objetos interactivos separados de la pintura estática. Un generador puede hornear un plano explícito en Edit respetando esa organización; no borrar el mapa de referencia al arrancar la partida.

Para cada sala guardar tres vistas con el mismo encuadre: arte, colisiones/ocupación y recorrido/objetivos. Verificar escala de actor/puertas, bordes, oclusión de copas, sombras, puntos de interés y salida. Evitar decoraciones que parezcan interactuables o bloqueos que parezcan transitables. Comprobar lo que ve el jugador al entrar, no solo el mapa completo desde arriba.

## 5. Herramientas y flujo de trabajo

Ya están disponibles el Editor conectado y Unity CLI para inspección, ejecución y capturas. En esta revisión no se instala ningún plugin: esas capacidades permiten construir las herramientas de autoría necesarias.

| Herramienta | Entrega / estado | Qué demuestra |
| --- | --- | --- |
| Skill `neo-level-design` | Creada en las skills locales | Procedimiento reutilizable; se puede invocar con `$neo-level-design` |
| Comprobador de plano | `tools/validate_puzzle.py` | Solución por pasos cardinales, sin tirar ni empujar cadenas; secuencia de referencia reproducible |
| Paleta de assets etiquetada | Especificada; falta implementar ventana de Editor | Ver subassets y estados antes de colocarlos |
| Overlay de ocupación | Especificado; falta implementar en Editor | Verde transitable, rojo bloqueado, naranja reservado; diferencias con física marcadas |
| Previsualización de rutas | Especificada; falta implementar en Editor | Inicio/destino, revisión del mapa, ruta y actor/capacidad usados |

Las dos ventanas pueden ser un único panel “Diseño Neo” en una siguiente implementación. Preferir herramientas pequeñas sobre los assets existentes. No cambiar paquetes ni reemplazar A* hasta identificar una limitación comprobada. Generación de imágenes puede ayudar con referencias de composición; un render bonito no valida un Tilemap ni sus colisiones.

## 6. Cierre de una sala

1. El comprobador encuentra solución y reproduce la secuencia de referencia.
2. En Unity, recorrerla con WASD real, incluso dos teclas juntas y cambios a mitad de paso; no aparecen diagonales ni saltos.
3. Verificar las cuatro caras de cada obstáculo y las reservas durante un empuje; humano y Pikachu no empujan; no se atraviesan rocas ni se empujan cadenas.
4. Seguir y volver agotado antes y después de mover la roca. Un bloqueo produce aviso y reinicio seguro; no una ruta por una pared.
5. Reiniciar durante y después del empuje; comparar posiciones, ocupación, colliders y rutas. Pausa y diálogos congelan todo.
6. Capturas con arte y overlay en 1920×1080, 1280×720 y 4:3. HUD legible, salida visible, identidad/animación coherentes.
7. Registrar resultado observado, captura/log y pendientes. Compilar no equivale a aprobar estas comprobaciones.

Para ampliar al nivel completo: verificar cada checkpoint, encuentro reiniciable y guardado; jugar desde cero y Continuar en Windows. Medir duración con otra persona. La revisión visual debe hacerse tras cada sala, antes de decorar la siguiente.
