using System.Collections;
using System.Collections.Generic;
using Pathfinding;
using UnityEngine;

public class Controller : MonoBehaviour
{
    public CameraScript mainCamera;
    public ProceduralGridMover AIPathfinder;
    public Transform marker;

    public List<Controllable> controllablesManual; 
    private Queue<Controllable> controllables;
    private Controllable active;


    private static int MOUSE_MOVE_BUTTON_ID = 1;

    void Awake()
    {
        controllables = new Queue<Controllable>(controllablesManual);
        active = controllables.Dequeue(); 
    }

    void Update()
    {
        marker.position = active.GetComponent<Transform>().position;
        if(Input.GetKeyDown("space"))
        {
            active.Release();
            controllables.Enqueue(active);
            
            active = controllables.Dequeue();
            AIPathfinder.target = active.GetComponent<Transform>();
            mainCamera.SetTarget(active.GetComponent<Transform>());
        }
        if(Input.GetMouseButtonDown(MOUSE_MOVE_BUTTON_ID))
        {
            active.SetTarget(Camera.main.ScreenToWorldPoint(Input.mousePosition));
        }
          
    }
}
