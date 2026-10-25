using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Moves the river of Bosque del Cauce from the River RuleTile on Water to NeoRiverTile on its own
// "Rio" tilemap, with three levels and two waterfalls:
//   - Salto del Estanque (north): the pond (level 2) drops 2 tiles onto the river, full width,
//     replacing the old 3 x 3 waterfall sprite on Terrain LV1.
//   - Salto Partido (south-west): four columns fall 2 tiles to the camera; the eastern side keeps
//     level 1 down the cliff and pours sideways into level 0 at its foot.
// The ground drops with the river at the Salto Partido: a Mountain cliff runs along the face of the
// fall on the west bank, and two rows lower on the east bank, where the side channel pours down.
// The east cliff is on the path of the east bank (and in line with the ledge at x 33-36), so it has
// stairs; the west bank there is closed in by forest, so its cliff has none.
// Running it again re-levels the Rio tilemap, so the recipe can be tuned and re-applied.
// Shores (PlayaArena, LagoPasto), bridge, rocks, reeds and the rest of the colliders stay as they were.
public static class NeoRiverBosqueAuthoring
{
    // Rows of the pond fall and the cells of the split fall, in tilemap coordinates.
    const int PondFallTop = 41, PondFallBottom = 40;
    static readonly RectInt SplitFront = new RectInt(19, 12, 4, 2);            // x 19-22, y 12-13
    static readonly Vector2Int[] SplitSide = { new Vector2Int(23, 11) };
    // Level 1 cells south of row 14 that keep their height before pouring sideways.
    static readonly HashSet<Vector2Int> SplitHigh = new HashSet<Vector2Int>
    {
        new Vector2Int(23, 13), new Vector2Int(24, 13), new Vector2Int(25, 13), new Vector2Int(26, 13),
        new Vector2Int(23, 12), new Vector2Int(24, 12), new Vector2Int(25, 12), new Vector2Int(24, 11),
    };
    // The drop of the Salto Partido on dry land. The west cliff takes the rows of the fall face, which
    // removes the low water that used to sit beside the face; the east cliff is two rows lower.
    static readonly RectInt WestCliff = new RectInt(13, 12, 6, 2);             // x 13-18, y 12-13
    static readonly RectInt EastCliff = new RectInt(24, 10, 9, 2);             // x 24-32, y 10-11, up to the ledge at x 33
    static readonly Vector2Int[] EastCliffGaps = { new Vector2Int(24, 11) };   // the side channel itself
    static readonly RectInt EastStairs = new RectInt(26, 10, 2, 2);            // x 26-27, y 10-11

    [MenuItem("Neo/Autoría/Río con niveles y cascadas en el bosque")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.path.EndsWith("BosqueDelCauce.unity")) { Debug.LogError("Abrí Assets/Scenes/BosqueDelCauce.unity primero."); return; }
        EditorSceneManager.SaveScene(scene);
        string backup = "Assets/Scenes/Reference/AntesDelRioNiveles_" + DateTime.Now.ToString("yyyyMMdd") + ".unity";
        if (AssetDatabase.LoadMainAssetAtPath(backup) != null) backup = backup.Replace(".unity", DateTime.Now.ToString("_HHmm") + ".unity");
        AssetDatabase.CopyAsset(scene.path, backup);

        var water = Find("Water");
        var detail = Find("Terrain LV1");
        var river = Find("Rio") ?? CreateLayer(water);
        var oldRiver = Load<TileBase>("Assets/Sprites/AniamtedTiles/River.asset");
        var oldFall = Load<TileBase>("Assets/Pallete/Pallete1_185.asset");
        TileBase Level(int n) => Load<TileBase>($"Assets/Sprites/River/Rio_N{n}.asset");
        var fall = Load<TileBase>("Assets/Sprites/River/Cascada.asset");

        var cells = new List<Vector3Int>();
        foreach (var p in water.cellBounds.allPositionsWithin) if (water.GetTile(p) == oldRiver) cells.Add(p);
        foreach (var p in river.cellBounds.allPositionsWithin) if (river.GetTile(p) is NeoRiverTile) cells.Add(p);
        foreach (var p in cells)
        {
            var c = new Vector2Int(p.x, p.y);
            river.SetTile(p, IsCliff(c) ? null : IsFall(c) ? fall : Level(LevelOf(c)));
            water.SetTile(p, null);
        }
        foreach (var p in detail.cellBounds.allPositionsWithin)
            if (detail.GetTile(p) == oldFall) detail.SetTile(p, null);
        PaintCliffs(detail);

        NeoRiverTile.RefreshAll(new[] { river });
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Río con niveles: {cells.Count} casillas en Rio. Copia previa: {backup}");
    }

    static bool IsCliff(Vector2Int c) =>
        WestCliff.Contains(c) || EastCliff.Contains(c) && Array.IndexOf(EastCliffGaps, c) < 0;

    /// <summary>Rock on Terrain, nothing on top of it, blocked in Colliders; the stairs stay walkable.
    /// A tree whose trunk stood on the cliff goes with its crown (the two cells above the trunk).</summary>
    static void PaintCliffs(Tilemap detail)
    {
        var terrain = Find("Terrain");
        var colliders = Find("Colliders");
        var crowns = new[] { Find("Foreground"), Find("Foreground LV1") };
        var mountain = Load<TileBase>("Assets/Sprites/AniamtedTiles/Mountain.asset");
        var stairs = Load<TileBase>("Assets/Sprites/AniamtedTiles/EscaleraMontana.asset");
        var block = Load<TileBase>("Assets/Demo/BoundaryCell.asset");
        foreach (var rect in new[] { WestCliff, EastCliff })
            foreach (var c in rect.allPositionsWithin)
            {
                if (!IsCliff(c)) continue;
                var p = new Vector3Int(c.x, c.y, 0);
                bool step = EastStairs.Contains(c);
                var ground = terrain.GetTile(p);
                if (ground != null && (ground.name == "Pallete1_486" || ground.name == "Pallete1_487"))
                    foreach (var map in crowns)
                        for (int up = 1; up <= 2; up++)
                        {
                            var q = p + new Vector3Int(0, up, 0);
                            var crown = map.GetTile(q);
                            if (crown != null && crown.name.StartsWith("treeForeground")) map.SetTile(q, null);
                        }
                terrain.SetTile(p, step ? stairs : mountain);
                // Reeds and flowers go; crowns of trees standing below the cliff stay in front of it.
                var decoration = detail.GetTile(p);
                if (decoration != null && !decoration.name.StartsWith("treeForeground")) detail.SetTile(p, null);
                colliders.SetTile(p, step ? null : block);
            }
    }

    static bool IsFall(Vector2Int c) =>
        c.y <= PondFallTop && c.y >= PondFallBottom || SplitFront.Contains(c) || Array.IndexOf(SplitSide, c) >= 0;

    static int LevelOf(Vector2Int c)
    {
        if (c.y > PondFallTop) return 2;
        if (c.y >= 14 || SplitHigh.Contains(c)) return 1;
        return 0;
    }

    static Tilemap CreateLayer(Tilemap water)
    {
        var go = new GameObject("Rio", typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(water.transform.parent, false);
        go.transform.SetSiblingIndex(water.transform.GetSiblingIndex() + 1);
        var source = water.GetComponent<TilemapRenderer>();
        var renderer = go.GetComponent<TilemapRenderer>();
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = source.sortingOrder; // Default sprite material: the tile does its own freshwater colours.
        Undo.RegisterCreatedObjectUndo(go, "Crear Tilemap Rio");
        return go.GetComponent<Tilemap>();
    }

    static Tilemap Find(string name)
    {
        foreach (var map in UnityEngine.Object.FindObjectsByType<Tilemap>()) if (map.name == name) return map;
        return null;
    }

    static T Load<T>(string path) where T : UnityEngine.Object =>
        AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Falta el asset " + path);
}
