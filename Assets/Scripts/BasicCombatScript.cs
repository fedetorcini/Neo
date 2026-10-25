using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BasicCombatScript : MonoBehaviour, IDemoDamageable
{
    public float MAX_HEALTH = 100;
    private float curHealth; 
    private bool defeatReported;

    public float CurrentHealth => curHealth;
    public float MaximumHealth => MAX_HEALTH;
    
    // Start is called before the first frame update
    void Awake()
    { 
        curHealth = MAX_HEALTH;
    }

    void Update(){
        if (curHealth <= 0 && !defeatReported)
        {
            defeatReported = true;
            if (GetComponent<PartnerAI>() != null)
            {
                DemoBootstrap flow = FindAnyObjectByType<DemoBootstrap>();
                if (flow != null) flow.ShowDefeat();
            }
            else Terminate();
        }
    }

    public virtual void ApplyDamage(float damage)
    {
        curHealth = Mathf.Max(0f, curHealth - Mathf.Max(0f, damage));
    }

    public void TakeDamage(float damage)
    {
        ApplyDamage(damage);
    }

    public void RestoreHealth()
    {
        curHealth = MAX_HEALTH;
        defeatReported = false;
    }

    public Vector3 GetPosition()
    {
        return transform.position;
    }

    public void Terminate(){Destroy(gameObject);}
}
