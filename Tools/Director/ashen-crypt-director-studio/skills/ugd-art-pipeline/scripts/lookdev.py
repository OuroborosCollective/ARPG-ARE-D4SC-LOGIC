"""Render the authored specimens from a locked camera under neutral and ember light on CPU."""
from pathlib import Path
import sys,math,random
import bpy
from mathutils import Vector
root=Path(sys.argv[sys.argv.index('--')+1]).resolve()
dest=Path(sys.argv[sys.argv.index('--')+2]).resolve()
dest.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.device='CPU';s.cycles.samples=24;s.cycles.use_denoising=bool(getattr(bpy.app.build_options,'openimagedenoise',False))
s.world=bpy.data.worlds.new('Cold sky');s.world.use_nodes=True
s.world.node_tree.nodes['Background'].inputs[0].default_value=(.23,.28,.38,1)
s.world.node_tree.nodes['Background'].inputs[1].default_value=.45
s.render.resolution_percentage=100;s.render.image_settings.file_format='PNG'
s.view_settings.view_transform='AgX'
library={}
for name in ('Schieferbruch','Gluthelmlinge','Ossuarbogen','Aschenhorcher'):
    with bpy.data.libraries.load(str(root/(name+'.blend')),link=False) as (before,after):
        after.objects=before.objects
    group=bpy.data.collections.new(name)
    for obj in after.objects:
        if obj and not obj.name.endswith(('_LOD1','_LOD2')):group.objects.link(obj)
    library[name]=group
def place(name,xyz,scale=1,angle=0):
    inst=bpy.data.objects.new(name,None);s.collection.objects.link(inst)
    inst.instance_type='COLLECTION';inst.instance_collection=library[name];inst.location=xyz
    inst.scale=(scale,)*3;inst.rotation_euler.z=angle
    return inst
def look(obj,target):
    obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
def light(name,p,aim,energy,color,size):
    data=bpy.data.lights.new(name,'AREA');data.energy=energy;data.color=color;data.shape='DISK';data.size=size
    obj=bpy.data.objects.new(name,data);s.collection.objects.link(obj);obj.location=p;look(obj,aim)
def camera(pos,target,lens):
    d=bpy.data.cameras.new('Portrait');o=bpy.data.objects.new('Portrait',d);s.collection.objects.link(o);o.location=pos;look(o,target);d.lens=lens;s.camera=o
bpy.ops.mesh.primitive_plane_add(size=160,location=(0,0,-.012))
floor=bpy.context.object;mat=bpy.data.materials.new('Windblown powder over black shale');mat.use_nodes=True
nt=mat.node_tree; bs=nt.nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.89
coord=nt.nodes.new('ShaderNodeTexCoord'); noise=nt.nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=2.6;noise.inputs['Detail'].default_value=5.0;nt.links.new(coord.outputs['Object'],noise.inputs[0])
r=nt.nodes.new('ShaderNodeValToRGB');r.color_ramp.elements[0].position=.24;r.color_ramp.elements[0].color=(.026,.034,.041,1);r.color_ramp.elements[1].position=.78;r.color_ramp.elements[1].color=(.29,.30,.313,1)
nt.links.new(noise.outputs[0],r.inputs[0]);nt.links.new(r.outputs[0],bs.inputs['Base Color'])
bn=nt.nodes.new('ShaderNodeBump');bn.inputs['Strength'].default_value=.36;bn.inputs['Distance'].default_value=.05;nt.links.new(noise.outputs[0],bn.inputs['Height']);nt.links.new(bn.outputs[0],bs.inputs['Normal']);floor.data.materials.append(mat)
place('Aschenhorcher',(.15,-.48,.005),1.0,-.28);place('Ossuarbogen',(-.40,2.30,0),.82,-.08)
place('Gluthelmlinge',(-1.38,-1.00,.01),.8,.28);place('Gluthelmlinge',(1.7,.50,.008),.48,-1.12)
for x,y,k,a in ((-2.25,1.7,1.0,.55),(1.45,2.3,.72,1.1),(-1.9,-.03,.43,1.4),(2.7,1.1,.49,1.9),(-.9,.76,.28,1.2)):
    place('Schieferbruch',(x,y,0),k,a)
g=random.Random(7619)
for _ in range(75):
    x,y=g.uniform(-5,5),g.uniform(-3.5,4)
    place('Schieferbruch',(x,y,-.023),g.uniform(.04,.14),g.uniform(-3,3))
# Far rock faces enclose the crypt approach and occlude the studio horizon.
for j in range(29):
    x=-23+j*1.62;y=6.7+g.uniform(-.6,1.3);h=g.uniform(3.6,6.5)
    cliff=place('Schieferbruch',(x,y,-.5),1,g.uniform(-2,2));cliff.scale=(2.6,2.2,h)
for j in range(4):
    debris=place('Schieferbruch',(-3.6+j*.42,3.2+j*.23,.09*j),1,.23*j);debris.scale=(1.2,.8,.55+j*.15)
light('Overcast rim',(-3,-1,7),(0,0,.7),690,(.68,.79,1),6)
light('Reflected dusk',(3.5,-4,3.5),(0,.1,.9),360,(.98,.84,.68),5)
light('Warm gate recess',(-.2,3.5,1.4),(0,.1,1),290,(1,.26,.075),2.1)
light('Under the gills',(-1.52,-1.15,.18),(-1,-1,.45),12,(1,.15,.032),.55)
s.render.resolution_x=1000;s.render.resolution_y=625
for id,p,a,lens in [('Aschenhorcher-am-Ossuar',(5.0,-8.5,3.1),(0,.67,1.74),44),('Aschenhorcher-Detail',(2.6,-4.45,1.74),(0,-.52,.64),55)]:
    camera(p,a,lens);s.render.filepath=str(dest/(id+'.png'));bpy.ops.render.render(write_still=True)
