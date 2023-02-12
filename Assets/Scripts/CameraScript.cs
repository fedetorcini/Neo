using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraScript : MonoBehaviour
{
    public Transform target;
    public float followSpeed = 5.0f;
   
    // Update is called once per frame
    void Update()
    {
        if (target != null) 
        {
            transform.position = Vector3.MoveTowards(transform.position, new Vector3(target.position.x, target.position.y, transform.position.z), followSpeed * Time.deltaTime);
        }       
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
