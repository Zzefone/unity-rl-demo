# Unity RL Demo

[中文](README.md) | [English](README.en.md)

A rolling-ball and chase demo built with **Unity 6000.6.4f1**, with optional Python reinforcement learning code and the Unity MCP plugin.

![Chase scene](docs/demo.png)

## Run the demo

1. Download or clone this repository, then add its `Project` folder to Unity Hub.
2. Open it with Unity **6000.6.4f1** and wait for asset importing and script compilation to finish.
3. Open `Assets/Scenes/ChaseGame.unity`, press ▶ Play, and click **Auto chase demo** in the upper-left corner of the Game view.
4. The blue ball chases the green ball. Use **WASD or the arrow keys** to move the green ball, evade the blue ball, and collect the small cubes.

Click **Switch environment** to open the rolling-ball scene. Click **Keyboard test** to control the blue ball with the keyboard and reach the red target. The **Trained AI** button is unavailable until a trained model is present.

Running the demo does not require Python or an active MCP service.

## Project structure

| Directory | Contents |
| --- | --- |
| `Project/Assets/Scenes` | RollerBall and ChaseGame scenes |
| `Project/Assets/Scripts` | Physics controls, rule-based pursuit, UI, a custom training bridge, and JSON policy inference |
| `Project/Assets/Editor` | Scene generation, Windows builds, and MCP connection tools |
| `Project/Packages` | Unity dependencies and the embedded Unity MCP 10.0.0 plugin |
| `Project/ProjectSettings` | Unity project settings |
| `Project/Training` | Optional Stable-Baselines3 PPO training code |

Unity caches, Python environments, logs, training results, and Windows build outputs are excluded from this repository. Unity `.meta` files are included.

## Optional: connect MCP

The plugin comes from [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) and is pinned to **10.0.0**. After installing `uv`, run `Start-MCP.cmd` from the repository root to start the local service.

Service endpoint: `http://127.0.0.1:8808/mcp`. In Unity, select **RL Lab > Connect Codex MCP**, then configure your MCP client to use this endpoint. Port 8808 must be available.

The embedded plugin's original MIT license is provided in [docs/licenses/unity-mcp-MIT.txt](docs/licenses/unity-mcp-MIT.txt). This repository does not grant an additional open-source license for the remaining code.

## Optional: reinforcement learning experiments

The training code uses **Python 3.12, Stable-Baselines3 2.4.1, Gymnasium 1.0.0**, and a custom Unity TCP bridge. The original local environment was checked with PyTorch 2.3.1; the trainer runs on the CPU. Create a separate environment to avoid changing your existing Python setup.

Run the following PowerShell commands from the repository root with Python 3.12 installed:

```powershell
py -3.12 -m venv TrainingEnv
.\TrainingEnv\Scripts\python.exe -m pip install "torch==2.3.1" "numpy<2" tensorboard
.\TrainingEnv\Scripts\python.exe -m pip install -r .\Project\Training\requirements.txt
```

Build the dedicated Windows training environments before training; the demo window cannot replace these builds. Close the Unity editor for this project, then set `$unityEditor` below to your actual installation path:

```powershell
$unityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe'
$projectPath = (Resolve-Path .\Project).Path
& $unityEditor -batchmode -quit -projectPath $projectPath -executeMethod LabSetup.BuildTraining -labScene roller -logFile "$projectPath\build-roller.log"
& $unityEditor -batchmode -quit -projectPath $projectPath -executeMethod LabSetup.BuildTraining -labScene chase -logFile "$projectPath\build-chase.log"
```

After confirming that the build result in each log is `Succeeded`, run one of these experiments:

```powershell
.\TrainingEnv\Scripts\python.exe .\Project\Training\train.py --scene roller --steps 100000
# Or:
.\TrainingEnv\Scripts\python.exe .\Project\Training\train.py --scene chase --steps 500000
```

Each training environment contains 16 independent arenas, with 8 observation values and 2 continuous action values that apply force in the horizontal plane. Reaching the target gives a reward of +1; falling or timing out gives 0. Each action advances 10 physics frames. Evaluation reports comparing the policies before and after training are saved to `Project/results`. Training saves the PPO model and JSON weights for C# inference; **no ONNX model is generated**.

The trainer starts a local TCP connection on port 9005 by default. The training code is provided for future experiments and has not completed a full training run, so it does not establish that an effective policy has been learned.

## Verification status

- Unity script compilation and Windows builds for both training environments and the demo succeeded.
- The rule-based chase demo runs, and the Python dependencies import successfully.
- Unity MCP connected successfully in the original local setup.
- Full PPO training performance has not been verified. Dependencies must be installed and builds regenerated on other machines.
