# Criaturas originales

Revisión visual del 25 de octubre de 2026. Los nombres son provisionales. Las especies anteriores siguen como identificadores internos para mantener las conexiones del juego.

| Identificador anterior | Diseño nuevo | Resolución por pose | Rasgos |
| --- | --- | --- | --- |
| Pikachu | Lumin | 16 × 16 | Animal eléctrico turquesa, orejas redondas y cola corta |
| Beedrill | Brispa | 16 × 16 | Abeja redonda ámbar, alas menta y sin taladros |
| Machop | Pedrugo | 16 × 16 | Criatura de terracota, grandes puños de piedra y ojos claros |
| Psyduck | Junquin | 16 × 16 | Ave de estanque verde, cara crema y pico naranja |
| Dratini | Caucel | 32 × 16 | Anguila acuática turquesa con aletas coral |
| Haunter | Farolin | 32 × 32 | Espíritu alado violeta con ojos dorados |
| Machoke | Granugo | 32 × 32 | Guardián de piedra con puños terracota y hombros musgosos |

Cada hoja contiene doce poses: cuatro columnas (reposo, paso A, paso B y acción) y tres filas (frente, espalda y costado izquierdo). El motor conserva el reflejo para mirar a la derecha. La resolución respeta la huella anterior; no se fuerza a las criaturas grandes a una sola casilla.

Los sprites están en `Assets/Sprites/Creatures/`. Se importan a 16 píxeles por unidad, con filtro Point, sin compresión ni mipmaps. Los nuevos prefabs conservan la escala nativa de los píxeles. Las animaciones existentes conservan sus GUID, claves temporales y controladores; solo cambian las referencias de las imágenes. Las hojas anteriores se conservan.

`Assets/Editor/NeoCreatureArt.cs` contiene el importador y la correspondencia de poses. `NeoCreatureArt.Import()` conserva los identificadores de las divisiones existentes al volver a importar. `NeoCreatureArt.Apply()` actualiza clips, prefabs y la escena activa desde el Editor detenido, creando una copia previa en `Temp/CreatureArtBackup/`. No hace falta ejecutarlo para usar el resultado guardado.

El generador `NeoPokemonAuthoring` consulta esta correspondencia, por lo que regenerar sus prefabs reutiliza el arte nuevo. Su comportamiento previo de regeneración de clips y controladores permanece igual.

Comparación: `docs/design/original-creatures-preview.png`. Comprobaciones de referencias, resoluciones y tiempos: `docs/design/original-creatures-validation.json`. Se verificaron 64 clips conservando su identidad y tiempos, y 271 claves de pose mediante el motor de animación de Unity; informe en `docs/design/original-creatures-pose-validation.json`. En Play se observaron los ocho actores del bosque usando las hojas nuevas, con sus animaciones avanzando y las huellas originales.

Los diseños se generaron con el editor de imágenes integrado. Brief común: criaturas originales con siluetas compactas, bloques grandes de color, pocos detalles, fondo transparente y doce poses compatibles con su movimiento. Pedrugo recibió una segunda revisión para que los ojos se lean en 16 × 16.
