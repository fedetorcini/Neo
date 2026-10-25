using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PartnerAI : Controllable
{
    public TirernessBar tirernessBar;

    [Tooltip("Valor histórico conservado para escenas antiguas.")]
    public float RECOVER_PER_TURN_AMOUNT;

    [Min(0f)] public float energyDrainPerDistancePerSecond = 3f;
    [Min(0f)] public float minimumEnergyDrainPerSecond = 1f;
    [Min(0f)] public float energyRecoveryPerSecond = 10f;

    public Transform master;
    public int HOVER_DISTANCE = 2;
    public PartnerMovement movementScript;

    private bool following;
    private bool directlyControlled;

    void Awake()
    {
        if (MAX_TIRERNESS <= 0f) MAX_TIRERNESS = 100f;
        tirerness = MAX_TIRERNESS;
        if (tirernessBar != null) tirernessBar.SetMaxTirerness(MAX_TIRERNESS);
        movementScript = GetComponent<PartnerMovement>();
        following = true;
    }

    void Update()
    {
        if (DemoBootstrap.IsPaused || master == null) return;

        if (!directlyControlled)
        {
            if (following && movementScript != null)
                movementScript.SetTarget(master.position, HOVER_DISTANCE);
            tirerness = Mathf.Min(MAX_TIRERNESS, tirerness + energyRecoveryPerSecond * Time.deltaTime);
        }
        else
        {
            float distance = Vector2.Distance(master.position, transform.position);
            float drain = Mathf.Max(minimumEnergyDrainPerSecond, distance * energyDrainPerDistancePerSecond);
            tirerness = Mathf.Max(0f, tirerness - drain * Time.deltaTime);
        }

        if (tirernessBar != null) tirernessBar.SetTirerness(tirerness);
    }

    public override void SetTarget(Vector3 newTarget)
    {
        following = false;
        movementScript.SetTarget(newTarget);
    }

    public override void Release()
    {
        directlyControlled = false;
        following = true;
    }

    public void BeginDirectControl()
    {
        directlyControlled = true;
        following = false;
        var motor = GetComponent<NeoGridMotor>();
        if (motor != null) motor.StopFollowing();
        else if (movementScript != null) movementScript.SetTarget(transform.position);
    }

    public bool IsDirectlyControlled => directlyControlled;

    public override void Recover()
    {
        tirerness = Mathf.Min(MAX_TIRERNESS, tirerness + energyRecoveryPerSecond * Time.deltaTime);
        if (tirernessBar != null) tirernessBar.SetTirerness(tirerness);
    }
}
