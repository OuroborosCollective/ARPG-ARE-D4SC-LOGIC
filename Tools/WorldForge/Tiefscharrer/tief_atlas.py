"""Paint the animal's eight continuous surface regions onto one standard PBR atlas."""
from pathlib import Path
import math,sys

REGIONS=('Coat','Cheek','Fore','Belly','Nail','Nose','Eyelid','GuardHair')
def tile_uv(k,u,v):
    if k=='Skin':
        return (.006+.988*u,.508+.484*v)
    i=REGIONS.index(k)
    return ((i%2+.014+.972*u)/2,(3-i//2+.020+.960*v)/4)

def prepare(folder):
    import numpy as np
    from PIL import Image,ImageDraw,ImageFilter
    folder=Path(folder);folder.mkdir(parents=True,exist_ok=True)
    w,h=1024,512
    col=np.zeros((h*4,w*2,3),np.uint8);normal=np.full_like(col,128);ms=np.zeros((h*4,w*2,4),np.uint8)
    orm=np.zeros_like(col);y,x=np.mgrid[0:h,0:w];u=x/(w-1);v=1-y/(h-1)
    entries=[((42,37,32),(108,85,64),.41),((62,46,41),(137,103,78),.39),
        ((55,44,38),(146,111,77),.34),((54,45,40),(130,98,82),.36),
        ((63,54,46),(197,174,133),.48),((58,37,38),(145,88,77),.36),
        ((49,40,38),(125,86,70),.41),((34,32,29),(132,107,79),.30)]
    for i,(dark,light,smoothness) in enumerate(entries):
        rng=np.random.default_rng(63081+i*9041)
        fy=np.fft.fftfreq(h)[:,None];fx=np.fft.rfftfreq(w)[None,:]
        def noise(sx,sy):
            a=np.fft.irfft2(np.fft.rfft2(rng.normal(size=(h,w)))*np.exp(-(sy*fy)**2*20-(sx*fx)**2*20),s=(h,w))
            return a/(a.std()+.00001)
        slow=noise(47,39);med=noise(6.5,7.5);fine=noise(.65,.75);long=noise(1.3,18)
        tone=.48+.116*slow+.083*med+.032*fine;surface=.22*med+.105*fine
        pores=np.maximum(0,-fine-1.56)
        if i in (0,2,3,7):
            # Overlapping streaks are shorter on the muzzle and palms; each follows the UV groom.
            tone+=.105*long+.045*noise(1,64)
            if i==0:tone-=.12*np.cos((u-.5)*math.tau)
            elif i==2:
                fold=.5+.5*np.cos(11*math.tau*v+1.75*np.sin(math.tau*u)+.24*slow)
                tone-=.15*fold**13;surface+=.5*fold**7-.32*pores
            elif i==3:tone+=.04*np.sin(7*math.tau*v+.32*med)
            hair=Image.new('L',(w,h));d=ImageDraw.Draw(hair)
            for j in range(15000 if i==0 else 6000):
                xx=float(rng.uniform(-8,w+8));yy=float(rng.uniform(-18,h+20))
                length=float(rng.uniform(3,23) if i!=2 else rng.uniform(3,13))
                bias=float(rng.normal(0,.23));slope=.16*np.sin(xx/w*math.tau+yy/h*5)+bias
                d.line([(xx,yy),(xx+length*slope,yy+length*.45),(xx+length*(slope+.10),yy+length)],fill=int(rng.uniform(72,210)),width=1)
            fibres=np.asarray(hair).astype(float)/255
            tone+=.21*fibres;surface+=.72*fibres+.17*long
        if i in (1,5,6):
            # Uncoated skin has pores and soft skin folds instead of hair.
            low=noise(12,2);base=(np.sin(13*math.tau*v+1.2*slow+.9*np.sin(math.tau*u*3))*.5+.5)
            crease=base**12 if i!=5 else np.zeros_like(base)
            tone+=.11*low-.15*crease-.24*pores
            surface+=.39*low-.60*crease-.45*pores
            if i==5:
                pap=np.minimum(1,np.maximum(0,noise(1.45,1.6)-.30))
                tone+=.125*pap-.08*(1-v);surface+=.87*pap
        if i==4:
            groove=.5+.5*np.sin(26*math.tau*u+1.7*slow+.28*med+2.2*v)
            split=np.exp(-np.abs(np.sin(6*math.tau*u+.22*med+.20*slow))*22)
            tone=.39+.28*v+.12*slow+.115*long-.18*split-.10*groove**15-.32*(1-v)**3
            surface+=.48*long-.68*split+.30*groove**8
        c=(1-np.clip(tone,0,1)[...,None])*np.array(dark)+np.clip(tone,0,1)[...,None]*np.array(light)
        gx=(np.roll(surface,-1,axis=1)-np.roll(surface,1,axis=1))*.36
        gy=(np.roll(surface,-1,axis=0)-np.roll(surface,1,axis=0))*.36
        nv=np.dstack((-gx,gy,np.ones_like(gx)));nv/=np.linalg.norm(nv,axis=2)[...,None]
        s=np.clip(smoothness+.064*med-.12*pores,0.15,.68)
        view=np.s_[i//2*h:(i//2+1)*h,i%2*w:(i%2+1)*w]
        col[view]=np.rint(c).astype('uint8');normal[view]=np.rint((.5+.5*nv)*255).astype('uint8')
        ms[view+(3,)]=np.rint(255*s).astype('uint8')
        orm[view+(0,)]=255;orm[view+(1,)]=np.rint(255*(1-s)).astype('uint8')
    # The entire torso, face and all four limbs use one chart. Changing anatomical regions
    # in this chart is continuous, so a shoulder never ends on a differently coloured island.
    H,W=h*2,w*2
    rng=np.random.default_rng(157831)
    fy=np.fft.fftfreq(H)[:,None];fx=np.fft.rfftfreq(W)[None,:]
    def multiscale(sx,sy):
        a=np.fft.irfft2(np.fft.rfft2(rng.normal(size=(H,W)))*np.exp(-(fx*sx)**2*20-(fy*sy)**2*20),s=(H,W))
        return a/(a.std()+.0001)
    y,x=np.mgrid[0:H,0:W];u=x/(W-1);v=1-y/(H-1)
    slow=multiscale(42,38);mid=multiscale(4.8,5.9);fine=multiscale(.65,.72);warp=multiscale(14,12)
    theta=(u-.5)*math.tau
    dorsal=np.clip((np.cos(theta)+.06)/1.02,0,1)
    cheeks=np.clip((v-.65)/.22,0,1);cheeks=cheeks*cheeks*(3-2*cheeks)
    coat=(1-cheeks)*(.34+.66*dorsal)
    hair=Image.new('L',(W,H));shadow=Image.new('L',(W,H));draw=ImageDraw.Draw(hair);und=ImageDraw.Draw(shadow)
    # Draw small directional fibres over the irregular hide. Their roots and ends live in the same chart.
    for j in range(72000):
        xx=float(rng.uniform(-14,W+8));yy=float(rng.uniform(-15,H+24));U=xx/W;V=1-yy/H
        if V>.76 and rng.random()<min(.97,(V-.76)*4.2):continue
        lean=.42*np.sin(math.tau*(U-.5))+.10*np.sin(math.tau*V*2)+rng.normal(0,.28)
        length=rng.uniform(3.0,19.0)*(1.05 if V<.65 else .65)
        bend=rng.normal(.18,.25)
        sequence=[(xx,yy),(xx+lean*length*.56,yy+length*.52),(xx+(lean+bend*.25)*length,yy+length)]
        d=int(rng.uniform(55,198));und.line([(a+.75,b-.15) for a,b in sequence],fill=d,width=2 if j%11==0 else 1);draw.line(sequence,fill=d,width=1)
    strands=np.asarray(hair).astype(float)/255;roots=np.asarray(shadow).astype(float)/255
    mottling=.50+.117*slow+.084*mid+.041*fine+.028*warp
    pores=np.maximum(0,-fine-1.40)
    tone=np.clip(mottling+.16*strands-.10*roots-.105*pores-.15*coat,0,1)
    deep=np.array((31,27,25));high=np.array((103,80,60))
    c=(1-tone[...,None])*deep+tone[...,None]*high
    c+=cheeks[...,None]*np.array((4.5,1.0,-.5))
    c+=np.clip(-np.cos(theta),0,1)[...,None]*np.array((3.5,1,0))
    c+=strands[...,None]*np.array((24,20,14))-roots[...,None]*np.array((11,8,6))
    height=(.145*mid+.095*fine+.58*strands-.31*roots-.3*pores)
    gy,gx=np.gradient(height);nrm=np.stack((-gx*.42,gy*.42,np.ones_like(gx)),axis=-1)
    nrm/=np.linalg.norm(nrm,axis=2)[...,None]
    shine=np.clip(.244+.071*(1-coat)+.035*mid-.035*pores,.12,.48)
    col[:H,:W]=np.rint(np.clip(c,0,255)).astype('uint8')
    normal[:H,:W]=np.rint((.5+.5*nrm)*255).astype('uint8');ms[:H,:W,3]=np.rint(shine*255).astype('uint8')
    orm[:H,:W,1]=np.rint(255*(1-shine)).astype('uint8')
    for name,data in [('BaseColor',col),('Normal',normal),('MetalSmooth',ms),('ORM',orm)]:
        Image.fromarray(data).save(folder/f'Tiefscharrer_{name}.png',compress_level=6)

if __name__=='__main__':prepare(sys.argv[1])
