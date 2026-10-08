using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using UnityEngine;

[Serializable] public class RLCommand{public string command;public int seed;public float[] actions;public string path;}
[Serializable] public class RLResponse{public RLState[] states;public string error;public string unityVersion;public int observations=8,continuousActions=2,decisionPeriod=10;}
public class RLBridge : MonoBehaviour
{
    public RollerAgent[] agents;
    TcpListener listener;Thread thread;bool stopping;
    readonly ConcurrentQueue<(string request,TaskCompletionSource<string> response)> pending=new();
    void Start()
    {
        var args=Environment.GetCommandLineArgs();if(Array.IndexOf(args,"-rl-server")<0)return;
        int p=Array.IndexOf(args,"-rl-port");int port=p>=0?int.Parse(args[p+1]):9005;
        agents=FindObjectsByType<RollerAgent>(FindObjectsSortMode.None);
        Array.Sort(agents,(a,b)=>string.CompareOrdinal(a.TrainingArea.name,b.TrainingArea.name));
        foreach(var a in agents){a.externalControl=true;a.ResetEpisode();}
        Physics.simulationMode=SimulationMode.Script;Time.timeScale=0;Application.runInBackground=true;Application.targetFrameRate=-1;QualitySettings.vSyncCount=0;
        listener=new TcpListener(IPAddress.Loopback,port);listener.Start();thread=new Thread(Serve){IsBackground=true};thread.Start();Debug.Log("RL_BRIDGE_READY "+port+" arenas="+agents.Length);
    }
    void Serve()
    {
        try{while(!stopping){using(var client=listener.AcceptTcpClient()){client.NoDelay=true;using var stream=client.GetStream();using var reader=new StreamReader(stream);using var writer=new StreamWriter(stream){AutoFlush=true};string line;while(!stopping&&(line=reader.ReadLine())!=null){var done=new TaskCompletionSource<string>();pending.Enqueue((line,done));if(!done.Task.Wait(60000))break;writer.WriteLine(done.Task.Result);}}}}
        catch(Exception e){if(!stopping)Debug.LogWarning("RL bridge: "+e.Message);}
    }
    void Update()
    {
        if(!pending.TryDequeue(out var item))return;
        try
        {
            var cmd=JsonUtility.FromJson<RLCommand>(item.request);var result=new RLResponse{unityVersion=Application.unityVersion};
            if(cmd.command=="reset")
            {
                UnityEngine.Random.InitState(cmd.seed);foreach(var a in agents)a.ResetEpisode();result.states=new RLState[agents.Length];for(int i=0;i<agents.Length;i++)result.states[i]=new RLState{observation=agents[i].Observe()};
            }
            else if(cmd.command=="load_policy")
            {
                var policy=JsonUtility.FromJson<DensePolicy>(File.ReadAllText(cmd.path));foreach(var a in agents)a.policy=policy;
            }
            else if(cmd.command=="step"||cmd.command=="policy_step")
            {
                if(cmd.command=="step"&&(cmd.actions==null||cmd.actions.Length!=agents.Length*2))throw new ArgumentException("Expected two actions per arena");
                var actions=new Vector2[agents.Length];for(int i=0;i<agents.Length;i++)actions[i]=cmd.command=="policy_step"?agents[i].policy.Predict(agents[i].Observe()):new Vector2(cmd.actions[2*i],cmd.actions[2*i+1]);
                for(int tick=0;tick<10;tick++){for(int i=0;i<agents.Length;i++)agents[i].Apply(actions[i]);Physics.Simulate(.02f);}
                result.states=new RLState[agents.Length];for(int i=0;i<agents.Length;i++){var s=agents[i].FinishDecision();s.action=new[]{actions[i].x,actions[i].y};if(s.terminated||s.truncated){s.terminalObservation=s.observation;agents[i].ResetEpisode();s.observation=agents[i].Observe();}result.states[i]=s;}
            }
            else if(cmd.command=="close")Application.Quit();
            else if(cmd.command!="hello")throw new ArgumentException("Unknown command");
            item.response.TrySetResult(JsonUtility.ToJson(result));
        }
        catch(Exception e){item.response.TrySetResult(JsonUtility.ToJson(new RLResponse{error=e.Message}));}
    }
    void OnDestroy(){stopping=true;listener?.Stop();}
}
