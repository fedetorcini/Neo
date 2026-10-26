using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pathfinding;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Builds every wandering Pokémon from its sprite sheet: idle/walk clips, an Animator Controller
// driven by NeoGridMotor (RunDown, RunUp, RunHorizontal) and a prefab sized to its canonical footprint.
// Side clips face left; the motor mirrors the visual when walking right.
public static class NeoPokemonAuthoring
{
    sealed class Species
    {
        public string name, sheet; // A .png sheet, or a folder whose sheets are all searched.
        public string[] idleFront, idleBack, idleSide, walkFront, walkBack, walkSide;
        public float idleFps = 3, walkFps = 8, speed = 2.5f, hover;
        public Vector2Int footprint = Vector2Int.one;
        public int wanderRadius = 3;
    }

    // Footprints follow canonical size: up to ~1 m is 1x1, long bodies lie 2x1, large or bulky
    // ones 2x2. The biggest Pokémon will reach 3x4.
    static Species[] All => new[]
    {
        // Pikachu 0,4 m: one idle frame per direction (front, side, back), two walk frames.
        new Species
        {
            name = "Pikachu", sheet = "Assets/Sprites/Characters/Pikachu/",
            idleFront = F("PikachuIdle", 0), idleSide = F("PikachuIdle", 1), idleBack = F("PikachuIdle", 2),
            walkFront = F("PikachuWalk", 0, 1), walkSide = F("PikachuWalk", 2, 3), walkBack = F("PikachuWalk", 4, 5),
            walkFps = 7, speed = 3,
        },
        // Psyduck 0,8 m. Sheet 7x3 (front, back, side): columns 0-2 stand and step; 3 onwards are other poses.
        new Species
        {
            name = "Psyduck", sheet = "Assets/Sprites/Characters/Psyduck.png",
            idleFront = F("Psyduck", 0), idleBack = F("Psyduck", 7), idleSide = F("Psyduck", 14),
            walkFront = F("Psyduck", 0, 1, 0, 2), walkBack = F("Psyduck", 7, 8, 7, 9), walkSide = F("Psyduck", 14, 15, 14, 16),
        },
        // Beedrill 1,0 m, always flying: the wing beat (columns 0-5) is both idle and walk.
        new Species
        {
            name = "Beedrill", sheet = "Assets/Sprites/Characters/Bedrill.png",
            idleFront = F("Bedrill", 0, 1, 2, 3, 4, 5), idleBack = F("Bedrill", 14, 15, 16, 17, 18, 19), idleSide = F("Bedrill", 28, 29, 30, 31, 32, 33),
            walkFront = F("Bedrill", 0, 1, 2, 3, 4, 5), walkBack = F("Bedrill", 14, 15, 16, 17, 18, 19), walkSide = F("Bedrill", 28, 29, 30, 31, 32, 33),
            idleFps = 10, walkFps = 14, speed = 3, hover = .12f,
        },
        // Dratini 1,8 m long: lies along 2x1. Front/back coil (1-3); the side slithers between stretched poses.
        new Species
        {
            name = "Dratini", sheet = "Assets/Sprites/Characters/Dratini.png",
            footprint = new Vector2Int(2, 1), wanderRadius = 2,
            idleFront = F("Dratini", 0, 4), idleBack = F("Dratini", 7, 11), idleSide = F("Dratini", 14),
            walkFront = F("Dratini", 1, 2, 3, 2), walkBack = F("Dratini", 8, 9, 10, 9), walkSide = F("Dratini", 14, 18, 14, 20),
            idleFps = 1.5f, walkFps = 7, speed = 2,
        },
        // Haunter 1,6 m and wide: 2x2, floating.
        new Species
        {
            name = "Haunter", sheet = "Assets/animations/Hunter/Hunter.png",
            footprint = new Vector2Int(2, 2), wanderRadius = 2,
            idleFront = F("Hunter", 0, 1), idleBack = F("Hunter", 12, 13), idleSide = F("Hunter", 24, 25),
            walkFront = F("Hunter", 3, 2, 3, 4), walkBack = F("Hunter", 15, 14, 15, 16), walkSide = F("Hunter", 27, 26, 27, 28),
            speed = 2, hover = .15f,
        },
        // Tall drawing, two ground cells. The upper body does not occupy another row.
        new Species
        {
            name = "Machoke", sheet = "Assets/Sprites/Characters/Machoke.png",
            footprint = new Vector2Int(2, 1), wanderRadius = 2,
            idleFront = F("Machoke", 0), idleBack = F("Machoke", 8), idleSide = F("Machoke", 16),
            walkFront = F("Machoke", 0, 1, 0, 2), walkBack = F("Machoke", 8, 9, 8, 10), walkSide = F("Machoke", 16, 17, 16, 18),
            walkFps = 7, speed = 2,
        },
    };
    static string[] F(string prefix, params int[] indices) => indices.Select(i => prefix + "_" + i).ToArray();

    const float Fill = .95f; // Share of the footprint the drawing may cover.

    [MenuItem("Neo/Pokémon/Generar Pokémon (animaciones, tamaño y prefabs)")]
    public static void GenerateAll()
    {
        foreach (var species in All) Generate(species);
        AssetDatabase.SaveAssets();
        Debug.Log("Pokémon generados: clips en Assets/animations/<Pokémon>/, prefabs en Assets/Prefabs/Pokemon/");
    }

    static void Generate(Species s)
    {
        var sheets = s.sheet.EndsWith("/") ? Directory.GetFiles(s.sheet, "*.png").Select(p => p.Replace('\\', '/')).ToArray() : new[] { s.sheet };
        var sprites = sheets.SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<Sprite>().ToDictionary(x => x.name);
        Sprite[] Frames(string[] names) => names.Select(n => sprites.TryGetValue(n, out var sp) ? NeoCreatureArt.Remap(sp) : throw new InvalidOperationException("Falta el sprite " + n)).ToArray();

        var controller = BuildController(s, Frames);
        var used = Frames(s.idleFront.Concat(s.idleBack).Concat(s.idleSide).Concat(s.walkFront).Concat(s.walkBack).Concat(s.walkSide).Distinct().ToArray());
        BuildPrefab(s, controller, Frames(s.idleFront)[0], OpaqueBounds(used));
    }

    static AnimatorController BuildController(Species s, Func<string[], Sprite[]> frames)
    {
        string folder = "Assets/animations/" + s.name;
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/animations", s.name);
        var path = folder + "/" + s.name + ".controller";
        AssetDatabase.DeleteAsset(path);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        foreach (var p in new[] { "RunDown", "RunUp", "RunHorizontal" }) controller.AddParameter(p, AnimatorControllerParameterType.Bool);
        var machine = controller.layers[0].stateMachine;

        AnimatorState State(string clipName, string[] names, float fps)
        {
            var state = machine.AddState(clipName);
            state.motion = Clip(folder + "/" + s.name + clipName + ".anim", frames(names), fps);
            return state;
        }
        var idleFront = State("IdleFront", s.idleFront, s.idleFps);
        var idleBack = State("IdleBack", s.idleBack, s.idleFps);
        var idleSide = State("IdleSide", s.idleSide, s.idleFps);
        machine.defaultState = idleFront;
        Walk(machine, State("WalkFront", s.walkFront, s.walkFps), idleFront, "RunDown");
        Walk(machine, State("WalkBack", s.walkBack, s.walkFps), idleBack, "RunUp");
        Walk(machine, State("WalkSide", s.walkSide, s.walkFps), idleSide, "RunHorizontal");
        return controller;
    }

    // Any state starts walking while the bool is on; releasing it rests facing the same way.
    static void Walk(AnimatorStateMachine machine, AnimatorState walk, AnimatorState idle, string parameter)
    {
        var start = machine.AddAnyStateTransition(walk);
        start.AddCondition(AnimatorConditionMode.If, 0, parameter);
        start.hasExitTime = false; start.duration = 0; start.canTransitionToSelf = false;
        var stop = walk.AddTransition(idle);
        stop.AddCondition(AnimatorConditionMode.IfNot, 0, parameter);
        stop.hasExitTime = false; stop.duration = 0;
    }

    static AnimationClip Clip(string path, Sprite[] frames, float fps)
    {
        AssetDatabase.DeleteAsset(path);
        var clip = new AnimationClip { frameRate = fps };
        var keys = new ObjectReferenceKeyframe[frames.Length + 1];
        for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
        // Repeat the last frame so it lasts as long as the others before looping.
        keys[frames.Length] = new ObjectReferenceKeyframe { time = frames.Length / fps, value = frames[frames.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    // Union of the drawn pixels of every frame, in units relative to the sprite pivot.
    static Rect OpaqueBounds(IEnumerable<Sprite> frames)
    {
        var textures = new Dictionary<Texture2D, Texture2D>();
        float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
        foreach (var sprite in frames)
        {
            if (!textures.TryGetValue(sprite.texture, out var readable))
            {
                // Import settings keep sheets unreadable; decode the source file instead.
                readable = new Texture2D(2, 2);
                readable.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite.texture)));
                textures[sprite.texture] = readable;
            }
            var r = sprite.rect;
            for (int y = (int)r.y; y < (int)r.yMax; y++)
                for (int x = (int)r.x; x < (int)r.xMax; x++)
                {
                    if (readable.GetPixel(x, y).a < .1f) continue;
                    float ux = (x - r.x - sprite.pivot.x) / sprite.pixelsPerUnit, uy = (y - r.y - sprite.pivot.y) / sprite.pixelsPerUnit;
                    xMin = Mathf.Min(xMin, ux); xMax = Mathf.Max(xMax, ux + 1 / sprite.pixelsPerUnit);
                    yMin = Mathf.Min(yMin, uy); yMax = Mathf.Max(yMax, uy + 1 / sprite.pixelsPerUnit);
                }
        }
        foreach (var t in textures.Values) UnityEngine.Object.DestroyImmediate(t);
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    // Root = logic on the feet cell (bottom-left of the footprint). Render = art scaled into the footprint.
    static void BuildPrefab(Species s, AnimatorController controller, Sprite firstFrame, Rect drawn)
    {
        const string folder = "Assets/Prefabs/Pokemon";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Prefabs", "Pokemon");
        string path = folder + "/" + s.name + ".prefab";
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        var root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(s.name);
        try
        {
            root.transform.localScale = Vector3.one;
            var render = root.transform.Find("Render");
            if (render == null) render = new GameObject("Render").transform;
            render.SetParent(root.transform, false);

            float scale = Mathf.Min(s.footprint.x * Fill / drawn.width, s.footprint.y * Fill / drawn.height);
            render.localScale = new Vector3(scale, scale, 1);
            // Centre the drawing over the footprint and rest its lowest pixel on the bottom edge.
            render.localPosition = new Vector3((s.footprint.x - 1) * .5f - drawn.center.x * scale, -.5f + .02f - drawn.yMin * scale, 0);
            if (AssetDatabase.GetAssetPath(firstFrame).StartsWith("Assets/Sprites/Creatures/", StringComparison.Ordinal))
            {
                render.localScale = Vector3.one;
                render.localPosition = new Vector3((s.footprint.x - 1) * .5f, (firstFrame.rect.height / firstFrame.pixelsPerUnit - 1) * .5f, 0);
            }

            var sprite = Get<SpriteRenderer>(render.gameObject);
            sprite.sprite = firstFrame;
            sprite.sortingLayerName = "Terrain";
            Get<Animator>(render.gameObject).runtimeAnimatorController = controller;
            Get<NeoYSort>(render.gameObject).feet = root.transform;

            Get<Seeker>(root);
            var motor = Get<NeoGridMotor>(root);
            motor.visual = render; motor.speed = s.speed; motor.footprint = s.footprint;
            var wild = Get<NeoWildPokemon>(root);
            wild.wanderRadius = s.wanderRadius; wild.hoverAmplitude = s.hover;

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            if (exists) PrefabUtility.UnloadPrefabContents(root);
            else UnityEngine.Object.DestroyImmediate(root);
        }
    }
    // Unity's == null, not ??: in the Editor a missing component is a "fake null" object.
    static T Get<T>(GameObject go) where T : Component { var c = go.GetComponent<T>(); return c != null ? c : go.AddComponent<T>(); }
}
