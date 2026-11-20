"""Headless Blender model of the Grubenlicht, including all three game detail levels."""
from pathlib import Path
import math
import sys
import json
import bpy
import numpy as np
from mathutils import Vector
from math import sin,cos,pi,tau

base=Path(sys.argv[sys.argv.index('--')+1]).resolve()
out=base/'out'; work=base/'work'
out.mkdir(exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene
scene.unit_settings.system='METRIC'

def lin(c):
    return tuple(((k/255+.055)/1.055)**2.4 if k/255>.04045 else k/3294.6 for k in c)
def material(name, rough, metal=0, glow=None):
    m=bpy.data.materials.new(name);m.use_nodes=True
    n=m.node_tree.nodes;lk=m.node_tree.links;pr=n.get('Principled BSDF')
    pr.inputs['Roughness'].default_value=rough;pr.inputs['Metallic'].default_value=metal
    attr=n.new('ShaderNodeVertexColor');attr.layer_name='Patina'
    lk.new(attr.outputs['Color'],pr.inputs['Base Color'])
    if glow:
        pr.inputs['Emission Color'].default_value=(*lin(glow[0]),1);pr.inputs['Emission Strength'].default_value=glow[1]
    return m
mats=[material('01 | Ascheton und Brennspuren',.77),material('02 | Hammereisen',.51,.78),
      material('03 | Messing, blank gerieben',.38,.84),material('04 | Oelgarn',.91),
      material('05 | Kohledocht',.84),material('06 | Kupferdocht • Grünspan',.38,0,((119,221,92),2.1))]
palette=[(150,126,92),(63,67,66),(147,111,67),(65,53,42),(32,36,29),(101,162,72)]
clay=mats[0];pr=clay.node_tree.nodes.get('Principled BSDF')
for name,target in [('Grubenlicht_Ton.png','Base Color'),('Grubenlicht_Poren.png','Normal')]:
    im=bpy.data.images.load(str(out/name));im.pack()
    tex=clay.node_tree.nodes.new('ShaderNodeTexImage');tex.image=im
    if target=='Normal':
        im.colorspace_settings.name='Non-Color';nrm=clay.node_tree.nodes.new('ShaderNodeNormalMap')
        nrm.inputs['Strength'].default_value=.66
        clay.node_tree.links.new(tex.outputs['Color'],nrm.inputs['Color']);clay.node_tree.links.new(nrm.outputs[0],pr.inputs[target])
    else:clay.node_tree.links.new(tex.outputs['Color'],pr.inputs[target])
all_lods=[]

class Sculpt:
    def __init__(self):
        self.vertices=[];self.faces=[];self.mat=[];self.colors=[]
    def add(self,points,faces,slot,tint=None):
        index=len(self.vertices)
        points=np.asarray(points,dtype=float)
        if tint is None:
            x,y,z=points.T
            grain=.94+.085*np.sin(28*x+9*z)*np.sin(19*y+7*z)+.055*np.cos(4*z-8*x)
            if slot in (1,2):grain+=.17*(np.sin(15*z-2*x)**12)
            rgb=np.array(palette[slot])[None,:]*grain[:,None]
            c=np.clip(rgb/255,0,1);tint=np.where(c<=.04045,c/12.92,((c+.055)/1.055)**2.4)
        self.vertices.extend(points.tolist());self.colors.extend(np.asarray(tint).tolist())
        self.faces.extend(tuple(index+int(j) for j in f) for f in faces);self.mat.extend([slot]*len(faces))
    def tube(self,pts,rr,slot,sections=9,closed=False):
        p=np.array(pts,float)
        if np.isscalar(rr):rr=np.full(len(p),rr)
        adj=np.roll(p,-1,axis=0)-np.roll(p,1,axis=0)
        if not closed:adj[0]=p[1]-p[0];adj[-1]=p[-1]-p[-2]
        adj/=np.maximum(1e-10,np.linalg.norm(adj,axis=1)[:,None])
        axis=np.eye(3)[np.argmin(np.abs(adj[0]))];v=axis.copy()
        pts=[];faces=[]
        for center,t,r in zip(p,adj,rr):
            v=v-np.dot(v,t)*t;v/=max(np.linalg.norm(v),1e-10);b=np.cross(t,v)
            for j in range(sections):
                a=tau*j/sections;pts.append(center+r*(v*cos(a)+b*sin(a)))
        for k in range(len(p) if closed else len(p)-1):
            k2=(k+1)%len(p)
            for j in range(sections):faces.append((k*sections+j,k*sections+(j+1)%sections,k2*sections+(j+1)%sections,k2*sections+j))
        if not closed:
            faces.extend([tuple(reversed(range(sections))),tuple((len(p)-1)*sections+j for j in range(sections))])
        self.add(pts,faces,slot)
    def oval(self,center,r1,r2,thick,u,v,slot,sides=24,cross=8):
        center=np.array(center);u=np.array(u);v=np.array(v)
        self.tube([center+r1*cos(a)*u+r2*sin(a)*v for a in np.arange(sides)*tau/sides],thick,slot,cross,True)
    def ball(self,center,scale,slot,n=14,rings=8):
        pts=[(center[0],center[1],center[2]+scale[2])];faces=[]
        for k in range(1,rings):
            a=pi*k/rings
            for j in range(n):
                t=tau*j/n;pts.append((center[0]+scale[0]*sin(a)*cos(t),center[1]+scale[1]*sin(a)*sin(t),center[2]+scale[2]*cos(a)))
        bot=len(pts);pts.append((center[0],center[1],center[2]-scale[2]))
        for j in range(n):
            h=(j+1)%n;faces.extend([(0,1+j,1+h),(bot,1+(rings-2)*n+h,1+(rings-2)*n+j)])
            for k in range(rings-2):
                i=1+k*n;faces.append((i+j,i+n+j,i+n+h,i+h))
        self.add(pts,faces,slot)
    def relief_tag(self):
        c=np.array([.53,-.31,.78]);us=np.array([.142,.016,0]);vs=np.array([.028,.014,.177])
        loop=[(-.79,-.96),(.64,-1),(.98,-.56),(1,.67),(.64,1),(-.67,.93),(-1,.51),(-.97,-.77)]
        poly=[c+u*us+v*vs for u,v in loop];verts=poly+[p+[0,.043,0] for p in poly];n=len(poly)
        self.add(verts,[tuple(range(n)),tuple(reversed(range(n,2*n)))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],2)
        polyline=lambda uv:[c+u*us+v*vs+[0,-.013,0] for u,v in uv]
        self.tube(polyline([(-.55,.46),(.01,.80),(.55,.44)]),.012,1,6)
        for x in [-.41,0,.41]:self.tube(polyline([(x-.03,.23),(x+.03,-.43)]),.013,1,6)
        self.ball(c+.76*vs+[0,-.02,0],(.029,.020,.029),1,8,5)
    def object(self,name):
        # The top of the handle is the origin. Coordinates in the game file are metres.
        pts=np.array(self.vertices);pts*=.14;pts[:,2]-=.14*2.045
        me=bpy.data.meshes.new(name);me.from_pydata(pts.tolist(),[],self.faces);me.update()
        for m in mats:me.materials.append(m)
        me.polygons.foreach_set('material_index',np.asarray(self.mat,dtype=np.int32))
        me.polygons.foreach_set('use_smooth',np.ones(len(me.polygons),dtype=np.bool_))
        col=me.color_attributes.new(name='Patina',type='FLOAT_COLOR',domain='POINT')
        rgba=np.column_stack([self.colors,np.ones(len(self.colors))]).astype('float32')
        col.data.foreach_set('color',rgba.ravel());me.color_attributes.active_color=col
        uv=me.uv_layers.new(name='Keramik').data
        for face in me.polygons:
            vi=[me.loops[i].vertex_index for i in face.loop_indices]
            if face.material_index==0:
                arr=[self.vertices[j] for j in vi]
                uu=[(math.atan2(y-.015,x)/tau+.5)%1 for x,y,z in arr]
                if max(uu)-min(uu)>.5:uu=[u+1 if u<.5 else u for u in uu]
                vv=[min(.998,max(.002,(z+1.08)/2.08)) for x,y,z in arr]
            else:
                uu=[min(.998,max(.002,.45+.35*math.sin(self.vertices[j][0]*22+self.vertices[j][2]*19))) for j in vi]
                vv=[.5+.45*math.sin(self.vertices[j][1]*16+self.vertices[j][2]*6) for j in vi]
            for i,u,v in zip(face.loop_indices,uu,vv):uv[i].uv=(u,v)
        ob=bpy.data.objects.new(name,me);scene.collection.objects.link(ob)
        return ob

def build(level,tag):
    s=Sculpt();data=np.load(work/f'shell-{tag}.npz');s.add(data['v'],data['f'],0,data['c'])
    n=[42,25,14][level];ss=[10,7,5][level]
    for side in (-1,1):
        # The carrying yoke is forged around the ear bosses. It widens before it reaches the handle.
        t=np.linspace(0,1,n)
        p=[(side*(.57-.53*j**1.78),.110+.02*sin(pi*j),.32+1.00*j+.11*sin(pi*j)) for j in t]
        s.tube(p,.034,1,ss)
        s.oval((side*.59,.10,.29),.135,.118,.027,(0,1,0),(0,0,1),2,n//2+6,ss)
        s.ball((side*.579,-.023,.30),(.072,.046,.073),2,ss+3,6)
        # An irregular lower hoop visible around the shell; it holds the bottom cup.
    angles=np.arange(n)*tau/n
    s.tube([(.554*cos(a),-.005+.487*sin(a),.14+.075*cos(a-.25)+.045*sin(2*a)) for a in angles],.027,1,ss,True)
    if level!=2:
        for a in [0.10,.52,1.25,2.6,3.6,4.05,5.30,5.9]:
            s.ball((.563*cos(a),-.010+.494*sin(a),.14+.075*cos(a-.25)+.045*sin(2*a)),(.035,.030,.034),2,9,5)
    # The star of eight elongated clay/iron plates is an artificer's imitation of the animal's nose.
    for j in range(8):
        a=tau*j/8+.17;length=1.+.12*cos(3*a+.2);pp=[];rr=[]
        for t in np.linspace(0,1,[11,8,5][level]):
            radius=(.052+.106*t)*length
            pp.append((sin(a)*radius,-.785+.052*t+.032*t*t,-.345+cos(a)*radius))
            rr.append(.015+.018*sin(pi*t)-.009*t)
        s.tube(pp,rr,2,ss-1)
    s.ball((0,-.789,-.344),(.048,.030,.045),5,12,7)
    # A single core sits within the hollow shell. Grilles cross the openings; the clay eye rim stays broad.
    s.ball((0,-.107,.10),(.46,.328,.432),4,n,12)
    for side in [-1,1]:
        s.ball((side*.373,-.299,.035),(.232,.136,.140),5,n//2+4,9)
        for j in [-1,0,1]:
            path=[(side*(.357+.060*j+.075*t),-.445-.04*sin(pi*t),-.109+.293*t-.025*j) for t in np.linspace(0,1,10)]
            s.tube(path,.015 if j else .019,1,max(5,ss-2))
    # A casting seam with three repair staples remains visible on the forehead.
    if level<2:
        t=np.linspace(0,1,20)
        s.tube([(.008+.036*sin(15*a)-.045*a,-.440-.056*sin(pi*a),.748-.475*a) for a in t],.010,4,5)
        for t in [.23,.48,.72]:
            xx=.008+.036*sin(15*t)-.045*t;zz=.748-.475*t;yy=-.45-.064*sin(pi*t)
            s.tube([(xx-.055,yy+.02,zz-.014),(xx-.047,yy-.009,zz+.006),(xx+.035,yy-.017,zz+.031),(xx+.046,yy+.013,zz+.045)],.0105,2,7)
    # Raised mineral eyebrows are rubbed black where fingers lift the hot body.
    for side in (-1,1):
        t=np.linspace(.06,2.77,max(9,n//2))
        s.tube([(side*(.353+.239*cos(a)),-.578+.089*abs(cos(a)),.002+.201*sin(a)-.052*cos(a)) for a in t],
               [.012+.017*sin((a-.06)/2.71*pi) for a in t],1,ss-1)
    # The wrapped handle and tether are solid links with visible alternating planes.
    s.oval((0,.115,1.32),.115,.087,.028,(1,0,0),(0,0,1),2,n//2+4,ss)
    for j in range(3):
        a=[.45,1.55,.18][j]
        s.oval((.008*j,.115,1.433+j*.137),.061,.102,.020,(cos(a),sin(a),0),(0,0,1),1,n//2+6,ss)
    s.oval((.023,.113,1.897),.101,.148,.021,(.975,.22,0),(0,0,1),2,n//2+7,ss)
    if level<2:
        for j in range(9):
            a=.21+j*.345;cx=.023+.101*.975*cos(a);cy=.113+.101*.22*cos(a);cz=1.897+.148*sin(a)
            s.oval((cx,cy,cz),.033,.028,.009,(0,1,0),(cos(a),0,sin(a)),3,11,5)
    s.relief_tag()
    s.tube([(.29,.08,1.09),(.42,-.085,1.13),(.51,-.24,1.02),(.52,-.295,.934)],.014,3,max(6,ss-1))
    # Below the worn cradle hang a fired-clay plummet and two short chains of service tokens.
    for j,side in enumerate([-1,1,-1]):
        x=[-.332,.305,-.474][j];y=[-.06,-.19,.15][j];top=[-.28,-.29,-.16][j]
        count=[3,2,2][j]
        for k in range(count):
            a=.23+1.0*(k%2)+.12*j
            s.oval((x+.026*side*k,y-.004*k,top-.102*k),.044,.077,.0135,(cos(a),sin(a),0),(0,0,1),1,n//2,ss-2)
        z=top-.102*(count-1)-.10;pm=[]
        for t in np.linspace(0,1,[15,10,6][level]):pm.append((x+side*(.06+.04*t)+.04*sin(pi*t),y-.03*sin(pi*t),z-.36*t))
        s.tube(pm,[.014+.066*sin(pi*t)**.7-.008*t for t in np.linspace(0,1,len(pm))],2 if j==1 else 0,ss+2)
        s.oval((x+.06*side,y,z-.04),.054,.056,.011,(1,0,0),(0,1,0),1,n//2,ss-2)
    return s.object('Grubenlicht_LOD'+str(level))

for i,tag in enumerate(['near','mid','far']):all_lods.append(build(i,tag))
# Export each portable detail level independently for predictable viewers and game bindings.
def select(items):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in items:ob.select_set(True)
    bpy.context.view_layer.objects.active=items[0]

counts=[]
for level,ob in enumerate(all_lods):
    select([ob]);ob.data.calc_loop_triangles();counts.append(len(ob.data.loop_triangles))
    filename='Grubenlicht.glb' if level==0 else f'Grubenlicht_LOD{level}.glb'
    bpy.ops.export_scene.gltf(filepath=str(out/filename),use_selection=True,export_format='GLB',export_materials='EXPORT',export_vertex_color='MATERIAL')
select(all_lods)
bpy.ops.export_scene.fbx(filepath=str(out/'Grubenlicht.fbx'),use_selection=True,object_types={'MESH'},apply_unit_scale=True,axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,colors_type='SRGB')
# The authoring file opens on a single clean object; the camera and studio objects are easy to hide.
for ob in all_lods[1:]:ob.hide_render=True;ob.hide_set(True)
select([all_lods[0]])
all_lods[0]['attachment']='Aufhängung am Ursprung';all_lods[0]['source']='Ashen Crypt — Grubenlicht'
scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=40;scene.cycles.use_denoising=False
scene.world=bpy.data.worlds.new('Schieferlicht');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.16,.20,.24,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.42
def aim(ob,point):ob.rotation_euler=(Vector(point)-ob.location).to_track_quat('-Z','Y').to_euler()
def area(name,loc,watts,size,color):
    d=bpy.data.lights.new(name,'AREA');d.energy=watts;d.shape='DISK';d.size=size;d.color=color
    ob=bpy.data.objects.new(name,d);scene.collection.objects.link(ob);ob.location=loc;aim(ob,(0,0,-.225))
area('Kalklicht',(-.46,-.72,.32),20,.4,(1,.86,.68))
area('Kaltes Streiflicht',(.35,.28,.18),26,.28,(.60,.78,1))
area('Weiches Fuelllicht',(.46,-.28,-.1),6,.32,(.80,1,.90))
area('Mittagslicht',(-.23,.09,-.48),3,.25,(.88,.58,.36))
d=bpy.data.lights.new('Dochtlicht','POINT');d.energy=.045;d.color=(.37,1,.196);d.shadow_soft_size=.032
ob=bpy.data.objects.new('Dochtlicht',d);scene.collection.objects.link(ob);ob.location=(0,-.099,-.27)
camdata=bpy.data.cameras.new('Werkansicht');camdata.type='ORTHO';camdata.ortho_scale=.49
cam=bpy.data.objects.new('Werkansicht',camdata);scene.collection.objects.link(cam);scene.camera=cam
scene.render.film_transparent=True;scene.render.resolution_x=680;scene.render.resolution_y=820;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA'
scene.view_settings.view_transform='AgX'
for sv in bpy.context.screen.areas:
    if sv.type=='VIEW_3D':
        sv.spaces.active.region_3d.view_distance=.6;sv.spaces.active.region_3d.view_location=(0,0,-.205)
def camera(x,y,z):
    cam.location=(x,y,z);aim(cam,(0,.005,-.218))
camera(.27,-.94,-.020)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(out/'Grubenlicht.blend'),compress=True)
report={'meters':[float(v) for v in all_lods[0].dimensions],'triangles':counts,'renderEngine':'Cycles CPU','authoringVersion':bpy.app.version_string}
(work/'mesh.json').write_text(json.dumps(report,indent=2))
for name,pose in [('Front',(.18,-.96,-.05)),('Seite',(.81,-.70,.025))]:
    camera(*pose);scene.render.filepath=str(work/f'{name}.png');bpy.ops.render.render(write_still=True)
