"""Eight asymmetrical sensory pads for the existing head and animation of the Tiefscharrer."""
from pathlib import Path
from math import sin,cos,pi,tau
import bpy
import sys
import numpy as np
root=Path(sys.argv[sys.argv.index('--')+1]).resolve();out=root/'out'
bpy.ops.wm.read_factory_settings(use_empty=True)
points=[];faces=[];slots=[]
def petal(theta,length,width,drift):
    start=len(points);along=17;around=12
    for j in range(along+1):
        u=j/along
        dx=sin(theta+drift*u);dz=cos(theta+drift*u)
        r=.015+length*u
        # Blender -Y becomes Unity +Z. The pads flatten slightly against the actual muzzle.
        cy=-.014-.014*sin(u*pi)+.008*u
        w=width*(.42+.75*sin(pi*u)**.65)*(1-.20*u)
        if j==along:w=.0008
        for k in range(around):
            a=tau*k/around;rx=dz*w*cos(a);rz=-dx*w*cos(a)
            points.append((dx*r+rx,cy-w*.60*sin(a),dz*r+rz))
    for j in range(along):
        for k in range(around):
            i=start+j*around+k;next_=start+j*around+(k+1)%around
            faces.append((i,next_,next_+around,i+around));slots.append(1 if j in (3,6,9,12) and k in (2,3,4) else 0)
    faces.append(tuple(range(start+along*around,start+(along+1)*around)));slots.append(0)
    faces.append(tuple(reversed(range(start,start+around))));slots.append(0)
for i in range(8):petal(tau*i/8+.155,.064+.010*cos(3*i+.75),.011+.004*cos(2.74*i),.10*sin(i+1))
start=len(points);n=34;cs=10
for j in range(n):
    t=tau*j/n
    for k in range(cs):
        a=tau*k/cs;r=.018+.0065*cos(a);points.append((sin(t)*r,-.012-.008*sin(a),cos(t)*r))
for j in range(n):
    for k in range(cs):
        faces.append((start+j*cs+k,start+j*cs+(k+1)%cs,start+(j+1)%n*cs+(k+1)%cs,start+(j+1)%n*cs+k));slots.append(0)
me=bpy.data.meshes.new('Tastkranz');me.from_pydata(points,[],faces);me.update()
for name,c in [('Haut – Schieferpflaume',(.14,.044,.037)),('Haut – feine Falten',(.19,.063,.049))]:
    m=bpy.data.materials.new(name);m.diffuse_color=(*c,1);m.use_nodes=True
    pr=m.node_tree.nodes.get('Principled BSDF');pr.inputs['Base Color'].default_value=(*c,1);pr.inputs['Roughness'].default_value=.66;me.materials.append(m)
me.polygons.foreach_set('material_index',np.array(slots,dtype=np.int32))
me.polygons.foreach_set('use_smooth',np.ones(len(me.polygons),dtype=np.bool_))
ob=bpy.data.objects.new('Tastkranz',me);bpy.context.scene.collection.objects.link(ob);ob.select_set(True);bpy.context.view_layer.objects.active=ob
bpy.ops.export_scene.fbx(filepath=str(out/'Tastkranz.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False)
