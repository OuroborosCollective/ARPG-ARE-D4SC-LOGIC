"""Original Ashen Crypt wardrobe, built in metric character sockets. Blender background entry point."""
from pathlib import Path
import sys,math,json
import bpy,bmesh
import numpy as np
from mathutils import Vector
from math import sin,cos,pi,tau
ROOT=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT))
from palette import atlas_uv
OUT=ROOT/'Assets/Resources/Equipment/AshWorks';VIEWS=ROOT/'ansichten'
VIEWS.mkdir(exist_ok=True);OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True);scene=bpy.context.scene
scene.unit_settings.system='METRIC';scene.unit_settings.length_unit='METERS'
bpy.context.preferences.filepaths.save_version=0
v=Vector
def mix(a,b,t):return a*(1-t)+b*t
def fit(x,a=0,b=1):return max(a,min(x,b))
def path_poly(points,n,closed=False):
    p=[v(k) for k in points];sample=[]
    count=len(p) if closed else len(p)-1
    for i in range(count):
        aa=p[(i-1)%len(p)] if i>0 or closed else p[i]
        bb=p[i];cc=p[(i+1)%len(p)];dd=p[(i+2)%len(p)] if (i+2<len(p) or closed) else cc
        for j in range(n):
            t=j/n
            sample.append((bb*2+(-aa+cc)*t+(aa*2-bb*5+cc*4-dd)*t*t+(-aa+bb*3-cc*3+dd)*t*t*t)*.5)
    if not closed:sample.append(p[-1])
    return sample
def mat():
    m=bpy.data.materials.new('AW_Werkstoff');m.use_nodes=True
    nt=m.node_tree;p=nt.nodes.get('Principled BSDF')
    def img(k,color):
        n=nt.nodes.new('ShaderNodeTexImage');im=bpy.data.images.load(str(OUT/f'Werkstoff_{k}.png'));im.colorspace_settings.name=color;n.image=im;return n
    c=img('BaseColor','sRGB');nt.links.new(c.outputs['Color'],p.inputs['Base Color'])
    z=img('RoughMetal','Non-Color');sep=nt.nodes.new('ShaderNodeSeparateColor');nt.links.new(z.outputs[0],sep.inputs[0])
    nt.links.new(sep.outputs['Green'],p.inputs['Roughness']);nt.links.new(sep.outputs['Blue'],p.inputs['Metallic'])
    q=img('Normal','Non-Color');no=nt.nodes.new('ShaderNodeNormalMap');no.inputs['Strength'].default_value=.50
    nt.links.new(q.outputs[0],no.inputs['Color']);nt.links.new(no.outputs[0],p.inputs['Normal'])
    return m
M=mat()
class Part:
    def __init__(self,name,bone):
        self.name=name;self.bone=bone;self.v=[];self.f=[];self.uv=[];self.sm=[]
    def emit(self,pts,faces,tile,uv=None,smooth=False):
        start=len(self.v);self.v.extend(tuple(p) for p in pts)
        for ix,face in enumerate(faces):
            self.f.append(tuple(start+j for j in face));self.sm.append(smooth)
            if uv:vals=uv[ix]
            elif len(face)==3:vals=[(.06,.04),(.94,.06),(.90,.92)]
            elif len(face)==4:vals=[(.025,.025),(.975,.025),(.975,.975),(.025,.975)]
            else:vals=[(.5+.47*cos(tau*i/len(face)),.5+.47*sin(tau*i/len(face))) for i in range(len(face))]
            self.uv.append([atlas_uv(tile,fit(a),fit(b)) for a,b in vals])
    def grid(self,nx,ny,fn,tile,thick=0,rev=False,smooth=True):
        front=[v(fn(i/nx,j/ny)) for j in range(ny+1) for i in range(nx+1)]
        faces=[];uv=[];stride=nx+1
        for j in range(ny):
            for i in range(nx):
                id=j*stride+i;faces.append((id,id+1,id+1+stride,id+stride))
                uv.append([(i/nx,j/ny),((i+1)/nx,j/ny),((i+1)/nx,(j+1)/ny),(i/nx,(j+1)/ny)])
        if thick:
            normals=[]
            for j in range(ny+1):
                for i in range(nx+1):
                    a=front[j*stride+min(nx,i+1)]-front[j*stride+max(0,i-1)]
                    b=front[min(ny,j+1)*stride+i]-front[max(0,j-1)*stride+i]
                    normals.append(a.cross(b).normalized())
            off=len(front);front.extend([pt+thick*n for pt,n in zip(front[:off],normals)])
            originals=list(faces);orig_uv=list(uv)
            faces.extend(tuple(ix+off for ix in reversed(face)) for face in originals)
            uv.extend(list(reversed(k)) for k in orig_uv)
            edge=list(range(nx+1))+[j*stride+nx for j in range(1,ny+1)]+[ny*stride+i for i in range(nx-1,-1,-1)]+[j*stride for j in range(ny-1,0,-1)]
            for i,a in enumerate(edge):
                b=edge[(i+1)%len(edge)];faces.append((b,a,a+off,b+off));uv.append([(.15,.10),(.84,.10),(.84,.25),(.15,.25)])
        if rev:faces=[tuple(reversed(x)) for x in faces];uv=[list(reversed(x)) for x in uv]
        self.emit(front,faces,tile,uv,smooth)
    def loop(self,pts,rad,tile,n=6,closed=False):
        ps=[v(p) for p in pts]
        axis=v((0,0,1));d=(ps[1]-ps[0]).normalized()
        if abs(axis.dot(d))>.88:axis=v((0,1,0))
        vv=[];faces=[];uv=[];seg=len(ps)
        for k,p in enumerate(ps):
            before=ps[k-1] if (k>0 or closed) else ps[k];after=ps[(k+1)%seg] if (k+1<seg or closed) else ps[k]
            tang=(after-before).normalized();next_axis=axis-tang*axis.dot(tang)
            axis=next_axis.normalized() if next_axis.length>.05 else tang.orthogonal().normalized()
            side=tang.cross(axis).normalized()
            r=rad[k] if isinstance(rad,(tuple,list,np.ndarray)) else rad
            for j in range(n):vv.append(p+r*(cos(tau*j/n)*axis+sin(tau*j/n)*side))
        for k in range(seg if closed else seg-1):
            m=(k+1)%seg
            for j in range(n):
                h=(j+1)%n;faces.append((k*n+j,k*n+h,m*n+h,m*n+j))
                uv.append([(k/seg,j/n),(k/seg,(j+1)/n),((k+1)/seg,(j+1)/n),((k+1)/seg,j/n)])
        if not closed: 
            faces.extend([tuple(reversed(range(n))),tuple((seg-1)*n+i for i in range(n))])
            uv.extend([[(.5+.42*cos(tau*i/n),.5+.42*sin(tau*i/n)) for i in range(n)]]*2)
        self.emit(vv,faces,tile,uv,True)
    def ring(self,cen,xradius,yradius,thick,tile,n=32,hh=lambda a:0):
        self.loop([(cen[0]+xradius*sin(a),cen[1]+yradius*cos(a),cen[2]+hh(a)) for a in np.arange(n)*tau/n],thick,tile,6,True)
    def rivet(self,p,normal,r=.003,tile='bronzeedge',n=7):
        cen=v(p);d=v(normal).normalized();u=d.orthogonal().normalized();w=d.cross(u).normalized()
        pts=[cen+.45*r*d]
        for t in [.67,1.]:
            for j in range(n):pts.append(cen+(cos(tau*j/n)*u+sin(tau*j/n)*w)*r*t+d*(r*(1-t)*.41))
        fa=[(0,1+j,1+(j+1)%n) for j in range(n)]
        fa+= [(1+j,1+n+j,1+n+(j+1)%n,1+(j+1)%n) for j in range(n)]
        self.emit(pts,fa,tile,smooth=True)
    def shell(self,levels,tile,n=42,mod=None):
        def p(a,j):
            rx,ry,z=levels[j]
            return mod(a,j,rx,ry,z) if mod else (rx*sin(a),ry*cos(a),z)
        pts=[p(tau*i/n,j) for j in range(len(levels)) for i in range(n)]
        f=[];uv=[]
        for j in range(len(levels)-1):
            for i in range(n):
                ii=(i+1)%n;f.append((j*n+i,(j+1)*n+i,(j+1)*n+ii,j*n+ii))
                uv.append([(i/n,j/(len(levels)-1)),(i/n,(j+1)/(len(levels)-1)),((i+1)/n,(j+1)/(len(levels)-1)),((i+1)/n,j/(len(levels)-1))])
        self.emit(pts,f,tile,uv,True)
    def panel(self,poly,tile,d=.004):
        # Raised hand-cut metal/appliqué plate lying on the front of a garment.
        pts=[v(j) for j in poly];n=len(pts);pts2=pts+[k+v((0,-d,0)) for k in pts]
        f=[tuple(range(n)),tuple(reversed(range(n,2*n)))]+[(j,(j+1)%n,(j+1)%n+n,j+n) for j in range(n)]
        xs=[a.x for a in pts];zs=[a.z for a in pts];xmin,xmax=min(xs),max(xs);zmin,zmax=min(zs),max(zs)
        uv=[((a.x-xmin)/max(xmax-xmin,.00001),(a.z-zmin)/max(zmax-zmin,.00001)) for a in pts]
        tuv=[uv,list(reversed(uv))]+[[(.05,.1),(.94,.1),(.94,.25),(.05,.25)]]*n
        self.emit(pts2,f,tile,tuv)
    def finish(self):
        me=bpy.data.meshes.new(self.name);me.from_pydata(self.v,[],self.f);me.update()
        layer=me.uv_layers.new(name='Werkstoff')
        raw=[value for face in self.uv for pair in face for value in pair]
        layer.data.foreach_set('uv',np.asarray(raw,dtype=np.float32));me.polygons.foreach_set('use_smooth',self.sm)
        me.materials.append(M)
        bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(me);bm.free();me.update()
        obj=bpy.data.objects.new(self.name,me);scene.collection.objects.link(obj);obj['HumanoidSocket']=self.bone
        return obj

def hammer(a,t,s=.0015):return s*(.60*sin(7*a+2*t)+.35*cos(9*a-4*t)+.26*sin(11*a+.2))
def bb(p,cx,cy,cz,rx,rz,tile):
    # worn squared mounting plaque with softened corners and rim
    poly=[(-.85,-1),(.74,-1),(1,-.70),(1,.63),(.67,1),(-.79,.93),(-1,.52),(-1,-.70)]
    f=[(cx+x*rx,cy,cz+z*rz) for x,z in poly];p.panel(f,tile,.003);p.loop(f,.00145,'bronzeedge',5,True)
def draw_stitch(p,a,b,count,rad=.0008):
    a=v(a);b=v(b)
    for j in range(count):
        i=(j+.1)/count;t=(j+.59)/count
        p.loop([a.lerp(b,i),a.lerp(b,(i+t)*.5)+v((.0007,.0005,0)),a.lerp(b,t)],rad,'linen',4)
def seam(p,path,width=.002,tile='bright'):p.loop(path,width,tile,6)
def collar_shape(rx,ry,basez,a):return (rx*sin(a),ry*cos(a),basez)
def hood():
    p=Part('Garde_Ofenhaube','Head')
    # The visor is cut into three independent forged plates, leaving an actual sight opening.
    def mask(a,t,side=1):
        z=mix(-.055,.108,t);r=.094+.014*t
        return (r*sin(a),.008+(.110+.017*t)*cos(a),z-.012*cos(a))
    for lo,hi in [(-pi,-.50),(.50,pi),(-.50,.50)]:
        cutoff=(.78 if abs(lo+.50)<.001 else 1)
        fn=lambda u,v:mask(mix(lo,hi,u),v*cutoff)
        p.grid(18,6,fn,'iron',-.003)
    for s in [-1,1]:
        path=[mask(s*a,.79) for a in np.linspace(0,.53,10)]
        seam(p,path,.0025,'bright')
    # Wide asymmetric rolled furnace hood, blunt at the apex, long to the back and off-hand side.
    lev=[(.132,.155,.082),(.159,.163,.092),(.156,.162,.099),(.121,.134,.122),(.106,.116,.154),(.088,.092,.162),(.049,.057,.169),(.003,.003,.168)]
    def flange(a,i,rx,ry,z):
        eas=max(0,1-i/5)
        f=(1+.067*cos(7*a+.38)*eas+.095*max(0,-sin(a))*eas)
        # The private fitted skull rises 151 mm above its eyelids. Keep both edges of the eye
        # opening fixed; add clearance only above the broad rim, conserving the bottom edge and jaw.
        crown=(0.,0.,.002,.026,.047,.070,.085,.089)[i]
        bolster=(0.,0.,.001,.004,.007,.011,.006,0.)[i]
        return ((rx+bolster+hammer(a,i))*f*sin(a)-.026*(1-eas),-.004+(ry+bolster)*f*cos(a),z+crown+eas*(-.038*max(0,-sin(a))-.014*max(0,-cos(a))+.006*cos(3*a))+.003*sin(a+i*.43))
    p.shell(lev,'iron',48,flange)
    cuff=[flange(a,1,*lev[1]) for a in np.arange(54)*tau/54];p.loop(cuff,.0035,'bright',6,True)
    for a in [-2.1,-1.18,-.29,.72,1.84,2.65]:
        seam(p,[flange(a+.027*sin(j),j,*lev[j]) for j in range(2,7)],.0014,'bright')
        x,y,z=flange(a,3,*lev[3]);p.rivet((x,y,z+.002),(sin(a),cos(a),.8),.0035,'bright')
    for s in [-1,1]:
        for a in [.77,1.26,1.74]:
            pt=mask(s*a,.28);p.rivet(pt,(s*sin(a),cos(a),0),.0033,'bright')
    # Dark vent housing and a genuine raised grille offset to the wearer's left.
    c=v((-.054,.130,-.008));ux=v((.91,.40,0));up=v((0,0,1));n=v((-.4,.91,0))
    poly=[c+ux*x+up*z+n*.004 for x,z in [(-.036,-.020),(.027,-.020),(.037,-.01),(.033,.020),(.019,.028),(-.027,.023),(-.037,.013)]]
    p.panel(poly,'oildark',.003);p.loop(poly,.002,'bronze',6,True)
    for ax in [-.022,-.006,.010,.026]:
        for h in [-.010,.006,.015]:
            xx=ax+.009*((round(h*1000)%2)); verts=[]
            if abs(xx)>.029 or (h>.011 and abs(xx)>.017):continue
            for j in range(6):verts.append(c+ux*(xx+.007*cos(tau*j/6))+up*(h+.007*sin(tau*j/6))+n*.012)
            p.loop(verts,.00165,'bronzeedge',5,True)
    # Reinforced brow and two angled cheek folds break the draw of a simple cylinder.
    for side in [-1,1]:
        pts=[(side*.005,.128,.049),(side*.041,.127,.052),(side*.097,.092,.063)]
        p.loop(pts,.0028,'bright',6)
        pts=[(side*.016,.121,-.045),(side*.035,.125,-.013),(side*.087,.088,.009)]
        p.loop(pts,.0018,'bright',6)
        for j in range(3):p.rivet((side*(.016+.016*j),.121,.024),(0,1,0),.0019,'oildark')
    # Black, pleated neck gusset inherits the head angle, below the sharp cheek edge.
    p.shell([(.086,.098,-.055),(.090,.101,-.074),(.086,.095,-.092)],'nightcloth',36,
            lambda a,j,x,y,z:((x+.004*sin(13*a))*sin(a),(y+.004*sin(13*a))*cos(a),z+.008*cos(3*a)))
    return p
def chest_guard():
    p=Part('Garde_Lamellenwams','Chest')
    p.shell([(.151,.107,-.176),(.168,.115,-.10),(.194,.136,.015),(.195,.130,.095),(.163,.108,.137),(.105,.088,.162)],'oildark',52,
            lambda a,i,x,y,z:((x+.0025*sin(16*a+z))*sin(a),(y+.0025*sin(16*a+z))*cos(a),z-.012*max(0,cos(a))*(i>3)))
    for side in [0,1]:
        for row in range(4):
            for col in range(4):
                a0=-1.26+col*.63+.01+(pi if side else 0);a1=a0+.618
                def point(u,t):
                    a=mix(a0,a1,u);z=-.134+row*.058+t*.064+.008*cos(a)*sin(row+.2)+.005*sin(a*3+row)
                    rad=(.166+.032*fit((z+.13)/.215))*(1.0+.012*cos((u-.5)*tau));dep=.115+.028*fit((z+.13)/.215)
                    return ((rad+.004+.002*row)*sin(a),(dep+.006+.0013*row)*cos(a),z)
                p.grid(5,2,point,'iron',-.0038,smooth=False)
                seam(p,[point(u,.026) for u in np.linspace(0,1,6)],.0016,'bright')
                for u in [.12,.87]:
                    a=mix(a0,a1,u);xyz=v(point(u,.83));out=v((sin(a),cos(a),0)).normalized()
                    p.rivet(xyz+.0015*out,out,.0035,'bright')
    # Padded fire collar folds around the neck and ends in irregular yarn-bound rolls.
    for band in range(4):
        start=-pi+.16*band;end=pi-.15-.10*band
        pts=[];rad=[]
        for a in np.linspace(start,end,59):
            rx=.099+.017*band;ry=.076+.012*band
            pts.append((rx*sin(a),ry*cos(a),.147+.008*band-.033*cos(a)+.007*sin(4*a+.71*band)))
            rad.append(.016+.005*sin(3*a+band)**2)
        p.loop(pts,rad,'suede',9)
    for side in [-1,1]:
        def scarf(u,t):
            a=side*mix(.47,2.15,u);r=.098+.104*t+.005*sin(14*u-2*t)*t
            return (r*sin(a),(.075+.052*t)*cos(a),.160-.031*t-.033*t*t-.012*cos(a)+.005*cos(9*u+2*t))
        p.grid(14,7,scarf,'suede',.003,smooth=True)
        p.loop([scarf(u,.96) for u in np.linspace(0,1,18)],.0024,'oildark',5)
        for k in range(9):
            u=(k+.5)/10;st=v(scarf(u,1));p.loop([st,st+v((side*.001,0,-.009-.003*cos(k)))],.0012,'suede',5)
    for a in [-2.1,-1.48,-.68,.49,1.54,2.38]:
        pts=[((.099+.017*j)*sin(a),(.076+.012*j)*cos(a),.16+.009*j-.024*cos(a)) for j in range(4)]
        seam(p,pts,.002,'oildark')
    # Front latch, concentric marking and eight short hammer rays.
    bb(p,-.083,.142,.068,.025,.021,'bronze')
    cent=v((-.083,.146,.069))
    for j in range(10):
        a=j*tau/10;d=v((cos(a),0,sin(a)));p.loop([cent+d*.007,cent+d*.015],.00115,'bronzeedge',5)
    p.rivet(cent,(0,1,0),.005,'bronzeedge')
    bb(p,-.041,.154,.068,.016,.007,'leather')
    for x in [-.13,.097]:
        bb(p,x,.123,.033,.010,.036,'leather')
        for z in [.007,.058]:p.rivet((x,.127,z),(0,1,0),.003,'bronzeedge')
    return p
def sash():
    p=Part('Garde_Zaehlgurt','Hips')
    for j,(rx,ry,z) in enumerate([(.159,.113,.080),(.155,.109,.056),(.155,.107,.034)]):
        p.shell([(rx-.001,ry-.001,z-.013),(rx,ry,z-.011),(rx+.001,ry+.001,z+.009),(rx,ry,z+.012)],'leather',46,
                lambda a,i,x,y,z:(x*sin(a),y*cos(a),z+.010*sin(a-.60)))
    bb(p,-.006,.117,.060,.032,.022,'bronze');bb(p,-.006,.119,.060,.022,.012,'oildark')
    for side in [-1,1]:
        def apron(u,vv):
            x=side*(.027+.137*u)+.012*sin(pi*vv);z=.024-.215*vv-.012*sin(9*u+.9*side)*vv**3
            y=.119-.052*u*u+.018*vv+.006*sin(3*pi*u+2*vv);return x,y,z
        p.grid(12,8,apron,'oildark',.0021)
    def red(u,t):
        x=.065+(u-.5)*(.071-.026*t)+.014*sin(pi*t);y=.134+.016*sin(2.4*t)+.004*sin(tau*(u+t));z=.075-t*(.327+.015*sin(14*u)+.006*cos(7*u));return x,y,z
    p.grid(11,13,red,'tally',.0015)
    for col in range(3):
        for row in range(4 if col<2 else 3):
            q=np.array(red(.25+.15*col,.37+.062*row));p.loop([q+[0,.001,0],q+[.003,.001,.014]],.001,'linen',4)
    # A short split coat under the tally cloth covers the belt line in the walking stride.
    for st,en in [(.23,2.08),(-2.17,-.55),(1.92,4.26)]:
        def tail(u,t):
            a=mix(st,en,u)+.024*sin(pi*t)*cos(9*u);w=.006*t*sin(8*pi*u+2*t)
            return ((.155+.037*t+w)*sin(a),(.119+.049*t+w)*cos(a),.020-(.213+.039*sin(pi*u)+.010*cos(5*u))*t)
        p.grid(13,8,tail,'nightcloth',.002)
        p.loop([tail(u,.975) for u in np.linspace(0,1,16)],.002,'oildark',5)
    # Cross-strokes render as actual pale stitches under side light.
    for t in [.44,.68]:
        seam(p,[v(red(u,t+.05*(u-.5)))+v((0,.002,0)) for u in np.linspace(.20,.70,5)],.0011,'linen')
    return p
def shield():
    p=Part('Garde_Schlackenschild','LeftHand')
    pts=[(-.13,-.295),(.105,-.305),(.144,-.245),(.157,.204),(.118,.26),(-.124,.273),(-.164,.233),(-.163,-.238)]
    def cover(x,z):return .092+.027*(1-(x/.165)**2)+.009*(z/.31)**2
    poly=[(x,cover(x,z),z) for x,z in pts];p.panel(poly,'gravel',.021)
    p.loop(poly,.0055,'bright',7,True)
    # Each raised kiln brick receives its own beveled rim. The joints stay dark and deep.
    for j in range(6):
        for i in range(2 if j%2==0 else 3):
            bounds=([-.142,-.003,.136] if j%2==0 else [-.142,-.095,.057,.136])
            x0,x1=bounds[i]+.002,bounds[i+1]-.002
            z0=-.239+j*.077+.002;z1=z0+.071
            coords=[(x0+.007,z0),(x1-.007,z0+.001),(x1,z0+.007),(x1-.002,z1-.003),(x1-.010,z1),(x0+.004,z1-.002),(x0,z1-.011),(x0,z0+.008)]
            face=[(x,cover(x,z)+.005+.0018*sin(j+i*7+x*41),z) for x,z in coords]
            p.panel(face,'brick',.008)
    for z in [-.192,.105,.211]:
        band=[(-.157,cover(-.157,z)+.013,z-.010),(.141,cover(.141,z)+.014,z-.011),(.145,cover(.145,z)+.014,z+.013),(-.155,cover(-.155,z)+.013,z+.012)]
        def strap(u,t):x=mix(-.157,.144,u);return (x,cover(x,z)+.015,z+mix(-.009,.012,t)+.003*u)
        p.grid(11,1,strap,'iron',.004,smooth=False)
        for x in [-.134,-.107,.103,.128]:
            p.rivet((x,cover(x,z)+.017,z),(0,1,0),.0042,'bright')
    p.loop(path_poly([(-.065,.078,-.059),(-.052,.021,-.052),(-.039,.008,.006),(-.046,.024,.061),(-.066,.076,.069)],4),.008,'leather',9)
    for x,z in [(-.13,-.24),(.121,-.232),(.125,.23),(-.128,.235)]:
        p.rivet((x,cover(x,z)+.009,z),(0,1,0),.0052)
    return p
def hook():
    p=Part('Garde_Schuerhaken','RightHand');pts=path_poly([(0,0,.112),(0,0,.016),(.006,.001,-.132),(.015,.012,-.202),(.043,.020,-.232),(.072,.024,-.220),(.079,.029,-.183)],4)
    rad=[.007 if i<len(pts)-5 else mix(.007,.002,(i-len(pts)+5)/4) for i in range(len(pts))]
    p.loop(pts,rad,'iron',9);p.loop([(0,0,-.068),(0,0,.070)],.011,'leather',10)
    for i in range(13):
        z=-.062+i*.010;p.ring((0,0,z),.0112,.0112,.0017,'suede',11)
    for z in [-.077,.079]:p.ring((0,0,z),.011,.011,.0026,'bronze',15)
    return p
def brow():
    p=Part('Glocke_Stirnreif','Head');lev=[(.096,.110,.046),(.097,.112,.049),(.097,.112,.071),(.096,.109,.073)]
    def hoop(a,j,rx,ry,z):return (rx*sin(a),ry*cos(a)+.006,z-.013*cos(a)+.002*cos(3*a))
    p.shell(lev,'bronzepale',48,hoop)
    for j in [0,3]:p.loop([hoop(a,j,*lev[j]) for a in np.arange(48)*tau/48],.00175,'bronzeedge',6,True)
    for j,a in enumerate(np.linspace(-1.65,1.65,9)):
        x,y,z=hoop(a,2,*lev[2]);d=v((sin(a),cos(a),0)).normalized();p.rivet((x+.0004*d.x,y+.0004*d.y,z-.012),d,.003 if j%2==0 else .0019,'bronze')
    qu=[(-.099,.042,.038),(-.083,.058,.031),(-.085,.055,.011),(-.094,.045,.005),(-.101,.037,.019)]
    p.panel(qu,'teal',.0018);return p
def yoke():
    p=Part('Glocke_Glockenjoch','Chest')
    def fabric(a,i,x,y,z):
        amp=.0023+.0032*(i<2);w=amp*sin(20*a+2.3*z)+.0015*cos(11*a)
        return ((x+w)*sin(a),(y+w)*cos(a),z+.009*sin(2*a+.14))
    p.shell([(.153,.106,-.176),(.158,.116,-.118),(.168,.119,-.048),(.180,.132,.035),(.182,.127,.111),(.136,.095,.143),(.097,.082,.157)],'linen',70,fabric)
    # Two gathered plastrons frame an open V. Their small stitches can catch grazing light.
    for side in [-1,1]:
        def bodice(u,t):
            x=side*(.015+.122*u);rt=fit((t-.15)/.85);z=-.147+.234*t+.043*t*u
            y=.130-.056*u**1.72+.011*sin(pi*t)+.0055*sin(5*pi*u+.95*t)+.003*sin(4*pi*t+3*u)*(1-u)
            return (x,y,z)
        p.grid(17,12,bodice,'linen' if side>0 else 'ashcloth',.002)
        p.loop([v(bodice(.01,t))+v((0,.002,0)) for t in np.linspace(.1,1,13)],.0021,'linen',6)
        for i in range(6):
            a=v(bodice(.028,.16+.11*i));p.loop([a+v((0,.003,0)),a+v((0,.003,.005))],.0008,'oildark',4)
    for side in [-1,1]:
        pts=path_poly([(side*.14,.075,.138),(side*.112,.109,.076),(side*.058,.137,.042),(side*.006,.130,.080)],4)
        p.loop(pts,.005,'leather',7)
    # Squared hoop section follows a rising bell-yoke laid over the upper chest, with an offset fracture.
    segments=[(-1.40,.29),(.405,1.44)]
    def frame(a,t,zstep=0,back=0):
        return (.204*sin(a),(.143+.009*t)*cos(a)-.012+.003*sin(4*a)+back,
                .115+.068*cos(a)-.018*abs(sin(a))+.032*t+.022*sin(a+.35)+zstep)
    for st,en in segments:
        def top(u,t):
            a=mix(st,en,u);return frame(a,t)
        p.grid(16,3,top,'bronze',-.009)
        for t in [0,1]:p.loop([top(u,t) for u in np.linspace(0,1,24)],.0021,'bronzeedge',6)
        # Dark casting ribs on the elbows, a deep old green line at each cut face.
        for a in np.linspace(st+.06,en-.06,4):
            for tt in [0,.95]:
                pt=v(frame(a,tt));p.rivet(pt+v((0,.002,0)),(sin(a),cos(a),.25),.0026,'bronzepale')
            if abs(a)>1:
                p.loop([frame(a-.03,0,.001),frame(a,.4,.002),frame(a+.03,1,.001)],.0031,'bronzepale',6)
        for a in [st,en]:
            p.loop([frame(a,0),frame(a+.002,.37,-.002),frame(a-.009,1)],.0026,'oildark',6)
        # The inside lip was filed from the same casting as the little handbell.
        p.loop([v(top(u,.22))+v((0,.0025,0)) for u in np.linspace(0,1,23)],.0016,'bronzepale',6)
    for side in [-1,1]:
        for k in range(2):
            x=side*(.172+.009*k)
            p.loop(path_poly([(x,.067,.140),(x,.083,.113),(x+side*.017,.067,.092),(x+side*.010,.048,.085)],3),.0054 if not k else .0037,'bronze' if not k else 'bronzeedge',7)
    # Shoulder keeper tabs and two concentric back bracing bands.
    for s in [-1,1]:
        def tab(u,t):
            x=s*(.145+.05*u);y=.098-.158*t-.034*u
            z=.147+.013*(1-u)-.017*sin(t*pi)+.005*sin(2*t)
            return x,y,z
        p.grid(6,8,tab,'bronze',-.006)
        for t in [.10,.83]:
            q=tab(.55,t);p.rivet((q[0],q[1],q[2]+.007),(0,0,1),.004,'bronzeedge')
    seam(p,[(-.145,-.077,.148),(-.09,-.102,.119),(0,-.116,.104),(.09,-.101,.119),(.15,-.073,.148)],.006,'oildark')
    # Split linen placket and rope button loops below the collar.
    pts=[(-.022,.139,.109),(-.008,.141,.074),(.010,.140,.064),(.001,.132,-.013)]
    seam(p,pts,.0028,'ashcloth')
    for k in range(6):
        z=.035-k*.024
        for side in [-1,1]:
            x=side*.013;yy=.148-.013*(k/6);p.rivet((x,yy,z),(0,1,0),.0027,'bronze')
            seam(p,[(x,yy+.002,z),(-x,yy+.002,z-.014)],.0013,'leather')
    for i in range(4):
        z=-.023-i*.032;p.rivet((.009,.128-i*.002,z),(0,1,0),.0032,'bronzepale')
    return p
def sail():
    p=Part('Glocke_Schultertuch','LeftUpperArm')
    def fold(u,t):
        a=mix(-2.55,-.34,u);radius=.085+.049*t+.009*sin(4*pi*u+.9*t)*(0.5+.5*t)
        z=.037-.245*t-.024*t**3*sin(4.4*u+.25)-.006*t**4*cos(30*u)
        return (radius*sin(a)-.008*t,radius*cos(a)-.01,z)
    p.grid(19,14,fold,'linen',.0019)
    for u in [.018,.961]:p.loop([v(fold(u,t))+v((-.001,0,0)) for t in np.linspace(.06,.988,19)],.0021,'ashcloth',6)
    for t in [.86,.888]:p.loop([v(fold(u,t))+v((-.001,0,0)) for u in np.linspace(.018,.965,19)],.0019,'ashcloth',6)
    for u in [.055,.11,.84,.91]:seam(p,[v(fold(u,t))+v((-.001,0,0)) for t in np.linspace(.82,.965,7)],.0014,'ashcloth')
    for i in range(17):
        u=(i+.24)/18;st=v(fold(u,1));off=v((-.002,.001,-.009-.008*cos(6*i)**2))
        p.loop([st,st+.4*off+v((0,.002,0)),st+off],.0011,'linen',4)
    # Visible cross-darn. All stitches share arm motion with the cloth.
    for k in range(5):
        u=.50+k*.024
        p.loop([v(fold(u,t))+v((-.004,0,0)) for t in [.65,.72,.78]],.0011,'oildark',4)
    for j in range(3):
        t=.67+.037*j;p.loop([v(fold(u,t))+v((-.0043,0,0)) for u in [.49,.55,.62]],.0011,'oildark',4)
    return p
def bell_solid(p,center,r,zscale,tile='bronze',n=26):
    # Open bell with rolled lip, hollow dark interior, handle and a visible clapper.
    cx,cy,cz=center
    prof=[(.12,.94),(.25,.91),(.35,.78),(.39,.63),(.50,.46),(.67,.23),(.86,.07),(.99,.035),(1,0),(.99,-.045),(.88,-.054),(.78,.00),(.60,.26),(.37,.60),(.24,.79),(.11,.84)]
    p.shell([(r*ra,r*ra,zscale*zz) for ra,zz in prof],tile,n,
        lambda a,i,x,y,z:(cx+x*sin(a),cy+y*cos(a),cz+z+.0015*sin(3*a)*(1-abs(z/zscale))))
    for rr,zz,th in [(.93,.0,.028),(.83,.095,.020),(.435,.578,.025),(.30,.86,.027)]:
        p.ring((cx,cy,cz+zscale*zz),r*rr,r*rr,r*th,'bronzeedge',n)
    p.loop([(cx,cy,cz+.74*zscale),(cx,cy,cz+.08*zscale)],r*.049,'iron',7)
    p.rivet((cx,cy,cz+.09*zscale),(0,0,-1),r*.14,'iron',8)
    for a in [-.7,0,.7]:p.rivet((cx+r*.63*sin(a),cy+r*.63*cos(a),cz+.285*zscale),(sin(a),cos(a),.1),r*.038,'bronzepale',7)
def skirt():
    p=Part('Glocke_Schurzgurt','Hips')
    p.shell([(.149,.105,.079),(.153,.108,.072),(.157,.113,.017),(.154,.109,.014)],'leather',55,
        lambda a,i,x,y,z:(x*sin(a),y*cos(a),z+.009*sin(a)))
    bb(p,.035,.115,.046,.030,.018,'bronze');bb(p,.035,.118,.046,.019,.010,'oildark')
    # Three overlapping panels keep the knee corridor open.
    for ii,(st,en) in enumerate([(.34,2.30),(-2.55,-.45),(2.12,4.05)]):
        def cloth(u,t):
            a=mix(st,en,u)+.032*sin(pi*t)*cos(tau*u)
            wr=.010*(.32+.68*t)*sin(tau*(3.8*u+.14*t))+.003*sin(11*u-2*t)
            x=(.154+.057*t+wr)*sin(a);y=(.114+.070*t+wr)*cos(a)
            z=.032-t*(.371+.045*sin(pi*u)+.021*ii+.010*sin(7*u+ii))
            return x,y,z
        p.grid(22,13,cloth,'ashcloth' if ii<2 else 'nightcloth',.0021)
        if ii==0:
            for u in [.046,.086,.11]:seam(p,[cloth(u,t) for t in np.linspace(.20,.945,11)],.0018,'linen')
    # Staggered fired-bronze bells hang visibly on the right hip.
    for j,(x,y,z,r) in enumerate([(.151,.100,-.043,.032),(.170,.099,-.120,.036),(.185,.088,-.209,.040)]):
        p.loop(path_poly([(.121,.084,.039),(x-.018,y+.007,z+.116),(x,y,z+.085)],3),.0045,'leather',7)
        bell_solid(p,(x,y,z),r,.074+j*.006,n=20)
    # Two small dyed cords distinguish woven cloth from metal in the side light.
    for j,x in enumerate([-.095,-.110]):
        ps=path_poly([(x,.112,.041),(x-.008,.132,-.06),(x+.001,.145,-.13),(x+.010,.154,-.186-j*.021)],4)
        p.loop(ps,.0024,'teal',6)
    return p
def handbell():
    p=Part('Glocke_Handglocke','RightHand')
    bell_solid(p,(0,0,-.194),.042,.087,n=32)
    p.loop([(0,0,-.114),(0,0,-.098),(0,0,-.048),(0,0,.027)], [.013,.011,.008,.010],'leather',12)
    for j in [-.086,-.029,.025]:p.ring((0,0,j),.0105,.0105,.002,'bronzeedge',16)
    p.loop([(.010*sin(a),0,.050+.024*cos(a)) for a in np.arange(22)*tau/22],.0032,'bronze',8,True)
    return p
def cuff(name,bone,side,leg=False,garda=False):
    p=Part(name,bone);rs=.052 if leg else .047;length=.195 if leg else .109;top=-.057 if leg else -.144
    # The silhouette narrows along the limb. Its upper pivot remains the real knee or elbow.
    def center(t):return (side*(.010*t if leg else .014*t),-.011*t if leg else .003*t,top-length*t)
    levels=[(rs*(1-.2*t),rs*.88*(1-.19*t),top-length*t) for t in np.linspace(0,1,8)]
    def fx(a,j,x,y,z):
        t=j/7;cx,cy,_=center(t);ww=.0018*sin(12*a+5*t);return (cx+(x+ww)*sin(a),cy+(y+ww)*cos(a),z+.002*cos(3*a))
    p.shell(levels,'suede' if garda else 'ashcloth',28,fx)
    if garda:
        for k in range(4 if leg else 3):
            h=1/(5 if leg else 4);st=.03+k*h
            def band(u,t):
                a=2*pi*u+.43*k;t=st+h*.85*t+.066*sin(a+.65*k)
                cx,cy,cz=center(t);rr=rs*(1-.20*t)+.003
                return cx+rr*sin(a),cy+.90*rr*cos(a),cz
            p.grid(28,2,band,'leather' if not leg else ('suede' if k%2 else 'ashcloth'),.0015)
            if not leg:
                q=band(.14, .53);p.rivet(q,(side*.55,.75,.07),.003,'bronzeedge')
    else:
        for j in range(4 if leg else 3):
            st=.07+j*(.22 if leg else .29)
            def lame(u,t):
                a=mix(-1.18,1.14,u);h=st+.26*t+.064*sin(u*pi)
                cx,cy,cz=center(h);rr=rs*(1-.2*h)+.0035+.002*t
                return (cx+rr*sin(a),cy+rr*.93*cos(a),cz)
            p.grid(9,3,lame,'bronzepale' if not leg else 'iron',.0017)
            seam(p,[lame(u,.96) for u in np.linspace(0,1,12)],.0014,'bronzeedge')
            for u in [.10,.89]:
                a=mix(-1.18,1.14,u);p.rivet(lame(u,.12),(sin(a),cos(a),.08),.0028,'bronzeedge')
    return p
def brand():
    p=Part('Brandreif','Head')
    lev=[(.089,.104,-.053),(.101,.119,-.018),(.107,.121,.035),(.108,.123,.088),(.095,.109,.130),(.071,.084,.158),(.032,.043,.172),(.002,.002,.174)]
    def dome(a,j,x,y,z):return ((x+hammer(a,j,.0009))*sin(a),(y+hammer(a,j,.0016))*cos(a),z+.0027*sin(4*a+j*.2))
    # Two separate cheek shells face forward leaving a broad angular open brow.
    for lo,hi in [(.49,pi),(-pi,-.49)]:
        def face(u,t):
            a=mix(lo,hi,u);z=mix(-.051,.133,t);rad=.099+.0085*sin(pi*t)
            return (rad*sin(a),(.110+.015*sin(pi*t))*cos(a),z-.016*cos(a))
        p.grid(20,9,face,'iron',-.003)
    p.shell(lev[3:],'iron',42,lambda a,j,x,y,z:dome(a,j+3,x,y,z))
    for j,(lo,hi) in enumerate([(-2.7,.49),(.78,2.95)]):
        def crown(u,t):
            a=mix(lo,hi,u);radius=.111+.0035*t;cut=.045*(1-u if j==0 else u)
            return (radius*sin(a),(.123+.005*t)*cos(a),.057+.022*t+cut+.014*sin(a-.42))
        p.grid(28,3,crown,'bronze',-.008)
        seam(p,[crown(u,.93) for u in np.linspace(0,1,34)],.0026,'bronzepale')
        for k in range(3):
            uu=(k+.33)/3;qa=crown(uu,.48);a=mix(lo,hi,uu);p.rivet(qa,(sin(a),cos(a),0),.0033,'iron')
    # Recessed fire-glass repair mark under the break in the ring.
    pts=[(.045,.111,.070),(.052,.117,.080),(.049,.118,.091),(.056,.116,.100)]
    p.loop(pts,.0028,'ember',7)
    for a in [-2.18,-.91,.13,1.48]:
        seam(p,[dome(a+.035*j,j,*lev[j]) for j in range(3,7)],.0017,'bright')
    return p
def work_sleeve(part, side, upper, guard):
    """Short overlapping pieces follow the original upper and lower arm independently."""
    base_radius=.064 if upper else .049
    length=(.224 if guard else .190) if upper else (.149 if guard else .092)
    z0=(-.018 if guard else -.014) if upper else .012
    levels=[i/8 for i in range(9)]
    def surface(a,t):
        r=base_radius*(1+(.24 if upper else .14)*sin(pi*t)-(.16 if upper else .22)*t)
        if not guard and not upper:r+=.008*max(0,1-t*1.6)
        # Grain-scale warp is painted; gather ridges and rolled elbows remain visible at distance.
        wrinkles=(.0028*sin(4*a+3*t)+.0021*sin(7*a-3.1*t))*(.5+.5*sin(pi*t))
        wrinkles+=.0027*cos(4.5*pi*t+1.5*sin(a))*(1 if not guard else .62)
        r+=wrinkles;z=z0-length*t+.0034*sin(4*a+.47)*t*t
        return (side*(.003+.003*t)+(r+.0006)*sin(a),-.006*upper*(1-t)+(.85*r+.001)*cos(a),z)
    part.shell([(base_radius,base_radius,t) for t in levels],('nightcloth' if upper else 'oildark') if guard else ('ashcloth' if upper else 'linen'),24,
               lambda a,i,x,y,t:surface(a,t))
    # Dark doubled sleeve ends cover the hinge without crossing onto the opposite moving bone.
    for t,r in [(.035,.0024),(.93,.0028),(.984,.0029)]:
        part.loop([surface(a,t) for a in np.arange(24)*tau/24],r,'oildark' if guard else 'ashcloth',4,True)
    if guard:
        if upper:
            for band in range(3):
                def flap(u,t):
                    a=side*mix(.21,2.99,u);h=.22+.20*band+.27*t-.12*sin(pi*u)
                    x,y,z=surface(a,h)
                    return x+.0055*sin(a),y+.0055*cos(a),z-.006*sin(pi*u)*t
                part.grid(7,2,flap,'leather' if band%2 else 'iron',.002,smooth=False)
                part.loop([flap(u,.977) for u in np.linspace(0,1,8)],.0018,'oildark' if band%2 else 'bright',4)
                for u in [.12,.87]:
                    a=side*mix(.21,2.99,u);pt=v(flap(u,.16));dd=v((sin(a),cos(a),0))
                    part.rivet(pt+dd*.001,dd,.0032,'bronzeedge',7)
        else:
            def wrap(u,t):
                a=tau*u;w=.52+.22*t+.14*sin(a+side*.45);x,y,z=surface(a,w)
                return (x+.002*sin(a),y+.002*cos(a),z)
            part.grid(20,1,wrap,'leather',.0013)
    else:
        for t in [.63,.725,.83] if upper else [.215,.37,.52,.705]:
            part.loop([surface(a,t+.042*sin(a*2+.6)) for a in np.arange(22)*tau/22],.0024,
                      'ashcloth' if t>.70 else 'linen',4,True)
    return part

g=[hood(),chest_guard(),sash(),shield(),hook()]
b=[brow(),yoke(),sail(),skirt(),handbell()]
for s,side,bonepre in [('L',-1,'Left'),('R',1,'Right')]:
    g.append(work_sleeve(Part('Garde_Armaermel_'+s,bonepre+'UpperArm'),side,True,True))
    b.append(work_sleeve(Part('Glocke_Armaermel_'+s,bonepre+'UpperArm'),side,True,False))
    g.extend([work_sleeve(cuff('Garde_Manschette_'+s,bonepre+'LowerArm',side,garda=True),side,False,True),cuff('Garde_Wickel_'+s,bonepre+'LowerLeg',side,True,True)])
    b.extend([work_sleeve(cuff('Glocke_Manschette_'+s,bonepre+'LowerArm',side),side,False,False),cuff('Glocke_Gamasche_'+s,bonepre+'LowerLeg',side,True)])
groups={'Schlackengarde':[k.finish() for k in g],'Glockentraegerin':[k.finish() for k in b],'Brandreif':[brand().finish()]}
def select(obs):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in obs:ob.select_set(True)
    bpy.context.view_layer.objects.active=obs[0]
evidence={'units':'metres','nativeCharacterAxes':{'x':'right','y':'up','z':'front'},'parts':{}}
for grp,obs in groups.items():
    select(obs)
    bpy.ops.export_scene.fbx(filepath=str(OUT/(grp+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',
          apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',use_mesh_modifiers=True,mesh_smooth_type='OFF',
          use_tspace=False,add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE',embed_textures=False,bake_space_transform=True)
    for ob in obs:
        m=ob.data;m.calc_loop_triangles()
        pts=np.array([tuple(v.co) for v in m.vertices]);pts=pts[:,[0,2,1]]
        evidence['parts'][ob.name]={'set':grp,'socket':ob['HumanoidSocket'],'triangles':len(m.loop_triangles),'min':pts.min(axis=0).round(5).tolist(),'max':pts.max(axis=0).round(5).tolist()}
print('EXPORTED',[(k,sum(q['triangles'] for q in evidence['parts'].values() if q['set']==k)) for k in groups],flush=True)
(ROOT/'schriftplan.json').write_text(json.dumps(evidence,indent=2,ensure_ascii=False))
# Original objects stay at their local mounting origins in the interchange models. For a single studio
# view the same native objects are mounted on independent non-exported tailoring stands.
positions={'Head':(0,0,1.47),'Chest':(0,0,1.132),'Hips':(0,-.005,.90),
           'LeftUpperArm':(-.19,0,1.24),'RightUpperArm':(.19,0,1.24),'LeftLowerArm':(-.250,.022,1.025),'RightLowerArm':(.249,.022,1.025),
           'LeftLowerLeg':(-.103,-.004,.48),'RightLowerLeg':(.105,0,.476),
           'LeftHand':(-.296,.065,.80),'RightHand':(.288,.067,.82)}
posX={'Schlackengarde':.47,'Glockentraegerin':-.43,'Brandreif':1.35}
for key,obs in groups.items():
    for ob in obs:
        ob.location=v(positions[ob['HumanoidSocket']])+v((posX[key],0,0));ob.rotation_euler.z=-.075 if key=='Schlackengarde' else .025
        if 'Armaermel' in ob.name:ob.rotation_euler.y=(-.235 if ob['HumanoidSocket']=='RightUpperArm' else .235)
# Stands and their linens are display furniture only and are excluded from every FBX.
def plainmat(n,col):
    m=bpy.data.materials.new(n);m.diffuse_color=(*col,1);m.use_nodes=True
    sp=m.node_tree.nodes.get('Principled BSDF');sp.inputs['Base Color'].default_value=(*col,1);sp.inputs['Roughness'].default_value=.83;return m
wood=plainmat('Kleiderstock / dunkle Eiche',(.071,.049,.028));wax=plainmat('Büsten / Wachsleinen',(.157,.129,.095))
felt=plainmat('Futter des Schneiderstocks',(.102,.097,.081));stand=plainmat('Sockel',(.067,.065,.054))
fixtures={}
def fixture(key,name,pos,scale,m):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24,ring_count=16,radius=1,location=tuple(pos))
    ob=bpy.context.object;ob.name=name;ob.scale=scale;ob.data.materials.append(m)
    for f in ob.data.polygons:f.use_smooth=True
    fixtures.setdefault(key,[]).append(ob);return ob
def rod(key,a,b,r,m):
    a=v(a);b=v(b);d=b-a
    bpy.ops.mesh.primitive_cylinder_add(vertices=20,radius=r,depth=d.length,location=(a+b)*.5)
    ob=bpy.context.object;ob.rotation_euler=d.to_track_quat('Z','Y').to_euler();ob.data.materials.append(m);fixtures.setdefault(key,[]).append(ob)
for key in ['Schlackengarde','Glockentraegerin','Brandreif']:
    h=posX[key];center=v((h,0,0))
    fixture(key,'Kopfform • sichtbare Schneiderbüste',center+v((0,.000,1.485)),(.084,.103,.126),wax)
    rod(key,center+v((0,0,1.25)),center+v((0,0,1.417)),.044,wax)
    if key!='Brandreif':
        fixture(key,'Leinenbüste',center+v((0,-.006,1.128)),(.154,.105,.187),felt)
        rod(key,center+v((0,0,.04)),center+v((0,0,.95)),.026,wood)
        for sign in [-1,1]:
            sh=center+v((sign*.155,0,1.268));el=center+v((sign*.248,.02,1.025));hand=center+v((sign*.288,.057,.801))
            rod(key,sh,el,.034,felt);rod(key,el,hand,.027,felt)
            fixture(key,'Handballen des Kleiderstocks',hand,(.030,.037,.052),wood)
            hip=center+v((sign*.085,-.008,.93));knee=center+v((sign*.105,0,.48));foot=center+v((sign*.107,.013,.103))
            rod(key,hip,knee,.039,felt);rod(key,knee,foot,.031,wood)
        bpy.ops.mesh.primitive_cylinder_add(vertices=64,radius=.255,depth=.025,location=(h,-.01,.04))
        ob=bpy.context.object;ob.data.materials.append(stand);fixtures.setdefault(key,[]).append(ob)
camdata=bpy.data.cameras.new('Schnittansicht');camdata.type='ORTHO'
cam=bpy.data.objects.new('Schnittansicht',camdata);scene.collection.objects.link(cam);scene.camera=cam
def aim(ob,to):ob.rotation_euler=(v(to)-ob.location).to_track_quat('-Z','Y').to_euler()
def area(name,loc,power,size,color):
    l=bpy.data.lights.new(name,'AREA');l.energy=power;l.shape='DISK';l.size=size;l.color=color
    ob=bpy.data.objects.new(name,l);scene.collection.objects.link(ob);ob.location=loc;aim(ob,(0,0,.98))
area('Helles Atelierfenster',(2.34,3.45,3.8),290,2.9,(1,.91,.79))
area('Kalte hohe Gegenkante',(-1.8,-1.0,2.8),340,2.0,(.72,.82,1.0))
area('Leinenreflex',(-.8,2.2,1.15),82,1.7,(.96,.98,1.0))
scene.world=bpy.data.worlds.new('Tageslicht');scene.world.use_nodes=True
back=scene.world.node_tree.nodes.get('Background');back.inputs[0].default_value=(.26,.28,.30,1);back.inputs[1].default_value=.50
scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.use_denoising=False;scene.cycles.samples=20
scene.render.resolution_percentage=100;scene.render.film_transparent=True
scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA'
scene.view_settings.view_transform='AgX'
def show(grps,parts=None,showstand=True):
    for gname,obs in groups.items():
        for part in obs:part.hide_render=gname not in grps or (parts is not None and part.name not in parts)
        for fix in fixtures.get(gname,[]):fix.hide_render=(gname not in grps or not showstand)
def lens(at,delta,scale,rx,ry):
    cam.location=v(at)+v(delta);aim(cam,at);camdata.ortho_scale=scale;scene.render.resolution_x=rx;scene.render.resolution_y=ry
def shot(path):scene.render.filepath=str(path);bpy.ops.render.render(write_still=True)
show(['Schlackengarde','Glockentraegerin']);lens((.02,.005,.858),(-.67,4.25,.37),1.98,1120,920)
select(groups['Schlackengarde'])
for im in bpy.data.images:
    if im.source=='FILE' and im.filepath:im.pack()
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Garnituren.blend'),compress=True)
shot(VIEWS/'Beide.png')
for grp in groups:
    x=posX[grp];show([grp])
    if grp=='Brandreif':
        lens((x,.010,1.53),(-.57,2.0,.25),.36,600,600)
    else:
        lens((x,.035,1.35 if grp=='Schlackengarde' else 1.285),(-.62 if grp=='Schlackengarde' else -.7,3,.35),.86 if grp=='Schlackengarde' else .78,695,765)
    shot(VIEWS/(grp+'-Detail.png'))
# Icons use the actual helmet and the broken shoulder yoke, with transparent, empty backgrounds.
for grp,part,z,span in [('Schlackengarde','Garde_Ofenhaube',1.555,.47),('Glockentraegerin','Glocke_Glockenjoch',1.145,.47),('Brandreif','Brandreif',1.53,.32)]:
    x=posX[grp];show([grp],[part],False);lens((x,.008,z),(-.76,2.4,.44),span,256,256);shot(OUT/(grp+'_Icon.png'))
