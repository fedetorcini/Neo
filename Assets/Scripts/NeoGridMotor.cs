using System;
using System.Collections.Generic;
using Pathfinding;
using UnityEngine;

/// <summary>One motor for cardinal manual steps and A* follow/return.</summary>
[DefaultExecutionOrder(100)] // Read this frame's control input before advancing the step.
public sealed class NeoGridMotor : MonoBehaviour
{
    public NeoGridBoard board;
    public Transform visual;
    public float speed = 5;
    [Tooltip("Casillas que ocupa (ancho x alto), desde la casilla de los pies hacia la derecha y hacia arriba.")]
    public Vector2Int footprint = Vector2Int.one;
    public Vector2Int Cell => NeoGridBoard.Cell(transform.position);
    public IEnumerable<Vector2Int> CellsAt(Vector2Int anchor)
    {
        for (int x = 0; x < Mathf.Max(1, footprint.x); x++)
            for (int y = 0; y < Mathf.Max(1, footprint.y); y++)
                yield return anchor + new Vector2Int(x, y);
    }
    public Vector2Int Destination { get; private set; }
    public bool IsMoving { get; private set; }
    public int PathRequests { get; private set; }
    public IReadOnlyList<Vector3> Route => route;
    public Vector2Int TargetCell => target;
    public event Action Arrived;
    Seeker seeker;
    Animator animator;
    readonly List<Vector3> route = new List<Vector3>();
    Vector2Int target;
    float stopDistance;
    float nextRequest;
    int generation;
    bool follow;
    bool waiting;
    Vector3 stepDirection;
    float blockedSince = -1;
    public bool ReturnBlocked => follow && blockedSince >= 0 && Time.time - blockedSince > 2;

    void Awake()
    {
        // A reusable actor prefab cannot serialize a reference to the scene's board.
        if (board == null) board = FindAnyObjectByType<NeoGridBoard>();
        seeker = GetComponent<Seeker>();
        if (visual != null) animator = visual.GetComponent<Animator>();
    }
    void OnEnable() { if (board != null) board.Changed += MapChanged; }
    void OnDisable() { if (board != null) { board.Changed -= MapChanged; board.Release(this); } CancelRoute(); }
    void MapChanged() { CancelRoute(); }
    void CancelRoute() { generation++; waiting = false; route.Clear(); }
    public void StopFollowing() { follow = false; CancelRoute(); }
    public void Follow(Vector3 position, float distance)
    {
        var cell = NeoGridBoard.Cell(position);
        if (!follow || cell != target) { CancelRoute(); target = cell; }
        follow = true;
        stopDistance = distance;
    }
    public bool Step(Vector2 direction)
    {
        if (IsMoving || board == null || DemoBootstrap.IsPaused || direction.sqrMagnitude < .1f) return false;
        var delta = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y) ? new Vector2Int(direction.x > 0 ? 1 : -1, 0) : new Vector2Int(0, direction.y > 0 ? 1 : -1);
        return StartStep(Cell + delta);
    }
    bool StartStep(Vector2Int destination)
    {
        if (Mathf.Abs(destination.x - Cell.x) + Mathf.Abs(destination.y - Cell.y) != 1) return false;
        // A larger body reserves every cell it is about to cover, or none of them.
        foreach (var cell in CellsAt(destination))
            if (!board.Reserve(cell, this)) { board.Release(this); return false; }
        Destination = destination;
        stepDirection = (NeoGridBoard.Center(destination) - transform.position).normalized;
        IsMoving = true;
        return true;
    }
    void Update()
    {
        if (DemoBootstrap.IsPaused || board == null || !board.Ready) return;
        if (IsMoving)
        {
            var end = NeoGridBoard.Center(Destination);
            Animate(stepDirection);
            transform.position = Vector3.MoveTowards(transform.position, end, speed * Time.deltaTime);
            if (transform.position == end)
            {
                // Keep the walk cycle alive until we know whether another step follows.
                board.Release(this); IsMoving = false; Arrived?.Invoke();
            }
            return;
        }
        if (!follow || Vector2.Distance(transform.position, NeoGridBoard.Center(target)) <= stopDistance)
        { blockedSince = -1; Animate(Vector3.zero); return; }
        while (route.Count > 0 && NeoGridBoard.Cell(route[0]) == Cell) route.RemoveAt(0);
        if (route.Count > 0)
        {
            if (StartStep(NeoGridBoard.Cell(route[0]))) { route.RemoveAt(0); blockedSince = -1; return; }
            CancelRoute();
        }
        Animate(Vector3.zero);
        if (blockedSince < 0) blockedSince = Time.time;
        if (waiting || Time.time < nextRequest || seeker == null || !seeker.IsDone()) return;
        nextRequest = Time.time + .2f;
        waiting = true;
        int token = generation, revision = board.Revision;
        PathRequests++;
        seeker.StartPath(transform.position, NeoGridBoard.Center(target), path => {
            if (this == null || token != generation || revision != board.Revision) return;
            waiting = false;
            if (path.error) return;
            route.Clear();
            foreach (var point in path.vectorPath) route.Add(NeoGridBoard.Center(NeoGridBoard.Cell(point)));
        });
    }
    public void Teleport(Vector3 point)
    {
        CancelRoute(); IsMoving = false; board?.Release(this);
        transform.position = NeoGridBoard.Center(NeoGridBoard.Cell(point));
        Animate(Vector3.zero);
    }
    void Animate(Vector3 delta)
    {
        if (animator == null) return;
        bool horizontal = Mathf.Abs(delta.x) > .001f;
        animator.SetBool("RunHorizontal", horizontal);
        animator.SetBool("RunUp", !horizontal && delta.y > .001f);
        animator.SetBool("RunDown", !horizontal && delta.y < -.001f);
        if (horizontal)
        {
            // Only mirror: the visual may carry its own size.
            var scale = visual.localScale;
            scale.x = Mathf.Abs(scale.x) * (delta.x > 0 ? -1 : 1);
            visual.localScale = scale;
        }
    }
}
