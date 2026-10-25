using System.Collections.Generic;
using System.Linq;
using Pathfinding;
using UnityEngine;

/// <summary>
/// A wild Pokémon that strolls around its home cell. It only plans routes while it is inside
/// the A* grid that follows the player, so distant Pokémon stay idle off camera.
/// Its body covers NeoGridMotor.footprint cells, all of them held while it rests.
/// </summary>
[RequireComponent(typeof(NeoGridMotor), typeof(Seeker))]
public sealed class NeoWildPokemon : MonoBehaviour
{
    [Tooltip("Casillas que puede alejarse de su posición inicial.")]
    [Min(1)] public int wanderRadius = 3;
    [Tooltip("Segundos de pausa entre paseos (mínimo, máximo).")]
    public Vector2 pauseRange = new Vector2(1.5f, 4f);
    [Tooltip("Casillas máximas de cada paseo.")]
    [Min(1)] public int maxStepsPerWalk = 4;
    [Tooltip("Altura del flotado (Haunter, Beedrill). 0 = camina pegado al suelo.")]
    public float hoverAmplitude;
    public float hoverSpeed = 2f;
    [Tooltip("Respiración en reposo: cuánto se estira el sprite hacia arriba (0,03 = 3 %).")]
    public float breathAmount = .03f;
    public float breathSpeed = 2.5f;

    NeoGridMotor motor;
    Seeker seeker;
    NeoGridBoard board;
    Vector2Int home;
    Vector2Int heldCell;
    bool holding;
    readonly List<Vector2Int> route = new List<Vector2Int>();
    float nextWalk;
    bool waitingPath;
    Vector3 visualRest;
    float visualHeight;
    float phase;

    void Awake()
    {
        motor = GetComponent<NeoGridMotor>();
        seeker = GetComponent<Seeker>();
        if (motor.visual != null) { visualRest = motor.visual.localPosition; visualHeight = motor.visual.localScale.y; }
        phase = Random.value * Mathf.PI * 2;
    }
    void OnEnable() { motor.Arrived += HoldCurrentCells; }
    void OnDisable()
    {
        motor.Arrived -= HoldCurrentCells;
        if (board != null) board.Release(motor);
        holding = false;
    }
    void Start()
    {
        board = motor.board;
        home = motor.Cell;
        nextWalk = Time.time + Random.Range(pauseRange.x, pauseRange.y);
    }

    // The motor only reserves cells while stepping. Holding the standing cells keeps the
    // player (and other Pokémon) from walking into this one while it rests. They are held in
    // the motor's name, so a large body may step into cells it already covers; the motor
    // releases them all when it arrives, and the new ones are held again.
    void HoldCurrentCells()
    {
        if (board == null || (holding && heldCell == motor.Cell)) return;
        board.Release(motor);
        holding = true;
        foreach (var cell in motor.CellsAt(motor.Cell)) holding &= board.Reserve(cell, motor);
        heldCell = motor.Cell;
    }

    void Update()
    {
        Animate();
        if (board == null || !board.Ready || DemoBootstrap.IsPaused) return;
        if (!holding) HoldCurrentCells();
        if (motor.IsMoving || waitingPath) return;
        if (!InGraph(motor.Cell)) { route.Clear(); return; }

        if (route.Count > 0)
        {
            var next = route[0];
            route.RemoveAt(0);
            var delta = next - motor.Cell;
            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1 && !motor.CellsAt(next).Contains(PlayerCell()) && motor.Step(delta)) return;
            route.Clear(); // Blocked: rest and choose another stroll.
            holding = false; HoldCurrentCells(); // A refused step releases the motor's cells.
        }
        if (route.Count == 0 && Time.time >= nextWalk) PlanWalk();
    }

    void PlanWalk()
    {
        nextWalk = Time.time + Random.Range(pauseRange.x, pauseRange.y);
        for (int attempt = 0; attempt < 8; attempt++)
        {
            var goal = home + new Vector2Int(Random.Range(-wanderRadius, wanderRadius + 1), Random.Range(-wanderRadius, wanderRadius + 1));
            if (goal == motor.Cell || !InGraph(goal) || !motor.CellsAt(goal).All(c => board.CanEnter(c, motor))) continue;
            waitingPath = true;
            int revision = board.Revision;
            seeker.StartPath(NeoGridBoard.Center(motor.Cell), NeoGridBoard.Center(goal), path => {
                if (this == null) return;
                waitingPath = false;
                if (path.error || revision != board.Revision) return;
                route.Clear();
                var last = motor.Cell;
                foreach (var point in path.vectorPath)
                {
                    var cell = NeoGridBoard.Cell(point);
                    if (cell == last) continue;
                    route.Add(cell);
                    last = cell;
                    if (route.Count >= maxStepsPerWalk) break;
                }
            });
            return;
        }
    }

    // The navigation grid moves with the player; outside it there are no nodes to plan on.
    bool InGraph(Vector2Int anchor)
    {
        var graph = board.Graph;
        if (graph == null) return false;
        var low = NeoGridBoard.Center(anchor);
        var high = NeoGridBoard.Center(anchor + motor.footprint - Vector2Int.one);
        float halfWidth = graph.width * .5f - 1, halfDepth = graph.depth * .5f - 1;
        return Mathf.Abs(low.x - graph.center.x) < halfWidth && Mathf.Abs(high.x - graph.center.x) < halfWidth
            && Mathf.Abs(low.y - graph.center.y) < halfDepth && Mathf.Abs(high.y - graph.center.y) < halfDepth;
    }
    Vector2Int PlayerCell() => board.navigationTarget != null ? NeoGridBoard.Cell(board.navigationTarget.position) : new Vector2Int(int.MinValue, int.MinValue);

    // Hover and breathing are procedural: the sheets only have one or two idle frames.
    void Animate()
    {
        var visual = motor.visual;
        if (visual == null || DemoBootstrap.IsPaused) return;
        float t = Time.time + phase;
        if (hoverAmplitude > 0)
            visual.localPosition = visualRest + Vector3.up * ((Mathf.Sin(t * hoverSpeed) * .5f + .5f) * hoverAmplitude);
        var scale = visual.localScale;
        scale.y = visualHeight * (motor.IsMoving ? 1 : 1 + (Mathf.Sin(t * breathSpeed) * .5f + .5f) * breathAmount);
        visual.localScale = scale;
    }
}
