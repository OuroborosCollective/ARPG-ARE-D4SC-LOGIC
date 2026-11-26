"""South-facing fragments which close the sides of the underground vistas."""
from math import cos, pi, sin

from underworld import _crystal, _ellipse, _prism, _rock, _tube


def _block(b, x, y, z, w, depth, height, turn, mat):
    """A dressed ashlar with three clipped corners and a slightly pitched top."""
    w *= .5
    poly = [(x-w+.11, z), (x+w-.13, z), (x+w, z+.09),
            (x+w-.13, z+height+turn), (x-w+.13, z+height-turn*.45),
            (x-w,z+height-.14)]
    _prism(b,poly,y,depth,mat)


def _long_bone(b, x, y, z, length, bearing, pitch):
    """Flared, asymmetric bone ends and a narrow shaft in a single shell."""
    ax,ay=cos(bearing),sin(bearing)
    rings=[]
    for t,w,h,tilt in ((0,.13,.105,.02),(.23,.066,.060,-.015),
                       (.75,.056,.046,.052),(.91,.14,.116,0),(1,.12,.081,-.014)):
        xx=x+ax*length*t; yy=y+ay*length*t; zz=z+pitch*t+tilt
        rings.append([(xx+ay*w,yy-ax*w,zz),(xx,yy,zz+h),
                      (xx-ay*w,yy+ax*w,zz),(xx,yy,zz-h)])
    b.face(rings[0], "bone")
    for left,right in zip(rings,rings[1:]):
        for i in range(4):
            j=(i+1)%4
            b.face([left[i],right[i],right[j],left[j]], "bone")
    b.face(rings[-1][::-1], "bone")


def _skull(b,x,y,z,r):
    b.ellipsoid((x,y,z),(.94*r,.78*r,r),"bone",3,7)
    # Eye sockets and an angular nasal hollow, shown against the light upper face.
    for s in (-1,1):
        b.face([(x+s*.69*r,y-.55*r,z+.18*r),(x+s*.25*r,y-.79*r,z+.04*r),
                (x+s*.31*r,y-.77*r,z-.28*r),(x+s*.68*r,y-.57*r,z-.19*r)],"dark")
    b.face([(x,y-.75*r,z-.15*r),(x-.15*r,y-.69*r,z-.47*r),
            (x+.12*r,y-.69*r,z-.47*r)],"dark")


def _ossuary(b):
    xs=(-5.75,-3.03,-.31,2.43,5.17)
    for j,x in enumerate(xs):
        y=-.25+.047*x*x+.14*sin(1.3*j)
        sill=.67+.10*(j%2)
        spring=2.38+.2*sin(j)
        w=.99; thick=.25
        tip=spring+1.10
        # All niches terminate against solid recessed stones.
        _prism(b,[(x-w,sill),(x+w,sill),(x+w,spring),
                  (x+.53,tip-.38),(x,tip),(x-.53,tip-.38),(x-w,spring)],
               y+.58,.76,"basalt")
        _block(b,x,y-.53,0,2.62,1.92,sill+.06,.045,"slate")
        for s in (-1,1):
            b.box((x+s*1.15,y+.06,.5*(spring+sill)),
                  (.31,1.30,spring-sill),"stone")
            b.box((x+s*1.15,y+.02,spring-.08),(.47,1.48,.18),"trim")
            # Three tapered voussoirs meet at the pointed cap of each side.
            inn=[(w,0),(.88,.39),(.57,.76),(0,1.10)]
            out=[(w+thick,0),(1.13,.49),(.76,1.00),(0,1.44)]
            for k in range(3):
                outline=[(x+s*a,spring+h) for a,h in
                         (inn[k],inn[k+1],out[k+1],out[k])]
                face_y=y-.55-.033*(k%2)
                front=[(u,face_y,v) for u,v in outline]
                back=[(u,face_y+.84,v) for u,v in outline]
                mat=("slate","trim","stone")[(k+j)%3]
                # Adjacent voussoirs share their radial cut faces.
                faces=[front,back[::-1],[front[1],back[1],back[0],front[0]],
                     [front[3],back[3],back[2],front[2]]]
                for face in faces:
                    b.face(face if s<0 else face[::-1],mat)
        _block(b,x,y-.42,tip+.36,2.50,1.56,.47,.04,"stone")
        if j==3:
            b.taper((x,y-.26,sill+.03),(x,y-.26,sill+.23),.42,.29,"iron",7)
            for k in range(3):
                xx=x+(.18,-.17,.045)[k]; yy=y+(-.23,-.04,.15)[k]
                zz=sill+.34+.1*(k%2)
                b.taper((xx,yy,sill+.17),(xx,yy,zz),.081,.075,"ash",5)
                _crystal(b,(xx,yy,zz),.30,.061,(.05,0),"amber" if k==0 else "ember")
        else:
            for k in range(2 if j in (0,2,4) else 1):
                _long_bone(b,x-.72+.24*k,y-.39+.26*(k%2),sill+.16+.10*k,
                           .92+.20*(k%2),(-.17,.74,-.63)[k]+.14*j,.04*sin(k+j))
            if j in (0,2,4): _skull(b,x+.49,y-.20,sill+.43,.30)
    # The end bay retains the tear of one diagonal vault spring.
    _block(b,-7.15,1.02,0,1.45,2.55,2.35,-.055,"basalt")
    _block(b,-7.08,1.22,2.31,1.27,2.24,2.01,.04,"stone")
    b.taper((-7.10,1.83,4.29),(-7.10,1.83,5.61),.53,.39,"stone",7)
    _tube(b,[(-7.10,1.83,5.50),(-6.50,1.80,6.45),(-5.69,2.10,7.13),
             (-4.75,2.48,7.78)], [.40,.37,.30,.24],"trim",6)
    _rock(b,6.58,1.22,0,1.42,1.47,2.72,9)
    _rock(b,4.96,-1.21,0,.94,.67,.37,4)


def _ledge(b,a,c,thickness):
    """Cut the shale shelf above a dripline going from a to c."""
    def ring(point):
        x,y,z=point
        return [(x,y,z),(x,2.42,z+.47),(x,2.82,z+thickness+.48),
                (x,y+.16,z+thickness),(x,y-.21,z+.21)]
    left,right=ring(a),ring(c)
    b.face(left[::-1],"basalt");b.face(right,"slate")
    for i,m in enumerate(("basalt","stone","slate","stone","mineral" if a[0]<-4 else "slate")):
        j=(i+1)%5
        b.face([left[i],right[i],right[j]],m)
        b.face([left[i],right[j],left[j]],m)


def _mineral(b):
    pts=[(-8.15,-.22,5.68),(-6.48,-1.06,5.71),(-4.2,-1.39,6.18),
         (-1.57,-1.57,5.92),(.87,-1.28,5.63),(2.81,-.66,4.9)]
    for i,(a,c) in enumerate(zip(pts,pts[1:])):
        _ledge(b,a,c,.85+.24*(i%3))
    for i,(x,y,h,w,d) in enumerate(((-7.45,1.63,6.51,1.52,1.72),(-5.50,2.78,6.36,2.12,1.61),
                                     (-2.80,2.68,6.57,2.19,1.56),(-.43,2.97,6.12,2.10,1.59),
                                     (2.57,2.06,5.36,1.38,1.48),(4.37,1.62,3.67,1.85,1.36),
                                     (6.35,2.04,2.97,1.73,1.46),(7.63,2.32,2.01,1.52,1.65))):
        _rock(b,x,y,0,w,d,h,i+12,("basalt","slate","stone","slate"))
    # Every pendant begins inside the corrugated underside immediately over it.
    for i,(a,c) in enumerate(zip(pts,pts[1:])):
        for j,t in enumerate((.17,.48,.82)):
            x=a[0]+t*(c[0]-a[0])
            y=a[1]+t*(c[1]-a[1])+.21+.31*(j%2)
            z=a[2]+t*(c[2]-a[2])+.14
            n=i*3+j
            length=.42+.42*abs(sin(n*1.84))+(1.32 if n in (3,8,11) else 0)
            r=.09+.14*length
            _tube(b,[(x,y,z+.14),(x+.02,y,z-.20),(x-.07,y-.02,z-.61*length),
                     (x-.09,y-.04,z-length)],
                  [r*1.33,r,.52*r,.018],
                  "mineral" if n in (3,4,11,14) else "slate",5)
    # A sinuous cold mineral ribbon follows the face of the shelf.
    vein=[(x,y-.23,z+.33+.10*sin(i*1.25)) for i,(x,y,z) in enumerate(pts)]
    _tube(b,vein,[.052,.104,.074,.095,.074,.024],"mineral",5)
    # This edge of the actual fifth shelf is also the source of the trickle.
    # Start slightly inside its front lip and let the stream enter its pool.
    t=.39
    a,c=pts[-2:]
    wx,wy,wz=(a[i]+t*(c[i]-a[i]) for i in range(3))
    _tube(b,[(wx,wy+.02,wz+.065),(wx-.07,wy-.04,2.87),(wx+.08,wy-.07,.63),
             (wx+.38,wy-.19,.15)], [.048,.030,.034,.064],"water",4)
    b.ellipsoid((wx+.41,wy-.20,.15),(1.16,.58,.067),"water",3,10)
    for i,x in enumerate((-6.10,-5.22,.14,3.97,7.43)):
        _rock(b,x,-.64+.17*i,0,1.24,.72,.41+.54*(i%3),4*i+3)


def _ring_mount(b, x, y, z, drop=False):
    _block(b,x,y,z-.36,.79,.32,.74,-.018,"trim")
    for sx in (-.23,.23):
        b.taper((x+sx,y-.02,z+.12),(x+sx,y-.17,z+.12),.075,.058,"iron",5)
    b.taper((x,y+.05,z+.06),(x,y-.29,z+.06),.125,.10,"iron",6)
    _ellipse(b,x,y-.34,z-.22,.325,.36,.095,"iron",9)
    if drop:
        for i in range(3):
            _ellipse(b,x+.035*i,y-.33+.042*(i%2),z-.63-i*.31,.15 if i%2 else .115,
                     .217,.067,"iron",7)


def _cistern(b):
    # Short, battered crescent of reservoir wall, faced with uneven ashlar courses.
    points=[(-7.57,1.57),(-5.47,.21),(-2.85,-.54),(-.43,-.79),
            (2.09,-.72),(4.32,-.11),(6.13,1.0),(7.25,2.19)]
    heights=(1.90,2.66,3.02,3.11,2.86,2.66,1.74,1.23)
    for i,((x0,y0),(x1,y1)) in enumerate(zip(points,points[1:])):
        dx=x1-x0;dy=y1-y0
        h0,h1=heights[i],heights[i+1]
        def at(t,depth,z):
            return (x0+dx*t,y0+dy*t+depth,z)
        for j in range(3):
            cut=(.39+.19*((i+j)%2)) if j%2==0 else .70
            for ta,tb in ((0,cut),(cut,1)):
                lo=j/3;hi=(j+1)/3
                def ring(t):
                    top=h0+(h1-h0)*t
                    dep= .045*((i+j)%3)+.14*lo
                    return [at(t,dep,lo*top),at(t,1.06+.22*lo,lo*top),
                            at(t,1.18+.22*hi,hi*top-.018),at(t,dep+.13,hi*top-.018)]
                l,r=ring(ta+.004),ring(tb-.004)
                m=("basalt","stone","slate","stone")[(j+i+(ta>0))%4]
                b.face(l[::-1],m);b.face(r,m)
                for k in range(4):
                    n=(k+1)%4
                    b.face([l[k],r[k],r[n],l[n]],m if k!=2 else "slate")
        if i in (0,6): continue
        # The low, front lip holds a half trough. Its broken NE end drains away.
        z=.76 if i<5 else .63
        band=[at(.002,-1.22,.16),at(.998,-1.22,.16),at(.998,-1.10,z+.12),at(.002,-1.10,z+.12)]
        b.face(band,"stone")
        b.face([at(.002,-1.10,z+.12),at(.998,-1.10,z+.12),
                at(.998,-.79,z+.12),at(.002,-.79,z+.12)],"trim")
        b.face([at(.002,-.77,z-.07),at(.998,-.77,z-.07),
                at(.998,.19,z-.07),at(.002,.19,z-.07)],"water")
    for k,(x,y,z,d) in enumerate(((-4.5,-.19,2.08,False),(-.62,-.67,2.39,True),
                                    (3.52,-.27,2.03,False))):
        _ring_mount(b,x,y-.04,z,d)
    # Fractured end in the silt, with damp marks between the unmortared slabs.
    for i,(x,y,rx,ry,h) in enumerate(((6.5,.52,1.42,.82,.75),(7.56,-.42,1.19,.74,.45),
                                       (6.38,-1.08,.73,.72,.36),(-6.92,.12,.88,.66,.71))):
        _rock(b,x,y,0,rx,ry,h,8+i,("slate","basalt","stone"))
    b.ellipsoid((5.51,-1.04,.10),(1.45,.59,.075),"water",3,9)


_ENTRIES = {"ossuary_wall":_ossuary, "mineral_wall":_mineral, "cistern_wall":_cistern}


def draw(b, kind):
    _ENTRIES[kind](b)
