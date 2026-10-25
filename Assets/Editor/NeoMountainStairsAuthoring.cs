using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

// Stairs carved into a Mountain ridge: the steps climb the rock face and a trail cut into the top
// carries on at the height of the ground beyond. EscaleraMontana is a RuleTile that picks its
// piece by its position in the stairs (top / middle / face, west / centre / east); Mountain and
// PathTile become NeoFriendRuleTile and count the stairs as their own, so rock and trail meet them.
public static class NeoMountainStairsAuthoring
{
    const string Sheet = "Assets/Sprites/AniamtedTiles/MountainStairs.png";
    const string TilePath = "Assets/Sprites/AniamtedTiles/EscaleraMontana.asset";
    const string FriendScript = "Assets/Scripts/Tiles/NeoFriendRuleTile.cs";
    static readonly string[] FriendTiles = { "Assets/Sprites/AniamtedTiles/Mountain.asset", "Assets/Sprites/AniamtedTiles/PathTile.asset" };
    const int T = 16;

    [MenuItem("Neo/Tiles/Crear escalera de montaña")]
    public static void CreateMenu() => Debug.Log(Create());

    public static string Create()
    {
        var sprites = ImportSheet();
        var stairs = BuildStairsTile(sprites);
        foreach (var path in FriendTiles) MakeFriend(path, stairs);
        AssetDatabase.SaveAssets();
        return "EscaleraMontana lista (" + sprites.Length + " piezas); Mountain y PathTile la cuentan como propia.";
    }

    // MountainStairs.png (tools/generate_mountain_stairs.py): 4 x 4 pieces, rows top/middle/face/single, columns west/centre/east/single.
    static Sprite[] ImportSheet()
    {
        AssetDatabase.ImportAsset(Sheet, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(Sheet);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = T;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var existing = provider.GetSpriteRects().ToDictionary(r => r.name); // keep IDs so tiles stay linked on re-runs
        var rects = new List<SpriteRect>();
        for (int row = 0; row < 4; row++)
            for (int col = 0; col < 4; col++)
            {
                string name = "MountainStairs_" + (row * 4 + col);
                var r = existing.TryGetValue(name, out var old) ? old : new SpriteRect { name = name, spriteID = GUID.Generate() };
                r.rect = new Rect(col * T, (3 - row) * T, T, T); // texture rows count from the bottom
                r.alignment = SpriteAlignment.Center;
                r.pivot = new Vector2(.5f, .5f);
                rects.Add(r);
            }
        provider.SetSpriteRects(rects.ToArray());
        var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        names?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();

        var all = AssetDatabase.LoadAllAssetsAtPath(Sheet).OfType<Sprite>().ToDictionary(s => s.name);
        return Enumerable.Range(0, 16).Select(i => all["MountainStairs_" + i]).ToArray();
    }

    static RuleTile BuildStairsTile(Sprite[] sprites)
    {
        var tile = AssetDatabase.LoadAssetAtPath<RuleTile>(TilePath);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<RuleTile>();
            AssetDatabase.CreateAsset(tile, TilePath);
        }
        const int This = RuleTile.TilingRuleOutput.Neighbor.This, Not = RuleTile.TilingRuleOutput.Neighbor.NotThis;
        tile.m_DefaultSprite = sprites[1 * 4 + 1];
        tile.m_DefaultColliderType = Tile.ColliderType.None; // Walkable: blocking lives on the Colliders tilemap.
        tile.m_TilingRules = new List<RuleTile.TilingRule>();
        // Row from the stairs above and below, column from the stairs to the west and east.
        var rows = new[] { (Not, This), (This, This), (This, Not), (Not, Not) };
        var cols = new[] { (Not, This), (This, This), (This, Not), (Not, Not) };
        for (int row = 0; row < 4; row++)
            for (int col = 0; col < 4; col++)
            {
                var rule = new RuleTile.TilingRule { m_Id = row * 4 + col, m_ColliderType = Tile.ColliderType.None };
                rule.m_Sprites = new[] { sprites[row * 4 + col] };
                rule.ApplyNeighbors(new Dictionary<Vector3Int, int>
                {
                    { Vector3Int.up, rows[row].Item1 }, { Vector3Int.down, rows[row].Item2 },
                    { Vector3Int.left, cols[col].Item1 }, { Vector3Int.right, cols[col].Item2 },
                });
                tile.m_TilingRules.Add(rule);
            }
        EditorUtility.SetDirty(tile);
        return tile;
    }

    // Turns a RuleTile asset into NeoFriendRuleTile in place (same GUID and rules) and lists the stairs as a friend.
    static void MakeFriend(string path, TileBase stairs)
    {
        var asset = AssetDatabase.LoadAssetAtPath<RuleTile>(path);
        if (!(asset is NeoFriendRuleTile))
        {
            var so = new SerializedObject(asset);
            so.FindProperty("m_Script").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MonoScript>(FriendScript);
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        var friend = AssetDatabase.LoadAssetAtPath<NeoFriendRuleTile>(path);
        if (!friend.friends.Contains(stairs)) friend.friends = friend.friends.Append(stairs).ToArray();
        EditorUtility.SetDirty(friend);
    }
}
