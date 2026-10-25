using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Builds a reference scene that shows every river piece: partial falls, side falls, wide and tall
// cascades, shallows, small steps. Each map below is drawn top to bottom; legend:
//   .  grass      #  cliff      0-3  water of that level      a-d  shallow water, level 0-3      F  waterfall
static class NeoRiverShowcase
{
    const string ScenePath = "Assets/Scenes/Reference/RioNiveles_Muestra.unity";

    // A 4-wide river: two columns fall to the camera, two keep their height and pour sideways.
    static readonly string[] Split =
    {
        "...2222...",
        "...2222...",
        "...2222...",
        "...2222...",
        "###FF22...",
        "###FF22...",
        "...11F2###",
        "...11F2###",
        "...111....",
        "...1111...",
        "....1111..",
        "....1111..",
        ".....1111.",
        ".....1111.",
        ".....1111.",
        ".....1111.",
    };

    // A 4-wide, 3-tall cascade in the middle of an 8-wide river, landing on shallows.
    static readonly string[] Wide =
    {
        "..33333333..",
        "..33333333..",
        "..33333333..",
        "..33333333..",
        "####FFFF####",
        "####FFFF####",
        "####FFFF####",
        "..11111111..",
        "..11bbbb11..",
        "..1bbbbbb1..",
        "..11bbbb11..",
        "..11111111..",
        "...111111...",
        "...111111...",
        "...111111...",
        "...111111...",
    };

    // A 3-wide, 1-tall cascade, then a small step (rapids) and a river that rises away from the camera.
    static readonly string[] Steps =
    {
        "...222........",
        "...222........",
        "...222........",
        "..#FFF#.......",
        "...111....00..",
        "...111....00..",
        "...111....00..",
        "...000....11..",
        "...000....11..",
        "...000....11..",
        "...aa0........",
        "...aaa........",
        "....00........",
        "....00........",
        "....00........",
        "....00........",
    };

    [MenuItem("Neo/Tiles/Crear escena de muestra del río")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var grid = new GameObject("Grid", typeof(Grid));
        var terrain = Layer(grid, "Terrain", -10000);
        var cliffs = Layer(grid, "Terrain LV1", -9998);
        var river = Layer(grid, "Rio", -9999);

        var grass = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Pallete/Pallete1_1.asset");
        var cliff = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Pallete/Pallete1_117.asset");
        var water = new TileBase[4];
        var shallow = new TileBase[4];
        for (int i = 0; i < 4; i++)
        {
            water[i] = AssetDatabase.LoadAssetAtPath<TileBase>($"Assets/Sprites/River/Rio_N{i}.asset");
            shallow[i] = AssetDatabase.LoadAssetAtPath<TileBase>($"Assets/Sprites/River/RioBajo_N{i}.asset");
        }
        var fall = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Sprites/River/Cascada.asset");

        int x0 = 0;
        foreach (var map in new[] { Split, Wide, Steps })
        {
            for (int row = 0; row < map.Length; row++)
                for (int col = 0; col < map[row].Length; col++)
                {
                    var p = new Vector3Int(x0 + col, map.Length - 1 - row, 0);
                    terrain.SetTile(p, grass);
                    char c = map[row][col];
                    if (c == '#') cliffs.SetTile(p, cliff);
                    else if (c >= '0' && c <= '3') river.SetTile(p, water[c - '0']);
                    else if (c >= 'a' && c <= 'd') river.SetTile(p, shallow[c - 'a']);
                    else if (c == 'F') river.SetTile(p, fall);
                }
            x0 += map[0].Length + 1;
            for (int y = 0; y < Split.Length; y++) terrain.SetTile(new Vector3Int(x0 - 1, y, 0), grass);
        }

        var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.orthographic = true;
        camera.orthographicSize = Split.Length / 2f + 0.5f;
        camera.transform.position = new Vector3(x0 / 2f, Split.Length / 2f, -10);
        camera.backgroundColor = new Color(0.44f, 0.75f, 0.63f);

        EditorSceneManager.SaveScene(scene, ScenePath);
        NeoRiverTileEditor.Refresh();
    }

    static Tilemap Layer(GameObject grid, string name, int order)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(grid.transform, false);
        go.GetComponent<TilemapRenderer>().sortingOrder = order;
        return go.GetComponent<Tilemap>();
    }
}
