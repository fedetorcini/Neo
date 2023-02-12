using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LookPointScript : MonoBehaviour
{
    public Transform master;
    

    void Update(){
        Vector3 aux;
        if (Input.GetAxisRaw("Horizontal") != 0f){
            aux = master.position + new Vector3(Input.GetAxisRaw("Horizontal"), 0, 0);
            transform.position = aux;  
        }
        else if (Input.GetAxisRaw("Vertical") != 0f){
            aux = master.position + new Vector3(0, Input.GetAxisRaw("Vertical"), 0);
            transform.position = aux;
        }
    }
}
