using UnityEngine;

public sealed class DemoProjectile : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Sprite[] frames;
    private float animationClock;
    private int frameIndex;
    private Vector2 direction;
    private float speed;
    private float damage;
    private Transform owner;
    private float lifetime = 2f;

    public static void Launch(Vector3 position, Vector2 direction, float speed, float damage, Transform owner)
    {
        GameObject projectile = new GameObject("Descarga de Pikachu");
        projectile.transform.position = position;
        projectile.transform.localScale = new Vector3(.34f, .22f, 1f);
        DemoProjectile behaviour = projectile.AddComponent<DemoProjectile>();
        behaviour.direction = direction.normalized;
        behaviour.speed = speed;
        behaviour.damage = damage;
        behaviour.owner = owner;
        behaviour.spriteRenderer = projectile.AddComponent<SpriteRenderer>();
        behaviour.frames = Resources.LoadAll<Sprite>("Demo/Art/PikachuAttack");
        behaviour.spriteRenderer.sprite = behaviour.frames.Length > 0 ? behaviour.frames[0] : DemoPixelArt.Square;
        behaviour.spriteRenderer.color = new Color(1f, .9f, .16f);
        behaviour.spriteRenderer.sortingOrder = 45;
        projectile.transform.localScale = new Vector3(.48f, .48f, 1f);
    }

    private void Update()
    {
        if (DemoBootstrap.IsPaused) return;
        if (frames != null && frames.Length > 1)
        {
            animationClock += Time.deltaTime;
            if (animationClock >= .08f)
            {
                animationClock = 0f;
                frameIndex = (frameIndex + 1) % frames.Length;
                spriteRenderer.sprite = frames[frameIndex];
            }
        }
        float step = speed * Time.deltaTime;
        RaycastHit2D wall = Physics2D.Raycast(transform.position, direction, step, LayerMask.GetMask("Collider", "Movable"));
        if (wall.collider != null)
        {
            Destroy(gameObject);
            return;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position + (Vector3)(direction * step), .25f, LayerMask.GetMask("Enemy"));
        foreach (Collider2D hit in hits)
        {
            if (owner != null && hit.transform.IsChildOf(owner)) continue;
            MonoBehaviour[] components = hit.GetComponentsInParent<MonoBehaviour>();
            foreach (MonoBehaviour component in components)
            {
                IDemoDamageable target = component as IDemoDamageable;
                if (target == null) continue;
                target.TakeDamage(damage);
                Destroy(gameObject);
                return;
            }
        }

        transform.position += (Vector3)(direction * step);
        lifetime -= Time.deltaTime;
        if (lifetime <= 0f) Destroy(gameObject);
    }
}
