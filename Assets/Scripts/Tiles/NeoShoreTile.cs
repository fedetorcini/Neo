using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Water that shapes its own shore. Paint only the water: each cell takes a band from its
/// distance to land (0 = shoreline, the last band = open water) and an edge piece toward
/// shallower bands. Edge pieces are assembled from quarter tiles, so every corner exists.
/// The art comes from a template baked by NeoShoreTileBaker; one asset per beach style.
/// </summary>
[CreateAssetMenu(menuName = "Neo/Tiles/Agua con orilla (playa, lago)", fileName = "NuevaOrilla")]
public sealed class NeoShoreTile : TileBase
{
    // Template layout, per band (rows) and animation frame (columns): a block of 2 x 3 tiles.
    //   (0,0) preview          (1,0) inner corners, one per quarter
    //   rows 1-2: a 2 x 2 island whose quarters give outer corners, edges and centre.
    [Tooltip("Plantilla PNG: un bloque de 2 x 3 casillas por banda (filas) y por frame (columnas).")]
    public Texture2D template;
    [Tooltip("Bandas desde la orilla hacia adentro. La última se repite en todo el agua abierta.")]
    [Min(1)] public int bands = 3;
    [Min(1)] public int frames = 4;
    public float framesPerSecond = 3;
    [Tooltip("Cualquier NeoShoreTile cuenta como agua (true) o solo este mismo asset (false).")]
    public bool mergeWithOtherShores = true;
    [Tooltip("Bandas que se pueden pisar desde la orilla. 1 = solo la primera, donde todavía hay arena.")]
    [Min(0)] public int walkableBands = 1;

    // Filled by the baker. sprites[(band * keys.Length + piece) * frames + frame]
    [SerializeField, HideInInspector] int[] keys = new int[0];
    [SerializeField, HideInInspector] Sprite[] sprites = new Sprite[0];
    Dictionary<int, int> pieceOfKey;

    public bool Baked => sprites != null && sprites.Length > 0 && sprites.Length == bands * keys.Length * frames;

    /// <summary>How each quarter of a cell connects: centre, inner corner, outer corner or one of two edges.</summary>
    public enum Quarter { Centre, InnerCorner, OuterCorner, OpenVertical, OpenHorizontal }

    public static Quarter QuarterFor(bool horizontal, bool vertical, bool diagonal)
    {
        if (horizontal && vertical) return diagonal ? Quarter.Centre : Quarter.InnerCorner;
        if (!horizontal && !vertical) return Quarter.OuterCorner;
        return horizontal ? Quarter.OpenVertical : Quarter.OpenHorizontal;
    }
    // Quarters in order top-left, top-right, bottom-left, bottom-right; five cases each.
    public static int KeyOf(Quarter tl, Quarter tr, Quarter bl, Quarter br) => (int)tl + 5 * (int)tr + 25 * (int)bl + 125 * (int)br;

    /// <summary>Key of a cell from which of its 8 neighbours belong to the same or a deeper band.</summary>
    public static int KeyFromNeighbours(bool n, bool ne, bool e, bool se, bool s, bool sw, bool w, bool nw) =>
        KeyOf(QuarterFor(w, n, nw), QuarterFor(e, n, ne), QuarterFor(w, s, sw), QuarterFor(e, s, se));

    public void SetBaked(int[] bakedKeys, Sprite[] bakedSprites)
    {
        keys = bakedKeys; sprites = bakedSprites; pieceOfKey = null;
    }

    bool IsWater(TileBase tile) => tile == this || (mergeWithOtherShores && tile is NeoShoreTile);
    bool IsWater(ITilemap tilemap, Vector3Int p) => IsWater(tilemap.GetTile(p));
    int Band(ITilemap tilemap, Vector3Int p) => Band(q => IsWater(tilemap.GetTile(q)), p);

    /// <summary>Band of a painted cell, for tools that sync colliders or decorations with the water.</summary>
    public int BandAt(Tilemap map, Vector3Int p) => Band(q => IsWater(map.GetTile(q)), p);
    /// <summary>Whether a painted cell is shallow enough to walk on; deeper bands belong on Colliders.</summary>
    public bool Walkable(Tilemap map, Vector3Int p) => BandAt(map, p) < walkableBands;

    /// <summary>0 at the shoreline, growing inward up to bands - 1; -1 on land.</summary>
    int Band(System.Func<Vector3Int, bool> isWater, Vector3Int p)
    {
        if (!isWater(p)) return -1;
        for (int r = 1; r < bands; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                    if ((Mathf.Abs(dx) == r || Mathf.Abs(dy) == r) && !isWater(p + new Vector3Int(dx, dy, 0)))
                        return r - 1;
        return bands - 1;
    }

    bool Piece(Vector3Int p, ITilemap tilemap, out int first)
    {
        first = 0;
        if (!Baked) return false;
        if (pieceOfKey == null)
        {
            pieceOfKey = new Dictionary<int, int>();
            for (int i = 0; i < keys.Length; i++) pieceOfKey[keys[i]] = i;
        }
        int band = Mathf.Max(0, Band(tilemap, p));
        bool Deep(int dx, int dy) => Band(tilemap, p + new Vector3Int(dx, dy, 0)) >= band;
        int key = KeyFromNeighbours(Deep(0, 1), Deep(1, 1), Deep(1, 0), Deep(1, -1), Deep(0, -1), Deep(-1, -1), Deep(-1, 0), Deep(-1, 1));
        if (!pieceOfKey.TryGetValue(key, out int piece)) return false;
        first = (band * keys.Length + piece) * frames;
        return true;
    }

    public override void RefreshTile(Vector3Int position, ITilemap tilemap)
    {
        // A cell's band depends on cells up to `bands` away, and its edge on its neighbours' bands.
        int r = bands + 1;
        for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
            {
                var p = position + new Vector3Int(dx, dy, 0);
                if (IsWater(tilemap, p)) tilemap.RefreshTile(p);
            }
    }

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        tileData.sprite = Piece(position, tilemap, out int first) ? sprites[first] : null;
        tileData.color = Color.white;
        tileData.flags = TileFlags.LockTransform | TileFlags.LockColor;
        tileData.colliderType = Tile.ColliderType.None; // Blocking stays on the Colliders tilemap.
    }

    public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap, ref TileAnimationData data)
    {
        if (frames < 2 || !Piece(position, tilemap, out int first)) return false;
        var animated = new Sprite[frames];
        System.Array.Copy(sprites, first, animated, 0, frames);
        data.animatedSprites = animated;
        data.animationSpeed = framesPerSecond;
        data.animationStartTime = 0; // Every cell in step, so waves read as one surface.
        return true;
    }
}
