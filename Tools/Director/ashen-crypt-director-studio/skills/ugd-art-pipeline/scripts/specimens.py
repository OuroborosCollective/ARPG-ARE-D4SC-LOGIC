"""Build four original Ashen Crypt specimens with the installed upstream modeling kit."""
import argparse
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import random
import sys
import bpy
import numpy as np
from mathutils import Vector
from math import sin, cos, pi, tau

parser=argparse.ArgumentParser()
parser.add_argument("--lab",type=Path,required=True)
parser.add_argument("--out",type=Path,required=True)
parser.add_argument("--seed",type=int,default=7619)
parser.add_argument("--size",type=int,choices=(512,1024,2048),default=1024)
args=parser.parse_args(sys.argv[sys.argv.index("--")+1:])
out=args.out.resolve()
out.mkdir(parents=True,exist_ok=True)
src=args.lab/"sources"
sys.path.insert(0,str(src/"modeler/scripts"))
import modelkit
rng=random.Random(args.seed)
palette={"slate":((.22,.245,.265),(.65,.67,.68)), "shell":((.10,.115,.13),(.42,.415,.39)),
         "cap":((.16,.045,.037),(.57,.22,.105)), "bone":((.21,.17,.12),(.69,.62,.46))}
def field(side,rng,n):
    # Periodic smooth lattice. Finite differences below share the same edge samples.
    lattice=rng.uniform(-1,1,(n,n)).astype(np.float32)
    pos=np.arange(side,dtype=np.float32)*n/side
    i=pos.astype(np.int32);f=pos-i;f=f*f*(3-2*f)
    a=lattice[i[:,None],i];b=lattice[i[:,None],(i+1)%n]
    c=lattice[((i+1)%n)[:,None],i];d=lattice[((i+1)%n)[:,None],(i+1)%n]
    return (a*(1-f)+b*f)*(1-f[:,None])+(c*(1-f)+d*f)*f[:,None]
def png(path,data):
    import struct,zlib
    h,w,channels=data.shape
    def chunk(k,b):return struct.pack(">I",len(b))+k+b+struct.pack(">I",zlib.crc32(k+b)&0xffffffff)
    rows=np.concatenate((np.zeros((h,1),dtype=np.uint8),data.reshape(h,-1)),axis=1).tobytes()
    path.write_bytes(b"\x89PNG\r\n\x1a\n"+chunk(b"IHDR",struct.pack(">IIBBBBB",w,h,8,2 if channels==3 else 6,0,0,0))+chunk(b"IDAT",zlib.compress(rows,7))+chunk(b"IEND",b""))
def write_maps():
    # Single padded atlas. Each quadrant is one authored surface family.
    n=args.size//2
    result={k:np.zeros((2*n,2*n,c),dtype=np.uint8) for k,c in (("albedo",3),("normal",3),("metalSmooth",4),("orm",3),("emission",3))}
    for index,(name,(low,high)) in enumerate(palette.items()):
        g=np.random.default_rng(args.seed+353*index)
        y,x=np.indices((n,n),dtype=np.float32)/n
        broad=field(n,g,5);fine=field(n,g,47);grain=field(n,g,119)
        seam=sin(1.0)*np.sin(tau*(3*x+2*y+.53*broad+.06*fine))
        veins=np.exp(-np.abs(seam)*38)*np.clip(2*broad+.55,0,1)
        beds=np.sin(tau*(6*y+2*x+.44*broad+.1*fine))
        t=np.clip(.48+.28*broad+.14*fine+.075*grain+.065*beds,0,1)
        if name=="cap": t=np.clip(t+.09*np.cos(tau*(17*x+fine*.15)),0,1)
        height=.36*broad+.17*fine+.05*grain+.08*beds-.22*veins
        dx=(np.roll(height,-1,1)-np.roll(height,1,1))*n*.028
        dy=(np.roll(height,-1,0)-np.roll(height,1,0))*n*.028
        normal=np.stack((-dx,dy,np.ones_like(dx)),axis=-1);normal/=np.linalg.norm(normal,axis=-1,keepdims=True)
        alb=np.array(low)+(np.array(high)-np.array(low))*t[:,:,None]
        em=veins*(.70 if name in ("slate","shell") else .30 if name=="cap" else 0)
        if name=='cap':em=np.maximum(em,.62*np.exp(-((y-.12)/.12)**2)*(.65+.35*np.cos(tau*(19*x+.18*broad))))
        alb=alb*(1-.7*em[:,:,None])+em[:,:,None]*np.array([.55,.115,.035])
        metal=np.clip(.06+.21*t if name=="shell" else 0*t,0,1)
        smooth=np.clip((.35 if name in ("shell","cap") else .18)+.13*broad+.08*fine,0,1)
        ao=.83+.17*t
        maps={"albedo":alb,"normal":.5+.5*normal,"metalSmooth":np.stack((metal,0*t,0*t,smooth),-1),
              "orm":np.stack((ao,1-smooth,metal),-1),"emission":em[:,:,None]*np.array([1,.23,.055])}
        row=index//2; col=index%2
        for k,v in maps.items():result[k][row*n:(row+1)*n,col*n:(col+1)*n]=np.rint(np.clip(v,0,1)*255).astype('uint8')
    for k,data in result.items():png(out/(k+'.png'),data)
def material():
    m=bpy.data.materials.new("AshenSurface");m.use_nodes=True
    nodes=m.node_tree.nodes; link=m.node_tree.links.new;bs=nodes.get("Principled BSDF")
    def image_node(key,colorspace):
        im=bpy.data.images.load(str(out/(key+'.png')));im.colorspace_settings.name=colorspace;im.pack()
        n=nodes.new('ShaderNodeTexImage');n.image=im;return n.outputs['Color']
    link(image_node('albedo','sRGB'),bs.inputs['Base Color'])
    norm=nodes.new('ShaderNodeNormalMap');link(image_node('normal','Non-Color'),norm.inputs['Color']);link(norm.outputs[0],bs.inputs['Normal'])
    sep=nodes.new('ShaderNodeSeparateColor');link(image_node('orm','Non-Color'),sep.inputs[0]);link(sep.outputs['Green'],bs.inputs['Roughness']);link(sep.outputs['Blue'],bs.inputs['Metallic'])
    link(image_node('emission','sRGB'),bs.inputs['Emission Color']);bs.inputs['Emission Strength'].default_value=1.5
    return m
def coat(obj,tile,group=None,smooth=True):
    idx=list(palette).index(tile);obj.data.materials.append(mat)
    mesh=obj.data;uv=mesh.uv_layers.active or mesh.uv_layers.new(name='UVMap')
    # Local triplanar box projection per face, always inside one padded atlas cell.
    positions=np.array([v.co[:] for v in mesh.vertices]);low=positions.min(0);span=np.maximum(positions.max(0)-low,.001)
    normalized=(positions-low)/span
    for p in mesh.polygons:
        p.use_smooth=smooth
        axis=max(range(3),key=lambda k:abs(p.normal[k]))
        u,v=((1,2),(0,2),(0,1))[axis]
        for lid in p.loop_indices:
            xyz=normalized[mesh.loops[lid].vertex_index]
            uv.data[lid].uv=((idx%2+.025+.95*xyz[u])*.5,(1-idx//2+.025+.95*xyz[v])*.5)
    if group:obj.vertex_groups.new(name=group).add(list(range(len(mesh.vertices))),1,'REPLACE')
    pieces.append(obj);return obj
def ell(name,pos,size,tile,group=None):
    o=modelkit.sphere(name,1,location=pos,segments=28,rings=16);o.scale=size;return coat(o,tile,group)
def rings(name,rows,tile,group=None,n=30,wave=0,smooth=True,vertical=False):
    verts=[];faces=[]
    for j,(center,rad) in enumerate(rows):
        c=Vector(center);t=Vector(rows[min(j+1,len(rows)-1)][0])-Vector(rows[max(j-1,0)][0]);t.normalize()
        if vertical:t=Vector((0,0,1))
        axis=Vector((0,1,0)) if abs(t.y)<.88 else Vector((1,0,0))
        e=t.cross(axis).normalized();f=t.cross(e).normalized()
        for i in range(n):
            a=tau*i/n;r=rad*(1+wave*(.6*cos(5*a+.23*j)+.4*sin(8*a-.4*j)))
            verts.append(c+r*(e*cos(a)+f*sin(a)))
        if j:
            q=(j-1)*n
            for i in range(n): faces.append((q+i,q+(i+1)%n,q+n+(i+1)%n,q+n+i))
    faces.append(tuple(range(n-1,-1,-1)));q=(len(rows)-1)*n;faces.append(tuple(q+i for i in range(n)))
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update()
    o=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(o);return coat(o,tile,group,smooth)
def stone(name,pos,size,phase):
    x,y,z=pos;w,d,h=size;rows=[]
    for j,(zz,r) in enumerate(((0,.79),(.10,1),(.26,.96),(.34,1.03),(.42,.93),(.68,.95),(.84,.89),(1,.66))):
        rows.append(((x+.05*w*sin(j*.8+phase),y+.02*d*cos(j+phase),z+h*zz),r))
    o=rings(name,rows,'slate',n=19,wave=.096,smooth=False,vertical=True)
    for v in o.data.vertices:
        v.co.x=x+(v.co.x-x)*w;v.co.y=y+(v.co.y-y)*d
    bevel=o.modifiers.new('Eroded edges','BEVEL');bevel.width=min(.023,h*.026);bevel.segments=2
    return o
def mushroom(x,y,s,drift,phase):
    top=(x+drift,y+.045*s,.76*s);base=(x,y,.005*s)
    rings('Folded stem',[(base,.105*s),((x+.02*s,y,.22*s),.08*s),((x+drift*.55,y,.5*s),.055*s),(top,.105*s)],'bone',n=21,wave=.055)
    rows=[((top[0],top[1],z*s),r*s) for z,r in ((.63,.04),(.65,.25),(.55,.49),(.585,.51),(.68,.46),(.79,.34),(.91,.16),(.93,.024))]
    rings('Fluted cap',rows,'cap',n=60,wave=.043)
    for i in range(33):
        a=tau*i/33+phase;v=Vector((cos(a),sin(a),0));c=Vector(top)
        line=[(c+v*.062*s+Vector((0,0,-.05*s)),.012*s),(c+v*.29*s+Vector((0,0,-.085*s)),.018*s),(c+v*.47*s+Vector((0,0,-.185*s)),.005*s)]
        rings('Lamella',line,'bone',n=5)
def fungi():
    for x,y,s,b,p in [(0,.06,1,.09,2),(-.41,.15,.68,-.06,1),(.32,-.23,.55,.06,3),(-.21,-.29,.34,-.05,1)]:mushroom(x,y,s,b,p)
def outcrop():
    for i,(pos,size) in enumerate([((0,0,0),(.93,.70,.73)),((-.58,-.22,0),(.55,.44,.30)),((.60,-.10,0),(.48,.46,.43)),((.2,.53,0),(.55,.38,.47))]):
        stone('Bedded shale',pos,size,i+args.seed*.03)
def gate():
    # Two pointed archivolts and independent masonry courses with a clear opening.
    for sign in (-1,1):
        for j in range(6):
            x=sign*1.38;z=.25+j*.43
            o=modelkit.box('Pier course',(.62+(j==0)*.16,1.01,.41),(x,0,z));coat(o,'slate',smooth=False)
            mod=o.modifiers.new('Chisel','BEVEL');mod.width=.048;mod.segments=2
        for band,(radius,y,wide) in enumerate(((2.04,.14,.30),(1.98,-.40,.13))):
            center=-sign*(radius-1.13)
            maxa=math.acos((radius-1.13)/radius)
            for j in range(9):
                a=maxa*(j-.019)/9;b=maxa*(j+1.019)/9
                pts=[(center+sign*r*cos(t),2.595+r*sin(t)) for r,t in [(radius,a),(radius,b),(radius+wide,b),(radius+wide,a)]]
                o=modelkit.extrude_profile('Archivolt',pts,.24 if band else .72,location=(0,y,0),rotation=(90,0,0))
                coat(o,'bone' if band else 'slate',smooth=False);m=o.modifiers.new('Worn arris','BEVEL');m.width=.018;m.segments=2
        for q in (-.14,.14):
            o=modelkit.lathe('Reeded pillar',[(0,0),(.12,0),(.15,.13),(.108,.24),(.1,2.38),(.16,2.46),(.15,2.61),(0,2.61)],location=(1.39*sign+q,-.55,.02),segments=18);coat(o,'bone')
def creature():
    bones={'Root':((0,0,0),(0,0,.23),None),'Carapace':((0,0,.76),(0,-.63,.89),'Root'),'Skull':((0,-.62,.89),(0,-1.22,.73),'Carapace')}
    ell('Ventral thorax',(0,-.08,.72),(.55,1.02,.39),'shell','Carapace')
    ell('Hammer head',(0,-1.02,.72),(.44,.43,.26),'shell','Skull')
    for i in range(8):
        y=-.70+i*.215; w=.55*(1-.038*i)
        ell('Imbricated plate',(0,y,.88+.035*sin(i*.68)),(w,.286,.225-i*.008),'shell','Carapace')
        for s in (-1,1):
            rings('Costal spur',[((s*w*.72,y,.91),.074),((s*w*1.05,y+.10,1.02),.057),((s*w*1.22,y+.25,1.055),.004)],'bone','Carapace',n=10)
    for s,suffix in [(-1,'L'),(1,'R')]:
        for j in range(4):
            y=-.68+.42*j;h=Vector((s*.36,y,.75));k=Vector((s*(.92+.11*sin(j)),y-.13+.055*j,.88-.037*j));f=Vector((s*(1.20+.10*cos(j)),y-.38+.14*j,.032))
            upper=f'Femur{j}.{suffix}';lower=f'Tibia{j}.{suffix}'
            bones[upper]=(h,k,'Carapace');bones[lower]=(k,f,upper)
            rings('Segmented thigh',[(h,.135),(h.lerp(k,.36)+Vector((0,0,.035)),.148),(h.lerp(k,.78),.080),(k,.073)],'shell',upper,n=14,wave=.07)
            ell('Articulation',k,(.088,.10,.095),'bone',lower)
            rings('Sickle shin',[(k,.088),(k.lerp(f,.25)+Vector((s*.07,0,.015)),.091),(k.lerp(f,.70)+Vector((s*.06,0,0)),.044),(f,.027),(f+Vector((-s*.12,-.055,-.023)),.005)],'shell',lower,n=13,wave=.055)
        for i in range(3):
            o=ell('Amber ocellus',(s*(.255+.052*i),-1.29+.069*i,.783+.04*i),(.054,.061,.048),'cap','Skull')
        bones['Palp.'+suffix]=((s*.22,-1.23,.68),(s*.22,-1.7,.61),'Skull')
        bones['Feeler.'+suffix]=((s*.27,-1.1,.89),(s*.52,-1.63,1.15),'Skull')
        rings('Palp',[((s*.22,-1.23,.68),.09),((s*.32,-1.55,.57),.085),((s*.22,-1.7,.61),.042),((s*.14,-1.73,.70),.004)],'bone','Palp.'+suffix,n=15)
        rings('Sensory barb',[((s*.27,-1.1,.89),.05),((s*.41,-1.29,1.04),.032),((s*.52,-1.63,1.15),.012),((s*.51,-1.85,1.14),.003)],'bone','Feeler.'+suffix,n=10)
    return bones
def merge(name):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in pieces:
        bpy.context.view_layer.objects.active=obj
        for mod in list(obj.modifiers):bpy.ops.object.modifier_apply(modifier=mod.name)
        obj.select_set(True)
    bpy.context.view_layer.objects.active=pieces[0];bpy.ops.object.join()
    obj=bpy.context.object;obj.name=name
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    return obj
def rig(mesh,bones):
    data=bpy.data.armatures.new('Aschenhorcher');arm=bpy.data.objects.new('Aschenhorcher_Rig',data);bpy.context.collection.objects.link(arm)
    bpy.context.view_layer.objects.active=arm;arm.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
    for name,(head,tail,parent) in bones.items():
        e=data.edit_bones.new(name);e.head=head;e.tail=tail
        if parent:e.parent=data.edit_bones[parent]
        if name=='Root':e.use_deform=False
    bpy.ops.object.mode_set(mode='OBJECT')
    mesh.parent=arm;mod=mesh.modifiers.new('Skin','ARMATURE');mod.object=arm
    for frame in (1,7,13,19,25):
        t=(frame-1)*tau/24
        for name in bones:
            if name=='Root':continue
            pb=arm.pose.bones[name];pb.rotation_mode='XYZ'
            if name=='Skull':pb.rotation_euler.x=.14*cos(t)
            elif name.startswith('Palp.'):pb.rotation_euler.z=(1 if name.endswith('L') else -1)*.24*sin(t)
            elif name.startswith('Feeler.'):pb.rotation_euler.x=.25*sin(t+(.35 if name.endswith('L') else -.35))
            else:continue
            pb.keyframe_insert('rotation_euler',frame=frame)
    arm.animation_data.action.name='Listen';bpy.context.scene.frame_end=25;bpy.context.scene.frame_set(1)
    return arm
def load_upstream(skill,script):
    p=src/'el4/skills'/skill/'scripts'/(script+'.py')
    sys.path.insert(0,str(p.parent));spec=importlib.util.spec_from_file_location(script,p);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
def build(name,fn):
    global pieces,mat
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mat=material();pieces=[];bones=fn();obj=merge(name)
    floor=min(v.co.z for v in obj.data.vertices)
    for v in obj.data.vertices:v.co.z-=floor
    obj.data.update()
    arm=rig(obj,bones) if bones else None
    stats=load_upstream('blender-game-optimization','mesh_stats').run(objects=[obj.name],budget='mobile')
    report={'stats':stats}
    if arm:
        report['rig']=load_upstream('blender-character-rigging','rig_check').run(armature=arm.name,meshes=[obj.name])
    if not arm:
        sys.path.insert(0,str(src/'modeler/scripts'));import check_geometry
        report['geometry']=check_geometry.run(names=[obj.name])
    # Keep the skinned source and weights when decimating, before applying the pose.
    obj.name=name+'_LOD0'
    lods=[]
    for level,ratio in enumerate((.5,.22),1):
        low=obj.copy();low.data=obj.data.copy();low.name=name+'_LOD'+str(level)
        bpy.context.collection.objects.link(low)
        arm_mod=next((m for m in low.modifiers if m.type=='ARMATURE'),None)
        if arm_mod:low.modifiers.remove(arm_mod)
        d=low.modifiers.new('Reduce geometry','DECIMATE');d.ratio=ratio;d.use_collapse_triangulate=True
        with bpy.context.temp_override(active_object=low,object=low):
            bpy.ops.object.modifier_apply(modifier=d.name)
        if arm:
            mod=low.modifiers.new('Skin','ARMATURE');mod.object=arm
            if any(not any(g.weight>.0001 for g in v.groups) for v in low.data.vertices):
                raise ValueError('Unweighted reduced mesh '+low.name)
        low.data.calc_loop_triangles();lods.append(len(low.data.loop_triangles))
        low.hide_render=True
    report['lodTriangles']=[stats['total_tris']]+lods
    if not stats['total_tris']>lods[0]>lods[1]>0:raise ValueError('LOD chain')
    if arm:
        zs=[]
        for f in range(1,26):
            bpy.context.scene.frame_set(f);ev=obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
            deformed=ev.to_mesh()
            zs.append(min((ev.matrix_world@v.co).z for v in deformed.vertices));ev.to_mesh_clear()
        report['animatedGroundMetres']=[min(zs),max(zs)]
        if max(abs(z) for z in zs)>.002:raise ValueError('Listening pose breaks foot contact')
        bpy.context.scene.frame_set(1)
    for ob in bpy.context.scene.objects:ob.select_set(True)
    bpy.context.view_layer.objects.active=obj
    bpy.ops.wm.save_as_mainfile(filepath=str(out/(name+'.blend')))
    for mesh in bpy.context.scene.objects:mesh.hide_render=False
    kwargs={'filepath':str(out/(name+'.glb')),'export_format':'GLB','use_selection':True,'export_animations':True,'export_yup':True}
    result=bpy.ops.export_scene.gltf(**kwargs)
    if result!={'FINISHED'}:raise RuntimeError(str(result))
    (out/(name+'-metrics.json')).write_text(json.dumps(report,indent=2,default=str))
    print('SPECIMEN',name,stats['total_tris'],flush=True)
write_maps()
for name,fn in [('Schieferbruch',outcrop),('Gluthelmlinge',fungi),('Ossuarbogen',gate),('Aschenhorcher',creature)]:build(name,fn)
