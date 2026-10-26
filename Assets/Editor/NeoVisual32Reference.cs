using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pathfinding;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

/// <summary>Isolated, reproducible art review before promoting the 32-pixel catalogue.</summary>
public static class NeoVisual32Reference
{
    public const string Root = "Assets/Visual32";
    public const string ScenePath = "Assets/Scenes/Visual32Reference.unity";
    static readonly string[] Directions = { "Front", "Back", "Side" };

    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = path.Substring(0, path.LastIndexOf('/')); Folder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }

    static Sprite[] Sheet(string name, int width, int height, int columns, int rows)
    {
        string path = Root + "/Art/" + name + ".png";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = NeoVisualStandard.PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        var factories = new SpriteDataProviderFactories(); factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var existing = provider.GetSpriteRects().ToDictionary(r => r.name);
        var rects = Enumerable.Range(0, columns * rows).Select(i => {
            string label = name + "_" + i;
            return new SpriteRect { name = label,
                rect = new Rect(i % columns * width, (rows - 1 - i / columns) * height, width, height),
                pivot = new Vector2(.5f, .5f), alignment = SpriteAlignment.Center,
                spriteID = existing.TryGetValue(label, out var old) ? old.spriteID : UnityEngine.GUID.Generate() };
        }).ToArray();
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply(); importer.SaveAndReimport();
        var frames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
        if (frames.Length != rects.Length) throw new InvalidOperationException("Incorrect sheet: " + name);
        return frames;
    }

    static AnimationClip Clip(string name, string pose, Sprite[] frames, float fps)
    {
        string folder = Root + "/Animations/" + name; Folder(folder);
        string path = folder + "/" + pose + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
        clip.frameRate = fps;
        bool exactWalkCycle = name == "Explorer" && pose.StartsWith("Walk", StringComparison.Ordinal);
        var keys = new ObjectReferenceKeyframe[frames.Length + (exactWalkCycle ? 0 : 1)];
        for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
        if (!exactWalkCycle) keys[frames.Length] = new ObjectReferenceKeyframe { time = frames.Length / fps, value = frames[frames.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = !pose.StartsWith("Action", StringComparison.Ordinal);
        if (exactWalkCycle) settings.stopTime = frames.Length / fps;
        AnimationUtility.SetAnimationClipSettings(clip, settings); EditorUtility.SetDirty(clip); AssetDatabase.SaveAssetIfDirty(clip);
        return clip;
    }

    static AnimatorController Controller(string name, Sprite[] frames)
    {
        string path = Root + "/Animations/" + name + "/" + name + ".controller";
        Folder(Root + "/Animations/" + name);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        bool create = controller == null;
        if (create) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        if (create) foreach (string parameter in new[] { "RunDown", "RunUp", "RunHorizontal" }) controller.AddParameter(parameter, AnimatorControllerParameterType.Bool);
        if (!controller.parameters.Any(p => p.name == "ActionPlaying")) controller.AddParameter("ActionPlaying", AnimatorControllerParameterType.Bool);
        foreach (string direction in Directions)
            if (!controller.parameters.Any(p => p.name == "Action" + direction)) controller.AddParameter("Action" + direction, AnimatorControllerParameterType.Trigger);
        var machine = controller.layers[0].stateMachine;
        var parameters = new[] { "RunDown", "RunUp", "RunHorizontal" };
        for (int row = 0; row < 3; row++)
        {
            int offset = row * 10;
            var idle = Clip(name, "Idle" + Directions[row], frames.Skip(offset).Take(2).ToArray(), 2);
            var walk = Clip(name, "Walk" + Directions[row], frames.Skip(offset + 2).Take(4).ToArray(), name == "Explorer" ? 16 : 8);
            var action = Clip(name, "Action" + Directions[row], frames.Skip(offset + 6).Take(4).ToArray(), 8);
            var idleState = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == idle.name) ?? machine.AddState(idle.name);
            var walkState = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == walk.name) ?? machine.AddState(walk.name);
            idleState.motion = idle; walkState.motion = walk;
            var actionState = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == action.name) ?? machine.AddState(action.name);
            actionState.motion = action;
            if (!machine.anyStateTransitions.Any(t => t.destinationState == actionState))
            {
                var attack = machine.AddAnyStateTransition(actionState);
                attack.AddCondition(AnimatorConditionMode.If, 0, action.name); attack.hasExitTime = false; attack.duration = 0;
            }
            if (actionState.transitions.Length == 0)
            {
                var finish = actionState.AddTransition(idleState); finish.hasExitTime = true; finish.exitTime = 1; finish.duration = 0;
            }
            foreach (var transition in machine.anyStateTransitions.Where(t => t.destinationState == walkState))
                if (!transition.conditions.Any(c => c.parameter == "ActionPlaying")) transition.AddCondition(AnimatorConditionMode.IfNot, 0, "ActionPlaying");
            if (row == 0) machine.defaultState = idleState;
            if (!create) continue;
            var enter = machine.AddAnyStateTransition(walkState);
            enter.AddCondition(AnimatorConditionMode.If, 0, parameters[row]); enter.hasExitTime = false; enter.duration = 0; enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.IfNot, 0, "ActionPlaying");
            var stop = walkState.AddTransition(idleState);
            stop.AddCondition(AnimatorConditionMode.IfNot, 0, parameters[row]); stop.hasExitTime = false; stop.duration = 0;
        }
        EditorUtility.SetDirty(controller); AssetDatabase.SaveAssetIfDirty(controller); return controller;
    }

    static Tile Tile(string name, Sprite sprite, Tile.ColliderType collider = UnityEngine.Tilemaps.Tile.ColliderType.None)
    {
        string path = Root + "/Tiles/" + name + ".asset";
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (tile == null) { tile = ScriptableObject.CreateInstance<Tile>(); AssetDatabase.CreateAsset(tile, path); }
        tile.sprite = sprite; tile.colliderType = collider; EditorUtility.SetDirty(tile); AssetDatabase.SaveAssetIfDirty(tile); return tile;
    }

    static AnimatedTile Animation(string name, Sprite[] frames, float min, float max)
    {
        string path = Root + "/Tiles/" + name + ".asset";
        var tile = AssetDatabase.LoadAssetAtPath<AnimatedTile>(path);
        if (tile == null) { tile = ScriptableObject.CreateInstance<AnimatedTile>(); AssetDatabase.CreateAsset(tile, path); }
        tile.m_AnimatedSprites = frames; tile.m_MinSpeed = min; tile.m_MaxSpeed = max;
        tile.m_TileColliderType = UnityEngine.Tilemaps.Tile.ColliderType.None;
        EditorUtility.SetDirty(tile); AssetDatabase.SaveAssetIfDirty(tile); return tile;
    }

    static Tilemap Layer(GameObject parent, string name, int order)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer)); go.transform.SetParent(parent.transform);
        var renderer = go.GetComponent<TilemapRenderer>(); renderer.sortingLayerName = "Terrain"; renderer.sortingOrder = order;
        return go.GetComponent<Tilemap>();
    }

    static SpriteRenderer SetArt(GameObject go, string name, Dictionary<string, Sprite[]> sheets)
    {
        var render = go.GetComponentsInChildren<SpriteRenderer>(true).First(r => r.transform.name == "Render");
        render.sprite = sheets[name][0]; render.sortingLayerName = "Terrain";
        render.transform.localScale = Vector3.one;
        var motor = go.GetComponent<NeoGridMotor>();
        if (name == "Granugo") motor.footprint = new Vector2Int(2, 1);
        var footprint = motor != null ? motor.footprint : Vector2Int.one;
        // The canvas may extend above the occupied cells; keep its feet anchored to the lower row.
        float canvasHeight = sheets[name][0].rect.height / NeoVisualStandard.PixelsPerUnit;
        render.transform.localPosition = new Vector3((footprint.x - 1) * .5f, (canvasHeight - 1) * .5f, 0);
        render.GetComponent<Animator>().runtimeAnimatorController = Controller(name, sheets[name]);
        var wild = go.GetComponent<NeoWildPokemon>(); if (wild != null) wild.breathAmount = 0;
        if (go.GetComponent<DemoPartnerCombat>() != null && go.GetComponent<NeoDirectionalSpriteAction>() == null) go.AddComponent<NeoDirectionalSpriteAction>();
        return render;
    }

    static void ConfigureCamera(Camera camera)
    {
        // These are separate implementations. Never attach the Built-in component to a URP camera.
        var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        if (!(pipeline is UniversalRenderPipelineAsset)) throw new InvalidOperationException("The 32px reference expects the project's URP renderer.");
        var legacy = camera.GetComponent<UnityEngine.U2D.PixelPerfectCamera>(); if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy);
        var pixels = camera.GetComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>();
        if (pixels == null) pixels = camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>();
        pixels.assetsPPU = NeoVisualStandard.PixelsPerUnit;
        pixels.refResolutionX = NeoVisualStandard.ReferenceWidth; pixels.refResolutionY = NeoVisualStandard.ReferenceHeight;
        pixels.gridSnapping = UnityEngine.Rendering.Universal.PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
        pixels.cropFrame = UnityEngine.Rendering.Universal.PixelPerfectCamera.CropFrame.Windowbox;
        camera.orthographic = true; camera.orthographicSize = NeoVisualStandard.ReferenceHeight / (2f * NeoVisualStandard.PixelsPerUnit);
        camera.allowHDR = false; camera.allowMSAA = false; camera.allowDynamicResolution = false;
        var additional = camera.GetUniversalAdditionalCameraData(); additional.antialiasing = AntialiasingMode.None; additional.renderPostProcessing = false;
        camera.backgroundColor = new Color(.1f, .16f, .13f);
    }

    [MenuItem("Neo/Visual 32/Crear referencia jugable")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before building the art reference.");
        var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (active.isDirty && !string.IsNullOrEmpty(active.path))
        {
            Directory.CreateDirectory("Temp/Visual32Backup");
            string backup = "Temp/Visual32Backup/" + active.name + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".unity";
            if (!EditorSceneManager.SaveScene(active, backup, true) || !EditorSceneManager.SaveScene(active))
                throw new IOException("Could not preserve the active scene before opening the reference.");
        }
        // Changing scenes can unload assets held only by temporary editor code.
        // Load the new catalogue after that transition, not before it.
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        foreach (string folder in new[] { Root + "/Tiles", Root + "/Animations", Root + "/Prefabs" }) Folder(folder);
        var sheets = new Dictionary<string, Sprite[]>();
        int tilePixels = NeoVisualStandard.TilePixels;
        foreach (string name in new[] { "Lumin", "Brispa", "Pedrugo", "Granugo", "Explorer" })
            sheets[name] = Sheet(name, name == "Granugo" ? NeoVisualStandard.DoubleTilePixels : tilePixels,
                name == "Granugo" ? NeoVisualStandard.DoubleTilePixels : name == "Explorer" ? NeoVisualStandard.TallCharacterPixels : tilePixels, 10, 3);
        var terrainSprites = Sheet("Terrain", tilePixels, tilePixels, 4, 2);
        var treeSprites = Sheet("Tree", tilePixels, tilePixels, 2, 3);
        var flowers = Sheet("BlueFlowers", tilePixels, tilePixels, 5, 1);
        var rockFrame = Sheet("PushRock", tilePixels, tilePixels, 1, 1)[0];
        var grass = Tile("Grass", terrainSprites[0]); var path = Tile("Path", terrainSprites[1]);
        var cliff = Tile("Cliff", terrainSprites[4]); var top = Tile("Plateau", terrainSprites[5]); var stairs = Tile("Stairs", terrainSprites[6]);
        var block = Tile("Collision", terrainSprites[4], UnityEngine.Tilemaps.Tile.ColliderType.Grid);
        var water = Animation("River", new[] { terrainSprites[2], terrainSprites[3] }, 2, 2);
        var flower = Animation("BlueFlowers", flowers, 2, 3);
        var tree = treeSprites.Select((s, i) => Tile("Tree_" + i, s)).ToArray();

        var light = new GameObject("Daylight", typeof(Light2D)).GetComponent<Light2D>();
        light.lightType = Light2D.LightType.Global; light.intensity = 1; light.color = Color.white;
        var grid = new GameObject("Grid", typeof(Grid), typeof(NeoGridBoard));
        var floor = Layer(grid, "Terrain", -10000); var overlay = Layer(grid, "Terrain LV1", -9998);
        var river = Layer(grid, "Water", -9999); var decorations = Layer(grid, "Decorations", -9997);
        var foreground = Layer(grid, "Foreground", 10000); var walls = Layer(grid, "Colliders", -10001);
        string materialPath = Root + "/TreeCutout.mat";
        var cutout = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (cutout == null)
        {
            cutout = new Material(Shader.Find("Neo/Visual32/PixelCutout"));
            AssetDatabase.CreateAsset(cutout, materialPath);
        }
        overlay.GetComponent<TilemapRenderer>().sharedMaterial = cutout;
        foreground.GetComponent<TilemapRenderer>().sharedMaterial = cutout;
        walls.gameObject.layer = LayerMask.NameToLayer("Collider");
        walls.gameObject.AddComponent<TilemapCollider2D>(); walls.GetComponent<TilemapRenderer>().enabled = false;
        for (int y = 0; y < 10; y++) for (int x = 0; x < 18; x++)
        {
            var cell = new Vector3Int(x, y, 0); floor.SetTile(cell, y == 4 ? path : grass);
            if (x == 0 || x == 17 || y == 0 || y == 9) { overlay.SetTile(cell, cliff); walls.SetTile(cell, block); }
            if (x >= 1 && x <= 3 && y >= 1 && y <= 8 && y != 4) { river.SetTile(cell, water); walls.SetTile(cell, block); }
            if (x >= 6 && x <= 12 && y == 7) { overlay.SetTile(cell, x == 9 ? stairs : cliff); if (x != 9) walls.SetTile(cell, block); }
            if (x >= 6 && x <= 12 && y == 8) floor.SetTile(cell, top);
        }
        foreach (var origin in new[] { new Vector2Int(14, 1), new Vector2Int(4, 6) })
        {
            for (int row = 0; row < 3; row++) for (int col = 0; col < 2; col++)
                (row == 2 ? overlay : foreground).SetTile(new Vector3Int(origin.x + col, origin.y + 2 - row, 0), tree[row * 2 + col]);
            walls.SetTile((Vector3Int)origin, block); walls.SetTile((Vector3Int)(origin + Vector2Int.right), block);
        }
        foreach (var cell in new[] { new Vector3Int(5, 2, 0), new Vector3Int(10, 2, 0), new Vector3Int(7, 5, 0), new Vector3Int(12, 5, 0) }) decorations.SetTile(cell, flower);
        var board = grid.GetComponent<NeoGridBoard>(); board.floor = floor; board.walls = walls;
        board.navigationBounds = new BoundsInt(0, 0, 0, 18, 10, 1);
        var navigation = new GameObject("Navigation", typeof(AstarPath)); board.pathfinder = navigation.GetComponent<AstarPath>();
        board.pathfinder.scanOnStartup = false;
        board.pathfinder.ConfigureReferencesInternal();
        board.pathfinder.data.FindGraphTypes();
        var graph = (GridGraph)board.pathfinder.data.AddGraph(typeof(GridGraph));
        graph.SetDimensions(18, 10, 1); graph.center = new Vector3(9, 5, 0); graph.rotation = new Vector3(-90, 0, 0);
        board.pathfinder.data.SetData(board.pathfinder.data.SerializeGraphs());
        var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerWithCamera.prefab"));
        PrefabUtility.UnpackPrefabInstance(player, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        player.name = "Explorador 32"; player.transform.position = NeoGridBoard.Center(new Vector2Int(9, 4));
        SetArt(player, "Explorer", sheets); player.GetComponent<NeoGridMotor>().board = board;
        var camera = player.GetComponentInChildren<Camera>(true); ConfigureCamera(camera);
        camera.transform.localPosition = new Vector3(0, 0, -10);
        var follow = camera.GetComponent<CameraScript>(); follow.target = player.transform; follow.clampToLevel = false;
        var control = new GameObject("Controls", typeof(Controller), typeof(NeoExplorationScene));
        var controller = control.GetComponent<Controller>(); controller.mainCamera = follow;
        controller.controllablesManual = new List<Controllable> { player.GetComponent<PlayerAI>() };
        controller.interactables = LayerMask.GetMask("Movable", "Characters");
        var exploration = control.GetComponent<NeoExplorationScene>(); exploration.board = board; exploration.controller = controller;
        var actors = new List<NeoGridMotor> { player.GetComponent<NeoGridMotor>() };
        var placements = new[] {
            new { name = "Lumin", legacy = "Pikachu", cell = new Vector2Int(7, 3) },
            new { name = "Brispa", legacy = "Beedrill", cell = new Vector2Int(6, 5) },
            new { name = "Pedrugo", legacy = "Machop", cell = new Vector2Int(11, 3) },
            new { name = "Granugo", legacy = "Machoke", cell = new Vector2Int(13, 6) }
        };
        foreach (var entry in placements)
        {
            string source = entry.legacy == "Machop" ? "Assets/Demo/MachopCompanion.prefab" : "Assets/Prefabs/Pokemon/" + entry.legacy + ".prefab";
            var actor = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source));
            PrefabUtility.UnpackPrefabInstance(actor, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            actor.name = entry.name + " 32"; actor.transform.position = NeoGridBoard.Center(entry.cell); actor.SetActive(true);
            SetArt(actor, entry.name, sheets); var motor = actor.GetComponent<NeoGridMotor>(); motor.board = board; actors.Add(motor);
            var partner = actor.GetComponent<PartnerAI>();
            if (partner != null) { partner.master = player.transform; controller.controllablesManual.Add(partner); }
        }
        board.actors = actors.ToArray();
        var rockGo = new GameObject("Roca de prueba", typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(NeoPushRock));
        rockGo.transform.position = NeoGridBoard.Center(new Vector2Int(11, 5)); rockGo.layer = LayerMask.NameToLayer("Movable");
        var rockSprite = rockGo.GetComponent<SpriteRenderer>(); rockSprite.sprite = rockFrame; rockSprite.sortingLayerName = "Terrain";
        rockGo.GetComponent<BoxCollider2D>().size = Vector2.one * .9f;
        var rock = rockGo.GetComponent<NeoPushRock>(); rock.board = board; rock.initialCell = new Vector2Int(11, 5); rock.solutionCell = new Vector2Int(11, 6); board.sceneRocks = new[] { rock };
        board.navigationTarget = player.transform;
        if (!floor.HasTile(new Vector3Int(9, 4, 0))) throw new InvalidOperationException("Reference floor was not painted.");
        foreach (var map in grid.GetComponentsInChildren<Tilemap>())
        {
            map.RefreshAllTiles(); map.CompressBounds(); EditorUtility.SetDirty(map);
        }
        EditorUtility.SetDirty(board.pathfinder);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
        Selection.activeGameObject = player;
        Debug.Log("Referencia 32 lista. WASD: caminar; ESPACIO: controlar a Pedrugo; E: empujar la roca.");
    }
}
