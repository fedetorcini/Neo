using System.Collections;
using System.Collections.Generic;
using Pathfinding;
using UnityEngine;

public class PartnerMovement : Teleportable
{
    private NeoGridMotor gridMotor;
    void Awake() { gridMotor = GetComponent<NeoGridMotor>(); }
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
    private bool manualStep;
    private bool pathFailed;

    public void SetTarget(Vector3 newTarget, int hoverDistance = 0)
    {
        if (gridMotor != null) { gridMotor.Follow(newTarget, hoverDistance); return; }
        targetPosition = new Vector3(RoundToPointFive(newTarget.x), RoundToPointFive(newTarget.y), transform.position.z);
        stopDistance = hoverDistance;
    }

    private void UpdatePath()
    {
        if (!pathFailed && seeker != null && seeker.IsDone() && Vector3.Distance(transform.position, targetPosition) > stopDistance)
        {
            seeker.StartPath(transform.position, targetPosition, OnPathComplete);
        }
    }

    private void OnPathComplete(Path seekerResultPath)
    {
        if (!seekerResultPath.error){
            path = seekerResultPath;
            currentWaypoint = 1;
            pathFailed = false;
        } else {
            path = null;
            pathFailed = true;
        }
    }

    void Start()
    {
        if (gridMotor != null) return;
        movePoint.parent = null;
        targetPosition = transform.position;

        animator = render.GetComponent<Animator>(); 
        seeker = gameObject.GetComponent<Seeker>();
    }

    void Update()
    {
        if (gridMotor != null) return;
        if (manualStep)
        {
            transform.position = Vector3.MoveTowards(transform.position, movePoint.position, moveSpeed * Time.deltaTime);
            Animate(movePoint.position.x - transform.position.x, movePoint.position.y - transform.position.y);
            if (transform.position == movePoint.position)
            {
                targetPosition = transform.position;
                manualStep = false;
                Animate(0, 0);
            }
            return;
        }

        if (Vector2.Distance(transform.position, targetPosition) <= stopDistance)
        {
            movePoint.position = transform.position;
            Animate(0, 0);
            return;
        }

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
        if (animator == null) return;

        bool horizontal = Mathf.Abs(x) >= Mathf.Abs(y);
        bool movingHorizontal = horizontal && Mathf.Abs(x) > .001f;
        bool movingVertical = !horizontal && Mathf.Abs(y) > .001f;
        animator.SetBool("RunHorizontal", movingHorizontal);
        animator.SetBool("RunUp", movingVertical && y > 0f);
        animator.SetBool("RunDown", movingVertical && y < 0f);

        if (movingHorizontal)
            render.localScale = new Vector3(x > 0f ? -1f : 1f, 1f, 1f);
    }

    public Vector3 GetPosition()
    {
        return transform.position;
    }

    float RoundToPointFive(float number)
    {
        return Mathf.Floor(number) + .5f;
    }

    public bool MoveManual(Vector2 direction)
    {
        if (gridMotor != null) return gridMotor.Step(direction);
        if (manualStep || direction.sqrMagnitude < .5f) return false;
        direction = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y)
            ? new Vector2(Mathf.Sign(direction.x), 0f)
            : new Vector2(0f, Mathf.Sign(direction.y));
        Vector3 destination = transform.position + new Vector3(direction.x, direction.y, 0f);
        if (Physics2D.OverlapCircle(destination, .32f, collisions) != null) return false;
        destination.x = RoundToPointFive(destination.x);
        destination.y = RoundToPointFive(destination.y);
        targetPosition = destination;
        movePoint.position = destination;
        manualStep = true;
        path = null;
        return true;
    }

    public void UseLocalFallback()
    {
        path = null;
        pathFailed = false;
    }

    private static Vector2Int ToCell(Vector3 position)
    {
        return new Vector2Int(Mathf.RoundToInt(position.x - .5f), Mathf.RoundToInt(position.y - .5f));
    }

    private static Vector3 CellCenter(Vector2Int cell)
    {
        return new Vector3(cell.x + .5f, cell.y + .5f, 0f);
    }

    public override void Teleport(Vector3 destiny)
    {
        if (gridMotor != null) { gridMotor.Teleport(destiny); return; }
        manualStep = false;
        path = null;
        transform.position = new Vector3(destiny.x, destiny.y, transform.position.z);
        movePoint.position = new Vector3(destiny.x, destiny.y, transform.position.z);
        targetPosition = new Vector3(destiny.x, destiny.y, transform.position.z);
    }
}
