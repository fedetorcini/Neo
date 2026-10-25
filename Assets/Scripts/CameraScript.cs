using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraScript : MonoBehaviour
{
    public Transform target;
    public bool clampToLevel;
    public Vector2 levelMin;
    public Vector2 levelMax;

    void Start()
    {
        // A nested camera prefab can follow its actor without a scene reference.
        if (target == null && transform.parent != null)
            target = transform.parent;
    }
   
    // Update is called once per frame
    void LateUpdate()
    {
        if (target != null) 
        {
            Vector3 destination = new Vector3(target.position.x, target.position.y, transform.position.z);
            if (clampToLevel)
            {
                Camera camera = GetComponent<Camera>();
                float halfY = camera.orthographicSize, halfX = halfY * camera.aspect;
                destination.x = levelMax.x - levelMin.x <= halfX * 2 ? (levelMin.x + levelMax.x) / 2 : Mathf.Clamp(destination.x, levelMin.x + halfX, levelMax.x - halfX);
                destination.y = levelMax.y - levelMin.y <= halfY * 2 ? (levelMin.y + levelMax.y) / 2 : Mathf.Clamp(destination.y, levelMin.y + halfY, levelMax.y - halfY);
            }
            // Follow after the motor moves, without a speed cap that leaves the camera behind.
            transform.position = destination;
        }       
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
