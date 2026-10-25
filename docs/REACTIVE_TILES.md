# Idle and footstep tile animation

`BosqueDelCauce.unity` has `NeoReactiveTiles` on its existing Grid/NeoGridBoard.
It references the original assets; it does not replace their sprites, GUIDs,
RuleTile rules, colliders, or painted cells.

- `HierbaDecorativaFollaje`: root-anchored idle sway and a stronger bend/compression on contact.
- `TallGrass` and `Pallete1_1`: a small green contact ring. These sprites include solid ground, so the whole square is not bent, translated, or tinted.
- `Pond`, `River`, and `Ocean`: sparse, faint ambient surface ripples plus a fading contact ripple. Existing sprite animation keeps running.
- `Mountain`: a short earth-colored contact pulse. Rock outlines and colors stay fixed.

Whole-cell brightness modulation was removed after the user's recording showed
flashing squares on continuous water. All original cell colors now remain
unchanged during both idle and footstep reactions. Stone remains still at idle.

## River animation repair

The generated river sheet also had inconsistent shoreline geometry between
frames. Its texture now uses a copy of the original aligned ocean animation,
preserving the river texture's GUID and all 384 sprite slice IDs. River rules
retain their neighbor/collision configuration and play at six frames per second
on the current Water Tilemap (animationFrameRate = 1). `RiverFreshwater.mat`
uses `RiverFreshwater.shader` on the Water renderer to brighten only blue water
pixels in the dedicated 512×192 sheet, preserving brown banks and other atlases.
The discarded generated sheet is backed up in Temp for this session.

`NeoReactiveTiles` no longer writes flags, matrices or colors to water/ground/
stone every update; only foliage receives matrix animation. Native water sprite
animation therefore runs independently of the contact-ripple overlay.
`docs/design/river-loop-validation.json` records all eight visible frames advancing
over three seconds, with no shader errors. A rendered close-up was inspected in
`Temp/river-loop-repaired.png`.

Footsteps are driven by `NeoGridMotor.Arrived`, including inactive companions
when they become active. Blocked water, rock and foliage can react to an arrival
in an adjacent cell at reduced strength; this does not change walkability.
Teleports do not emit footsteps. Repeated arrivals restart the local reaction.

The component caches painted cells on enable, updates nearby presentation at
15 Hz, and restores original transforms, colors and tile flags on disable.
Pause freezes idle and contact effects. Original continuous terrain geometry
and the separate collision Tilemap are preserved. Idle and reaction strengths
and durations are editable in the component's Styles list.

Paint with the same existing tile assets. Changes made in Edit mode are picked
up on the next Play session. Runtime painting needs `Rebuild()` afterward.
Runtime-spawned actors added after the component binds are not automatically
subscribed; disable/enable the component to bind the new actor set.

Validation: `docs/design/reactive-tiles-validation.json`. Eight Play-mode checks
cover idle motion, overlapping reactions, restart, expiry, exact restoration,
an actual movement step, collision asset preservation, and pause. Unity
compilation passed without errors. Existing A* HTTP update-check errors remain
unrelated to this feature.

The main regression script is `tools/validate_reactive_tiles.cs`, evaluated with
Unity Pipeline in Play mode. It temporarily disables input and moves the actor
one open cell, then restores its initial position and input state. Run only in
an exploration scene without movement-dependent quest/save side effects. The
pause check was performed separately during implementation.
