using UnityEngine;

/// <summary>Optional visual response to existing combat; does not change movement or damage.</summary>
[RequireComponent(typeof(DemoPartnerCombat))]
public sealed class NeoDirectionalSpriteAction : MonoBehaviour
{
    public float duration = .5f;
    DemoPartnerCombat combat;
    Animator animator;
    Transform visual;
    float until;
    bool playing;
    float side;
    static readonly int Busy = Animator.StringToHash("ActionPlaying");

    void Awake()
    {
        combat = GetComponent<DemoPartnerCombat>();
        visual = GetComponent<NeoGridMotor>().visual;
        animator = visual.GetComponent<Animator>();
    }
    void OnEnable() { combat.AttackPerformed += Play; }
    void OnDisable()
    {
        combat.AttackPerformed -= Play;
        if (animator != null) animator.SetBool(Busy, false);
        playing = false;
    }
    void Play(Vector2 direction)
    {
        bool horizontal = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y);
        string pose = horizontal ? "Side" : direction.y > 0 ? "Back" : "Front";
        side = horizontal ? (direction.x > 0 ? -1 : 1) : visual.localScale.x;
        animator.SetBool(Busy, true); animator.SetTrigger("Action" + pose);
        until = Time.time + duration; playing = true;
    }
    void LateUpdate()
    {
        if (!playing) return;
        if (Time.time >= until) { animator.SetBool(Busy, false); playing = false; return; }
        var scale = visual.localScale; scale.x = Mathf.Abs(scale.x) * Mathf.Sign(side); visual.localScale = scale;
    }
}
