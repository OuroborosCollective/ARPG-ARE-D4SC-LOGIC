"""Bake four original, seamless 512px surface-detail tiles.

python3 surfaces.py --out textures
Coordinates and convolutions wrap on both axes. All four PNGs are grayscale
colour modulation with neutral mean and baked fine surface relief.
"""
import argparse
from math import cos, pi, sin
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import gaussian_filter

SIZE = 512
Y, X = np.mgrid[0:SIZE, 0:SIZE].astype(np.float32)
FX, FY = np.meshgrid(np.fft.fftfreq(SIZE), np.fft.fftfreq(SIZE))
TURN = 2 * pi


def cloud(rng, along, across=None, angle=0):
    """Stationary periodic band, optionally stretched along a geological axis."""
    across = along if across is None else across
    u = FX*cos(angle)+FY*sin(angle)
    v = FY*cos(angle)-FX*sin(angle)
    weights = np.exp(-2*pi*pi*((along*u)**2+(across*v)**2))
    data = np.fft.ifft2(np.fft.fft2(rng.standard_normal(X.shape))*weights).real
    return ((data-data.mean()) / max(data.std(), 1e-6)).astype(np.float32)


def smooth(a, b, x):
    t = np.clip((x-a)/(b-a), 0, 1)
    return t*t*(3-2*t)


def relief(height):
    """North-west rim light; central differences include the wrap-around edge."""
    return (.60*(np.roll(height, 1, 0)-np.roll(height, -1, 0))
            + .40*(np.roll(height, 1, 1)-np.roll(height, -1, 1)))


def paths(rng, specs):
    """Antialiased joint strokes. Each unwrapped polyline is stamped on a torus."""
    ss = 3
    canvas = Image.new("L",(SIZE*ss,SIZE*ss))
    brush = ImageDraw.Draw(canvas)
    def stamp(points, width, strength):
        for ox in (-SIZE, 0, SIZE):
            for oy in (-SIZE, 0, SIZE):
                line=[(round((px+ox)*ss),round((py+oy)*ss)) for px,py in points]
                brush.line(line, fill=round(strength*255),width=max(1,round(width*ss)))
    for x,y,angle,length,w,jitter,branches in specs:
        n=max(4,int(length/6))
        pts=[(x,y)]; cur=angle
        for i in range(n):
            cur=.60*cur+.40*angle+rng.normal(0,jitter)
            dist=length/n*rng.uniform(.72,1.28)
            x+=dist*cos(cur);y+=dist*sin(cur);pts.append((x,y))
        s=rng.uniform(.70,.97)
        stamp(pts[:n//2+1],w,s)
        stamp(pts[n//2:],w*.72,s*.83)
        for _ in range(branches):
            j=int(rng.integers(1,n))
            a=angle+rng.choice([-1,1])*rng.uniform(.55,1.06)
            bx,by=pts[j];branch=[(bx,by)]
            for _ in range(max(2,int(n*rng.uniform(.14,.32)))):
                a+=rng.normal(0,jitter*.64)
                step=rng.uniform(2.4,5.6);bx+=step*cos(a);by+=step*sin(a)
                branch.append((bx,by))
            stamp(branch,w*.55,s*.69)
    return np.asarray(canvas.resize((SIZE,SIZE),Image.Resampling.LANCZOS),dtype=np.float32)/255


def scars(rng, count, minlen, maxlen, orientation, spread, width, jitter, branches):
    return [(rng.uniform(0,SIZE),rng.uniform(0,SIZE),orientation+rng.uniform(-spread,spread),
             rng.uniform(minlen,maxlen),rng.uniform(.7,1.3)*width,jitter,
             int(rng.integers(0,branches+1))) for _ in range(count)]


def pores(rng, scale=1.0):
    cluster = cloud(rng, 7.8)
    pits = smooth(1.26,2.45,cloud(rng,scale)+.22*cluster)
    return pits


def masonry(rng):
    weather = cloud(rng, 24)
    chisel = cloud(rng, 3.1, .85, -.33)
    mottling = cloud(rng, 6.4)
    erode = cloud(rng, 1.55)
    # Courses line up across x. Each row has a different lap and block lengths.
    # The shared warp also curves the bed joints and gives the edges chipped lips.
    xx = (X+2.9*cloud(rng,19)+1.0*cloud(rng,3.1)+1.5*erode)%SIZE
    yy = (Y+4.3*cloud(rng,32)+1.1*cloud(rng,4.3)+.65*erode)%SIZE
    starts = np.array([0,76,161,235,330,421,512])
    edge=np.empty_like(X);block_tone=np.empty_like(X);block_face=np.empty_like(X)
    for j,(y0,y1) in enumerate(zip(starts[:-1],starts[1:])):
        count=(5,4,5,4,5,4)[j]
        intervals=rng.uniform(.64,1.30,count)
        bounds=np.r_[0,np.cumsum(SIZE*intervals/intervals.sum())]
        xxr=(xx+(48*j%119)+rng.uniform(0,48))%SIZE
        i=np.minimum(np.searchsorted(bounds,xxr,side="right")-1,count-1)
        dx=np.minimum(xxr-bounds[i],bounds[i+1]-xxr)
        dy=np.minimum(yy-y0,y1-yy)
        # Chamfer the block corners beyond the regular joint recess.
        corner=(dx+dy-5.1-1.9*np.maximum(mottling,0))*.72
        d=np.minimum(np.minimum(dx,dy),corner)
        mask=(yy>=y0)&(yy<y1)
        edge[mask]=d[mask]
        tones=rng.uniform(-12,11,count)
        block_tone[mask]=tones[i[mask]]
        tilt=.06*(xxr-(bounds[i]+bounds[i+1])*.5)*np.cos(i*1.4+j)
        block_face[mask]=tilt[mask]
    inside=smooth(.9,4.6,edge)
    top=smooth(1.30,6.20,edge)
    pits=pores(rng)
    crack=paths(rng,scars(rng,27,11,45,-1.04,1.28,1.05,.42,2))
    rakes=paths(rng,scars(rng,82,3.8,15.5,-.58,.38,.86,.10,0))
    grain=rng.standard_normal(X.shape)
    mortar=166+4.2*erode+4.4*mottling+2.8*grain
    face=209+block_tone+block_face+3.9*weather+3.5*mottling+1.8*chisel+1.2*grain
    face-=9.2*pits+8.5*crack+2.8*rakes
    h=19*top+1.35*mottling+.85*chisel-3*pits-4.1*crack
    return mortar*(1-inside)+face*inside+relief(h)*.83


def rock(rng):
    big=cloud(rng,38); small=cloud(rng,5.4)
    glide=cloud(rng,25,9,-.17)
    lamina=cloud(rng,16,1.02,-.23)
    t=(2*X+8*Y)/SIZE + .18*big + .043*glide
    phi=TURN*t
    cleave=(np.sin(phi*3+.71*glide)+.45*np.sin(phi*7+.80*small))/1.45
    # Thin discontinuous mineral edges feather into the plane of the parent rock.
    seams=np.exp(-((np.sin(phi+.29*np.sin(2*phi)))/.115)**2)
    seam_gate=smooth(-.85,.93,glide+.26*small)
    strata=cloud(rng,27,2.25,-.22)
    pits=pores(rng,1.35)
    specs=scars(rng,31,26,118,-.97,.46,1.12,.36,2)
    fissure=paths(rng,specs)
    halo=gaussian_filter(fissure,1.05,mode="wrap")
    fine=cloud(rng,.56)
    h=3.15*strata+1.32*lamina+2.2*cleave+1.2*seams-6.0*fissure-1.7*pits
    return (206+4.65*big+3.55*glide+4.1*strata+2.15*lamina+2.5*cleave+1.6*fine
            +12.8*seams*seam_gate-7.8*pits-15.5*fissure-6.8*halo+relief(h))


def timber(rng):
    stretched=cloud(rng,31,2.2)
    broad=cloud(rng,45,9.3)
    ywarp=Y+3.8*cloud(rng,49,13)+1.43*cloud(rng,29,4.0)
    knots=np.zeros_like(X); raised=np.zeros_like(X)
    for x,y,rx,ry in ((90,140,29,12),(314,372,46,16),(492,58,21,9),(214,295,17,7)):
        dx=(X-x+SIZE/2)%SIZE-SIZE/2;dy=(Y-y+SIZE/2)%SIZE-SIZE/2
        rr=np.sqrt((dx/rx)**2+(dy/ry)**2)
        ywarp-=.98*ry*np.tanh(dy/(ry*.51))*np.exp(-(dx/(rx*1.65))**2-.60*(dy/(ry*1.85))**2)
        fade=np.exp(-1.5*np.maximum(0,rr-.30)**2)
        ring=np.cos(12.3*rr+.68*broad)
        knots+=(11*np.exp(-3.5*rr**2)+7.6*smooth(.03,.99,ring))*fade
        raised+=1.4*ring*fade
    early=np.sin(TURN*31*ywarp/SIZE+.34*stretched)
    late=(.5+.5*np.sin(TURN*63*ywarp/SIZE+.62*broad))**7
    fibers=cloud(rng,8.2,.58)
    split=paths(rng,scars(rng,26,35,178,0,.072,1.20,.095,1))
    score=paths(rng,scars(rng,66,12,83,0,.035,.70,.045,0))
    checks=gaussian_filter(split,.80,mode='wrap')
    high=1.24*stretched+.80*early-2.5*late-1.2*fibers+raised-4.8*checks
    return (208+4.1*broad+3.8*stretched+2.5*early-8.25*late+2.75*fibers-knots
            -19.2*split-6.55*checks-4.8*score+relief(high)+1.12*cloud(rng,.48))


def fragment(rel, tint, rng, leaf=False):
    """An asymmetric grit chip or curled leaf sliver; touching borders wraps it."""
    x,y=rng.uniform(0,SIZE,2)
    rx=rng.uniform(2.9,8.9) if leaf else min(rng.lognormal(.98,.60),12.0)
    ry=rx*rng.uniform(.17,.35) if leaf else rx*rng.uniform(.46,.93)
    r=int(np.ceil(rx*1.29))+1
    xi=np.arange(int(x)-r,int(x)+r+1);yi=np.arange(int(y)-r,int(y)+r+1)
    dx,dy=np.meshgrid(xi-x,yi-y)
    a=rng.uniform(0,TURN);c,s=cos(a),sin(a)
    u,v=(dx*c+dy*s)/rx,(-dx*s+dy*c)/ry
    az=np.arctan2(v,u)
    rr=np.hypot(u,v)*(1+.13*np.sin(3*az+a)+.07*np.cos(5*az-a))
    weight=1-smooth(.80,1.08,rr)
    dome=np.sqrt(np.maximum(0,1-np.minimum(rr,1)**2))*weight
    ix=np.ix_(yi%SIZE,xi%SIZE)
    h=rng.uniform(1.8,5.7)*(1.20 if leaf else 1)
    rel[ix]+=h*dome
    tone=rng.uniform(-12,10) if not leaf else rng.uniform(-14,-5)
    tint[ix]+=tone*weight
    if leaf:
        tint[ix]+=3.8*np.exp(-16*v*v)*weight


def earth(rng):
    hummock=cloud(rng,18,9,.45)
    dirt=cloud(rng,4.85)
    grains=cloud(rng,.61)
    grit=pores(rng,1.48)
    eroded=cloud(rng,15,2,.17)
    # Fine parallel drifts break up where exposed aggregate shows through.
    phi=TURN*(2*X+9*Y)/SIZE+.83*cloud(rng,20)
    dust=np.maximum(0,np.sin(phi))**6*smooth(-1.35,1.06,hummock)
    high=2.65*dirt+1.25*grains-1.35*grit+.80*eroded+.95*dust
    t=np.zeros_like(X)
    for _ in range(890):fragment(high,t,rng)
    for _ in range(40):fragment(high,t,rng,leaf=True)
    return (204+5.0*hummock+4.65*dirt+1.95*grains-6.8*grit+1.44*eroded+4.7*dust+t
            +relief(high)*1.15)


def bake(out):
    out=Path(out);out.mkdir(parents=True,exist_ok=True)
    for fn,seed,gain in ((masonry,21971,1.0), (rock,49131,1.25),
                         (timber,78101,1.35), (earth,87037,1.18)):
        value=fn(np.random.default_rng(seed))
        value=value.mean()+(value-value.mean())*gain
        if fn is masonry:
            # The periodic seam passes through a stone, away from a horizontal bed joint.
            value=np.roll(value,37,axis=0)
        pixels=np.clip(np.rint(value),0,255).astype(np.uint8)
        Image.fromarray(pixels).save(out/(fn.__name__+'.png'),compress_level=9)


if __name__=='__main__':
    cli=argparse.ArgumentParser(description=__doc__)
    cli.add_argument('--out',type=Path,default=Path(__file__).parent/'textures')
    bake(cli.parse_args().out)
