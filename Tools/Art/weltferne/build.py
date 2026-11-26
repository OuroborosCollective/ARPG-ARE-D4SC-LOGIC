"""Compile authored metre-scale geometry into a Unity stream and an editable glTF 2.0 scene.
Run with Python 3 and NumPy. Local art coordinates are X east, Y north and Z up.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import os
import shutil
import tempfile

import numpy as np
import architecture
import grounding
import mesh_format
import landscape
import underworld
from world_catalog import catalog

HERE=Path(__file__).resolve().parent
PALETTE = {
    "basalt":"#434552","slate":"#566074","stone":"#777578","trim":"#a19889",
    "ash":"#a29f99","iron":"#56565c","wood":"#6e5144","roof":"#4b4d5d",
    "moss":"#5b665d","ember":"#f47843","amber":"#ffd088","bone":"#a99f8e",
    "mineral":"#6e92a2","dark":"#292c38","fog":"#999594","water":"#4b6873",
    "bark":"#53484b","leaf":"#3d4e52",
}
MATS=list(PALETTE)
COLOR=np.array([tuple(bytes.fromhex(h[1:])) for h in PALETTE.values()],dtype=float)
INDOOR={'crypt','nest','bear','rift','vault'}
CLOSE = {"old_crypt":(75,66,72),"hidden_crypt":(77,71,79),"nest":(74,63,70),"bear":(67,72,77),
         "rift":(63,74,86),"vault":(65,72,82),"kiln":(151,119,111),"forest":(128,135,139),
         "future":(123,137,126),"store":(142,132,131),"ridge":(132,135,146)}

def sub(a,b): return tuple(u-v for u,v in zip(a,b))
def cross(a,b): return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def unit(a):
    n=math.sqrt(sum(x*x for x in a))
    if n < 1e-10: raise ValueError("zero-length axis")
    return tuple(x/n for x in a)

class Builder:
    def __init__(self):
        self.tris=[]
        self.tags=[]
        self.ranges=[]
    def face(self,points,mat):
        idx=MATS.index(mat)
        for i in range(1,len(points)-1):
            self.tris.append((points[0],points[i],points[i+1]))
            self.tags.append(idx)
    def box(self,center,size,mat):
        x,y,z=center; w,d,h=[v/2 for v in size]
        if min(w,d,h)<=0: return
        p=[(x+i*w,y+j*d,z+k*h) for k in (-1,1) for i,j in [(-1,-1),(1,-1),(1,1),(-1,1)]]
        for q in ((3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)):
            self.face([p[i] for i in q],mat)
    def basis(self,a,end):
        w=unit(sub(end,a)); u=unit(cross((0.,0.,1.) if abs(w[2])<.92 else (0.,1.,0.),w))
        return u,cross(w,u)
    def beam(self,a,end,width,mat,depth=None):
        if a==end or width<=0:return
        u,v=self.basis(a,end); w=width/2; h=(depth if depth is not None else width)/2
        corners=[(1,1),(-1,1),(-1,-1),(1,-1)]
        loops=[[tuple(c[j]+i*w*u[j]+k*h*v[j] for j in range(3)) for i,k in corners] for c in (a,end)]
        self.connect(loops[0],loops[1],mat)
    def connect(self,lo,hi,mat):
        self.face(lo[::-1],mat);self.face(hi,mat)
        for i in range(len(lo)):
            j=(i+1)%len(lo)
            self.face([lo[i],lo[j],hi[j],hi[i]],mat)
    def taper(self,a,end,r0,r1,mat,sides=8):
        if a==end or max(r0,r1)<=0:return
        u,v=self.basis(a,end)
        angles=[2*math.pi*i/sides for i in range(sides)]
        def ring(c,r):
            return [tuple(c[j]+r*(math.cos(t)*u[j]+math.sin(t)*v[j]) for j in range(3)) for t in angles]
        lo,hi=ring(a,r0),ring(end,r1)
        if min(r0,r1)>1e-9:self.connect(lo,hi,mat);return
        if r0>0:
            self.face(lo[::-1],mat)
            for i in range(sides): self.face([lo[i],lo[(i+1)%sides],end],mat)
        else:
            self.face(hi,mat)
            for i in range(sides): self.face([a,hi[(i+1)%sides],hi[i]],mat)
    def ellipsoid(self,center,radii,mat,rings=5,sides=10):
        x,y,z=center;rx,ry,rz=radii
        circles=[]
        for j in range(1,rings):
            a=math.pi*(j/rings-.5)
            circles.append([(x+rx*math.cos(a)*math.cos(2*math.pi*i/sides),y+ry*math.cos(a)*math.sin(2*math.pi*i/sides),z+rz*math.sin(a)) for i in range(sides)])
        for i in range(sides):
            k=(i+1)%sides
            self.face([(x,y,z-rz),circles[0][k],circles[0][i]],mat)
            for lo,hi in zip(circles,circles[1:]):self.face([lo[i],lo[k],hi[k],hi[i]],mat)
            self.face([circles[-1][i],circles[-1][k],(x,y,z+rz)],mat)
def small(b,k):
    if k=='standing_rock':
        for i,(x,y,h,r,lean) in enumerate([(-2,0,8,2.1,-1.5),(1,1,11,2.8,1.6),(3,0,5.7,1.65,.3),(-3,1,3.0,1.3,-.7)]):
            b.taper((x,y,-.45),(x+lean,y+.6,h),r,.45*r,['slate','basalt','stone','slate'][i],7)
            b.taper((x+lean,y+.6,h),(x+lean-.3,y+.7,h+.9),r*.45,.12,'ash',7)
    elif k=='broken_pier':
        for i in range(8):
            s=2.7-i*.115;off=.12*math.sin(i*2.4)
            b.box((off,0,.6+1.18*i),(s,s*1.08,1.14),'stone' if i%3 else 'slate')
            if i in (0,3,6):b.box((0,0,1.13+1.18*i),(s+1,s+.85,.23),'trim')
        b.taper((-.55,0,8.65),(-.85,.3,12.9),.89,.43,'basalt',5)
        for x in [-1,1]:b.beam((x,-1.1,.9),(x*.74,-.99,7.4),.28,'trim')
    elif k=='dead_oak':
        import random
        rr=random.Random(313)
        def grow(p,length,r,angle,lean,tier):
            q=(p[0]+math.sin(angle)*length*lean,p[1]+math.cos(angle)*length*lean,p[2]+length*(1-.36*lean))
            b.taper(p,q,r,r*.61,'bark',7 if tier>1 else 5)
            if tier:
                for side in [-1,1]:
                    grow(q,length*(.66+rr.random()*.12),r*.62,angle+side*(.51+rr.random()*.75),min(1.05,lean+.24),tier-1)
            return q
        q=grow((0,0,-.35),4.85,.86,.32,.10,4)
        grow((.12,0,3.55),3.6,.47,-1.64,.67,3)
        for i in range(7):
            a=i*.91;b.taper((0,0,.5),(2*math.sin(a),2*math.cos(a),-.18),.52,.08,'bark',6)
    elif k=='cairn':
        for i in range(7):
            b.ellipsoid((math.sin(i)*.2,math.cos(i)*.2,.25+i*.64),(2.25-i*.23,1.6-i*.14,.45),'slate' if i%2 else 'ash',4,9)
        b.taper((0,0,4.3),(.18,0,6.2),.24,.15,'iron',6)
    else:raise KeyError(k)

def make_landmark(k):
    b=Builder()
    if k in ("bell","kiln","village","mill","granary","toll","crypt_crown","abbey","aqueduct","road_shrine"):
        architecture.draw(b,k)
    elif k in ('court_wall','forest_margin','mill_bank','freight_margin'):
        import exterior_dressing
        exterior_dressing.draw(b,k)
    elif k in ('ossuary_wall','mineral_wall','cistern_wall'):
        import interior_dressing
        interior_dressing.draw(b,k)
    elif k in ('crypt_arch','sealed_arch','nest','bear_den','side_rift','mill_vault','king','guardian'):
        underworld.draw(b,k)
    else:small(b,k)
    return b

def compile_all(out, models, studies, only, texture_dir=None):
    out.mkdir(parents=True,exist_ok=True);models.mkdir(parents=True,exist_ok=True);studies.mkdir(parents=True,exist_ok=True)
    texture_dir=texture_dir or ((HERE/"textures") if (HERE/"textures").is_dir() else (out/"Detail"))
    assets={};report=[]
    for rec in catalog:
        key=rec['id']
        if only and key not in only:continue
        b=Builder()
        seed=int.from_bytes(hashlib.sha256(('weltferne/1/'+key).encode()).digest()[:4],'little')
        marks = rec['landmarks'] + rec['dressing']
        for mark in marks:
            k=mark['kind']
            if k not in assets:assets[k]=make_landmark(k)
        landscape.terrain_surface(b, rec['style'], seed)
        plan = grounding.prepare(b, marks, assets)
        details = landscape.terrain_details(b, rec['style'], seed, plan.surface.height, plan.blocked)
        b.terrain_count = len(b.tris)
        plan.append(b, assets)
        xyz,col,types=mesh_format.shaders(b,rec,MATS,COLOR,CLOSE,rec['style'] in INDOOR)
        dest=(studies if rec['planned'] else out)/(key+'.bytes')
        mesh_format.stream(dest,xyz,col,types)
        mesh_format.glb(models/(key+'.glb'),xyz,col,types,rec,texture_dir)
        radius=np.linalg.norm(xyz[:,:,:2],axis=2)
        row = dict(id=key,triangles=len(xyz),closestVertex=float(radius.min()),farthestVertex=float(radius.max()),resourceBytes=dest.stat().st_size,sha256=hashlib.sha256(dest.read_bytes()).hexdigest(),planned=rec['planned'])
        row.update(surfaceTriangles=b.landscape_surface_count, terrainTriangles=b.terrain_count,
                   details=details, grading=plan.report(), objects=[
                       dict(kind=p.kind, bearing=p.bearing, distance=p.distance, originalDistance=p.original_distance, scale=p.scale,
                            turn=p.turn, x=p.x, y=p.y, originZ=p.origin_z,
                            firstTriangle=p.triangle_start, triangleCount=p.triangle_count)
                       for p in plan.placements])
        # Reopen the exported mesh: two matching file formats can still share one placement error.
        from verify_grounding import require_scene_contacts
        from verify_landscape_contacts import check_details
        require_scene_contacts(rec, row, dest)
        check_details(rec, row, dest)
        report.append(row)
        print(f"{key:14} {len(xyz):6} tris  {radius.min():.1f}..{radius.max():.1f} m",flush=True)
    return report


# These three released scenes reproduce both signs of the placement bug and a stilted floor.
CONTACT_REGRESSIONS = {'bellcourt': 'village', 'freight': 'village', 'ash_road': 'granary'}

def compact_contact_index(report):
    index = {}
    for row in report:
        kind = CONTACT_REGRESSIONS.get(row['id'])
        if kind:
            member, = [entry for entry in row['objects'] if entry['kind'] == kind]
            index[row['id']] = dict(surfaceTriangles=row['surfaceTriangles'], member=member)
    if set(index) != set(CONTACT_REGRESSIONS):
        raise ValueError('A contact index requires all three regression scenes')
    return index


def publish(source, target):
    target.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(dir=target.parent, delete=False) as tmp:
        temporary = Path(tmp.name)
    try:
        shutil.copyfile(source, temporary)
        temporary.chmod(0o644)
        os.replace(temporary, target)
    finally:
        temporary.unlink(missing_ok=True)


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);p.add_argument('--models',type=Path,required=True);p.add_argument('--studies',type=Path,required=True);p.add_argument('--report',type=Path,required=True);p.add_argument('--only',nargs='*');p.add_argument('--textures',type=Path)
    p.add_argument('--contact-index', type=Path, help='Compact index of three historical regressions for repository preflight')
    a=p.parse_args()
    textures = a.textures or (HERE/'textures' if (HERE/'textures').is_dir() else a.out/'Detail')
    with tempfile.TemporaryDirectory(prefix='weltferne-bau-') as tmp:
        stage = Path(tmp)
        report = compile_all(stage/'active', stage/'models', stage/'studies', a.only, textures)
        if a.contact_index:
            (stage/'contacts.json').write_text(json.dumps(compact_contact_index(report),indent=2)+'\n')
        (stage/'report.json').write_text(json.dumps(report,separators=(',',':'))+'\n')
        for source, target in ((stage/'active',a.out),(stage/'studies',a.studies),(stage/'models',a.models)):
            for path in source.iterdir():
                publish(path, target/path.name)
        if a.contact_index:
            publish(stage/'contacts.json', a.contact_index)
        publish(stage/'report.json', a.report)
