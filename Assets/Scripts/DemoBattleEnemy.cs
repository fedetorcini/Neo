using UnityEngine;

public sealed class DemoBattleEnemy : Interactable, IDemoDamageable
{
    private DemoAdventureCourse course;
    private bool boss;
    private float health;
    private float maximumHealth;
    private float nextAttackTime;
    private float tellRemaining;
    private bool attackTelegraphed;
    private bool circleAttack;
    private Vector2 attackDirection;
    private Vector3 attackOrigin;
    private Transform warningTransform;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer healthFill;
    private Sprite[] animationFrames;
    private float animationTimer;
    private float flashTimer;
    private Color baseColor = Color.white;
    private bool defeated;
    private string displayName;

    public float CurrentHealth => health;
    public float MaximumHealth => maximumHealth;
    public bool IsBoss => boss;
    public string DisplayName => displayName;

    public void Configure(DemoAdventureCourse owner, bool isBoss, string actorName, float maxHealth, Sprite[] frames)
    {
        course = owner;
        boss = isBoss;
        displayName = actorName;
        maximumHealth = maxHealth;
        health = maxHealth;
        animationFrames = frames;
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && frames != null && frames.Length > 0)
        {
            spriteRenderer.sprite = frames[0];
            baseColor = spriteRenderer.color;
        }
        CreateHealthBar();
        nextAttackTime = Time.time + 1.5f;
    }

    private void Update()
    {
        if (DemoBootstrap.IsPaused || defeated) return;
        Animate();
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f && spriteRenderer != null) spriteRenderer.color = baseColor;
        }

        if (attackTelegraphed)
        {
            tellRemaining -= Time.deltaTime;
            if (tellRemaining <= 0f) ResolveAttack();
            return;
        }

        PartnerAI target = course != null ? course.GetEnemyTarget() : null;
        if (target == null || Vector2.Distance(transform.position, target.transform.position) > 7f) return;
        if (Time.time >= nextAttackTime) BeginAttack(target.transform.position);
    }

    public void TakeDamage(float amount)
    {
        if (defeated || amount <= 0f) return;
        health = Mathf.Max(0f, health - amount);
        if (healthFill != null)
        {
            Vector3 scale = healthFill.transform.localScale;
            scale.x = Mathf.Clamp01(health / maximumHealth);
            healthFill.transform.localScale = scale;
            healthFill.color = health / maximumHealth <= .25f ? new Color(1f, .31f, .23f) : new Color(.28f, .9f, .37f);
        }
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
            flashTimer = .12f;
        }
        if (health <= 0f && boss)
        {
            defeated = true;
            if (course != null) course.BossDefeated(this);
            Collider2D collider = GetComponent<Collider2D>();
            if (collider != null) collider.enabled = false;
            PlayExplosion();
        }
    }

    public override void Interact(Interactable interacting)
    {
        if (boss || defeated) return;
        if (health > maximumHealth * .25f)
        {
            if (course != null) course.ShowHint("Debilita a Pikachu hasta el 25 % para poder acercarte y capturarlo.");
            return;
        }
        if (course != null) course.CapturePikachu(this);
    }

    public void ResetEncounter()
    {
        defeated = false;
        attackTelegraphed = false;
        health = maximumHealth;
        nextAttackTime = Time.time + 1f;
        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null) collider.enabled = true;
        if (warningTransform != null) Destroy(warningTransform.gameObject);
        UpdateHealthBar();
    }

    private void BeginAttack(Vector3 targetPosition)
    {
        attackOrigin = transform.position;
        attackDirection = ((Vector2)(targetPosition - transform.position)).normalized;
        circleAttack = boss && Time.frameCount % 2 == 0;
        tellRemaining = boss ? .85f : .65f;
        nextAttackTime = Time.time + (boss ? 2.4f : 1.9f);
        attackTelegraphed = true;
        CreateWarning();
    }

    private void ResolveAttack()
    {
        attackTelegraphed = false;
        if (warningTransform != null) Destroy(warningTransform.gameObject);
        PartnerAI target = course != null ? course.GetEnemyTarget() : null;
        if (target == null) return;

        Vector2 offset = (Vector2)(target.transform.position - attackOrigin);
        bool hit;
        float damage;
        if (circleAttack)
        {
            hit = offset.magnitude <= 3f;
            damage = 22f;
        }
        else
        {
            float along = Vector2.Dot(offset, attackDirection);
            float side = Mathf.Abs(attackDirection.x * offset.y - attackDirection.y * offset.x);
            hit = along > 0f && along <= (boss ? 5f : 3.5f) && side <= (boss ? .8f : .6f);
            damage = boss ? 20f : 12f;
        }

        if (hit)
        {
            BasicCombatScript combat = target.GetComponent<BasicCombatScript>();
            if (combat != null) combat.ApplyDamage(damage);
        }
    }

    private void CreateWarning()
    {
        if (warningTransform != null) Destroy(warningTransform.gameObject);
        GameObject warning = new GameObject(boss ? "Aviso del jefe" : "Aviso de ataque");
        warningTransform = warning.transform;
        warning.transform.position = circleAttack
            ? transform.position + Vector3.down * .12f
            : transform.position + (Vector3)(attackDirection * (boss ? 2.5f : 1.8f));
        SpriteRenderer renderer = warning.AddComponent<SpriteRenderer>();
        renderer.sprite = circleAttack ? DemoPixelArt.Circle : DemoPixelArt.Square;
        renderer.color = new Color(1f, .18f, .1f, .5f);
        renderer.sortingOrder = 38;
        if (circleAttack)
        {
            warning.transform.localScale = new Vector3(5.8f, 5.8f, 1f);
        }
        else
        {
            warning.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(attackDirection.y, attackDirection.x) * Mathf.Rad2Deg);
            warning.transform.localScale = new Vector3(boss ? 5f : 3.6f, boss ? 1.25f : .8f, 1f);
        }
    }

    private void CreateHealthBar()
    {
        GameObject back = new GameObject("Barra de vida · " + displayName);
        back.transform.SetParent(transform, false);
        back.transform.localPosition = new Vector3(0f, boss ? 1.7f : 1.25f, 0f);
        back.transform.localScale = new Vector3(boss ? 1.7f : 1.1f, .14f, 1f);
        SpriteRenderer background = back.AddComponent<SpriteRenderer>();
        background.sprite = DemoPixelArt.Square;
        background.color = new Color(.1f, .07f, .08f, .9f);
        background.sortingOrder = 41;

        GameObject fill = new GameObject("Vida restante");
        fill.transform.SetParent(back.transform, false);
        fill.transform.localPosition = new Vector3(0f, 0f, -.01f);
        fill.transform.localScale = new Vector3(.94f, .55f, 1f);
        healthFill = fill.AddComponent<SpriteRenderer>();
        healthFill.sprite = DemoPixelArt.Square;
        healthFill.color = new Color(.28f, .9f, .37f);
        healthFill.sortingOrder = 42;
        UpdateHealthBar();
    }

    private void UpdateHealthBar()
    {
        if (healthFill == null) return;
        Vector3 scale = healthFill.transform.localScale;
        scale.x = .94f * Mathf.Clamp01(health / maximumHealth);
        healthFill.transform.localScale = scale;
    }

    private void Animate()
    {
        if (spriteRenderer == null || animationFrames == null || animationFrames.Length < 2) return;
        animationTimer += Time.deltaTime;
        if (animationTimer < .22f) return;
        animationTimer = 0f;
        int index = (Time.frameCount / 13) % animationFrames.Length;
        spriteRenderer.sprite = animationFrames[index];
    }

    private void PlayExplosion()
    {
        if (spriteRenderer == null) return;
        Sprite[] explosion = Resources.LoadAll<Sprite>("Demo/Art/ElectrodeExplodes");
        if (explosion.Length > 0) StartCoroutine(ShowExplosion(explosion));
        else spriteRenderer.enabled = false;
    }

    private System.Collections.IEnumerator ShowExplosion(Sprite[] frames)
    {
        spriteRenderer.enabled = true;
        for (int i = 0; i < frames.Length; i++)
        {
            spriteRenderer.sprite = frames[i];
            yield return new WaitForSeconds(.1f);
        }
        spriteRenderer.enabled = false;
    }
}
