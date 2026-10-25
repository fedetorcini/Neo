using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Quarter = NeoShoreTile.Quarter;

// Bakes a NeoShoreTile template into every edge piece the tile can ask for. Bands and frames come
// from the template size (blocks of 2 x 3 tiles). The baked sheet and sprites live inside the tile asset.
public static class NeoShoreTileBaker
{
    const int Tile = 16, Half = 8, BlockW = 2 * Tile, BlockH = 3 * Tile;

    [MenuItem("Neo/Tiles/Hornear todas las orillas")]
    public static void BakeAll()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:NeoShoreTile"))
            Bake(AssetDatabase.LoadAssetAtPath<NeoShoreTile>(AssetDatabase.GUIDToAssetPath(guid)));
        AssetDatabase.SaveAssets();
    }

    // Blocking lives on the board's Colliders tilemap: deep bands block, walkable ones stay open.
    [MenuItem("Neo/Tiles/Sincronizar colliders de orillas")]
    public static void SyncSceneColliders()
    {
        var board = Object.FindAnyObjectByType<NeoGridBoard>();
        if (board == null || board.walls == null) { Debug.LogWarning("La escena no tiene NeoGridBoard con Colliders."); return; }
        var block = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.TileBase>("Assets/Demo/BoundaryCell.asset");
        int blocked = 0, open = 0;
        foreach (var map in board.GetComponentsInChildren<UnityEngine.Tilemaps.Tilemap>())
        {
            if (map == board.walls) continue;
            Undo.RegisterCompleteObjectUndo(board.walls, "Sincronizar colliders de orillas");
            foreach (var p in map.cellBounds.allPositionsWithin)
            {
                if (!(map.GetTile(p) is NeoShoreTile shore)) continue;
                bool walkable = shore.Walkable(map, p);
                board.walls.SetTile(p, walkable ? null : block);
                if (walkable) open++; else blocked++;
            }
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(board.gameObject.scene);
        Debug.Log($"Orillas: {open} casillas se pisan, {blocked} bloquean.");
    }

    [MenuItem("Assets/Create/Neo/Tiles/Orilla desde esta plantilla", true)]
    static bool CanCreateFromTemplate() => Selection.activeObject is Texture2D;
    [MenuItem("Assets/Create/Neo/Tiles/Orilla desde esta plantilla")]
    static void CreateFromTemplate()
    {
        var template = (Texture2D)Selection.activeObject;
        var path = Path.ChangeExtension(AssetDatabase.GetAssetPath(template), ".asset");
        var tile = ScriptableObject.CreateInstance<NeoShoreTile>();
        tile.template = template;
        AssetDatabase.CreateAsset(tile, AssetDatabase.GenerateUniqueAssetPath(path));
        Bake(tile);
        AssetDatabase.SaveAssets();
        Selection.activeObject = tile;
    }

    public static void Bake(NeoShoreTile tile)
    {
        if (tile.template == null) { Debug.LogWarning(tile.name + ": falta la plantilla.", tile); return; }
        var source = new Texture2D(2, 2);
        source.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(tile.template))); // Readable copy.
        if (source.width % BlockW != 0 || source.height % BlockH != 0)
        { Debug.LogError(tile.name + ": la plantilla debe medir múltiplos de 32 x 48 px.", tile); return; }
        tile.frames = source.width / BlockW;
        tile.bands = source.height / BlockH;

        // Every distinct piece the 256 neighbour combinations produce (47 for this layout).
        var keys = new List<int>();
        for (int m = 0; m < 256; m++)
        {
            bool B(int bit) => (m & (1 << bit)) != 0;
            int key = NeoShoreTile.KeyFromNeighbours(B(0), B(1), B(2), B(3), B(4), B(5), B(6), B(7));
            if (!keys.Contains(key)) keys.Add(key);
        }

        int count = tile.bands * keys.Count * tile.frames, columns = 16, rows = Mathf.CeilToInt(count / (float)columns);
        var sheet = new Texture2D(columns * Tile, rows * Tile, TextureFormat.RGBA32, false)
        { name = tile.name + " (horneado)", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        sheet.SetPixels32(new Color32[sheet.width * sheet.height]);

        // Template coordinates are from the top-left of a block, like the drawing.
        Color Read(int band, int frame, int x, int y) => source.GetPixel(frame * BlockW + x, source.height - 1 - (band * BlockH + y));
        var sprites = new Sprite[count];
        int index = 0;
        for (int band = 0; band < tile.bands; band++)
            for (int piece = 0; piece < keys.Count; piece++)
                for (int frame = 0; frame < tile.frames; frame++, index++)
                {
                    int ox = index % columns * Tile, oyTop = index / columns * Tile;
                    for (int q = 0; q < 4; q++)
                    {
                        var kind = (Quarter)(keys[piece] / (int)Mathf.Pow(5, q) % 5);
                        var from = QuarterOrigin(kind, q % 2, q / 2);
                        var under = QuarterOrigin(Quarter.Centre, q % 2, q / 2);
                        for (int y = 0; y < Half; y++)
                            for (int x = 0; x < Half; x++)
                            {
                                // Deeper bands sit on the centre of the band before them, so their
                                // see-through edge shows shallower water instead of land.
                                var below = band > 0 ? Read(band - 1, frame, under.x + x, under.y + y) : Color.clear;
                                var top = Read(band, frame, from.x + x, from.y + y);
                                var c = Color.Lerp(below, top, top.a);
                                c.a = top.a + below.a * (1 - top.a);
                                int dx = ox + q % 2 * Half + x, dyTop = oyTop + q / 2 * Half + y;
                                sheet.SetPixel(dx, sheet.height - 1 - dyTop, c);
                            }
                    }
                    var rect = new Rect(ox, sheet.height - oyTop - Tile, Tile, Tile);
                    sprites[index] = Sprite.Create(sheet, rect, new Vector2(.5f, .5f), Tile, 0, SpriteMeshType.FullRect);
                    sprites[index].name = $"{tile.name}_b{band}_p{piece}_f{frame}";
                }
        sheet.Apply();
        Object.DestroyImmediate(source);

        var assetPath = AssetDatabase.GetAssetPath(tile);
        foreach (var old in AssetDatabase.LoadAllAssetsAtPath(assetPath).Where(o => o is Sprite || o is Texture2D))
            AssetDatabase.RemoveObjectFromAsset(old);
        AssetDatabase.AddObjectToAsset(sheet, tile);
        foreach (var sprite in sprites) AssetDatabase.AddObjectToAsset(sprite, tile);
        tile.SetBaked(keys.ToArray(), sprites);
        EditorUtility.SetDirty(tile);
        AssetDatabase.SaveAssetIfDirty(tile);
        Debug.Log($"{tile.name}: {tile.bands} bandas, {tile.frames} frames, {keys.Count} piezas de borde.", tile);
    }

    // Where a quarter comes from in a 2 x 3 block. qx/qy: 0 = left/top quarter, 1 = right/bottom.
    // The island's ring is a 4 x 4 grid of quarters under the first row; left quarters read
    // from its third column and right ones from its second, so patterns stay in step.
    static Vector2Int QuarterOrigin(Quarter kind, int qx, int qy)
    {
        int ringX(bool edge) => edge ? (qx == 0 ? 0 : 3) : (qx == 0 ? 2 : 1);
        int ringY(bool edge) => edge ? (qy == 0 ? 0 : 3) : (qy == 0 ? 2 : 1);
        switch (kind)
        {
            case Quarter.InnerCorner: return new Vector2Int(Tile + qx * Half, qy * Half);
            case Quarter.OuterCorner: return new Vector2Int(ringX(true) * Half, Tile + ringY(true) * Half);
            case Quarter.OpenVertical: return new Vector2Int(ringX(false) * Half, Tile + ringY(true) * Half);
            case Quarter.OpenHorizontal: return new Vector2Int(ringX(true) * Half, Tile + ringY(false) * Half);
            default: return new Vector2Int(ringX(false) * Half, Tile + ringY(false) * Half);
        }
    }
}

[CustomEditor(typeof(NeoShoreTile))]
sealed class NeoShoreTileEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var tile = (NeoShoreTile)target;
        EditorGUILayout.HelpBox(tile.Baked ? "Horneado. Volvé a hornear si cambiás la plantilla." : "Sin hornear: el tile no se ve hasta hornearlo.", tile.Baked ? MessageType.Info : MessageType.Warning);
        if (GUILayout.Button("Hornear plantilla")) { NeoShoreTileBaker.Bake(tile); AssetDatabase.SaveAssets(); }
    }
}
