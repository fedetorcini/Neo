# Contexto de Unity: movimiento y animación

Revisión acotada: 25 de octubre de 2026. Proyecto: Neo, carpeta hermana de Dungeons-Pokemon-Adventure. Se identificó `DemoLevel1` tanto en el Editor conectado como en el video del jugador.

## Sistemas confirmados

- Unity 6000.6.7f1. Escena activa durante la revisión: `Assets/Scenes/DemoLevel1.unity`.
- Entrada de movimiento mediante los ejes del Input Manager en `Assets/Scripts/Controller.cs`.
- `NeoGridBoard` posee ocupación, reservas y navegación cardinal mediante A*.
- `NeoGridMotor` mueve tanto al humano como al compañero; `PlayerMovement` y `PartnerMovement` delegan en él cuando está presente.
- La animación usa Animator sobre el objeto visual y los parámetros `RunHorizontal`, `RunUp` y `RunDown`.
- `CameraScript` sigue directamente al actor en LateUpdate, conservando Z. En DemoLevel1 y BosqueDelCauce 3 se desactivó clampToLevel: el encuadre y PixelPerfectCamera podían mantener fija la cámara dentro del rectángulo original. El motor avanza en Update después de los controles, mediante DefaultExecutionOrder(100).
- La escena se construye en Editor; no regenerarla para corregir movimiento. Ver `REPARACION_PASO_01.md`.
- Unity Pipeline está disponible mediante la CLI local. El Editor estaba en Play y sus frames avanzaban.
- Existen cambios locales previos extensos. Preservarlos; no reconstruir las escenas para modificar la cámara o la navegación.
- No se auditó el proyecto completo ni se confirmó aquí el render pipeline.

## Corrección de los tirones

El control aplicaba 0,16 segundos de espera incluso si el motor rechazaba una solicitud por seguir en movimiento. Con pasos de 0,2 segundos eso podía posponer el próximo paso hasta unos 0,33 segundos. El motor además apagaba la caminata al llegar a cada casilla. Una distancia restante inferior a 0,001 podía apagarla antes de llegar.

Ahora el control aplica la espera solo si se acepta el paso. El motor mantiene una dirección estable durante ese paso y conserva la caminata al llegar, hasta comprobar en el siguiente Update si continúa o queda quieto.

## Validación realizada

- Recompilación del Editor completada sin errores de compilación.
- Prueba en Play con objetos temporales aislados: tres pasos en cada dirección, caminata activa en el evento de llegada, vuelta a idle al detenerse, rechazo del borde del tablero y teletransporte que detiene movimiento y animación. Siete comprobaciones correctas; objetos de prueba eliminados.
- La prueba detectó el corte por distancia inferior a 0,001 antes de aplicar la dirección estable; pasó después de esa corrección.
- Modelo del intervalo de repetición a 30, 60 y 144 FPS: aproximadamente 0,333 segundos antes y 0,200–0,201 después. Es una simulación del temporizador, no una medición visual del juego.
- Informe local de la prueba: `Temp/player-animation-validation.json`; Temp es transitorio.
- La escena continuó en Play, sin cambios pendientes de guardar. No se modificaron escenas, prefabs, clips ni guardados.
- Ya existían errores del comprobador de actualizaciones de A* por una conexión HTTP insegura y advertencias de paquetes; no se investigaron como parte de esta corrección.

## Comprobación visual pendiente

En DemoLevel1, mantener una dirección durante varias casillas; verificar ritmo continuo y ausencia de destellos de idle. Repetir arriba, abajo y a ambos lados; girar, soltar la tecla y caminar contra un obstáculo. Repetir con Machop después de obtenerlo y comprobar cambio de control, pausa y seguimiento. Las comprobaciones automáticas del motor no sustituyen esta valoración visual con el teclado.

## Cámara y navegación móviles: segunda revisión

- Se confirmó en la escena real que el encuadre limitado mantenía la cámara fija; corregir solo CameraScript no había resuelto esa configuración. DemoLevel1 conserva ahora al explorador como instancia de `Assets/Prefabs/PlayerWithCamera.prefab`, con `PlayerCamera.prefab` anidado, target interno y clamp desactivado. Controller enlaza cámara y objetivo de navegación tanto al iniciar como al cambiar de control. El motor resuelve el tablero de la escena si el prefab no tiene una referencia asignada.
- `NeoGridBoard.StaticBlocked` usa el suelo pintado y Colliders, no el rectángulo del grafo como pared invisible. El grafo mantiene 32 × 20 nodos y sigue al personaje controlado mediante el ProceduralGridMover ya incluido, con umbral de cuatro casillas. Tras desplazar nodos se reconstruyen sus coordenadas, walkability y conexiones, y se invalidan rutas anteriores. Las rocas conservan actualizaciones locales.
- `NeoExplorationScene` inicializa el bosque sin construir el curso anterior de la demo ni abrir su menú. La composición final es `Assets/Scenes/BosqueDelCauce 3.unity`; los archivos sin sufijo, 1 y 2 son iteraciones anteriores. El mapa y su arte se conservaron. Pokémon y situaciones del bosque continúan como referencias visuales.
- Pruebas en Play sobre las escenas guardadas: seis comprobaciones de cámara y navegación en cada escena. En DemoLevel1 el jugador caminó de (5,6) a (-3,6), fuera del rectángulo anterior. En el bosque caminó de (39,4) a (39,12). Cámara real con PixelPerfectCamera: viewport (0,50; 0,50) en ambos casos. Se comprobó desplazamiento del grafo, concordancia de todos sus nodos con terreno y obstáculos y rechazo de suelo vacío; en el bosque también rechazo de pared. En DemoLevel1 se verificó el cambio de objetivo de cámara y navegación a Machop.
- Las 19 comprobaciones existentes de NeoRoomValidation pasaron después: cambio diferido de control, empuje, reservas, pausa, energía, seguimiento y reinicio. Compilación del Editor sin errores nuevos. Persisten avisos del comprobador HTTP de A* ajenos a este cambio.
- Informes persistidos en `docs/design/demo-camera-movement-validation.json`, `demo-room-validation.json` y `bosque-exploracion-validacion.json`. Capturas de juego revisadas en `Temp/DemoLevel1-camera-follow.png` y `Temp/BosqueDelCauce 3-camera-follow.png`.
- Regresión repetible: `tools/validate_camera_movement.csx`, mediante Unity Pipeline script eval en Play. Requiere `NEO_DEMO_SAVE_PATH` aislado; mueve a los actores de prueba y en DemoLevel1 entrega a Machop. No ejecutarlo sobre el guardado normal.
- Para reutilizar el actor: colocar una sola instancia de PlayerWithCamera en una escena con NeoGridBoard y Controller; incluir su PlayerAI en controllablesManual. Enlazar el tablero al suelo, Colliders y AstarPath. Usar NeoExplorationScene para exploración independiente o NeoFirstRoom para la demo. El prefab incluye la cámara y sus enlaces internos; no incluye el mapa ni el controlador de escena.
