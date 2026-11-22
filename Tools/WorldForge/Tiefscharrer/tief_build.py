"""Blender CPU authoring and export. The original game clips drive these projection joints in Unity."""
from pathlib import Path
import json, sys, math
import numpy as np
import bpy,bmesh
from mathutils import Vector
here=Path(__file__).resolve().parent;sys.path.insert(0,str(here))
from tief_detail import Build,skinpoint,normal
from tief_bind import basis,influences

args=sys.argv[sys.argv.index('--')+1:];root=Path(args[0]).resolve()
out=root/'Assets/Resources/Equipment/Tiefscharrer';preview=root/'preview';preview.mkdir(exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene;scene.unit_settings.system='METRIC'
def select(obs):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in obs:ob.select_set(True)
    bpy.context.view_layer.objects.active=obs[0]
def studio_material():
    m=bpy.data.materials.new('Tiefscharrer_Atlas');m.use_nodes=True
    n=m.node_tree.nodes;l=m.node_tree.links;p=n.get('Principled BSDF');m.diffuse_color=(.12,.086,.062,1)
    def sample(key,space):
        t=n.new('ShaderNodeTexImage');t.image=bpy.data.images.load(str(out/f'Tiefscharrer_{key}.png'));t.image.colorspace_settings.name=space;t.image.pack();return t
    c=sample('BaseColor','sRGB');mr=sample('ORM','Non-Color');b=sample('Normal','Non-Color')
    l.new(c.outputs['Color'],p.inputs['Base Color']);separate=n.new('ShaderNodeSeparateColor');l.new(mr.outputs['Color'],separate.inputs[0]);l.new(separate.outputs['Green'],p.inputs['Roughness']);l.new(separate.outputs['Blue'],p.inputs['Metallic'])
    nm=n.new('ShaderNodeNormalMap');nm.inputs['Strength'].default_value=.61;l.new(b.outputs['Color'],nm.inputs['Color']);l.new(nm.outputs['Normal'],p.inputs['Normal'])
    p.inputs['Specular IOR Level'].default_value=.26
    return m
mat=studio_material()
def decimate(v,f,i):
    name='Volumen';mesh=bpy.data.meshes.new(name);mesh.from_pydata(v.tolist(),[],f.tolist());mesh.update()
    temp=bpy.data.objects.new(name,mesh);scene.collection.objects.link(temp)
    dec=temp.modifiers.new('Krümmung erhalten','DECIMATE');dec.ratio=[.482,.58,.62][i]
    dg=bpy.context.evaluated_depsgraph_get();ev=temp.evaluated_get(dg);q=ev.to_mesh()
    arr=np.empty(len(q.vertices)*3,dtype=np.float32);q.vertices.foreach_get('co',arr)
    faces=[tuple(p.vertices) for p in q.polygons];ev.to_mesh_clear()
    bpy.data.objects.remove(temp,do_unlink=True);bpy.data.meshes.remove(mesh)
    return arr.reshape((-1,3)),faces

brand=skinpoint(.600,.816,1);bn=normal(brand);brand=brand+bn*.006
bones=basis(brand,bn)
arm=bpy.data.armatures.new('Tiefscharrer_Projektion');rig=bpy.data.objects.new('Tiefscharrer_Projektion',arm);scene.collection.objects.link(rig);select([rig])
bpy.ops.object.mode_set(mode='EDIT')
for name,parent,head,tail in bones:
    b=arm.edit_bones.new(name);b.head=head;b.tail=tail
    if parent:b.parent=arm.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT');rig.show_in_front=True
def unity(v):return [round(float(v[0]),5),round(float(v[2]),5),round(float(v[1]),5)]
report={'authoringVersion':bpy.app.version_string,'authorAxes':'Blender +Y forward, +Z up','units':'m',
        'neutralBonesUnity':{name:{'parent':parent,'head':unity(h),'tail':unity(t)} for name,parent,h,t in bones},
        'brandNormalUnity':unity(bn),'skinWeightLimit':4,'lods':{}}
models=[]
for i in range(3):
    raw=np.load(root/'cache'/f'koerper{i}.npz');p,f=decimate(raw['v'],raw['f'],i)
    geo=Build(p,f,i);geo.claws();geo.features();geo.fur()
    name='Tiefscharrer_LOD'+str(i);me=bpy.data.meshes.new(name);me.from_pydata(geo.v,[],geo.f);me.update()
    uv0=me.uv_layers.new(name='Hautatlas');uv0.data.foreach_set('uv',np.array([v for face in geo.uv for v in face],np.float32).ravel())
    uv1=me.uv_layers.new(name='Tastkranz_Anatomie')
    uv1.data.foreach_set('uv',np.array([(0.,0.) if part<0 else ((part+.5)/8.,1.) for f,part in zip(geo.f,geo.part) for _ in f],np.float32).ravel())
    me.uv_layers.active_index=0;uv0.active_render=True
    me.polygons.foreach_set('use_smooth',np.full(len(me.polygons),True,dtype=np.bool_))
    # Skin (one closed volume) and the attached keratin/fibres have consistent exterior normals.
    bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(me);bm.free();me.update()
    ob=bpy.data.objects.new(name,me);scene.collection.objects.link(ob);me.materials.append(mat)
    groups=[ob.vertex_groups.new(name=q[0]) for q in bones]
    coords=np.array(geo.v,dtype=np.float32);ix,vv=influences(coords,bones)
    for row,(ids,values) in enumerate(zip(ix,vv)):
        for j,w in zip(ids,values):
            if w:groups[j].add([row],float(w),'REPLACE')
    modifier=ob.modifiers.new('Bewegung aus dem vorhandenen Rattler','ARMATURE');modifier.object=rig;ob.parent=rig
    me.calc_loop_triangles();parts=np.array(geo.part);pcount={}
    for j in range(8):
        selected=np.flatnonzero(parts==j);pcount[str(j)]=int(sum(len(geo.f[k])-2 for k in selected))
    assert min(pcount.values())>0,(i,pcount)
    report['lods'][name]={'triangles':len(me.loop_triangles),'vertices':len(me.vertices),'anatomicalPadTriangles':pcount,
                'minUnity':unity(coords.min(0)),'maxUnity':unity(coords.max(0)),
                'usedDeformBones':[g.name for j,g in enumerate(groups) if np.any((ix==j)&(vv>.0001))],
                'skinWeightErrorMax':float(np.max(np.abs(vv.sum(1)-1)))}
    assert len(report['lods'][name]['usedDeformBones'])>=16
    print(name,report['lods'][name]['triangles'],pcount,flush=True);models.append(ob)
select([rig]+models)
bpy.ops.export_scene.fbx(filepath=str(out/'Tiefscharrer.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},
                         axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',
                         use_custom_props=False,bake_anim=False,add_leaf_bones=False,armature_nodetype='NULL',
                         use_armature_deform_only=False,path_mode='STRIP',mesh_smooth_type='OFF')
(preview/'Tiefscharrer-Messwerte.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
if '--no-review' in args:sys.exit(0)
select([rig,models[0]]);bpy.ops.export_scene.gltf(filepath=str(preview/'Tiefscharrer.glb'),use_selection=True,export_format='GLB',export_materials='EXPORT',export_animations=False)
for ob in models[1:]:ob.hide_render=True;ob.hide_set(True)
rig.show_in_front=False
scene.world=bpy.data.worlds.new('Ascheschiefer');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.18,.20,.21,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.56
def aim(o,p):o.rotation_euler=(Vector(p)-o.location).to_track_quat('-Z','Y').to_euler()
def area(name,loc,power,size,color):
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.color=color;d.size=size
    ob=bpy.data.objects.new(name,d);scene.collection.objects.link(ob);ob.location=loc;aim(ob,(0,.10,.54))
area('Gefaltetes Seitenlicht',(-2.1,3.2,3.6),245,2.5,(1,.85,.66))
area('Kalter Reflex',(2.5,1.25,1.9),166,2.,(.77,.87,1))
area('Schieferkante',(.45,-2.8,2.9),188,2.3,(.89,.82,.69))
area('Nasenlicht',(-.40,3.50,.55),38,1.7,(1,.76,.66))
floor_mat=bpy.data.materials.new('Ascheboden');floor_mat.diffuse_color=(.084,.083,.080,1)
floor_mat.use_nodes=True;n=floor_mat.node_tree.nodes;l=floor_mat.node_tree.links;p=n.get('Principled BSDF');p.inputs['Roughness'].default_value=.95
noise=n.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=430;noise.inputs['Detail'].default_value=3.5
ramp=n.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].color=(.036,.037,.035,1);ramp.color_ramp.elements[1].color=(.102,.096,.080,1);l.new(noise.outputs[0],ramp.inputs[0]);l.new(ramp.outputs[0],p.inputs['Base Color'])
bpy.ops.mesh.primitive_plane_add(size=160,location=(0,0,.002));floor=bpy.context.object;floor.name='Studio_Aschegrund';floor.data.materials.append(floor_mat)
camdata=bpy.data.cameras.new('Werkansicht');camdata.type='ORTHO';camdata.clip_start=.04
cam=bpy.data.objects.new('Werkansicht',camdata);scene.collection.objects.link(cam);scene.camera=cam
scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=23;scene.cycles.use_denoising=False
scene.render.resolution_x=1120;scene.render.resolution_y=720;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='AgX'
scene.render.film_transparent=False;bpy.context.preferences.filepaths.save_version=0
cam.location=(3.6,4.9,2.17);aim(cam,(0,.03,.56));camdata.ortho_scale=3.74
for area_ in bpy.context.screen.areas:
    if area_.type=='VIEW_3D':area_.spaces.active.region_3d.view_distance=4.;area_.spaces.active.region_3d.view_location=(0,0,.67)
select([models[0]]);bpy.ops.wm.save_as_mainfile(filepath=str(preview/'Tiefscharrer.blend'),compress=True)
for name,loc,at,scale in [('Tiefscharrer-Werkansicht',(3.4,5.2,2.15),(0,.04,.55),3.60),
                        ('Tiefscharrer-Profil',(5.9,.41,1.60),(0,-.02,.6),4.00),
                        ('Tiefscharrer-Sinneskranz',(1.9,4.4,1.57),(0,1.015,.603),1.12)]:
    cam.location=loc;aim(cam,at);camdata.ortho_scale=scale;scene.render.filepath=str(preview/(name+'.png'));bpy.ops.render.render(write_still=True)
