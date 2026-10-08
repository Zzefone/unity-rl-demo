using UnityEngine;
public class CameraController : MonoBehaviour
{
    public Transform Player;
    public Vector3 offset=new Vector3(0,11,-10);
    public bool follow=true;
    void LateUpdate(){if(!Player)return; var center=follow?Player.position:Vector3.zero;transform.position=Vector3.Lerp(transform.position,center+offset,Time.deltaTime*5);transform.LookAt(center);}
}
