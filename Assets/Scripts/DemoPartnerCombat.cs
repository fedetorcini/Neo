using UnityEngine;

public enum DemoSpecies
{
    Machop,
    Pikachu
}

/// <summary>Small demo-only combat adapter; the legacy movement and team objects stay intact.</summary>
public sealed class DemoPartnerCombat : MonoBehaviour
{
    [SerializeField] private DemoSpecies species = DemoSpecies.Machop;
    [SerializeField] private float attackCooldown = .65f;
    [SerializeField] private float meleeDamage = 25f;
    [SerializeField] private float rangedDamage = 18f;
    [SerializeField] private float projectileSpeed = 8f;
    private float nextAttackTime;
    private Vector2 lastAimDirection = Vector2.right;

    public DemoSpecies Species => species;
    public bool CanPushRocks => species == DemoSpecies.Machop;
    public string DisplayName => species == DemoSpecies.Machop ? "Machop" : "Pikachu";
    public Vector2 LastAimDirection => lastAimDirection;

    public void SetSpecies(DemoSpecies value)
    {
        species = value;
    }

    public bool Attack(Vector2 direction)
    {
        if (DemoBootstrap.IsPaused || Time.time < nextAttackTime || direction.sqrMagnitude < .001f)
            return false;

        lastAimDirection = direction.normalized;
        nextAttackTime = Time.time + attackCooldown;
        if (species == DemoSpecies.Pikachu)
        {
            DemoProjectile.Launch(transform.position + (Vector3)(lastAimDirection * .65f), lastAimDirection, projectileSpeed, rangedDamage, transform);
            ShowStrike(lastAimDirection, new Color(1f, .9f, .2f), .38f);
        }
        else
        {
            Vector2 center = (Vector2)transform.position + lastAimDirection * .9f;
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, .9f, LayerMask.GetMask("Enemy"));
            foreach (Collider2D hit in hits)
            {
                IDemoDamageable target = FindDamageable(hit);
                if (target == null) continue;
                Vector2 offset = (Vector2)hit.transform.position - (Vector2)transform.position;
                if (Vector2.Dot(lastAimDirection, offset.normalized) < .25f) continue;
                target.TakeDamage(meleeDamage);
            }
            ShowStrike(lastAimDirection, new Color(1f, .63f, .28f), .85f);
        }
        return true;
    }

    private static IDemoDamageable FindDamageable(Collider2D collider)
    {
        MonoBehaviour[] components = collider.GetComponentsInParent<MonoBehaviour>();
        foreach (MonoBehaviour component in components)
        {
            IDemoDamageable damageable = component as IDemoDamageable;
            if (damageable != null) return damageable;
        }
        return null;
    }

    private void ShowStrike(Vector2 direction, Color color, float length)
    {
        GameObject effect = new GameObject("Ataque · " + DisplayName);
        effect.transform.position = transform.position + (Vector3)(direction * .8f);
        effect.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        effect.transform.localScale = new Vector3(length, .12f, 1f);
        SpriteRenderer renderer = effect.AddComponent<SpriteRenderer>();
        renderer.sprite = DemoPixelArt.Square;
        renderer.color = color;
        renderer.sortingOrder = 40;
        Destroy(effect, .12f);
    }
}
