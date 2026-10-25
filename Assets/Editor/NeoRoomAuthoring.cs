using System;
using System.Collections.Generic;
using System.Linq;
using Pathfinding;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Explicit one-off authoring recipe. Saved scenes run without this editor tool.</summary>
public static class NeoRoomAuthoring
{
    static Sprite SpriteAt(string path, string name = null)
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
        var found = name == null ? sprites.FirstOrDefault() : sprites.FirstOrDefault(s => s.name == name);
        if (found == null) throw new InvalidOperationException("Sprite no encontrado: " + path + "/" + name);
        return found;
    }
    static Tilemap Map(Transform parent, string name, int order, bool collision = false)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(parent);
        var map = go.GetComponent<Tilemap>();
        var renderer = go.GetComponent<TilemapRenderer>();
        renderer.sortingLayerName = name == "Colliders" ? "Collider" : name;
        renderer.sortingOrder = order;
        if (collision) { go.layer = LayerMask.NameToLayer("Collider"); go.AddComponent<TilemapCollider2D>(); }
        return map;
    }
    static Tile TileAsset(string path, Sprite sprite, Tile.ColliderType collision)
    {
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (tile != null) return tile;
        tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite; tile.colliderType = collision;
        AssetDatabase.CreateAsset(tile, path); return tile;
    }
    static SpriteRenderer Art(string name, Sprite sprite, Vector3 position, Transform parent, int order)
    {
        var go = new GameObject(name); go.transform.SetParent(parent); go.transform.position = position;
        var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
        renderer.sortingLayerName = "Default"; renderer.sortingOrder = order; return renderer;
    }
    static void StripActor(GameObject go)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(go)) PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        foreach (var canvas in go.GetComponentsInChildren<Canvas>(true)) UnityEngine.Object.DestroyImmediate(canvas.gameObject);
        foreach (var component in go.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(component);
        foreach (var component in go.GetComponentsInChildren<Collider2D>(true)) UnityEngine.Object.DestroyImmediate(component);
        foreach (var component in go.GetComponentsInChildren<Rigidbody2D>(true)) UnityEngine.Object.DestroyImmediate(component);
        foreach (var child in go.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
    }
    public static string Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Salir de Play antes de construir.");
        var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (active.isDirty) throw new InvalidOperationException("La escena tiene cambios sin guardar; se conserva abierta.");
        if (!AssetDatabase.IsValidFolder("Assets/Demo")) AssetDatabase.CreateFolder("Assets", "Demo");
        if (!AssetDatabase.IsValidFolder("Assets/Scenes/Reference")) AssetDatabase.CreateFolder("Assets/Scenes", "Reference");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Reference/DemoLevel1_Corredor.unity") == null)
            AssetDatabase.CopyAsset("Assets/Scenes/DemoLevel1.unity", "Assets/Scenes/Reference/DemoLevel1_Corredor.unity");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var grid = new GameObject("Grid", typeof(Grid));
        var board = grid.AddComponent<NeoGridBoard>();
        board.navigationBounds = new BoundsInt(0,0,0,32,20,1);
        var walls = Map(grid.transform, "Colliders", 0, true);
        var terrain = Map(grid.transform, "Terrain", -10000);
        var foreground = Map(grid.transform, "Foreground", 0);
        board.floor = terrain; board.walls = walls;
        var grass = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Resources/Demo/Art/NeoGrassA.asset");
        var path = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Sprites/AniamtedTiles/PathTile.asset");
        var mountain = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Sprites/AniamtedTiles/Mountain.asset");
        var tree = SpriteAt("Assets/Sprites/tree.png", "tree_0");
        var boulder = SpriteAt("Assets/Sprites/MovableRock.png");
        var rangerSprite = SpriteAt("Assets/Demo/Hiker.png", "Hiker_0");
        var walk = new HashSet<Vector2Int>();
        for (int x=4;x<=15;x++) for(int y=4;y<=11;y++)
            if (!(x<=5 && y>=10)) walk.Add(new Vector2Int(x,y));
        for (int x=18;x<=27;x++) for(int y=6;y<=13;y++)
            if (!(x>=26 && y>=12)) walk.Add(new Vector2Int(x,y));
        for(int x=15;x<=19;x++) walk.Add(new Vector2Int(x,9));
        for(int x=-14;x<=46;x++) for(int y=-12;y<=32;y++) terrain.SetTile(new Vector3Int(x,y,0),grass);
        foreach (var cell in walk)
        {
            bool trail = (cell.y>=8 && cell.y<=9 && cell.x>=9 && cell.x<=26) || (cell.x>=7 && cell.x<=9 && cell.y>=5 && cell.y<=8);
            terrain.SetTile((Vector3Int)cell, trail ? path : grass);
        }
        NeoTilemapMigration.PaintBoundary(walls,walk,board.navigationBounds);
        // A continuous rocky ridge explains the choke point; forest fills the other boundaries.
        for(int x=16;x<=17;x++) for(int y=2;y<=17;y++) if(y!=9) terrain.SetTile(new Vector3Int(x,y,0),mountain);
        for(int x=-10;x<=40;x+=2) for(int y=-6;y<=26;y+=2)
        {
            bool clear=false;
            for(int dx=0;dx<2;dx++) for(int dy=0;dy<2;dy++) if(walk.Contains(new Vector2Int(x+dx,y+dy))) clear=true;
            if(clear || (x==16 && y>=2 && y<=16)) continue;
            NeoTilemapMigration.PaintTree(terrain,foreground,new Vector3(x,y,0));
        }
        var astarGo = new GameObject("Navegación fija A*", typeof(AstarPath));
        var astar = astarGo.GetComponent<AstarPath>(); astar.scanOnStartup=false; board.pathfinder=astar;
        astar.ConfigureReferencesInternal();
        astar.data.FindGraphTypes();
        var graph=(GridGraph)astar.data.AddGraph(typeof(GridGraph));
        graph.SetDimensions(32,20,1); graph.center=new Vector3(16,10,0); graph.rotation=new Vector3(-90,0,0); graph.neighbours=NumNeighbours.Four;
        astar.data.SetData(astar.data.SerializeGraphs());
        var controllerGo = new GameObject("Control y progreso");
        var controller = controllerGo.AddComponent<Controller>();
        controllerGo.AddComponent<NeoAimIndicator>().controller=controller;
        var room = controllerGo.AddComponent<NeoFirstRoom>(); room.board=board; room.controller=controller;
        room.humanStart=new Vector2Int(5,6); room.companionStart=new Vector2Int(6,6); room.safeExit=new Vector2Int(22,9);
        var humanGo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player 1.prefab"));
        StripActor(humanGo); humanGo.name="Explorador"; humanGo.transform.SetParent(grid.transform); humanGo.transform.position=NeoGridBoard.Center(room.humanStart);
        humanGo.AddComponent<Seeker>(); humanGo.AddComponent<Interactable>();
        var humanMotor=humanGo.AddComponent<NeoGridMotor>(); humanMotor.board=board; humanMotor.visual=humanGo.transform.Find("Render");
        var humanMove=humanGo.AddComponent<PlayerMovement>(); humanMove.render=humanMotor.visual;
        var human=humanGo.AddComponent<PlayerAI>(); room.human=human;
        var machopGo=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Machop.prefab"));
        machopGo.name="Machop"; machopGo.transform.position=Vector3.zero;
        machopGo.AddComponent<Seeker>(); machopGo.AddComponent<Interactable>();
        var partnerMotor=machopGo.AddComponent<NeoGridMotor>(); partnerMotor.visual=machopGo.transform.Find("Render");
        var partnerMove=machopGo.AddComponent<PartnerMovement>(); partnerMove.render=partnerMotor.visual;
        machopGo.AddComponent<PartnerAI>(); machopGo.AddComponent<BasicCombatScript>(); machopGo.AddComponent<DemoPartnerCombat>();
        PrefabUtility.SaveAsPrefabAsset(machopGo,"Assets/Demo/MachopCompanion.prefab");
        UnityEngine.Object.DestroyImmediate(machopGo);
        machopGo=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Demo/MachopCompanion.prefab"));
        machopGo.transform.SetParent(grid.transform); machopGo.transform.position=NeoGridBoard.Center(room.companionStart);
        machopGo.GetComponent<NeoGridMotor>().board=board;
        var partner=machopGo.GetComponent<PartnerAI>(); partner.master=humanGo.transform; partner.HOVER_DISTANCE=1; room.machop=partner;
        foreach(var renderer in humanGo.GetComponentsInChildren<SpriteRenderer>()) { renderer.sortingLayerName="Terrain"; renderer.sortingOrder=15; }
        foreach(var renderer in machopGo.GetComponentsInChildren<SpriteRenderer>()) { renderer.sortingLayerName="Terrain"; renderer.sortingOrder=15; }
        machopGo.SetActive(false);
        controller.controllablesManual=new List<Controllable>{human};
        controller.interactables=LayerMask.GetMask("Movable","Characters");
        var ranger=Art("Guardabosques",rangerSprite,new Vector3(7.5f,7.5f,0),grid.transform,15).gameObject;
        ranger.layer=LayerMask.NameToLayer("Characters"); ranger.AddComponent<BoxCollider2D>().isTrigger=true;
        ranger.AddComponent<NeoRanger>().room=room;
        var rockGo=Art("Roca que bloquea el paso",boulder,NeoGridBoard.Center(new Vector2Int(15,9)),grid.transform,5).gameObject;
        rockGo.layer=LayerMask.NameToLayer("Movable"); rockGo.AddComponent<BoxCollider2D>().size=Vector2.one*.95f;
        var rock=rockGo.AddComponent<NeoPushRock>(); rock.board=board; rock.initialCell=new Vector2Int(15,9); rock.solutionCell=new Vector2Int(15,10); room.rock=rock;
        board.sceneRocks=new[]{rock};
        board.actors=new[]{humanMotor,machopGo.GetComponent<NeoGridMotor>()};
        var actors=new GameObject("Actores e interacciones");
        foreach(var child in grid.GetComponentsInChildren<Transform>(true).Where(t=>t.parent==grid.transform && t.GetComponent<Tilemap>()==null).ToArray()) child.SetParent(actors.transform,true);
        foreach(var renderer in actors.GetComponentsInChildren<SpriteRenderer>(true))renderer.sortingLayerName="Terrain";
        var cameraGo=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener),typeof(CameraScript)); cameraGo.tag="MainCamera";
        var camera=cameraGo.GetComponent<Camera>(); camera.orthographic=true; camera.orthographicSize=7.5f; camera.backgroundColor=new Color(.2f,.4f,.3f);
        camera.allowHDR=false; camera.allowMSAA=false; camera.allowDynamicResolution=false;
        if(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline!=null) throw new InvalidOperationException("La sala usa el renderizado Built-in de Neo.");
        var pixels=cameraGo.AddComponent<UnityEngine.U2D.PixelPerfectCamera>();
        pixels.assetsPPU=16; pixels.refResolutionX=320; pixels.refResolutionY=240; pixels.upscaleRT=true; pixels.cropFrameX=false; pixels.cropFrameY=false;
        cameraGo.transform.position=new Vector3(12,8,-10);
        cameraGo.transform.SetParent(humanGo.transform, true);
        cameraGo.transform.localPosition=new Vector3(0,0,-10);
        var follow=cameraGo.GetComponent<CameraScript>(); follow.target=humanGo.transform; follow.clampToLevel=false; follow.levelMin=new Vector2(0,0); follow.levelMax=new Vector2(32,20); controller.mainCamera=follow;
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/DemoLevel1.unity");
        var palette=AssetDatabase.LoadAssetAtPath<NeoArtPalette>("Assets/Demo/NeoPalette.asset");
        if(palette==null){ palette=ScriptableObject.CreateInstance<NeoArtPalette>(); AssetDatabase.CreateAsset(palette,"Assets/Demo/NeoPalette.asset"); }
        palette.entries=new[]{
            new NeoArtPalette.Entry{label="Árbol de bosque",state="Normal, sin fuego",sprite=tree,footprint=new Vector2Int(2,2)},
            new NeoArtPalette.Entry{label="Roca móvil",state="Empujable por Machop",sprite=boulder,footprint=Vector2Int.one},
            new NeoArtPalette.Entry{label="Guardabosques",state="Diálogo",sprite=rangerSprite,footprint=Vector2Int.one},
            new NeoArtPalette.Entry{label="Machop",state="Compañero",sprite=machopGo.GetComponentInChildren<SpriteRenderer>(true).sprite,footprint=Vector2Int.one}
        };
        EditorUtility.SetDirty(palette); AssetDatabase.SaveAssets();
        return "Sala persistida, Machop variante y paleta creados.";
    }

    public static string CreateMenu()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Salir de Play.");
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Hay cambios sin guardar.");
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/DemoLevel1.unity");
        foreach(var actor in UnityEngine.Object.FindObjectsByType<NeoGridMotor>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            actor.gameObject.SetActive(true);
        foreach(var component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(component is not UnityEngine.U2D.PixelPerfectCamera) UnityEngine.Object.DestroyImmediate(component);
        foreach(var collider in UnityEngine.Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            UnityEngine.Object.DestroyImmediate(collider);
        foreach(var animator in UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None)) animator.updateMode=AnimatorUpdateMode.UnscaledTime;
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/DemoMenu.unity");
        EditorSceneManager.OpenScene("Assets/Scenes/DemoLevel1.unity");
        return "Menú con paisaje persistido sin simulación de partida.";
    }

    public static string BuildWindows()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Salir de Play antes de compilar el ejecutable.");
        string folder=System.IO.File.Exists("Builds/NeoPaso01/Neo.exe") ? "Builds/NeoPaso01-"+DateTime.Now.ToString("yyyyMMdd-HHmmss") : "Builds/NeoPaso01";
        var output=System.IO.Path.GetFullPath(folder+"/Neo.exe");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes=new[]{"Assets/Scenes/DemoMenu.unity","Assets/Scenes/DemoLevel1.unity"},
            locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development
        });
        var result=$"{report.summary.result}: {report.summary.totalErrors} errores, {report.summary.totalWarnings} advertencias. {output}";
        System.IO.File.WriteAllText("Temp/neo-windows-build.txt",result);
        if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new InvalidOperationException(result);
        return result;
    }
}
