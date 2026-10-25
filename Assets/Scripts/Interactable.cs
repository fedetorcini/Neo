using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Interactable : MonoBehaviour
{
    public virtual void Interact(Interactable interacting)
    {
        Debug.Log("Hi! " + interacting.name + " this is an Interactable.");
    }
}
