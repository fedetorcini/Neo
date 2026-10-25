using UnityEngine;

public sealed class DemoSpriteAnimator : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Sprite[] frames;
    private float frameDuration = .24f;
    private float timer;
    private int index;

    public void Configure(SpriteRenderer target, Sprite[] animationFrames, float duration = .24f)
    {
        spriteRenderer = target;
        frames = animationFrames;
        frameDuration = Mathf.Max(.04f, duration);
        index = 0;
        if (spriteRenderer != null && frames != null && frames.Length > 0)
            spriteRenderer.sprite = frames[0];
    }

    private void Update()
    {
        if (DemoBootstrap.IsPaused || spriteRenderer == null || frames == null || frames.Length < 2) return;
        timer += Time.deltaTime;
        if (timer < frameDuration) return;
        timer = 0f;
        index = (index + 1) % frames.Length;
        spriteRenderer.sprite = frames[index];
    }
}
