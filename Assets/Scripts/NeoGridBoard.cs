using System;
using System.Collections.Generic;
using Pathfinding;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Authoritative cell occupancy. Physics and A* mirror this board.</summary>
public sealed class NeoGridBoard : MonoBehaviour
{
    public Tilemap floor;
    public Tilemap walls;
    public AstarPath pathfinder;
    public NeoPushRock[] sceneRocks = Array.Empty<NeoPushRock>();
    public NeoGridMotor[] actors = Array.Empty<NeoGridMotor>();
    public BoundsInt navigationBounds = new BoundsInt(0, 0, 0, 24, 16, 1);
    public BoundsInt TerrainBounds => floor != null ? floor.cellBounds : navigationBounds;
    public Transform navigationTarget;
    public float navigationUpdateDistance = 4f;
    ProceduralGridMover navigationMover;
    readonly Dictionary<Vector2Int, NeoPushRock> rocks = new Dictionary<Vector2Int, NeoPushRock>();
    readonly Dictionary<Vector2Int, GridNodeBase> nodes = new Dictionary<Vector2Int, GridNodeBase>();
    readonly Dictionary<Vector2Int, UnityEngine.Object> reservations = new Dictionary<Vector2Int, UnityEngine.Object>();
    public int Revision { get; private set; }
    public bool Ready { get; private set; }
    public event Action Changed;
    public GridGraph Graph { get; private set; }
    public static Vector2Int Cell(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));
    public static Vector3 Center(Vector2Int c) => new Vector3(c.x + .5f, c.y + .5f, 0);
    // Terrain is artwork, not a second hand-painted navigation mask.
    public bool StaticBlocked(Vector2Int c) => (floor != null ? !floor.HasTile((Vector3Int)c) : !navigationBounds.Contains((Vector3Int)c)) || (walls != null && walls.HasTile((Vector3Int)c));
    public bool Blocked(Vector2Int c)
    {
        if (StaticBlocked(c) || rocks.ContainsKey(c)) return true;
        if (!Application.isPlaying)
            foreach (var rock in sceneRocks) if (rock != null && Cell(rock.transform.position) == c) return true;
        return false;
    }
    public bool Reserved(Vector2Int c) => reservations.ContainsKey(c);
    public bool CanEnter(Vector2Int c, UnityEngine.Object owner) => !Blocked(c) && (!reservations.TryGetValue(c, out var held) || held == owner);
    public bool Reserve(Vector2Int c, UnityEngine.Object owner)
    {
        if (!CanEnter(c, owner)) return false;
        reservations[c] = owner;
        return true;
    }
    public void Release(UnityEngine.Object owner)
    {
        var cells = new List<Vector2Int>();
        foreach (var pair in reservations) if (pair.Value == owner) cells.Add(pair.Key);
        foreach (var c in cells) reservations.Remove(c);
    }
    public bool ReserveRock(NeoPushRock rock, Vector2Int from, Vector2Int to)
    {
        if (!CanEnter(to, rock)) return false;
        foreach (var actor in actors)
            if (actor != null && actor.gameObject.activeInHierarchy && (actor.Cell == to || (actor.IsMoving && actor.Destination == to))) return false;
        reservations[from] = rock;
        reservations[to] = rock;
        return true;
    }
    public void RegisterRock(NeoPushRock rock, Vector2Int cell)
    {
        rocks[cell] = rock;
    }
    public void RelocateRock(NeoPushRock rock, Vector2Int from, Vector2Int to)
    {
        if (rocks.TryGetValue(from, out var old) && old == rock) rocks.Remove(from);
        rocks[to] = rock;
        Release(rock);
        Revision++;
        Physics2D.SyncTransforms();
        if (Ready) UpdateNavigation(new[] { from, to });
        Changed?.Invoke();
    }
    public void InitializeNavigation()
    {
        if (Ready) return;
        Graph = pathfinder.data.gridGraph;
        if (Graph == null) Graph = (GridGraph)pathfinder.data.AddGraph(typeof(GridGraph));
        Graph.SetDimensions(navigationBounds.size.x, navigationBounds.size.y, 1);
        Graph.center = new Vector3(navigationBounds.center.x, navigationBounds.center.y, 0);
        if (navigationTarget != null)
            Graph.center = new Vector3(Mathf.Floor(navigationTarget.position.x) + (Graph.width % 2 == 0 ? 0 : .5f), Mathf.Floor(navigationTarget.position.y) + (Graph.depth % 2 == 0 ? 0 : .5f), 0);
        Graph.rotation = new Vector3(-90, 0, 0);
        Graph.neighbours = NumNeighbours.Four;
        Graph.cutCorners = false;
        Graph.erodeIterations = 0;
        Graph.collision.use2D = true;
        Graph.collision.heightCheck = false;
        Graph.collision.collisionCheck = false;
        pathfinder.Scan(Graph);
        RefreshNavigationNodes();
        navigationMover = pathfinder.GetComponent<ProceduralGridMover>();
        if (navigationMover == null) navigationMover = pathfinder.gameObject.AddComponent<ProceduralGridMover>();
        // The board owns when movement and terrain synchronization occur, in one frame.
        navigationMover.enabled = false;
        navigationMover.graph = Graph;
        Ready = true;
    }
    void RefreshNavigationNodes()
    {
        pathfinder.AddWorkItem(new AstarWorkItem(ctx => {
            nodes.Clear();
            Graph.GetNodes(node => {
                var cell = Cell((Vector3)node.position);
                nodes[cell] = (GridNodeBase)node;
                node.Walkable = !Blocked(cell);
            });
            Graph.GetNodes(node => Graph.CalculateConnections((GridNodeBase)node));
            ctx.QueueFloodFill();
        }));
        pathfinder.FlushWorkItems();
    }
    void Update()
    {
        if (!Ready || DemoBootstrap.IsPaused || navigationTarget == null) return;
        float threshold = Mathf.Max(1f, navigationUpdateDistance);
        if (Vector2.Distance(navigationTarget.position, Graph.center) <= threshold) return;
        navigationMover.target = navigationTarget;
        navigationMover.UpdateGraph();
        pathfinder.FlushWorkItems();
        // Rebuild the cell lookup after nodes rotate through the moving grid, and apply
        // the same floor/wall/rock rules used by direct movement to every node.
        RefreshNavigationNodes();
        Revision++;
        Changed?.Invoke();
    }
    void UpdateNavigation(Vector2Int[] changed)
    {
        pathfinder.AddWorkItem(new AstarWorkItem(ctx => {
            foreach (var cell in changed)
            {
                if (!nodes.TryGetValue(cell, out var node)) continue;
                node.Walkable = !Blocked(cell);
                int index = node.NodeInGridIndex;
                Graph.CalculateConnectionsForCellAndNeighbours(index % Graph.width, index / Graph.width);
            }
            ctx.QueueFloodFill();
        }));
        pathfinder.FlushWorkItems();
    }
}
