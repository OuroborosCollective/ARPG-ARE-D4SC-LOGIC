"""Periodic, seeded clay glaze and roughness detail for the private CPU authoring host."""
from pathlib import Path
import numpy as np
from PIL import Image
dst=Path(__file__).resolve().parent/'out';dst.mkdir(exist_ok=True)
size=1024
y,x=np.mgrid[0:size,0:size]
a=(x/size-.5)*2*np.pi;z=(1-y/size)*2.08-1.08
rng=np.random.default_rng(918225)
fy=np.fft.fftfreq(size)[:,None];fx=np.fft.rfftfreq(size)[None,:]
def noise(r):
    b=np.fft.irfft2(np.fft.rfft2(rng.normal(size=(size,size)))*np.exp(-2*np.pi**2*r**2*(fy*fy+fx*fx)),s=(size,size))
    return b/(b.std()+1e-6)
n1,n4,n15,n45=[noise(s) for s in (.6,3.8,13,38)]
front=np.maximum(np.cos(a+np.pi/2),0);up=np.clip((z+.7)/1.55,0,1)
iron=np.maximum(0,n45*.8+.38*n15-.4)*np.clip((.7-z)*1.35,.17,1)
kiln=np.clip(n15*.2+n4*.16+.51*(1-up),0,1)
striae=np.sin(100*z+4*np.sin(9*a)+.7*n15)**4
base=110+38*up+14*n45+6*n15+7*n4+4*n1-35*iron-16*kiln+6*striae
forehead=front**6*np.clip((z-.03)/.20,0,1)*np.clip((.76-z)/.14,0,1)
for side in (-1,1):
    band=np.exp(-(((a+np.pi/2)*side-.45-.28*np.cos(4*z))/.023)**2)
    base-=forehead*(14+13*np.maximum(np.cos(36*z+side*.2),0)**4)*band
dot=np.maximum(0,(-n1-1.82))*.7
color=np.stack([base*1.12+11,base*.965+8,base*.79+7],2)-np.minimum(32,20*dot)[:,:,None]
dy,dx=np.gradient(.36*n4+.17*n1+.34*n15-.64*dot+.15*striae)
normal=np.stack((-dx*.45,dy*.45,np.ones_like(dx)),2)
normal/=np.linalg.norm(normal,axis=2)[:,:,None]
for dest,arr in [('Grubenlicht_Ton.png',color),('Grubenlicht_Poren.png',(normal*.5+.5)*255)]:
    Image.fromarray(np.uint8(np.clip(arr,0,255))).save(dst/dest,optimize=True)
