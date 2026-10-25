using UnityEngine;

public sealed class NeoRanger : Interactable
{
    public NeoFirstRoom room;
    public override void Interact(Interactable actor)
    {
        if (actor != null && actor.GetComponent<PlayerAI>() != null && !DemoBootstrap.IsPaused) room.TalkToRanger();
    }
}
