using System.Collections.Generic;
using Pathfinding;
using UnityEngine;
using UnityEngine.EventSystems;

public class Controller : MonoBehaviour
{
    public CameraScript mainCamera;
    public ProceduralGridMover AIPathfinder;
    public Transform marker;

    public List<Controllable> controllablesManual; 
    private readonly List<Controllable> controllables = new List<Controllable>();
    private Controllable active;
    private Controllable pendingActive;
    public LayerMask interactables;
    [SerializeField] private float gridSize = 1f;
    [SerializeField] private float retryEnergyThreshold = 25f;
    [SerializeField] private float moveRepeatDelay = 0.16f;
    private float nextMoveTime;
    private int selectedPartnerIndex;

    public Controllable Active => active;
    public event System.Action<Controllable> ActiveChanged;

    void Awake()
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0) interactables |= 1 << enemyLayer;
        controllables.Clear();
        if (controllablesManual != null)
        {
            foreach (Controllable controllable in controllablesManual)
            {
                if (controllable != null && !controllables.Contains(controllable))
                    controllables.Add(controllable);
            }
        }

        if (controllables.Count > 0)
        {
            active = controllables[0];
            selectedPartnerIndex = 0;
            if (mainCamera == null) mainCamera = active.GetComponentInChildren<CameraScript>(true);
            if (mainCamera != null) mainCamera.SetTarget(active.transform);
            var motor = active.GetComponent<NeoGridMotor>();
            if (motor != null && motor.board != null) motor.board.navigationTarget = active.transform;
        }
    }

    void Update()
    {
        if (DemoBootstrap.IsPaused || active == null) return;
        if (pendingActive != null)
        {
            var motor = active.GetComponent<NeoGridMotor>();
            if (motor == null || !motor.IsMoving) { var next = pendingActive; pendingActive = null; SetActive(next); }
            return;
        }
        if (marker != null) marker.position = active.transform.position;
        UpdateInput();
    }

    public void UpdateInput()
    {
        if (active == null || DemoBootstrap.IsPaused || Time.unscaledTime < DemoBootstrap.InputBlockedUntil) return;

        if (active.GetTirerness() <= 0 && active is not PlayerAI)
        {
            SelectHuman();
        }

        if (Input.GetKeyDown(KeyCode.Space)) ToggleControl();

        if (Input.GetKeyDown(KeyCode.Alpha1)) ChoosePartner(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) ChoosePartner(1);

        Vector2 movement = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (movement.sqrMagnitude > 0.1f && Time.unscaledTime >= nextMoveTime)
        {
            movement = Mathf.Abs(movement.x) >= Mathf.Abs(movement.y)
                ? new Vector2(Mathf.Sign(movement.x), 0f)
                : new Vector2(0f, Mathf.Sign(movement.y));
            bool accepted;
            if (active is PlayerAI human)
                accepted = human.movementScript.MoveManual(movement);
            else if (active is PartnerAI partner)
                accepted = partner.movementScript.MoveManual(movement);
            else
            {
                active.SetTarget(active.transform.position + (Vector3)(movement * gridSize));
                accepted = true;
            }
            // A busy motor must be retried next frame, without another repeat delay.
            if (accepted) nextMoveTime = Time.unscaledTime + moveRepeatDelay;
        }

        if (Input.GetKeyDown(KeyCode.E))
            InteractNearby();

        if (Input.GetMouseButtonDown(0) && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
        {
            FireAtPointer();
        }
    }

    private void ToggleControl()
    {
        if (active is PlayerAI) ControlSelectedPartner();
        else SelectHuman();
    }

    private void SelectHuman()
    {
        for (int i = 0; i < controllables.Count; i++)
        {
            if (controllables[i] is PlayerAI)
            {
                SetActive(controllables[i]);
                return;
            }
        }
    }

    private void ChoosePartner(int requestedIndex)
    {
        List<PartnerAI> partners = GetPartners();
        if (requestedIndex < 0 || requestedIndex >= partners.Count) return;

        PartnerAI requested = partners[requestedIndex];
        if (requested.GetTirerness() < retryEnergyThreshold) return;
        selectedPartnerIndex = requestedIndex;
        if (active is PlayerAI) SetActive(requested);
        else if (active != requested) SelectHuman();
    }

    private void ControlSelectedPartner()
    {
        List<PartnerAI> partners = GetPartners();
        if (partners.Count == 0) return;
        int index = Mathf.Clamp(selectedPartnerIndex, 0, partners.Count - 1);
        if (partners[index].GetTirerness() >= retryEnergyThreshold) SetActive(partners[index]);
    }

    private List<PartnerAI> GetPartners()
    {
        List<PartnerAI> partners = new List<PartnerAI>();
        foreach (Controllable controllable in controllables)
            if (controllable is PartnerAI partner) partners.Add(partner);
        return partners;
    }

    private void SetActive(Controllable next)
    {
        if (next == null || next == active) return;
        var moving = active != null ? active.GetComponent<NeoGridMotor>() : null;
        if (moving != null && moving.IsMoving) { pendingActive = next; moving.StopFollowing(); return; }
        if (active != null) active.Release();
        active = next;
        if (active is PartnerAI partner)
        {
            selectedPartnerIndex = GetPartners().IndexOf(partner);
            partner.BeginDirectControl();
        }
        else if (active is PlayerAI) active.Release();
        if (AIPathfinder != null) AIPathfinder.target = active.transform;
        var motor = active.GetComponent<NeoGridMotor>();
        if (motor != null && motor.board != null) motor.board.navigationTarget = active.transform;
        if (mainCamera != null) mainCamera.SetTarget(active.transform);
        ActiveChanged?.Invoke(active);
    }

    private void InteractNearby()
    {
        Collider2D[] nearby = Physics2D.OverlapCircleAll(active.transform.position, 1.25f, interactables);
        Interactable closest = null;
        float closestDistance = float.MaxValue;
        foreach (Collider2D collider in nearby)
        {
            Interactable candidate = collider.GetComponent<Interactable>();
            if (candidate == null || candidate.gameObject == active.gameObject || candidate is not NeoPushRock && candidate is not NeoRanger && active.GetComponent<NeoGridMotor>() != null) continue;
            float distance = (candidate.transform.position - active.transform.position).sqrMagnitude;
            if (distance < closestDistance)
            {
                closest = candidate;
                closestDistance = distance;
            }
        }
        if (closest != null)
        {
            Interactable actor = active.GetComponent<Interactable>();
            if (actor != null) closest.Interact(actor);
        }
    }

    private void FireAtPointer()
    {
        if (active is PlayerAI || Camera.main == null) return;
        Vector3 pointer = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        pointer.z = active.transform.position.z;
        Vector2 direction = (pointer - active.transform.position).normalized;
        if (direction.sqrMagnitude < 0.01f) return;

        DemoPartnerCombat combat = active.GetComponent<DemoPartnerCombat>();
        if (combat != null)
        {
            combat.Attack(direction);
            return;
        }

        RaycastHit2D hit = Physics2D.Raycast(active.transform.position, direction, 6f);
        if (hit.collider == null) return;
        IDemoDamageable target = null;
        MonoBehaviour[] components = hit.collider.GetComponentsInParent<MonoBehaviour>();
        foreach (MonoBehaviour component in components)
        {
            target = component as IDemoDamageable;
            if (target != null) break;
        }
        if (target != null && active is PartnerAI) target.TakeDamage(20f);
    }

    public void RegisterPartner(PartnerAI partner)
    {
        if (partner == null || controllables.Contains(partner)) return;
        PlayerAI human = GetComponentInChildren<PlayerAI>();
        if (human == null) human = FindAnyObjectByType<PlayerAI>();
        if (human != null) partner.master = human.transform;
        controllables.Add(partner);
    }

    public void SelectPartnerByIndex(int requestedIndex)
    {
        ChoosePartner(requestedIndex);
    }

    public void ReturnControlToHuman()
    {
        SelectHuman();
    }

    public int PartnerCount
    {
        get
        {
            int count = 0;
            foreach (Controllable controllable in controllables)
                if (controllable is PartnerAI) count++;
            return count;
        }
    }

    public bool IsPartnerControlled => active is PartnerAI;
}
