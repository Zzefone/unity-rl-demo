using UnityEngine;
public class PickupMotion : MonoBehaviour
{
    Vector3 origin;
    void Start(){origin=transform.localPosition;}
    void Update(){transform.Rotate(15*Time.deltaTime,35*Time.deltaTime,0);transform.localPosition=origin+Vector3.up*(.15f*Mathf.Sin(Time.time*2f));}
}
