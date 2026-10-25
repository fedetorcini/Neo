using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// River pieces live only in memory, so tilemaps are recomposed whenever the Editor opens a scene,
// reloads scripts or leaves Play mode. Also creates the river assets from the template.
[InitializeOnLoad]
static class NeoRiverTileEditor
{
    const string Folder = "Assets/Sprites/River", TemplatePath = Folder + "/rio_cauce.png";

    static NeoRiverTileEditor()
    {
        EditorApplication.delayCall += Refresh;
        EditorSceneManager.sceneOpened += (scene, mode) => Refresh();
        EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredEditMode) Refresh(); };
        EditorApplication.update += RefreshWhereMissing;
    }

    // Catches tilemaps loaded some other way (the Tile Palette window, prefab mode): once a second,
    // recompose any tilemap whose river cells came from disk without sprites.
    static double nextCheck;
    static void RefreshWhereMissing()
    {
        if (EditorApplication.timeSinceStartup < nextCheck || EditorApplication.isPlayingOrWillChangePlaymode) return;
        nextCheck = EditorApplication.timeSinceStartup + 1;
        foreach (var map in Resources.FindObjectsOfTypeAll<Tilemap>())
        {
            if (EditorUtility.IsPersistent(map) || map.GetUsedTilesCount() == 0) continue;
            foreach (var p in map.cellBounds.allPositionsWithin)
                if (map.GetTile(p) is NeoRiverTile)
                {
                    if (map.GetSprite(p) == null) map.RefreshAllTiles();
                    break;
                }
        }
    }

    [MenuItem("Neo/Tiles/Crear Tile Palette del río")]
    public static void CreatePalette()
    {
        const string path = "Assets/Pallete/Rio.prefab";
        var palette = AssetDatabase.LoadAssetAtPath<GameObject>(path) ??
            UnityEditor.Tilemaps.GridPaletteUtility.CreateNewPalette("Assets/Pallete", "Rio", GridLayout.CellLayout.Rectangle,
                GridPalette.CellSizing.Automatic, Vector3.one, GridLayout.CellSwizzle.XYZ);
        var root = PrefabUtility.LoadPrefabContents(path);
        var map = root.GetComponentInChildren<Tilemap>();
        map.ClearAllTiles();
        // One row per kind, a gap between tiles so each shows alone instead of joining its neighbours.
        string[][] rows =
        {
            new[] { "Cascada" },
            new[] { "RioBajo_N0", "RioBajo_N1", "RioBajo_N2", "RioBajo_N3" },
            new[] { "Rio_N0", "Rio_N1", "Rio_N2", "Rio_N3" },
        };
        for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < rows[y].Length; x++)
                map.SetTile(new Vector3Int(2 * x, 2 * y, 0), AssetDatabase.LoadAssetAtPath<TileBase>($"{Folder}/{rows[y][x]}.asset"));
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("Tile Palette del río: " + path, palette);
    }

    [MenuItem("Neo/Tiles/Refrescar ríos")]
    public static void Refresh()
    {
        // Includes the Tile Palette, which lives in a preview scene.
        var maps = Resources.FindObjectsOfTypeAll<Tilemap>().Where(m => !EditorUtility.IsPersistent(m));
        NeoRiverTile.RefreshAll(maps);
        SceneView.RepaintAll();
    }

    [MenuItem("Neo/Tiles/Crear tiles de río (niveles 0-3)")]
    public static void CreateAll()
    {
        var template = AssetDatabase.LoadAssetAtPath<Texture2D>(TemplatePath);
        if (template == null) { Debug.LogError("Falta " + TemplatePath + ": ejecutá tools/generate_river_template.py."); return; }
        var importer = (TextureImporter)AssetImporter.GetAtPath(TemplatePath);
        if (!importer.isReadable || importer.filterMode != FilterMode.Point || importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.isReadable = true;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            template = AssetDatabase.LoadAssetAtPath<Texture2D>(TemplatePath);
        }
        for (int level = 0; level < 4; level++)
        {
            Create($"Rio_N{level}", NeoRiverTile.Kind.Agua, level, template);
            Create($"RioBajo_N{level}", NeoRiverTile.Kind.Bajo, level, template);
        }
        Create("Cascada", NeoRiverTile.Kind.Cascada, 0, template);
        AssetDatabase.SaveAssets();
        Refresh();
    }

    static void Create(string name, NeoRiverTile.Kind kind, int level, Texture2D template)
    {
        var path = $"{Folder}/{name}.asset";
        var tile = AssetDatabase.LoadAssetAtPath<NeoRiverTile>(path);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<NeoRiverTile>();
            tile.freshwater = true; // Like the river of Bosque del Cauce.
            AssetDatabase.CreateAsset(tile, path);
        }
        tile.kind = kind;
        tile.level = level;
        tile.template = template;
        EditorUtility.SetDirty(tile);
    }
}

[CustomEditor(typeof(NeoRiverTile))]
sealed class NeoRiverTileInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var tile = (NeoRiverTile)target;
        if (tile.template != null && !tile.template.isReadable)
            EditorGUILayout.HelpBox("La plantilla necesita Read/Write activado.", MessageType.Error);
        EditorGUILayout.HelpBox(tile.kind == NeoRiverTile.Kind.Cascada
            ? "Pintá cascadas entre agua de distinto nivel: entre arriba y abajo cae hacia la cámara; entre izquierda y derecha cae de costado."
            : $"Agua de nivel {tile.level}. Junto a agua más baja derrama; la corriente va hacia la caída más cercana.", MessageType.Info);
        if (GUILayout.Button("Refrescar ríos de la escena")) NeoRiverTileEditor.Refresh();
    }
}
