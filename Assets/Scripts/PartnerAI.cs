using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PartnerAI : Controllable
{
    public Transform master;
    public int HOVER_DISTANCE = 2;
    public PartnerMovement movementScript;

    private bool following;

    void Awake()
    {
        movementScript = GetComponent<PartnerMovement>();
        following = true;
    }

    void Update()
    {
        if (following == true)
        {
            movementScript.SetTarget(master.position, HOVER_DISTANCE);
        }
    }

    public override void SetTarget(Vector3 newTarget)
    {
        following = false;
        movementScript.SetTarget(newTarget);
    }

    public override void Release()
    {
        following = true;
    }
}
