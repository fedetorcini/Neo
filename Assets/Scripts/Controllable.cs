using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Controllable : MonoBehaviour
{
    public abstract void SetTarget(Vector3 newTarget);

    public abstract void Release();
}
