using UnityEngine;

public sealed class DemoCourseGate : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Collider2D blockingCollider;

    public void Configure(SpriteRenderer renderer, Collider2D collider)
    {
        spriteRenderer = renderer;
        blockingCollider = collider;
        SetOpen(false);
    }

    public void SetOpen(bool isOpen)
    {
        if (spriteRenderer != null) spriteRenderer.color = isOpen
            ? new Color(.35f, .83f, .48f, .28f)
            : new Color(.81f, .49f, .24f, 1f);
        if (blockingCollider != null) blockingCollider.enabled = !isOpen;
    }
}
