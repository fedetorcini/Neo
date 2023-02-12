using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BasicRenderScript : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;

    // Start is called before the first frame update
    void Awake(){
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    // Update is called once per frame
    void Update(){spriteRenderer.sortingOrder = -Mathf.RoundToInt(transform.position.y * 10);}
}
