"""Gymnasium and SB3 adapters for the actual Unity physics training build."""
import json
import socket
import time
import subprocess
from pathlib import Path
import numpy as np
import gymnasium as gym
from gymnasium import spaces
from stable_baselines3.common.vec_env import VecEnv

class UnityConnection:
    def __init__(self, executable=None, port=9005, log=None):
        self.process = None
        if executable:
            args=[str(executable),'-batchmode','-nographics','-rl-server','-rl-port',str(port)]
            if log: args += ['-logFile',str(log)]
            self.process=subprocess.Popen(args,creationflags=subprocess.CREATE_NO_WINDOW)
        self.socket=socket.socket()
        deadline=time.monotonic()+120
        while True:
            try:self.socket.connect(('127.0.0.1',port));break
            except OSError:
                if self.process and self.process.poll() is not None:raise RuntimeError('Unity exited before connection; inspect its log')
                if time.monotonic()>deadline:raise TimeoutError('Unity training bridge did not start')
                time.sleep(.2)
        self.socket.setsockopt(socket.IPPROTO_TCP,socket.TCP_NODELAY,1)
        self.socket.settimeout(120)
        self.reader=self.socket.makefile('r',encoding='utf-8')
        self.writer=self.socket.makefile('w',encoding='utf-8',newline='\n')
        self.hello=self.request(command='hello')
    def request(self,**data):
        self.writer.write(json.dumps(data,separators=(',',':'))+'\n');self.writer.flush()
        line=self.reader.readline()
        if not line:raise ConnectionError('Unity closed the bridge')
        result=json.loads(line)
        if result.get('error'):raise RuntimeError(result['error'])
        return result
    def close(self):
        try:self.request(command='close')
        except (OSError,RuntimeError):pass
        self.reader.close();self.writer.close();self.socket.close()
        if self.process:
            try:self.process.wait(timeout=10)
            except subprocess.TimeoutExpired:self.process.terminate()

class UnityVecEnv(VecEnv):
    def __init__(self,connection):
        self.connection=connection
        states=connection.request(command='reset',seed=123)['states']
        super().__init__(len(states),spaces.Box(-np.inf,np.inf,(8,),np.float32),spaces.Box(-1,1,(2,),np.float32))
        self.actions=None
    def reset(self):
        seed=self._seeds[0] if self._seeds[0] is not None else 123
        states=self.connection.request(command='reset',seed=int(seed))['states']
        self._reset_seeds();return np.asarray([s['observation'] for s in states],dtype=np.float32)
    def step_async(self,actions):self.actions=np.clip(actions,-1,1)
    def step_wait(self):
        states=self.connection.request(command='step',actions=self.actions.reshape(-1).tolist())['states']
        obs=np.asarray([s['observation'] for s in states],dtype=np.float32)
        rewards=np.asarray([s['reward'] for s in states],dtype=np.float32)
        dones=np.asarray([s['terminated'] or s['truncated'] for s in states],dtype=bool)
        infos=[]
        for s,done in zip(states,dones):
            info={'is_success':bool(s['success']),'TimeLimit.truncated':bool(s['truncated'] and not s['terminated'])}
            if done:
                info['terminal_observation']=np.asarray(s['terminalObservation'],dtype=np.float32)
                info['episode']={'r':s['episodeReturn'],'l':s['episodeLength'],'t':0}
            infos.append(info)
        return obs,rewards,dones,infos
    def close(self):self.connection.close()
    def get_attr(self,attr_name,indices=None):return [None if attr_name=='render_mode' else getattr(self,attr_name,None) for _ in self._get_indices(indices)]
    def set_attr(self,attr_name,value,indices=None):setattr(self,attr_name,value)
    def env_method(self,method_name,*args,indices=None,**kwargs):return [getattr(self,method_name)(*args,**kwargs) for _ in self._get_indices(indices)]
    def env_is_wrapped(self,wrapper_class,indices=None):return [False for _ in self._get_indices(indices)]

class UnityGymEnv(gym.Env):
    """Single-arena Gymnasium view, used for the official environment checker."""
    metadata={'render_modes':[]}
    def __init__(self,connection):
        self.connection=connection
        self.num_arenas=len(connection.request(command='reset',seed=1)['states'])
        self.observation_space=spaces.Box(-np.inf,np.inf,(8,),np.float32)
        self.action_space=spaces.Box(-1,1,(2,),np.float32)
    def reset(self,*,seed=None,options=None):
        super().reset(seed=seed)
        s=self.connection.request(command='reset',seed=int(seed or 0))['states'][0]
        return np.asarray(s['observation'],np.float32),{}
    def step(self,action):
        actions=np.zeros((self.num_arenas,2),np.float32);actions[0]=action
        s=self.connection.request(command='step',actions=actions.reshape(-1).tolist())['states'][0]
        done=s['terminated'] or s['truncated']
        obs=s['terminalObservation'] if done else s['observation']
        return np.asarray(obs,np.float32),float(s['reward']),bool(s['terminated']),bool(s['truncated']),{'is_success':s['success']}
