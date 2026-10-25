using UnityEngine;

public sealed class DemoWorldInteractable : Interactable
{
    public enum InteractionKind { Ranger, Camp }
    [SerializeField] private InteractionKind kind;
    private DemoAdventureCourse course;

    public void Configure(DemoAdventureCourse owner, InteractionKind interactionKind)
    {
        course = owner;
        kind = interactionKind;
    }

    public override void Interact(Interactable interacting)
    {
        if (course == null) return;
        if (kind == InteractionKind.Ranger) course.TalkToRanger();
        else course.TalkAtCamp();
    }
}
