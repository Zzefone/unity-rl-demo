using UnityEngine;
public class PlayerController : MonoBehaviour
{
    public RollerAgent chaser;
    public float speed=8;
    public int collected;
    Rigidbody body;
    void Awake(){body=GetComponent<Rigidbody>();}
    void FixedUpdate(){if((!chaser.inference&&!chaser.demoController)||chaser.externalControl)return;var a=RollerAgent.KeyboardAction();body.AddForce(new Vector3(a.x,0,a.y)*speed);}
    void OnTriggerEnter(Collider other){if(other.CompareTag("PickUp")){other.gameObject.SetActive(false);collected++;}}
}
