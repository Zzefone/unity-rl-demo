using UnityEngine;
using UnityEngine.InputSystem;
public class RollerAgent : MonoBehaviour
{
    public Transform Target;
    public Transform TrainingArea;
    public float forceMultiplier=10;
    public bool inference;
    public bool demoController;
    public bool externalControl;
    public TextAsset policyAsset;
    public int successes,failures;
    public int MaxStep=100;
    public DensePolicy policy;
    public Rigidbody body;
    public int steps;
    public float episodeReturn;
    int decisionTicks;
    Vector2 currentAction;
    void Awake(){body=GetComponent<Rigidbody>();if(policyAsset)policy=JsonUtility.FromJson<DensePolicy>(policyAsset.text);}
    void Start(){if(!externalControl)ResetEpisode();}
    public void ResetEpisode()
    {
        body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;
        transform.localPosition=new Vector3(0,.5f,0);transform.localRotation=Quaternion.identity;
        Vector3 p;do{p=new Vector3(Random.Range(-3.6f,3.6f),.5f,Random.Range(-3.6f,3.6f));}while(new Vector2(p.x,p.z).magnitude<2);
        Target.localPosition=p;var b=Target.GetComponent<Rigidbody>();if(b){b.linearVelocity=Vector3.zero;b.angularVelocity=Vector3.zero;}
        steps=0;episodeReturn=0;decisionTicks=0;Physics.SyncTransforms();
    }
    public float[] Observe()
    {
        Vector3 t=Target.localPosition,p=transform.localPosition,v=body.linearVelocity;
        return new[]{t.x/5,t.y/5,t.z/5,p.x/5,p.y/5,p.z/5,v.x/5,v.z/5};
    }
    public void Apply(Vector2 a){body.AddForce(new Vector3(Mathf.Clamp(a.x,-1,1),0,Mathf.Clamp(a.y,-1,1))*forceMultiplier);}
    public RLState FinishDecision()
    {
        steps++;bool success=Vector3.Distance(transform.localPosition,Target.localPosition)<1.42f;
        bool fell=transform.localPosition.y<0;bool timeout=steps>=MaxStep;
        float reward=success?1:0;episodeReturn+=reward;
        var result=new RLState{observation=Observe(),reward=reward,terminated=success||fell,truncated=timeout&&!success&&!fell,success=success,episodeReturn=episodeReturn,episodeLength=steps};
        if(success)successes++;else if(fell||timeout)failures++;
        return result;
    }
    public static Vector2 KeyboardAction()
    {
        var k=Keyboard.current;if(k==null)return Vector2.zero;
        return new Vector2((k.dKey.isPressed||k.rightArrowKey.isPressed?1:0)-(k.aKey.isPressed||k.leftArrowKey.isPressed?1:0),(k.wKey.isPressed||k.upArrowKey.isPressed?1:0)-(k.sKey.isPressed||k.downArrowKey.isPressed?1:0));
    }
    void FixedUpdate()
    {
        if(externalControl)return;
        if(decisionTicks==0)
        {
            if(inference&&policy!=null)currentAction=policy.Predict(Observe());
            else if(demoController){var o=Observe();currentAction=new Vector2(Mathf.Clamp(3*(o[0]-o[3])-1.2f*o[6],-1,1),Mathf.Clamp(3*(o[2]-o[5])-1.2f*o[7],-1,1));}
            else currentAction=KeyboardAction();
        }
        Apply(currentAction);decisionTicks++;
        if(decisionTicks==10){decisionTicks=0;var result=FinishDecision();if(result.terminated||result.truncated)ResetEpisode();}
    }
}
[System.Serializable]
public class RLState
{
    public float[] observation,terminalObservation,action;
    public float reward,episodeReturn;
    public int episodeLength;
    public bool terminated,truncated,success;
}
