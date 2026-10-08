import argparse,json,sys,time
from pathlib import Path
import numpy as np
import torch
from stable_baselines3 import PPO
from stable_baselines3.common.env_checker import check_env
from stable_baselines3.common.callbacks import BaseCallback
from unity_env import UnityConnection,UnityGymEnv,UnityVecEnv

ROOT=Path(__file__).resolve().parents[1]
class Progress(BaseCallback):
    def __init__(self,path):super().__init__();self.path=path;self.started=time.time()
    def _on_step(self):
        if self.num_timesteps%10000==0:
            self.path.write_text(json.dumps({'steps':self.num_timesteps,'elapsed_seconds':time.time()-self.started}),encoding='utf-8')
        return True

class ExportPolicy(torch.nn.Module):
    def __init__(self,policy):super().__init__();self.policy=policy
    def forward(self,observation):
        latent=self.policy.mlp_extractor.forward_actor(self.policy.extract_features(observation))
        return torch.clamp(self.policy.action_net(latent),-1,1)

def export(model,name):
    path=ROOT/'Assets/Models';path.mkdir(parents=True,exist_ok=True)
    model.policy.to('cpu');wrapper=ExportPolicy(model.policy).eval()
    layers=[]
    for layer in [*model.policy.mlp_extractor.policy_net,model.policy.action_net]:
        if isinstance(layer,torch.nn.Linear):
            layers.append({'inputSize':layer.in_features,'outputSize':layer.out_features,'weights':layer.weight.detach().numpy().reshape(-1).tolist(),'bias':layer.bias.detach().numpy().tolist(),'tanh':layer is not model.policy.action_net})
    (path/f'{name}.json').write_text(json.dumps({'layers':layers},separators=(',',':')),encoding='utf-8')
    return {"format":"Unity JSON network weights; ONNX disabled"}

def evaluate(connection,model,episodes=100,seed=456,unity_policy=False):
    result=connection.request(command='reset',seed=seed)
    obs=np.asarray([s['observation'] for s in result['states']],np.float32)
    finished=[];steps=0;max_error=0.
    while len(finished)<episodes and steps<episodes*100:
        actions=model.predict(obs,deterministic=True)[0]
        result=connection.request(command='policy_step') if unity_policy else connection.request(command='step',actions=actions.reshape(-1).tolist())
        states=result['states']
        if unity_policy:max_error=max(max_error,float(np.max(np.abs(np.asarray([s['action'] for s in states])-actions))))
        for s in states:
            if s['terminated'] or s['truncated']:finished.append({'success':s['success'],'return':s['episodeReturn'],'length':s['episodeLength']})
        obs=np.asarray([s['observation'] for s in states],np.float32);steps+=1
    finished=finished[:episodes]
    return {'episodes':len(finished),'success_rate':float(np.mean([e['success'] for e in finished])),'mean_return':float(np.mean([e['return'] for e in finished])),'mean_decisions':float(np.mean([e['length'] for e in finished])),'unity_action_max_absolute_error':max_error}

def main():
    p=argparse.ArgumentParser();p.add_argument('--scene',choices=['roller','chase'],default='roller');p.add_argument('--steps',type=int,default=100000);p.add_argument('--run-id',default=None);p.add_argument('--resume');p.add_argument('--port',type=int,default=9005);args=p.parse_args()
    name='RollerBall' if args.scene=='roller' else 'ChaseAgent'
    run=args.run_id or f'{name}-{time.strftime("%Y%m%d-%H%M%S")}'
    output=ROOT/'results'/run;output.mkdir(parents=True,exist_ok=False)
    connection=UnityConnection(ROOT/f'Builds/Training/{name}/{name}.exe',args.port,output/'unity.log')
    try:
        check_env(UnityGymEnv(connection),warn=True)
        env=UnityVecEnv(connection);torch.set_num_threads(4)
        model=PPO.load(args.resume,env=env,device='cpu') if args.resume else PPO('MlpPolicy',env,learning_rate=3e-4,n_steps=320,batch_size=512,n_epochs=3,gamma=.99,gae_lambda=.99,clip_range=.2,ent_coef=5e-4,policy_kwargs={'net_arch':{'pi':[128,128],'vf':[128,128]}},verbose=1,seed=42,device='cpu',tensorboard_log=str(ROOT/'results/tensorboard'))
        baseline=evaluate(connection,model)
        model.learn(total_timesteps=args.steps,callback=Progress(output/'progress.json'),reset_num_timesteps=not bool(args.resume),tb_log_name=run)
        model.save(output/name)
        trained=evaluate(connection,model)
        exported=export(model,name)
        connection.request(command='load_policy',path=str(ROOT/f'Assets/Models/{name}.json'))
        native=evaluate(connection,model,unity_policy=True)
        if native['unity_action_max_absolute_error']>2e-4:raise AssertionError('Unity C# policy differs from PyTorch export')
        report={'scene':name,'requested_steps':args.steps,'actual_steps':model.num_timesteps,'environment':connection.hello,'baseline':baseline,'trained':trained,'unity_inference':native,'export':exported,'versions':{'python':sys.version,'torch':torch.__version__},'reward':'success +1, fall 0, timeout 0; no shaping','observations':'8 values: target xyz/5, agent xyz/5, velocity xz/5','decision_period':10,'physics_delta_seconds':.02}
        (output/'evaluation.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report,indent=2))
    finally:connection.close()
if __name__=='__main__':main()
