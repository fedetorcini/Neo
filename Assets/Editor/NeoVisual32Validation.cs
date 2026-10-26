using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>Checks the isolated reference without changing the production scenes.</summary>
public static class NeoVisual32Validation
{
    [Serializable] public class Report
    {
        public string utc, scene;
        public bool passed;
        public List<string> checks = new List<string>();
        public List<string> failures = new List<string>();
    }
    static Report runtime;
    static NeoGridBoard board;
    static Controller control;
    static NeoGridMotor player, partner;
    static NeoPushRock rock;
    static int phase;
    static double deadline;

    static void Require(Report report, bool condition, string message)
    {
        (condition ? report.checks : report.failures).Add(message);
    }
    static string Save(Report report, string name)
    {
        report.passed = report.failures.Count == 0;
        report.utc = DateTime.UtcNow.ToString("o");
        Directory.CreateDirectory("docs/validation");
        string json = JsonUtility.ToJson(report, true);
        File.WriteAllText("docs/validation/" + name + ".json", json);
        if (report.passed) Debug.Log(name + ": PASS"); else Debug.LogError(json);
        return json;
    }

    [MenuItem("Neo/Visual 32/Validar recursos de referencia")]
    public static string AssetsCheck()
    {
        var report = new Report { scene = NeoVisual32Reference.ScenePath };
        var expected = new Dictionary<string, Vector3Int> {
            { "Lumin", new Vector3Int(32,32,30) }, { "Brispa", new Vector3Int(32,32,30) },
            { "Pedrugo", new Vector3Int(32,32,30) }, { "Granugo", new Vector3Int(64,64,30) },
            { "Explorer", new Vector3Int(32,48,30) }, { "Terrain", new Vector3Int(32,32,8) },
            { "Tree", new Vector3Int(32,32,6) }, { "BlueFlowers", new Vector3Int(32,32,5) },
            { "PushRock", new Vector3Int(32,32,1) }
        };
        foreach (var pair in expected)
        {
            string path = NeoVisual32Reference.Root + "/Art/" + pair.Key + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Require(report, importer != null && importer.spritePixelsPerUnit == NeoVisualStandard.PixelsPerUnit &&
                importer.filterMode == FilterMode.Point && !importer.mipmapEnabled &&
                importer.textureCompression == TextureImporterCompression.Uncompressed, pair.Key + ": point, 32 PPU, no compression or mipmaps");
            var frames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            Require(report, frames.Length == pair.Value.z && frames.All(s => s.rect.size == new Vector2(pair.Value.x, pair.Value.y)),
                pair.Key + ": expected native frame size and count");
        }
        int poses = 0;
        var clips = AssetDatabase.FindAssets("t:AnimationClip", new[] { NeoVisual32Reference.Root })
            .Select(id => AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(id))).ToArray();
        Require(report, clips.Length == 45, "45 directional idle, walk and action clips");
        foreach (var clip in clips)
        {
            var root = new GameObject("Reference pose check", typeof(Animator), typeof(SpriteRenderer));
            root.hideFlags = HideFlags.HideAndDontSave; root.transform.position = new Vector3(-9999, -9999, 0);
            var graph = PlayableGraph.Create("Reference pose check");
            try
            {
                var renderer = root.GetComponent<SpriteRenderer>();
                var animator = root.GetComponent<Animator>(); animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var output = AnimationPlayableOutput.Create(graph, "Pose", animator);
                var playable = AnimationClipPlayable.Create(graph, clip); output.SetSourcePlayable(playable);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual); graph.Play();
                var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                int expectedFrames = clip.name.StartsWith("Idle", StringComparison.Ordinal) ? 2 : 4;
                bool valid = keys.Select(k => k.value).Distinct().Count() == expectedFrames;
                foreach (var key in keys)
                {
                    playable.SetTime(key.time + .00001); graph.Evaluate(0);
                    valid &= key.value != null && renderer.sprite == key.value; poses++;
                }
                Require(report, valid, clip.name + " / " + AssetDatabase.GetAssetPath(clip).Split('/').Reverse().Skip(1).First() + ": all poses bind correctly");
            }
            finally { graph.Destroy(); UnityEngine.Object.DestroyImmediate(root); }
        }
        Require(report, poses == 195, "195 animation keyframes sampled through the Animator");
        return Save(report, "visual32-assets");
    }

    [MenuItem("Neo/Visual 32/Validar referencia en Play (sale de Play al terminar)")]
    public static string BeginRuntimeCheck()
    {
        if (!EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != NeoVisual32Reference.ScenePath)
            throw new InvalidOperationException("Open Visual32Reference and enter Play first.");
        if (runtime != null) throw new InvalidOperationException("Validation is already running.");
        runtime = new Report { scene = NeoVisual32Reference.ScenePath };
        board = UnityEngine.Object.FindAnyObjectByType<NeoGridBoard>();
        control = UnityEngine.Object.FindAnyObjectByType<Controller>();
        player = board.actors.First(a => a.GetComponent<PlayerAI>() != null);
        partner = board.actors.First(a => a.GetComponent<PartnerAI>() != null);
        rock = board.sceneRocks.Single();
        control.enabled = false;
        foreach (var actor in board.actors)
        {
            var wild = actor.GetComponent<NeoWildPokemon>(); if (wild != null) wild.enabled = false;
            var ai = actor.GetComponent<PartnerAI>(); if (ai != null) ai.enabled = false;
            actor.StopFollowing(); actor.Teleport(actor.transform.position);
        }
        player.Teleport(NeoGridBoard.Center(new Vector2Int(9,4)));
        partner.Teleport(NeoGridBoard.Center(new Vector2Int(11,4))); rock.ResetRock();
        Require(runtime, board.Ready && !DemoBootstrap.IsPaused, "Board and navigation initialize after reopening the saved scene");
        Require(runtime, board.floor.GetTilesBlock(board.floor.cellBounds).Count(t => t != null) == 180, "All 180 floor cells persist");
        Require(runtime, board.Blocked(new Vector2Int(2,2)) && board.Blocked(new Vector2Int(8,7)) && !board.Blocked(new Vector2Int(9,7)), "River and cliff block; stairs remain walkable");
        var camera = control.mainCamera.GetComponent<Camera>();
        var pixels = camera.GetComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>();
        Require(runtime, pixels != null && pixels.assetsPPU == 32 && pixels.refResolutionX == 640 && pixels.refResolutionY == 360 &&
            pixels.cropFrame == UnityEngine.Rendering.Universal.PixelPerfectCamera.CropFrame.Windowbox &&
            pixels.gridSnapping == UnityEngine.Rendering.Universal.PixelPerfectCamera.GridSnapping.UpscaleRenderTexture,
            "URP camera: 640x360, 32 PPU, integer upscale and windowbox");
        Require(runtime, board.actors.All(a => Mathf.Abs(a.visual.localScale.x) == 1 && a.visual.localScale.y == 1), "No inherited visual stretch");
        Require(runtime, player.Step(Vector2.up), "Human accepts a cardinal step");
        phase = 0; deadline = EditorApplication.timeSinceStartup + 8;
        EditorApplication.update += Tick;
        return "Runtime validation started; report: docs/validation/visual32-runtime.json";
    }
    static void Tick()
    {
        if (runtime == null) return;
        try
        {
            if (!EditorApplication.isPlaying) { Finish("Play stopped before validation finished"); return; }
            if (EditorApplication.timeSinceStartup > deadline) { Finish("Runtime phase " + phase + " timed out"); return; }
            if (phase == 0 && !player.IsMoving)
            {
                Require(runtime, player.Cell == new Vector2Int(9,5) && player.transform.position == NeoGridBoard.Center(player.Cell), "Human arrives exactly one world unit away");
                typeof(Controller).GetMethod("ToggleControl", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(control, null);
                Require(runtime, control.Active == partner.GetComponent<PartnerAI>() && partner.GetComponent<PartnerAI>().IsDirectlyControlled && control.mainCamera.target == partner.transform,
                    "Space control path selects Pedrugo and switches camera target");
                Require(runtime, partner.Step(Vector2.right), "Partner accepts a cardinal step"); phase = 1;
            }
            else if (phase == 1 && !partner.IsMoving)
            {
                Require(runtime, partner.Cell == new Vector2Int(12,4), "Partner arrives at the next cell");
                partner.Teleport(NeoGridBoard.Center(new Vector2Int(11,4)));
                rock.Interact(partner.GetComponent<Interactable>());
                Require(runtime, rock.IsMoving, "Existing push interaction starts"); phase = 2;
            }
            else if (phase == 2 && !rock.IsMoving)
            {
                Require(runtime, NeoGridBoard.Cell(rock.transform.position) == new Vector2Int(11,6) && !board.Blocked(new Vector2Int(11,5)) && board.Blocked(new Vector2Int(11,6)), "Push updates rock position and cell occupancy");
                bool oldOpen = false, newBlocked = false;
                board.Graph.GetNodes(node => { var cell = NeoGridBoard.Cell((Vector3)node.position); if (cell == new Vector2Int(11,5)) oldOpen = node.Walkable; if (cell == new Vector2Int(11,6)) newBlocked = !node.Walkable; });
                Require(runtime, oldOpen && newBlocked, "A* follows changed rock occupancy");
                Require(runtime, partner.GetComponent<DemoPartnerCombat>().Attack(Vector2.left), "Existing combat accepts an attack");
                phase = 3;
            }
            else if (phase == 3 && partner.visual.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("ActionSide"))
            {
                var sprite = partner.visual.GetComponent<SpriteRenderer>().sprite;
                Require(runtime, sprite != null && sprite.name.StartsWith("Pedrugo_", StringComparison.Ordinal) && int.Parse(sprite.name.Substring(8)) >= 26,
                    "Combat displays the new directional attack frames");
                phase = 4;
            }
            else if (phase == 4 && !partner.visual.GetComponent<Animator>().GetBool("ActionPlaying"))
            {
                Require(runtime, partner.Step(Vector2.down), "Movement resumes after the attack"); phase = 5;
            }
            else if (phase == 5 && !partner.IsMoving)
            {
                Require(runtime, partner.Cell == new Vector2Int(11,3), "Post-attack movement still covers one cell");
                control.ReturnControlToHuman();
                Require(runtime, control.Active == player.GetComponent<PlayerAI>() && control.mainCamera.target == player.transform, "Control and camera return to the human");
                Finish(null);
            }
        }
        catch (Exception e) { Finish(e.ToString()); }
    }
    static void Finish(string failure)
    {
        EditorApplication.update -= Tick;
        if (failure != null) runtime.failures.Add(failure);
        Save(runtime, "visual32-runtime"); runtime = null;
        // Exiting Play restores every temporary AI/input setting and actor position.
        EditorApplication.isPlaying = false;
    }
}
