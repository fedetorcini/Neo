using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Converts only the authored demo environment; never edits SampleScene.</summary>
public static class NeoTilemapMigration
{
    static readonly Vector2Int[] Cardinal = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
    static TileBase[] treeTiles;
    public static void PaintTree(Tilemap terrain, Tilemap foreground, Vector3 position)
    {
        if (treeTiles == null)
        {
            // Bottom, middle and canopy of the normal tree in Neo's original palette.
            int[] indices = { 484,485,476,477,468,469 };
            treeTiles = indices.Select(i=>AssetDatabase.LoadAssetAtPath<TileBase>($"Assets/Pallete/Pallete1_{i}.asset")).ToArray();
            if (treeTiles.Any(t=>t==null)) throw new InvalidOperationException("Faltan tiles originales del árbol.");
        }
        var origin = terrain.WorldToCell(position);
        for (int y=0;y<3;y++) for(int x=0;x<2;x++)
            (y==2?foreground:terrain).SetTile(origin+new Vector3Int(x,y,0),treeTiles[y*2+x]);
    }
    public static void PaintBoundary(Tilemap colliders, HashSet<Vector2Int> walk, BoundsInt bounds)
    {
        const string path="Assets/Demo/BoundaryCell.asset";
        var tile=AssetDatabase.LoadAssetAtPath<Tile>(path);
        if(tile==null)
        {
            tile=ScriptableObject.CreateInstance<Tile>();
            tile.sprite=AssetDatabase.LoadAssetAtPath<Tile>("Assets/Resources/Demo/Art/NeoGrassA.asset").sprite;
            tile.colliderType=Tile.ColliderType.Grid;
            AssetDatabase.CreateAsset(tile,path);
        }
        colliders.ClearAllTiles();
        foreach(var cell in bounds.allPositionsWithin)
        {
            var c=new Vector2Int(cell.x,cell.y);
            if(!walk.Contains(c) && Cardinal.Any(d=>walk.Contains(c+d))) colliders.SetTile(cell,tile);
        }
    }
    public static void Configure(Tilemap map, string name)
    {
        map.name=name;
        var renderer=map.GetComponent<TilemapRenderer>();
        renderer.mode=TilemapRenderer.Mode.Chunk;
        renderer.sortingLayerName=name=="Colliders"?"Collider":name;
        renderer.sortingOrder=name=="Terrain"?-10000:0;
    }
    static void CopyTiles(Tilemap source,Tilemap destination)
    {
        foreach(var cell in source.cellBounds.allPositionsWithin)
        {
            var tile=source.GetTile(cell);
            if(tile==null)continue;
            destination.SetTile(cell,tile);
            destination.SetTileFlags(cell,TileFlags.None);
            destination.SetColor(cell,source.GetColor(cell));
            destination.SetTransformMatrix(cell,source.GetTransformMatrix(cell));
        }
    }
    public static string MigrateOpenScene()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Salir de Play antes de migrar.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/DemoLevel1.unity" && scene.path!="Assets/Scenes/DemoMenu.unity")throw new InvalidOperationException("Solo se migra la demo.");
        var grid=UnityEngine.Object.FindAnyObjectByType<Grid>();
        var maps=grid.GetComponentsInChildren<Tilemap>();
        if(maps.Length==3 && maps.Any(m=>m.name=="Foreground"))return "La escena ya usa los tres Tilemaps.";
        var scenery=maps.Single(m=>m.name=="Paisaje");
        var oldFloor=maps.Single(m=>m.name=="Suelo transitable");
        var walls=maps.Single(m=>m.name=="Huellas del bosque");
        var ridge=maps.Single(m=>m.name=="Desprendimiento");
        var trees=grid.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.name.StartsWith("Árbol normal",StringComparison.Ordinal)).ToArray();
        foreach(var tree in trees)
            if(tree.GetComponents<Component>().Length!=2 || tree.transform.childCount!=0 || tree.transform.localScale!=Vector3.one || Vector3.Distance(tree.transform.position,(Vector3)scenery.WorldToCell(tree.transform.position))>.001f)
                throw new InvalidOperationException("Árbol con cambios especiales: "+tree.name+". Se conserva la escena sin migrar.");
        var walk=new HashSet<Vector2Int>();
        foreach(var c in oldFloor.cellBounds.allPositionsWithin)if(oldFloor.HasTile(c))walk.Add(new Vector2Int(c.x,c.y));
        var board=grid.GetComponent<NeoGridBoard>();
        var bounds=board!=null?board.navigationBounds:new BoundsInt(0,0,0,32,20,1);
        Undo.RegisterFullObjectHierarchyUndo(grid.gameObject,"Migrar Grid de Neo");
        var fgObject=new GameObject("Foreground",typeof(Tilemap),typeof(TilemapRenderer));
        Undo.RegisterCreatedObjectUndo(fgObject,"Crear Foreground");
        fgObject.transform.SetParent(grid.transform,false);
        var foreground=fgObject.GetComponent<Tilemap>();
        CopyTiles(oldFloor,scenery);
        CopyTiles(ridge,scenery);
        foreach(var tree in trees)
        {
            PaintTree(scenery,foreground,tree.transform.position);
            Undo.DestroyObjectImmediate(tree.gameObject);
        }
        PaintBoundary(walls,walk,bounds);
        Configure(scenery,"Terrain");Configure(walls,"Colliders");Configure(foreground,"Foreground");
        if(board!=null)
        {
            board.floor=scenery;board.walls=walls;
            board.actors=UnityEngine.Object.FindObjectsByType<NeoGridMotor>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(a=>a.board==board).ToArray();
        }
        Undo.DestroyObjectImmediate(oldFloor.gameObject);Undo.DestroyObjectImmediate(ridge.gameObject);
        grid.name="Grid";
        var dynamicObjects=new GameObject("Actores e interacciones");Undo.RegisterCreatedObjectUndo(dynamicObjects,"Agrupar interactivos");
        foreach(var child in grid.GetComponentsInChildren<Transform>(true).Where(t=>t.parent==grid.transform && t.GetComponent<Tilemap>()==null).ToArray())
            Undo.SetTransformParent(child,dynamicObjects.transform,"Separar actores del Grid");
        foreach(var renderer in dynamicObjects.GetComponentsInChildren<SpriteRenderer>(true))renderer.sortingLayerName="Terrain";
        walls.transform.SetSiblingIndex(0);scenery.transform.SetSiblingIndex(1);foreground.transform.SetSiblingIndex(2);
        scenery.RefreshAllTiles();foreground.RefreshAllTiles();Physics2D.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"{scene.name}: tres Tilemaps, {trees.Length} árboles convertidos; {walls.GetUsedTilesCount()} tipo de tile de borde.";
    }
}
