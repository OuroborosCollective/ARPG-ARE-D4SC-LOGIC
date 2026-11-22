"""Paint original modular garment surfaces for URP and Blender. UV tiles include a colour-safe guard band."""
from pathlib import Path
import numpy as np
ROOT=Path(__file__).resolve().parent
OUT=ROOT/'Assets/Resources/Equipment/AshWorks'
TILES=['iron','bright','bronze','bronzeedge','leather','suede','oildark','tally','linen','ashcloth','nightcloth','teal','brick','gravel','ember','bronzepale']
W=1024
# UVs are Blender-bottom-up, with each cell self padded.
def atlas_uv(key,u,v):
    i=TILES.index(key);a=.018+.964*u;b=.018+.964*v
    return ((i%4+a)/4,(3-i//4+b)/4)

def generate():
    from PIL import Image,ImageDraw
    OUT.mkdir(parents=True,exist_ok=True);size=W//4
    al=np.empty((W,W,3),np.uint8);pk=np.zeros((W,W,4),np.uint8);no=np.empty((W,W,3),np.uint8);rm=np.zeros((W,W,3),np.uint8)
    specs=[
     ((67,70,69),.84,.36),((126,124,111),.89,.58),((119,93,61),.80,.47),((164,132,82),.84,.65),
     ((79,55,41),.03,.31),((129,102,66),.01,.19),((53,45,38),.07,.37),((117,53,44),0,.21),
     ((166,155,132),0,.21),((120,119,105),0,.20),((70,71,69),0,.18),((69,110,106),.02,.26),
     ((122,84,62),.01,.21),((64,65,62),.02,.15),((160,62,37),.28,.45),((159,140,108),.72,.51)]
    yy,xx=np.mgrid[:size,:size];u=xx/size;v=yy/size
    for i,(key,style) in enumerate(zip(TILES,specs)):
        (base,met,gloss)=style;rng=np.random.default_rng(37529+i*197)
        def field(s,ay=None):
            fy=np.fft.fftfreq(size)[:,None]; fx=np.fft.rfftfreq(size)[None,:]
            kernel=np.exp(-2*np.pi**2*((s*fx)**2+((s if ay is None else ay)*fy)**2))
            n=np.fft.irfft2(np.fft.rfft2(rng.normal(size=(size,size)))*kernel,s=(size,size))
            return (n-n.mean())/max(n.std(),1e-6)
        slow=field(16);cloud=field(4.2);speck=field(.65);streak=field(1,11)
        col=np.zeros((size,size,3))+np.array(base);h=field(.9)*.13;metal=np.zeros((size,size))+met;smooth=np.zeros((size,size))+gloss
        if key in ('iron','bright','bronze','bronzeedge','bronzepale','ember'):
            # Directional hammer-strike grain, black-copper pits, oxidation, occasional bright nicks.
            dial=8*slow+5*cloud+3*speck+3.1*streak
            pits=np.clip(-(field(1.15)+1.45),0,1)
            smudges=np.clip((slow+.28*cloud)-.84,0,1.3)
            col+=dial[...,None];col-=pits[...,None]*np.array([34,32,29])
            col+=smudges[...,None]*(np.array([-12,1,0]) if 'bronze' in key else np.array([9,0,-6]))
            ink=Image.new('L',(size,size));dr=ImageDraw.Draw(ink)
            for k in range(90):
                x,y=rng.integers(-9,size+4,2);dx=int(rng.integers(2,27));dy=int(rng.integers(-5,9))
                dr.line((x,y,x+dx*.48,y+dy*.35,x+dx,y+dy),fill=int(rng.integers(45,210)),width=1)
            scratches=np.asarray(ink)/255
            col+=scratches[...,None]*27;h+=.22*streak+.46*scratches-.6*pits;smooth+=.036*slow+.23*scratches-.18*pits
            metal-=.26*smudges
        elif key in ('leather','suede','oildark'):
            cells=np.abs(field(2.4)+.83*field(.8));pin=np.exp(-np.abs(cells-.97)*13)
            col+=(11*slow+4*cloud+4.5*speck-13*pin)[...,None];h+=.4*pin+.16*speck;smooth+=.025*cloud
        elif key in ('brick','gravel'):
            pores=np.clip(-1.25-field(.82),0,1.1);layer=field(.95,5)
            col+=(15*slow+9*cloud+9*speck+5*layer-18*pores)[...,None];h+=.64*cloud+.70*speck-1.1*pores;smooth+=.08*cloud
        else:
            woof=np.cos(tau*yy/5.0+.12*slow);warp=np.cos(tau*xx/4.17+.20*cloud)
            thread=(warp*.49+woof*.44+.6*warp*woof)
            col+=(9.1*slow+4*cloud+5*thread+2.9*speck)[...,None];h+=.52*thread+.15*speck;smooth+=.016*cloud
        h=np.array(h,float)
        dx=(np.roll(h,-1,axis=1)-np.roll(h,1,axis=1))*.64;dy=(np.roll(h,-1,axis=0)-np.roll(h,1,axis=0))*.64
        normal=np.stack((-dx,dy,np.ones_like(h)),axis=-1);normal/=np.linalg.norm(normal,axis=-1,keepdims=True)
        x0=i%4*size;y0=i//4*size;rect=np.s_[y0:y0+size,x0:x0+size]
        al[rect]=np.uint8(np.clip(col,0,255));pk[rect][...,0]=np.uint8(255*np.clip(metal,0,1));pk[rect][...,3]=np.uint8(255*np.clip(smooth,.05,.9))
        no[rect]=np.uint8(np.clip((.5+.5*normal)*255,0,255))
        rm[rect][...,1]=255-pk[rect][...,3];rm[rect][...,2]=pk[rect][...,0]
    for name,data in [('BaseColor',al),('MetalSmooth',pk),('Normal',no),('RoughMetal',rm)]:Image.fromarray(data).save(OUT/f'Werkstoff_{name}.png',optimize=True)
tau=2*np.pi
if __name__=='__main__':generate()
