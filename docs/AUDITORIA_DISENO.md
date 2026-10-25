# Revisión del diseño de la demo — 2026-10-24

La implementación actual es un prototipo de sistemas. No cumple la calidad visual, espacial ni de integración necesaria para presentar la demo. La responsabilidad de estas decisiones es de la implementación realizada por Codex.

## Evidencia y prioridades

| Prioridad / certeza | Hallazgo | Evidencia | Corrección y comprobación |
| --- | --- | --- | --- |
| Alta / confirmado | Se usan estados de árbol con fuego como variaciones de bosque normal. | `DemoAdventureCourse.cs:251` selecciona todos los sprites por módulo. Captura `Temp/Neo-demo-assets.png`. | Catalogar subassets por estado; usar una lista explícita de árboles normales. Revisar cada sprite y una captura de composición. |
| Alta / confirmado | El HUD identifica como Machop a un actor con apariencia de Haunter. | Captura; `FindMachop`, línea 101, devuelve el primer PartnerAI; `ConfigureStarter`, línea 160, cambia solo el enum. La escena referencia `animations/Hunter/Hunter.controller`. | Prefab de Machop con identidad, apariencia y capacidades concordantes. Verificar Inspector, animaciones y HUD juntos. |
| Alta / confirmado | Lo visible y lo bloqueado se construyen por separado. Hay límites sin representación. | `CreateWall`, línea 578, oculta renderers de los límites superior/inferior; `CreateTree` no crea bloqueo. | Autoría de huellas físicas explícitas; superponer celdas, colisión y arte. Comprobar bordes caminando por ambos lados. No toda copa debe bloquear: debe hacerlo la huella del tronco. |
| Alta / confirmado | La demo fuerza búsqueda local adicional y no integra la navegación con los cambios de obstáculos. | `ConfigureStarter:157` y creación de Pikachu llaman `UseLocalFallback`; `PartnerMovement.SetFallbackStep` hace BFS con consultas físicas. `DemoPuzzleRock` sincroniza transforms, pero no notifica rutas; `DemoCourseGate` cambia el collider sin invalidarlas. | Elegir un único proveedor de rutas para seguimiento/regreso; cambios de roca/puerta deben actualizar ocupación e invalidar rutas afectadas. Probar abrir, cerrar, empujar y regresar agotado. La incidencia exacta de rutas obsoletas aún requiere reproducción. |
| Media / confirmado | Se sustituyó el escenario por una franja de habitaciones repetidas sin composición ni explicación espacial. | `BuildBoard`, `PaintNeoTerrain`, divisores en x=9.5,19.5,31.5,41.5; captura. | Diseñar primero lugares con propósito, conexiones y encuadres; validar una sala antes del recorrido completo. |
| Media / confirmado | El diseño real solo aparece en Play. El prototipo se oculta masivamente. | `PaintNeoTerrain:204` borra tiles en runtime; `HidePrototypeStage:109` desactiva renderers/colliders por búsqueda global. | Escena de nivel dedicada y persistida; objetos y capas revisables en Edit. Aislar el prototipo como referencia. Auditar componentes de IA que permanecen activos, no solo sus renderers. |
| Media / confirmado | Se cambió el primer puzzle del plan: despejar un paso pasó a activar una placa. | `BuildPuzzles:269`; comparación con el plan aprobado. | Volver al aprendizaje de empujar/despejar; introducir placas en el segundo puzzle. |
| Media / confirmado | La UI pierde legibilidad sobre el bosque y conserva barras del prototipo. | Captura conservada. | Paneles con contraste y jerarquía; mostrar información del actor correcto; revisar 16:9 y 4:3. |

## Qué no está demostrado

- No se ha reproducido una actualización de IA «cada minuto». `ProceduralGridMover.Update` comprueba distancia cada frame y mueve el grafo al superar un umbral; `PlayerMovement.UpdatePath` solicita rutas cuando el Seeker termina. Son tareas distintas. Medir solicitudes, reconstrucciones, latencia y coste antes de tocar frecuencias.
- La compilación y las llamadas aisladas a `MoveManual` no demuestran una partida correcta. Falta validar entradas reales, transición de control a mitad de casilla, callbacks pendientes, seguimiento, colisiones dinámicas y regreso automático.
- No hay evidencia suficiente para cerrar la demo ni afirmar que dura 10–15 minutos. No se completó la validación del ejecutable Windows.
- La revisión actual usa fuentes y una captura anterior, no una nueva reproducción completa en Play. El Editor está conectado; no se guardaron escenas, cambiaron paquetes ni sobrescribieron partidas durante esta auditoría.

## Orden de reparación

1. Paleta semántica y contrato de ocupación; identidad real de Machop.
2. Una sala persistida con entrada, roca, salida y vuelta segura; overlay de colisión.
3. Unificar movimiento manual, seguimiento y regreso sobre el mismo criterio de paso; actualizar obstáculos por eventos.
4. Validar esa sala con entradas reales y captura. Solo entonces extender a placas, captura y jefe.
5. Acabado visual y recorrido completo en ejecutable.

El flujo y los criterios concretos están en [FLUJO_DISENO_NEO.md](FLUJO_DISENO_NEO.md). El estado anterior de “implementado” en `DEMO_NIVEL_1.md` describe presencia de código, no aceptación funcional ni visual.
