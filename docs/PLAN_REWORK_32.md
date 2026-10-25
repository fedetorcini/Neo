# Plan aprobado: rework visual completo a 32 × 32

Fecha: 25 de octubre de 2026. Estado de este respaldo: implementación del rework todavía no iniciada. El punto de recuperación conserva el proyecto previo a la migración.

## Objetivo

Renovar todo el proyecto funcional para que las criaturas originales, los personajes y el escenario se lean claramente al tamaño real de juego. La resolución base queda fijada en 32 × 32 por casilla; no se comparará con una base de 64 × 64.

## Estándar

- Cada casilla conserva una unidad de mundo y pasa a 32 píxeles por unidad.
- Criaturas pequeñas: 32 × 32; jugador y personajes altos: 32 × 48; cuerpos de dos casillas horizontales: 64 × 32; criaturas de 2 × 2: 64 × 64. El dibujo y la huella física son independientes.
- Cámara de referencia: 640 × 360, ampliación entera, sin estiramiento y con márgenes donde sean necesarios.
- Diseñar y revisar a resolución nativa. Ampliar o reducir automáticamente imágenes no cuenta como acabado final.
- Frente, espalda y costado, con reflejo lateral cuando corresponda. Por orientación: dos poses de reposo, cuatro de movimiento y cuatro de ataque cuando la acción ya exista.
- Revisar escalas heredadas y deformaciones procedurales para preservar nitidez.

## Alcance

- Criaturas originales, jugador, personajes y enemigos utilizados en bosque, demo y combate.
- Terrenos, caminos, arquitectura, escaleras, puentes, árboles y transiciones.
- Ríos, orillas, cascadas, espuma y animaciones de agua.
- Decoraciones, sombras, transparencias y reacciones a las pisadas.
- Ataques, proyectiles, impactos y efectos de interacción.
- Retratos, iconos, barras y menús; la interfaz no queda restringida a tiles cuadrados.
- Importadores, paletas y generadores, centralizando las medidas actualmente fijadas en 16 píxeles.

Conservar distribución de mapas, coordenadas, velocidades, distancias de interacción, huellas, puzzles, combate y guardados. No añadir mecánicas ni ampliar mapas. Mantener las escenas archivadas como referencias históricas.

## Entregas

1. Crear una referencia jugable separada con jugador, criatura eléctrica, abeja, luchador y criatura grande, más terreno, escalera, árbol, río y decoración. Revisarla dentro del juego antes de producir el catálogo completo.
2. Adaptar cámara, importación y generadores. Crear copias recuperables y correspondencias entre recursos anteriores y nuevos; preservar identificadores y referencias cuando sea posible.
3. Producir e integrar familias completas: personajes y criaturas; terreno y arquitectura; agua y decoración; efectos e interfaz. Regenerar variantes derivadas desde sus familias.
4. Actualizar todas las escenas funcionales y los recursos cargados dinámicamente. Eliminar la mezcla transitoria de estándares y comprobar la compilación.

## Criterios de cierre

- Criaturas y acciones reconocibles al tamaño normal de juego.
- Sin desenfoque, variaciones involuntarias de escala, deslizamiento de pies ni saltos entre direcciones.
- Terrenos y animaciones sin costuras ni fondos opacos accidentales.
- Movimiento, seguimiento, ocupación, empuje, puzzles, combate, transiciones y guardados conservan su comportamiento.
- Escenas funcionales y generadores utilizan el nuevo estándar.

Los diseños recientes son exploraciones, no diseños definitivos obligatorios. Conservar los nombres actuales como identificadores internos durante la migración. El esfuerzo principal es arte y animación, seguido de herramientas, integración y pruebas; no se plantea reescribir el movimiento.
