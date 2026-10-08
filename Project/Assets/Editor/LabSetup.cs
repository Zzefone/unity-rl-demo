using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;




using MCPForUnity.Editor.Services;

public static class LabSetup
{
    static string[] scenes={"Assets/Scenes/RollerBall.unity","Assets/Scenes/ChaseGame.unity"};
    static Material Material(string name, Color color)
    {
        string p="Assets/Materials/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(p);
        if(!m){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,p);}
        m.color=color;return m;
    }
    static GameObject Shape(string name, PrimitiveType type,Transform parent,Vector3 position,Vector3 scale,Material material)
    {
        var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=position;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=material;return g;
    }
    static void Tags()
    {
        var asset=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var tags=asset.FindProperty("tags");
        foreach(string name in new[]{"floor","PickUp"})
        {bool exists=false;for(int i=0;i<tags.arraySize;i++)exists|=tags.GetArrayElementAtIndex(i).stringValue==name;if(!exists){tags.InsertArrayElementAtIndex(tags.arraySize);tags.GetArrayElementAtIndex(tags.arraySize-1).stringValue=name;}}
        asset.ApplyModifiedProperties();
    }
    [MenuItem("RL Lab/Create tutorial scenes")]
    public static void CreateScenes()
    {
        Directory.CreateDirectory("Assets/Scenes");Directory.CreateDirectory("Assets/Materials");Directory.CreateDirectory("Assets/Models");AssetDatabase.Refresh();Tags();
        var floor=Material("Floor",new Color(.10f,.15f,.22f));
        var wall=Material("Walls",new Color(.80f,.86f,.91f));
        var blue=Material("Agent",new Color(.08f,.4f,.95f));
        var red=Material("Target",new Color(.97f,.20f,.22f));
        var cyan=Material("Player",new Color(.10f,.85f,.65f));
        var gold=Material("Pickups",new Color(1,.76f,.08f));
        for(int index=0;index<2;index++)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var area=new GameObject("TrainingArea").transform;
            var ground=Shape("Floor",PrimitiveType.Plane,area,Vector3.zero,Vector3.one,floor);ground.tag="floor";
            var walls=new GameObject("Walls").transform;walls.SetParent(area,false);
            Shape("North",PrimitiveType.Cube,walls,new Vector3(0,.5f,5),new Vector3(10.5f,1,.4f),wall);
            Shape("South",PrimitiveType.Cube,walls,new Vector3(0,.5f,-5),new Vector3(10.5f,1,.4f),wall);
            Shape("East",PrimitiveType.Cube,walls,new Vector3(5,.5f,0),new Vector3(.4f,1,10.5f),wall);
            Shape("West",PrimitiveType.Cube,walls,new Vector3(-5,.5f,0),new Vector3(.4f,1,10.5f),wall);
            var target=Shape("Target",index==0?PrimitiveType.Cube:PrimitiveType.Sphere,area,new Vector3(3,.5f,3),Vector3.one,index==0?red:cyan);
            var ball=Shape("Agent",PrimitiveType.Sphere,area,new Vector3(0,.5f,0),Vector3.one,blue);
            var rb=ball.AddComponent<Rigidbody>();rb.mass=1;rb.linearDamping=.5f;rb.angularDamping=.05f;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            
            var agent=ball.AddComponent<RollerAgent>();agent.Target=target.transform;agent.TrainingArea=area;agent.MaxStep=100;
            
            if(index==1)
            {
                var trb=target.AddComponent<Rigidbody>();trb.linearDamping=.7f;trb.constraints=RigidbodyConstraints.FreezeRotation;
                target.AddComponent<PlayerController>().chaser=agent;
                var pickups=new GameObject("PickUps").transform;pickups.SetParent(area,false);
                for(int i=0;i<8;i++)
                {
                    float angle=i*Mathf.PI/4;
                    var p=Shape("PickUp_"+i,PrimitiveType.Cube,pickups,new Vector3(3.6f*Mathf.Cos(angle),.7f,3.6f*Mathf.Sin(angle)),Vector3.one*.35f,i==0?red:gold);
                    p.tag="PickUp";p.GetComponent<Collider>().isTrigger=true;var prb=p.AddComponent<Rigidbody>();prb.isKinematic=true;prb.useGravity=false;p.AddComponent<PickupMotion>();
                }
            }
            var cam=new GameObject("Main Camera");cam.tag="MainCamera";var camera=cam.AddComponent<Camera>();camera.fieldOfView=48;camera.backgroundColor=new Color(.04f,.07f,.11f);camera.clearFlags=CameraClearFlags.SolidColor;cam.AddComponent<AudioListener>();cam.transform.position=new Vector3(0,11,-10);cam.transform.LookAt(Vector3.zero);
            var follow=cam.AddComponent<CameraController>();follow.Player=index==0?ball.transform:target.transform;follow.follow=index==1;
            var light=new GameObject("Directional Light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(50,-30,0);RenderSettings.ambientLight=new Color(.45f,.49f,.55f);
            new GameObject("Python Training Bridge").AddComponent<RLBridge>();
            var controls=new GameObject("Lab Controls").AddComponent<LabControls>();controls.agent=agent;controls.title=index==0?"RollerBall - reach the red target":"Chase Game - collect and evade";
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),scenes[index]);
        }
        PlayerSettings.companyName="Personal RL Lab";PlayerSettings.productName="RollerBall RL";
        PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;
        var player=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);var input=player.FindProperty("activeInputHandler");if(input!=null){input.intValue=2;player.ApplyModifiedProperties();}
        EditorBuildSettings.scenes=scenes.Select(s=>new EditorBuildSettingsScene(s,true)).ToArray();
        AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(scenes[0]);
        File.WriteAllText("setup-complete.json","{\"unity\":\""+Application.unityVersion+"\",\"scenes\":2,\"observations\":8,\"continuous_actions\":2,\"decision_period\":10}");
        Debug.Log("RL_LAB_SETUP_COMPLETE");
    }
    [MenuItem("RL Lab/Connect Codex MCP")]
    public static async void Connect()
    {
        var config=EditorConfigurationCache.Instance;config.SetUseHttpTransport(true);config.SetHttpTransportScope("local");config.SetHttpBaseUrl("http://127.0.0.1:8808");
        EditorPrefs.SetBool("MCPForUnity.SetupCompleted",true);EditorPrefs.SetBool("MCPForUnity.AutoStartOnLoad",true);
        bool connected=await MCPServiceLocator.Bridge.StartAsync();Debug.Log("RL_LAB_MCP_CONNECTED="+connected);
    }
    public static void SetupAndConnect(){if(!File.Exists(scenes[0]))CreateScenes();Connect();}
    public static void BuildTraining()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-labScene");int index=i>=0&&args[i+1]=="chase"?1:0;
        EditorSceneManager.OpenScene(scenes[index]);
        var agent=UnityEngine.Object.FindFirstObjectByType<RollerAgent>();agent.inference=false;
        // Parallel independent arenas retain local-coordinate observations and accelerate PPO collection.
        var original=GameObject.Find("TrainingArea");for(int k=1;k<16;k++){var clone=UnityEngine.Object.Instantiate(original);clone.name="TrainingArea_"+k;clone.transform.position=new Vector3((k%4)*14,0,(k/4)*14);}
        string temporary="Assets/Scenes/TrainingBuild.unity";EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),temporary);
        string name=index==0?"RollerBall":"ChaseAgent";string target="Builds/Training/"+name+"/"+name+".exe";
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        var report=BuildPipeline.BuildPlayer(new[]{temporary},target,BuildTarget.StandaloneWindows64,BuildOptions.None);
        EditorSceneManager.OpenScene(scenes[index]);AssetDatabase.DeleteAsset(temporary);
        Debug.Log("RL_LAB_TRAINING_BUILD="+report.summary.result);if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Training build failed");
    }

    [MenuItem("RL Lab/Attach trained models")]
    public static void AttachModels()
    {
        AssetDatabase.Refresh();
        for(int i=0;i<2;i++)
        {
            EditorSceneManager.OpenScene(scenes[i]);var a=UnityEngine.Object.FindFirstObjectByType<RollerAgent>();
            var model=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Models/"+(i==0?"RollerBall":"ChaseAgent")+".json");
            if(!model)throw new Exception("Trained model missing");a.policyAsset=model;a.inference=true;
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }
        EditorSceneManager.OpenScene(scenes[0]);AssetDatabase.SaveAssets();
    }
    [MenuItem("RL Lab/Build Windows game")]
    public static void BuildGame()
    {
        AttachModels();Directory.CreateDirectory("Builds/Game");
        var report=BuildPipeline.BuildPlayer(scenes,"Builds/Game/RollerBallRL.exe",BuildTarget.StandaloneWindows64,BuildOptions.None);
        Debug.Log("RL_LAB_GAME_BUILD="+report.summary.result);if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Game build failed");
    }

    public static void BuildDemo()
    {
        Directory.CreateDirectory("Builds/Game");
        var report=BuildPipeline.BuildPlayer(scenes,"Builds/Game/RollerBallRL.exe",BuildTarget.StandaloneWindows64,BuildOptions.None);
        Debug.Log("RL_LAB_GAME_BUILD="+report.summary.result);
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Game build failed");
    }
    public static void PreviewAndConnect()
    {
        Directory.CreateDirectory("../verification");
        foreach(var scene in scenes)
        {
            EditorSceneManager.OpenScene(scene);
            var camera=Camera.main;
            var target=new RenderTexture(1280,720,24);camera.targetTexture=target;camera.Render();
            var previous=RenderTexture.active;RenderTexture.active=target;
            var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
            File.WriteAllBytes("../verification/"+Path.GetFileNameWithoutExtension(scene)+".png",image.EncodeToPNG());
            camera.targetTexture=null;RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(target);
        }
        EditorSceneManager.OpenScene(scenes[0]);Connect();
    }

    public static void ShowDemo()
    {
        PreviewAndConnect();
        EditorSceneManager.OpenScene(scenes[1]);
        EditorApplication.isPlaying=true;
        EditorApplication.delayCall += ()=>{var a=UnityEngine.Object.FindAnyObjectByType<RollerAgent>();if(a)a.demoController=true;};
    }
}
