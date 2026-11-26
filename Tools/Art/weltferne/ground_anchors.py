"""Survey marks on the authored meshes (model metres: east X, north Y, up Z).

support_probes are actual foundation bottoms, pier soles, planted trunk rims or
rock-base vertices. Labels distinguish structurally independent feet. sill_probes
mark the top of visible stone, thresholds or a sealed door's lower retaining band.
They are upper limits on the terrain. Empty spans, suspended mechanisms and raised
decks are deliberately excluded from bearing tests. A negative sole is an
authored buried footing and is measured at that physical elevation, not at Z=0.

interface_probes give (label,x,y,z_below,z_above): test the material continuity
locally in this interval, independently of the height of the surrounding terrain.
Do not infer independent travel or spawn positions from any of these art marks.
"""
from math import cos, hypot, pi, sin

# All distances are local metres. Apply the placement scale to dimensional tolerances.
SUPPORT_GAP_TOLERANCE = .03
SILL_CLEARANCE = .04
INTERFACE_GAP_TOLERANCE = .01


def _rect(name,x,y,w,d,z,angle=0):
    """Four probe locations just inside the bearing footprint. w,d are half-spans."""
    c,s=cos(angle),sin(angle)
    return tuple((name+"-"+direction,x+c*u-s*v,y+s*u+c*v,z)
                 for direction,u,v in (("SW",-w,-d),("SE",w,-d),("NE",w,d),("NW",-w,d)))


def _at(name,x,y,u,v,z,a):
    return (name,x+cos(a)*u-sin(a)*v,y+sin(a)*u+cos(a)*v,z)


def _rock(name,x,y,rx,ry,seed,vertex=0):
    # Where underworld._rock's first unsealed polygon actually meets the soil.
    az=2*pi*vertex/7+.13*sin(seed+.30*vertex)
    radius=.89*(1+.12*sin(3*vertex+seed))
    return (name,x+rx*radius*cos(az),y+ry*radius*sin(az),0.)


def _trunk_rim(name,x,y,scale,height):
    # First lower-rim vertex in exterior_dressing._tree and _twig (uncapped).
    dx,dz=-.18,height*.21+.12
    n=hypot(dx,dz)
    return (name,x+scale*(-.08+.62*dz/n),y,scale*(-.12-.62*dx/n))


_HOUSES=(("longhouse",-2.06,1.65,5.55,6.65,.31,-.13),
         ("forge",3.36,1.87,3.65,5.34,.23,.13),
         ("front-house",-.73,-2.66,3.96,3.34,.20,.07))

_SUPPORTS={
    "village":tuple(p for name,x,y,w,d,f,a in _HOUSES
                    for p in _rect(name,x,y,w/2-.055,d/2-.055,-.16,a)),
    "bell":_rect("bell-keep",0,0,3.45,3.45,0)
           +( ("west-curtain",-6.25,-.32,-.14),("east-curtain",5.22,-.04,-.14) ),
    "kiln":_rect("furnace-base",-.48,.85,5.23,3.21,0)
           +( ("west-stack",-2.85,2.25,0),("middle-stack",.29,2.42,0),("east-stack",3.12,2.9,0) ),
    "mill":_rect("mill-foundation",.84,1.12,4.04,3.10,0)
           +tuple(("sluice-post-"+side,x,-2.42,-.14) for side,x in (("west",-8.55),("east",-6.44)))
           +tuple(("wheel-bearing-"+side,x,-3.92,-.12) for side,x in (("west",-3.01),("east",-1.52)))
           +tuple(("race-wall-"+side,x,y,0) for side,x,y in
                  (("SW",-6.91,-4.51),("SE",2.24,-4.51),("NW",-6.91,-1.79),("NE",2.24,-1.79))),
    "granary":tuple(("granary-"+ns+"-"+ew,x,y,0)
                    for ns,y in (("south",-2.52),("north",2.52))
                    for ew,x in (("west",-3.13),("centre",0.),("east",3.13))),
    "toll":_rect("weighbridge",-.76,1.04,4.78,3.10,0)
           +tuple(("gantry-"+ns+"-"+ew,x,y,0) for ns,y in (("south",-1.16),("north",1.10))
                  for ew,x in (("west",.25),("east",2.27))),
    "crypt_crown":tuple(("octagon-"+str(i),4.70*cos(i*pi/4),4.70*sin(i*pi/4),0) for i in range(8)),
    "abbey":_rect("nave",0,.84,5.53,6.04,0)
           +( ("west-flyer-front",-6.96,-1.65,-.14),("west-flyer-back",-6.96,3.40,-.14),
              ("east-flyer",6.96,.21,-.14),("lowest-step",.31,-6.94,-.13) ),
    "aqueduct":tuple(("aqueduct-bearing-"+str(i+1)+end,(i-2)*4.12,y,0)
                     for i in range(5) for end,y in (("-south",-.92),("-north",.92))),
    "road_shrine":_rect("shrine-step",0,.25,2.53,1.94,-.08),
    "court_wall":tuple(("curtain-"+label,x,.17*sin(.60*x),-.16) for label,x in
                       (("west",-9.45),("left",-5.72),("middle",-.29),("right",4.09),("east",9.01))),
    "mill_bank":tuple(("retaining-pile-"+str(i+1),x,y,-.21) for i,(x,y) in enumerate(
               ((-9.30,-.54),(-7.35,-.55),(-5.51,-.72),(-3.80,-.46),(-2.36,.05),
                (-.93,.09),(.81,.64),(2.64,.89),(4.48,.74),(6.23,.78),(8.34,1.04)))),
    "forest_margin":(_trunk_rim("west-ash",-4.72,.91,1.05,6.84),
                     _trunk_rim("east-ash",3.23,1.67,.88,6.20),
                     ("snag-east",6.86+.30*(3.75*.49+.12)/hypot(.21,3.75*.49+.12),-.80,
                      -.12+.30*.21/hypot(.21,3.75*.49+.12)),
                     ("snag-west",-7.62+.30*(2.05*.49+.12)/hypot(.21,2.05*.49+.12),-.12,
                      -.12+.30*.21/hypot(.21,2.05*.49+.12))),
    "freight_margin":tuple(("quay-bay-"+str(i+1),x,y,-.11) for i,(x,y) in enumerate(
                     ((-7.74,.15),(-5.51,.18),(-3.20,.3),(-1.16,.42),(1.02,.51),(3.23,.72))))
                     +tuple(("cart-rim-"+side,6.04,y,.978-1.04*cos(pi/10))
                            for side,y in (("south",-1.77),("north",.28))),
    "crypt_arch":tuple(("crypt-arcade-"+str(i+1)+"-"+side,s*(w+.375),y+.54,0)
                       for i,(y,w) in enumerate(((-3.7,3.45),(0,3.25),(3.9,3.04)))
                       for s,side in ((-1,"west"),(1,"east"))),
    "sealed_arch":_rect("sealed-slab",0,0,3.19,1.07,0)
                  +(("western-jamb",-4.45,.10,0),("eastern-jamb",4.45,.10,0)),
    "nest":tuple(_rock("fossil-bank-"+side+"-"+str(i),s*(4.5+i*.35),-2.9+i*2.4,
                      1.95,2.2,i+(6 if s>0 else 2),3 if s<0 else 0)
                 for side,s in (("west",-1),("east",1)) for i in (0,2,3)),
    "bear_den":(_rock("west-bedrock",-6,-1.6,2.9,2.5,3,3),
                _rock("east-bedrock",5.75,-1.22,2.9,2.4,5,0),
                _rock("west-rear",-3.65,3.70,2.45,2.8,7,2),
                _rock("east-rear",4.20,4.55,1.92,2.1,8,1)),
    "side_rift":tuple(_rock("rift-"+side+"-"+str(i),s*(4.55+.25*i)+(0,.63,-.42,.35)[i],
                           -3.9+i*2.65,1.62,1.65,i*11+(5 if s>0 else 2),3 if s<0 else 0)
                      for side,s in (("west",-1),("east",1)) for i in range(4)),
    "mill_vault":_rect("cistern-bed",0,1.10,4.83,4.64,0)
                 +tuple(("drive-vault-"+str(i+1)+side,s*(w+.53),y+.59,0)
                        for i,(y,w) in enumerate(((-2.55,4.15),(1.34,3.85),(5.02,3.64)))
                        for s,side in ((-1,"-west"),(1,"-east"))),
    "king":tuple(_rock("crown-bed-"+str(j),-.40,.26,5.72,4.54,6,j) for j in (0,2,3,5)),
    "guardian":tuple(("dais-"+str(i),3.78*cos(i*pi/2),3.78*sin(i*pi/2),0.) for i in range(4)),
    "standing_rock":tuple(("column-"+str(i+1),x,y,-.45) for i,(x,y) in enumerate(
                    ((-2,0),(1,1),(3,0),(-3,1)))),
    "broken_pier":_rect("fallen-pillar",0,0,1.16,1.28,.03),
    "dead_oak":(("oak-rootcollar",0,0,-.35),)+tuple(
                    ("oak-root-"+str(i),2*sin(i*.91),2*cos(i*.91),-.18) for i in (0,2,4,6)),
    "cairn":(("basal-slab-bottom",0,.2,-.20),)+tuple(
              ("basal-slab-"+str(i),2.25*cos(pi/4)*cos(2*pi*i/9),.2+1.60*cos(pi/4)*sin(2*pi*i/9),
               .25-.45*sin(pi/4)) for i in (0,2,4,6)),
    "ossuary_wall":tuple(("niche-base-"+str(i),x,-.25+.047*x*x+.14*sin(1.3*i)-.10,0)
                         for i,x in enumerate((-5.75,-3.03,-.31,2.43,5.17)))
                  +(("west-return",-7.15,1.19,0),),
    "mineral_wall":tuple(_rock("rock-bearing-"+str(i),x,y,w,d,i+12,0 if i>3 else 3) for i,(x,y,w,d) in
                  enumerate(((-7.45,1.63,1.52,1.72),(-5.50,2.78,2.12,1.61),(-2.80,2.68,2.19,1.56),
                             (-.43,2.97,2.10,1.59),(2.57,2.06,1.38,1.48),(4.37,1.62,1.85,1.36),
                             (6.35,2.04,1.73,1.46),(7.63,2.32,1.52,1.65)))),
    "cistern_wall":tuple(("masonry-bay-"+str(i+1),.6*a[0]+.4*c[0],.6*a[1]+.4*c[1]+.50,0)
                    for i,(a,c) in enumerate(zip(
                      ((-7.57,1.57),(-5.47,.21),(-2.85,-.54),(-.43,-.79),(2.09,-.72),(4.32,-.11),(6.13,1.0)),
                      ((-5.47,.21),(-2.85,-.54),(-.43,-.79),(2.09,-.72),(4.32,-.11),(6.13,1.0),(7.25,2.19))))),
}

_SILLS={
    "village":tuple(_at(name+"-stone-sill",x,y,0,-d/2-.12,f+.06,a) for name,x,y,w,d,f,a in _HOUSES),
    "bell":(("keep-first-tread",3.51,-2.91,.56),),
    "mill":(("mill-stone-apron",1.10,-2.03,.66),),
    "granary":(("raised-entrance-deck",.36,-3.17,3.20),),
    "kiln":(("front-hearth",1.55,-1.39,.935),),
    "toll":(("weighhouse-apron",-2.22,-1.15,.82),),
    "abbey":(("lowest-tread",.31,-6.94,.41),),
    "road_shrine":(("first-votive-step",.10,-1.82,.35),),
    "crypt_crown":(("lowest-seal-step",.13,-5.76,.62),),
    "crypt_arch":(("west-portal-base",-3.90,-3.11,.45),("east-portal-base",3.90,-3.11,.45)),
    "mill_vault":(("drive-vault-walkway",-4.06,-3.25,.735),),
    "guardian":(("first-dais-course",4.21,0,.56),),
}

_INTERFACES={
    "village":tuple((d+"-wall-joint",x,y,f-.05,f+.11)
                    for name,cx,cy,w,depth,f,a in _HOUSES
                    for d,x,y,_ in _rect(name,cx,cy,w/2-.055,depth/2-.055,0,a)),
    "bell":(("yoke-to-hung-bell",.03,.02,14.67,14.96),),
    "road_shrine":tuple(("votive-"+str(i+1),x,-.87,z-.035,z+.09)
                        for i,(x,z) in enumerate(((1.73,1.40),(2.04,1.05),(2.35,.70)))),
    "aqueduct":(("iron-in-ruined-bay",6.10,-.606,8.00,8.13),),
    "granary":tuple(("pier-to-platform-"+str(i),x,y,2.71,3.00)
                    for i,(x,y) in enumerate(((-3.13,-2.52),(0,-2.52),(3.13,-2.52),
                                               (-3.13,2.52),(0,2.52),(3.13,2.52)))),
    "abbey":tuple(("outboard-foot-to-column-"+str(i),x,y,.45,.70)
                  for i,(x,y) in enumerate(((-6.96,-1.65),(-6.96,3.40),(6.96,.21)))),
    "mill":(("flume-heel-left",-8.55,-2.91,.41,.62),("flume-heel-right",-6.44,-2.91,.41,.62)),
}


def support_probes(kind):
    return _SUPPORTS.get(kind,())


def sill_probes(kind):
    return _SILLS.get(kind,())


def interface_probes(kind):
    return _INTERFACES.get(kind,())
