using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BasicCombatScript : MonoBehaviour
{
    public float MAX_HEALTH = 100;
    private float curHealth; 
    
    // Start is called before the first frame update
    void Awake()
    { 
        curHealth = MAX_HEALTH;
    }

    void Update(){
        if (curHealth <= 0)Terminate();
    }

    public virtual void ApplyDamage(float damage)
    {
        curHealth -= damage;
    }

    public Vector3 GetPosition()
    {
        return transform.position;
    }

    public void Terminate(){Destroy(gameObject);}
}
