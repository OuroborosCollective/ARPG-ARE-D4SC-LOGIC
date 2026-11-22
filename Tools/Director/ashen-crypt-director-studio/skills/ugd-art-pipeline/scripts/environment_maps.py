"""Bake tileable basalt crust, lava mask, tangent normals and an ember sprite on CPU."""
import argparse
from pathlib import Path
import numpy as np
from PIL import Image

def field(n,k,rng):
    a=rng.uniform(-1,1,(k,k));p=np.arange(n)*k/n
    i=p.astype(int);t=p-i;t=t*t*(3-2*t)
    top=a[i[:,None],i]*(1-t)+a[i[:,None],(i+1)%k]*t
    bottom=a[((i+1)%k)[:,None],i]*(1-t)+a[((i+1)%k)[:,None],(i+1)%k]*t
    return top*(1-t[:,None])+bottom*t[:,None]

def bake(out,n,seed):
    out.mkdir(parents=True,exist_ok=True)
    rng=np.random.default_rng(seed);y,x=np.indices((n,n))/n
    broad=field(n,4,rng);rough=field(n,18,rng);grain=field(n,91,rng)
    u=6*x+.32*broad+.085*rough;v=6*y+.38*field(n,5,rng)+.07*grain
    cx,cy=np.floor(u).astype(int),np.floor(v).astype(int)
    offsets=rng.uniform(.14,.86,(6,6,2))
    first=np.full((n,n),100.); second=first.copy()
    for j in (-1,0,1):
        for i in (-1,0,1):
            ox,oy=offsets[(cy+j)%6,(cx+i)%6].transpose(2,0,1)
            d=np.hypot(cx+i+ox-u,cy+j+oy-v)
            second=np.minimum(second,np.maximum(first,d));first=np.minimum(first,d)
    edge=second-first
    heat=np.exp(-np.square(edge/(.047+.018*broad)))*np.clip(.72+.30*rough+.16*grain,.22,1)
    h=.53*np.tanh(edge*13)+.16*broad+.055*rough+.035*grain
    dx=(np.roll(h,-1,1)-np.roll(h,1,1))*n*.017
    dy=(np.roll(h,-1,0)-np.roll(h,1,0))*n*.017
    norm=np.stack((-dx,dy,np.ones_like(dx)),axis=-1);norm/=np.linalg.norm(norm,axis=-1,keepdims=True)
    powder=np.clip(.33+.40*broad+.15*rough+.13*grain,0,1)*np.clip(edge*7,0,1)
    basalt=np.stack((.235+.37*powder,.245+.38*powder,.258+.39*powder),axis=-1)
    basalt*=np.clip(.85+.17*rough+.10*grain,.48,1.20)[:,:,None]
    def save(name,values):Image.fromarray(np.uint8(np.rint(255*np.clip(values,0,1)))).save(out/name)
    save('CrustHeat.png',np.dstack((basalt,heat)))
    save('CrustNormal.png',.5+.5*norm)
    # A round, soft emissive fleck avoids opaque rectangular particle cards.
    py,px=np.indices((64,64));r=np.hypot((px-31.5)/27,(py-31.5)/30)
    a=np.clip(1-r,0,1)**1.65
    save('EmberFleck.png',np.dstack((np.ones_like(a),.73+.27*a,.25+.75*a,a)))
    print(f'{n}x{n} hot mask coverage {np.mean(heat>.4):.4f}; seed {seed}')

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('output',type=Path);p.add_argument('--size',type=int,choices=(256,512,1024,2048),default=1024)
    p.add_argument('--seed',type=int,default=7619);o=p.parse_args();bake(o.output,o.size,o.seed)
