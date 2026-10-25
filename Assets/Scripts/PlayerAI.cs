using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAI : Controllable
{    
    public PlayerMovement movementScript;

    void Awake()
    {
        tirerness = MAX_TIRERNESS;
        movementScript = GetComponent<PlayerMovement>();
    }

    public override void SetTarget(Vector3 newTarget)
    {
        movementScript.SetTarget(newTarget);
    }

    public override void Release()
    {
    }

    public override void Recover()
    {
    }
}
