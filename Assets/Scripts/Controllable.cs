using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Controllable : MonoBehaviour
{
    public float MAX_TIRERNESS = 100f;
    protected float tirerness;

    public float MaxEnergy => MAX_TIRERNESS;
    public float Energy => tirerness;

    public abstract void SetTarget(Vector3 newTarget);

    public abstract void Release();

    public abstract void Recover();

    public float GetTirerness()
    {
        return tirerness;
    }

    public void RestoreEnergy()
    {
        tirerness = MAX_TIRERNESS;
    }
}
