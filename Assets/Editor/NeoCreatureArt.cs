using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;

// Original art replacements. Legacy species identifiers remain compatible with game logic.
public static class NeoCreatureArt
{
    public sealed class Design
    {
        public string legacy, name;
        public int width, height;
        public Design(string legacy, string name, int width, int height)
        { this.legacy = legacy; this.name = name; this.width = width; this.height = height; }
        public string Path => "Assets/Sprites/Creatures/" + name + ".png";
    }

    public static readonly Design[] Designs = {
        new Design("Pikachu", "Lumin", 16, 16),
        new Design("Beedrill", "Brispa", 16, 16),
        new Design("Machop", "Pedrugo", 16, 16),
        new Design("Psyduck", "Junquin", 16, 16),
        new Design("Dratini", "Caucel", 32, 16),
        new Design("Haunter", "Farolin", 32, 32),
        new Design("Machoke", "Granugo", 32, 32)
    };

    static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();
    static string backupRoot;
    static readonly List<string> changed = new List<string>();

    static void Backup(string path)
    {
        if (backupRoot == null) return;
        var destination = System.IO.Path.Combine(backupRoot, path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination));
        if (File.Exists(path) && !File.Exists(destination)) File.Copy(path, destination);
        if (File.Exists(path + ".meta") && !File.Exists(destination + ".meta")) File.Copy(path + ".meta", destination + ".meta");
        if (!changed.Contains(path)) changed.Add(path);
    }

    public static void Import()
    {
        cache.Clear();
        foreach (var d in Designs)
        {
            AssetDatabase.ImportAsset(d.Path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(d.Path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            var factories = new SpriteDataProviderFactories(); factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var old = provider.GetSpriteRects().ToDictionary(r => r.name);
            var rects = Enumerable.Range(0, 12).Select(i => {
                string name = d.name + "_" + i;
                return new SpriteRect { name = name,
                    rect = new Rect(i % 4 * d.width, (2 - i / 4) * d.height, d.width, d.height),
                    pivot = new Vector2(.5f, .5f), alignment = SpriteAlignment.Center,
                    spriteID = old.TryGetValue(name, out var r) ? r.spriteID : UnityEngine.GUID.Generate() };
            }).ToArray();
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply(); importer.SaveAndReimport();
        }
    }

    public static Sprite Remap(Sprite original)
    {
        if (original == null) return null;
        if (AssetDatabase.GetAssetPath(original).StartsWith("Assets/Sprites/Creatures/", StringComparison.Ordinal)) return original;
        int split = original.name.LastIndexOf('_');
        if (split < 0 || !int.TryParse(original.name.Substring(split + 1), out int index)) return original;
        string prefix = original.name.Substring(0, split), legacy;
        int row = 0, col = 0;
        switch (prefix)
        {
            case "PikachuIdle": legacy = "Pikachu"; row = new[] { 0, 2, 1 }[index % 3]; break;
            case "PikachuWalk": legacy = "Pikachu"; row = new[] { 0, 2, 1 }[(index / 2) % 3]; col = 1 + index % 2; break;
            case "PikachuAttack": legacy = "Pikachu"; row = Math.Min(2, index / 4); col = 3; break;
            case "Bedrill": legacy = "Beedrill"; row = Math.Min(2, index / 14); col = index % 14 >= 6 ? 3 : new[] { 0, 1, 2, 1, 0, 2 }[index % 14]; break;
            case "Psyduck": legacy = "Psyduck"; row = Math.Min(2, index / 7); col = Math.Min(3, index % 7); break;
            case "Dratini":
                legacy = "Dratini"; row = Math.Min(2, index / 7);
                int local = index % 7;
                col = row == 2 ? (local == 0 ? 0 : local == 4 ? 1 : local == 6 ? 2 : 3) : new[] { 0, 1, 2, 1, 0, 3, 3 }[local]; break;
            case "Hunter": legacy = "Haunter"; row = Math.Min(2, index / 12); col = index % 12 < 2 ? index % 12 : index % 12 < 5 ? 1 + index % 2 : 3; break;
            case "Machoke": legacy = "Machoke"; row = Math.Min(2, index / 8); col = Math.Min(3, index % 8); break;
            case "Machop":
                legacy = "Machop";
                row = new[] { 0, 2, 1 }[index < 9 ? index / 3 : Math.Min(2, index - 9)];
                col = index < 9 ? index % 3 : 0;
                break;
            case "MachopAttack": legacy = "Machop"; row = Math.Min(2, index / 4); col = index % 4 == 0 ? 0 : 3; break;
            default: return original;
        }
        var design = Designs.Single(d => d.legacy == legacy);
        if (!cache.TryGetValue(design.name, out var frames))
        {
            frames = AssetDatabase.LoadAllAssetsAtPath(design.Path).OfType<Sprite>().OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
            if (frames.Length != 12) throw new InvalidOperationException("Expected 12 creature frames: " + design.name);
            cache[design.name] = frames;
        }
        return frames[row * 4 + col];
    }

    static void Align(SpriteRenderer render)
    {
        var motor = render.GetComponentInParent<NeoGridMotor>();
        if (motor == null || motor.visual != render.transform) return;
        var footprint = motor.footprint;
        // Native pixels stay on the 16 PPU world grid instead of being resampled to 95% fill.
        render.transform.localScale = new Vector3(render.transform.localScale.x < 0 ? -1 : 1, 1, 1);
        render.transform.localPosition = new Vector3((footprint.x - 1) * .5f, (footprint.y - 1) * .5f, 0);
    }

    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Apply creature art in edit mode.");
        backupRoot = "Temp/CreatureArtBackup/" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        changed.Clear(); cache.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                bool edit = false;
                foreach (var key in keys)
                    if (key.value is Sprite s && Remap(s) != s) { edit = true; break; }
                if (!edit) continue;
                Backup(path);
                for (int i = 0; i < keys.Length; i++) if (keys[i].value is Sprite s) keys[i].value = Remap(s);
                AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
                EditorUtility.SetDirty(clip); AssetDatabase.SaveAssetIfDirty(clip);
            }
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool edit = false;
                foreach (var render in root.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    var next = Remap(render.sprite);
                    if (next == render.sprite) continue;
                    if (!edit) Backup(path);
                    render.sprite = next; Align(render); edit = true;
                }
                if (edit) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        var scene = SceneManager.GetActiveScene();
        bool sceneEdited = false;
        if (!string.IsNullOrEmpty(scene.path))
        {
            Directory.CreateDirectory(backupRoot);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, backupRoot + "/ActiveScene.unity", true);
            foreach (var render in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SpriteRenderer>(true)))
            {
                var next = Remap(render.sprite);
                if (next == render.sprite && (next == null || !AssetDatabase.GetAssetPath(next).StartsWith("Assets/Sprites/Creatures/", StringComparison.Ordinal))) continue;
                Undo.RecordObjects(new UnityEngine.Object[] { render, render.transform }, "Aplicar criaturas originales");
                render.sprite = next; Align(render); PrefabUtility.RecordPrefabInstancePropertyModifications(render);
                PrefabUtility.RecordPrefabInstancePropertyModifications(render.transform); sceneEdited = true;
            }
            if (sceneEdited)
            {
                Backup(scene.path);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            }
        }
        File.WriteAllText(backupRoot + "/changed-files.txt", string.Join("\n", changed));
        Debug.Log("Arte original aplicado: " + changed.Count + " archivos. Copia: " + backupRoot);
    }
}
