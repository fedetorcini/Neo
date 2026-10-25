using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Rebuilds the west bank of Bosque del Cauce (everything north-west of the river) in the open scene.
// The river, bridge, river colliders and every other part of the map stay untouched.
// Layout: bridge -> Sendero del Cauce -> stairs through the Peñón -> Claro de los Beedrill,
// a branch to the Mirador del río in front of the waterfall, and the Laguna del Bosque.
public static class NeoWestBankAuthoring
{
    const int Width = 64, Height = 48;
    static readonly Vector2Int Start = new Vector2Int(39, 4);

    static Tilemap terrain, water, detail, foreground, foregroundLv1, colliders;
    static TileBase grass, path, mountain, stairs, block, boulder, tallGrass, reeds, shore;
    static TileBase[] trunk, crown, flowers;
    static HashSet<Vector2Int> region, routes, open, ridge, stairCells, pond, trees, bank;

    static Vector3Int C(Vector2Int c) => new Vector3Int(c.x, c.y, 0);
    static bool Inside(Vector2Int c) => c.x >= 0 && c.x < Width && c.y >= 0 && c.y < Height;
    static T Load<T>(string path) where T : UnityEngine.Object =>
        AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Falta el asset " + path);
    static TileBase Palette(int id) => Load<TileBase>("Assets/Pallete/Pallete1_" + id + ".asset");
    // Deterministic noise, so the same recipe always draws the same forest.
    static float Noise(int x, int y) => Mathf.Abs(Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f) % 1f;
    static bool InOval(Vector2Int c, float cx, float cy, float rx, float ry) =>
        Mathf.Pow((c.x - cx) / rx, 2) + Mathf.Pow((c.y - cy) / ry, 2) <= 1;
    static IEnumerable<Vector2Int> Oval(float cx, float cy, float rx, float ry)
    {
        for (int y = Mathf.FloorToInt(cy - ry); y <= Mathf.CeilToInt(cy + ry); y++)
            for (int x = Mathf.FloorToInt(cx - rx); x <= Mathf.CeilToInt(cx + rx); x++)
            {
                var c = new Vector2Int(x, y);
                if (region.Contains(c) && InOval(c, cx, cy, rx, ry)) yield return c;
            }
    }

    [MenuItem("Neo/Autoría/Rehacer orilla oeste del bosque")]
    public static string Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Salí de Play antes de rehacer el mapa.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "BosqueDelCauce") throw new InvalidOperationException("Abrí Assets/Scenes/BosqueDelCauce.unity.");
        // One copy of the scene as it was before the first rebuild of the day; reruns keep it.
        string backup = "Assets/Scenes/Reference/AntesDeOrillaOeste_" + DateTime.Now.ToString("yyyyMMdd") + ".unity";
        if (!File.Exists(backup) && !EditorSceneManager.SaveScene(scene, backup, true)) throw new InvalidOperationException("No se pudo guardar la copia " + backup);

        LoadMapsAndTiles();
        Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[] { terrain, water, detail, foreground, foregroundLv1, colliders }, "Rehacer orilla oeste");
        FindRegion();
        Clear();
        Layout();
        Paint();
        SealRiver();
        PlaceActors();
        foreach (var map in new[] { terrain, water, detail, foreground, foregroundLv1, colliders }) map.RefreshAllTiles();
        colliders.GetComponent<TilemapCollider2D>()?.ProcessTilemapChanges();
        EditorSceneManager.MarkSceneDirty(scene);
        return Validate() + " Copia previa: " + backup;
    }

    static void LoadMapsAndTiles()
    {
        Tilemap Map(string name)
        {
            var go = GameObject.Find("Grid/" + name);
            return go != null ? go.GetComponent<Tilemap>() : throw new InvalidOperationException("Falta Grid/" + name);
        }
        terrain = Map("Terrain"); water = Map("Water"); detail = Map("Terrain LV1");
        foreground = Map("Foreground"); foregroundLv1 = Map("Foreground LV1"); colliders = Map("Colliders");
        grass = Palette(1); boulder = Palette(189);
        // Carved into the ridge: see NeoMountainStairsAuthoring.
        stairs = Load<TileBase>("Assets/Sprites/AniamtedTiles/EscaleraMontana.asset");
        path = Load<TileBase>("Assets/Sprites/AniamtedTiles/PathTile.asset");
        mountain = Load<TileBase>("Assets/Sprites/AniamtedTiles/Mountain.asset");
        block = Load<TileBase>("Assets/Demo/BoundaryCell.asset");
        tallGrass = Load<TileBase>("Assets/ForestMock/HierbaDecorativaFollaje.asset");
        reeds = Load<TileBase>("Assets/Sprites/AniamtedTiles/AnimatedRiverReeds.asset");
        shore = Load<TileBase>("Assets/Sprites/Shore/LagoPasto.asset");
        flowers = new[] { "AnimatedFlowers", "AnimatedPurpleBellflowers", "AnimatedYellowDaisies", "AnimatedBlueWildflowers" }
            .Select(n => Load<TileBase>("Assets/Sprites/AniamtedTiles/" + n + ".asset")).ToArray();
        trunk = new[] { Palette(486), Palette(487) };
        crown = new[] { 2, 3, 0, 1 }.Select(i => Load<TileBase>("Assets/Pallete/treeForeground_" + i + ".asset")).ToArray();
    }

    // The west bank is every dry cell reachable from the north-west corner without crossing the river.
    static void FindRegion()
    {
        region = new HashSet<Vector2Int>();
        var queue = new Queue<Vector2Int>();
        var seed = new Vector2Int(1, Height - 2);
        region.Add(seed); queue.Enqueue(seed);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            {
                var n = c + d;
                if (Inside(n) && !IsRiver(n) && region.Add(n)) queue.Enqueue(n);
            }
        }
        if (region.Count > Width * Height / 2) throw new InvalidOperationException("El río no separa el mapa: la orilla oeste llega a " + region.Count + " casillas.");
        bank = new HashSet<Vector2Int>(region.Where(c =>
            Enumerable.Range(-1, 3).Any(dx => Enumerable.Range(-1, 3).Any(dy => IsRiver(c + new Vector2Int(dx, dy))))));
    }
    // Only the river splits the map; a pond painted by an earlier run is part of the bank.
    static bool IsRiver(Vector2Int c) { var t = water.GetTile(C(c)); return t != null && !(t is NeoShoreTile); }

    static void Clear()
    {
        // Trees just outside the region may hang their crown over it; their trunks decide.
        foreach (var c in region)
        {
            var p = C(c);
            terrain.SetTile(p, grass); detail.SetTile(p, null); water.SetTile(p, null);
            foreground.SetTile(p, null); foregroundLv1.SetTile(p, null); colliders.SetTile(p, null);
        }
    }

    static void Trail(Vector2Int a, Vector2Int b, int width = 2)
    {
        int dx = Math.Sign(b.x - a.x), dy = Math.Sign(b.y - a.y);
        if (dx != 0 && dy != 0) throw new InvalidOperationException("Los tramos de sendero son cardinales.");
        for (int i = 0; i <= Math.Abs(b.x - a.x) + Math.Abs(b.y - a.y); i++)
            for (int w = 0; w < width; w++)
            {
                var c = new Vector2Int(a.x + dx * i + (dy != 0 ? w : 0), a.y + dy * i + (dx != 0 ? w : 0));
                if (region.Contains(c)) routes.Add(c);
            }
    }

    static void Layout()
    {
        routes = new HashSet<Vector2Int>(); open = new HashSet<Vector2Int>();
        ridge = new HashSet<Vector2Int>(); stairCells = new HashSet<Vector2Int>();
        pond = new HashSet<Vector2Int>(); trees = new HashSet<Vector2Int>();

        // Sendero del Cauce: bridge landing, west, then north through the Peñón.
        Trail(new Vector2Int(26, 23), new Vector2Int(17, 23));
        Trail(new Vector2Int(17, 23), new Vector2Int(17, 35));
        // Branch to the Laguna del Bosque.
        Trail(new Vector2Int(17, 26), new Vector2Int(10, 26));
        // Branch from the clearing to the Mirador del río.
        Trail(new Vector2Int(20, 41), new Vector2Int(28, 41));
        Trail(new Vector2Int(28, 41), new Vector2Int(28, 43));

        // Peñón: a rock ridge from the west edge to the river, crossed only by the stairs.
        ridge.UnionWith(region.Where(c => c.y >= 30 && c.y <= 32));
        ridge.UnionWith(Oval(6, 31, 3.5f, 2.6f).Concat(Oval(24, 31.5f, 2.5f, 2.2f)));

        // Claro de los Beedrill, Mirador and the pond clearing stay free of trees.
        open.UnionWith(Oval(12.5f, 38, 9.5f, 5.8f));
        open.UnionWith(Oval(30.5f, 43.5f, 4f, 3f));
        open.UnionWith(Oval(9, 26.5f, 4.5f, 2.2f));
        pond.UnionWith(Oval(8, 20.5f, 5.2f, 3.4f));
        open.UnionWith(Oval(8, 20.5f, 6.2f, 4.4f));
        open.UnionWith(region.Where(c => c.x >= 21 && c.x <= 27 && c.y >= 21 && c.y <= 26)); // Bridge landing.
        // The trail climbs the ridge on stairs.
        foreach (var c in routes) if (ridge.Remove(c)) stairCells.Add(c);
    }

    static bool CanTree(Vector2Int c)
    {
        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 3; y++)
            {
                var p = c + new Vector2Int(x, y);
                if (!region.Contains(p) || open.Contains(p) || bank.Contains(p) || ridge.Contains(p) || pond.Contains(p) || trees.Contains(p) || routes.Contains(p)) return false;
                // Trunks keep one free cell beside trails; crowns may lean over them.
                if (y == 0) for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) if (routes.Contains(p + new Vector2Int(dx, dy))) return false;
            }
        return true;
    }

    static void Tree(Vector2Int c)
    {
        for (int x = 0; x < 2; x++) { var p = c + new Vector2Int(x, 0); terrain.SetTile(C(p), trunk[x]); trees.Add(p); }
        for (int y = 0; y < 2; y++)
            for (int x = 0; x < 2; x++) foreground.SetTile(C(c + new Vector2Int(x, y + 1)), crown[y * 2 + x]);
    }

    static void Paint()
    {
        foreach (var c in routes) terrain.SetTile(C(c), stairCells.Contains(c) ? stairs : path);
        foreach (var c in ridge) terrain.SetTile(C(c), mountain);
        // Sandy heart of the clearing and the mirador.
        foreach (var c in Oval(12.5f, 38, 4.2f, 2.6f).Concat(Oval(30.5f, 43.5f, 2.2f, 1.5f))) { terrain.SetTile(C(c), path); routes.Add(c); }
        foreach (var c in pond) water.SetTile(C(c), shore);

        // Forest: two interleaved passes so crowns overlap into masses; dense along the map
        // edges, with a few gaps inside; never on clearings, trails, rock or the bank.
        for (int pass = 0; pass < 2; pass++)
            for (int y = pass; y < Height - 2; y += 2)
                for (int x = (y / 2 + pass) % 2; x < Width - 1; x += 2)
                {
                    var c = new Vector2Int(x, y);
                    bool edge = c.x < 5 || c.y > 42;
                    if ((edge || Noise(x + 31, y) > .2f) && CanTree(c)) Tree(c);
                }

        // Tall grass ring around the clearing, flowers scattered through it.
        foreach (var c in Oval(12.5f, 38, 8.6f, 5.2f))
        {
            if (routes.Contains(c) || trees.Contains(c) || ridge.Contains(c)) continue;
            bool ring = !InOval(c, 12.5f, 38, 5.6f, 3.6f);
            if (ring && Noise(c.x, c.y) > .18f) detail.SetTile(C(c), tallGrass);
            else if (Noise(c.x + 5, c.y) > .8f) detail.SetTile(C(c), flowers[0]);
        }
        void Patch(float cx, float cy, float r, TileBase tile)
        {
            foreach (var c in Oval(cx, cy, r, r))
                if (!routes.Contains(c) && !trees.Contains(c) && !ridge.Contains(c) && !pond.Contains(c) && detail.GetTile(C(c)) == null && Noise(c.x * 3, c.y) > .55f)
                    detail.SetTile(C(c), tile);
        }
        Patch(31, 44.5f, 2.5f, flowers[1]); Patch(33, 42, 1.5f, flowers[2]);
        Patch(13.5f, 27, 2.5f, flowers[3]); Patch(4.5f, 25, 2, flowers[2]);
        Patch(22, 27, 2, flowers[0]); Patch(20, 36, 1.5f, flowers[1]); Patch(5, 41, 1.8f, flowers[1]);
        // Reeds on the river bank and around the pond, leaving the trails clear.
        foreach (var c in region)
        {
            if (routes.Contains(c) || trees.Contains(c) || ridge.Contains(c) || pond.Contains(c) || (open.Contains(c) && !NextToPond(c))) continue;
            if ((bank.Contains(c) || NextToPond(c)) && Noise(c.x, c.y * 2) > .55f) detail.SetTile(C(c), reeds);
        }
        // A couple of boulders at the mirador and in the woods.
        foreach (var c in new[] { new Vector2Int(33, 45), new Vector2Int(27, 45), new Vector2Int(11, 15), new Vector2Int(3, 34) })
            if (region.Contains(c) && !routes.Contains(c) && !trees.Contains(c) && !ridge.Contains(c)) { detail.SetTile(C(c), boulder); trees.Add(c); }

        // Collisions: trunks, boulders, rock (not its stairs), the pond beyond its walkable shore, and the map edge.
        foreach (var c in trees.Concat(ridge)) colliders.SetTile(C(c), block);
        var shoreTile = (NeoShoreTile)shore;
        foreach (var c in pond) colliders.SetTile(C(c), shoreTile.Walkable(water, C(c)) ? null : block);
        foreach (var c in region) if (c.x == 0 || c.y == Height - 1) colliders.SetTile(C(c), block);
    }
    // The river's whole contour blocks, on both banks, except under the bridge. Only the contour:
    // the inside stays empty, as in the original forest recipe.
    static void SealRiver()
    {
        foreach (var p in water.cellBounds.allPositionsWithin)
        {
            var c = new Vector2Int(p.x, p.y);
            if (!IsRiver(c)) continue;
            var deck = detail.GetTile(p);
            if (deck != null && deck.name.Contains("bridge")) continue;
            if (new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right }.Any(d => Inside(c + d) && !IsRiver(c + d)))
                colliders.SetTile(p, block);
        }
    }
    static bool NextToPond(Vector2Int c) => !pond.Contains(c) && pond.Any(p => Mathf.Abs(p.x - c.x) <= 1 && Mathf.Abs(p.y - c.y) <= 1);

    static void PlaceActors()
    {
        var group = GameObject.Find("Personajes y situaciones — referencia visual")?.transform;
        if (group == null) return;
        var homes = new Dictionary<string, Vector2Int>
        {
            { "Beedrill — claro oeste A", new Vector2Int(9, 39) },
            { "Beedrill — claro oeste B", new Vector2Int(15, 40) },
            { "Beedrill — claro oeste C", new Vector2Int(13, 36) },
        };
        foreach (Transform t in group)
            if (homes.TryGetValue(t.name, out var cell)) { Undo.RecordObject(t, "Rehacer orilla oeste"); t.position = new Vector3(cell.x + .5f, cell.y + .5f, 0); }
        // The western Poké Ball hides in the tall grass at the clearing's north-west edge.
        var ball = group.Cast<Transform>().Where(t => t.name.StartsWith("Poké Ball")).OrderBy(t => t.position.x).FirstOrDefault();
        if (ball != null && ball.position.x < 32) { Undo.RecordObject(ball, "Rehacer orilla oeste"); ball.position = new Vector3(6.5f, 41.5f, 0); }
    }

    // Every place must be reachable on foot from the entrance, with the board's own blocking rule.
    public static string Validate()
    {
        HashSet<Vector2Int> Reach(Func<Vector2Int, bool> extraBlock)
        {
            bool Blocked(Vector2Int c) => !Inside(c) || !terrain.HasTile(C(c)) || colliders.HasTile(C(c)) || extraBlock(c);
            var reached = new HashSet<Vector2Int> { Start };
            var queue = new Queue<Vector2Int>(reached);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                    if (!Blocked(c + d) && reached.Add(c + d)) queue.Enqueue(c + d);
            }
            return reached;
        }
        var seen = Reach(c => false);
        // With the stairs closed the clearing must be out of reach: the Peñón has no way around.
        bool sealedRidge = !Reach(c => stairCells.Contains(c)).Contains(new Vector2Int(12, 38));
        var places = new Dictionary<string, Vector2Int>
        {
            { "puente oeste", new Vector2Int(24, 23) }, { "escalera del Peñón", new Vector2Int(17, 33) },
            { "Claro de los Beedrill", new Vector2Int(12, 38) }, { "Mirador del río", new Vector2Int(30, 43) },
            { "orilla de la Laguna", new Vector2Int(9, 26) }, { "Poké Ball", new Vector2Int(6, 41) },
            { "Beedrill A", new Vector2Int(9, 39) }, { "Beedrill B", new Vector2Int(15, 40) }, { "Beedrill C", new Vector2Int(13, 36) },
        };
        var missing = places.Where(p => !seen.Contains(p.Value)).Select(p => p.Key + " " + p.Value).ToList();
        string report = $"Orilla oeste: {region.Count} casillas, {trees.Count(t => terrain.GetTile(C(t)) == trunk[0])} árboles, "
            + $"{ridge.Count} de roca, {pond.Count} de laguna. Lugares alcanzables: {places.Count - missing.Count}/{places.Count}"
            + (missing.Count > 0 ? ". SIN ACCESO: " + string.Join(", ", missing) : ".")
            + (sealedRidge ? " El Peñón solo se cruza por la escalera." : " AVISO: el Peñón se puede rodear sin la escalera.");
        Directory.CreateDirectory("docs/design");
        File.WriteAllText("docs/design/orilla-oeste-validacion.txt", report);
        return report;
    }
}
