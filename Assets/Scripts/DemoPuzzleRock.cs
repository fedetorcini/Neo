using UnityEngine;

public sealed class DemoPuzzleRock : Interactable
{
    [SerializeField] private float moveSpeed = 4f;
    private DemoAdventureCourse course;
    private string puzzleId;
    private Vector3 spawnPosition;
    private Vector3 targetPosition;
    private Vector3 destination;
    private bool moving;

    public string PuzzleId => puzzleId;
    public Vector3 TargetPosition => targetPosition;
    public bool IsMoving => moving;

    public void Configure(DemoAdventureCourse owner, string id, Vector3 target)
    {
        course = owner;
        puzzleId = id;
        spawnPosition = Snap(transform.position);
        targetPosition = Snap(target);
        transform.position = spawnPosition;
        destination = spawnPosition;
    }

    private void Update()
    {
        if (!moving || DemoBootstrap.IsPaused) return;
        transform.position = Vector3.MoveTowards(transform.position, destination, moveSpeed * Time.deltaTime);
        if ((transform.position - destination).sqrMagnitude > .0001f) return;
        transform.position = destination;
        moving = false;
        Physics2D.SyncTransforms();
        if ((transform.position - targetPosition).sqrMagnitude < .001f && course != null)
            course.OnPuzzleRockSettled(this);
    }

    public override void Interact(Interactable interacting)
    {
        if (moving || interacting == null) return;
        PartnerAI partner = interacting.GetComponent<PartnerAI>();
        if (partner == null)
        {
            if (course != null) course.ShowHint("Solo Machop puede empujar estas rocas.");
            return;
        }

        DemoPartnerCombat combat = partner.GetComponent<DemoPartnerCombat>();
        if (combat != null && combat.Species != DemoSpecies.Machop)
        {
            if (course != null) course.ShowHint("Pikachu no tiene fuerza suficiente para mover la roca.");
            return;
        }

        Vector2 difference = (Vector2)(transform.position - interacting.transform.position);
        if (Mathf.Abs(difference.x) + Mathf.Abs(difference.y) > 1.3f || difference.sqrMagnitude < .5f)
        {
            if (course != null) course.ShowHint("Acércate a un lado de la roca para empujarla.");
            return;
        }

        Vector2 direction = Mathf.Abs(difference.x) >= Mathf.Abs(difference.y)
            ? new Vector2(Mathf.Sign(difference.x), 0f)
            : new Vector2(0f, Mathf.Sign(difference.y));
        Vector3 next = Snap(transform.position + (Vector3)direction);
        Collider2D blocker = Physics2D.OverlapCircle(next, .32f, LayerMask.GetMask("Collider", "Movable", "Player", "Characters"));
        if (blocker != null && blocker.gameObject != gameObject)
        {
            if (course != null) course.ShowHint("No hay espacio para empujar la roca hacia allí.");
            return;
        }

        destination = next;
        moving = true;
    }

    public void ResetPuzzle()
    {
        moving = false;
        destination = spawnPosition;
        transform.position = spawnPosition;
        Physics2D.SyncTransforms();
    }

    private static Vector3 Snap(Vector3 value)
    {
        return new Vector3(Mathf.Floor(value.x) + .5f, Mathf.Floor(value.y) + .5f, value.z);
    }
}
