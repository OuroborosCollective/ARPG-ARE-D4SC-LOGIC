"""Render transparent inventory sprites from the assembled original .blend without changing existing assets."""
from pathlib import Path
import bpy
from mathutils import Vector
d=Path(__file__).resolve().parent
sc=bpy.context.scene
dst=d/'Assets/Resources/Equipment/AshWorks';dst.mkdir(parents=True,exist_ok=True)
jobs=[('Garde_Schlackenschild','G_Ziegelschild',(-.58,1.40,.20)),('Garde_Schuerhaken','G_Schuerhaken',(-.20,1.52,.23)),('Glocke_Handglocke','M_Handglocke',(-.37,1.4,-.20))]
for node,icon,view in jobs:
    model=bpy.data.objects[node]
    for ob in sc.objects:
        if ob.type=='MESH':ob.hide_render=ob!=model
    bbox=[model.matrix_world @ Vector(c) for c in model.bound_box]
    ctr=sum(bbox,Vector((0,0,0)))/8
    cam=sc.camera;cam.location=ctr+Vector(view);cam.rotation_euler=(ctr-cam.location).to_track_quat('-Z','Y').to_euler()
    bpy.context.view_layer.update();proj=[cam.matrix_world.inverted() @ p for p in bbox]
    wh=[max(q[i] for q in proj)-min(q[i] for q in proj) for i in [0,1]];cam.data.ortho_scale=max(wh)*1.13;cam.data.type='ORTHO'
    sc.render.engine='CYCLES';sc.cycles.samples=28;sc.cycles.use_denoising=False;sc.render.film_transparent=True
    sc.render.resolution_x=sc.render.resolution_y=256;sc.render.resolution_percentage=100;sc.render.image_settings.file_format='PNG';sc.render.image_settings.color_mode='RGBA'
    sc.render.filepath=str(dst/f'{icon}_Icon.png');bpy.ops.render.render(write_still=True)
