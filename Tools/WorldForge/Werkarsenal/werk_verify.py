"""Read back the real FBX payload against the authored mesh without accessing runtime authorities."""
import json,sys
from pathlib import Path
import bpy

root=Path(sys.argv[sys.argv.index('--')+1]).resolve()
expected=json.loads((root/'preview/Werkarsenal-Messwerte.json').read_text())
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(root/'Assets/Resources/Equipment/Werkarsenal/Werkarsenal.fbx'),use_anim=False)
actual={o.name:o for o in bpy.context.scene.objects if o.type=='MESH'}
assert actual.keys()==expected.keys(),(actual.keys(),expected.keys())
readback={}
for name,ob in actual.items():
    me=ob.data;me.calc_loop_triangles();tri=len(me.loop_triangles)
    assert tri==expected[name]['triangles'],(name,tri,expected[name]['triangles'])
    dims=list(ob.dimensions)
    assert max(abs(x-y) for x,y in zip(dims,expected[name]['dimensionsM']))<.00015,(name,dims)
    assert max(abs(x) for x in ob.location)<.00001,(name,ob.location)
    assert len(me.uv_layers)==1 and len(me.materials)==1,name
    limits=[p.uv for p in me.uv_layers[0].data]
    assert all(.0<co.x<1 and .0<co.y<1 for co in limits),name
    readback[name]={'triangles':tri,'dimensionsM':[round(x,5) for x in dims],'materialSlots':len(me.materials),'uvSets':len(me.uv_layers)}
(root/'preview/Werkarsenal-FBX-Readback.json').write_text(json.dumps(readback,ensure_ascii=False,indent=2)+'\n')
print('Read back',len(readback),'separate FBX meshes and',sum(s['triangles'] for s in readback.values()),'triangles.')
