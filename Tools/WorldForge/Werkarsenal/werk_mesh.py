"""Hand-authored silhouette tools. Each finished item is a single UV-atlased mesh."""
import math
from math import pi,tau,sin,cos
import bpy,bmesh
from mathutils import Vector
from mathutils.geometry import tessellate_polygon
from werk_atlas import tile_uv

def clamp(t):return max(0.,min(1.,t))

class Piece:
    def __init__(self,key):
        self.key=key;self.v=[];self.faces=[];self.uv=[];self.smooth=[]
    def mesh(self,vertices,faces,mat,uv=None,smooth=False):
        start=len(self.v);v=[Vector(p) for p in vertices];self.v.extend(v)
        for i,f in enumerate(faces):
            self.faces.append(tuple(start+j for j in f))
            m=mat if isinstance(mat,str) else mat[i]
            if uv is None:
                n=(v[f[1]]-v[f[0]]).cross(v[f[2]]-v[f[0]])
                omit=max(range(3),key=lambda j:abs(n[j]));axes=[j for j in range(3) if j!=omit]
                # Preserve texture density with a separate patch for every surface. Front plates and
                # internal barrel walls have different axes and independent edge padding.
                ext=[(min(v[j][k] for j in f),max(v[j][k] for j in f)) for k in axes]
                uvface=[]
                for j in f:
                    uvface.append(tuple(.04+.92*clamp(.5+(v[j][k]-(a+b)*.5)/max(.24,b-a+.002)) for k,(a,b) in zip(axes,ext)))
            else:uvface=uv[i]
            self.uv.append([tile_uv(m,u,w) for u,w in uvface])
            self.smooth.append(smooth if isinstance(smooth,bool) else smooth[i])
    def tube(self,path,radius,mat='Eisen',sides=8,closed=False,aspect=1.0):
        p=list(map(Vector,path));n=len(p);r=[radius]*n if isinstance(radius,(float,int)) else radius
        v=[];f=[];uv=[]
        ref=min((Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1))),key=lambda vec:abs(vec.dot((p[1]-p[0]).normalized())))
        for i,q in enumerate(p):
            prev=p[(i-1)%n] if closed or i else p[0]
            nxt=p[(i+1)%n] if closed or i<n-1 else p[-1]
            tn=(nxt-prev).normalized();ref=(ref-ref.dot(tn)*tn).normalized()
            if ref.length<.1:ref=tn.orthogonal().normalized()
            bt=tn.cross(ref)
            for j in range(sides):
                a=tau*j/sides;v.append(q+(ref*cos(a)+bt*sin(a)*aspect)*r[i])
        stops=n if closed else n-1
        for i in range(stops):
            i2=(i+1)%n
            for j in range(sides):
                j2=(j+1)%sides;f.append((i*sides+j,i*sides+j2,i2*sides+j2,i2*sides+j))
                uv.append(((i/stops,j/sides),(i/stops,(j+1)/sides),((i+1)/stops,(j+1)/sides),((i+1)/stops,j/sides)))
        if not closed:
            for a in (0,n-1):
                f.append(tuple(a*sides+j for j in range(sides)));uv.append(tuple((.5+.46*cos(j*tau/sides),.5+.46*sin(j*tau/sides)) for j in range(sides)))
        self.mesh(v,f,mat,uv,True)
    def loop(self,c,rx,rz,r,mat='Eisen',axis='front',n=24):
        x,y,z=c
        pts=[(x+rx*cos(a), y+(rz*sin(a) if axis=='top' else 0),z+(0 if axis=='top' else rz*sin(a))) for a in [tau*j/n for j in range(n)]]
        self.tube(pts,r,mat,7,True)
    def rivet(self,x,y,z,rad=.009,mat='Messing'):
        self.tube([(x,y-.004,z),(x,y,z),(x,y+rad*.43,z),(x,y+rad*.63,z)],[rad*.85,rad,rad*.87,rad*.46],mat,8)
    def plate(self,coords,mat,y=.02,depth=.025,edge='Kante',bevel=.06,crown=.01):
        # Coordinates in x/z; front faces positive Y, as measured in Unity's FBX importer.
        n=len(coords);cx=sum(p[0] for p in coords)/n;cz=sum(p[1] for p in coords)/n
        def profile(l):
            if callable(y):return y(*l)
            return y
        v=[];f=[];ms=[]
        for scale,dy in ((.99,-depth),(1.,-min(.004,.25*depth)),(1-bevel,crown)):
            for x,z in coords:
                nx=cx+(x-cx)*scale;nz=cz+(z-cz)*scale;v.append((nx,profile((nx,nz))+dy,nz))
        for j in range(n):
            k=(j+1)%n
            for row,m in ((0,mat),(1,edge)):
                f.append((row*n+j,row*n+k,(row+1)*n+k,(row+1)*n+j));ms.append(m)
        inds=tessellate_polygon([[Vector((x,z,0)) for x,z in coords]])
        for tri in inds:
            f.append(tuple(tri));ms.append(mat);f.append(tuple(2*n+i for i in tri));ms.append(mat)
        self.mesh(v,f,ms)
    def blade(self,stations,mat,edge='Kante',rib='Eisen',rough=.0,depth=.017):
        # Section depths and edge colours express the folds under an ordinary game camera.
        v=[];f=[];ms=[];uv=[]
        cross=[-1.,-.78,-.16,0,.18,.77,1.,.70,0,-.70]
        deps=[0,.43,.75,1,.77,.45,0,-.41,-.73,-.40]
        for i,(z,x,l,r) in enumerate(stations):
            p=i/(len(stations)-1);taper=.33+ .67*(1-p**2)
            for j,w in enumerate(cross):
                v.append((x+(l if w<0 else r)*w,depth*deps[j]*taper,z+rough*sin(4*i+j)*(.20+abs(w)*.80)))
        for i in range(len(stations)-1):
            for j in range(len(cross)):
                k=(j+1)%len(cross);n=len(cross);f.append((i*n+j,i*n+k,(i+1)*n+k,(i+1)*n+j))
                ms.append(edge if j in (0,5,6,9) else rib if j in (2,3) else mat)
                uv.append(((j/n,i/(len(stations)-1)),((j+1)/n,i/(len(stations)-1)),((j+1)/n,(i+1)/(len(stations)-1)),(j/n,(i+1)/(len(stations)-1))))
        for i in (0,len(stations)-1):
            f.append(tuple(i*len(cross)+j for j in range(len(cross))));ms.append(mat);uv.append(tuple((.5+.45*cos(tau*j/len(cross)),.5+.45*sin(tau*j/len(cross))) for j in range(len(cross))))
        self.mesh(v,f,ms,uv)
    def shell(self,rows,mat,span=pi,edge='Leder',depth=.008,phase=0,n=30):
        v=[];f=[];ms=[];uv=[];closed=span>=pi-.001
        width=n if closed else n+1
        for layer in (0,1):
            for row in rows:
                z,rx,ry,*other=row;lean=other[0] if other else 0
                for i in range(width):
                    a=phase-span+2*span*i/n
                    v.append(((rx-depth*layer)*sin(a),lean+(ry-depth*layer)*cos(a),z))
        h=len(rows);m=width*h
        def face(ids,kind,coords):f.append(ids);ms.append(kind);uv.append(coords)
        for j in range(h-1):
            for i in range(n):
                k=(i+1)%width
                for k0 in (0,m):
                    face((k0+j*width+i,k0+j*width+k,k0+(j+1)*width+k,k0+(j+1)*width+i),mat if k0==0 else edge,
                         ((i/n,j/(h-1)),((i+1)/n,j/(h-1)),((i+1)/n,(j+1)/(h-1)),(i/n,(j+1)/(h-1))))
        for j in (0,h-1):
            for i in range(n):
                k=(i+1)%width;f0=j*width
                face((f0+i,f0+k,m+f0+k,m+f0+i),edge,((i/n,0),((i+1)/n,0),((i+1)/n,1),(i/n,1)))
        if not closed:
            for i in (0,n):
                for j in range(h-1):
                    k=j*width+i;face((k,k+width,m+k+width,m+k),edge,((0,0),(1,0),(1,1),(0,1)))
        self.mesh(v,f,ms,uv,True)
    def part(self,key=None):
        name=key or self.key;mesh=bpy.data.meshes.new(name);mesh.from_pydata(self.v,[],self.faces);mesh.update()
        uv=mesh.uv_layers.new(name='Werkatlas')
        for poly,coords,smooth in zip(mesh.polygons,self.uv,self.smooth):
            poly.use_smooth=smooth
            for k,p in zip(poly.loop_indices,coords):uv.data[k].uv=p
        bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(mesh);bm.free();mesh.update()
        ob=bpy.data.objects.new(name,mesh);bpy.context.scene.collection.objects.link(ob)
        return ob

def ellipse(cx,cz,rx,rz,n=24):
    return [(cx+rx*cos(tau*i/n),cz+rz*sin(tau*i/n)) for i in range(n)]

def transform(m,sign=1,dx=0):
    if sign!=1 or dx:m.v=[Vector((dx+v.x*sign,v.y,v.z)) for v in m.v]

def rail(m,coords,depth,mat,r):
    m.tube([(x,y if callable(depth) else depth,z) for x,z in coords for y in ([depth(x,z)] if callable(depth) else [depth])],r,mat,7)
