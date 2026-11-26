"""Shade the unified distant art, export Unity projection data and editable glTF."""
import json
import struct
from pathlib import Path
import numpy as np

def smooth_ground(xyz, normal, areas, n):
    # Shared terrain vertices use the same light on both triangles at a diagonal.
    # Surface polygons can wrap a lip; the original index order remains intact.
    surface=normal[:n].copy()
    surface[surface[:,2]<0] *= -1
    _,inverse=np.unique(xyz[:n].reshape(-1,3).round(4),axis=0,return_inverse=True)
    aggregate=np.zeros((inverse.max()+1,3))
    np.add.at(aggregate,inverse,np.repeat(surface*np.clip(areas[:n],.005,20)[:,None]**.4,3,axis=0))
    aggregate/=np.maximum(1.e-12,np.linalg.norm(aggregate,axis=1,keepdims=True))
    return aggregate[inverse].reshape(n,3,3)

DETAIL=("none","masonry","rock","timber","earth")
TILE=np.asarray([1.,3.,5.,2.5,4.])
def shaders(mesh,rec,names,palette,atmospheres,indoors):
    xyz=np.asarray(mesh.tris,dtype=np.float64);tags=np.array(mesh.tags)
    face=np.cross(xyz[:,1]-xyz[:,0],xyz[:,2]-xyz[:,0]);lens=np.linalg.norm(face,axis=1)
    if (lens<=1e-9).any():raise ValueError(f"{rec['id']}: degenerate triangles")
    normal=face/lens[:,None]
    nr=np.repeat(normal[:,None,:],3,axis=1)
    n=getattr(mesh,'landscape_surface_count',mesh.terrain_count)
    if n: nr[:n]=smooth_ground(xyz,normal,lens,n)
    centers=xyz.mean(axis=1)
    typ=np.array([2 if m in ['mineral','slate','roof'] else 4 if m in ['ash','leaf','moss'] else
                  3 if m in ['bark','wood'] else 1 if m in ['trim','stone','basalt','dark'] else 0 for m in names],np.uint8)[tags]
    natural={'mineral','slate','basalt','trim','stone','dark'}
    rockmat=np.isin(tags,[names.index(x) for x in natural])
    typ[:mesh.terrain_count][rockmat[:mesh.terrain_count]]=2
    last=mesh.terrain_count
    for kind,end in mesh.ranges:
        if kind in ('nest','bear_den','side_rift','standing_rock','cairn'):
            typ[last:end][rockmat[last:end]]=2
        last=end
    sun=np.array([-.56,-.44,.70])
    light=np.einsum('ijk,k->ij',nr,sun)
    lum=.76+.25*np.maximum(0,light)+.12*np.maximum(0,nr[:,:,2])+.03*np.maximum(0,-light)
    # Distributed weathering is continuous in space. Avoid triangle-sized jumps on scree.
    broad=.035*np.sin(xyz[:,:,0]*.21+xyz[:,:,2]*.39)*np.cos(xyz[:,:,1]*.29)+.018*np.sin(xyz[:,:,2]*1.33+xyz[:,:,0]*.07)
    grain=.025*np.sin(centers[:,0]*1.73+centers[:,1]*2.47+centers[:,2]*1.51);grain[:n]=0
    shaded=palette[tags,None,:]*(lum[:,:,None]+broad[:,:,None]+grain[:,None,None])
    hot=np.isin(tags,[names.index('ember'),names.index('amber')]);shaded[hot]=palette[tags[hot],None,:]*(1+broad[hot,:,None]*.3)
    d=np.linalg.norm(xyz[:,:,:2],axis=2)
    fog=np.clip((d-(43 if indoors else 46))/(146 if indoors else 148),0,1)**1.25*(.47 if indoors else .79);fog[hot]*=.45
    air=np.array(atmospheres.get(rec['id'],atmospheres.get(rec['style'],(148,146,150))))
    c=shaded*(1-fog[:,:,None])+air*fog[:,:,None]
    c[typ>0]*=1.14
    return xyz.astype('<f4'),np.rint(c).clip(0,255).astype('u1'),typ

def stream(path,vertices,colors,types):
    u=vertices[:,:,[0,2,1]].copy()
    record=np.empty(u.shape[:2],dtype=[('position','<f4',(3,)),('color','u1',(4,))])
    record['position']=u;record['color'][:,:,:3]=colors;record['color'][:,:,3]=types[:,None]
    path.write_bytes(b'WFH1'+struct.pack('<i',len(u))+record.tobytes())

def glb(path,xyz,col,types,rec,texture_dir):
    # Geometry in glTF is right-handed +Y up / north -Z. Texture V=0 points to the top row.
    p=xyz[:,:,[0,2,1]].copy();p[:,:,2]*=-1
    n=np.cross(p[:,1]-p[:,0],p[:,2]-p[:,0]);n/=np.linalg.norm(n,axis=1)[:,None]
    pos=p.reshape(-1,3);norms=np.repeat(n,3,axis=0)
    # Use source axes for UVs. The runtime uses the same dominant-plane rule.
    floor=np.abs(n[:,1])>.71
    side=np.abs(n[:,0])>np.abs(n[:,2])
    x,y,z=xyz[:,:,0],xyz[:,:,1],xyz[:,:,2]
    texu=np.where((~floor&side)[:,None],y,x)/TILE[types,None]
    texv=-np.where(floor[:,None],y,z)/TILE[types,None]
    rgb=col.reshape(-1,3).astype(float)/255
    linear=np.where(rgb<=.04045,rgb/12.92,((rgb+.055)/1.055)**2.4)
    verts=np.zeros(len(pos),dtype=[('p','<f4',3),('n','<f4',3),('c','<u2',4),('uv','<f4',2)])
    verts['p']=pos;verts['n']=norms;verts['c'][:,:3]=np.rint(linear*65535).astype('<u2');verts['c'][:,3]=65535
    verts['uv']=np.stack([texu,texv],axis=-1).reshape(-1,2)
    unique,idx=np.unique(verts,return_inverse=True);idx=idx.reshape(-1,3)
    content=bytearray();views=[]
    def buf(data,**attributes):
        while len(content)%4:content.append(0)
        vi=len(views);views.append({'buffer':0,'byteOffset':len(content),'byteLength':len(data),**attributes});content.extend(data);return vi
    vb=buf(unique.tobytes(),byteStride=40,target=34962)
    acc=[{"bufferView":vb,"byteOffset":0,"componentType":5126,"count":len(unique),"type":"VEC3","min":pos.min(axis=0).tolist(),"max":pos.max(axis=0).tolist()},
         {"bufferView":vb,"byteOffset":12,"componentType":5126,"count":len(unique),"type":"VEC3"},
         {"bufferView":vb,"byteOffset":24,"componentType":5123,"normalized":True,"count":len(unique),"type":"VEC4"},
         {"bufferView":vb,"byteOffset":32,"componentType":5126,"count":len(unique),"type":"VEC2"}]
    materials=[];images=[];textures=[];primitives=[]
    itype=5123 if len(unique)<=65535 else 5125
    for typ,detail in enumerate(DETAIL):
        pbr={"metallicFactor":0,"roughnessFactor":.94}
        if typ:
            image=buf((texture_dir/(detail+'.png')).read_bytes())
            pbr['baseColorTexture']={'index':len(textures)}
            textures.append({'sampler':0,'source':len(images)})
            images.append({'name':detail,'mimeType':'image/png','bufferView':image})
        materials.append({'name':detail,'pbrMetallicRoughness':pbr,'doubleSided':True,'extensions':{'KHR_materials_unlit':{}}})
        subset=idx[types==typ].astype('<u2' if itype==5123 else '<u4')
        if len(subset):
            ab=buf(subset.tobytes(),target=34963)
            primitives.append({'attributes':{'POSITION':0,'NORMAL':1,'COLOR_0':2,'TEXCOORD_0':3},'indices':len(acc),'material':typ})
            acc.append({'bufferView':ab,'byteOffset':0,'componentType':itype,'count':subset.size,'type':'SCALAR'})
    g={'asset':{'version':'2.0','generator':'OuroborosCollective · Ashen Crypt / Weltferne'},'scene':0,'scenes':[{'nodes':[0]}],
       'nodes':[{'mesh':0,'name':rec['name']}],'meshes':[{'name':rec['name'],'primitives':primitives}], 'materials':materials,'images':images,'textures':textures,
       'samplers':[{'magFilter':9729,'minFilter':9987,'wrapS':10497,'wrapT':10497}],
       'extensionsUsed':['KHR_materials_unlit'],'buffers':[{'byteLength':len(content)}],'bufferViews':views,'accessors':acc}
    js=json.dumps(g,ensure_ascii=False,separators=(',',':')).encode();js+=b' '*(-len(js)%4)
    while len(content)%4:content.append(0)
    path.write_bytes(struct.pack('<4sII',b'glTF',2,28+len(js)+len(content))+struct.pack('<I4s',len(js),b'JSON')+js+struct.pack('<I4s',len(content),b'BIN\0')+content)
