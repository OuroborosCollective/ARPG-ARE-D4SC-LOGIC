"""Eight sculptural horizon landmarks. South (-Y) is the principal viewing face."""
from math import acos, cos, pi, sin


def _prism(b, outline, y, depth, material, edge=None):
    """Extrude a convex X/Z polygon. A counterclockwise front points south."""
    outline = list(outline)
    if sum(x * qz - qx * z for (x, z), (qx, qz)
           in zip(outline, outline[1:] + outline[:1])) < 0:
        outline.reverse()
    front = [(x, y, z) for x, z in outline]
    back = [(x, y + depth, z) for x, z in outline]
    b.face(front, material)
    b.face(back[::-1], material)
    for i in range(len(front)):
        j = (i + 1) % len(front)
        b.face([front[i], back[i], back[j], front[j]], edge or material)


def _arc(width, spring, rise, t):
    """Right half of a two-centred pointed arch, spring to crown."""
    offset = width * .68
    limit = acos(offset / (width + offset))
    angle = limit * t
    return ((width + offset) * cos(angle) - offset,
            spring + rise * sin(angle) / sin(limit))


def _arch(b, x, y, width, spring, rise, thick, depth, stone="stone", steps=6):
    course = ("basalt", stone, "slate", stone, stone)
    for mirror in (-1, 1):
        for k in range(steps):
            a, c = k / steps + .002, (k + 1) / steps - .002
            points = [_arc(width, spring, rise, a), _arc(width, spring, rise, c),
                      _arc(width + thick, spring, rise + thick * 1.25, c),
                      _arc(width + thick, spring, rise + thick * 1.25, a)]
            mat = course[(k + (mirror > 0)) % len(course)]
            _prism(b, [(x + mirror * u, z) for u, z in points],
                   y - (.065 if k % 3 == 0 else 0), depth, mat)
        px = x + mirror * (width + thick * .5)
        b.box((px, y + depth * .5, spring * .5), (thick, depth, spring), stone)
        for height, ledge in ((.23, .45), (.58, .23), (spring - .23, .14),
                              (spring + .06, .28)):
            b.box((px, y + depth * .5, height),
                  (thick + ledge, depth + ledge, .20 if height > .3 else .44), "trim")
    _prism(b, [(x - thick * .3, spring + rise + thick * .55),
               (x, spring + rise - thick * .23),
               (x + thick * .3, spring + rise + thick * .55),
               (x + thick * .25, spring + rise + thick * 1.33),
               (x - thick * .25, spring + rise + thick * 1.33)],
           y - .13, depth + .16, "trim")


def _tube(b, coords, radii, mat, sides=7):
    for i in range(len(coords) - 1):
        b.taper(coords[i], coords[i + 1], radii[i], radii[i + 1], mat, sides)


def _ellipse(b, x, y, z, rx, rz, thickness, mat, n=18):
    """Raised ring in the X/Z plane, with an actual open centre."""
    outer, inner = [], []
    for i in range(n):
        angle = 2 * pi * i / n
        ca, sa = cos(angle), sin(angle)
        outer.append((x + rx * ca, z + rz * sa))
        inner.append((x + (rx - thickness) * ca, z + (rz - thickness) * sa))
    for i in range(n):
        j = (i + 1) % n
        f0, f1, f2, f3 = [(u, y, v) for u, v in (inner[i], outer[i], outer[j], inner[j])]
        d = thickness * .7
        b.face([f0, f1, f2, f3], mat)
        b.face([f1, (f1[0], y+d, f1[2]), (f2[0], y+d, f2[2]), f2], mat)
        b.face([f3, (f3[0], y+d, f3[2]), (f0[0], y+d, f0[2]), f0], mat)
        b.face([(f3[0], y+d, f3[2]), (f2[0], y+d, f2[2]),
                (f1[0], y+d, f1[2]), (f0[0], y+d, f0[2])], mat)


def _rock(b, x, y, z, rx, ry, height, seed, mats=("slate", "basalt", "stone")):
    """Jointed rock with an angled, broken top and offset bedding courses."""
    n = 7
    rings = []
    for j, (up, rad, dx, dy) in enumerate(((0, .89, 0, 0), (.21, 1, .02, .01),
                                           (.78, .87, -.08, .025), (1, .66, -.15, .05))):
        rim = []
        for i in range(n):
            az = 2 * pi * i / n + .13 * sin(seed + .3 * i)
            w = 1 + .12 * sin(3 * i + seed)
            slope = .14 * cos(az) + .10 * sin(az - seed) if j >= 2 else 0
            rim.append((x + rx * (w * rad * cos(az) + dx),
                        y + ry * (w * rad * sin(az) + dy),
                        z + height * (up + slope)))
        rings.append(rim)
    for j in range(3):
        for i in range(n):
            k = (i + 1) % n
            p, q, r, s = rings[j][i], rings[j][k], rings[j+1][k], rings[j+1][i]
            m = mats[(i + j + seed) % len(mats)]
            b.face([p, q, r], m)
            b.face([p, r, s], m)
    cap = (x - rx * .13, y, z + height * 1.025)
    for i in range(n):
        b.face([rings[-1][i], rings[-1][(i+1) % n], cap], mats[-1])


def _crystal(b, loc, length, radius, drift, mat):
    x, y, z = loc
    dx, dy = drift
    tip = (x+dx, y+dy, z+length)
    neck = (x+dx*.73, y+dy*.73, z+length*.74)
    b.taper(loc, neck, radius*.7, radius, mat, 5)
    b.taper(neck, tip, radius, .018, mat, 5)


def _crypt(b):
    for i, (y, w, spring, rise) in enumerate(((-3.7, 3.45, 5.45, 4.9),
                                               (0, 3.25, 5.15, 4.65),
                                               (3.9, 3.04, 4.8, 4.4))):
        _arch(b, 0, y, w, spring, rise, .75, 1.08)
        for s in (-1, 1):
            x = s * (w + .55)
            b.taper((x, y+.2, .55), (x, y+.2, spring-.3), .57, .40, "basalt", 8)
            _prism(b, [(x+s*.38, .12), (x+s*2.0, .12),
                       (x+s*.64, spring*.88), (x+s*.35, spring*.85)],
                   y+.32, .96, "slate")
            b.taper((x, y+.60, spring), (x, y+.60, spring+2.2),
                    .32, .24, "stone", 6)
            b.taper((x, y+.60, spring+2.15), (x, y+.60, spring+3.65-i*.19),
                    .39, .025, "slate", 6)
            if i < 2:
                _tube(b, [(x,y+1.04,spring), (s*2.2,y+2.02,spring+2.7),
                          (0,y+3.5,spring+3.65)], [.20,.17,.12], "trim")
    for s in (-1, 1):
        for level in (1.25, 2.6, 4.0):
            b.box((s*4.92, .67, level), (1.85, 7.35, .28), "ash")
            for k in range(4):
                y = -2.2 + k*1.82
                b.box((s*4.82,y,level-.62), (.43,.35,.96), "basalt")
                b.box((s*4.28,y+.55,level+.25), (.60,.89,.17), "bone")
    # A real hanging mineral lamp silhouettes against the second arch.
    b.taper((0,-3.20,8.35), (0,-3.20,10.23), .055,.055,"iron",6)
    for s in (-1, 1):
        _tube(b, [(0,-3.20,8.40),(s*.45,-3.20,7.95),(s*.28,-3.20,7.05),
                  (0,-3.20,6.80)], [.075,.087,.078,.045], "iron", 5)
    _crystal(b, (0,-3.20,7.0), 1.15,.24,(.06,0),"mineral")
    _rock(b,-4.8,-4.25,0,1.35,1.0,1.25,2)
    _rock(b, 5.45,2.7,0,1.8,1.22,2.45,7)


def _seal(b):
    width, spring, rise = 3.45, 3.65, 3.78
    # A single solid slab fills the entire arch, including the pointed tympanum.
    outline = [(-width,0),(width,0),(width,spring)]
    outline += [_arc(width,spring,rise,i/8) for i in range(1,9)]
    outline += [(-x,z) for x,z in [_arc(width,spring,rise,i/8) for i in range(7,-1,-1)]]
    _prism(b,outline,-1.28,2.62,"basalt")
    _arch(b,0,-1.63,width,spring,rise,.78,3.60,stone="slate")
    for s in (-1,1):
        x = s*4.45
        b.box((x,.10,1.30),(1.65,3.60,2.60),"stone")
        _prism(b,[(x-.82,2.55),(x+.82,2.55),(x+.48,6.78),(x-.48,6.78)],
               -1.35,2.55,"slate")
        b.taper((x,-.10,6.80),(x,-.10,9.55 if s>0 else 8.75),.59,.06,"trim",6)
        for z in (.78,2.48):
            b.beam((s*.30,-1.34,z),(s*3.15,-1.34,z+.08*s),.15,"iron")
    # The relief is on a projecting stone boss cut into the solid slab. Its
    # front receives the petals, while the rivets reach through the outer band.
    b.taper((0,-1.23,3.85),(0,-1.41,3.85),2.47,2.42,"basalt",24)
    b.taper((0,-1.40,3.85),(0,-1.70,3.85),2.42,2.35,"stone",24)
    for rad,th,m,y in ((2.35,.23,"trim",-1.71),(1.99,.13,"iron",-1.76),
                        (1.43,.10,"stone",-1.82),(.53,.19,"mineral",-1.91)):
        _ellipse(b,0,y,3.85,rad,rad,th,m,24)
    for i in range(8):
        az = pi*i/4
        def at(rad, dang=0):
            return (rad*cos(az+dang),3.85+rad*sin(az+dang))
        _prism(b,[at(.44),at(1.36,-.19),at(1.82),at(1.36,.19)],
               -1.83,.17,"trim" if i%2 else "slate")
        x,z = at(2.16)
        b.taper((x,-1.55,z),(x,-1.86,z),.08,.06,"iron",6)
    _tube(b,[(-2.67,-1.295,5.57),(-2.37,-1.295,5.39),(-2.46,-1.295,5.19),
             (-2.15,-1.295,5.17)], [.047,.055,.038,.013], "dark", 4)


def _nest(b):
    bones = []
    for i in range(10):
        y = -3.8 + i*1.10
        bones.append((.84*sin(i*.55)-.24, y, 5.08+1.92*sin(.48+i*.28)))
    _tube(b,bones,[.31+.22*sin(pi*i/9) for i in range(10)],"bone",7)
    for i,(x,y,z) in enumerate(bones):
        b.ellipsoid((x,y,z),(.59,.40,.48),"bone",3,7)
        _tube(b,[(x,y,z+.18),(x-.12,y+.13,z+.78),(x-.22,y+.26,z+1.36)],
              [.25,.18,.026],"bone",6)
        if i==9: continue
        for s in (-1,1):
            reach = 2.1 + 2.05*sin(.3+i*.3) + .25*s
            tip_z = .45 + .28*(i%3)
            if (s<0 and i==1) or (s>0 and i==3):
                reach *= .84
                tip_z = 2.7
            _tube(b,[(x+s*.24,y,z),(x+s*reach*.55,y-.07,z-.16),
                     (x+s*reach*.95,y-.18,z-1.19),(x+s*reach,y-.48,z-2.35),
                     (x+s*reach*.84,y-.68,tip_z)],
                  [.31,.35,.28,.20,.065],"bone",7)
    for s in (-1,1):
        for i in range(4):
            _rock(b,s*(4.5+i*.35),-2.9+i*2.4,0,1.95,2.2,
                  2.6+abs(sin(i+.8))*2.15,i+(6 if s>0 else 2))
    # A split mandible and hooked teeth make the shape read as a fossil, not a fence.
    for s in (-1,1):
        jaw=[(s*2.7,-3.45,1.75),(s*2.25,-5.50,1.00),(s*1.42,-6.10,.84),
             (s*.44,-6.36,1.00)]
        _tube(b,jaw,[.45,.46,.32,.22],"bone",7)
        for i in range(4):
            x,y,z = s*(2.52-i*.42),-4.50-i*.49,1.02
            b.taper((x,y,z),(x-s*.14,y-.11,z+.65+.47*(i==1)),.14,.02,"bone",5)


def _bear(b):
    left = [(-7.35,.05),(-5.70,.05),(-2.68,3.62),(-1.42,5.15),(-3.14,6.44),(-6.41,5.11)]
    crown = [(-3.14,6.44),(-1.42,5.15),(1.30,5.72),(3.28,5.05),(4.50,6.65),
             (2.72,8.02),(-.86,7.68)]
    # Split the broken eyebrow into independent convex stone wedges.
    _prism(b,left,-1.35,4.35,"slate")
    for k in ((0,1,2),(0,2,6),(2,5,6),(2,3,4),(2,4,5)):
        _prism(b,[crown[i] for i in k],-1.53,4.50,"basalt" if k[0]==2 else "slate")
    _prism(b,[(3.28,5.05),(5.19,.12),(7.82,.12),(7.32,5.31),(4.5,6.65)],
           -1.62,4.60,"stone")
    for k,(x,y,rx,ry,h) in enumerate(((-6,-1.6,2.9,2.5,4.35),(-4.85,.2,2.7,2,5),
                                     (5.75,-1.22,2.9,2.4,4.5),(6.2,2.7,2.65,2.1,5.6),
                                     (-3.65,3.70,2.45,2.8,3.5),(4.20,4.55,1.92,2.1,3.6),
                                     (-6.15,-3.90,1.65,1.4,1.45),(3.43,-3.9,1.45,.98,.66))):
        _rock(b,x,y,0,rx,ry,h,3+k)
    _tube(b,[(-6.2,.14,6.0),(-4.7,-.18,7.04),(-.65,-.96,7.28),
             (3.2,-.77,8.0),(5.1,-.07,9.5)], [.53,.43,.32,.22,.06],"bark",7)
    for i,x in enumerate((-3.5,-2.87,-2.15,-1.52,.21,.91,2.58,3.02,3.72)):
        sway=.45*sin(2.93*i)
        top = 7.05+.32*sin(i)
        length=1.5+1.55*(.5+.5*cos(i*1.75))
        _tube(b,[(x,-1.12,top),(x+sway,-1.52,top-.66),(x-.19+sway,-1.87,top-1.16),
                 (x-.18,-1.94,top-length)], [.14,.105,.08,.016],"bark",5)
    for i in range(3):
        x=-5.94+i*.25
        _tube(b,[(x,-3.12,2.73),(x+.14,-3.18,2.30),(x+.55,-3.17,1.85)],
              [.052,.062,.019],"trim",4)


def _rift(b):
    # Four successive ledges, geologically continuous but offset in plan.
    for layer in range(4):
        y=-3.9+layer*2.65
        x_shift=(0,.63,-.42,.35)[layer]
        for s in (-1,1):
            x=s*(4.55+.25*layer)+x_shift
            height=(6.64,8.15,9.66,11.35)[layer]+.9*s
            for j in range(3):
                _rock(b,x+s*j*.92,y+.63*j,0,1.62+.30*j,1.65+.21*j,
                      height*(1-.145*j),layer*11+j+(5 if s>0 else 2))
            if (layer + (s>0))%2==0:
                _tube(b,[(x-s*.58,y-1.22,.40),(x-s*1.12,y-1.26,2.06),
                         (x-s*.55,y-1.44,3.05),(x-s*1.29,y-1.13,4.10),
                         (x-s*.76,y-1.21,height*.75)],
                      [.09,.14,.13,.09,.034],"mineral",5)
    for k,(x,y) in enumerate(((-3.25,-4.26),(3.43,-3.82),(2.75,-.78),(-4.44,1.20))):
        for i in range(4):
            theta=i*2.40+k
            _crystal(b,(x+.42*cos(theta),y+.36*sin(theta),.40),
                     .85+1.30*abs(sin(1.61*i+.52*k)),.18+.10*(i==1),
                     (.42*cos(theta),.29*sin(theta)), "mineral" if i%2==0 else "slate")


def _mill_vault(b):
    for i,(y,w,z) in enumerate(((-2.55,4.15,4.18),(1.34,3.85,3.96),(5.02,3.64,3.81))):
        _arch(b,0,y,w,z,3.85,.88,1.08,stone="basalt",steps=5)
        for s in (-1,1):
            x=s*(w+.53)
            b.box((x,y+.59,1.00),(1.46,1.78,2.0),"slate")
            if i<2:
                _tube(b,[(x,y+.55,z+.35),(x-s*2.1,y+1.77,z+2.85),(0,y+2.94,z+3.39)],
                      [.23,.22,.16],"stone",6)
    b.box((0,1.10,.19),(10.20,9.80,.38),"basalt")
    b.box((0,.69,.405),(7.04,8.37,.035),"water")
    for s in (-1,1):
        b.box((s*4.06,.22,.54),(1.04,9.25,.39),"ash")
    # Two masonry journals carry the axle. The wheel sits clear of the arch
    # masonry and the bottom of its band just grazes the cistern water.
    x,y,z=-1.56,-1.60,2.84
    for bearing_y in (-2.78,2.46):
        b.box((x,bearing_y,.57),(1.43,.92,.64),"basalt")
        _prism(b,[(x-.67,.79),(x+.67,.79),(x+.43,2.43),(x-.43,2.43)],
               bearing_y-.33,.66,"slate")
        b.box((x,bearing_y,z-.33),(1.06,.79,.39),"wood")
        _ellipse(b,x,bearing_y-.42,z,.38,.38,.15,"iron",10)
        for s in (-1,1):
            b.taper((x+s*.39,bearing_y-.32,z-.34),(x+s*.39,bearing_y-.46,z-.34),
                    .081,.062,"iron",5)
    b.taper((x,y-1.30,z),(x,y+4.43,z),.24,.27,"wood",9)
    _ellipse(b,x,y,z,2.42,2.42,.22,"wood",18)
    _ellipse(b,x,y-.035,z,2.45,2.45,.085,"iron",18)
    for i in range(8):
        ang=i*pi/4
        b.beam((x,y+.065,z),(x+2.24*cos(ang),y+.065,z+2.24*sin(ang)),.16,"wood")
        xx,zz=x+2.35*cos(ang),z+2.35*sin(ang)
        b.box((xx,y+.13,zz),(.19,.58,.18),"iron")
    b.taper((x,y-.24,z),(x,y+.44,z),.43,.41,"iron",8)
    b.beam((-5.3,-2.14,6.39),(5.43,-2.14,6.17),.31,"wood",.46)
    for col,height in ((0,7),(1,4)):
        for j in range(height):
            rx=.115 if j%2 else .17
            _ellipse(b,1.87+1.26*col,-2.11+.04*(j%2),6.16-.35*j,
                     rx,.255,.055,"iron",8)
    for i in range(3):
        b.box((4.12,-3.82+i*.88,.52+.16*i),(.89,.52,.19),"slate")


def _king(b):
    # A ruined ring of crown-stones overtaken by the roots of a single tree.
    _rock(b,-.40,.26,0,5.72,4.54,2.0,6)
    for i in range(7):
        a=2*pi*i/7+.15
        dx,dy=cos(a),sin(a)
        x,y=3.15*dx,2.60*dy+.45
        h=(6.3,7.3,5.15,8.15,5.85,3.74,7.45)[i]
        _tube(b,[(x,y,1.15),(x+dx*.55,y+dy*.35,2.75),
                 (x+dx*1.32,y+dy*.75,h),(x+dx*1.07,y+dy*.61,h+1.27)],
              [.66,.62,.47,.13],"slate",6)
        _crystal(b,(x+dx*1.15,y+dy*.68,h+.20),1.65,.38,(-dx*.24,-dy*.15),"trim")
        b.beam((x,y,2.28),(3.15*cos(a+2*pi/7),2.60*sin(a+2*pi/7)+.45,2.28),.47,"trim")
    branches=[((-.46,.64,1.15),(-.92,.83,4.14),(.28,1.02,6.53),(-.55,1.13,9.83)),
              ((.28,1.02,6.53),(1.48,.84,8.38),(2.80,.62,9.12),(3.21,.54,10.72)),
              ((-.76,.80,4.18),(-2.0,.72,6.01),(-3.32,.83,6.99),(-3.83,.45,8.24))]
    for j,p in enumerate(branches):
        _tube(b,p,[.94,.63,.41,.105] if j==0 else [.46,.33,.18,.055],"bark",7)
    for i in range(6):
        a=i*pi/3
        _tube(b,[(-.46,.64,2.14),(1.67*cos(a),.70+1.67*sin(a),1.68),
                 (3.87*cos(a),.37+3.18*sin(a),1.22),(5.32*cos(a),4.2*sin(a),.13)],
              [.39,.28,.16,.035],"bark",6)
    for k,(x,y,z) in enumerate(((-.65,1.4,9.3),(1.87,.89,8.74),(2.76,.88,9.65),(-2.74,.72,7.26))):
        for i in range(3):
            b.ellipsoid((x+.65*cos(i*2.14),y+.40*sin(i*2.14),z+.17*i),
                        (.95,.67,.51),"leaf" if (i+k)%3 else "moss",3,6)


def _guardian(b):
    for i,(z,r,top,h) in enumerate(((0,4.64,4.52,.56),(.55,4.12,4.08,.38),
                                     (.93,3.75,2.9,.55),(1.48,2.72,2.14,1.14),
                                     (2.60,2.48,2.43,.30))):
        b.taper((0,0,z),(0,0,z+h),r,top,"basalt" if i%2==0 else "trim",10)
    # Bowl with a genuine hollow. The empty centre remains visible from above.
    n=12
    for i in range(n):
        a=2*pi*i/n
        c=2*pi*(i+1)/n
        def at(r,h,angle):
            return (r*cos(angle),r*sin(angle),h)
        lip=4.18-(.25 if i in (3,4) else 0)
        lip2=4.18-(.25 if (i+1)%n in (3,4) else 0)
        b.face([at(1.32,2.86,a),at(1.32,2.86,c),at(2.10,lip2,c),at(2.10,lip,a)],"slate")
        b.face([at(2.10,lip,a),at(2.10,lip2,c),at(1.66,lip2-.08,c),at(1.66,lip-.08,a)],"trim")
        b.face([at(1.66,lip-.08,a),at(1.66,lip2-.08,c),at(.62,3.12,c),at(.62,3.12,a)],"dark")
    b.taper((0,0,3.085),(0,0,3.12),.63,.63,"basalt",12)
    for i,(x,y,h) in enumerate(((-3.7,.95,7.67),(3.6,1.35,5.32),(.18,3.44,9.55))):
        _tube(b,[(x,y,.65),(x*1.16,y*1.04,2.76),(x*.95,y*1.1,h-1.83),
                 (x*.72,y*1.0,h)], [.50,.74,.53,.30],"basalt",6)
        _tube(b,[(x*.95,y*1.11,h-1.79),(x*.86,y*1.05,h-.79),(x*.70,y,h-.20)],
              [.088,.080,.034],"ember",5)
    for i in range(5):
        a=i*2*pi/5+.30
        _tube(b,[(2.09*cos(a),2.09*sin(a),3.72),(2.26*cos(a),2.26*sin(a),4.35),
                 (1.75*cos(a),1.75*sin(a),4.99)],
              [.18,.16,.034],"iron",6)
    _rock(b,-4.10,-2.62,0,1.36,1.08,.82,5)
    _rock(b,3.39,-2.35,0,1.52,1.22,1.09,8)


_BUILDERS = {"crypt_arch": _crypt, "sealed_arch": _seal, "nest": _nest, "bear_den": _bear,
             "side_rift": _rift, "mill_vault": _mill_vault, "king": _king, "guardian": _guardian}


def draw(b, kind):
    """Draw the named landmark into the supplied world-geometry builder."""
    _BUILDERS[kind](b)
