using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{

    public float moveSpeed = 5f;
    public Transform movePoint;
    public LayerMask collisions;
    public GameObject Call;
    
    private Animator Animator;
    private Transform current;
    private Vector3 targetPosition;
    private int life;
    private bool locked;

    void Start(){
        movePoint.parent = null;    //Releases Movepoint
        current = transform.GetChild(0);    //Sets current to state's transform
        Animator = current.GetComponent<Animator>();    //Sets Animator to state's animator
        targetPosition = transform.position;    //Set target position to player position
        life = 5;
        locked = false;
    }

    void Update(){

        if (Vector3.Distance(transform.position, targetPosition) == 0f){
            NewTargetPosition();
            Animate(0, 0);
        }
        else {
            float signoX = 0;
            float signoY = 0;
            if (targetPosition.x - transform.position.x != 0f) signoX = Mathf.Sign(targetPosition.x - transform.position.x);
            if (targetPosition.y - transform.position.y != 0f) signoY = Mathf.Sign(targetPosition.y - transform.position.y);
            movePoint.position = targetPosition;
            transform.position = Vector3.MoveTowards(transform.position, movePoint.position, moveSpeed * Time.deltaTime);
            Animate(signoX, signoY);
        }
    }
  
    private void NewTargetPosition(){
        Vector3 aux;
        if (!locked){        
            if (Input.GetAxisRaw("Horizontal") != 0f){
                aux = transform.position + new Vector3(Input.GetAxisRaw("Horizontal"), 0, 0);

                if (!Physics2D.OverlapCircle(aux, .4f, collisions)){
                    targetPosition = aux;
                } 
                
            }
            else if (Input.GetAxisRaw("Vertical") != 0f){
                aux = transform.position + new Vector3(0, Input.GetAxisRaw("Vertical"), 0);

                if (!Physics2D.OverlapCircle(aux, .4f, collisions)){
                    targetPosition = aux;
                } 
            }
        }
    }

    private void Animate(float x, float y){
        if (x > 0){
            Animator.SetBool("RunHorizontal", true);
            current.localScale = new Vector3(-1.0f, 1.0f, 1.0f);
        } else if (y > 0){
            Animator.SetBool("RunUp", true);
        } else if (x < 0){
            Animator.SetBool("RunHorizontal", true);
            current.localScale = new Vector3(1.0f, 1.0f, 1.0f);
        } else if (y < 0){
            Animator.SetBool("RunDown", true);
        } else if (x == 0 && y == 0){
            Animator.SetBool("RunDown", false);
            Animator.SetBool("RunUp", false);
            Animator.SetBool("RunHorizontal", false);
        }
    }
    public Vector3 GetPosition(){return transform.position;}

    public void Transform(GameObject type){

        Destroy(current.gameObject); //Destroy current state

        type = Instantiate(type, transform.position, Quaternion.identity); //Instatiate new state, and set it to the player
        current = type.GetComponent<Transform>();   
        current.parent = transform; 
        Animator = current.GetComponent<Animator>(); //Set animator to state

        Instantiate(Call, current.position + new Vector3(0,0,-1f), Quaternion.identity);//Animate
    }

    public void Hit(int hitpoints){
        life -= hitpoints;
        if (life <= 0){
            Debug.Log("OH SHIT");
        }
    }

    public void Lock(){ locked = true; }
}
