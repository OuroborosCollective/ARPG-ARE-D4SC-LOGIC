"""Build the portable game bundle, editable visual arrangement and actual CPU previews."""
import json,math,sys
from pathlib import Path
import bpy
from mathutils import Vector

here=Path(__file__).resolve().parent;sys.path.insert(0,str(here))
from werk_items import rattler,guard,mill
from werk_armour import armour

labels={
 'R_Gratkeule':'Gratkeule','R_Splitterzahn':'Splitterzahn','R_Scharrbeil':'Scharrbeil',
 'R_Rippenschild':'Rippenschild','R_Grabschild':'Grabschild','R_Wangenmaske':'Grabwangenmaske',
 'R_Gratharnisch':'Gratharnisch','R_Scharrstulpe_L':'Scharrstulpe links','R_Scharrstulpe_R':'Scharrstulpe rechts',
 'R_Gratschiene_L':'Gratschiene links','R_Gratschiene_R':'Gratschiene rechts',
 'G_Ofengabel':'Ofengabel','G_Zollmesser':'Zollmesser','G_Hammerbeil':'Hammerbeil','G_Zollbuckler':'Zollbuckler','G_Zunderpfanne':'Zunderpfanne',
 'M_Wehrsaebel':'Wehrsäbel','M_Lotklinge':'Lotklinge','M_Jochbeil':'Jochbeil','M_Wehrschild':'Wehrschild','M_Glockenring':'Glockenring'}
bpy.ops.wm.read_factory_settings(use_empty=True)
args=sys.argv[sys.argv.index('--')+1:];root=Path(args[0]).resolve()
output=root/'Assets/Resources/Equipment/Werkarsenal';preview=root/'preview'
preview.mkdir(exist_ok=True);scene=bpy.context.scene;scene.unit_settings.system='METRIC'
mat=bpy.data.materials.new('ARE_Werkarsenal');mat.use_nodes=True;mat.diffuse_color=(.31,.29,.24,1)
nodes=mat.node_tree.nodes;links=mat.node_tree.links;bs=nodes.get('Principled BSDF')
def tex(key,space):
    n=nodes.new('ShaderNodeTexImage');n.image=bpy.data.images.load(str(output/f'Werkarsenal_{key}.png'));n.image.colorspace_settings.name=space;n.image.pack();return n
base=tex('BaseColor','sRGB');normal=tex('Normal','Non-Color');orm=tex('ORM','Non-Color')
sep=nodes.new('ShaderNodeSeparateColor');links.new(orm.outputs['Color'],sep.inputs[0]);links.new(sep.outputs['Blue'],bs.inputs['Metallic']);links.new(sep.outputs['Green'],bs.inputs['Roughness'])
links.new(base.outputs['Color'],bs.inputs['Base Color']);norm=nodes.new('ShaderNodeNormalMap');norm.inputs['Strength'].default_value=.62;links.new(normal.outputs['Color'],norm.inputs['Color']);links.new(norm.outputs[0],bs.inputs['Normal'])

artists=rattler()+guard()+mill()+armour()
assert len(artists)==21 and {a.key for a in artists}==set(labels)
all_obs={};summary={}
for a in artists:
    ob=a.part();all_obs[a.key]=ob;ob.data.materials.append(mat);ob['displayName']=labels[a.key];ob.data.calc_loop_triangles()
    minv=[min(v.co[i] for v in ob.data.vertices) for i in range(3)]
    maxv=[max(v.co[i] for v in ob.data.vertices) for i in range(3)]
    summary[a.key]={'label':labels[a.key],'triangles':len(ob.data.loop_triangles),
        'blenderMin':minv,'blenderMax':maxv,'dimensionsM':[round(b-a,5) for a,b in zip(minv,maxv)],'material':'ARE_Werkarsenal'}
def select(obs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:o.select_set(True)
    bpy.context.view_layer.objects.active=obs[0]
select(list(all_obs.values()))
bpy.ops.export_scene.fbx(filepath=str(output/'Werkarsenal.fbx'),use_selection=True,object_types={'MESH'},
                         axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',
                         use_custom_props=False,bake_anim=False,add_leaf_bones=False,path_mode='STRIP',mesh_smooth_type='OFF')
(preview/'Werkarsenal-Messwerte.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2)+'\n')
if '--no-review' in args:
    if '--icons' in args:
        from werk_icons import render_icons
        render_icons(all_obs,output)
    sys.exit(0)

groups=[('Schlackengarde',['G_Ofengabel','G_Zollmesser','G_Hammerbeil','G_Zollbuckler','G_Zunderpfanne']),
        ('Muehlwehr',['M_Wehrsaebel','M_Lotklinge','M_Jochbeil','M_Wehrschild','M_Glockenring']),
        ('Tiefscharrer',['R_Gratkeule','R_Splitterzahn','R_Scharrbeil','R_Rippenschild','R_Grabschild']),
        ('Gratmontur',['R_Wangenmaske','R_Gratharnisch','R_Scharrstulpe_L','R_Scharrstulpe_R','R_Gratschiene_L','R_Gratschiene_R'])]
for group,keys in groups:
    x=0
    for obkey in keys:
        ob=all_obs[obkey];dims=summary[obkey]['dimensionsM'];vmin=summary[obkey]['blenderMin'];vmax=summary[obkey]['blenderMax']
        ob.location=(x-vmin[0],0,-vmin[2]);x+=dims[0]+.12
    select([all_obs[k] for k in keys]);bpy.ops.export_scene.gltf(filepath=str(preview/(group+'.glb')),use_selection=True,export_format='GLB',export_materials='EXPORT')
    for idx,k in enumerate(keys):all_obs[k].location.x+=groups.index((group,keys))*3.1
for i,ob in enumerate(all_obs.values()):ob.select_set(False)
scene.world=bpy.data.worlds.new('Studio');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.39,.42,.46,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.48
def aim(o,p):o.rotation_euler=(Vector(p)-o.location).to_track_quat('-Z','Y').to_euler()
def area(name,p,power,size,col):
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.size=size;d.color=col
    o=bpy.data.objects.new(name,d);scene.collection.objects.link(o);o.location=p;aim(o,(0,0,0))
area('Kalklicht',(-1.6,2.3,2),165,1.9,(1,.86,.71))
area('Weiches Fuelllicht',(2.1,2,.7),71,2.1,(.72,.84,1))
area('Streiflicht',(1.3,-1,1.4),100,1.2,(.99,.84,.64))
data=bpy.data.cameras.new('Aufsicht');data.type='ORTHO';data.ortho_scale=2.85
cam=bpy.data.objects.new('Aufsicht',data);scene.collection.objects.link(cam);scene.camera=cam
cam.location=(.18,5.5,.56);aim(cam,(0,0,0))
scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=18;scene.cycles.use_denoising=False
scene.view_settings.view_transform='AgX';scene.render.film_transparent=True;scene.render.image_settings.file_format='PNG'
scene.render.resolution_x=1350;scene.render.resolution_y=670;scene.render.resolution_percentage=100
scene.render.image_settings.color_mode='RGBA';bpy.context.preferences.filepaths.save_version=0
for group,keys in groups:
    for ob in all_obs.values():ob.hide_render=ob.name not in keys
    if group=='Gratmontur':
        layout={'R_Wangenmaske':(-.96,.01,1.55),'R_Gratharnisch':(-.26,.07,1.78),
                'R_Scharrstulpe_L':(.28,.31,1.55),'R_Scharrstulpe_R':(.56,.29,1.55),
                'R_Gratschiene_L':(.84,.05,1.60),'R_Gratschiene_R':(1.09,.03,1.60)}
        for key,(x,z,scale) in layout.items():
            o=all_obs[key];o.location=(x,0,z);o.scale=(scale,)*3;o.rotation_euler.z=.18 if key.endswith('_R') else -.11
        data.ortho_scale=2.66
    else:
        data.ortho_scale=2.68
        for i,key in enumerate(keys):
            o=all_obs[key];vmin=summary[key]['blenderMin'];vmax=summary[key]['blenderMax']
            scale=[.86,1.20,1.02,.98,1.18][i]
            if group=='Muehlwehr' and i==1:scale=1.12
            if group=='Schlackengarde' and i==3:scale=1.23
            o.scale=(scale,)*3;o.location=((-1.04+i*.52),0,-(vmin[2]+vmax[2])*.5*scale)
            o.rotation_euler.z=-.13+.08*(i%2)
    scene.render.filepath=str(preview/(group+'.png'));bpy.ops.render.render(write_still=True)
for group_idx,(_,keys) in enumerate(groups):
    x=0
    for key in keys:
        ob=all_obs[key];ob.scale=(1,1,1);ob.rotation_euler=(0,0,0);ob.hide_render=False
        ob.location=(group_idx*2.27+x-summary[key]['blenderMin'][0],0,-summary[key]['blenderMin'][2])
        x+=summary[key]['dimensionsM'][0]+.10
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.region_3d.view_location=(3.8,0,.4);area.spaces.active.region_3d.view_distance=9
bpy.ops.wm.save_as_mainfile(filepath=str(preview/'Werkarsenal.blend'),compress=True)
if '--icons' in args:
    from werk_icons import render_icons
    render_icons(all_obs,output)
