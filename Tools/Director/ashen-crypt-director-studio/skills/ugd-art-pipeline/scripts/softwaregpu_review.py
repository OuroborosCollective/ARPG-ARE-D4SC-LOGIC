"""Read the original GLBs through the pinned ARE diagnostics and render each real LOD in one camera."""
import argparse
import hashlib
from io import BytesIO
import json
from pathlib import Path
import re
import struct
import subprocess
import sys

import numpy as np
from PIL import Image

REVISION = "e4783dc62cf88591c8d114df1f5ed80b985d4f60"
ASSETS = ("Schieferbruch", "Gluthelmlinge", "Ossuarbogen", "Aschenhorcher")
SIZE = 144

def sha(data):
    return hashlib.sha256(data).hexdigest()

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("engine","models","output"):
        parser.add_argument("--"+name,type=Path,required=True)
    parser.add_argument("--asset",action="append",help="GLB basename with nodes <name>_LOD0..2; may be repeated")
    args=parser.parse_args()
    names=args.asset or ASSETS
    if len(set(names))!=len(names) or any(re.fullmatch(r"[\w -]{1,80}",n) is None for n in names):
        raise ValueError("Select distinct local GLB asset names")
    root=args.engine.resolve()
    revision=subprocess.check_output(["git","-C",str(root),"rev-parse","HEAD"],text=True).strip()
    if revision!=REVISION or subprocess.run(["git","-C",str(root),"diff","--quiet","HEAD","--","software_gpu","tools"]).returncode:
        raise ValueError("Unexpected engine source revision or paths")
    sys.path.insert(0,str(root))
    from tools.glb_diagnostic_evidence import inspect_diagnostic_glb, _embedded_accessor
    from software_gpu.graphics.shader import Vertex, Matrix4, BlinnPhongShader
    from software_gpu.graphics.framebuffer import Framebuffer
    from software_gpu.graphics.rasterizer import SoftwareRasterizer

    args.output.mkdir(exist_ok=True,parents=True)
    result={"engine":revision,"size":SIZE,"shader":"ARE BlinnPhongShader and original base-color texture","pose":"authored mesh rest shape","assets":[]}
    for name in names:
        data=(args.models/(name+".glb")).read_bytes()
        diagnostics=inspect_diagnostic_glb(data)
        (args.output/(name+"-mesh.json")).write_text(json.dumps(diagnostics,sort_keys=True))
        length=struct.unpack_from("<I",data,12)[0]
        doc=json.loads(data[20:20+length]); binary=data[28+length:]
        file_result={"name":name,"source":sha(data),"lod":[]}
        frames=[]
        for lod in range(3):
            node=next(n for n in doc["nodes"] if n.get("name")==f"{name}_LOD{lod}")
            chain=doc["nodes"].index(node)
            parents={child:i for i,n in enumerate(doc["nodes"]) for child in n.get("children",[])}
            seen=set()
            while chain is not None:
                if chain in seen: raise ValueError("Invalid node cycle")
                seen.add(chain)
                if any(k in doc["nodes"][chain] for k in ("translation","rotation","scale","matrix")):
                    raise ValueError("LOD review expects baked model transforms")
                chain=parents.get(chain)
            primitive,=doc["meshes"][node["mesh"]]["primitives"]
            record=next(p for p in diagnostics["primitives"] if (p["mesh"],p["primitive"])==(node["mesh"],0))
            if record["topology"]["status"]!="analyzed": raise ValueError(record)
            attrs=primitive["attributes"]
            def read(key,width):
                a=_embedded_accessor(doc,binary,attrs[key],width,(5126,),100000)
                if a is None: raise ValueError(key)
                return np.array(a,dtype=np.float32)
            coords=read("POSITION",3);normals=read("NORMAL",3);uv=read("TEXCOORD_0",2)
            indices=_embedded_accessor(doc,binary,primitive["indices"],1,(5121,5123,5125),200000)
            faces=np.asarray(indices,dtype=np.int32).reshape((-1,3))
            if lod==0:
                pbr=doc["materials"][primitive["material"]]["pbrMetallicRoughness"]
                view=doc["bufferViews"][doc["images"][doc["textures"][pbr["baseColorTexture"]["index"]]["source"]]["bufferView"]]
                offset=view.get("byteOffset",0)
                with Image.open(BytesIO(binary[offset:offset+view["byteLength"]])) as img:
                    color=np.asarray(img.convert("RGB"))
                centre=(coords.min(axis=0)+coords.max(axis=0))/2
                radius=np.linalg.norm(coords.max(axis=0)-coords.min(axis=0))/2
                direction=np.array([.54,.37,1.0],dtype=np.float32);direction/=np.linalg.norm(direction)
                camera=Matrix4.look_at(centre+direction*radius*2.90,centre,np.array([0,1,0],np.float32))
                shader=BlinnPhongShader(Matrix4.identity(),camera,Matrix4.perspective(np.deg2rad(44),1,.02,100),
                    texture=color,ambient_color=np.array([.38,.4,.44],np.float32),specular_color=np.array([.09,.09,.09],np.float32))
            verts=[Vertex(p,n,t) for p,n,t in zip(coords,normals,uv)]
            fb=Framebuffer(SIZE,SIZE); fb.clear()
            renderer=SoftwareRasterizer(fb,num_threads=1,backend="tiles")
            try: renderer.draw_mesh(verts,faces,shader)
            finally: renderer.executor.shutdown(wait=True)
            mask=fb.depth_buffer<1
            occupied=int(mask.sum())
            if occupied<100 or not np.all(np.isfinite(fb.depth_buffer)): raise ValueError(name+" lost depth")
            reference=frames[0] if frames else mask
            union=np.count_nonzero(reference|mask)
            overlap=float(np.count_nonzero(reference&mask)/union)
            path=args.output/f"{name}-LOD{lod}.png"
            Image.fromarray(fb.color_buffer).save(path)
            file_result["lod"].append({"level":lod,"triangles":len(faces),"depthPixels":occupied,"silhouetteIouVsLod0":round(overlap,6),
                                       "rgba8":sha(fb.color_buffer.tobytes()),"depthFloat32":sha(fb.depth_buffer.astype("<f4").tobytes()),"png":sha(path.read_bytes())})
            frames.append(mask.copy())
        result["assets"].append(file_result)
        print(name,[v["silhouetteIouVsLod0"] for v in file_result["lod"]],flush=True)
    (args.output/"are-asset-review.json").write_text(json.dumps(result,indent=2,sort_keys=True)+"\n")

if __name__=="__main__":
    main()
