using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// An Edit-mode composition of the forest reference using existing project art.
// This does not regenerate the map at runtime or replace the demo's game systems.
public static class ForestMockAuthoring
{
    const int Width = 64, Height = 48;
    static Tilemap terrain, detail, foreground, collisions;
    static Transform actors;
    static readonly HashSet<Vector3Int> water = new HashSet<Vector3Int>();
    static readonly HashSet<Vector3Int> routes = new HashSet<Vector3Int>();
    static readonly HashSet<Vector3Int> rock = new HashSet<Vector3Int>();
    static readonly HashSet<Vector3Int> bridge = new HashSet<Vector3Int>();
    static readonly HashSet<Vector3Int> treeBases = new HashSet<Vector3Int>();
    static readonly Dictionary<int, TileBase> atlas = new Dictionary<int, TileBase>();
    static readonly List<Vector2Int> landmarks = new List<Vector2Int>();
    static TileBase grass, path, ocean, block, flowers, tallGrass;
    static TileBase[] trunk, crown;

    static Vector3Int C(int x, int y) => new Vector3Int(x, y, 0);
    static bool Inside(Vector3Int c) => c.x >= 0 && c.x < Width && c.y >= 0 && c.y < Height;
    static T Asset<T>(string p) where T : UnityEngine.Object
    {
        var result = AssetDatabase.LoadAssetAtPath<T>(p);
        if (result == null) throw new InvalidOperationException("Missing asset: " + p);
        return result;
    }
    static TileBase A(int id)
    {
        if (!atlas.TryGetValue(id, out var tile))
            atlas[id] = tile = Asset<TileBase>("Assets/Pallete/Pallete1_" + id + ".asset");
        return tile;
    }
    static Sprite Sprite(string path, string name)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
            .FirstOrDefault(s => s.name == name) ?? throw new InvalidOperationException("Missing sprite: " + path + "/" + name);
    }
    static Tilemap Map(Transform parent, string name, string layer, int order, bool solid = false)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(parent, false);
        var renderer = go.GetComponent<TilemapRenderer>();
        renderer.sortingLayerName = layer; renderer.sortingOrder = order;
        if (solid) { go.layer = LayerMask.NameToLayer("Collider"); go.AddComponent<TilemapCollider2D>(); }
        return go.GetComponent<Tilemap>();
    }
    static Tile PlainTile(string name, Sprite sprite)
    {
        string p = "Assets/ForestMock/" + name + ".asset";
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(p);
        if (tile != null) return tile;
        tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite; tile.colliderType = Tile.ColliderType.None;
        AssetDatabase.CreateAsset(tile, p); return tile;
    }
    static void Ellipse(HashSet<Vector3Int> set, float cx, float cy, float rx, float ry)
    {
        for (int y = Mathf.FloorToInt(cy - ry); y <= Mathf.CeilToInt(cy + ry); y++)
            for (int x = Mathf.FloorToInt(cx - rx); x <= Mathf.CeilToInt(cx + rx); x++)
                if (Inside(C(x,y)) && Mathf.Pow((x-cx)/rx,2) + Mathf.Pow((y-cy)/ry,2) <= 1) set.Add(C(x,y));
    }
    static void Stream(Vector2 a, Vector2 b, float radius)
    {
        int steps = Mathf.CeilToInt(Vector2.Distance(a,b) * 3);
        for (int i = 0; i <= steps; i++)
        { var p = Vector2.Lerp(a,b,i/(float)steps); Ellipse(water,p.x,p.y,radius,radius); }
    }
    static void Trail(Vector2Int a, Vector2Int b, int width = 2)
    {
        if (a.x != b.x && a.y != b.y) throw new InvalidOperationException("Use cardinal trail segments.");
        int dx = Math.Sign(b.x-a.x), dy = Math.Sign(b.y-a.y);
        for (int i=0; i<=Math.Abs(b.x-a.x)+Math.Abs(b.y-a.y); i++)
            for (int w=0; w<width; w++)
            { var c=C(a.x+dx*i+(dy!=0?w:0),a.y+dy*i+(dx!=0?w:0)); if(Inside(c))routes.Add(c); }
    }
    static void Wall(int left, int right, int y, int stair = -100)
    {
        for(int x=left;x<=right;x++)
            for(int d=0;d<2;d++)
            {
                var c=C(x,y-d);
                if(x==stair||x==stair+1)
                { terrain.SetTile(c,A(207)); routes.Add(c); continue; }
                if(water.Contains(c)||routes.Contains(c))continue;
                terrain.SetTile(c,A(d==0?(x==left?121:x==right?125:124):145)); rock.Add(c);
            }
    }
    static void VerticalWall(int x, int low, int high)
    {
        for(int y=low;y<=high;y++)
        { var c=C(x,y); if(!water.Contains(c)&&!routes.Contains(c)){terrain.SetTile(c,A(117));rock.Add(c);} }
    }
    static bool OpenClearing(int x,int y)
    {
        return InOval(x,y,12,35,11,7) || InOval(x,y,41,25,9,6)
            || InOval(x,y,52,42,8,5) || InOval(x,y,55,14,8,6)
            || InOval(x,y,58,31,5,5) || InOval(x,y,39,5,5,5);
    }
    static bool InOval(float x,float y,float cx,float cy,float rx,float ry)
        => Mathf.Pow((x-cx)/rx,2)+Mathf.Pow((y-cy)/ry,2)<1;
    static bool CanTree(int x,int y, bool clearing = false)
    {
        if(!clearing&&OpenClearing(x,y))return false;
        for(int yy=0;yy<=2;yy++)for(int xx=0;xx<=1;xx++)
        {
            var c=C(x+xx,y+yy);
            if(!Inside(c)||water.Contains(c)||routes.Contains(c)||rock.Contains(c))return false;
            if(treeBases.Contains(c))return false;
        }
        return true;
    }
    static void Tree(int x,int y)
    {
        for(int xx=0;xx<2;xx++){var c=C(x+xx,y);terrain.SetTile(c,trunk[xx]);treeBases.Add(c);}
        for(int yy=0;yy<2;yy++)for(int xx=0;xx<2;xx++)foreground.SetTile(C(x+xx,y+yy+1),crown[yy*2+xx]);
    }
    static void FlowerPatch(int cx,int cy,int radius)
    {
        for(int y=cy-radius;y<=cy+radius;y++)for(int x=cx-radius;x<=cx+radius;x++)
        {
            var c=C(x,y);
            if(!Inside(c)||water.Contains(c)||rock.Contains(c)||routes.Contains(c)||treeBases.Contains(c)||foreground.HasTile(c))continue;
            if((x*13+y*7)%4==0 && (x-cx)*(x-cx)+(y-cy)*(y-cy)<=radius*radius)detail.SetTile(c,flowers);
        }
    }
    static void Character(string name,string sheet,string sprite,int x,int y,float scale=1)
    {
        var go=new GameObject(name);go.transform.SetParent(actors,false);go.transform.position=new Vector3(x+.5f,y+.5f,0);
        go.transform.localScale=Vector3.one*scale;
        var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=Sprite(sheet,sprite);
        renderer.sortingLayerName="Terrain";renderer.sortingOrder=500-y;
    }
    static void Landmark(int x,int y) => landmarks.Add(new Vector2Int(x,y));

    [MenuItem("Neo/Autoría/Construir bosque de referencia")]
    public static string Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play before authoring.");
        var current=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(current.isDirty)
        {
            string backup=AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/Reference/AntesDelBosque_20261025.unity");
            if(!EditorSceneManager.SaveScene(current,backup,true))throw new InvalidOperationException("Could not preserve current edits.");
            Debug.Log("Forest authoring preserved pending scene as " + backup);
        }
        if(!AssetDatabase.IsValidFolder("Assets/ForestMock"))AssetDatabase.CreateFolder("Assets","ForestMock");
        water.Clear();routes.Clear();rock.Clear();bridge.Clear();treeBases.Clear();atlas.Clear();landmarks.Clear();
        grass=A(1);path=Asset<TileBase>("Assets/Sprites/AniamtedTiles/PathTile.asset");
        ocean=Asset<TileBase>("Assets/Sprites/AniamtedTiles/Ocean.asset");
        block=Asset<TileBase>("Assets/Demo/BoundaryCell.asset");flowers=Asset<TileBase>("Assets/Sprites/AniamtedTiles/AnimatedFlowers.asset");
        tallGrass=PlainTile("HierbaDecorativaFollaje",Sprite("Assets/Sprites/tallgrass.png","tallgrass_0"));
        trunk=new[]{A(486),A(487)};
        crown=new[]{2,3,0,1}.Select(i=>Asset<TileBase>("Assets/Pallete/treeForeground_"+i+".asset")).ToArray();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var grid=new GameObject("Grid",typeof(Grid));grid.GetComponent<Grid>().cellSize=new Vector3(1,1,0);
        collisions=Map(grid.transform,"Colliders","Collider",0,true);
        terrain=Map(grid.transform,"Terrain","Terrain",-10000);
        detail=Map(grid.transform,"Terrain LV1","Terrain",-9999);
        foreground=Map(grid.transform,"Foreground","Foreground",0);
        actors=new GameObject("Personajes y situaciones — referencia visual").transform;
        for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)terrain.SetTile(C(x,y),grass);

        Vector2[] river={new Vector2(39,49),new Vector2(38,40),new Vector2(34,35),new Vector2(29,29),new Vector2(29,23),new Vector2(25,18),new Vector2(23,13),new Vector2(14,9),new Vector2(5,4),new Vector2(-2,2)};
        for(int i=0;i<river.Length-1;i++)Stream(river[i],river[i+1],2.8f);
        Ellipse(water,54,13,5,3.8f);Ellipse(water,58,32,3,2.5f);
        foreach(var c in water)terrain.SetTile(c,ocean);

        Trail(new Vector2Int(39,0),new Vector2Int(39,23));
        Trail(new Vector2Int(39,23),new Vector2Int(21,23));
        Trail(new Vector2Int(21,23),new Vector2Int(21,32));
        Trail(new Vector2Int(21,32),new Vector2Int(9,32));
        Trail(new Vector2Int(9,32),new Vector2Int(9,36));
        Trail(new Vector2Int(39,23),new Vector2Int(49,23));
        Trail(new Vector2Int(49,23),new Vector2Int(49,37));
        Trail(new Vector2Int(49,37),new Vector2Int(52,37));
        Trail(new Vector2Int(52,37),new Vector2Int(52,47));
        Trail(new Vector2Int(40,18),new Vector2Int(59,18));
        Trail(new Vector2Int(59,18),new Vector2Int(59,14));
        Trail(new Vector2Int(49,27),new Vector2Int(61,27));
        Trail(new Vector2Int(61,27),new Vector2Int(61,32));
        Trail(new Vector2Int(21,23),new Vector2Int(15,23));
        Trail(new Vector2Int(15,23),new Vector2Int(15,17));
        foreach(var c in routes)if(!water.Contains(c))terrain.SetTile(c,path);
        var clearing=new HashSet<Vector3Int>();Ellipse(clearing,52,42,5,3);Ellipse(clearing,12,35,4,2);
        foreach(var c in clearing)if(!water.Contains(c)){terrain.SetTile(c,path);routes.Add(c);}

        // The existing white-railed bridge replaces the invented wooden one.
        for(int x=25;x<=33;x++)for(int y=23;y<=24;y++)
        {var c=C(x,y);detail.SetTile(c,A(y==24?245:237));bridge.Add(c);routes.Add(c);}
        Wall(3,9,31);Wall(9,17,30);Wall(17,24,29,21);
        VerticalWall(3,32,39);Wall(5,21,41);
        Wall(35,44,13,39);
        Wall(46,49,37);Wall(49,55,38,52);Wall(55,61,39);VerticalWall(46,38,44);
        Wall(54,62,28,60);VerticalWall(54,29,34);
        // Cliffs frame the upper waterfall without blocking the river itself.
        Wall(31,36,41);Wall(42,45,41);
        for(int x=37;x<=39;x++)for(int row=0;row<3;row++)
            detail.SetTile(C(x,42-row),A(176+(x-37)+row*8));
        // Follow selected bends with rock faces rather than a straight river wall.
        for(int y=14;y<=39;y++)
        {
            if(y>=22&&y<=25)continue;
            var bank=water.Where(c=>c.y==y&&c.x<42).OrderBy(c=>c.x).FirstOrDefault();
            var edge=C(bank.x-1,y);
            if(Inside(edge)&&!routes.Contains(edge)&&!water.Contains(edge))
            {terrain.SetTile(edge,A(117));rock.Add(edge);}
        }
        foreach(var c in new[]{C(30,31),C(24,14),C(9,6)})
            if(water.Contains(c)){detail.SetTile(c,A(189));rock.Add(c);}

        // A visually readable Haunter enclosure and the opened stone access.
        for(int x=40;x<=44;x+=2)if(CanTree(x,27,true))Tree(x,27);
        detail.SetTile(C(46,29),A(305));
        for(int y=24;y<=25;y++){terrain.SetTile(C(37,y),A(189));rock.Add(C(37,y));terrain.SetTile(C(42,y),A(189));rock.Add(C(42,y));}
        for(int y=0;y<Height-2;y+=3)for(int x=(y/3)%2;x<Width-1;x+=2)
        {
            int shiftedY=Mathf.Max(0,y+((x*19+y*7)%3)-1);
            if(CanTree(x,shiftedY) && ((x*19+y*31)%13!=0 || x<4 || x>59 || y<3 || y>43))Tree(x,shiftedY);
        }
        // Overlapping crowns create wooded masses; paths and clearings stay reserved.
        for(int y=1;y<Height-2;y+=2)for(int x=(y/2)%2;x<Width-1;x+=2)
        {
            bool grove=x<7||x>57||y<7||y>42||InOval(x,y,16,11,9,7)
                ||InOval(x,y,24,38,7,7)||InOval(x,y,44,9,7,6)||InOval(x,y,44,34,6,6);
            if(grove&&CanTree(x,y))Tree(x,y);
        }

        // Small authored clusters break the regular forest edge around each place.
        int[] accents={6,26, 10,25, 18,26, 24,34, 25,30, 35,27, 36,18, 43,19, 45,33, 55,39, 59,39, 46,7, 34,6, 34,12};
        for(int i=0;i<accents.Length;i+=2)if(CanTree(accents[i],accents[i+1],true))Tree(accents[i],accents[i+1]);
        for(int x=5;x<=18;x++)for(int y=33;y<=38;y++)
            if((x+y)%4!=0&&!routes.Contains(C(x,y))&&!treeBases.Contains(C(x,y))&&!foreground.HasTile(C(x,y)))detail.SetTile(C(x,y),tallGrass);
        int[] patches={36,5,43,7,35,16,43,15,35,25,45,22,45,31,55,36,57,43,47,44,7,30,17,31,19,38,9,39,18,20,60,20};
        for(int i=0;i<patches.Length;i+=2)FlowerPatch(patches[i],patches[i+1],3);
        for(int i=0;i<5;i++)
        {
            var c=C(33+i*2,10+i*6);
            if(!water.Contains(c)&&!routes.Contains(c)&&!treeBases.Contains(c)&&!foreground.HasTile(c)){terrain.SetTile(c,A(189));rock.Add(c);}
        }
        foreach(var c in rock.Concat(treeBases))collisions.SetTile(c,block);
        // Only the shoreline contour is blocked; do not fill the whole river with colliders.
        Vector3Int[] cardinal={Vector3Int.up,Vector3Int.down,Vector3Int.left,Vector3Int.right};
        foreach(var c in water)if(!bridge.Contains(c)&&cardinal.Any(d=>Inside(c+d)&&!water.Contains(c+d)&&!bridge.Contains(c+d)))collisions.SetTile(c,block);
        // Boundary is explicit; the two authored entry/exit strips stay open.
        for(int x=0;x<Width;x++)foreach(int y in new[]{0,Height-1})if(!routes.Contains(C(x,y)))collisions.SetTile(C(x,y),block);
        for(int y=0;y<Height;y++)foreach(int x in new[]{0,Width-1})if(!routes.Contains(C(x,y)))collisions.SetTile(C(x,y),block);

        Character("Entrenador — entrada","Assets/Sprites/Characters/Red_idle.png","Red_idle_0",39,4);
        Character("Pikachu — compañero","Assets/Sprites/Characters/Pikachu/PikachuIdle.png","PikachuIdle_0",40,2,1.15f);
        Character("Haunter — barrera del bosque","Assets/Sprites/Characters/Hunter.png","Hunter_0",39,29,.85f);
        Character("Psyduck — orilla tranquila","Assets/Sprites/Characters/Psyduck.png","Psyduck_0",59,14,1.1f);
        Character("Dratini — laguna escondida","Assets/Sprites/Characters/Dratini.png","Dratini_0",58,32,1.2f);
        Character("Beedrill — claro oeste A","Assets/Sprites/Characters/Bedrill.png","Bedrill_0",7,36,1.2f);
        Character("Beedrill — claro oeste B","Assets/Sprites/Characters/Bedrill.png","Bedrill_0",15,36,1.2f);
        Character("Beedrill — claro oeste C","Assets/Sprites/Characters/Bedrill.png","Bedrill_0",11,33,1.2f);
        Character("Machoke — guardián del claro","Assets/Sprites/Characters/Machoke.png","Machoke_0",52,43,1.1f);
        var ballSprites=AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/Pokeball.png").OfType<Sprite>().OrderBy(s=>s.rect.x).ToArray();
        if(ballSprites.Length>0)
        {
            foreach(var p in new[]{new Vector2Int(17,34),new Vector2Int(53,44)})
            {var go=new GameObject("Poké Ball — recompensa visual");go.transform.SetParent(actors,false);go.transform.position=new Vector3(p.x+.5f,p.y+.5f,0);var sr=go.AddComponent<SpriteRenderer>();sr.sprite=ballSprites[0];sr.sortingLayerName="Terrain";sr.sortingOrder=500-p.y;}
        }
        foreach(var m in new[]{terrain,detail,foreground,collisions}){m.RefreshAllTiles();m.CompressBounds();}
        foreach(var c in rock){terrain.SetTileFlags(c,TileFlags.None);terrain.SetTransformMatrix(c,Matrix4x4.identity);}
        foreach(var c in routes)if(terrain.GetTile(c)==A(207)){terrain.SetTileFlags(c,TileFlags.None);terrain.SetTransformMatrix(c,Matrix4x4.identity);}
        collisions.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();Physics2D.SyncTransforms();
        Landmark(39,4);Landmark(21,32);Landmark(9,36);Landmark(39,23);Landmark(49,35);Landmark(52,42);Landmark(59,18);Landmark(61,31);
        var cameraObject=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));cameraObject.tag="MainCamera";
        var camera=cameraObject.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=24;camera.transform.position=new Vector3(32,24,-50);camera.backgroundColor=new Color(.1f,.13f,.14f);camera.clearFlags=CameraClearFlags.SolidColor;
        Validate();
        string scenePath=AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/BosqueDelCauce.unity");
        EditorSceneManager.SaveScene(scene,scenePath);AssetDatabase.SaveAssets();
        return scenePath+" | "+treeBases.Count/2+" trees | 4 tilemaps | visual reference scene; no puzzle logic";
    }

    public static string Validate()
    {
        var solid=collisions.GetComponent<TilemapCollider2D>();int blocks=0;
        foreach(var c in collisions.cellBounds.allPositionsWithin)if(collisions.HasTile(c))
        { if(!solid.OverlapPoint(collisions.GetCellCenterWorld(c)))throw new InvalidOperationException("Missing physics at "+c);blocks++; }
        var visited=new HashSet<Vector2Int>();var queue=new Queue<Vector2Int>();queue.Enqueue(new Vector2Int(39,4));visited.Add(queue.Peek());
        Vector2Int[] steps={Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right};
        while(queue.Count>0){var c=queue.Dequeue();foreach(var d in steps){var n=c+d;var v=C(n.x,n.y);if(Inside(v)&&!collisions.HasTile(v)&&(!water.Contains(v)||bridge.Contains(v))&&visited.Add(n))queue.Enqueue(n);}}
        foreach(var point in landmarks)if(!visited.Contains(point))throw new InvalidOperationException("Unreachable landmark: "+point);
        string report="Physical blocks: "+blocks+"; reachable landmarks: "+landmarks.Count+"; four-layer static composition. Gameplay not tested.";
        Directory.CreateDirectory("docs/design/evidencias");File.WriteAllText("docs/design/bosque-validacion.txt",report);return report;
    }
    public static string Capture()
    {
        var cam=Camera.main;var previous=RenderTexture.active;var target=cam.targetTexture;
        var rt=new RenderTexture(1536,1152,24);rt.filterMode=FilterMode.Point;rt.Create();var image=new Texture2D(1536,1152,TextureFormat.RGBA32,false);
        try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1536,1152),0,0);image.Apply();File.WriteAllBytes("docs/design/evidencias/bosque-del-cauce-unity.png",image.EncodeToPNG());}
        finally{cam.targetTexture=target;RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);}
        return "docs/design/evidencias/bosque-del-cauce-unity.png";
    }
}
