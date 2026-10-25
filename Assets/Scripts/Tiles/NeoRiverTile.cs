using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// River water with height levels. Paint water (deep or shallow) with the asset of each level and
/// waterfall cells where the river drops: every cell reads its neighbours and draws banks, the
/// current flowing toward the nearest drop, lips where water spills to a lower level, falling
/// sheets, splashes and foam. Waterfalls take any width and height; a fall painted between two
/// waters of different level side by side pours sideways.
/// Pieces are composed on demand from a template of 16 px materials and kept in memory, so every
/// combination exists; tilemaps holding river tiles are refreshed when a scene loads.
/// </summary>
[CreateAssetMenu(menuName = "Neo/Tiles/Río con niveles", fileName = "Rio_N0")]
public sealed class NeoRiverTile : TileBase
{
    public enum Kind { Agua, Bajo, Cascada }

    [Tooltip("Agua profunda, agua baja (se ve el lecho) o cascada (la caída entre dos niveles).")]
    public Kind kind;
    [Tooltip("Altura del agua. Las cascadas no usan nivel: lo toman del agua de arriba y de abajo.")]
    [Min(0)] public int level;
    [Tooltip("Plantilla de materiales (rio_cauce.png). Read/Write activado.")]
    public Texture2D template;
    [Tooltip("Hacia dónde corre el agua cuando no hay una caída cerca (por ejemplo, un río que sale del mapa).")]
    public Vector2Int defaultFlow = new Vector2Int(0, -1);
    public float framesPerSecond = 10;
    [Tooltip("Agua dulce: los azules se vuelven celestes como el río del bosque (mismo ajuste que RiverFreshwater.shader).")]
    public bool freshwater;

    public const int Frames = 16, Size = 16;
    const int SearchLimit = 1500, RapidsDistance = 1;

    /// <summary>Deep water and falls block the way; shallow water can be walked. For collider tools.</summary>
    public bool Walkable => kind == Kind.Bajo;

    // ---------------------------------------------------------------- neighbourhood

    enum Rel { Land, Same, Lower, Higher, FallIn, Variant }
    enum Fall { Frontal, ToEast, ToWest }

    static readonly Vector2Int[] Cardinals = { new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0) }; // N E S W
    static readonly Vector2Int[] Diagonals = { new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 1) }; // NE SE SW NW
    static readonly Vector2Int[] FlowDirs =
    {
        new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, 0), new Vector2Int(1, -1),
        new Vector2Int(0, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 0), new Vector2Int(-1, 1),
    };

    delegate NeoRiverTile Lookup(Vector3Int p);
    static Vector3Int V(Vector2Int d) => new Vector3Int(d.x, d.y, 0);
    static bool IsWater(NeoRiverTile t) => t != null && t.kind != Kind.Cascada;
    static bool IsFall(NeoRiverTile t) => t != null && t.kind == Kind.Cascada;

    /// <summary>Level of the first water found walking from p through waterfall cells, or -1.</summary>
    static int WaterLevelPast(Lookup at, Vector3Int p, Vector3Int step)
    {
        for (int i = 0; i < 8; i++)
        {
            p += step;
            var t = at(p);
            if (!IsFall(t)) return IsWater(t) ? t.level : -1;
        }
        return -1;
    }

    /// <summary>Waterfall cells in a row from p (p included) along +step and -step.</summary>
    static int FallRun(Lookup at, Vector3Int p, Vector3Int step)
    {
        int run = 1;
        for (var q = p + step; run < 16 && IsFall(at(q)); q += step) run++;
        for (var q = p - step; run < 16 && IsFall(at(q)); q -= step) run++;
        return run;
    }

    /// <summary>
    /// A fall between two waters of different level on its sides pours sideways; any other faces the camera.
    /// When there are different levels both above/below and left/right (a wide fall whose row ends at a lower
    /// pool on one side, or a tall side fall between two channels), the fall runs along its longer side:
    /// wider than tall faces the camera, taller than wide pours sideways.
    /// </summary>
    static Fall Orientation(Lookup at, Vector3Int p)
    {
        int west = WaterLevelPast(at, p, Vector3Int.left), east = WaterLevelPast(at, p, Vector3Int.right);
        if (west < 0 || east < 0 || west == east) return Fall.Frontal;
        int north = WaterLevelPast(at, p, Vector3Int.up), south = WaterLevelPast(at, p, Vector3Int.down);
        bool dropsToCamera = north >= 0 && south >= 0 && north > south;
        if (dropsToCamera && FallRun(at, p, Vector3Int.right) > FallRun(at, p, Vector3Int.up)) return Fall.Frontal;
        return west > east ? Fall.ToEast : Fall.ToWest;
    }

    static bool SameFall(Lookup at, Vector3Int p, Fall orientation) => IsFall(at(p)) && Orientation(at, p) == orientation;

    Rel Relation(Lookup at, Vector3Int p, Vector2Int d)
    {
        var n = at(p + V(d));
        if (n == null) return Rel.Land;
        if (n.kind == Kind.Cascada)
        {
            var o = Orientation(at, p + V(d));
            bool pouringHere = (o == Fall.Frontal && d.y == 1) || (o == Fall.ToEast && d.x == -1) || (o == Fall.ToWest && d.x == 1);
            return pouringHere ? Rel.FallIn : Rel.Same;
        }
        if (n.level < level) return Rel.Lower;
        if (n.level > level) return Rel.Higher;
        return n.kind == kind ? Rel.Same : Rel.Variant;
    }

    /// <summary>Whether water at p drops from there: a fall it feeds or a lower water beside it.</summary>
    static bool DropToward(Lookup at, Vector3Int p, int level, Vector2Int d)
    {
        var n = at(p + V(d));
        if (IsWater(n)) return n.level < level;
        if (!IsFall(n)) return false;
        var o = Orientation(at, p + V(d));
        return (o == Fall.Frontal && d.y == -1) || (o == Fall.ToEast && d.x == 1) || (o == Fall.ToWest && d.x == -1);
    }

    /// <summary>Breadth-first search through water of the same level for the nearest drop.</summary>
    bool FindFlow(Lookup at, Vector3Int start, out Vector2Int flow, out int distance)
    {
        flow = defaultFlow; distance = int.MaxValue;
        var parent = new Dictionary<Vector3Int, Vector3Int> { [start] = start };
        var queue = new Queue<Vector3Int>();
        queue.Enqueue(start);
        while (queue.Count > 0 && parent.Count < SearchLimit)
        {
            var p = queue.Dequeue();
            foreach (var d in Cardinals)
            {
                if (!DropToward(at, p, level, d)) continue;
                // Walk back to the start: the direction toward a cell a few steps along the path
                // (or toward the drop itself when it is close) gives smooth diagonals.
                var path = new List<Vector3Int> { p + V(d), p };
                while (path[path.Count - 1] != start) path.Add(parent[path[path.Count - 1]]);
                distance = path.Count - 2;
                var target = path[Mathf.Max(0, path.Count - 1 - 3)];
                flow = Quantise(target - start);
                return true;
            }
            foreach (var d in Cardinals)
            {
                var q = p + V(d);
                if (parent.ContainsKey(q)) continue;
                var t = at(q);
                if (!IsWater(t) || t.level != level) continue;
                parent[q] = p;
                queue.Enqueue(q);
            }
        }
        return false;
    }

    static Vector2Int Quantise(Vector3Int v)
    {
        float m = Mathf.Max(Mathf.Abs(v.x), Mathf.Abs(v.y));
        if (m == 0) return Vector2Int.zero;
        int Axis(int c) => Mathf.Abs(c) >= m * 0.4f ? (int)Mathf.Sign(c) : 0;
        return new Vector2Int(Axis(v.x), Axis(v.y));
    }

    // ---------------------------------------------------------------- piece key
    // Everything a piece depends on, packed in a long. Pieces never depend on the cell position.

    long KeyAt(Lookup at, Vector3Int p)
    {
        long key = (long)kind | (freshwater ? NeoRiverPieces.FreshwaterBit : 0);
        if (kind == Kind.Cascada)
        {
            var o = Orientation(at, p);
            key |= (long)o << 2;
            bool Edge(Vector2Int d) => !SameFall(at, p + V(d), o);
            bool Land(Vector2Int d) => at(p + V(d)) == null;
            bool Water(Vector2Int d) => IsWater(at(p + V(d)));
            int bits = 0;
            for (int i = 0; i < 4; i++)
            {
                if (Edge(Cardinals[i])) bits |= 1 << i;
                if (Land(Cardinals[i])) bits |= 1 << (4 + i);
                if (Water(Cardinals[i])) bits |= 1 << (8 + i);
            }
            return key | (long)bits << 4;
        }
        int flowIndex = 0, rapids = 0;
        if (FindFlow(at, p, out var flow, out int distance) || flow != Vector2Int.zero)
        {
            flowIndex = 1 + System.Array.IndexOf(FlowDirs, new Vector2Int(Mathf.Clamp(flow.x, -1, 1), Mathf.Clamp(flow.y, -1, 1)));
            rapids = distance <= RapidsDistance ? 1 : 0;
        }
        key |= (long)flowIndex << 2 | (long)rapids << 6;
        for (int i = 0; i < 4; i++) key |= (long)Relation(at, p, Cardinals[i]) << (7 + 3 * i);
        for (int i = 0; i < 4; i++) if (at(p + V(Diagonals[i])) == null) key |= 1L << (19 + i);
        return key;
    }

    Sprite[] Piece(Vector3Int position, ITilemap tilemap)
    {
        if (template == null) return null;
        Lookup at = q => tilemap.GetTile(q) as NeoRiverTile;
        return NeoRiverPieces.Get(template, KeyAt(at, position));
    }

    // ---------------------------------------------------------------- TileBase

    public override void RefreshTile(Vector3Int position, ITilemap tilemap)
    {
        // Edges depend on neighbours, but the current depends on drops anywhere along the river:
        // refresh the whole river connected to this cell (8 neighbours, so erasing a cell also works).
        const int limit = 8000;
        var seen = new HashSet<Vector3Int> { position };
        var queue = new Queue<Vector3Int>();
        queue.Enqueue(position);
        tilemap.RefreshTile(position);
        while (queue.Count > 0 && seen.Count < limit)
        {
            var p = queue.Dequeue();
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    var q = p + new Vector3Int(dx, dy, 0);
                    if (!seen.Add(q) || !(tilemap.GetTile(q) is NeoRiverTile)) continue;
                    tilemap.RefreshTile(q);
                    queue.Enqueue(q);
                }
        }
    }

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        var piece = Piece(position, tilemap);
        tileData.sprite = piece != null ? piece[0] : null;
        tileData.color = Color.white;
        tileData.transform = Matrix4x4.identity;
        tileData.flags = TileFlags.LockTransform | TileFlags.LockColor;
        tileData.colliderType = Tile.ColliderType.None; // Blocking stays on the Colliders tilemap.
    }

    public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap, ref TileAnimationData data)
    {
        var piece = Piece(position, tilemap);
        if (piece == null) return false;
        data.animatedSprites = piece;
        data.animationSpeed = framesPerSecond;
        data.animationStartTime = 0; // Every cell in step, so the current reads as one surface.
        return true;
    }

    // ---------------------------------------------------------------- refresh on load

    static readonly HashSet<NeoRiverTile> Loaded = new HashSet<NeoRiverTile>();
    void OnEnable() => Loaded.Add(this);
    void OnDisable() => Loaded.Remove(this);

#if UNITY_EDITOR
    // Recompose after an Inspector change; tilemaps can't be refreshed inside OnValidate itself.
    void OnValidate() => UnityEditor.EditorApplication.delayCall += () =>
    {
        NeoRiverPieces.Clear();
        RefreshAll(Object.FindObjectsByType<Tilemap>());
    };
#endif

    /// <summary>Recomposes every river cell of the given tilemaps (sprites live only in memory).</summary>
    public static void RefreshAll(IEnumerable<Tilemap> maps)
    {
        foreach (var map in maps)
            foreach (var tile in Loaded)
                if (tile != null && map != null && map.ContainsTile(tile)) { map.RefreshAllTiles(); break; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RefreshOnSceneLoad() =>
        SceneManager.sceneLoaded += (scene, mode) => RefreshAll(Object.FindObjectsByType<Tilemap>());
}

/// <summary>Composes river pieces from the template materials into in-memory atlas pages.</summary>
static class NeoRiverPieces
{
    const int S = NeoRiverTile.Size, F = NeoRiverTile.Frames, Page = 512;
    enum Mat { Agua, Bajo, Orilla, Cascada, Espuma, Pared }

    static readonly Dictionary<(Texture2D, long), Sprite[]> Cache = new Dictionary<(Texture2D, long), Sprite[]>();
    static readonly Dictionary<Texture2D, Color32[]> Templates = new Dictionary<Texture2D, Color32[]>();
    static readonly List<Texture2D> Pages = new List<Texture2D>();
    static int nextSlot;
    static Color32[] tpl;
    static int tplHeight;

    public static void Clear()
    {
        foreach (var page in Pages) Object.DestroyImmediate(page);
        Pages.Clear(); Cache.Clear(); Templates.Clear(); nextSlot = 0;
    }

    public static Sprite[] Get(Texture2D template, long key)
    {
        if (Cache.TryGetValue((template, key), out var sprites) && sprites[0] != null) return sprites;
        if (!Templates.TryGetValue(template, out tpl))
        {
            if (!template.isReadable) { Debug.LogError(template.name + ": activá Read/Write en la importación.", template); return null; }
            tpl = template.GetPixels32();
            Templates[template] = tpl;
        }
        tplHeight = template.height;

        sprites = new Sprite[F];
        var pixels = new Color32[S * S];
        for (int f = 0; f < F; f++)
        {
            Compose(key, f, pixels);
            sprites[f] = Store(pixels, $"Rio_{key:X}_{f}");
        }
        foreach (var page in Pages) page.Apply();
        Cache[(template, key)] = sprites;
        return sprites;
    }

    static Sprite Store(Color32[] pixels, string name)
    {
        int perRow = Page / S, perPage = perRow * perRow;
        if (nextSlot >= Pages.Count * perPage)
        {
            var page = new Texture2D(Page, Page, TextureFormat.RGBA32, false)
            { name = "Río (piezas)", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            page.SetPixels32(new Color32[Page * Page]);
            Pages.Add(page);
        }
        var target = Pages[nextSlot / perPage];
        int slot = nextSlot++ % perPage, ox = slot % perRow * S, oy = slot / perRow * S;
        // pixels are row 0 = top; textures are row 0 = bottom.
        var flipped = new Color32[S * S];
        for (int y = 0; y < S; y++) System.Array.Copy(pixels, y * S, flipped, (S - 1 - y) * S, S);
        target.SetPixels32(ox, oy, S, S, flipped);
        var sprite = Sprite.Create(target, new Rect(ox, oy, S, S), new Vector2(.5f, .5f), S, 0, SpriteMeshType.FullRect);
        sprite.name = name;
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    // ---------------------------------------------------------------- materials and palette

    static Color32 M(Mat m, int x, int y)
    {
        x = ((x % S) + S) % S; y = ((y % S) + S) % S;
        return tpl[(tplHeight - 1 - ((int)m * S + y)) * S + x];
    }

    static Color32 C(uint rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    static readonly Color32 Clear0 = new Color32(0, 0, 0, 0);
    static readonly Color32 Outline = C(0x1b3663), Shadow = C(0x244367), RockDark = C(0x604050), RockLight = C(0xb89088);
    static readonly Color32 Lip = C(0xa8c0e0), Foam = C(0xd8e0e8), White = C(0xf8fcff), SheetEdge = C(0x4870a8), Glint = C(0x6088d0);

    static float Hash(int x, int y, int z)
    {
        unchecked
        {
            uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(z * 83492791);
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return (h & 0xffff) / 65535f;
        }
    }

    // 4 x 4 ordered dither, 0..1.
    static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
    static float Dither(int x, int y) => (Bayer[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f;

    // ---------------------------------------------------------------- composition
    // Local coordinates: x to the right, y downward (0 = top row of the cell).

    static readonly Vector2Int[] FlowDirs =
    {
        new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, 0), new Vector2Int(1, -1),
        new Vector2Int(0, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 0), new Vector2Int(-1, 1),
    };

    public const long FreshwaterBit = 1L << 62;

    static void Compose(long key, int f, Color32[] o)
    {
        var kind = (NeoRiverTile.Kind)(key & 3);
        if (kind == NeoRiverTile.Kind.Cascada) ComposeFall(key & ~FreshwaterBit, f, o);
        else ComposeWater(key & ~FreshwaterBit, kind, f, o);
        if ((key & FreshwaterBit) != 0)
            for (int i = 0; i < o.Length; i++) o[i] = Freshwater(o[i]);
    }

    /// <summary>Same remap as RiverFreshwater.shader: only clearly blue pixels turn to fresh, light water.</summary>
    static Color32 Freshwater(Color32 c)
    {
        if (c.a == 0 || !(c.b > c.r * 1.5f && c.b > c.g * 1.15f)) return c;
        byte B(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v), 0, 255);
        return new Color32(B(c.r * .7f), B(c.g * 1.6f + 25.5f), B(c.b * 1.25f + 25.5f), c.a);
    }

    /// <summary>Water surface: pattern scrolled with the current, then foam, lips and banks.</summary>
    // NeoRiverTile.Rel, as packed in the key.
    const int Land0 = 0, Lower = 2, Higher = 3, FallIn = 4, Variant = 5;

    static void ComposeWater(long key, NeoRiverTile.Kind kind, int f, Color32[] o)
    {
        int flowIndex = (int)(key >> 2 & 15);
        bool rapids = (key >> 6 & 1) == 1;
        var rel = new int[4];
        for (int i = 0; i < 4; i++) rel[i] = (int)(key >> (7 + 3 * i) & 7);
        var diagLand = new bool[4];
        for (int i = 0; i < 4; i++) diagLand[i] = (key >> (19 + i) & 1) == 1;
        bool Land(int side) => rel[side] == Land0;

        // Current in screen space (y down). Rapids run twice as fast; both loop in 16 frames.
        var flow = flowIndex == 0 ? Vector2Int.zero : FlowDirs[flowIndex - 1];
        int speed = rapids ? 2 : 1, sx = flow.x * speed * f, sy = -flow.y * speed * f;
        var surface = kind == NeoRiverTile.Kind.Bajo ? Mat.Bajo : Mat.Agua;

        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float cx = x + .5f, cy = y + .5f;
                // Distance from this pixel to each side: N E S W.
                float[] side = { cy, S - cx, S - cy, cx };
                int ux = x - sx, uy = y - sy; // pattern coordinate carried by the current
                var c = M(surface, ux, uy);

                // Shallow water fades into deep water of the same level.
                if (kind == NeoRiverTile.Kind.Bajo)
                    for (int s = 0; s < 4; s++)
                        if (rel[s] == Variant && side[s] < 4 && Dither(x, y) > side[s] / 4f)
                            c = M(Mat.Agua, ux, uy);

                if (rapids)
                {
                    // Short streaks along the current: hash the line across it and 4 px stretches along it.
                    int px = ((ux % S) + S) % S, py = ((uy % S) + S) % S, fy = -flow.y;
                    int across = px * fy - py * flow.x, along = px * flow.x + py * fy;
                    float h = Hash(across, Mathf.FloorToInt(along / 4f), 7);
                    if (h < .03f) c = (along & 3) == 0 ? Lip : Glint;
                }

                // Edges toward other levels and falls.
                for (int s = 0; s < 4; s++)
                {
                    float d = side[s];
                    int along = s % 2 == 0 ? x : y;
                    switch (rel[s])
                    {
                        case Lower: // The water spills over this edge.
                            if (d < 1) c = (along + f / 2) % 4 == 0 ? Foam : White;
                            else if (d < 2) c = (along + y + f) % 2 == 0 ? Lip : c;
                            else if (d < 3 && (along + f) % 3 == 0) c = Glint;
                            break;
                        case Higher: // Water lands here from a small step.
                            if (d < 5)
                            {
                                var foam = SampleFoam(along + f, (int)d + f / 2);
                                if (foam.a / 255f > .3f + d / 5f * .7f) c = Opaque(foam);
                            }
                            break;
                        case FallIn: // A waterfall crashes on this edge; foam drifts away.
                            if (d < 15)
                            {
                                var foam = SampleFoam(along + (f * 3) % S, (int)d - f);
                                float threshold = d < 4 ? 0 : .05f + (d - 4) / 11f * .75f;
                                if (foam.a / 255f > threshold) c = Opaque(foam);
                                else if (d < 10 && Hash(along, (int)d, f) < .04f) c = White; // bubbles
                            }
                            break;
                    }
                }

                // Banks against land, with rounded outer corners and inner corners.
                float depth = 99;
                for (int s = 0; s < 4; s++) if (Land(s)) depth = Mathf.Min(depth, side[s]);
                // Corners: NE uses sides N(0) E(1), SE: S(2) E(1), SW: S(2) W(3), NW: N(0) W(3).
                int[,] corner = { { 0, 1 }, { 2, 1 }, { 2, 3 }, { 0, 3 } };
                for (int k = 0; k < 4; k++)
                {
                    int v = corner[k, 0], h = corner[k, 1];
                    float a = side[h], b = side[v];
                    if (Land(v) && Land(h))
                    {
                        const float R = 4;
                        if (a < R && b < R) depth = Mathf.Min(depth, R - Mathf.Sqrt((R - a) * (R - a) + (R - b) * (R - b)));
                    }
                    else if (!Land(v) && !Land(h) && diagLand[k])
                        depth = Mathf.Min(depth, Mathf.Sqrt(a * a + b * b));
                }
                const int W = 3;
                if (depth < 0) c = Clear0;
                else if (depth < W - 1) c = depth < 1 && Hash(x, y, 3) < .3f ? RockLight : M(Mat.Orilla, x, y);
                else if (depth < W) c = RockDark;
                else if (depth < W + 1) c = Outline;
                else if (depth < W + 2 && (x + y) % 2 == 0) c = Shadow;

                o[y * S + x] = c;
            }
    }

    /// <summary>Foam material, churned; alpha is density.</summary>
    static Color32 SampleFoam(int x, int y) => M(Mat.Espuma, x, y);
    static Color32 Opaque(Color32 c) { c.a = 255; return c; }

    static void ComposeFall(long key, int f, Color32[] o)
    {
        int orientation = (int)(key >> 2 & 3), bits = (int)(key >> 4);
        bool Edge(int s) => (bits >> s & 1) == 1;      // N E S W: the fall does not continue there
        bool Land(int s) => (bits >> (4 + s) & 1) == 1;
        bool Water(int s) => (bits >> (8 + s) & 1) == 1;
        if (orientation == 0) ComposeFrontal(f, o, Edge, Land, Water);
        else ComposeLateral(f, o, orientation == 2, Land);
    }

    /// <summary>A fall facing the camera: lip on top, sheet running down, rock sides, splash at the foot.</summary>
    static void ComposeFrontal(int f, Color32[] o, System.Func<int, bool> edge, System.Func<int, bool> land, System.Func<int, bool> water)
    {
        bool top = edge(0), bottom = edge(2), left = edge(3), right = edge(1);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                var c = M(Mat.Cascada, x, y - 4 * f);
                int wobble = Hash(x, 0, 5) < .5f ? 0 : 1;

                if (top && water(0))
                {
                    int ly = y - wobble;
                    if (ly < 2) c = M(Mat.Agua, x, y - 2 * f);
                    else if (ly < 3) c = Glint;
                    else if (ly < 4) c = Lip;
                    else if (ly < 5) c = (x + f / 2) % 3 == 0 ? Foam : White;
                }
                else if (top && y < 2) c = M(Mat.Orilla, x, y);

                if (bottom && water(2))
                {
                    if (y >= 10)
                    {
                        var foam = SampleFoam(x + (f * 3) % S, y + f);
                        float threshold = 1f - (y - 9) / 6f;
                        if (foam.a / 255f > threshold * .9f) c = Opaque(foam);
                        else if (y >= 13) c = Foam;
                    }
                    else if (y >= 5 && Hash(x, y, f) < .14f * (y - 4) / 5f) c = White; // spray
                }
                else if (bottom && y >= 14) c = M(Mat.Orilla, x, y);

                // Rock walls on the sides where the sheet ends.
                if (left && x < 3 || right && x > 12) c = M(Mat.Pared, x, y);
                else if (left && x == 3 || right && x == 12) c = SheetEdge;
                o[y * S + x] = c;
            }
    }

    /// <summary>A fall pouring sideways: incoming water and lip on the high side, slanted sheet, spray on the low side.</summary>
    static void ComposeLateral(int f, Color32[] o, bool toWest, System.Func<int, bool> land)
    {
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                int lx = toWest ? S - 1 - x : x; // draw as pouring east, mirror for west
                Color32 c;
                if (lx < 4) c = M(Mat.Agua, lx - 2 * f, y);
                else if (lx < 5) c = Lip;
                else if (lx < 6) c = (y + f / 2) % 3 == 0 ? Foam : White;
                else if (lx < 12) c = M(Mat.Cascada, lx, y - 4 * f - (lx - 6));
                else
                {
                    var foam = SampleFoam(lx + f, y + (f * 3) % S);
                    float threshold = 1f - (lx - 11) / 5f;
                    c = foam.a / 255f > threshold * .9f ? Opaque(foam) : M(Mat.Cascada, lx, y - 4 * f - (lx - 6));
                    if (Hash(lx, y, f) < .08f) c = White;
                }
                if (land(0) && y < 2 || land(2) && y > 13) c = M(Mat.Orilla, x, y);
                o[y * S + x] = c;
            }
    }
}
