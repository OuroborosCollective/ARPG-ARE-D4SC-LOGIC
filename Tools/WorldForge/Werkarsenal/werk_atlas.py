"""Paint the shared object's physically based surface atlas in CPU memory."""
from pathlib import Path
import sys

TILES = ('Schiefer', 'Grat', 'Ton', 'Elfenbein', 'Eisen', 'Kante', 'Schlacke', 'Zunder',
         'Glockenguss', 'Messing', 'Zinn', 'Gruenspan', 'Leder', 'Band', 'Leinen', 'Rotsiegel')

def tile_uv(k, u, v):
    i=TILES.index(k)
    return ((i%4+.028+.944*u)/4, (3-i//4+.028+.944*v)/4)

def paint(folder):
    import numpy as np
    from PIL import Image
    folder=Path(folder);folder.mkdir(parents=True,exist_ok=True)
    entries=[((39,48,49),(120,139,136),.28,.42),((91,90,73),(207,191,147),.03,.34),
             ((87,71,53),(163,136,93),.03,.26),((145,119,84),(232,209,164),.02,.37),
             ((56,58,58),(150,151,139),.83,.44),((122,125,125),(190,193,178),.9,.65),
             ((57,46,42),(144,82,57),.15,.26),((125,61,31),(240,161,70),.23,.4),
             ((83,74,56),(169,137,79),.82,.45),((114,83,49),(202,161,92),.86,.55),
             ((94,106,114),(178,184,183),.82,.52),((63,91,78),(111,143,115),.46,.34),
             ((58,45,40),(129,91,64),0,.33),((97,78,52),(173,139,87),.02,.32),
             ((133,117,93),(201,185,148),0,.23),((73,38,35),(152,73,57),.01,.29)]
    side=256;out=[np.zeros((1024,1024,c),np.uint8) for c in (3,4,3,3)]
    y,x=np.mgrid[0:side,0:side];u=x/side;v=y/side
    for i,(base,edge,metal,smooth) in enumerate(entries):
        rng=np.random.default_rng(42031+919*i)
        fy=np.fft.fftfreq(side)[:,None];fx=np.fft.rfftfreq(side)[None,:]
        def jitter(sx,sy):
            s=np.fft.irfft2(np.fft.rfft2(rng.normal(size=(side,side)))*np.exp(-2*np.pi**2*((fx*sx)**2+(fy*sy)**2)),s=(side,side))
            return s/max(.0001,s.std())
        slow=jitter(14,17);mid=jitter(2.8,3.5);speck=jitter(.6,.5);scar=jitter(17,.75)
        w=np.clip(.41+.16*slow+.08*mid,0,1);surface=.20*mid+.08*speck
        if i==0:
            fractures=np.exp(-np.abs(np.sin(5*np.pi*u+1.1*slow+1.7*v))*17)
            w+=.27*fractures-.18*np.clip(-slow,0,1);surface+=.7*fractures
        elif i in (1,2,3):
            grain=np.sin(43*u+1.75*slow+5*v)**6
            pores=np.clip(-speck-1.55,0,1)
            w+=.13*grain-.26*pores;surface+=.65*grain-.78*pores
        elif i in (4,5,6,8,9,10,11):
            dent=np.exp(-np.abs(np.sin(9*u+2.8*v+.30*mid))*17)
            pits=np.clip(speck-1.88,0,1)*np.clip(-slow+.56,0,1)
            w+=.12*scar+.16*dent-.32*pits;surface+=.40*scar+.32*dent-.8*pits
        elif i==14:
            weave=.5*(np.sin(tau(34)*u)**5+np.sin(tau(32)*v)**5);w+=.09*weave;surface+=.69*weave
        else:
            fold=np.abs(np.sin(52*v+slow*.72+3*u))**8
            w+=.08*fold+.08*speck;surface+=.6*fold
        w=np.clip(w,0,1);c=(1-w[...,None])*np.array(base)+w[...,None]*np.array(edge)
        gx=(np.roll(surface,-1,axis=1)-np.roll(surface,1,axis=1))*.53
        gy=(np.roll(surface,-1,axis=0)-np.roll(surface,1,axis=0))*.53
        normal=np.dstack((-gx,gy,np.ones_like(gx)));normal/=np.linalg.norm(normal,axis=2)[...,None]
        met=np.clip(metal*(.84+.24*w),0,1);s=np.clip(smooth+.07*scar+.07*(w-.5),.07,.9)
        depth=np.s_[i//4*side:(i//4+1)*side,i%4*side:(i%4+1)*side]
        out[0][depth]=np.round(c).astype('uint8')
        out[1][depth]=np.rint(255*np.stack((met,met,met,s),axis=-1)).astype('uint8')
        out[2][depth]=np.rint(255*(normal*.5+.5)).astype('uint8')
        out[3][depth]=np.rint(255*np.stack((np.ones_like(met),1-s,met),axis=-1)).astype('uint8')
    for name,d in zip(('BaseColor','MetalSmooth','Normal','ORM'),out):
        Image.fromarray(d).save(folder/f'Werkarsenal_{name}.png',compress_level=9)

def tau(n):
    import math
    return math.tau*n

if __name__=='__main__':paint(sys.argv[1])
