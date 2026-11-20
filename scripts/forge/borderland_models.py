"""Create Ashen Crypt's original regional kit. Run with Blender -b -P ... -- OUT.

Blender Z is up. All meshes share one hand-authored trim atlas and start at the
ground plane. The individual packed scenes are inputs for worldforge-blender submit.
No source from PrivateArt, the Asset Store, or a running game is read.
"""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

TAU = 2 * math.pi
OUT = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
OUT.mkdir(parents=True, exist_ok=True)
# Stone, slate, mortar, ochre wood, bark, cut wood, iron, dull bronze, foliage,
# moss, ivory, bloodcap, gills, canvas, ember and warm gold.
PALETTE = ("77736b", "474c50", "635f57", "876a49", "52412e", "ad9062",
           "41474a", "ab8050", "3b5140", "687653", "b8b8b2", "a32113",
           "b76b53", "a28a67", "e28945", "c99f46")
KEYS = ("BloodcapBank", "AshPine", "StoneOutcrop", "TimberHouse", "MillWheel",
        "MillHouse", "GrainBay", "CryptArch", "CaveMouth", "StoneBridge",
        "WatchPost", "RibPillar", "Brazier", "SupplySack", "CoinStack",
        "BrokenCart", "Palisade", "Waystone",
        "RangedQuiver", "ShieldPlate", "SwarmSpines", "EliteCrest", "ShaleLedge", "DenLitter",
        "EmberSnag", "EmberSedge")

def paint_atlas():
    size, tile = 256, 64
    image = bpy.data.images.new("BorderlandAtlas", width=size, height=size, alpha=False)
    pixels = []
    for y in range(size):
        for x in range(size):
            band = (y // tile) * 4 + x // tile
            c = tuple(int(PALETTE[band][j:j+2], 16) / 255 for j in (0, 2, 4))
            u, v = x % tile, y % tile
            grain = ((x * 137 + y * 389 + x*y*17) % 31) / 31 - .5
            form = math.sin(u*.23 + 1.3*math.sin(v*.12)) + .6*math.sin(u*.59 + v*.11)
            if band in (3, 4, 5): detail = .064*form + .034*grain
            elif band in (0, 1, 2, 9): detail = .04*math.sin(u*.34+v*.26) + .10*grain
            elif band == 13: detail = .022*((u%4)-1.5) + .019*((v%4)-1.5)
            elif band == 12: detail = .09*math.sin(u*.65 + .2*math.sin(v*.21))
            else: detail = .044*grain + .035*math.cos(v*.105)
            pixels.extend([max(.01, min(1, a*(1+detail))) for a in c]+[1.0])
    image.pixels.foreach_set(pixels)
    image.filepath_raw = str(OUT/"BorderlandAtlas.png")
    image.file_format = 'PNG'
    image.save()
    image.pack()
    return image

class Mesh:
    def __init__(self, lod):
        self.lod = lod
        self.verts, self.faces, self.bands = [], [], []

    def rings(self, rows, side, bottom=None, top=None):
        """Rows go from base to tip, each counterclockwise around the axis."""
        n = len(rows[0])
        start = len(self.verts)
        for row in rows: self.verts.extend(row)
        def face(indices, band):
            self.faces.append(tuple(start+i for i in indices))
            self.bands.append(band)
        face(reversed(range(n)), side if bottom is None else bottom)
        for j in range(len(rows)-1):
            for i in range(n):
                k=(i+1)%n
                face([j*n+i,j*n+k,(j+1)*n+k,(j+1)*n+i],side)
        end=(len(rows)-1)*n
        face(range(end,end+n),side if top is None else top)

    def post(self, a, b, ra, rb, band, n=None):
        a,b=Vector(a),Vector(b)
        axis=(b-a).normalized()
        u=axis.cross(Vector((0,1,0)) if abs(axis.y)<.9 else Vector((1,0,0))).normalized()
        v=axis.cross(u)
        n=n or (9,6,4)[self.lod]
        ring=[math.cos(TAU*i/n)*u + math.sin(TAU*i/n)*v for i in range(n)]
        self.rings([[a+d*ra for d in ring],[b+d*rb for d in ring]],band)

    def box(self, x,y,z, w,d,h,band, yaw=0):
        """A cropped-corner dressed block or plank, dimensions in metres."""
        co,si=math.cos(yaw),math.sin(yaw)
        a,b=w/2,d/2
        t=min(a,b)*.16
        outline=[(-a,-b),(a,-b),(a,b),(-a,b)]
        if self.lod==0:
            outline=[(-a+t,-b),(a-t,-b),(a,-b+t),(a,b-t),(a-t,b),(-a+t,b),(-a,b-t),(-a,-b+t)]
        self.rings([[(x+u*co-v*si,y+u*si+v*co,z+k) for u,v in outline] for k in (0,h)],band)

    def bulb(self,x,y,z,rx,ry,h,band,profile, n=None, phase=0,rough=0):
        n=n or (12,8,5)[self.lod]
        rows=[]
        for j,(height,radius) in enumerate(profile):
            row=[]
            for i in range(n):
                a=TAU*i/n+phase
                factor=radius*(1+rough*math.sin(a*3+.53*j)+rough*.4*math.sin(a*5))
                row.append((x+rx*factor*math.cos(a),y+ry*factor*math.sin(a),z+h*height))
            rows.append(row)
        self.rings(rows,band)

    def wheel(self,x,y,z,r,width,steps,band, paddles=True):
        """Open wheel in the YZ plane, with two timber rims and a real axle."""
        for s in (-1,1):
            side=x+s*width*.42
            for i in range(steps):
                a,b=TAU*i/steps,TAU*(i+1)/steps
                p=(side,y+r*math.cos(a),z+r*math.sin(a))
                q=(side,y+r*math.cos(b),z+r*math.sin(b))
                self.post(p,q,r*.075,r*.075,band,4)
                if s==1 and i%2==0:
                    self.post((x,y,z),p,r*.068,r*.05,3,4)
                if s==1 and paddles:
                    self.post((x-width*.62,p[1],p[2]),(x+width*.62,p[1],p[2]),r*.105,r*.105,5,4)
        self.post((x-width*.78,y,z),(x+width*.78,y,z),r*.15,r*.15,6)

    def house(self,w,d,h, mill=False):
        self.box(0,0,0,w,d,.55,0)
        self.box(0,0,.54,w*.94,d*.94,h-.54,13)
        for s in (-1,1):
            self.box(s*w*.47,0,.48,.15,d,h-.48,4)
            for j in range((4,3,2)[self.lod]):
                y=-d*.44+j*d*.88/((4,3,2)[self.lod]-1)
                self.box(s*w*.48,y,.42,.17,.18,h-.42,4)
            for y in (-d*.472,d*.472):
                self.post((s*w*.45,y,.6),(0,y,h-.09),.085,.085,4,4)
            x=s*(w*.5+.35)
            self.rings([[(x,-d*.55,h-.15),(x,d*.55,h-.15),(0,d*.55,h+1.65),(0,-d*.55,h+1.65)],
                        [(x,-d*.55,h+.02),(x,d*.55,h+.02),(0,d*.55,h+1.82),(0,-d*.55,h+1.82)]],1)
            if self.lod<2:
                for j in range(1,5 if self.lod==0 else 3):
                    t=j/(5 if self.lod==0 else 3)
                    self.post((x*t,-d*.55,h+1.82*(1-t)+.03),(x*t,d*.55,h+1.82*(1-t)+.03),.04,.04,2,4)
        self.box(0,-d*.482,.50,1.15,.085,2.15,4)
        self.box(0,-d*.49,.51,.86,.10,1.96,3)
        for x in (-w*.31,w*.31):
            self.box(x,-d*.484,1.56,.72,.12,.89,6)
            if self.lod<2:
                self.box(x,-d*.5,1.51,.9,.16,.11,5)
                self.box(x,-d*.5,1.61,.08,.13,.81,5)
        self.box(w*.22,d*.2,h+.45,.58,.66,1.87,0)
        if mill and self.lod<2:
            for s in (-1,1):
                self.box(s*w*.48,d*.2,.48,.34,.8,.94,0)

    def arch(self,rx,rz,thick,depth,blocks,stone=True):
        for i in range(blocks):
            a=math.pi*i/blocks+.008
            b=math.pi*(i+1)/blocks-.008
            k=1.12 if i==blocks//2 else 1
            outline=[(rx*math.cos(a),rz*math.sin(a)),((rx+thick*k)*math.cos(a),(rz+thick*k)*math.sin(a)),
                     ((rx+thick*k)*math.cos(b),(rz+thick*k)*math.sin(b)),(rx*math.cos(b),rz*math.sin(b))]
            self.rings([[(x,y,z+.55) for x,z in outline] for y in (depth/2,-depth/2)],(0,2,1)[i%3] if stone else 3)
        for s in (-1,1): self.box(s*(rx+thick/2),0,0,thick+.14,depth+.2,.57,0)

    def shale_ledge(self):
        # Folded beds share a slope. Each weathered lip casts a short shadow on the seam below.
        for block,(x,y,width,height) in enumerate(((-1.82,.22,1.68,2.30),(.62,.10,1.45,1.94),(2.45,.35,1.10,1.38))):
            steps = (6,4,3)[self.lod]
            sides = (11,8,6)[self.lod]
            for layer in range(steps):
                base = height * layer / steps
                taper = 1.04 - .30 * layer / steps
                rows = []
                for lift,lip in ((0,.93),(.17,1),(.77,1.06),(1,.94)):
                    row = []
                    for i in range(sides):
                        a = TAU * i/sides + block*.24
                        chip = 1 + .11*math.sin(3*a + block) + .055*math.sin(5*a + layer*.48)
                        dx = width * taper * lip * math.cos(a) * chip
                        dy = (.76 + .12*block) * lip * math.sin(a) * chip
                        z = base + lift*height/steps + (dx*.10 + dy*.08)*min(1,layer+lift)
                        row.append((x+dx+.11*layer/steps, y+dy, max(0,z)))
                    rows.append(row)
                self.rings(rows,(1,2,0,1,2,0)[(layer+block)%6],top=2 if layer%2 else 0)
        for i in range((9,5,2)[self.lod]):
            angle = 2.21*i
            self.bulb(-2.85+i*.77,-.91+.22*math.cos(angle),0,.24,.15,.11,
                      0 if i%3 else 2,((0,.8),(.42,1),(1,.42)),n=5,phase=angle,rough=.14)

    def den_litter(self):
        # A bed of earth, flattened reeds and snapped boughs. The young bears can stand on the level centre.
        self.bulb(0,0,0,2.15,1.56,.035,4,((0,.94),(.65,1),(1,.81)),
                  n=(24,16,9)[self.lod],rough=.11)
        pieces = (68,38,15)[self.lod]
        for i in range(pieces):
            a = i*2.39996323
            ring = .34 + .64*math.sqrt((i+.5)/pieces)
            x,y = 2.06*ring*math.cos(a),1.48*ring*math.sin(a)
            length = .24 + .34*((i*19)%27)/26
            direction = a + 1.13 + .41*math.sin(i)
            dx,dy = length*math.cos(direction),length*math.sin(direction)
            z = .035+.043*ring
            band = (3,4,5,9,13)[i%5]
            self.post((x-dx,y-dy,z),(x+dx,y+dy,z+.035*(i%3)),.029 if i%4 else .045,.009,band,3)
            if self.lod==0 and i%6==0:
                self.post((x,y,z+.018),(x+.63*dx-.24*dy,y+.63*dy+.24*dx,z+.13),.019,.006,4,3)

    def ash_pine(self):
        levels = (8,7,6)[self.lod]
        branches = (10,8,6)[self.lod]
        self.post((0,0,0),(.14,-.04,7.18),.29,.031,4)
        # Overlapping whorls of drooping boughs. Gaps between the individual branch tips
        # reveal the trunk, while neighbouring tiers overlap even at the coarsest LOD.
        for tier in range(levels):
            height = tier/(levels-1)
            z = 1.28+5.34*height
            reach = 1.92*(1-height)**.69 + .18
            for k in range(branches):
                a = TAU*(k+.34*math.sin(2.3*tier+k*.74))/branches + tier*2.40
                extent = reach*(.80+.20*math.cos(k*1.43+tier))
                bend = .12*math.sin(tier*2.33+k)
                ca,sa = math.cos(a),math.sin(a)
                points=[]
                for t,width,lift in ((.05,.16,.46),(.47,.50,.32),(.79,.30,.16),(1,.012,.06)):
                    cx=.10*height+extent*t*ca-bend*t*t*sa
                    cy=-.04*height+extent*t*sa+bend*t*t*ca
                    cz=z+lift-.40*t*(1-height)+.08*math.sin(a*2+tier)
                    w=extent*width
                    points.append([(cx-w*sa,cy+w*ca,cz-.10),(cx,cy,cz-.22),
                                   (cx+w*sa,cy-w*ca,cz-.11),(cx,cy,cz+.19*(1-t))])
                band = 8 if (tier+k)%5 else 9
                if self.lod == 2:
                    start=len(self.verts)
                    self.verts.extend((points[0][3],points[1][2],points[3][3],points[1][0],points[2][3]))
                    for face in ((0,1,4),(1,2,4),(2,3,4),(3,0,4),(2,1,0),(3,2,0)):
                        self.faces.append(tuple(start+i for i in face))
                        self.bands.append(band)
                else:
                    self.rings(points,band)
                if self.lod==0 and (k+2*tier)%4==0:
                    tip=points[2][3]; base=points[1][3]
                    self.post(base,tip,.040,.016,10,n=3)
        self.bulb(.12,-.04,6.49,.20,.19,.80,8,((0,.76),(.37,1),(1,.03)),
                  n=(8,6,4)[self.lod],rough=.09)

    def ember_snag(self):
        steps = (13,9,6)[self.lod]
        levels = [(0,.51,0,0),(.28,.42,.02,.01),(1.04,.34,.07,.01),(1.87,.27,.02,-.02),
                  (2.57,.24,-.13,.04),(3.25,.18,-.24,.14),(3.75,.12,-.34,.21),(4.16,.10,-.39,.28)]
        rows = []
        for j,(z,r,cx,cy) in enumerate(levels):
            row = []
            for k in range(steps):
                a = TAU*k/steps
                bark = r*(1+.12*math.cos(5*a+.16*j))
                broken = (.24*math.sin(3*a)+.16*math.sin(5*a)) if j==len(levels)-1 else 0
                row.append((cx+bark*math.cos(a),cy+bark*math.sin(a),z+broken))
            rows.append(row)
        self.rings(rows,4,top=1)
        for j,(height,r,cx,cy) in enumerate(levels[1:6]):
            a = j*2.28+.4
            length = (1.32,.81,1.04,.91,.64)[j]
            x,y = cx+length*math.cos(a), cy+length*math.sin(a)
            fork=(x,y,height+.31+.12*j)
            self.post((cx,cy,height),fork,r*.39,.075,4,n=(7,5,4)[self.lod])
            self.post(fork,(x+.32*math.cos(a+.32),y+.32*math.sin(a+.32),height+.94+.09*j),.080,.026,1)
        # Crevices follow this same crooked trunk. Most of the silhouette stays dark.
        for fissure in range((4,3,2)[self.lod]):
            a = -1.56+fissure*1.57
            centres = []
            for j,(z,r,cx,cy) in enumerate(levels[:6-fissure%2]):
                off = a+.11*math.sin(j*1.83+fissure)
                radius = (r*(1+.12*math.cos(5*off+.16*j)))+.012
                centres.append((cx+radius*math.cos(off),cy+radius*math.sin(off),max(.06,z)))
            for j in range(len(centres)-1):
                if j==3 and fissure%2: continue
                self.post(centres[j],centres[j+1],.028 if j in (1,2) else .016,.012,14,n=3)
        for i in range((8,5,3)[self.lod]):
            a=TAU*i/((8,5,3)[self.lod])+.13
            self.post((.15*math.cos(a),.15*math.sin(a),.32),
                      ((.78+.16*math.sin(i))*math.cos(a),(.78+.16*math.sin(i))*math.sin(a),.04),.16,.028,4,n=5)

    def ember_sedge(self):
        count = (38,22,9)[self.lod]
        for i in range(count):
            a = i*2.39996323
            radius = .46*math.sqrt((i+.5)/count)
            x,y = radius*math.cos(a),radius*.85*math.sin(a)
            h = .22 + .41*((i*17)%29)/28
            lean = .14 + .18*(i%5)/4
            dx,dy = math.cos(a)*lean,math.sin(a)*lean
            nx,ny = -.023*math.sin(a),.023*math.cos(a)
            def section(t,w):
                cx,cy = x+dx*t*t,y+dy*t*t
                return [(cx+nx*w,cy+ny*w,h*t),(cx-.42*nx*w-.15*ny*w,cy-.42*ny*w+.15*nx*w,h*t),
                        (cx-nx*w,cy-ny*w,h*t)]
            self.rings([section(0,.85),section(.46,1),section(.82,.66)],(4,9,3)[i%3])
            self.rings([section(.82,.66),section(.98,.12),section(1,.01)],14 if i%5==0 else 5)

    def make(self,key):
        l=self.lod
        rock=((0,.63),(.17,1),(.64,.91),(1,.28))
        if key=="BloodcapBank":
            for i,(x,y,h,r) in enumerate([(-.23,.08,.29,.16),(.12,0,.33,.18),(.28,.10,.19,.11),(-.05,-.16,.21,.115),(.02,.18,.15,.09)][:5-l]):
                self.bulb(x,y,0,r*.36,r*.34,h*.77,10,((0,1),(.2,.95),(.83,.65),(1,.82)))
                self.bulb(x,y,h*.64,r,r*.93,h*.13,12,((0,.22),(.62,.88),(1,1)),phase=i*.37)
                self.bulb(x,y,h*.72,r,r*.93,h*.28,11,((0,1),(.35,.95),(.78,.69),(1,.18)),phase=i*.37,rough=.035)
                if l==0:
                    for j in range(3):
                        a=TAU*j/3+i
                        self.bulb(x+r*.48*math.cos(a),y+r*.47*math.sin(a),h*.933,r*.10,r*.065,h*.029,10,((0,1),(1,.58)),n=5)
        elif key=="AshPine": self.ash_pine()
        elif key=="StoneOutcrop":
            for i,(x,y,w,h) in enumerate([(0,0,1.15,1.46),(.84,.33,.66,.76),(-.72,.19,.67,.71),(.32,-.66,.42,.36)][:4-l]):
                self.bulb(x,y,0,w,w*.75,h,(0,2,1,0)[i],rock,phase=i+.28,rough=.16)
        elif key=="ShaleLedge": self.shale_ledge()
        elif key=="DenLitter": self.den_litter()
        elif key=="EmberSnag": self.ember_snag()
        elif key=="EmberSedge": self.ember_sedge()
        elif key in ("TimberHouse","MillHouse"):
            self.house(6.45 if key=="MillHouse" else 4.8,10.5 if key=="MillHouse" else 5.9,4.0 if key=="MillHouse" else 3.75,key=="MillHouse")
        elif key=="MillWheel":
            self.wheel(0,0,1.82,1.64,.70,(18,12,8)[l],4)
            for s in (-1,1):self.box(s*.43,0,0,.21,.48,1.81,2)
        elif key=="GrainBay":
            self.box(0,0,0,3,1.5,.15,3)
            for s in (-1,1):
                self.post((s*1.34,-.59,.13),(s*1.34,-.59,2.4),.13,.1,4)
                self.post((s*1.34,.59,.13),(s*1.34,.59,2.4),.13,.1,4)
            for z in (.95,2.18):self.box(0,0,z,2.96,1.42,.14,5)
            for j in range(6-2*l):
                self.bulb((j%3-1)*.81,-.08,.16+(j//3)*.94,.39,.40,.68,13,((0,.72),(.2,1),(.74,.87),(1,.33)))
        elif key=="CryptArch":self.arch(1.65,2.45,.50,.91,(13,9,5)[l])
        elif key=="CaveMouth":
            self.arch(2.02,2.15,.86,1.48,(11,7,5)[l])
            for s in (-1,1):
                self.bulb(s*2.7,.47,0,1.12,.94,2.13,1,rock,rough=.16,phase=s*.32)
                if l<2:self.bulb(s*3.37,.18,0,.77,.61,1.11,2,rock,rough=.13)
        elif key=="StoneBridge":
            for s in (-1,1):
                for y in (-2.7,2.7):self.box(s*1.32,y,0,.64,.82,1.46,0)
                self.post((s*1.53,-2.8,1.42),(s*1.53,2.8,1.42),.12,.12,3)
            n=(16,10,6)[l]
            for j in range(n):
                y=-2.8+(j+.5)*5.6/n
                self.box(0,y,.52+.16*(1-abs(y)/2.8),3,.99*5.6/n,.19,3+j%3)
        elif key=="WatchPost":
            for x in (-1.2,1.2):
                for y in (-1.1,1.1):
                    self.post((x,y,0),(x*.94,y*.94,4.94),.2,.145,4)
                self.post((x,-1.1,.7),(x,1.1,3.25),.10,.10,5,4)
            self.box(0,0,3.1,3.1,2.9,.30,3)
            for s in (-1,1):
                self.box(0,s*1.26,3.45,3.0,.14,.68,3)
                self.box(s*1.35,0,3.45,.13,2.65,.68,3)
            self.bulb(0,0,4.84,2.66,2.45,1.45,1,((0,1),(1,.12)),n=4,phase=math.pi*.25)
            if l<2:
                for j in range(7-l*2):self.box(.1,-1.33,.32+j*.39,.74,.17,.12,5)
        elif key=="RibPillar":
            for z,r,h,band in [(0,.62,.28,2),(.28,.46,.29,0),(.57,.39,2.5,1),(3.07,.49,.27,0),(3.34,.61,.31,2)]:
                self.post((0,0,z),(0,0,z+h),r,r,band)
            if l<2:
                for i in range((8,4)[l]):
                    a=TAU*i/(8,4)[l];x,y=.37*math.cos(a),.37*math.sin(a)
                    self.post((x,y,.53),(x,y,3.12),.066,.066,0,5)
        elif key=="Brazier":
            for i in range(3):
                a=TAU*i/3
                self.post((.38*math.cos(a),.38*math.sin(a),.026),(.21*math.cos(a),.21*math.sin(a),.69),.05,.076,6)
            self.bulb(0,0,.58,.44,.44,.41,6,((0,.44),(.74,1),(1,.95)))
            self.bulb(0,0,.89,.34,.34,.15,14,((0,1),(.63,.84),(1,.32)),rough=.13)
        elif key=="SupplySack":
            self.bulb(0,0,0,.29,.215,.55,13,((0,.68),(.16,.96),(.46,1),(.79,.70),(.87,.42),(1,.53)),rough=.065)
            self.bulb(0,0,.447,.148,.113,.049,4,((0,1),(1,.94)))
            if l<2:self.post((.10,-.09,.47),(.18,-.14,.32),.017,.013,5,5)
        elif key=="CoinStack":
            for x,y,count in [(0,0,4),(.14,-.07,2),(-.1,-.12,1)][:3 if l<2 else 2]:
                for i in range(count if l==0 else max(1,count//2)):
                    self.post((x,y,.022*i),(x,y,.022*i+.021),.097,.095,15)
        elif key=="BrokenCart":
            self.box(0,0,.54,1.42,2.28,.17,3)
            for s in (-1,1):
                self.wheel(s*.77,.18,.57,.47,.16,(10,8,6)[l],4,False)
                for j in range(3-l):self.box(s*.68,.1,.74+j*.19,.12,2.1-j*.12,.14,3)
                self.post((s*.48,-1,.61),(s*.45,-2.05,.14),.09,.055,5)
            self.box(0,.99,.73,1.25,.13,.52,3)
            self.box(-.10,-1.69,0,.40,.31,.16,2)
        elif key=="Palisade":
            for i in range((9,7,5)[l]):
                x=-1.5+3*i/((9,7,5)[l]-1);h=1.86+.2*math.sin(i*1.9)
                self.post((x,0,0),(x+.04,0,h),.20,.16,4)
                self.post((x+.04,0,h),(x+.07,0,h+.31),.16,.013,5)
            for h in (.50,1.24):self.box(0,-.16,h,3.45,.17,.17,3)
        elif key=="RangedQuiver":
            self.post((-.09,0,.04),(.11,0,.56),.072,.11,4)
            self.post((.092,0,.50),(.12,0,.57),.112,.113,7)
            self.box(-.09,0,0,.10,.09,.06,4)
            for i in range(4-l):
                x=.045+.027*i; y=-.052+.029*i
                self.post((x,y,.44),(x+.13,y,.85-i*.025),.009,.009,5,5)
                self.post((x+.097,y,.74-i*.025),(x+.13,y,.85-i*.025),.021,.006,10,4)
        elif key=="ShieldPlate":
            outline=[(0,0),(.26,.22),(.31,.57),(.19,.66),(-.19,.66),(-.31,.57),(-.26,.22)]
            self.rings([[(x,y,z) for x,z in outline] for y in (.04,-.04)],4)
            self.post((0,-.045,.34),(0,-.11,.34),.13,.07,6)
            self.box(0,-.049,.12,.057,.028,.52,7)
            if l<2:
                for s in (-1,1):self.box(s*.20,-.048,.30,.031,.025,.27,7)
        elif key=="SwarmSpines":
            for s in (-1,1):
                for i in range(4-l):
                    x=s*(.04+i*.054)
                    self.post((x,0,.023),(x+s*.115,.02,.22+.045*i),.06,.008,10)
            self.box(0,0,0,.36,.12,.055,4)
        elif key=="EliteCrest":
            self.bulb(0,0,0,.27,.19,.22,6,((0,.7),(.35,1),(1,.68)))
            for i in range(5-l):
                a=TAU*i/(5-l)
                self.post((.21*math.cos(a),.14*math.sin(a),.13),
                          (.29*math.cos(a),.19*math.sin(a),.38+.035*(i%2)),.065,.011,7)
        elif key=="Waystone":
            self.bulb(0,0,0,.54,.38,1.64,2,((0,1),(.25,.88),(.8,.65),(1,.31)),rough=.12)
            for i in range(3-l):
                self.box(0,-.249,.78+i*.17,.32-i*.045,.09,.075,10,yaw=.08)

    def bake(self,label,mat):
        mesh=bpy.data.meshes.new(label)
        mesh.from_pydata(self.verts,[],self.faces)
        mesh.update()
        layer=mesh.uv_layers.new(name="TrimAtlas")
        # Each face is a planar island inside its padded material tile.
        for face,band in zip(mesh.polygons,self.bands):
            n=face.normal
            axis=max(range(3),key=lambda i:abs(n[i]))
            a,b=((1,2),(0,2),(0,1))[axis]
            xy=[(mesh.vertices[v].co[a],mesh.vertices[v].co[b]) for v in face.vertices]
            xmin=min(x for x,y in xy);ymin=min(y for x,y in xy)
            span=max(max(x for x,y in xy)-xmin,max(y for x,y in xy)-ymin,.000001)
            for index,(x,y) in zip(face.loop_indices,xy):
                layer.data[index].uv=((band%4+.09+.82*(x-xmin)/span)/4,
                                      (band//4+.09+.82*(y-ymin)/span)/4)
        obj=bpy.data.objects.new(label,mesh)
        bpy.context.collection.objects.link(obj)
        mesh.materials.append(mat)
        return obj

bpy.ops.wm.read_factory_settings(use_empty=True)
image=paint_atlas()
material=bpy.data.materials.new("BorderlandAtlas")
material.use_nodes=True
node=material.node_tree.nodes.new("ShaderNodeTexImage")
node.image=image
principled=material.node_tree.nodes.get("Principled BSDF")
material.node_tree.links.new(node.outputs["Color"],principled.inputs["Base Color"])
principled.inputs["Roughness"].default_value=.88
bpy.context.preferences.filepaths.save_version=0
selected = sys.argv[sys.argv.index("--")+2:] or KEYS
if set(selected)-set(KEYS): raise ValueError("Unknown regional mesh")
for key in selected:
    counts=[]
    for level in range(3):
        builder=Mesh(level)
        builder.make(key)
        obj=builder.bake(key+"Detail"+str(level),material)
        obj.data.calc_loop_triangles()
        counts.append(len(obj.data.loop_triangles))
    print("BORDERLAND_MESH",key,counts,flush=True)
    assert 3500 >= counts[0] > counts[1] > counts[2] >= 4, (key,counts)
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/(key+".blend")),compress=True)
    for obj in list(bpy.context.scene.objects):
        m=obj.data
        bpy.data.objects.remove(obj,do_unlink=True)
        bpy.data.meshes.remove(m)
