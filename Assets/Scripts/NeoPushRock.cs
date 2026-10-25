using System;
using UnityEngine;

public sealed class NeoPushRock : Interactable
{
    public NeoGridBoard board;
    public Vector2Int initialCell;
    public Vector2Int solutionCell;
    public bool IsMoving { get; private set; }
    public bool Solved => !IsMoving && NeoGridBoard.Cell(transform.position) != initialCell;
    public event Action Settled;
    Vector2Int origin, destination;
    void Awake() { origin = NeoGridBoard.Cell(transform.position); board.RegisterRock(this, origin); }
    public override void Interact(Interactable actor)
    {
        if (DemoBootstrap.IsPaused || IsMoving || actor == null) return;
        var combat = actor.GetComponent<DemoPartnerCombat>();
        var partner = actor.GetComponent<PartnerAI>();
        if (partner == null || !partner.IsDirectlyControlled || combat == null || !combat.CanPushRocks) return;
        var motor = actor.GetComponent<NeoGridMotor>();
        if (motor == null || motor.IsMoving) return;
        origin = NeoGridBoard.Cell(transform.position);
        var delta = origin - motor.Cell;
        if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1) return;
        destination = origin + delta;
        if (!board.ReserveRock(this, origin, destination)) return;
        IsMoving = true;
    }
    void Update()
    {
        if (!IsMoving || DemoBootstrap.IsPaused) return;
        transform.position = Vector3.MoveTowards(transform.position, NeoGridBoard.Center(destination), 4 * Time.deltaTime);
        if (transform.position != NeoGridBoard.Center(destination)) return;
        IsMoving = false;
        board.RelocateRock(this, origin, destination);
        Settled?.Invoke();
    }
    public void ResetRock(bool solved = false)
    {
        var previous = IsMoving ? origin : NeoGridBoard.Cell(transform.position);
        IsMoving = false;
        var cell = solved ? solutionCell : initialCell;
        transform.position = NeoGridBoard.Center(cell);
        board.RelocateRock(this, previous, cell);
    }
}
