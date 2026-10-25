using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WarpScript : Interactable
{
    public WarpScript pair;

    public override void Interact(Interactable interacting)
    {
        if (Vector2.Distance(interacting.transform.position, transform.position) == 1.0f)
        {
            Teleportable aux = interacting.GetComponent<Teleportable>();
            if (aux)
            {
                aux.Teleport(pair.GetPosition());
            }
        }
        else 
        {
            Debug.Log("This looks like it can get me somewhere...");
        }
    }

    public Vector3 GetPosition()
    {
        return transform.position;
    }
}
