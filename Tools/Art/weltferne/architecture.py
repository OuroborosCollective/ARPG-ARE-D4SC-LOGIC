"""Original landmark meshes for the Ashen Crypt sightlines (metres; +Y north).

Most of the masonry is built as free-standing voussoirs and voussoir courses.
The dark openings in the large silhouettes are real geometry gaps.
"""
from math import atan2, cos, hypot, pi, sin

TAU = 2 * pi


def _plane(x, y, u, v, z, angle):
    c, s = cos(angle), sin(angle)
    return (x + c*u - s*v, y + s*u + c*v, z)


def _slab(b, x, y, z, outline, depth, mat, angle=0):
    """Extrude a convex polygon in a vertical plane with consistent normals."""
    area = sum(u*q - p*v for (u, v), (p, q) in zip(outline, outline[1:] + outline[:1]))
    if area < 0:
        outline = list(reversed(outline))
    front = [_plane(x, y, u, -depth/2, z+h, angle) for u, h in outline]
    back = [_plane(x, y, u, depth/2, z+h, angle) for u, h in outline]
    b.face(front, mat)
    b.face(list(reversed(back)), mat)
    for i in range(len(front)):
        j = (i+1) % len(front)
        b.face([front[i], back[i], back[j], front[j]], mat)


def _archpath(w, h, steps, pointed):
    if not pointed:
        return [(-w*cos(pi*i/steps), h*sin(pi*i/steps)) for i in range(steps+1)]
    c = (h*h-w*w)/(2*w)
    finish = atan2(h, -c)
    half = [((c+(c+w)*cos(pi+(finish-pi)*i/(steps//2))),
             (c+w)*sin(pi+(finish-pi)*i/(steps//2))) for i in range(steps//2+1)]
    return half + [(-u, y) for u, y in reversed(half[:-1])]


def _arch(b, x, y, spring, w, h, thickness, depth, angle=0, mat="stone", steps=12,
          pointed=True, broken=(), detail=True):
    inner = _archpath(w, h, steps, pointed)
    outer = _archpath(w+thickness, h+thickness, steps, pointed)
    for i in range(steps):
        if i in broken:
            continue
        ia, ib, oa, ob = inner[i], inner[i+1], outer[i], outer[i+1]
        # Straight, closely fitted jamb stones keep the seams visible at a distance.
        k = .012
        blend = lambda p,q,t: (p[0]*(1-t)+q[0]*t, p[1]*(1-t)+q[1]*t)
        profile = [blend(ia,ib,k), blend(ib,ia,k), blend(ob,oa,k), blend(oa,ob,k)]
        tone = ("trim" if mat == "stone" else "stone") if detail and i % 7 == 2 else mat
        _slab(b, x, y, spring, profile, depth*(1.055 if i == steps//2 else 1), tone, angle)


def _column(b,x,y,z,h,w,mat="stone"):
    b.box((x,y,z+.10*h),(w*1.23,w*1.23,.2*h),"basalt")
    b.box((x,y,z+.53*h),(w,w,.75*h),mat)
    b.box((x,y,z+.90*h),(w*1.14,w*1.14,.10*h),"trim")
    b.box((x,y,z+.97*h),(w*1.28,w*1.28,.07*h),mat)


def _buttress(b,x,y,z,h,w,angle):
    outline=[(-w*.38,0),(w,0),(w,h*.27),(w*.66,h*.32),(w*.66,h*.62),
             (w*.38,h*.67),(w*.38,h*.90),(-w*.38,h)]
    _slab(b,x,y,z,outline,w*.80,"stone",angle)
    for dz,rad in ((.28,.86),(.64,.64)):
        _slab(b,x,y,z+h*dz,[(-w*.41,-.13),(w*rad,-.13),(w*(rad-.08),.1),
                                 (-w*.39,.1)],w*.93,"trim",angle)


def _lathe(b,x,y,profile,materials,sides=12,phase=0,cap=False):
    """Outer skin of a vessel. Its unfilled mouth stays open."""
    rings=[[(x+r*cos(TAU*i/sides+phase),y+r*sin(TAU*i/sides+phase),z) for i in range(sides)]
           for z,r in profile]
    for k,(lower,upper) in enumerate(zip(rings,rings[1:])):
        mat=materials[k%len(materials)]
        for i in range(sides):
            j=(i+1)%sides
            b.face([lower[i],lower[j],upper[j],upper[i]],mat)
    if cap:
        b.face(rings[-1],materials[-1])


def _spire(b,x,y,z,r,h,lean=0,mat="roof"):
    b.taper((x,y,z),(x+lean*.12,y,z+h*.20),r,r*.72,mat,8)
    b.taper((x+lean*.12,y,z+h*.20),(x+lean*.28,y,z+h*.58),r*.72,r*.28,mat,8)
    b.taper((x+lean*.28,y,z+h*.58),(x+lean,y,z+h),r*.28,.045,mat,8)
    b.taper((x+lean,y,z+h-.22),(x+lean,y,z+h+.55),.08,.024,"iron",6)


def _slit(b,x,y,z,height,width,angle=0,lit=False):
    """Recess, deep reveal and pointed head for a small lancet."""
    d=.11
    profile=[(-width,0),(width,0),(width,height*.68),(0,height),(-width,height*.68)]
    _slab(b,x,y,z,profile,d,"dark",angle)
    def bar(p,q,w,mat):
        b.beam(_plane(x,y,p[0],-.10,z+p[1],angle),_plane(x,y,q[0],-.10,z+q[1],angle),w,mat)
    for p,q in zip(profile,profile[1:]+profile[:1]):
        bar(p,q,.068,"trim")
    if lit:
        _slab(b,*_plane(x,y,0,-.075,z+.11*height,angle),
              [(-width*.16,0),(width*.16,0),(width*.10,height*.47),(0,height*.58),
               (-width*.11,height*.49)],.04,"amber",angle)


def _chimney(b,x,y,z,h,r,hot=False):
    _lathe(b,x,y,[(z,r*1.25),(z+.13*h,r*1.20),(z+.17*h,r),(z+.65*h,r*.83),
                   (z+.68*h,r*.93),(z+.72*h,r*.93),(z+.74*h,r*.80),(z+.92*h,r*.74),
                   (z+.94*h,r*.92),(z+h,r*.92),(z+h,r*.56),(z+.86*h,r*.52)],
           ["basalt","trim","stone","iron","trim","iron","slate","dark","slate","trim","dark"],10)
    if hot:
        for u,k in ((.27,.20),(2.8,.32),(4.65,.24)):
            p=[(x+r*cos(u+.09*j)*(1-.10*j),y+r*sin(u+.09*j)*(1-.10*j),
                z+h*(k+.064*j)) for j in range(4)]
            for a,c in zip(p,p[1:]):
                b.beam(a,c,.048,"ember")


def _roof(b,x,y,z,w,d,rise,mat="roof",angle=0):
    """Sloping sheets, raised ridge, dark eaves and individual metal battens."""
    # Each side has its own thickness; avoid fan triangulation across a concave shape.
    for sign in (-1,1):
        pts=[(sign*w*.54,-.12),(sign*w*.54,.03),(0,rise+.08),(0,rise-.12)]
        _slab(b,x,y,z,pts,d*1.10,mat,angle)
    for offset in (-.50,.50):
        for sign in (-1,1):
            b.beam(_plane(x,y,sign*w*.54,d*offset*1.09,z-.04,angle),
                   _plane(x,y,0,d*offset*1.09,z+rise+.075,angle),.16,"wood")
    for i in range(1,6):
        v=d*(i/6-.5)
        for sign in (-1,1):
            b.beam(_plane(x,y,sign*w*.52,v,z+.10,angle),
                   _plane(x,y,0,v,z+rise+.13,angle),.061,"slate")
    b.beam(_plane(x,y,0,-d*.57,z+rise+.05,angle),
           _plane(x,y,0,d*.57,z+rise+.05,angle),.215,"slate")


def _house(b,x,y,z,w,d,h,rise,angle=0,lit=True):
    wall=[(-w*.5,0),(w*.5,0),(w*.5,h),(0,h+rise*.93),(-w*.5,h)]
    _slab(b,x,y,z,wall,d,"stone",angle)
    for side in (-1,1):
        v=side*d*.508
        for fx in (-.46,.0,.46):
            b.beam(_plane(x,y,w*fx,v,z+.19,angle),_plane(x,y,w*fx,v,z+h,angle),.22,"wood")
        for frac in (.19,.57,.98):
            b.beam(_plane(x,y,-w*.49,v,z+h*frac,angle),_plane(x,y,w*.49,v,z+h*frac,angle),.18,"wood")
        for fx in (-1,1):
            b.beam(_plane(x,y,w*fx*.455,v,z+h*.58,angle),
                   _plane(x,y,w*fx*.07,v,z+h*.93,angle),.14,"wood")
            xx,yy,zz=_plane(x,y,w*fx*.25,v+side*.070,z+h*.25,angle)
            _slit(b,xx,yy,zz,h*.24,w*.043,angle if side<0 else angle+pi,lit and side<0)
    for fx in (-.504,.504):
        u=w*fx
        for v in (-.42,-.12,.18,.45):
            b.beam(_plane(x,y,u,v*d,z+.1,angle),_plane(x,y,u,v*d,z+h,angle),.17,"wood")
        for f in (.18,.57,.99):
            b.beam(_plane(x,y,u,-.5*d,z+h*f,angle),_plane(x,y,u,.5*d,z+h*f,angle),.18,"wood")
        for v in (-.42,-.10,.18):
            b.beam(_plane(x,y,u,v*d,z+h*.59,angle),_plane(x,y,u,(v+.24)*d,z+h*.90,angle),.14,"wood")
    _roof(b,x,y,z+h,w,d,rise,"roof",angle)


def _ivy(b,x,y,z,length,angle=0,seed=1):
    """A climbing vine rooted in a mortar joint: crooked stem, twigs, a handful of torn leaves."""
    def at(u,v,h):
        return _plane(x,y,u,v,z+h,angle)
    joints=[]
    for i in range(8):
        u=.16*sin(i*1.44+seed)+.10*sin(i*2.2)+i*.071
        h=length*i/7
        joints.append(at(u,-.035-.025*sin(i),h))
        if i<1 or i>6:
            continue
        side=1 if i%2 else -1
        twig_u=u+side*(.20+.07*(i%3))
        stem=at(twig_u,-.11,h+.16)
        b.beam(joints[-1],stem,.045,"bark")
        if i not in (2,5):
            a=at(twig_u-.10,-.15,h+.13)
            c=at(twig_u+.09,-.12,h+.14)
            tip=at(twig_u+.15*side,-.19,h+.43)
            mid=at(twig_u,-.20,h+.20)
            b.face([a,mid,tip],"moss")
            b.face([mid,c,tip],"leaf")
    for i,(a,c) in enumerate(zip(joints,joints[1:])):
        b.beam(a,c,.074-i*.005,"bark")


def _tower_screen(b):
    """Load-bearing stage with two pierced lancets, inset rear reveals and a blind lower register."""
    # Four real openings face across a hollow chamber. The thick ends knit the faces together.
    b.box((0,0,2.56),(5.50,5.50,3.03),"basalt")
    b.box((0,0,8.60),(5.50,5.50,1.78),"stone")
    for face_angle in (0,pi/2,pi,3*pi/2):
        fx,fy=2.48*sin(face_angle),-2.48*cos(face_angle)
        def pt(u,v,z):
            return _plane(fx,fy,u,v,z,face_angle)
        def wall_strip(u0,u1,lo,hi,material,depth=.53):
            x,y,z=pt((u0+u1)/2,0,lo)
            _slab(b,x,y,z,[(u0-(u0+u1)/2,0),(u1-(u0+u1)/2,0),
                    (u1-(u0+u1)/2,hi-lo),(u0-(u0+u1)/2,hi-lo)],depth,material,face_angle)
        wall_strip(-2.75,-1.23,4.06,7.72,"stone")
        wall_strip(-.27,.27,4.06,7.72,"basalt")
        wall_strip(1.23,2.75,4.06,7.72,"stone")
        for slit in (-.75,.75):
            x,y,_=pt(slit,0,0)
            # Spandrel from the inner arch to the flat soffit. Front faces have a second, splayed edge.
            collar=_archpath(.48,1.13,6,True)
            for (a,h),(c,k) in zip(collar,collar[1:]):
                _slab(b,x,y,6.48,[(a,h),(c,k),(c,1.245),(a,1.245)],.53,"stone",face_angle)
            if face_angle not in (0,pi/2):
                continue
            qx,qy,_=pt(slit,-.29,0)
            _arch(b,qx,qy,6.48,.52,1.14,.15,.19,face_angle,mat="trim",steps=8,detail=False)
            for sign in (-1,1):
                b.beam(pt(slit+sign*.55,-.30,4.06),pt(slit+sign*.55,-.30,6.49),.115,"trim")
            # A fork of stone inside each aperture, with a narrow ring of sky round the upper leaf.
            b.beam(pt(slit,0,4.09),pt(slit,0,6.58),.12,"slate")
            for sign in (-1,1):
                b.beam(pt(slit,0,6.04),pt(slit+sign*.27,0,6.41),.084,"trim")
                b.beam(pt(slit+sign*.27,0,6.41),pt(slit,0,7.02),.081,"trim")
        x,y,z=pt(0,-.32,3.83)
        _slab(b,x,y,z,[(-2.30,0),(2.30,0),(2.30,.16),(-2.30,.16)],.39,"trim",face_angle)
        x,y,z=pt(0,-.32,7.75)
        _slab(b,x,y,z,[(-2.44,0),(2.37,0),(2.31,.13),(-2.30,.13)],.34,"slate",face_angle)
        # A modest plinth panel with a funerary mark; it is too small and too high to read as an exit.
        if face_angle in (0,pi):
            x,y,z=pt(0,-.33,1.78)
            _slab(b,x,y,z,[(-.72,.28),(-.58,0),(.58,0),(.72,.28),(.72,.99),(0,1.24),(-.72,.99)],
                  .15,"slate",face_angle)
            b.beam(pt(-.41,-.438,2.42),pt(.38,-.438,2.42),.086,"trim")
            b.beam(pt(0,-.438,2.12),pt(0,-.438,2.76),.075,"iron")
    # Low rubble and staggered wall remnants attach to a lower aisle. They carry no door.
    # Divide the ragged upper line into convex prisms; a fan over the whole wall would fill the missing bites.
    def rags(x,y,axis,knots,deep):
        for i,((u,h),(v,k)) in enumerate(zip(knots,knots[1:])):
            if abs(u-v)<.01: continue
            # Masonry continues below grade along the complete wall; retain the broken skyline.
            _slab(b,x,y,-.14,[(u,0),(v,0),(v,k+.26),(u,h+.26)],deep,"stone" if i%3 else "slate",axis)
            if i%3!=1:
                a=_plane(x,y,u,-.01,h+.15,axis); c=_plane(x,y,v,-.01,k+.15,axis)
                b.beam(a,c,.16,"trim" if i==4 else "slate",depth=deep*1.12)
    rags(-2.74,-.74,-.11,[(0,5.0),(-.51,4.68),(-.78,4.72),(-1.02,3.64),(-1.6,3.68),
           (-2.0,3.12),(-2.45,3.11),(-2.94,2.2),(-3.47,2.31),(-4.18,.91),(-5.39,.51)],1.08)
    rags(2.74,-.35,.13,[(0,3.4),(.38,3.47),(.72,2.75),(1.30,2.75),
           (1.62,1.53),(2.22,1.42),(2.80,1.0),(3.44,.66)],.97)
    _buttress(b,-5.18,-1.04,.08,3.10,.80,-pi/2-.11)
    _buttress(b,4.14,-.40,.07,2.31,.72,-pi/2+.13)
    for x,y,z,sz,a in ((-6.83,-1.23,.11,.80,.19),(-7.50,-.15,.08,.54,-.32),
                        (-5.82,-1.90,.06,.48,.30),(5.64,-.70,.07,.67,.12)):
        _slab(b,x,y,-.07,[(-sz,0),(sz*.80,0),(sz*.70,sz*.44+z+.07),(-sz*.30,sz*.70+z+.07)],
              .8*sz,"basalt",a)
    _ivy(b,-1.48,-2.83,1.28,2.43,seed=2)
    _ivy(b,-5.19,-1.31,.65,2.16,angle=-.11,seed=3)
    # Broken anchor chain from the old aisle roof to the corner bond stone.
    path=[(-2.38,-3.10,8.85),(-2.79,-3.13,7.19),(-3.19,-2.90,6.34),
          (-3.62,-2.39,6.01),(-3.97,-1.68,5.94)]
    for a,c in zip(path,path[1:]):
        b.beam(a,c,.085,"iron")


def _bell(b):
    for z,w,h in ((.28,7.35,.56),(.72,6.72,.36),(1.01,6.15,.23)):
        b.box((0,0,z),(w,w,h),"basalt" if z<1 else "trim")
    _tower_screen(b)
    for side in (-1,1):
        for trans in (-2.13,2.13):
            _buttress(b,side*2.74,trans,.3,9.55,1.22,0 if side>0 else pi)
            _buttress(b,trans,side*2.74,.3,9.55,1.22,pi/2 if side>0 else -pi/2)
    for z,w,h,m in ((9.27,5.88,.34,"basalt"),(9.60,6.13,.30,"stone"),
                    (9.90,6.46,.30,"trim"),(10.19,6.0,.32,"slate")):
        b.box((0,0,z),(w,w,h),m)
    for x in (-2.24,-1.02,.92,2.24):
        for side in (-1,1):
            _slab(b,x,side*2.91,8.73,[(-.18,.06),(.18,.06),(.27,.53),(-.25,.53)],.39,"basalt")
    for x in (-2.35,2.35):
        for y in (-2.35,2.35):
            _column(b,x,y,10.22,4.48,1.0)
            _spire(b,x*1.23,y*1.23,10.17,.41,2.10,mat="slate")
    for side in (-1,1):
        _arch(b,0,side*2.35,14.18,1.86,2.42,.53,.91,mat="trim")
        _arch(b,side*2.35,0,14.18,1.86,2.42,.53,.91,pi/2,mat="trim")
    b.beam((-2.53,0,15.64),(2.53,0,15.64),.33,"wood")
    for xx in (-2.35,2.35):
        for yy in (-2.0,2.0):
            b.beam((xx,yy,14.24),(xx*.49,yy*.07,15.62),.19,"wood")
    b.beam((-1.85,-.37,15.40),(1.84,-.37,15.40),.10,"iron")
    b.taper((0,0,14.62),(0,0,15.65),.22,.13,"iron",10)
    _lathe(b,0,0,[(12.39,1.38),(12.48,1.5),(12.66,1.5),(12.79,1.3),(13.01,1.14),
                   (13.68,.81),(14.34,.68),(14.60,.46),(14.70,.22)],
           ["iron","trim","iron","mineral","iron","iron","iron","trim"],18,cap=True)
    # Cast inner skin, under-crown and rolled mouth close the actual wall of the bell.
    # The clapper hangs from the under-crown; the great opening stays hollow.
    _lathe(b,0,0,[(14.48,.19),(14.27,.50),(13.60,.68),(13.00,.99),(12.72,1.23),
                    (12.39,1.28),(12.39,1.38)],["iron"],18)
    b.face([(.19*cos(-TAU*i/18),.19*sin(-TAU*i/18),14.48) for i in range(18)],"iron")
    b.taper((0,0,12.05),(0,0,14.52),.085,.11,"iron")
    b.ellipsoid((0,0,12.07),(.23,.23,.28),"iron",4,8)
    _lathe(b,0,0,[(13.11,1.15),(13.16,1.125),(13.20,1.095)],["trim","iron"],18)
    for x in (-2.25,2.25):
        b.beam((x,-2.4,16.96),(x,2.4,16.96),.32,"wood")
    b.box((0,0,17.05),(5.97,5.97,.48),"slate")
    b.box((0,0,17.37),(5.66,5.66,.14),"trim")
    # A wound on only one roof face exposes the rafters of the crooked belfry.
    base=[(-2.75,-2.75,17.43),(2.75,-2.75,17.43),(2.75,2.75,17.43),(-2.75,2.75,17.43)]
    mid=[(-.96,-.83,21.26),(.79,-.76,20.78),(.62,.88,21.62),(-.91,.88,21.4)]
    for i in (1,2,3):
        j=(i+1)%4
        b.face([base[i],base[j],mid[j]],"roof");b.face([base[i],mid[j],mid[i]],"slate")
    for low,high in zip(base,mid):
        b.beam(low,high,.17,"wood")
    b.beam(base[0],(.34,-.74,20.49),.14,"wood")
    _spire(b,-.18,.12,20.89,.80,4.35,-.60)
    for x,y in ((-2.52,2.52),(2.52,2.52),(-2.52,-2.52)):
        _spire(b,x,y,17.27,.35,2.63,mat="slate")


def _kiln(b):
    b.box((-.48,.85,.34),(11.0,6.95,.68),"basalt")
    for x in (-3.47,2.50):
        b.box((x,.80,2.18),(.72,5.65,3.39),"basalt")
    b.box((-.5,3.40,2.78),(6.59,.77,4.78),"basalt")
    for y in (-1.66,-.65,.4,1.45,2.58):
        _arch(b,-.48,y,2.32,2.64,2.37,.56,.90,mat="stone",steps=10,pointed=False)
    _arch(b,-.48,-2.12,2.32,2.64,2.37,.59,.39,mat="trim",steps=12,pointed=False)
    for x in (-3.37,2.40):
        _column(b,x,-1.98,.42,2.05,.88,"basalt")
    b.box((-.48,-.22,.78),(4.7,2.85,.31),"iron")
    for i in range(6):
        x=-2.27+i*.67
        b.beam((x,-1.61,.98),(x,1.2,.92),.14,"iron")
    for i in range(5):
        x=-1.87+i*.64
        b.beam((x,.03,1.02),(x+.31,.45,1.20+(i%2)*.31),.15,"ember")
    # Three stacks at unlike heights, joined to the furnace by broad flue shoulders.
    for x,y,h,r in ((-2.85,2.25,13.1,1.18),(.29,2.42,16.15,1.03),(3.12,2.90,9.7,.92)):
        b.taper((x,y,.56),(x,y,4.7),r*1.72,r*1.07,"basalt",8)
        _chimney(b,x,y,3.12,h-3.12,r,True)
    for x in (-4.42,3.35):
        _buttress(b,x,1.16,.31,4.61,1.37,pi if x<0 else 0)
    for dx in (3.65,4.04,4.39):
        b.beam((dx,-1.68,.51),(dx+.26,-1.76,4.70),.10,"iron")
    b.box((4.10,-1.73,3.65),(1.14,.3,.14),"iron")
    for i in range(3):
        b.ellipsoid((-4.6+i*.43,-1.23+(i%2)*.52,.45),(.78,.70,.48+i*.16),"basalt",4,7)


def _village(b):
    # Each house has its own setting-out angle and floor height. Its stone course must follow both.
    houses=((-2.06,1.65,.31,5.55,6.65,5.20,3.70,-.13,True),
            (3.36,1.87,.23,3.65,5.34,4.04,3.26,.13,False),
            (-.73,-2.66,.20,3.96,3.34,3.12,2.63,.07,True))
    for x,y,floor,w,d,h,rise,angle,lit in houses:
        bottom=-.16; half=w*.5+.23
        _slab(b,x,y,bottom,[(-half,0),(half,0),(half,floor+.06-bottom),(-half,floor+.06-bottom)],
              d+.46,"basalt",angle)
        _house(b,x,y,floor,w,d,h,rise,angle,lit)
    _chimney(b,-3.65,2.86,.25,9.56,.52)
    _chimney(b,.25,-2.10,.25,6.20,.35)
    # A crooked fire-watch cupola makes the village distinct from the fortified court.
    x,y=2.08,3.04
    b.box((x,y,6.89),(1.76,1.66,.27),"slate")
    for sx in (-.63,.63):
        for sy in (-.61,.61):
            xx,yy=x+sx,y+sy
            # The cupola straddles a raked roof: the four curb posts have different heel cuts.
            roof_u=abs((xx-3.36)*cos(.13)+(yy-1.87)*sin(.13))
            heel=4.27+3.26*max(0,1-roof_u/(3.65*.54))-.24
            b.beam((xx,yy,heel),(xx,yy,8.49),.18,"wood")
    _spire(b,x,y,8.45,1.25,2.88,.17)
    b.beam((x+.17,y,11.66),(x+1.09,y,11.58),.075,"iron")


def _ring_xz(b,cx,cy,cz,r,width,depth,n,mat,missing=()):
    for i in range(n):
        if i in missing:
            continue
        a,c=TAU*i/n,TAU*(i+1)/n
        outline=[((r-width)*cos(a),(r-width)*sin(a)),
                 (r*cos(a),r*sin(a)),(r*cos(c),r*sin(c)),
                 ((r-width)*cos(c),(r-width)*sin(c))]
        _slab(b,cx,cy,cz,outline,depth,mat)


def _mill(b):
    b.box((.84,1.12,.33),(8.57,6.70,.66),"basalt")
    _house(b,.91,1.15,.61,6.61,5.98,6.52,4.01,0)
    _house(b,3.78,.91,.58,2.48,4.20,3.58,1.56,.05,False)
    _chimney(b,2.17,2.18,.68,10.94,.46)
    # Vertical wheel faces south, with independent rims, cross-paddles and through axle.
    cx,cy,cz=-2.26,-2.74,3.84
    rad=3.45
    for y in (cy-.62,cy+.61):
        # A snapped outer felloe and a missing paddle leave a bite on the upper right.
        missing=(2,3) if y<cy else (3,)
        _ring_xz(b,cx,y,cz,rad,.29,.18,18,"wood",missing)
        _ring_xz(b,cx,y,cz,rad-.28,.10,.20,18,"iron",missing)
        for n in range(10):
            a=TAU*n/10
            if n==2:
                b.beam((cx+.30*cos(a),y,cz+.30*sin(a)),
                       (cx+(rad-1.15)*cos(a+.035),y,cz+(rad-1.15)*sin(a+.035)),.13,"wood")
                continue
            b.beam((cx+.3*cos(a),y,cz+.3*sin(a)),
                   (cx+(rad-.24)*cos(a),y,cz+(rad-.24)*sin(a)),.16,"wood")
    for n in range(18):
        if n==3:
            continue
        a=TAU*n/18
        x,z=cx+rad*cos(a),cz+rad*sin(a)
        outline=[(-.07,-.32),(.11,-.31),(.08,.22),(-.15,.21)]
        ca,sa=cos(a),sin(a)
        pl=[(xx*ca-zz*sa,xx*sa+zz*ca) for xx,zz in outline]
        _slab(b,x,cy,z,pl,1.48,"slate" if n%6==0 else "wood")
    b.taper((cx,cy-1.06,cz),(cx,cy+1.23,cz),.30,.30,"iron",12)
    b.taper((cx,cy-.81,cz),(cx,cy+.71,cz),.51,.51,"wood",12)
    for xx in (cx-.75,cx+.74):
        # The wet A-frame stands on two individual masonry bearing shoes in the millrace.
        b.box((xx,-3.92,.09),(.50,.52,.42),"basalt")
        b.beam((xx,-3.92,.22),(cx,-3.92,cz-.20),.26,"wood")
    for y in (-4.51,-1.79):
        b.box((-2.31,y,.27),(9.65,.42,.54),"stone")
    b.box((cx,-3.14,.16),(8.54,2.13,.18),"water")
    # Hoist loft projects out of the gable. Its narrow, boarded aperture sits above
    # the wheel, framed by braces instead of leaving a broad triangular plane.
    for x in (-.92,2.78):
        b.beam((x,-1.97,7.16),(.94,-1.99,9.87),.20,"wood")
    b.beam((.94,-1.99,7.10),(.94,-1.99,10.76),.26,"wood")
    for zz,w in ((8.34,4.85),(9.22,3.08)):
        b.beam((.94-w/2,-1.97,zz),(.94+w/2,-1.97,zz),.19,"wood")
    _slab(b,.94,-2.19,7.73,[(-1.09,0),(1.09,0),(1.09,1.35),(0,2.34),(-1.09,1.35)],
          .56,"wood")
    _slab(b,.94,-2.50,7.74,[(-.48,0),(.53,0),(.53,1.33),(.36,1.52),(-.48,1.52)],.10,"dark")
    for i in range(4):
        left=.47+i*.18
        b.beam((left,-2.57,7.94),(left+.68,-2.57,9.09),.07,"wood")
    for x in (-.15,2.04):
        b.beam((x,-1.95,6.91),(x,-2.64,7.68),.18,"wood")
        b.beam((x,-2.59,7.68),(x,-2.59,9.03),.18,"wood")
    _roof(b,.94,-2.23,9.08,2.19,1.22,1.13,"roof")
    b.box((.94,-2.46,7.64),(2.55,1.07,.22),"slate")
    b.beam((.94,-2.55,9.57),(.94,-3.85,9.71),.16,"wood")
    b.taper((.94,-3.66,9.38),(.94,-3.91,9.38),.19,.19,"iron",8)
    b.beam((.95,-3.74,9.21),(.95,-3.75,8.37),.051,"iron")
    # The short, damaged headrace approaches from the left bank on squat timber trestles.
    for y in (-2.76,-2.08):
        b.beam((-9.14,y,5.00),(-5.34,y,4.47),.22,"wood",depth=.37)
    b.beam((-9.05,-2.43,4.84),(-5.30,-2.43,4.31),.63,"wood",depth=.16)
    for x,level in ((-8.55,4.87),(-6.44,4.58)):
        b.box((x,-2.42,.23),(1.02,1.34,.74),"basalt")
        for y in (-2.91,-1.91):
            b.taper((x,y,.42),(x,y,level),.21,.145,"wood",6)
        b.beam((x,-3.18,level-.19),(x,-1.65,level-.19),.23,"wood")
        b.beam((x-.03,-2.84,level*.47),(x+.10,-1.82,level-.28),.15,"wood")
    b.beam((-8.60,-2.97,1.18),(-6.45,-2.89,4.24),.18,"wood")
    # Bolts around the hub separate the visibly aged iron crossing from the spokes.
    for i in range(8):
        a=TAU*i/8+.12
        px,pz=cx+.395*cos(a),cz+.395*sin(a)
        b.taper((px,cy-.80,pz),(px,cy-.92,pz),.069,.058,"slate",5)
    _ivy(b,3.74,-2.01,1.42,2.03,.01,4)
    # Thin undershot sluice braces read even behind the wheel.
    for x in (-6.43,-5.05):
        b.beam((x,-1.75,.44),(x,-1.75,1.33),.16,"wood")
    for z in (.65,1.14):
        b.beam((-6.50,-1.75,z),(-4.97,-1.75,z+.08),.14,"wood")


def _granary(b):
    for x in (-3.13,0,3.13):
        for y in (-2.52,2.52):
            b.box((x,y,.35),(1.24,1.21,.70),"basalt")
            b.taper((x,y,.67),(x,y,2.75),.46,.35,"stone",6)
            b.box((x,y,2.86),(1.26,1.22,.23),"trim")
    b.box((0,0,3.03),(7.92,6.57,.34),"wood")
    for sx in (-1,1):
        b.beam((sx*3.1,-2.52,1.03),(sx*.3,-2.52,2.94),.19,"wood")
        b.beam((sx*3.09,2.51,1.0),(sx*.26,2.51,2.94),.19,"wood")
    _house(b,0,0,3.18,7.42,6.04,6.56,4.59,0,False)
    for xx in (-2.69,0,2.69):
        for j in range(4):
            b.box((xx,-3.107,6.79+j*.22),(1.03,.18,.094),"wood")
    for xx in (-3.0,3.0):
        b.beam((xx,-3.05,4.23),(xx,-4.03,5.57),.19,"wood")
    b.box((0,-3.49,5.56),(6.39,.90,.16),"wood")
    for xx in (-2.95,-.94,.91,2.95):
        b.box((xx,-3.92,6.05),(.11,.11,.96),"iron")
    b.beam((-3.0,-3.91,6.48),(3.0,-3.91,6.48),.095,"wood")
    # Raised brick corbies protect the gable; each course steps inward.
    for side in (-1,1):
        for k in range(6):
            xx=side*(3.6-.60*k)
            zz=9.85+.73*k
            b.box((xx,-3.10,zz),(.66,.50,.94),"basalt" if k%2 else "stone")
            b.box((xx,-3.10,zz+.49),(.76,.59,.13),"trim")
    _chimney(b,2.37,1.40,7.04,6.20,.43)


def _toll(b):
    # The gallows-shaped freight crane, weighing arm and suspended pans read from across the forest.
    b.box((-.76,1.04,.41),(10.02,6.70,.82),"basalt")
    _house(b,-2.31,1.52,.77,4.59,5.05,3.95,2.18,.02,False)
    for x in (.25,2.27):
        for y in (-1.16,1.10):
            _column(b,x,y,.64,.90,.76,"basalt")
            b.beam((x,y,1.35),(x-.21,y+.10,10.70),.51,"wood")
    for z in (3.58,6.45,9.40,10.57):
        for x in (.14,2.08):
            b.beam((x,-1.16,z),(x,1.13,z),.34,"wood")
        for y in (-1.16,1.13):
            b.beam((.14,y,z),(2.19,y,z),.34,"wood")
    for y in (-1.16,1.11):
        for lo,hi in ((1.51,3.48),(3.85,6.29),(6.69,9.27)):
            b.beam((.24,y,lo),(2.16,y,hi),.19,"wood")
            b.beam((2.16,y,lo),(.24,y,hi),.15,"wood")
    # A long, slightly leaning jib and its two steel stays span the open weighbridge.
    for y in (-.41,.41):
        b.beam((-.40,y,8.89),(5.62,y,12.93),.38,"wood")
        b.beam((1.02,y,10.80),(5.62,y,12.93),.085,"iron")
    b.beam((5.55,-.61,12.88),(5.55,.58,12.88),.22,"iron")
    b.taper((5.44,-.30,12.78),(5.44,.30,12.78),.33,.33,"wood",10)
    b.beam((5.44,0,12.57),(5.44,0,8.36),.064,"iron")
    b.beam((3.47,-.10,8.70),(7.24,.10,8.04),.17,"iron")
    for cx,cz,r in ((3.55,5.90,.85),(7.12,5.47,.99)):
        pivot=(cx,0,8.68 if cx<5.0 else 8.07)
        for i in range(3):
            a=TAU*i/3+.34
            b.beam(pivot,(cx+r*cos(a),r*sin(a),cz+.33),.043,"iron")
        _lathe(b,cx,0,[(cz,.26*r),(cz+.11,.58*r),(cz+.32,r),(cz+.38,r),
                        (cz+.33,.80*r)],["iron","iron","trim","iron"],12)
    for x in (-4.52,3.78):
        b.beam((x,-2.29,.72),(x,-2.26,4.44),.24,"wood")
    b.beam((-4.52,-2.29,4.34),(3.79,-2.29,4.03),.23,"wood")
    for x in (-3.35,-1.21):
        b.beam((x,-2.38,4.23),(x,-2.38,3.17),.045,"iron")
    b.box((-2.28,-2.38,3.12),(2.36,.15,.68),"wood")
    for i in range(3):
        b.taper((-4.5+i*.89,-1.93,.78),(-4.5+i*.89,-1.93,1.72),.36,.31,"wood",8)


def _crypt_crown(b):
    # Buried mausoleum with a blunt, sealed slab under a collar of splintered pinnacles.
    for z,w,h,mat in ((.31,6.0,.62,"basalt"),(.73,5.56,.23,"slate"),(.94,5.08,.23,"trim")):
        b.taper((0,0,z-h/2),(0,0,z+h/2),w,w,mat,8)
    b.taper((0,0,1.03),(0,0,4.21),4.64,4.32,"basalt",8)
    b.taper((0,0,4.19),(0,0,4.53),4.39,4.62,"trim",8)
    b.taper((0,0,4.51),(0,0,5.26),4.60,3.85,"slate",8)
    for i,h in enumerate((9.6,7.7,10.8,8.6,11.5,7.4,9.65,8.1)):
        a=TAU*i/8+pi/8
        x,y=4.32*cos(a),4.32*sin(a)
        _buttress(b,x,y,.5,h*.56,1.05,a)
        b.taper((x,y,4.45),(x*1.048,y*1.048,h*.73),.49,.30,"stone",5)
        b.taper((x*1.048,y*1.048,h*.73),(x*1.13,y*1.13,h),.30,.034,"slate",5)
    # The central lantern is open between the jambs under a broken rather than intact steeple.
    for x in (-1.33,1.33):
        for y in (-1.33,1.33):
            _column(b,x,y,5.03,2.41,.48,"slate")
    for s in (-1,1):
        _arch(b,0,s*1.33,7.08,1.08,1.45,.31,.47,mat="stone",steps=8)
        _arch(b,s*1.33,0,7.08,1.08,1.45,.31,.47,pi/2,mat="stone",steps=8)
    b.taper((0,0,8.56),(0,0,9.04),2.34,2.03,"slate",8)
    _spire(b,0,0,9.02,1.80,4.17,-.32,"slate")
    # Incised closed seal. This foreground motif reads as occluded, never as a passage.
    b.box((0,-4.37,2.25),(2.02,.30,2.66),"slate")
    _slab(b,0,-4.38,3.54,[(-1.01,-.05),(1.01,-.05),(0,1.22)],.29,"slate")
    _arch(b,0,-4.46,2.85,1.07,1.56,.34,.52,mat="trim",steps=8)
    for x in (-1.20,1.20):
        _column(b,x,-4.43,.77,2.08,.40)
    for x0,x1 in ((-.74,.74),(.74,-.74)):
        b.beam((x0,-4.57,1.66),(x1,-4.57,3.14),.12,"iron")
    b.ellipsoid((0,-4.67,2.43),(.25,.075,.25),"mineral",4,8)


def _rose(b,x,y,z,r):
    # Open wheel with eight narrow foils. The rosette stays perforated, including the centre.
    _ring_xz(b,x,y,z,r,.15,.41,24,"trim")
    _ring_xz(b,x,y,z,r*.34,.09,.37,16,"stone")
    for k in range(8):
        a=TAU*k/8
        p=lambda s,t: (x+s*r*cos(a)-t*r*sin(a),y,z+s*r*sin(a)+t*r*cos(a))
        b.beam(p(.31,0),p(.54,0),.095,"trim")
        for sign in (-1,1):
            p0=p(.49,0)
            for s,t in ((.62,.12*sign),(.80,.135*sign),(.94,0)):
                p1=p(s,t);b.beam(p0,p1,.080,"trim");p0=p1


def _abbey(b):
    # The front has two pierced lancets and a lace wheel in a genuinely open upper wall.
    b.box((0,.84,.34),(11.62,12.64,.68),"basalt")
    front=-4.95
    for x in (-4.75,0,4.75):
        _column(b,x,front,.62,5.62,.96 if x else .70)
    for x in (-2.38,2.38):
        _arch(b,x,front,4.91,1.99,3.64,.55,1.06,mat="trim",steps=12)
        _arch(b,x,front-.32,4.91,1.77,3.28,.13,.41,mat="stone",steps=10,detail=False)
        for s in (-1,1):
            b.beam((x+s*1.76,front-.33,.71),(x+s*1.76,front-.33,4.98),.14,"trim")
    # Side spandrels climb up the arch haunches without closing the wheel or lancets.
    for s in (-1,1):
        for lev in range(4):
            u=4.43-.30*lev
            _slab(b,0,front,7.65+lev*.60,
                  [(s*2.70,0),(s*u,0),(s*u,.60),(s*(2.64-.08*lev),.60)],.84,
                  "stone" if lev%2 else "slate")
        _buttress(b,s*4.53,front,.35,11.22,1.15,pi if s<0 else 0)
    _rose(b,0,front,10.90,2.18)
    _arch(b,0,front,9.98,2.61,3.49,.47,.83,mat="stone",steps=14)
    for s in (-1,1):
        _column(b,s*2.61,front,8.27,1.75,.46)
        b.beam((s*4.84,front,9.53),(s*.10,front,14.58),.29,"trim")
    # Stepped loose stones on surviving western gable slope.
    for i in range(6):
        b.box((-4.52+i*.47,front,10.03+i*.48),(.55,.85,.58),"stone")
    for side in (-1,1):
        x=side*4.88
        for j,y in enumerate((-2.75,1.43,5.61)):
            h=(5.38,4.70,3.42)[j] if side>0 else (5.82,6.54,5.55)[j]
            _column(b,x,y,.62,h,.78)
            _buttress(b,x,y,.40,h*.9,1.06,0 if side>0 else pi)
        for j,y in enumerate((-0.65,3.51)):
            missing=(0,1,2,3) if side==1 and j==1 else ()
            _arch(b,x,y,4.85,1.72,2.68,.44,.81,pi/2,mat="stone",steps=10,broken=missing)
    # The nave has lost its roof. Lateral flying arches still describe the height of the old vault.
    # Each is an incomplete half-arch with a steep weathering along the top of the flyer.
    for side,v,top in ((-1,-1.65,7.46),(-1,3.40,7.03),(1,.21,6.32)):
        x=side*6.96
        # The three exterior flyers fall outside the nave platform. Each has a pad of its own.
        b.taper((x,v,-.14),(x,v,.60),.81,.63,"basalt",8)
        _column(b,x,v,.52,3.98,.77,"basalt")
        _spire(b,x,v,4.42,.49,1.26,side*.09,"slate")
        axis=0 if side<0 else pi
        _slab(b,x,v,0,[(0,3.45),(.32,3.68),(.70,4.52),(1.26,5.30),(2.14,top-.33),
                          (2.14,top),(0,4.45)],.58,"stone",axis)
        a=_plane(x,v,.01,0,4.47,axis);c=_plane(x,v,2.08,0,top,axis)
        b.beam(a,c,.12,"trim",depth=.66)
    _ivy(b,-3.73,front-.57,3.72,3.4,.02,4)
    _ivy(b,4.40,front-.48,7.10,1.71,-.06,5)
    # One failed tie is still attached to a corbel and drops across the empty north-west aisle.
    for a,c in zip(((-4.78,.41,5.84),(-4.45,.05,4.28),(-4.13,-.22,3.83)),
                   ((-4.45,.05,4.28),(-4.13,-.22,3.83),(-3.77,-.86,3.70))):
        b.beam(a,c,.069,"iron")
    # One lesser tower survived. Its sibling has a broken cap and iron in the fracture.
    _column(b,-4.87,front+.22,8.58,4.84,1.19)
    _spire(b,-4.87,front+.22,13.31,1.0,4.35,-.20)
    _column(b,4.82,front+.18,8.54,2.25,1.13)
    b.beam((4.57,front,10.67),(4.33,front-.10,11.64),.072,"iron")
    for xx in (-4.74,4.75):
        b.beam((xx,1.43,6.72),(xx*.52,1.64,8.63),.21,"wood")
    for z,w in ((.76,7.48),(.54,8.40),(.31,9.15)):
        top=z+.10; bottom=-.13
        b.box((0,front-1.04-(.76-z)*1.18,(top+bottom)*.5),(w,1.19,top-bottom),"stone")


def _aqueduct(b):
    # Three complete bays and a fourth broken arch end; water trough is continuous over the first three.
    span=4.12
    for j in range(5):
        x=(j-2)*span
        h=5.72 if j<4 else 3.28
        b.box((x,0,.35),(1.69,2.51,.7),"basalt")
        _slab(b,x,0,.63,[(-.74,0),(.74,0),(.53,h),(-.53,h)],2.02,"stone")
        for zz in (1.39,3.12,5.15):
            if zz<h+.3:
                b.box((x,0,zz),(1.44,2.18,.20),"trim" if zz>5 else "slate")
        if j%2==0:
            _buttress(b,x,-.84,.45,min(h,4.9),.91,-pi/2)
    for j in range(4):
        x=(j-1.5)*span
        broken=(5,6,7,8,9) if j==3 else ()
        _arch(b,x,0,5.62,1.59,1.92,.53,1.97,mat="stone",steps=10,pointed=False,broken=broken)
        if j==3:
            continue
        # Spandrels follow the outer curve. The fabric from arch crown to channel is solid masonry.
        pts=_archpath(2.12,2.45,10,False)
        for (xa,ha),(xb,hb) in zip(pts,pts[1:]):
            roof=2.74
            _slab(b,x,0,5.62,[(xa,ha),(xb,hb),(xb,roof),(xa,roof)],1.93,"stone")
        b.box((x,0,8.47),(span+.02,2.34,.23),"trim")
        for y in (-.94,.94):
            b.box((x,y,8.89),(span,.31,.63),"basalt")
            b.box((x,y,9.26),(span+.05,.43,.15),"stone")
        b.box((x,0,8.65),(span,1.52,.08),"water")
        for i in (1,3):
            px=x-1.38+i*.54
            b.beam((px,-1.001,7.90),(px+.05,-1.02,7.35-i*.10),.054,"moss")
    # Raised inspection turret and a crooked sluice gate distinguish this from an ordinary bridge.
    x=-6.77
    for xx in (x-.57,x+.60):
        for y in (-.71,.71):
            b.beam((xx,y,8.62),(xx,y,10.32),.15,"wood")
    _roof(b,x,0,10.3,1.55,1.72,1.05,"slate")
    b.beam((5.93,-.59,7.91),(6.77,-.67,8.71),.13,"iron")


def _road_shrine(b):
    for i in range(4):
        w=5.49-i*.57
        top=.35+i*.35; bottom=-.08 if i==0 else .35*i-.018
        b.box((0,.25,(top+bottom)*.5),(w,w*.79,top-bottom),"basalt" if i%2==0 else "stone")
    b.box((0,.22,1.68),(3.36,2.46,.58),"slate")
    for x in (-1.27,1.27):
        for y in (-.64,1.08):
            _column(b,x,y,1.93,2.84,.44)
    for y in (-.65,1.08):
        _arch(b,0,y,4.50,1.07,1.57,.27,.43,mat="trim",steps=8)
    for x in (-1.27,1.27):
        _arch(b,x,.22,4.53,.67,1.06,.24,.42,pi/2,mat="stone",steps=8)
    _roof(b,0,.20,5.63,3.16,2.62,1.62,"slate")
    _spire(b,0,.20,7.33,.31,1.48,.05,"slate")
    # A worn standing effigy holds a hood over the censer, leaving a dark pocket below it.
    b.taper((0,.25,1.92),(0,.29,3.72),.47,.26,"slate",7)
    b.ellipsoid((.01,.27,3.83),(.34,.28,.40),"stone",5,8)
    b.ellipsoid((.01,.045,3.80),(.22,.075,.22),"dark",4,8)
    b.beam((-.29,.22,3.31),(-.44,-.23,3.05),.18,"slate")
    b.beam((.27,.21,3.34),(.42,-.24,3.11),.18,"slate")
    _lathe(b,0,-.39,[(2.70,.11),(2.92,.35),(3.05,.39),(3.09,.29)],
           ["iron","iron","trim"],10)
    b.ellipsoid((0,-.39,3.08),(.15,.14,.067),"ember",4,8)
    # Bent processional lamp on the left provides the recognisable road fork silhouette.
    p=[(-2.09,-.33,.09),(-2.09,-.31,4.42),(-1.96,-.29,5.10),(-1.69,-.26,5.33),(-1.37,-.26,5.14)]
    for a,c in zip(p,p[1:]):
        b.beam(a,c,.095,"iron")
    b.taper((-1.37,-.27,4.62),(-1.37,-.27,5.13),.12,.035,"iron",6)
    b.ellipsoid((-1.36,-.27,4.68),(.088,.088,.12),"amber",4,7)
    for i in range(3):
        x=1.73+i*.31
        tread=(1.40,1.05,.70)[i]
        b.box((x,-.87,tread+.153),(.14,.13,.33),"bone")


_MODELS = {"bell": _bell, "kiln": _kiln, "village": _village, "mill": _mill,
           "granary": _granary, "toll": _toll, "crypt_crown": _crypt_crown,
           "abbey": _abbey, "aqueduct": _aqueduct, "road_shrine": _road_shrine}


def draw(b,kind):
    """Add one map landmark to the supplied builder. Geometry is in metres."""
    _MODELS[kind](b)
