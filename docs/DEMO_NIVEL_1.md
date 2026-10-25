# Neo: demo completa del nivel 1

> Estado actual: [primera sala reconstruida](REPARACION_PASO_01.md). El recorrido completo descrito debajo corresponde al alcance y al prototipo anterior; Pikachu, las placas y Electrode no están conectados a la escena reparada.

> Revisión de diseño: la implementación actual no está aceptada como demo. La [auditoría](AUDITORIA_DISENO.md) registra problemas confirmados de arte, identidad de actores y construcción espacial. El [flujo de diseño](FLUJO_DISENO_NEO.md) define la reconstrucción por salas y su validación. Los estados de implementación siguientes describen código existente, no calidad ni funcionamiento aprobados.

## Objetivo

Preparar una demo en español para Windows, de 10–15 minutos, que presente el control directo del compañero: explorar, resolver puzzles con Machop, debilitar y capturar a Pikachu, combatir a Electrode y llegar al campamento.

Neo es la base. `SampleScene` se conserva como prototipo de referencia. Dungeons aporta recursos concretos; no se integra el proyecto completo.

## Recorrido

1. **Guardabosques:** conversación opcional al empezar que enseña movimiento, control del compañero, energía e interacción.
2. **Puzzle 1:** Machop empuja una roca una casilla hasta la primera placa.
3. **Puzzle 2:** una roca recorre el corredor hasta una placa y abre la puerta.
4. **Puzzle 3:** dos rocas deben llegar a dos placas en cámaras separadas.
5. **Pikachu:** se combate apuntando con el mouse; al 25 % de vida deja de atacar y puede capturarse con E.
6. **Electrode:** alterna embestida anunciada y descarga circular anunciada.
7. **Campamento:** conversación final y cierre de la demo.

El nivel usa posiciones fijas y se construye al cargar la escena de juego. Cada puzzle se puede reiniciar desde pausa si todavía no fue completado.

## Controles

| Acción | Control |
| --- | --- |
| Caminar por casillas | WASD |
| Apuntar | Mouse |
| Atacar con el compañero | Clic izquierdo |
| Interactuar / capturar | E |
| Alternar personaje y compañero elegido | Espacio |
| Elegir Machop / Pikachu | 1 / 2 |
| Pausa | Escape |

El humano no ataca y queda protegido mientras se controla al Pokémon. La energía baja con la distancia y también mientras el compañero está quieto bajo control directo. Al agotarse, el control vuelve al humano y el Pokémon regresa. Recupera 10 puntos por segundo mientras no está controlado y se puede seleccionar de nuevo desde 25 %.

## Pantallas y guardado

- `DemoMenu` abre con splash saltable de dos segundos, menú y personajes animados.
- `DemoLevel1` es el recorrido jugable; ambas escenas están antes de `SampleScene` en Build Settings.
- El menú ofrece Jugar, Continuar, Opciones y Salir. Empezar de nuevo pide confirmación si ya existe un archivo.
- La pausa ofrece reanudar, controles, reinicio del puzzle actual, opciones y regreso al menú.
- Hay opciones para volumen y pantalla completa.
- El guardado JSON versionado se escribe en `Application.persistentDataPath/neo-demo-save.json`. Registra tutorial, checkpoint, puzzles, captura y jefe; las posiciones de objetos y actores se reconstruyen desde un lugar seguro.
- Un guardado corrupto o incompatible se conserva para revisión; Continuar queda deshabilitado hasta empezar una partida nueva por confirmación.

## Recursos gráficos

La implementación usa los recursos locales de `Assets/Resources/Demo/Art`: Pikachu idle y ataque, Electrode, su animación de derrota, el guardabosques y la roca. Se conservaron los metadatos de importación de las hojas de sprites. Spriters Resource queda como referencia de arte indicada por el autor; no se descargaron archivos desde el sitio.

## Implementación y validación

| Hito | Estado |
| --- | --- |
| Base recuperable | `SampleScene` queda como referencia y los cambios locales del autor se conservaron. |
| Entrada y salida | Splash, menú, opciones, carga asíncrona, pausa, escena de nivel y regreso al menú implementados. |
| Control y HUD | Movimiento cardinal, puntería, ataque, selección, energía, salud y bloqueo de entradas en UI implementados. |
| Introducción y puzzles | Guardabosques, tutorial, tres puzzles, puertas y reinicio seguro implementados. |
| Ciclo completo | Pikachu, captura guiada, Electrode, derrota, reintento y final implementados. |
| Persistencia | Guardado JSON atómico, Continuar y reconstrucción por checkpoint implementados. |
| Demo presentable | Pendiente de prueba completa en Play y ejecutable, ajuste visual y pruebas con visitantes. |

Los scripts del proyecto compilan con Unity 6.6: comprobación incremental con **0 errores y 0 advertencias**. Esto no valida la importación final de escenas, el modo Play ni el ejecutable. Antes de dar la demo por terminada, probar en Unity:

- iniciar desde menú, completar el recorrido y volver a jugar;
- terminar puzzles con Machop y verificar que no se puedan empujar varias rocas juntas;
- agotar energía y confirmar el regreso automático y el umbral de 25 %;
- atacar sin que los clics de UI lleguen al mundo;
- capturar Pikachu una sola vez y conservarlo tras Continuar;
- derrotar al jefe con cada compañero, perder salud y reintentar;
- probar archivo ausente, corrupto e incompatible;
- revisar 1920×1080, 1280×720 y 4:3;
- generar y ejecutar una build Windows sin errores nuevos en consola.

La duración de 10–15 minutos, la legibilidad en varias resoluciones y el equilibrio del combate siguen sin medirse en una sesión de juego.
