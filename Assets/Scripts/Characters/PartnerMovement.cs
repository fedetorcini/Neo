using System.Collections;
using System.Collections.Generic;
using Pathfinding;
using UnityEngine;

public class PartnerMovement : MonoBehaviour
{
    public LayerMask collisions;
    public float moveSpeed = 5f;
    private float stopDistance = 2.0f;
    public Transform movePoint;
    public Transform render;
    private Vector3 targetPosition;
    private Animator animator;
   
    private Path path;  //A* Path calculated
    private Seeker seeker;  //Path finding class
    private int currentWaypoint = 0; //Aux for pathing

    public void SetTarget(Vector3 newTarget, int hoverDistance = 0)
    {
        targetPosition = new Vector3(Mathf.Floor(newTarget.x), RoundToPointFive(newTarget.y), transform.position.z);
        stopDistance = hoverDistance;
    }

    private void UpdatePath()
    {
        if (seeker.IsDone() && Vector3.Distance(transform.position, targetPosition) > stopDistance)  
        {
            seeker.StartPath(transform.position, targetPosition, OnPathComplete);
        }
    }

    private void OnPathComplete(Path seekerResultPath)
    {
        if (!seekerResultPath.error){
            path = seekerResultPath;
            currentWaypoint = 1;
        }
    }

    void Start()
    {
        movePoint.parent = null;
        targetPosition = transform.position;

        animator = render.GetComponent<Animator>(); 
        seeker = gameObject.GetComponent<Seeker>();
    }

    void Update()
    {
        UpdatePath();
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
            float deltaX = RoundToPointFive(path.vectorPath[currentWaypoint].x) - transform.position.x;
            float deltaY = RoundToPointFive(path.vectorPath[currentWaypoint].y) - transform.position.y;

            if( deltaX == 0 ||  deltaY == 0 )
            {
                if(!Physics2D.OverlapCircle(path.vectorPath[currentWaypoint], 0.4f, collisions))
                {
                    movePoint.position = new Vector3(RoundToPointFive(path.vectorPath[currentWaypoint].x), RoundToPointFive(path.vectorPath[currentWaypoint].y), transform.position.z);
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
    }

    float RoundToPointFive(float number)
    {
        return Mathf.Floor(number) + .5f;
    }

}
