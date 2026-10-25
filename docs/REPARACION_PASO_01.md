# Reparación de la primera sala

## Entrega actual

La escena `DemoLevel1` contiene el paso del guardabosques: claro de entrada, sendero, roca móvil, bosque, límite rocoso y salida. El contenido se guarda en Edit; no se borra ni reconstruye el mapa al comenzar Play. `DemoMenu` contiene un fondo persistido sin componentes de progreso, combate o navegación.

El recorrido actual termina al cruzar el paso con el humano: muestra una conversación de cierre y conserva el checkpoint. Placas, captura de Pikachu, Electrode y campamento se reconstruirán después de revisar esta sala. Sus scripts anteriores permanecen como referencia, sin ejecutarse en esta escena.

## Organización

- `NeoGridBoard` posee ocupación y reservas; deriva la navegación de las mismas celdas. El equipo puede cruzarse; una roca no entra en una celda con un actor. El bosque y las paredes están reflejados en un Tilemap con collider de cuadrícula.
- `NeoGridMotor` comparte pasos cardinales para control directo y seguimiento. A* es el único planificador. `ProceduralGridMover` desplaza la cuadrícula de 32 × 20 alrededor del personaje controlado al alejarse cuatro casillas del centro; el tablero resincroniza suelo, paredes, rocas y conexiones tras cada desplazamiento. El rectángulo de navegación no limita el movimiento manual: se puede pisar el suelo pintado fuera de él. Las solicitudes de rutas se limitan a cinco por segundo, con actualizaciones locales tras empujar y descarte de callbacks de otra revisión o modo de control.
- `NeoFirstRoom` enlaza referencias serializadas, tutorial por acciones, entrega del compañero, reinicio y checkpoint. `NeoPushRock` implementa el empuje de una casilla. La salida requiere cruzar con el humano después de tomar el control de Machop; se aceptan distintas formas de apartar la roca.
- `DemoBootstrap` conserva el flujo uGUI, opciones y persistencia. El guardado usa versión 3; rechaza archivos del corredor previo sin modificarlos. Jugar conserva la confirmación para reemplazar el archivo. El tutorial y el checkpoint se reconstruyen en posiciones seguras.
- `MachopCompanion` es una variante del prefab original de Machop. La identidad de combate y sus animaciones corresponden a Machop.

## Herramientas

Abrir **Neo → Diseño Neo**. Seleccionar el tablero de la escena y un actor para ver ocupación, reservas, diferencias con colliders y ruta A*. La paleta muestra sprites exactos, estados, PPU, pivotes y huellas. La comprobación de rutas requiere Play; la comparación física funciona también en Edit.

`NeoRoomAuthoring.Build()` es una receta explícita de autoría en Editor, no un generador al iniciar la partida. Reemplaza únicamente la escena de demo y sus recursos propios; rechaza una escena con cambios sin guardar. Las escenas guardadas pueden editarse normalmente sin volver a ejecutarla. `CreateMenu()` deriva un fondo sin simulación a partir de la escena guardada.

## Evidencia de validación

- Compilación de scripts en Unity 6000.6.7f1 sin errores nuevos.
- Teclado real en el Editor: D avanzó exactamente una celda horizontal; W, E y Espacio permitieron acercarse, conversar y controlar a Machop. La prueba encontró y corrigió la pulsación inicial que saltaba una frase.
- 19 comprobaciones de integración pasaron en Play: identidad, paso cardinal, cambio diferido de control, capacidades, reservas, ocupación, actualización A*, pausa, agotamiento, umbral del 25 %, seguimiento y reinicio.
- Comparación de las 640 celdas: cero diferencias entre el tablero y colliders de obstáculos al estar en reposo.
- El informe reproducible queda en `Temp/neo-room-validation.json`. Para repetir: usar una partida nueva y `NEO_DEMO_SAVE_PATH` apuntando a un archivo de prueba; en Play ejecutar `NeoRoomValidation.Run()`. Este comprobador automatiza componentes; no reemplaza una partida mediante teclado real.
- El comprobador Python del plano inicial sigue disponible. Su resultado no sustituye el de Unity.

Las pruebas utilizan `Temp/neo-repair-test-save.json`; el guardado personal de versión 2 se conserva. Los archivos incompatibles y corruptos deben conservarse también en las pruebas del menú.

La compilación y ejecución Windows, comprobaciones de Continuar y capturas finales se registran al terminar la validación. No se considera aprobada la demo completa por terminar esta sala.

## Recuperación

Antes de editar se guardaron los archivos modificados/no versionados, el diff y la revisión Git de referencia en `../Neo-Recovery/20261024-234414/`. El escenario anterior está en `Assets/Scenes/Reference/DemoLevel1_Corredor.unity`. `SampleScene` y Dungeons no se modificaron durante esta reparación.

Sigue existiendo el error del comprobador de actualizaciones del paquete A* por conexión HTTP no permitida; es ajeno a la navegación y ya existía antes. No se alteró el código del proveedor para ocultarlo.
