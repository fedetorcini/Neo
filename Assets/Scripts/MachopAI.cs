using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MachopAI : MonoBehaviour
{
    /*public LayerMask collisions;
    public float moveSpeed = 3f;
    public Transform movePoint;
    public Transform render;
    private Vector3 targetPosition;
    private Animator animator;
   
    private Path path;  //A* Path calculated
    private Seeker seeker;  //Path finding class
    private int currentWaypoint = 0; //Aux for pathing

    private void OnPathComplete(Path seekerResultPath)
    {
        if (!seekerResultPath.error){
            path = seekerResultPath;
            currentWaypoint = 1;
        }
    }

    public override void UpdateTargetPosition()
    {
        if (seeker.IsDone())  
        {
            Vector3 auxPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            targetPosition = new Vector3(Mathf.Floor(auxPos.x) +0.5f, Mathf.Floor(auxPos.y)+0.5f, transform.position.z);
            seeker.StartPath(transform.position, targetPosition, OnPathComplete);
        }
    }
    public override void UpdateIndependentTargetPosition(){}

    void Start()
    {
        movePoint.parent = null;
        targetPosition = transform.position;

        animator = render.GetComponent<Animator>(); 
        seeker = gameObject.GetComponent<Seeker>();
    }

    void Update()
    {
        if (transform.position != targetPosition)
        {
            Move();
            if (transform.position == movePoint.position)
            {
                NewMovePoint();
                Animate(0,0);
            }
        }
    }

    void Move()
    {
        transform.position = Vector3.MoveTowards(transform.position, movePoint.position, moveSpeed * Time.deltaTime);
        Animate(movePoint.position.x - transform.position.x, movePoint.position.y - transform.position.y);
    }

    private void NewMovePoint()
    {
        if (path != null && currentWaypoint < path.vectorPath.Count)
        {
            float deltaX = path.vectorPath[currentWaypoint].x - transform.position.x;
            float deltaY = path.vectorPath[currentWaypoint].y - transform.position.y;
            
            if( deltaX == 0 ||  deltaY == 0 )
            {
                if(!Physics2D.OverlapCircle(path.vectorPath[currentWaypoint], 0.4f, collisions))
                {
                    movePoint.position = path.vectorPath[currentWaypoint];
                    if (currentWaypoint < path.vectorPath.Count) currentWaypoint++;
                }
            } else
            {
                if (!Physics2D.OverlapCircle(transform.position + new Vector3(deltaX, 0, 0), .4f, collisions))
                {
                    movePoint.position = transform.position + new Vector3(deltaX, 0, 0);
                }
                else if (!Physics2D.OverlapCircle(transform.position + new Vector3(0, deltaY, 0), .4f, collisions))
                {
                    movePoint.position = transform.position + new Vector3(0, deltaY, 0);
                }   
            }
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
    }*/
}
