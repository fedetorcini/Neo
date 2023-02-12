using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BasicMovementScript : MonoBehaviour
{
    public LayerMask collisions;
    public float moveSpeed = 5f;
    public Transform movePoint;
    public Transform render;
    private Animator animator;
   
    void Start()
    {
        movePoint.position = transform.position;
        movePoint.parent = null;

        animator = render.GetComponent<Animator>(); 
    }

    void Update()
    {
        if (transform.position != movePoint.position)
        {
            Move();
        }
        else
        {
            NewMovePoint();
            Animate(0,0);
        }
    }

    void Move()
    {
        transform.position = Vector3.MoveTowards(transform.position, movePoint.position, moveSpeed * Time.deltaTime);
        Animate(movePoint.position.x - transform.position.x, movePoint.position.y - transform.position.y);
    }

    private void NewMovePoint(){
        int deltaX = Random.Range(-1000,1000);
        int deltaY = Random.Range(-1000,1000);
        
        Vector3 aux;

        if (deltaX >= -1 && deltaX <= 1)
            aux =  new Vector3(deltaX + Mathf.FloorToInt(transform.position.x) + 0.5f, Mathf.FloorToInt(transform.position.y) + 0.5f, 0);

        else if (deltaY >= -1 && deltaY <= 1)
            aux =  new Vector3(Mathf.FloorToInt(transform.position.x) + 0.5f, deltaY + Mathf.FloorToInt(transform.position.y) + 0.5f, 0);    

        else return;

        if(!Physics2D.OverlapCircle(aux, 0.4f, collisions))
        {
            movePoint.position = aux;
        }
    }

    private void Animate(float x, float y)
    {
        if (x > 0){
            animator.SetBool("RunHorizontal", true);
            transform.localScale = new Vector3(-1.0f, 1.0f, 1.0f);
        } else if (y > 0){
            animator.SetBool("RunUp", true);
        } else if (x < 0){
            animator.SetBool("RunHorizontal", true);
            transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
        } else if (y < 0){
            animator.SetBool("RunDown", true);
        } else if (x == 0 && y == 0){
            animator.SetBool("RunDown", false);
            animator.SetBool("RunUp", false);
            animator.SetBool("RunHorizontal", false);
        }
    }

    public Vector3 GetPosition()
    {
        return transform.position;
    }
    
}
