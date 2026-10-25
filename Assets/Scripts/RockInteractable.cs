using System;
using Pathfinding;
using UnityEngine;

/// <summary>A one-cell pushable rock. Only a directly controlled Machop can move it.</summary>
public class RockInteractable : Interactable
{
    public LayerMask collisions;
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private bool machopOnly = true;

    private bool moving;
    private Vector3 targetPosition;
    private Vector3 initialPosition;
    private Vector3 moveStartPosition;

    public event Action<RockInteractable, Vector3, Vector3> PushCompleted;
    public Vector3 InitialPosition => initialPosition;
    public bool IsMoving => moving;

    private void Awake()
    {
        initialPosition = Snap(transform.position);
        targetPosition = initialPosition;
        transform.position = initialPosition;
    }

    private void Update()
    {
        if (!moving || DemoBootstrap.IsPaused) return;

        transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
        if ((transform.position - targetPosition).sqrMagnitude > .0001f) return;

        transform.position = targetPosition;
        moving = false;
        if (AstarPath.active != null) AstarPath.active.Scan();
        PushCompleted?.Invoke(this, moveStartPosition, targetPosition);
    }

    public override void Interact(Interactable interacting)
    {
        if (moving || interacting == null) return;
        PartnerAI partner = interacting.GetComponent<PartnerAI>();
        if (machopOnly && (partner == null || !IsMachop(partner)))
        {
            Debug.Log("Solo Machop puede empujar esta roca.");
            return;
        }

        Vector2 delta = (Vector2)(transform.position - interacting.transform.position);
        Vector2 direction = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
            ? new Vector2(Mathf.Sign(delta.x), 0f)
            : new Vector2(0f, Mathf.Sign(delta.y));
        if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) > 1.3f || delta.sqrMagnitude < .5f)
        {
            Debug.Log("Acércate a uno de los lados de la roca.");
            return;
        }

        Vector3 next = Snap(transform.position + (Vector3)direction);
        Collider2D obstruction = Physics2D.OverlapCircle(next, .32f, collisions);
        if (obstruction != null && obstruction.gameObject != gameObject)
        {
            Debug.Log("La roca no puede avanzar en esa dirección.");
            return;
        }

        targetPosition = next;
        moveStartPosition = transform.position;
        moving = true;
    }

    public void ResetToInitialPosition()
    {
        moving = false;
        targetPosition = initialPosition;
        transform.position = initialPosition;
        if (AstarPath.active != null) AstarPath.active.Scan();
    }

    private static bool IsMachop(PartnerAI partner)
    {
        DemoPartnerCombat combat = partner.GetComponent<DemoPartnerCombat>();
        return combat == null || combat.Species == DemoSpecies.Machop;
    }

    private static Vector3 Snap(Vector3 position)
    {
        return new Vector3(Mathf.Floor(position.x) + .5f, Mathf.Floor(position.y) + .5f, position.z);
    }
}
