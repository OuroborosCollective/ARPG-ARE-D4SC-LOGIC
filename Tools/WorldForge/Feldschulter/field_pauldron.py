"""Original Ashen Crypt field surgeon's pauldron. Blender background CPU author and FBX export."""
import argparse
import json
import math
from pathlib import Path
import sys
import bpy
import bmesh
from mathutils import Vector, Matrix

sys.path.insert(0,str(Path(__file__).resolve().parent))
from field_atlas import uv_rect

PI=math.pi
sin,cos=math.sin,math.cos
lerp=lambda a,b,t: a+(b-a)*t
unit=lambda v: Vector(v).normalized()
def clamp(x,a=0,b=1): return min(b,max(a,x))
def v3(x,y,z): return Vector((x,y,z))
# Calibrated with Unity 6000.6.8f1's native ModelImporter. An outward left shoulder
# must occupy avatar -X and its anterior tip +Z after the complete FBX import.
def blender_point(p): return (p.x,p.z,p.y)

def silhouette(a,p):
    cs,sn=cos(a),sin(a)
    s,c=sin(p),cos(p)
    rr=s if c>0 else .77+.23*s
    lower=clamp((p-1.08)/1.44)
    cx=.015-.034*lower
    # Blended medial shortening clears neck rotation.
    medial=clamp((-cs+.04)/1.04)
    rx=.166-.063*medial**1.45
    ry=.100 if c>=0 else .146
    # A slightly shallower back and a fuller anterior make the two ends distinct.
    rz=.140+.009*sn
    lobe=.0009*sin(a*3.0+.47)*rr*rr
    return v3(cx-cs*(rx*rr+lobe),-.019+c*ry+.0025*sn*rr, sn*(rz*rr+lobe)+.012*lower)
def surface(a,p,d=0,flute=0):
    q=silhouette(a,p)
    da=silhouette(a+.0005,p)-silhouette(a-.0005,p)
    dp=silhouette(a,p+.0005)-silhouette(a,p-.0005)
    norm=da.cross(dp).normalized()
    ref=v3(-cos(a)*sin(p),cos(p),(sin(a)*sin(p)))
    if norm.dot(ref)<0: norm=-norm
    if flute:
        # Long shallow forged flutes feather into the crown and flow into each lame.
        phase=8*a+.44*sin(p*1.8)+.22*sin(2*a)
        rib=(max(0,cos(phase))**4.5)*.0036
        d+=flute*rib*min(1,sin(p)*1.6)
    return q+norm*d, norm

# Crown, middle plate and lower plate have related, irregularly descending hems.
def arch(a): return (1+cos(a))*.5
def crown_end(a): return math.radians(48+39*arch(a)+4.6*cos(3*a)+2.1*sin(2*a))
def second_end(a): return math.radians(71+46*arch(a)+4.6*cos(3*a+.18)+1.7*sin(2*a))
def bottom_end(a): return math.radians(90+55*arch(a)+5.2*cos(3*a+.22)+1.7*sin(2*a))
def pad_end(a): return math.radians(82+71*arch(a)+5*cos(3*a+.22)+1.7*sin(2*a))

class Geometry:
    def __init__(self):
        self.points=[];self.polys=[];self.uvs=[];self.smooth=[]
    def vert(self,q):
        self.points.append(blender_point(q));return len(self.points)-1
    def face(self,indices,uvs,kind,smooth=True):
        self.polys.append(indices)
        self.uvs.append([uv_rect(kind,.012+.976*clamp(u),.012+.976*clamp(v)) for u,v in uvs])
        self.smooth.append(smooth)
    def sheet(self,pos,domain,steps,rows,kind,thickness,closed,distance,top_kind='dark',flutes=0):
        start,end=domain
        count=steps if closed else steps+1
        grid=[]
        for k in range(2):
            layer=[]
            for j in range(rows+1):
                v=j/rows
                ring=[]
                for i in range(count):
                    u=i/steps
                    a=lerp(start,end,u); p=pos(a,v)
                    q,n=surface(a,p,distance-thickness*k,flutes)
                    ring.append(self.vert(q))
                layer.append(ring)
            grid.append(layer)
        for k,layer in enumerate(grid):
            for j in range(rows):
                v=j/rows;vv=(j+1)/rows
                for i in range(steps):
                    h=(i+1)%count;u=i/steps;uu=(i+1)/steps
                    self.face((layer[j][i],layer[j][h],layer[j+1][h],layer[j+1][i]),
                              ((u,v),(uu,v),(uu,vv),(u,vv)),kind if k==0 else top_kind)
        for j in (0,rows):
            for i in range(steps):
                h=(i+1)%count
                self.face((grid[0][j][i],grid[1][j][i],grid[1][j][h],grid[0][j][h]),
                          ((i/steps,0),(i/steps,1),((i+1)/steps,1),((i+1)/steps,0)),top_kind)
        if not closed:
            for i in (0,count-1):
                for j in range(rows):
                    self.face((grid[0][j][i],grid[0][j+1][i],grid[1][j+1][i],grid[1][j][i]),
                              ((0,j/rows),(0,(j+1)/rows),(1,(j+1)/rows),(1,j/rows)),top_kind)
    def skin(self,pos,domain,steps,rows,kind,distance,closed):
        start,end=domain
        count=steps if closed else steps+1
        rings=[]
        for j in range(rows+1):
            row=[]
            for i in range(count):
                a=lerp(start,end,i/steps)
                q,n=surface(a,pos(a,j/rows),distance,1)
                row.append(self.vert(q))
            rings.append(row)
        for j in range(rows):
            for i in range(steps):
                h=(i+1)%count; u=i/steps; uu=(i+1)/steps;v=j/rows;vv=(j+1)/rows
                self.face((rings[j][i],rings[j][h],rings[j+1][h],rings[j+1][i]),((u,v),(uu,v),(uu,vv),(u,vv)),kind)
    def tube(self,points,radius,kind,sides,closed=False):
        if len(points)<2:return
        axis=None; seq=[]
        count=len(points)
        for i,c in enumerate(points):
            back=points[(i-1)%count] if closed or i else c
            ahead=points[(i+1)%count] if closed or i+1<count else c
            f=(ahead-back).normalized()
            if axis is None:
                temp=v3(0,1,0) if abs(f.y)<.88 else v3(0,0,1)
                axis=(temp-f*temp.dot(f)).normalized()
            else:
                axis=(axis-f*axis.dot(f)).normalized()
            side=f.cross(axis).normalized()
            r=radius(i/count) if callable(radius) else radius
            seq.append([self.vert(c+r*(axis*cos(2*PI*j/sides)+side*sin(2*PI*j/sides))) for j in range(sides)])
        for i in range(count if closed else count-1):
            h=(i+1)%count
            for j in range(sides):
                l=(j+1)%sides
                self.face((seq[i][j],seq[h][j],seq[h][l],seq[i][l]),
                          ((i/count,j/sides),((i+1)/count,j/sides),((i+1)/count,(j+1)/sides),(i/count,(j+1)/sides)),kind)
        if not closed:
            for k in (0,count-1):
                self.face(tuple(seq[k]),tuple((.5+.49*cos(j*2*PI/sides),.5+.49*sin(j*2*PI/sides)) for j in range(sides)),kind)
    def boss(self,q,n,r,kind,sides=10,washer=True,shape='pin'):
        main=(v3(0,1,0) if abs(n.y)<.9 else v3(0,0,1)).cross(n).normalized()
        aux=n.cross(main).normalized();rings=[]
        profile=((1.05,-.001),(.99,.0002),(.73,.0008),(.69,r*.32),(.30,r*.53)) if washer else ((1.,-.001),(.96,0.),(.65,r*.52),(.25,r*.73))
        for mult,h in profile:
            ring=[]
            for j in range(sides):
                a=2*PI*j/sides
                ring.append(self.vert(q+n*h+r*mult*(cos(a)*main+sin(a)*aux)))
            rings.append(ring)
        for k in range(len(rings)-1):
            for j in range(sides):
                l=(j+1)%sides
                self.face((rings[k][j],rings[k][l],rings[k+1][l],rings[k+1][j]),
                          ((j/sides,k/4),((j+1)/sides,k/4),((j+1)/sides,(k+1)/4),(j/sides,(k+1)/4)),kind if k or not washer else 'dark')
        for ring in (rings[0],rings[-1]):
            self.face(tuple(ring),tuple((.5+.46*cos(j*2*PI/sides),.5+.46*sin(j*2*PI/sides)) for j in range(sides)),kind)
    def build(self,name,mat):
        mesh=bpy.data.meshes.new(name);mesh.from_pydata(self.points,[],self.polys);mesh.update()
        uv=mesh.uv_layers.new(name='FieldAtlas')
        for pol,coords,smooth in zip(mesh.polygons,self.uvs,self.smooth):
            pol.use_smooth=smooth
            for i,uv_i in enumerate(pol.loop_indices):
                uv.data[uv_i].uv=coords[i]
        bm=bmesh.new();bm.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        bmesh.ops.triangulate(bm,faces=list(bm.faces),quad_method='BEAUTY',ngon_method='BEAUTY')
        bm.to_mesh(mesh);bm.free();mesh.update()
        mesh.materials.append(mat)
        o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);return o

def make_lod(level,mat):
    g=Geometry()
    seg=(64,35,24)[level]; rows=(10,5,3)[level]; sides=(6,4,3)[level]
    coords=lambda aa,pp,d=0,f=0:surface(aa,pp,d,f)[0]
    def hem(domain,fn,kind,r,d,steps,closed=False,flutes=0):
        start,end=domain
        strip=[coords(a:=lerp(start,end,i/steps),fn(a),d,flutes) for i in range(steps if closed else steps+1)]
        g.tube(strip,r,kind,sides,closed)
    entire=(-PI,PI)
    if level==2:
        # Only the distant silhouette and livery survive the screen-space threshold.
        # Rolled hems are broad enough to become a single metal strip.
        g.skin(lambda a,v:lerp(.025,pad_end(a),v),entire,22,4,'hide',-.0043,True)
        for low,high,span,full,label,dist,n in (
            (lambda a:.02,crown_end,entire,True,'crown',.0076,24),
            (lambda a:crown_end(a)-.18,second_end,(-2.59,2.59),False,'lame',.0025,18),
            (lambda a:second_end(a)-.15,bottom_end,(-2.19,2.19),False,'fauld',-.0005,15)):
            g.skin(lambda a,v:lerp(low(a),high(a),v),span,n,4 if full else 2,label,dist,full)
            g.skin(lambda a,v:high(a)-.032+.040*v,span,n,1,'edge',dist+.0008,full)
        # Front/back catches remain visible just beyond the silhouette even at distance.
        for a in (-1.23,1.35):
            p=crown_end(a)-.31;q,n=surface(a,p,.0085,1)
            g.boss(q,n,.0092,'bronze',6,False)
        return g.build('Feldschulter_LOD2',mat)
    g.sheet(lambda a,v:lerp(.014,pad_end(a),v),entire,seg,rows if not level else 3,'hide',.006,True,-.0043,'binding')
    hem(entire,pad_end,'binding',.0028,-.004,seg,True)
    # Wrap leather around the underside, retaining a free inside for the rig.
    for k,(center,width) in enumerate(((1.85,.075),(2.22,.084))):
        span=(-.97,4.65) if k==0 else (-.76,4.36)
        def belt(a,v):
            # Leather arcs climb clear of the neck on their medial ends.
            back=clamp((-cos(a)+.15)/1.15)
            return center+(v-.5)*width*2-.78*back
        g.sheet(belt,span,int(seg*.90),2,'strap',.0036,False,-.0051,'binding')
    layers=(
        (lambda a:.013,crown_end,entire,True,'crown',.0076),
        (lambda a:crown_end(a)-.18,second_end,(-2.59,2.59),False,'lame',.0025),
        (lambda a:second_end(a)-.15,bottom_end,(-2.19,2.19),False,'fauld',-.0005))
    for i,(lo,hi,domain,closed,kind,dist) in enumerate(layers):
        steps=seg if closed else int(seg*(domain[1]-domain[0])/(2*PI))
        g.sheet(lambda a,v:lerp(lo(a),hi(a),v),domain,steps,rows if not i else max(3,rows//2),
                kind,.0033 if not i else .0024,closed,dist,'dark',1)
        hem(domain,hi,'edge',.0024 if not i else .00165,dist+.00015,steps,closed,1)
        if level<2:
            # Twin rolled perimeter is dark oxidized iron above the brighter worn lip.
            hem(domain,lambda a:hi(a)-(.040 if not i else .032),'dark',.00095,dist+.0004,steps,closed,1)
        if level==0 and i<2:
            hem((-.98,1.34),lambda a:hi(a)-.065,'edge',.00043,dist+.0009,34,False,1)
    top,n=surface(.0,.013,.0081)
    g.boss(top,n,.0064,'dark',12 if not level else 8,True)
    # Press-work tapers from the protective crest into the shell, avoiding bulky added spikes.
    for a0 in (-1.82,-.98,.95,1.76):
        count=20 if not level else 10; curve=[]
        for k in range(count+1):
            t=k/count; a=a0+.15*sin(t*PI); p=lerp(.19,crown_end(a)-.20,t)
            curve.append(coords(a,p,.0094,1))
        g.tube(curve,lambda t:.00048+.0011*sin(PI*t)**.75,'dark',sides)
    # A quilted edge and tiny local cross-stitches show up when the pauldron is opened in inventory.
    if level<2:
        count=47 if not level else 16
        for i in range(count):
            a=lerp(-PI,PI,(i+.5)/count);spread=.020 if level==0 else .029
            q=[coords(a-spread,pad_end(a)-.047,-.0031),coords(a,pad_end(a)-.063,-.0021),coords(a+spread,pad_end(a)-.071,-.0031)]
            g.tube(q,.00063 if not level else .00079,'flax',4)
    # Rivets alternate with forged scallops. Retain landmarks at LOD1/2.
    for i,fn,span,n,r in ((0,crown_end,(-2.66,2.66),(12,9,5)[level],.0035),
                            (1,second_end,(-2.32,2.32),(10,7,5)[level],.0032),
                            (2,bottom_end,(-2.,2.),(9,6,4)[level],.003)):
        dist=layers[i][-1]
        for j in range(n):
            a=lerp(*span,(j+.5)/n);q,norm=surface(a,fn(a)-.13,dist+.0009,1)
            g.boss(q,norm,r,'bronze' if (j+2*i)%4 else 'edge',(10,7,5)[level],level==0)
    # Asymmetrical secondary pivot rosettes at front and heel.
    for a in (-1.23,1.35):
        p=crown_end(a)-.31;q,norm=surface(a,p,.0085,1)
        g.boss(q,norm,.0092,'bronze',(16,10,6)[level],level<2)
        if level==0:
            axis=(coords(a+.03,p,.009,1)-q).normalized()
            g.tube([q-axis*.0033+norm*.0052,q+axis*.0033+norm*.0052],.00065,'dark',5)
    # Old surgeon's device: a cupped poppy, three bound stems and a long running stitch.
    # Drawn on the curved shell in its own metric chart; never a floating flat decal.
    def mark(x,y,height=.0013):
        a=.165+x*.49; p=.92-y*.35
        return coords(a,p,.0091+height,1)
    def inlay(points,r,kind='bronze',height=.0013):
        seq=[]
        for (x0,y0),(x1,y1) in zip(points,points[1:]):
            steps=max(1,int(math.hypot(x1-x0,y1-y0)*(7 if not level else 3)))
            for j in range(steps):
                t=j/steps;seq.append(mark(lerp(x0,x1,t),lerp(y0,y1,t),height))
        seq.append(mark(*points[-1],height));g.tube(seq,r,kind,max(4,sides-1))
    # Trophy shape is intentionally open at the crown, like a blossom cut from old metal.
    inlay([(-.77,.41),(-.67,.03),(-.51,-.38),(-.18,-.67),(0,-1.0),(.16,-.67),(.45,-.39),(.64,-.01),(.73,.39)],.00105)
    inlay([(0,-.75),(-.06,-.25),(.02,.08),(.04,.44),(.16,.69)],.00175,'bronze')
    inlay([(.035,.16),(-.20,.33),(-.38,.58),(-.45,.87),(-.21,.77),(.015,.58)],.00155)
    inlay([(.036,.21),(.27,.43),(.45,.72),(.47,.92),(.24,.81),(.07,.67)],.0014)
    inlay([(-.07,-.44),(-.31,-.25),(-.39,-.08),(-.21,-.14),(-.05,-.31)],.0012)
    inlay([(0,-.20),(.26,-.04),(.37,.17),(.20,.11),(-.01,-.04)],.00115)
    if level<2:
        for y in (-.45,-.57,-.69):
            inlay([(-.115,y+.070),(.085,y-.025)],.00083,'edge',.0020)
        for x,y in ((-.64,.61),(.69,.56),(-.51,.97),(.49,1.08),(0.,.87)):
            a=.165+x*.49;p=.92-y*.35;q,n=surface(a,p,.0096,1);g.boss(q,n,.00185,'bronze',7,False)
    # A tanned front tab carries a cast rectangular buckle and hinged tongue.
    for a,p in ((1.39,1.60),(-1.35,1.59)):
        g.sheet(lambda aa,v:lerp(p-.16,p+.35,v),(a-.091,a+.091),max(3,seg//12),5,'strap',.003,False,.0051,'binding')
        def buckle_q(da,dp):
            return coords(a+da,p+.05+dp,.0084)
        corner=.017
        pts=[]
        for x,y,sx,sy in ((.094,.102,1,1),(-.094,.102,-1,1),(-.094,-.102,-1,-1),(.094,-.102,1,-1)):
            pts.append(buckle_q(x-sx*corner,y) if sx==sy else buckle_q(x,y-sy*corner))
            pts.append(buckle_q(x,y-sy*corner) if sx==sy else buckle_q(x-sx*corner,y))
        g.tube(pts,.0020,'bronze',sides,True)
        g.tube([buckle_q(.004,-.099),buckle_q(-.004,.025),buckle_q(-.012,.074)],.00125,'edge',sides)
        q,n=surface(a,p-.09,.0065);g.boss(q,n,.0030,'bronze',(9,6,5)[level],False)
    return g.build(f'Feldschulter_LOD{level}',mat)

def make_material(assetdir):
    mat=bpy.data.materials.new("Feldschulter_Atlas");mat.use_nodes=True
    tree=mat.node_tree;bs=tree.nodes.get('Principled BSDF')
    def tex(name,color_space):
        n=tree.nodes.new('ShaderNodeTexImage');n.image=bpy.data.images.load(str(assetdir/f'Feldschulter_{name}.png'),check_existing=True)
        n.image.colorspace_settings.name=color_space
        return n
    base=tex('BaseColor','sRGB');metal=tex('MetalSmooth','Non-Color');normal=tex('Normal','Non-Color')
    # R/A are independent data in the Unity map, so disable alpha-premultiplied RGB.
    metal.image.alpha_mode='CHANNEL_PACKED'
    base.location=(-660,300);metal.location=(-680,-30);normal.location=(-690,-300)
    split=tree.nodes.new('ShaderNodeSeparateColor');split.location=(-395,20)
    inv=tree.nodes.new('ShaderNodeMath');inv.operation='SUBTRACT';inv.inputs[0].default_value=1;inv.location=(-365,-165)
    bump=tree.nodes.new('ShaderNodeNormalMap');bump.inputs['Strength'].default_value=.51;bump.location=(-355,-325)
    for src,dest in ((base.outputs['Color'],bs.inputs['Base Color']),(metal.outputs['Color'],split.inputs[0]),
                     (split.outputs['Red'],bs.inputs['Metallic']),(metal.outputs['Alpha'],inv.inputs[1]),
                     (inv.outputs[0],bs.inputs['Roughness']),(normal.outputs['Color'],bump.inputs['Color']),(bump.outputs['Normal'],bs.inputs['Normal'])):
        tree.links.new(src,dest)
    return mat

def add_area(name,power,shade,origin,size,target):
    l=bpy.data.lights.new(name,'AREA');l.energy=power;l.color=shade;l.shape='DISK';l.size=size
    ob=bpy.data.objects.new(name,l);bpy.context.collection.objects.link(ob);ob.location=origin
    ob.rotation_euler=(target-ob.location).to_track_quat('-Z','Y').to_euler()
    return ob

def lookdev(hero,assetdir,path,res,samples):
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=samples
    scene.cycles.use_denoising=False;scene.render.threads_mode='FIXED';scene.render.threads=6
    scene.cycles.max_bounces=6;scene.world.color=(.28,.28,.28);scene.world.use_nodes=True
    worldbg=scene.world.node_tree.nodes.get("Background");worldbg.inputs['Color'].default_value=(.26,.30,.37,1);worldbg.inputs['Strength'].default_value=.24
    scene.view_settings.view_transform='AgX'
    scene.view_settings.look='AgX - Medium High Contrast'
    scene.view_settings.exposure=.03
    scene.render.film_transparent=False
    scene.render.image_settings.file_format='PNG'
    scene.render.resolution_x=res;scene.render.resolution_y=int(res*.83);scene.render.resolution_percentage=100
    # Product photo of the real model on a gently textured charcoal slab, against a darker wall.
    bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.1535))
    slab=bpy.context.object;slab.name='Lookdev Charcoal Plinth'
    sm=bpy.data.materials.new('Lookdev Fired Slate');sm.use_nodes=True
    nodes=sm.node_tree.nodes;links=sm.node_tree.links
    bs=nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.68;bs.inputs['Metallic'].default_value=.14
    nt=nodes.new('ShaderNodeTexNoise');nt.inputs['Scale'].default_value=35;nt.inputs['Detail'].default_value=6
    tc=nodes.new('ShaderNodeTexCoord');links.new(tc.outputs['Object'],nt.inputs['Vector'])
    ramp=nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].color=(.012,.018,.022,1);ramp.color_ramp.elements[1].color=(.055,.062,.061,1)
    links.new(nt.outputs['Fac'],ramp.inputs['Fac']);links.new(ramp.outputs[0],bs.inputs['Base Color'])
    b=nodes.new('ShaderNodeBump');b.inputs['Strength'].default_value=.25;b.inputs['Distance'].default_value=.0008
    links.new(nt.outputs['Fac'],b.inputs['Height']);links.new(b.outputs['Normal'],bs.inputs['Normal'])
    slab.data.materials.append(sm)
    focus=Vector(blender_point(v3(-.025,-.035,0)))
    camdata=bpy.data.cameras.new('Lookdev Optics');cam=bpy.data.objects.new('Lookdev Camera',camdata);bpy.context.collection.objects.link(cam)
    def position_cam(direction,scale):
        cam.location=focus+Vector(blender_point(unit(direction)))*.88
        cam.rotation_euler=(focus-cam.location).to_track_quat('-Z','Y').to_euler()
        camdata.type='ORTHO';camdata.ortho_scale=scale;camdata.clip_start=.01;camdata.clip_end=30
    position_cam(v3(-1.0,.45,.78),.453)
    scene.camera=cam
    def lamp(name,energy,col,loc,size):
        return add_area(name,energy,col,Vector(blender_point(v3(*loc))),size,focus)
    lamp('Silk overhead',7.4,(1.,.85,.72),(-.25,.42,.15),.37)
    lamp('Cool vellum strip',7.6,(.65,.81,1.),(.06,.20,-.37),.24)
    lamp('Near silver reflection',3.6,(.82,.90,1.),(-.49,.01,-.085),.29)
    lamp('Amber kicker',4.8,(1.,.56,.32),(.28,.22,.19),.23)
    lamp('Card for lower steel',1.3,(1.,.90,.73),(-.19,-.04,.39),.33)
    scene.render.filepath=str(path/'Feldschulter-Werkansicht.png');bpy.ops.render.render(write_still=True)
    slab.hide_render=True
    scene.render.resolution_x=256;scene.render.resolution_y=256;scene.render.film_transparent=True
    scene.cycles.samples=max(samples,12)
    position_cam(v3(-1.,.34,.66),.347)
    scene.render.filepath=str(assetdir/'Feldschulter_Icon.png');bpy.ops.render.render(write_still=True)

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--out',type=Path,required=True)
    parser.add_argument('--lookdev',type=Path)
    parser.add_argument('--blend',type=Path)
    parser.add_argument('--resolution',type=int,default=1260)
    parser.add_argument('--samples',type=int,default=28)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    out=args.out.resolve();out.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version=0
    scene=bpy.context.scene
    if not scene.world:scene.world=bpy.data.worlds.new("Studio air")
    scene.unit_settings.system='METRIC';scene.unit_settings.length_unit='METERS'
    mat=make_material(out)
    pieces=[make_lod(i,mat) for i in range(3)]
    results={'authorUnit':'m','avatarBoundsAxes':'+X right, +Y up, +Z anterior','lods':[]}
    for o in pieces:
        m=o.data
        if any(p.area<1e-15 for p in m.polygons):
            raise ValueError(f'Degenerate triangles in {o.name}')
        b0=[min(q.co.x for q in m.vertices),min(q.co.z for q in m.vertices),min(q.co.y for q in m.vertices)]
        b1=[max(q.co.x for q in m.vertices),max(q.co.z for q in m.vertices),max(q.co.y for q in m.vertices)]
        results['lods'].append({'name':o.name,'triangles':len(m.polygons),'vertices':len(m.vertices),'slots':len(m.materials),'boundLow':b0,'boundHigh':b1})
        o.select_set(True)
    bpy.context.view_layer.objects.active=pieces[0]
    bpy.ops.export_scene.fbx(filepath=str(out/'Feldschulter.fbx'),use_selection=True,object_types={'MESH'},
                             axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',
                             use_mesh_modifiers=True,mesh_smooth_type='OFF',use_tspace=False,add_leaf_bones=False,bake_anim=False,
                             path_mode='RELATIVE',embed_textures=False,bake_space_transform=True)
    for obj in pieces[1:]:obj.hide_render=True;obj.hide_set(True)
    if args.blend:
        args.blend.parent.mkdir(parents=True,exist_ok=True)
        for im in bpy.data.images:
            if im.source=='FILE' and im.filepath: im.pack()
        bpy.ops.wm.save_as_mainfile(filepath=str(args.blend.resolve()),compress=True)
    if args.lookdev:
        folder=args.lookdev.resolve();folder.mkdir(parents=True,exist_ok=True)
        (folder/'Feldschulter-LOD.json').write_text(json.dumps(results,indent=2))
        lookdev(pieces[0],out,folder,args.resolution,args.samples)
    print('FELDSCHULTER_GEOMETRY',json.dumps(results,sort_keys=True))

if __name__=='__main__': main()
