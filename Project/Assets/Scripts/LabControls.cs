using UnityEngine;
using UnityEngine.SceneManagement;
public class LabControls : MonoBehaviour
{
    public RollerAgent agent;public string title;
    void OnGUI()
    {
        if(Application.isBatchMode||agent.externalControl)return;
        GUILayout.BeginArea(new Rect(18,18,320,240),GUI.skin.box);
        GUILayout.Label(title);GUILayout.Label(agent.inference?"Trained AI":agent.demoController?"Rule-based demo (not trained)":"Keyboard test");GUILayout.Label("WASD / arrows: move the ball");GUILayout.Label("Targets reached: "+agent.successes);
        if(GUILayout.Button("Keyboard test")){agent.inference=false;agent.demoController=false;agent.ResetEpisode();}
        if(GUILayout.Button("Auto chase demo")){agent.inference=false;agent.demoController=true;agent.ResetEpisode();}
        GUI.enabled=agent.policy!=null;if(GUILayout.Button("Play with trained AI")){agent.inference=true;agent.ResetEpisode();}GUI.enabled=true;
        if(GUILayout.Button("Reset scene"))SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        if(GUILayout.Button("Switch environment"))SceneManager.LoadScene(1-SceneManager.GetActiveScene().buildIndex);
        GUILayout.EndArea();
    }
}
