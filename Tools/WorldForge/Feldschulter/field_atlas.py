"""Paint the field surgeon's pauldron atlas. Run with Python, NumPy and Pillow."""
import argparse
from pathlib import Path

# UV rectangles are specified in Blender's bottom-up coordinate system.
RECT = {
    'crown': (.005,.005,.695,.48), 'lame': (.005,.485,.695,.74),
    'fauld': (.005,.745,.695,.995), 'hide': (.705,.005,.995,.37),
    'strap': (.705,.375,.995,.53), 'binding': (.705,.535,.995,.60),
    'dark': (.705,.605,.995,.68), 'edge': (.705,.685,.995,.78),
    'bronze': (.705,.785,.995,.89), 'flax': (.705,.895,.995,.995)
}
SIZE = 1024

def uv_rect(key, u, v):
    x0,y0,x1,y1=RECT[key]
    return x0+(x1-x0)*u, y0+(y1-y0)*v

def atlas(path):
    import numpy as np
    from PIL import Image, ImageDraw, ImageFilter
    size=SIZE
    color=np.full((size,size,3),26,np.uint8)
    packed=np.zeros((size,size,4),np.uint8)
    packed[...,3]=100
    normals=np.full((size,size,3),128,np.uint8)
    normals[...,2]=255
    entries={
        'crown': ((52,56,59),(132,132,125),.87,.39,.71),
        'lame': ((49,52,54),(134,131,121),.86,.41,.72),
        'fauld': ((46,49,52),(130,130,123),.88,.39,.74),
        'hide': ((63,42,36),(104,77,59),.01,.28,.80),
        'strap': ((78,49,38),(121,85,60),.02,.36,.66),
        'binding': ((106,74,49),(146,111,74),.04,.36,.36),
        'dark': ((58,55,51),(92,88,75),.77,.40,.32),
        'edge': ((140,142,141),(175,171,156),.92,.58,.40),
        'bronze': ((143,104,60),(183,149,100),.83,.48,.43),
        'flax': ((167,144,108),(180,155,114),.01,.26,.65),
    }
    for index,(kind,(base,wear,metal,gloss,power)) in enumerate(entries.items()):
        x0,yb,x1,yt=RECT[kind]
        # Paint through the guard band so mipmaps sample a related surface.
        x0=max(0,int(x0*size)-3); x1=min(size,int(x1*size)+4)
        y0=max(0,int((1-yt)*size)-3); y1=min(size,int((1-yb)*size)+4)
        h,w=y1-y0,x1-x0
        y,x=np.mgrid[0:h,0:w]
        u=x/max(1,w-1); v=1-y/max(1,h-1)
        rng=np.random.default_rng(40137+index*1009)
        def noise(radius, axis=None):
            sy,sx=axis or (radius,radius)
            fy=np.fft.fftfreq(h)[:,None]; fx=np.fft.rfftfreq(w)[None,:]
            kernel=np.exp(-2*np.pi*np.pi*((sy*fy)**2+(sx*fx)**2))
            a=np.fft.irfft2(np.fft.rfft2(rng.normal(0.,1.,(h,w)))*kernel,s=(h,w),axes=(0,1))
            return (a-np.mean(a))/max(float(a.std()),.0001)
        wide=noise(24); ribbon=noise(8); hammer=noise(3)
        grain=rng.normal(0,1,(h,w)); hair=noise(1,(.55,3.))
        if kind in ('crown','lame','fauld'):
            # Uneven iron, low-frequency oxidation, fine cut marks and edge polish.
            scars=Image.new('L',(w,h)); pen=ImageDraw.Draw(scars)
            for i in range(int(w*h/420)):
                sx=rng.integers(0,w); sy=rng.integers(0,h)
                length=rng.uniform(2.0,20.0)*(2.4 if i%14==0 else 1.)
                dx=length*rng.uniform(.4,1.); dy=length*rng.uniform(-.9,.25)
                pen.line((sx,sy,sx+dx,sy+dy),fill=int(rng.integers(24,115)))
            cuts=np.asarray(scars.filter(ImageFilter.GaussianBlur(.37)),float)/255
            lip=np.exp(-np.maximum(0,1-v)*45)*(.44+.26*np.cos((u-.50)*2*np.pi))
            oxid=np.clip((wide*.70+ribbon*.52-.86)/2.2,0,.85)
            pits=np.clip(-hammer*.75-grain*.40-.92,0,1)
            # Directional fluting is modelled as actual form. Its dark patina reads on a distant mip.
            flutes=(np.abs(np.sin((u-.5)*np.pi*18 +.6*v))**16)*np.sin(np.pi*v)**1.5
            val=3.9*wide+3.5*ribbon+2.6*hammer+.9*hair+grain*1.9-6*pits-4*flutes
            c=np.array(base)+val[...,None]
            c+=oxid[...,None]*np.array((14.,3.,-6.))
            # A buffed elongated hollow follows the left-side lifting and carrying edge.
            abrasion=np.exp(-((u-.574)/.078)**2-((v-.87-.16*(u-.575))/.081)**2)
            abrasion*=np.clip(.33+.19*ribbon+.10*hair,0,.88)
            if kind!='crown': abrasion*=.23
            polish=np.clip(lip+.35*cuts+abrasion,0,.86)
            c=c*(1-polish[...,None])+np.array(wear)*polish[...,None]
            m=np.clip(metal-.32*oxid-.08*pits+.10*abrasion,0,1)
            s=np.clip(gloss+.04*ribbon+.035*hammer+.26*lip-.10*oxid+.15*cuts+.15*abrasion,0,1)
            ht=.62*hammer+.26*ribbon+.08*hair+.22*grain-.40*cuts-.12*pits
        elif kind in ('hide','strap','binding','flax'):
            direction=np.sin(x*.59+y*(.11 if kind=='flax' else .26)+.64*ribbon)
            crossing=np.sin(y*.75-x*.09+.3*hammer)
            if kind=='flax':
                weave=.60*direction+.65*crossing
            else:
                weave=.36*direction-.56*np.clip(np.abs(hammer-.0)*-2.2+1,0,1)+.14*crossing
            nap=np.clip(.25*wide+.50+ribbon*.12,0,1)
            val=5.*wide+3.8*ribbon+3.4*hammer+2*grain+weave*4.
            c=np.array(base)+val[...,None]+(nap*.12)[...,None]*(np.array(wear)-np.array(base))
            ht=.60*hammer+.27*ribbon+.35*grain+weave*.65
            s=np.clip(gloss+.048*wide+.025*hammer,0,1)
            m=np.full((h,w),metal)
        else:
            tarnish=np.clip(wide*.45+ribbon*.38+.1,0,1)
            val=3.*wide+3.1*ribbon+2.2*hammer+1.1*grain+1.2*hair
            c=np.array(base)+val[...,None]-tarnish[...,None]*np.array((18,16,15))
            if kind=='bronze':
                c+=tarnish[...,None]*np.array((-12.,-1.,.0))
            m=np.clip(metal-.16*tarnish,0,1)
            s=np.clip(gloss+.035*ribbon-.10*tarnish,0,1)
            ht=.42*hammer+.13*ribbon+.17*grain+.14*hair
        gy,gx=np.gradient(ht)
        # The second image axis points down. Positive G is UV +V in Unity's OpenGL convention.
        n=np.dstack((-gx*power*.39,gy*power*.39,np.ones_like(gx)))
        n/=np.linalg.norm(n,axis=2)[...,None]
        color[y0:y1,x0:x1]=np.clip(c,0,255).astype('uint8')
        packed[y0:y1,x0:x1,0]=np.rint(255*m).astype('uint8')
        packed[y0:y1,x0:x1,3]=np.rint(255*s).astype('uint8')
        normals[y0:y1,x0:x1]=np.rint((.5+.5*n)*255).astype('uint8')
    color_image=Image.fromarray(color)
    packed_image=Image.fromarray(packed)
    ink=ImageDraw.Draw(color_image); ink_pack=ImageDraw.Draw(packed_image)
    def mark_xy(point):
        import math
        ax=.165+point[0]*.49; pa=.92-point[1]*.35
        bottom=math.radians(48+39*(1+math.cos(ax))*.5+4.6*math.cos(3*ax)+2.1*math.sin(2*ax))
        u,v=uv_rect('crown',.012+.976*(ax+math.pi)/(2*math.pi),.012+.976*(pa-.013)/(bottom-.013))
        return (int(u*size),int((1-v)*size))
    for points,width in [
        ([(-.77,.41),(-.67,.03),(-.51,-.38),(-.18,-.67),(0.,-1.),(.16,-.67),(.45,-.39),(.64,-.01),(.73,.39)],2),
        ([(0,-.75),(-.06,-.25),(.02,.08),(.04,.44),(.16,.69)],3),
        ([(.035,.16),(-.20,.33),(-.38,.58),(-.45,.87),(-.21,.77),(.015,.58)],2),
        ([(.036,.21),(.27,.43),(.45,.72),(.47,.92),(.24,.81),(.07,.67)],2),
        ([(-.07,-.44),(-.31,-.25),(-.39,-.08),(-.21,-.14),(-.05,-.31)],2),
        ([(0,-.20),(.26,-.04),(.37,.17),(.20,.11),(-.01,-.04)],2)]:
        poly=[mark_xy(pt) for pt in points]
        ink.line(poly,fill=(67,52,42),width=width+2)
        ink.line(poly,fill=(126,96,62),width=width)
        ink_pack.line(poly,fill=(206,0,0,120),width=width)
    color=np.asarray(color_image);packed=np.asarray(packed_image)
    for name,array in [('BaseColor',color),('MetalSmooth',packed),('Normal',normals)]:
        Image.fromarray(array).save(path/f'Feldschulter_{name}.png',optimize=True)
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('directory',type=Path)
    dest=p.parse_args().directory;dest.mkdir(parents=True,exist_ok=True);atlas(dest)
