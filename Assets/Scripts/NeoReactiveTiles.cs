using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Cell-local idle motion and footstep reactions using the existing painted artwork.</summary>
[RequireComponent(typeof(NeoGridBoard))]
public sealed class NeoReactiveTiles : MonoBehaviour
{
    public enum Surface { Foliage, GroundGrass, Water, Stone }
    [Serializable]
    public sealed class Style
    {
        public Surface surface;
        public TileBase[] tiles = Array.Empty<TileBase>();
        public float idleAmount = .025f;
        public float reactionAmount = .18f;
        public float duration = .65f;
    }
    public NeoGridBoard board;
    public Style[] styles = Array.Empty<Style>();
    public float visibleRadius = 15f;
    readonly List<CellVisual> cells = new List<CellVisual>();
    readonly Dictionary<NeoGridMotor, Action> subscriptions = new Dictionary<NeoGridMotor, Action>();
    readonly Dictionary<TileBase, Style> lookup = new Dictionary<TileBase, Style>();
    float clock;
    float nextFrame;
    Material rippleMaterial;
    bool bound;
    public int ReactiveCellCount => cells.Count;
    public int ReactionCount { get; private set; }
    public int ActiveReactionCount
    {
        get { int n = 0; foreach (var c in cells) if (c.started >= 0 && clock - c.started < c.style.duration) n++; return n; }
    }

    sealed class CellVisual
    {
        public Tilemap map;
        public Vector3Int cell;
        public TileBase tile;
        public Style style;
        public Vector3 world;
        public Matrix4x4 matrix;
        public Color color;
        public TileFlags flags;
        public float phase;
        public float started = -1;
        public float strength;
        public bool changed;
        public LineRenderer ring;
        public LineRenderer splash;
    }

    void OnEnable()
    {
        if (board == null) board = GetComponent<NeoGridBoard>();
        if (!Application.isPlaying) return;
        Rebuild();
    }

    void BindActors()
    {
        // Includes inactive companions so changing control needs no special handling.
        foreach (var motor in FindObjectsByType<NeoGridMotor>(FindObjectsInactive.Include))
        {
            if (motor.board != board) continue;
            var actor = motor;
            // Large Pokémon touch every cell of their footprint.
            Action callback = () => { foreach (var cell in actor.CellsAt(actor.Cell)) ReactAt(NeoGridBoard.Center(cell)); };
            subscriptions.Add(actor, callback);
            actor.Arrived += callback;
        }
        bound = true;
    }

    /// <summary>Refresh after runtime painting; restores any overrides before caching new tiles.</summary>
    public void Rebuild()
    {
        Restore();
        lookup.Clear();
        foreach (var style in styles)
            foreach (var tile in style.tiles)
                if (tile != null) lookup[tile] = style;
        foreach (var map in GetComponentsInChildren<Tilemap>())
        {
            // Presentation never changes the authoritative collision tilemap.
            if (map == board.walls || map.GetComponent<TilemapCollider2D>() != null) continue;
            foreach (var cell in map.cellBounds.allPositionsWithin)
            {
                var tile = map.GetTile(cell);
                if (tile == null || !lookup.TryGetValue(tile, out var style)) continue;
                cells.Add(new CellVisual {
                    map = map, cell = cell, tile = tile, style = style,
                    world = map.GetCellCenterWorld(cell), matrix = map.GetTransformMatrix(cell),
                    color = map.GetColor(cell), flags = map.GetTileFlags(cell),
                    phase = Mathf.Repeat(cell.x * 1.73f + cell.y * 2.31f, Mathf.PI * 2)
                });
            }
        }
    }

    public void ReactAt(Vector3 world)
    {
        if (!isActiveAndEnabled || DemoBootstrap.IsPaused) return;
        foreach (var c in cells)
        {
            if (c.map == null || c.map.GetTile(c.cell) != c.tile) continue;
            var foot = c.map.WorldToCell(world);
            int distance = Mathf.Abs(foot.x - c.cell.x) + Mathf.Abs(foot.y - c.cell.y);
            // Water/cliff edges and bushes can be brushed without bypassing their collisions.
            bool edge = c.style.surface == Surface.Water || c.style.surface == Surface.Stone || c.style.surface == Surface.Foliage;
            if (distance != 0 && (!edge || distance != 1)) continue;
            c.started = clock;
            c.strength = distance == 0 ? 1 : .4f;
            ReactionCount++;
        }
        nextFrame = 0;
    }

    void Update()
    {
        // All actor Awakes have resolved their scene board before the first Update.
        if (!bound) BindActors();
        if (DemoBootstrap.IsPaused) return;
        clock += Time.deltaTime;
        if (clock < nextFrame) return;
        nextFrame = clock + 1f / 15f;
        var target = board.navigationTarget;
        foreach (var c in cells)
        {
            if (c.map == null || c.map.GetTile(c.cell) != c.tile) continue;
            if (target != null && (c.world - target.position).sqrMagnitude > visibleRadius * visibleRadius)
            {
                RestoreCell(c);
                continue;
            }
            float age = c.started < 0 ? c.style.duration : clock - c.started;
            float progress = Mathf.Clamp01(age / Mathf.Max(.1f, c.style.duration));
            float reaction = Mathf.Sin(progress * Mathf.PI) * (1 - progress) * c.strength;
            float idle = Mathf.Sin(clock * 2.2f + c.phase) * c.style.idleAmount;
            var matrix = c.matrix;
            if (c.style.surface == Surface.Foliage)
            {
                // Shear around the root instead of translating the whole plant off its soil.
                var shear = Matrix4x4.identity;
                shear.m01 = idle + Mathf.Sin(progress * Mathf.PI * 4) * reaction * c.style.reactionAmount;
                shear.m11 = 1 - reaction * c.style.reactionAmount;
                matrix = Matrix4x4.Translate(new Vector3(0, -.5f, 0)) * shear * Matrix4x4.Translate(new Vector3(0, .5f, 0)) * c.matrix;
            }
            // Never pulse an entire terrain cell's tint: independently phased squares
            // reveal the grid as a flashing checkerboard, especially on animated water.
            if (c.style.surface == Surface.Foliage)
            {
                c.map.SetTileFlags(c.cell, c.flags & ~TileFlags.LockTransform);
                c.map.SetTransformMatrix(c.cell, matrix);
                c.changed = true;
            }
            if (progress < 1 && c.style.surface != Surface.Foliage)
                DrawRing(c, progress, c.strength);
            else if (c.style.surface == Surface.Water && c.cell.x % 6 == 0 && c.cell.y % 6 == 0)
            {
                // Sparse, low-opacity surface ripples, rather than whole-cell flashes.
                float ambient = Mathf.Repeat(clock * .15f + c.phase, 1);
                if (ambient < .12f) DrawRing(c, ambient / .12f, .18f);
                else if (c.ring != null) c.ring.enabled = false;
                if (c.splash != null) c.splash.enabled = false;
            }
            else
            {
                if (c.ring != null) c.ring.enabled = false;
                if (c.splash != null) c.splash.enabled = false;
            }
        }
    }

    void DrawRing(CellVisual c, float progress, float strength)
    {
        // Stepping into water splashes: a thicker foam ring, then a second one opening behind it.
        bool splash = c.style.surface == Surface.Water && strength > .3f;
        c.ring = Ring(c, c.ring, progress, strength, splash ? 1.5f : 1f);
        float trailing = Mathf.Clamp01(progress * 1.5f - .35f);
        if (splash && trailing > 0 && trailing < 1) c.splash = Ring(c, c.splash, trailing, strength * .7f, 1.2f);
        else if (c.splash != null) c.splash.enabled = false;
    }

    LineRenderer Ring(CellVisual c, LineRenderer ring, float progress, float strength, float size)
    {
        if (ring == null)
        {
            if (rippleMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null) return null;
                rippleMaterial = new Material(shader);
            }
            var effect = new GameObject("Tile contact ripple");
            effect.transform.SetParent(transform, false);
            ring = effect.AddComponent<LineRenderer>();
            ring.sharedMaterial = rippleMaterial;
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.positionCount = 12;
            var renderer = c.map.GetComponent<TilemapRenderer>();
            if (renderer != null) { ring.sortingLayerID = renderer.sortingLayerID; ring.sortingOrder = renderer.sortingOrder + 1; }
        }
        ring.enabled = true;
        ring.widthMultiplier = size > 1 ? 1f / 16f : 1f / 32f;
        float radius = Mathf.Lerp(.08f, .42f, progress) * size;
        var color = c.style.surface == Surface.Water
            ? new Color(.8f, .94f, 1, (1 - progress) * strength)
            : c.style.surface == Surface.GroundGrass
                ? new Color(.25f, .55f, .3f, (1 - progress) * strength * .5f)
                : new Color(.85f, .74f, .57f, (1 - progress) * strength * .7f);
        ring.startColor = ring.endColor = color;
        for (int i = 0; i < 12; i++)
        {
            float angle = i * Mathf.PI * 2 / 12;
            ring.SetPosition(i, c.world + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * .45f, -.02f));
        }
        return ring;
    }

    void RestoreCell(CellVisual c)
    {
        if (c.changed && c.map != null && c.map.GetTile(c.cell) == c.tile)
        {
            c.map.SetTransformMatrix(c.cell, c.matrix);
            c.map.SetColor(c.cell, c.color);
            c.map.SetTileFlags(c.cell, c.flags);
        }
        c.changed = false;
        if (c.ring != null) c.ring.enabled = false;
        if (c.splash != null) c.splash.enabled = false;
    }
    void Restore()
    {
        foreach (var c in cells)
        {
            RestoreCell(c);
            if (c.ring != null) Destroy(c.ring.gameObject);
            if (c.splash != null) Destroy(c.splash.gameObject);
        }
        cells.Clear();
    }
    void OnDisable()
    {
        foreach (var pair in subscriptions) if (pair.Key != null) pair.Key.Arrived -= pair.Value;
        subscriptions.Clear();
        bound = false;
        Restore();
        if (rippleMaterial != null) Destroy(rippleMaterial);
        clock = nextFrame = 0;
    }
}
