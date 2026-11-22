"""Anatomical surface features. Every placement is evaluated on the continuous volume."""
import math
import numpy as np
from tief_form import interp, nodes, clamp, surface, all_regions
from tief_atlas import tile_uv

def unit(a):return a/max(1.e-9,np.linalg.norm(a))
def normal(p):
    p=np.array(p,dtype=np.float32);delta=.007
    return unit(np.array([float(surface(*(p+v))-surface(*(p-v))) for v in np.eye(3,dtype=np.float32)*delta]))
def skinpoint(y,z,s,limit=.59):
    x=np.linspace(0,limit,100,dtype=np.float32)
    d=surface(x*s,np.full_like(x,y),np.full_like(x,z));cross=np.flatnonzero((d[:-1]<=0)&(d[1:]>=0))
    if len(cross):
        i=int(cross[-1]);v=x[i]+(x[i+1]-x[i])*(-d[i])/(d[i+1]-d[i]);return np.array([s*v,y,z])
    i=np.argmin(abs(d));return np.array([s*x[i],y,z])

def ranges(p):
    eps=.00001
    low=p.min(0);high=p.max(0)
    return (p-low)/np.maximum(high-low,eps)

class Build:
    def __init__(self,p,f,level):
        self.v=list(map(tuple,p));self.f=[tuple(face) for face in f]
        x,y,z=p.T
        # Derive smooth per-vertex directions from the native surface triangles.
        n=np.zeros_like(p);ff=np.array([s[:3] for s in f]);normals=np.cross(p[ff[:,1]]-p[ff[:,0]],p[ff[:,2]]-p[ff[:,0]])
        for i in range(3):np.add.at(n,ff[:,i],normals)
        qidx=[(i,a[3]) for i,a in enumerate(f) if len(a)==4]
        if qidx:
            fi,vi=np.array(qidx).T;np.add.at(n,vi,normals[fi])
        norms=np.linalg.norm(n,axis=1);n/=np.maximum(norms,1.e-8)[:,None]
        self.body_v=p;self.body_n=n;self.level=level
        # Spatially mapped dermis, not a random colour assigned to polygons.
        center=np.array([p[list(s)].mean(0) for s in f],np.float32);cx,cy,cz=center.T
        ds=np.stack(all_regions(cx,cy,cz),axis=1);dominant=ds.argmin(1)
        cz0=interp(y,1)
        theta=(.5+np.arctan2(x,z-cz0)/math.tau)%1
        body=np.stack((theta,np.clip((y+1.78)/3.20,.002,.998)),axis=1)
        cheeks=np.stack((theta,np.clip((y-.28)/1.14,.002,.998)),axis=1)
        skin=np.full(len(f),'Skin',dtype='U10')
        nose=(cy>1.305)&(abs(cx)<.215)&(abs(cz-.531)<.215)
        skin[nose]='Nose'
        radial=np.sqrt(cx**2+(cz-.53)**2)
        mark=nose&(cy>1.336)&(radial>.070)
        sector=((np.arctan2(cx,cz-.53)-.15+math.pi/8)%(math.tau)/(math.pi/4)).astype(int)
        self.regions=skin.tolist();self.part=np.where(mark,sector,-1).tolist()
        self.uv=[]
        noseuv=np.stack((np.clip(.50+x/.475,.001,.999),np.clip(.50+(z-.53)/.426,.001,.999)),axis=1)
        lmaps={}
        for j,s in enumerate([-1,1]):
            nd=nodes(s)
            for isfore,di in [(True,2+2*j),(False,3+2*j)]:
                if isfore:
                    span=np.clip((y-.12)/.98,0,1);armx=s*(.45+.35*np.minimum(span*2,1));armz=.75-.64*span
                    angle=.5+np.arctan2(x-armx,z-armz)/math.tau
                else:
                    span=np.clip((.78-z)/.74,0,1);armx=s*(.39+.16*span);armz=y+.88
                    angle=.5+np.arctan2(x-armx,armz)/math.tau
                lmaps[di]=np.stack((angle%1,span),axis=1)
        for j,ids in enumerate(f):
            k=skin[j];mm=noseuv if k=='Nose' else body
            zz=mm[list(ids)].copy()
            if k!='Nose' and zz[:,0].max()-zz[:,0].min()>.50:zz[zz[:,0]<.5,0]=1
            self.uv.append([tile_uv(k,u,v) for u,v in zz])
        self.base=len(p)
    def geom(self,v,f,k,uv):
        start=len(self.v);self.v.extend(map(tuple,v));self.f.extend(tuple(start+j for j in pp) for pp in f)
        self.regions.extend([k]*len(f));self.part.extend([-1]*len(f))
        self.uv.extend([[tile_uv(k,u,w) for u,w in zz] for zz in uv])
    def tube(self,points,radii,k,sides=7):
        p=np.asarray(points);rad=np.repeat(radii,len(p)) if np.isscalar(radii) else np.array(radii)
        f=[];uv=[];v=[];previous=(p[1]-p[0]);ref=np.eye(3)[np.argmin(abs(unit(previous)))]
        for i,q in enumerate(p):
            tn=unit(p[min(i+1,len(p)-1)]-p[max(0,i-1)])
            ref=unit(ref-tn*np.dot(ref,tn));bt=np.cross(tn,ref)
            for j in range(sides):
                a=math.tau*j/sides;v.append(q+rad[i]*(math.cos(a)*ref+math.sin(a)*bt))
        ns=len(p)-1
        for i in range(ns):
            for j in range(sides):
                h=(j+1)%sides;f.append((i*sides+j,i*sides+h,(i+1)*sides+h,(i+1)*sides+j))
                uv.append(((j/sides,i/ns),((j+1)/sides,i/ns),((j+1)/sides,(i+1)/ns),(j/sides,(i+1)/ns)))
        for i in (0,ns):
            seq=list(range(sides));seq=seq[::-1] if i==0 else seq
            f.append(tuple(i*sides+j for j in seq));uv.append(tuple((.5+.46*math.cos(j*math.tau/sides),.5+.46*math.sin(j*math.tau/sides)) for j in seq))
        self.geom(v,f,k,uv)
    def claws(self):
        lev=self.level
        for s in [-1,1]:
            nd=nodes(s)
            for fore in [True,False]:
                if fore:
                    px,py,_=nd['palm']
                    digits=[(-.175,.008,.139,.028),(-.074,.032,.328,.077),(.082,.031,.311,.081),(.182,.007,.129,.027)]
                else:
                    px,py,_=nd['hindpaw'];digits=[(-.072,.114,.092,.023),(0,.131,.109,.026),(.074,.109,.087,.021)]
                for idx,(dx,start,length,width) in enumerate(digits):
                    sides=[18,12,9][lev] if width>.04 else [12,9,7][lev];strips=[14,10,7][lev];v=[];f=[];uv=[]
                    for i in range(strips+1):
                        t=i/strips;sz=(.78+.31*math.sin(math.pi*t**.76))*(1-.96*t**2.8)
                        y=py+start+length*t
                        cx=px+s*(dx+.014*t+dx*.13*t*t)
                        zz=(.125 if fore else .075)+.024*math.sin(math.pi*t)-(.103 if fore else .052)*t**1.14
                        thick=(.65 if width>.04 else .84)*width
                        for j in range(sides):
                            a=math.tau*j/sides
                            xx=math.cos(a);hh=math.sin(a)
                            facet=(max(hh,0)**2)*(.08*math.cos(5*a+.36)*math.sin(math.pi*t))
                            z=zz+thick*sz*(hh if hh>0 else .49*hh)+thick*facet
                            v.append((cx+width*sz*xx,y,z))
                    for i in range(strips):
                        for j in range(sides):
                            h=(j+1)%sides;f.append((i*sides+h,i*sides+j,(i+1)*sides+j,(i+1)*sides+h))
                            uv.append((((j+1)/sides,1-i/strips),(j/sides,1-i/strips),(j/sides,1-(i+1)/strips),((j+1)/sides,1-(i+1)/strips)))
                    for i in (0,strips):
                        f.append(tuple(i*sides+j for j in range(sides)));uv.append(tuple((.5+.01*math.cos(j),.2+.01*math.sin(j)) for j in range(sides)))
                    self.geom(v,f,'Nail',uv)
    def features(self):
        lev=self.level
        for s in [-1,1]:
            # Wrinkled sealed lids, rooted in the actual cheek on both sides. The dark lid does not glow.
            for side in [-1,0,1]:
                points=[];rs=[];num=[20,12,8][lev]
                for i in range(num):
                    t=i/(num-1);q=skinpoint(.933+.140*t,.729-.015*t+side*.017*math.sin(math.pi*t),s)
                    n=normal(q);p=q+n*(.005 if side==0 else .006)
                    points.append(p);rs.append((.0028 if side==0 else .009)*(0.17+.83*math.sin(t*math.pi)**.65))
                self.tube(points,rs,'Eyelid',6 if lev else 8)
            # The ears are small cupped structures swept backwards along the side of the head.
            q=skinpoint(.700,.885,s);n=normal(q)
            vaxis=np.array((0.,0.,1.));vaxis=unit(vaxis-np.dot(n,vaxis)*n)
            uaxis=unit(np.cross(n,vaxis))
            if np.dot(uaxis,np.array((0,-1,0)))<0:uaxis=-uaxis
            verts=[];faces=[];uv=[];ns=[26,17,11][lev];nr=[6,5,4][lev]
            for ring in range(nr):
                r=ring/(nr-1)
                for j in range(ns):
                    a=math.tau*j/ns
                    uu=.049*math.cos(a)*(1-.35*math.sin(a));vv=.064*math.sin(a)
                    k=math.exp(-((r-.77)/.20)**2)
                    base=q+uu*r*uaxis+vv*r*vaxis+.016*vaxis*r*(1+math.sin(a))
                    verts.append(base+n*(.003+.027*k-.025*r**8))
            for ring in range(nr-1):
                for j in range(ns):
                    k=(j+1)%ns;faces.append((ring*ns+j,ring*ns+k,(ring+1)*ns+k,(ring+1)*ns+j))
                    uv.append(((j/ns,ring/nr),((j+1)/ns,ring/nr),((j+1)/ns,(ring+1)/nr),(j/ns,(ring+1)/nr)))
            self.geom(verts,faces,'Eyelid',uv)
            # The jaw seam can follow the source creature's jaw weighting.
            points=[];rs=[]
            for t in np.linspace(0,1,[25,15,9][lev]):
                q=skinpoint(.892+.395*t,.454+.020*math.sin(math.pi*t)+.019*t,s)
                points.append(q+normal(q)*.0038);rs.append(.0054*(.19+.81*math.sin(math.pi*t)))
            self.tube(points,rs,'Eyelid',6)
            if lev<2:
                for j in range(5 if lev==0 else 3):
                    q=skinpoint(1.08+.032*j,.581-.013*(j%3),s)
                    n=normal(q);tip=q+np.array((s*(.044+.009*j),-.082-.011*j,.019-.007*j))
                    self.tube([q-n*.003,q+n*.014,(q+tip)*.50+n*.022,tip],[.0017,.0018,.0011,.0003],'GuardHair',4)
    def fur(self):
        lev=self.level
        rng=np.random.default_rng(4711);p=self.body_v;n=self.body_n
        eligible=(n[:,2]>-.19)&(p[:,1]<.68)&(p[:,2]>.32)&((p[:,1]<.27)|(abs(p[:,0])>.45))
        ids=np.flatnonzero(eligible)
        count=[1250,362,62][lev];sel=rng.choice(ids,size=count,replace=True)
        for idx in sel:
            root=p[idx];nn=n[idx];s=1 if root[0]>0 else -1
            if s>0 and .36<root[1]<.77 and .70<root[2]<.98:continue
            direction=np.array((s*rng.uniform(-.22,.13),-1,rng.uniform(-.15,.075)))
            tangent=unit(direction-nn*np.dot(nn,direction));side=unit(np.cross(nn,tangent))
            length=rng.uniform(.028,.086)*(1.23 if root[2]>.87 else .74);half=rng.uniform(.0011,.0032)*(1.21 if lev==2 else 1.)
            center=root-nn*.003;mid=root+tangent*.45*length+nn*.014
            tip=root+tangent*length+nn*rng.uniform(.010,.037)+side*rng.uniform(-.009,.009)
            v=[center+side*half,center-side*half,center+nn*half,
               mid+side*half*.57,mid-side*half*.61,mid+nn*half*.63,tip]
            faces=[(0,1,4,3),(1,2,5,4),(2,0,3,5),(3,4,6),(4,5,6),(5,3,6)]
            offset=rng.uniform(.02,.92);e=rng.uniform(.005,.035)
            uv=[]
            for f in faces:uv.append([(offset+(i%3)*e, .04 if i<3 else .57 if i<6 else .98) for i in f])
            self.geom(v,faces,'GuardHair',uv)
