from pathlib import Path
import bpy
from mathutils import Vector

def render_icons(models,output):
    """Inventory silhouettes come from the actual mesh, with original proportions and transparent surround."""
    scene=bpy.context.scene
    output=Path(output)/'Icons';output.mkdir(exist_ok=True)
    for ob in list(scene.objects):
        if ob.type in ('LIGHT','CAMERA'):bpy.data.objects.remove(ob,do_unlink=True)
    world=bpy.data.worlds.new('Inventarlicht');world.use_nodes=True;scene.world=world
    world.node_tree.nodes['Background'].inputs[0].default_value=(.40,.46,.5,1)
    world.node_tree.nodes['Background'].inputs[1].default_value=.54
    def light(name,at,power,size,color):
        data=bpy.data.lights.new(name,'AREA');data.energy=power;data.size=size;data.color=color
        ob=bpy.data.objects.new(name,data);scene.collection.objects.link(ob);ob.location=at
        ob.rotation_euler=(-ob.location).to_track_quat('-Z','Y').to_euler()
    light('Helles Inventarlicht',(-1.2,1.7,1.9),115,1.45,(1.,.90,.76))
    light('Kantenlicht',(1.2,.4,1.),56,.95,(.70,.83,1.))
    light('Ruecklicht',(.5,-1.3,.75),79,1.,(1.,.79,.51))
    camdata=bpy.data.cameras.new('Inventarkamera');camdata.type='ORTHO';camdata.clip_start=.01
    cam=bpy.data.objects.new('Inventarkamera',camdata);scene.collection.objects.link(cam);scene.camera=cam
    look=Vector((.30,1.,.18)).normalized();cam.location=look*2.4
    cam.rotation_euler=(-look).to_track_quat('-Z','Y').to_euler()
    scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=12;scene.cycles.use_denoising=False
    scene.view_settings.view_transform='AgX';scene.render.film_transparent=True;scene.render.image_settings.file_format='PNG'
    scene.render.image_settings.color_mode='RGBA'
    scene.render.resolution_x=256;scene.render.resolution_y=256;scene.render.resolution_percentage=100
    for ob in models.values():ob.hide_render=True
    for key,ob in models.items():
        ob.hide_render=False;ob.scale=(1,1,1);ob.rotation_euler=(0,-.43 if key[2:] in ('Gratkeule','Splitterzahn','Scharrbeil','Ofengabel','Zollmesser','Hammerbeil','Wehrsaebel','Lotklinge','Jochbeil') else -.10,0)
        q=ob.rotation_euler.to_matrix()
        corners=[q@Vector(v) for v in ob.bound_box];p=(sum(corners,Vector())/8)
        ob.location=-p
        camcoords=[cam.rotation_euler.to_matrix().inverted()@(v-p) for v in corners]
        dims=[max(v[i] for v in camcoords)-min(v[i] for v in camcoords) for i in (0,1)]
        camdata.ortho_scale=max(dims)*1.115
        scene.render.filepath=str(output/(key+'.png'));bpy.ops.render.render(write_still=True)
        ob.hide_render=True
