"""Low edge dressing for four Ashen Crypt vistas. +Y is north; models face -Y."""
from math import cos, hypot, pi, sin, sqrt
from architecture import _plane, _slab

TAU=2*pi


def _stone(b,x,y,z,w,d,h,key,mat="stone"):
    # Two offset, split-cleft rings avoid the regular outline of placed cobbles.
    pts=[];top=[]
    for k in range(6):
        a=TAU*k/6+key*.46
        r=.89+.13*sin(k*4.13+key)
        dx,dy=w*.52*cos(a)*r,d*.54*sin(a)*r
        pts.append((x+dx,y+dy,z+.05*h*cos(k*2+key)))
        top.append((x+dx*(.73+.19*cos(k*1.6+key))+.07*w,
                    y+dy*.83-.04*d,z+h*(.77+.18*sin(k*1.7+key))))
    b.face(pts[::-1],mat)
    for i in range(6):
        j=(i+1)%6
        b.face([pts[i],pts[j],top[j]],mat)
        b.face([pts[i],top[j],top[i]],mat)
        b.face([top[i],top[j],(x+.1*w,y-.12*d,z+h)],"slate" if i==1 and mat=="basalt" else mat)


def _fern(b,x,y,z,h,seed,fronds=5):
    """Split fronds and a little ash above the lowest midrib."""
    for j in range(fronds):
        a=j*TAU/fronds+.37*seed+.23*sin(j*2+seed)
        dx,dy=cos(a),sin(a)
        width=1.0+.22*sin(j*2.14+seed)
        def blade(t,side):
            hx=h*width*(.09+.90*t**1.16)
            return (x+dx*hx-dy*side,y+dy*hx+dx*side,z+h*(.16+.64*sin(t*pi*.83)))
        p0=(x,y,z);p1=blade(.52,0);p2=blade(1,0)
        b.face([p0,blade(.48,-h*.035),p1],"bark")
        b.face([p1,blade(.48,-h*.035),p2],"moss")
        for k in range(1,5):
            t=k*.16
            for s in (-1,1):
                w=s*h*(.19*(1-.70*t))
                b.face([blade(t,0),blade(t+.22,w),blade(t+.13,s*h*.036)],
                       "moss" if (k+seed)%3==1 else "leaf")


def _broken_wall(b):
    # A ruin low enough to see over; no central opening to confuse it with the actual gate.
    knots=[(-10.0,.28),(-9.1,.51),(-7.8,.64),(-7.03,1.31),(-5.87,1.22),(-5.05,.89),
           (-4.24,1.45),(-3.72,2.41),(-3.12,2.58),(-2.21,2.56),(-1.63,3.07),
           (-.54,3.37),(.22,3.30),(.32,2.92),(1.42,2.92),(1.46,3.58),
           (2.26,3.57),(2.44,2.91),(3.19,2.88),(3.50,2.49),(4.43,2.37),
           (4.75,1.92),(5.34,1.94),(5.62,1.50),(6.25,1.43),(6.98,.98),
           (7.75,.94),(8.64,.56),(9.42,.37)]
    for j,((a,ha),(c,hc)) in enumerate(zip(knots,knots[1:])):
        y=.17*sin((a+c)*.30); d=.91+.12*sin(j*1.62)
        _slab(b,0,y,-.16,[(a,0),(c,0),(c,hc+.16),(a,ha+.16)],d,"stone" if j%4 else "slate")
        if abs(ha-hc)<.40 and (j%4!=1):
            b.beam((a,y,ha+.035),(c,y,hc+.035),d*1.14,"slate" if j%3 else "trim",depth=.12)
    for a,h in ((-5.33,1.35),(-1.19,3.54),(3.31,2.96),(6.34,1.58)):
        _slab(b,a,-.35,-.08,[(-.47,0),(.47,0),(.26,h-.22),(.20,h),(-.22,h-.06)],
              1.0,"basalt")
        b.box((a,-.63,h*.53),(.70,1.09,.13),"slate")
    # The former parapet on the western side carries a bent iron palisade.
    posts=[(-8.70,.60,1.79),(-8.02,.67,1.96),(-7.36,1.07,2.32),(-6.71,1.32,2.60),
           (-6.05,1.31,2.61),(-5.39,1.16,2.36),(-4.74,1.04,2.00),(-4.12,1.65,2.47)]
    ends=[]
    for i,(x,base,high) in enumerate(posts):
        lean=.24 if i==6 else -.11 if i==5 else .045*sin(i)
        x2=x+lean; y=.15*cos(x*.45)-.11
        b.taper((x,y,base),(x2,y-.05,high),.064,.041,"iron",5)
        _slab(b,x2,y-.05,high-.06,[(-.02,0),(-.13,.10),(0,.36),(.12,.11),(.024,0)],.045,"iron")
        ends.append((x2,y-.04,high))
    for a,c in zip(ends,ends[1:]):
        for dz in (.29,.64):
            b.beam((a[0],a[1],a[2]-dz),(c[0],c[1],c[2]-dz),.079,"iron")
    # Grave markers tilt toward the old ditch instead of forming an orderly row.
    for j,(x,y,h,w,lean) in enumerate(((-6.98,-1.79,1.14,.65,-.14),(-4.54,-2.05,1.74,.86,.16),
                     (-1.90,-1.76,1.40,.73,-.11),(.35,-2.21,1.13,.59,.15),(3.04,-1.52,1.69,.71,-.20),
                     (5.62,-1.68,.87,.63,.21),(7.45,-1.01,.72,.48,-.22))):
        _stone(b,x,y,-.06,w*1.59,.84,.28,j+2,"basalt")
        pts=[(-w*.47,0),(w*.46,0),(w*.47+lean,h*.77),(w*.40+lean,h*.93),
             (lean+w*.04,h),(lean-w*.33,h*.94),(lean-w*.45,h*.82)]
        _slab(b,x,y,.15,pts,.20+.07*(j%2),"basalt" if j%4==0 else "slate",-.12+.12*(j%3))
        if j in (1,2,4):
            b.beam((x+w*-.24+lean*.6,y-.15,h*.72),(x+w*.24+lean*.6,y-.15,h*.73),.067,"trim")
    for j,(x,y) in enumerate(((-7.48,-.75),(-3.52,-1.18),(1.89,-1.78),(6.90,-.54))):
        _stone(b,x,y,-.11,1.30-j*.09,.81,.38+.07*j,j,"stone")
    _fern(b,-3.80,-1.55,0,.69,2,4)
    _fern(b,4.55,-.83,0,.53,5,4)


def _bank(b):
    # Weathered retaining works wrap around an eroded reach, not a bridge across it.
    coords=[(-9.30,-.54),(-7.35,-.55),(-5.51,-.72),(-3.80,-.46),(-2.36,.05),
            (-.93,.09),(.81,.64),(2.64,.89),(4.48,.74),(6.23,.78),(8.34,1.04)]
    for i,(x,y) in enumerate(coords):
        h=1.02+.59*(1+sin(1.8*i+.8))*.5
        lean=.12*sin(i*1.6)
        b.taper((x,y,-.21),(x+lean,y-.12,h),.205,.157,"wood",7)
        _slab(b,x+lean,y-.11,h-.07,[(-.13,0),(.13,0),(.05,.22),(-.04,.10)],.18,"wood")
        if i not in (4,7):
            nx,ny=coords[min(i+1,len(coords)-1)]
            if nx==x: continue
            for p in range(2 if i!=5 else 1):
                zz=.28+p*.32+.10*cos(i)
                b.beam((x,y+.11,zz),(nx,ny+.11,zz+.14*sin(i+p)),.24,"wood",depth=.30)
    for j,(x,y,w,h) in enumerate(((-9.1,-.28,3.77,.78),(-7.15,.52,3.71,1.16),(-5.30,.58,3.45,1.35),
                (-3.15,.85,3.67,1.51),(-.89,1.19,3.11,.90),(1.44,1.41,3.65,.94),
                (4.15,1.57,3.77,.98),(6.73,1.47,3.65,1.03),(8.48,1.47,3.15,.68))):
        _stone(b,x,y,-.27,w,2.45,h,j,"basalt" if j%3 else "slate")
    # Decayed trestle that once supported a millrace flume on the high bank.
    for x,z in ((-4.99,3.32),(-2.57,3.47),(.19,3.11)):
        for y in (.94,2.10):
            b.taper((x+(y-1.52)*.25,y,-.05),(x,y* .90+.17,z),.18,.13,"wood",6)
        b.beam((x,.65,z),(x,2.40,z+.04),.22,"wood",depth=.37)
    b.beam((-5.20,.85,3.49),(-2.22,.95,3.62),.22,"wood",depth=.36)
    b.beam((-5.38,2.21,3.49),(.52,2.31,3.38),.24,"wood",depth=.44)
    b.beam((-2.59,.93,3.56),(-1.24,1.13,2.70),.14,"wood")
    b.beam((-5.09,1.01,1.02),(-2.60,1.02,3.23),.19,"wood")
    b.beam((-.03,1.02,1.18),(-2.51,.96,3.31),.16,"wood")
    # Last isolated rail and the tail of a mineral-water stain.
    b.taper((6.91,-1.38,-.1),(7.22,-1.11,.93),.17,.15,"wood",6)
    for j,(x,y) in enumerate(((-8.05,.80),(-6.55,1.74),(-1.59,1.85),(2.18,2.11),(5.82,1.95),(7.81,2.04))):
        _fern(b,x,y,.41,.72+.13*(j%3),j+1,4)
    for j in range(4):
        x=-7.92+j*3.98
        for k in range(5):
            t=TAU*k/5+j*.67; stem=1.23+.41*sin(2*k+j)
            a=(x+.31*cos(t),-.90+.38*sin(t),-.08)
            tip=(a[0]+.31*cos(t),a[1]+.21*sin(t),stem)
            b.face([(a[0]-.055,a[1],a[2]),(a[0]+.07,a[1],a[2]),tip],"moss")
            if k%2==0:
                b.taper((tip[0]-.055*cos(t),tip[1]-.035*sin(t),stem-.27),
                        tip,.065,.048,"bark",5)


def _twig(b,points,radii,mat,sides=5):
    """Uncapped branch junctions share continuous rings; a cap is only useful at a dead end."""
    rings=[]
    for i,p in enumerate(points):
        a=points[max(0,i-1)];c=points[min(len(points)-1,i+1)]
        dx,dy,dz=[c[k]-a[k] for k in range(3)]
        norm=sqrt(dx*dx+dy*dy+dz*dz); dx,dy,dz=dx/norm,dy/norm,dz/norm
        # All wind-shaped limbs keep an upward or side component; this basis stays stable at the crown.
        length=hypot(dz,dx)
        if length<1e-7: u=(1,0,0)
        else: u=(dz/length,0,-dx/length)
        v=(dy*u[2]-dz*u[1],dz*u[0]-dx*u[2],dx*u[1]-dy*u[0])
        rings.append([tuple(p[k]+radii[i]*(cos(TAU*j/sides)*u[k]+sin(TAU*j/sides)*v[k]) for k in range(3))
                      for j in range(sides)])
    for lo,hi in zip(rings,rings[1:]):
        for j in range(sides):
            n=(j+1)%sides
            b.face([lo[j],lo[n],hi[n],hi[j]],mat)
    b.face(rings[-1],mat)


def _leaf_spray(b,p,phi,size,tone):
    for j in range(5):
        a=phi-.83+j*.40
        s=size*(.82+.16*sin(j*2.91))
        c=(p[0]+cos(a)*.16*s,p[1]+sin(a)*.16*s,p[2]+.1*s)
        d=(p[0]+cos(a)*s,p[1]+sin(a)*s,p[2]+(.11+.20*sin(a))*s)
        r=(p[0]+.59*s*cos(a)-.18*s*sin(a),p[1]+.59*s*sin(a)+.18*s*cos(a),p[2]+.22*s)
        l=(p[0]+.59*s*cos(a)+.15*s*sin(a),p[1]+.59*s*sin(a)-.15*s*cos(a),p[2]+.07*s)
        b.face([c,r,d,l],tone if j%3 else "moss")


def _tree(b,x,y,h,scale,phase):
    def world(u,v,z):
        return (x+u*scale,y+v*scale,z*scale)
    trunk=[world(-.08,0,-.12),world(-.26,.10,h*.21),world(.06,-.08,h*.43),
           world(.60,.15,h*.65),world(1.26,-.05,h*.83),world(2.27,.28,h)]
    _twig(b,trunk,[scale*r for r in (.62,.44,.32,.22,.12,.036)],"bark",7)
    for k in range(5):
        a=TAU*k/5+phase;end=1.2+.55*(1+sin(phase+k))
        _twig(b,[world(-.02,0,.54),world(.61*cos(a),.66*sin(a),.18),
                world(end*cos(a),end*sin(a),-.06)], [s*scale for s in (.22,.15,.035)],"bark",5)
    # Irregular main limbs all catch the prevailing wind at the tips. Distal twigs are finer than their parents.
    for j in range(6):
        a=phase+j*2.39
        t=(.35,.44,.53,.61,.72,.81)[j]
        base=(.03+max(0,t-.4)*2.66, .13*sin(2*j), h*t)
        spread=(3.25-1.45*t)*(.89+.20*cos(j*1.77+phase))
        dx,dy=cos(a)*spread,sin(a)*spread
        tip=(base[0]+dx+1.05,base[1]+dy,base[2]+(1.55+.63*sin(a))*h*.31)
        joint=(base[0]+dx*.81+.02,base[1]+dy*.85-.08,base[2]+h*.11)
        sweep=(base[0]+dx*.98+.39,base[1]+dy*.98+.07,base[2]+(tip[2]-base[2])*.72)
        radius=.195*(1.19-t*.43)
        _twig(b,[world(*base),world(*joint),world(*sweep),world(*tip)],
              [s*scale for s in (radius,radius*.66,radius*.33,.027)],"bark",5)
        for k,point in enumerate((joint,sweep)):
            side=1 if (j+k)%2 else -1
            aa=a+side*(.80+.23*sin(phase+j))
            l=1.22 if k==0 else .85
            q=(point[0]+l*cos(aa)+.21,point[1]+l*sin(aa),point[2]+.58)
            r=(q[0]+.56*cos(aa)+.33,q[1]+.69*sin(aa),q[2]+.72+.31*cos(j))
            _twig(b,[world(*point),world(*q),world(*r)], [.081*scale,.040*scale,.011*scale],"bark",5)
            if (j+2*k)%3==0:
                _leaf_spray(b,world(*r),aa,scale*.70,"leaf")
            if k==0 and j%2==0:
                u=(q[0]+.82*cos(aa-.86),q[1]+.72*sin(aa-.86),q[2]+.82)
                _twig(b,[world(*q),world(*u)],[.036*scale,.009*scale],"bark",5)
        if j%2:
            _leaf_spray(b,world(*tip),a,scale*.81,"leaf")


def _forest(b):
    _tree(b,-4.72,.91,6.84,1.05,.38)
    _tree(b,3.23,1.67,6.20,.88,1.74)
    # A shorter, lightning-struck bole gives an irregular lower break at the edge.
    for k,(x,y,z) in enumerate(((6.86,-.80,3.75),(-7.62,-.12,2.05))):
        _twig(b,[(x,y,-.12),(x-.21,y+.02,z*.49),(x+.13,y+.14,z*.85),(x+.29,y+.10,z)],
              [.30,.216,.118,.047],"bark",6)
        _twig(b,[(x-.20,y+.01,z*.49),(x-.71,y+.23,z*.69),(x-.94,y+.31,z*.82)],
              [.126,.059,.015],"bark",5)
    for j,(x,y) in enumerate(((-7.1,-.73),(-3.8,-1.46),(-2.8,.04),(.72,-.57),(2.41,-.66),
                               (4.29,-1.11),(6.03,-.41))):
        _fern(b,x,y,0,.76+.13*(j%3),j+2,4)
    for j,(x,y) in enumerate(((-6.70,.12),(-1.70,-.31),(.25,.61),(5.58,.48))):
        _stone(b,x,y,-.17,1.59,.90,.65,j,"basalt" if j%2 else "slate")


def _hoop(b,x,y,z,r,width,mat,vertical=False,gap=False):
    """A narrow forged band, not a disk. In vertical mode its axle runs north/south."""
    segments=10;w=width*.5
    for i in range(segments):
        if gap and i in (2,3): continue
        e,f=TAU*i/segments,TAU*(i+1)/segments
        def v(a,edge,side):
            rr=r+w*edge
            return ((x+rr*cos(a),y+w*.62*side,z+rr*sin(a)) if vertical
                    else (x+rr*cos(a),y+rr*sin(a),z+w*.62*side))
        for side in (-1,1):
            b.face([v(e,-1,side),v(e,1,side),v(f,1,side),v(f,-1,side)],mat)
        b.face([v(e,1,-1),v(f,1,-1),v(f,1,1),v(e,1,1)],mat)


def _freight(b):
    # Mismatched slab sizes and very low deck retain the horizontal hierarchy of the gantry beyond.
    for j,(x,y,w,d,h) in enumerate(((-7.74,.15,2.38,3.42,.76),(-5.51,.18,2.59,3.76,.93),
                     (-3.20,.30,2.31,3.91,1.02),(-1.16,.42,2.30,4.01,1.10),
                     (1.02,.51,2.31,4.12,1.03),(3.23,.72,2.47,3.89,.88))):
        _slab(b,x,y,-.11,[(-w*.56,0),(w*.54,0),(w*.49,h),
                           (w*.33,h+.09),(-w*.51,h+.03)],d,"basalt" if j%3 else "stone")
        if j in (1,4):
            b.box((x,y-d*.52,h-.10),(w*.8,.25,.19),"slate")
    # Stripped timber ends protrude at different distances from the sleepers.
    for x in (-6.13,-3.52):
        b.box((x,.22,.97),(.30,2.03,.30),"wood")
    for tier in range(3):
        for j in range(3-tier):
            y=-.39+(j+.43*tier)*.62
            z=1.22+tier*.43
            a=(-7.65+.24*sin(1.17*j+tier),y,z)
            c=(-2.71+.28*cos(1.36*j+tier),y+.08*sin(j+2*tier),z+.07*cos(j+tier))
            b.taper(a,c,.31,.26,"bark",7)
            b.taper((a[0]-.034,a[1],a[2]),(a[0]+.015,a[1],a[2]),.24,.24,"wood",7)
    # Park the complete axle on the low yard next to the dock. Both wheel rims sit
    # in the same rut instead of one wheel hanging while the other enters the quay wall.
    axle_x,axle_y,axle_z=6.04,-.75,.978
    def cart(x,y,z):
        return (axle_x+x,axle_y+y,axle_z+z)
    b.taper(cart(0,-1.11,0),cart(0,1.09,0),.12,.12,"iron",8)
    for y in (-1.02,1.03):
        full=y>0
        px,py,pz=cart(0,y,0)
        _hoop(b,px,py,pz,.97,.14,"wood",True,not full)
        for j in range(6):
            if not full and j==1: continue
            angle=TAU*j/6
            b.beam(cart(.08*cos(angle),y,.08*sin(angle)),
                   cart(.88*cos(angle),y,.88*sin(angle)),.11,"wood")
        b.taper(cart(0,y-.17,0),cart(0,y+.16,0),.19,.19,"iron",7)
        b.beam(cart(-.18,y*.84,-.04),cart(.22,y*.84,.32),.16,"iron")
    b.beam(cart(-1.08,-.89,.23),cart(1.53,-.84,.11),.22,"wood")
    b.beam(cart(-1.04,.88,.24),cart(1.49,.90,.18),.22,"wood")
    for j in range(4):
        y=-.65+j*.44
        b.beam(cart(-1.55,y,.43),cart(1.55,y+.05,.31),.36,"wood",depth=.12)
    for y in (-.74,.85):
        for j in range(2):
            if y<0 and j==1: continue
            b.beam(cart(-1.43,y,.71+j*.31),cart(1.40,y+.13,.59+j*.27),.13,"wood",depth=.22)
        for x in (-1.36,1.05):
            b.beam(cart(x,y,.28),cart(x-.05,y-.09,1.18),.13,"wood")
    b.beam(cart(1.21,-.57,.22),cart(3.66,-.47,-.22),.20,"wood")
    b.beam(cart(1.24,.62,.26),cart(2.23,.61,.01),.17,"wood")
    # Abandoned lean-to with two missing sheets at one end, showing its purlins.
    for x,y,h in ((-1.62,.05,3.39),(1.13,.15,3.19),(-1.60,2.07,4.54),(1.04,2.21,4.21)):
        b.taper((x,y,.85),(x+.09,y-.09,h),.16,.12,"wood",6)
    for y,z in ((0,3.36),(2.16,4.43)):
        b.beam((-1.78,y,z),(1.31,y+.12,z-.20),.22,"wood")
    for j in range(6):
        x=-1.76+.57*j
        b.beam((x,-.17,3.34-.07*j),(x+.05,2.29,4.54-.07*j),.15,"wood")
        if j in (2,4): continue
        a=(x-.21,-.25,3.36-.07*j);c=(x+.28,-.25,3.34-.07*j)
        b.face([a,c,(x+.33,2.34,4.59-.07*j),(x-.18,2.34,4.60-.07*j)],"roof" if j%3 else "wood")
    b.beam((-1.61,2.03,1.19),(1.15,2.11,3.98),.16,"wood")
    _hoop(b,-2.13,-.99,1.32,.38,.075,"iron",True)
    # Two windings plus a tail retain the coiled rope silhouette without dozens of little links.
    points=[]
    for i in range(23):
        a=TAU*i/11
        r=.24+.0135*i
        points.append((1.43+r*cos(a),-1.15+r*.69*sin(a),1.06+.028*sin(a)))
    _twig(b,points,[.055]*len(points),"wood",5)
    _twig(b,[points[-1],(2.17,-1.15,1.06),(2.38,-1.43,.94),(2.51,-1.73,.58)],
          [.06,.052,.049,.035],"wood",5)
    _fern(b,7.56,.15,0,.68,4,4)


_MODELS={"court_wall":_broken_wall, "mill_bank":_bank, "forest_margin":_forest,
         "freight_margin":_freight}


def draw(b,kind):
    """Add one low, directionally staged settlement or woodland margin in metres."""
    _MODELS[kind](b)
