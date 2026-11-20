"""Independent FBX import, including real skin influence, field-marker and reversible pose checks."""
from pathlib import Path
import sys,json,math
import numpy as np
import bpy
from mathutils import Vector

root=Path(sys.argv[sys.argv.index('--')+1]).resolve()
reference=json.loads((root/'preview/Tiefscharrer-Messwerte.json').read_text())
model=root/'Assets/Resources/Equipment/Tiefscharrer/Tiefscharrer.fbx'
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(model),use_anim=False,ignore_leaf_bones=False)
objs=bpy.context.scene.objects;rig=next(o for o in objs if o.type=='ARMATURE')
assert set(reference['neutralBonesUnity'])<=set(b.name for b in rig.data.bones)
assert not bpy.data.actions
checked={'sourceFormat':'FBX 7.4','readerVersion':bpy.app.version_string,'nativeSourceClipsInFile':0,'lods':{}}
def positions(ob):
    ob=ob.evaluated_get(bpy.context.evaluated_depsgraph_get());me=ob.to_mesh()
    co=np.empty(len(me.vertices)*3,dtype='float32');me.vertices.foreach_get('co',co);ob.to_mesh_clear()
    return co.reshape(-1,3)
for name,expected in reference['lods'].items():
    ob=objs[name];me=ob.data;me.calc_loop_triangles()
    assert any(m.type=='ARMATURE' and m.object==rig for m in ob.modifiers),name
    assert len(me.materials)==1 and len(me.uv_layers)==2,name
    assert len(me.loop_triangles)==expected['triangles'],name
    marker=me.uv_layers[1];counts=[0]*8;points=[[] for _ in range(8)]
    for tr in me.loop_triangles:
        uv=[marker.data[i].uv for i in tr.loops]
        if all(v.y>.95 for v in uv):
            j=round(uv[0].x*8-.5)
            assert 0<=j<=7 and all(abs(v.x-(j+.5)/8)<.0002 for v in uv),name
            xyz=np.array([tuple(ob.matrix_world@me.vertices[i].co) for i in tr.vertices])
            if np.linalg.norm(np.cross(xyz[1]-xyz[0],xyz[2]-xyz[0]))>1.e-10:
                counts[j]+=1;points[j].append(xyz.mean(axis=0))
    assert min(counts)>0,(name,counts)
    centers=np.array([np.mean(c,axis=0) for c in points])
    assert np.min(centers[:,1])>1.30 and np.ptp(centers[:,0])>.16 and np.ptp(centers[:,2])>.16,(name,centers)
    maxn=max(len([g for g in v.groups if g.weight>.0001]) for v in me.vertices)
    err=max(abs(sum(g.weight for g in v.groups)-1) for v in me.vertices)
    assert maxn<=4 and err<.0001,(name,maxn,err)
    checked['lods'][name]={'triangles':len(me.loop_triangles),'oneMaterial':True,'anatomicalRegions':counts,
       'centerUnityM':[[round(float(p[i]),5) for i in (0,2,1)] for p in centers],'maxSkinInfluences':maxn,'maxWeightError':err}
ob=objs['Tiefscharrer_LOD0'];base=positions(ob);b=rig.pose.bones['TS_ForeElbow_L']
b.rotation_mode='XYZ';b.rotation_euler.z=.19;bpy.context.view_layer.update()
delta=np.linalg.norm(positions(ob)-base,axis=1);b.rotation_euler.z=0.;bpy.context.view_layer.update()
rest=float(np.max(np.linalg.norm(positions(ob)-base,axis=1)))
assert float(delta.max())>.025 and (delta>.002).sum()>100 and rest<.0001,(delta.max(),rest)
checked['blenderPoseProbe']={'bone':'TS_ForeElbow_L','imposedRadians':.19,'verticesMovedOver2mm':int((delta>.002).sum()),
      'maxMovementM':float(delta.max()),'restoredPoseMaxErrorM':rest,'purpose':'DCC bind test only; gameplay clips are evaluated in Unity.'}
(root/'preview/Tiefscharrer-FBX-Readback.json').write_text(json.dumps(checked,ensure_ascii=False,indent=2)+'\n')
print('All three native FBX levels, eight anatomical sectors, weights and reversible Blender pose passed.')
