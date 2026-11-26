"""Weathered landscape rings for the Ashen Crypt vistas. X east, Y north, Z up."""

from math import cos, exp, hypot, pi, sin
from random import Random


TAU = 2.0 * pi

# Scarp relief, distant relief, number of tree groves, trees per grove.
# The distant mountain profiles share their compass bearings across the open levels.
_OPEN = {
    "ash":     (1.00, 1.02, 6, 2),
    "kiln":    (1.30, 1.04, 4, 2),
    "village": (0.67, 0.91, 9, 3),
    "road":    (0.87, 0.99, 9, 2),
    "mill":    (0.80, 1.02, 9, 3),
    "store":   (1.05, 1.01, 6, 3),
    "forest":  (0.80, 0.94, 10, 3),
    "toll":    (1.05, 0.97, 9, 3),
    "ridge":   (1.30, 1.12, 9, 3),
    "future":  (0.94, 1.06, 9, 3),
}

# Wall relief, warmer/lighter cut, dark cut, occasional exposed stratum.
_CLOSED = {
    "crypt": (1.05, "stone", "basalt", "trim"),
    "nest":  (0.86, "slate", "basalt", "bone"),
    "bear":  (0.92, "stone", "basalt", "moss"),
    "rift":  (1.24, "slate", "dark", "mineral"),
    "vault": (1.12, "stone", "basalt", "mineral"),
}


def _point(angle, radius, height):
    return (radius * sin(angle), radius * cos(angle), height)


def _mix(a, b, t):
    return a + (b - a) * t


def _peak(angle, centre, left, right, height):
    """Asymmetric, angular peak, wrapped across the back of the panorama."""
    distance = (angle - centre + pi) % TAU - pi
    span = left if distance < 0.0 else right
    return height * max(0.0, 1.0 - abs(distance) / span) ** 1.22


def _mountain(angle):
    # A split summit in the NE, a long western saddle, a fallen escarpment to the south.
    h = 12.8 + 2.1 * sin(3 * angle - .4) + 2.5 * cos(5 * angle + .7)
    for degree, left, right, height in (
        (20, .40, .16, 22), (36, .16, .36, 27), (68, .16, .48, 11),
        (127, .33, .22, 17), (160, .38, .12, 12), (202, .63, .23, 20),
        (254, .19, .40, 14), (286, .37, .18, 22), (311, .17, .46, 16),
    ):
        h += _peak(angle, degree * pi / 180.0, left, right, height)
    individual_teeth = sin(14 * angle + .65 * sin(5 * angle))
    return h + 2.8 * individual_teeth + 1.35 * sin(31 * angle + .7) + .85 * cos(53 * angle)


def _fractures(angle, radius, phase):
    """Drainage divides and gullies bending downhill around resistant rock."""
    w = angle + .069 * sin(radius * .061 + 3.0 * angle + phase)
    w += .032 * sin(5 * angle - .055 * radius) + .012 * sin(.14 * radius + 9 * angle)
    broad = 1.0 - abs(sin(7 * w + 1.8 * sin(3 * w + phase)))
    rib = 1.0 - abs(sin(16 * w + 1.1 * cos(5 * w) + .6 * sin(8 * w + phase)))
    splinter = 1.0 - abs(cos(33 * w + 1.1 * sin(9 * w)))
    return .54 * broad ** 1.4 + .32 * rib ** 1.6 + .14 * splinter


def _radii(anchors, detail):
    if not detail:
        return anchors
    out = [anchors[0]]
    for i in range(1, len(anchors)):
        out.extend((.5 * (anchors[i - 1] + anchors[i]), anchors[i]))
    return out


def _open_section(angle, style, phase, detail=False):
    relief, peaks, _, _ = _OPEN[style]
    shoulder = .52 + .26 * sin(4 * angle + phase) + .19 * cos(7 * angle + .6)
    basin = .52 + .24 * sin(3 * angle - .3) + .17 * sin(7 * angle + phase)
    spur_crest = 104 + 8 * sin(3 * angle + phase) + 3.2 * cos(7 * angle) + 2.1 * sin(14 * angle)
    distant_crest = 146 + 6 * sin(3 * angle) + 3.1 * cos(8 * angle + .6)
    summit = _mountain(angle) * peaks
    knots = (46, 59, 73, 83, 89, 95, 101, 107, 114, 123, 132, 147, 164)
    coordinates = []
    for r0 in _radii(knots, detail):
        # Uneven row spacing follows the massif and avoids concentric mesh outlines.
        radius = r0 + sin(3 * angle + phase + r0 * .017) * (1.0 + .015 * (r0 - 46))
        radius += .8 * cos(7 * angle - .034 * r0)
        exposure = max(0.0, min(1.0, (radius - 51) / 31))
        ridge = _fractures(angle, radius, phase)
        # A composite asymmetric scarp. Its fingers trail well down the frontal slope;
        # intervening gullies continue up into the crown rather than ending at a contour.
        shoulder_dist = (radius - spur_crest) / (15.5 + 9 * ridge)
        inner_mass = exp(-shoulder_dist * shoulder_dist * (1.15 if shoulder_dist < 0 else 2.5))
        spurs = inner_mass * relief * (1.2 + shoulder * (8.2 + 14.2 * ridge))
        foot = distant_crest - 49.5 + 4.5 * sin(5 * angle)
        fraction = max(0.0, min(1.0, (radius - foot) / (distant_crest - foot)))
        if radius <= distant_crest:
            far = summit * fraction ** (1.62 - .72 * ridge)
            far += summit * .31 * sin(pi * fraction) * (ridge - .34)
        else:
            far = summit * max(.19, 1 - (radius - distant_crest) / 24)
        # Eroded channels also incise the talus. Incision varies along its length.
        channel = sin(10 * angle + .071 * radius + .79 * sin(5 * angle + phase))
        runnels = 1.6 * (1.0 - abs(channel)) ** 5 * exposure
        lobe = (sin(3.7 * sin(angle) + .079 * radius) + .64 * cos(7 * angle - .14 * radius))
        ground = -1.7 + .041 * (radius - 46) + lobe * (.35 + exposure * .68)
        coordinates.append((radius, ground + spurs + far - runnels + .7 * basin))
    return coordinates


def _cave_section(angle, style, phase, detail=False):
    relief = _CLOSED[style][0]
    bay = .49 + .24 * sin(4 * angle + phase) + .19 * cos(8 * angle - phase)
    fissure = 5.3 * cos(5 * angle + phase) + 2.2 * sin(11 * angle)
    height = relief * (11.8 + 17.4 * bay + 1.7 * sin(19 * angle))
    knots = (46, 59, 72, 81, 85, 91, 95, 99, 104, 109, 115, 125, 136)
    coordinates = []
    for r0 in _radii(knots, detail):
        radius = r0 + fissure * (.13 + .66 * max(0.0, min(1.0, (r0 - 63) / 34)))
        rib = _fractures(angle, radius, phase)
        crest = 107.0 + fissure + 4.0 * cos(3 * angle - phase) - 7 * rib
        floor = -1.9 + .043 * (radius - 46) + .62 * sin(.12 * radius + 7 * angle)
        foot = crest - (24 + 11 * rib)
        climb = max(0.0, min(1.0, (radius - foot) / (crest - foot)))
        ledge = height * climb ** (2.03 - .94 * rib) + height * .29 * sin(climb * pi) * (rib - .32)
        if radius > crest:
            ledge = height - min(7.0, (radius - crest) * .55)
            ledge += max(0.0, (radius - crest - 13.0) / 12.0) * (2.7 + 5.7 * rib)
        coordinates.append((radius, floor + ledge))
    return coordinates


def _on_band(section, band, t):
    a, b = section[band:band + 2]
    return _mix(a[0], b[0], t), _mix(a[1], b[1], t)


class _Attachments:
    """Mounts individual decoration on final terrain and records its actual contact."""
    def __init__(self, mesh, height_at, blocked):
        self.mesh = mesh
        self.height = height_at
        self.blocked = blocked
        self.records = []

    def begin(self, kind, attachment, x, y, radius):
        rec = dict(kind=kind, attachment=attachment, x=float(x), y=float(y),
                   radius=float(radius), first_triangle=len(self.mesh.tris), status="placed", contacts=[])
        self.records.append(rec)
        if self.blocked is not None and self.blocked(x, y, radius):
            rec["status"] = "blocked"
            return None
        if self.height(x, y) is None:
            rec["status"] = "outside"
            return None
        return rec

    @staticmethod
    def contact(rec, x, y, z, support_z):
        rec["contacts"].append([float(x), float(y), float(z), float(support_z)])


class _DryGeometry:
    """Consume the same authored shape choices when its footprint is occupied."""
    def taper(self, *unused):
        pass

    def face(self, *unused):
        pass


_DRY = _DryGeometry()


def _top_at(triangles, x, y, minimum):
    """Top intersection of an actual local rock skin or its underlying ground."""
    height = minimum
    for p, q, r in triangles:
        ax, ay = q[0]-p[0], q[1]-p[1]
        bx, by = r[0]-p[0], r[1]-p[1]
        det = ax*by-ay*bx
        if abs(det) < 1e-8:
            continue
        dx, dy = x-p[0], y-p[1]
        u, v = (dx*by-dy*bx)/det, (dy*ax-dx*ay)/det
        if u >= -1e-7 and v >= -1e-7 and u+v <= 1.0000001:
            h = p[2]+u*(q[2]-p[2])+v*(r[2]-p[2])
            if height is None or h > height:
                height = h
    return height


def _rock(b, rng, centre, width, depth, height, side, top, direction, plates=2,
          *, mounts, kind):
    """A nose of bedrock with a wedge-shaped head and a joint-broken foot."""
    cx, cy, _ = centre
    sides = rng.randrange(5, 8)
    u = (cos(direction), sin(direction))
    v = (-u[1], u[0])
    offset = rng.uniform(-.34, .34)
    cut = []
    for i in range(sides):
        a = (i + rng.uniform(-.15,.15)) * TAU / sides + offset
        cut.append((cos(a), sin(a), rng.uniform(.78, 1.18), rng.uniform(-.16,.14)))
    lean = rng.uniform(-.28, .29)
    # The footprint includes the projecting cap. Every stone samples its own basal
    # polygon against the final grade; a wide stone cannot hang off a sloping pad.
    radius = 1.45*hypot(width, depth)
    rec = mounts.begin(kind, "floor", cx, cy, radius)
    if rec is None:
        return None
    mid = mounts.height(cx, cy)
    embed = min(.30, max(.045, height*.105))
    base_ground = []
    for dx, dy, chip, joint in cut:
        x = cx + dx*width*chip*u[0] + dy*depth*chip*v[0]
        y = cy + dx*width*chip*u[1] + dy*depth*chip*v[1]
        ground = mounts.height(x, y)
        if ground is None:
            rec["status"] = "outside"
            return None
        base_ground.append(ground)
        mounts.contact(rec, x, y, ground-embed, ground)
    last, skin = [], []
    for k in range(plates + 1):
        t = k / plates
        row = []
        for ground, (dx, dy, chip, joint) in zip(base_ground, cut):
            # Short fractured tip at one end, long feathered slope at the other.
            nose = dx * .13 + dy * .07
            profile = 1.0 - t * (.44 + nose - .44 * joint)
            length = dx * width * profile * chip + t * width * lean
            breadth = dy * depth * profile * chip + .16 * depth * t
            surface = t * (.66 + .40 * dx + .17 * dy + joint)
            row.append((cx + length * u[0] + breadth * v[0],
                        cy + length * u[1] + breadth * v[1],
                        mid + (ground-mid)*(1-.38*t) - embed + height*surface))
        if last:
            for i, old in enumerate(last):
                j = (i + 1) % sides
                a, c = [old, last[j], row[j]], [old, row[j], row[i]]
                b.face(a, side)
                b.face(c, side)
                skin.extend((a,c))
        last = row
    cap = (cx + width * (lean + .08) * u[0] + depth * .18 * v[0],
           cy + width * (lean + .08) * u[1] + depth * .18 * v[1], mid - embed + .77 * height)
    for i, vertex in enumerate(last):
        tri = [vertex, last[(i + 1) % sides], cap]
        b.face(tri, top)
        skin.append(tri)
    return skin


def _rings(b, style, section):
    steps = 135 if style in _CLOSED else 158
    # Whole slopes expose one host formation. Pale/dark veins are separate,
    # local outcrops below; a change in the sampling row never changes rock.
    material = "basalt" if style in _CLOSED or style == "kiln" else "slate"
    rows = []
    for i in range(steps):
        angle = TAU * i / steps
        rows.append([_point(angle, r, h) for r, h in section(angle, True)])
    for i, row in enumerate(rows):
        other = rows[(i + 1) % steps]
        for j in range(len(row) - 1):
            if (i + j) % 2:
                b.face([row[j], other[j], other[j + 1]], material)
                b.face([row[j], other[j + 1], row[j + 1]], material)
            else:
                b.face([row[j], other[j], row[j + 1]], material)
                b.face([other[j], other[j + 1], row[j + 1]], material)
    if hasattr(b, "tris"):
        # The offline light bake may average geometric normals at shared bevel-free
        # terrain vertices, while preserving hard joints on the outcrops and buildings.
        b.landscape_surface_count = len(b.tris)


def _talus(b, rng, section, style, mounts):
    for group in range(12):
        a = (group + rng.uniform(.15, .85)) * TAU / 12
        main_band = rng.choice((1, 2, 3, 8))
        patches = rng.randrange(4, 8)
        for i in range(patches):
            angle = a + rng.gauss(0, .052)
            band = main_band if i % 4 else max(1, main_band - 1)
            r, _ = _on_band(section(angle), band, rng.uniform(.18, .85))
            scale = rng.uniform(.46, 1.55) * (1.7 if i == 0 else 1.0)
            side = "basalt" if (style == "kiln" or i % 3) else "stone"
            top = "slate" if i % 4 else ("moss" if style in ("bear", "forest", "mill") else "stone")
            _rock(b, rng, _point(angle, r, 0),
                  2.2 * scale, 1.1 * scale, 1.35 * scale,
                  side, top, -angle + rng.uniform(-.50, .50), mounts=mounts, kind="talus")


def _trunk_base(b, x, y, z, radius, end, upper_radius, mounts, rec):
    # A real tapered butt fitted around the trunk circumference. On a steep
    # ridge a level cap would either reveal daylight or bury the uphill trunk.
    base=[]; top=[]
    for i in range(6):
        ca,sa=cos(i*TAU/6),sin(i*TAU/6)
        px,py=x+radius*ca,y+radius*sa
        ground=mounts.height(px,py)
        if ground is None:
            rec["status"]="outside"
            return False
        base.append((px,py,ground-.05))
        top.append((end[0]+upper_radius*ca,end[1]+upper_radius*sa,end[2]))
    for px,py,pz in base:
        mounts.contact(rec,px,py,pz,pz+.05)
    mounts.contact(rec,x,y,z,mounts.height(x,y))
    for i,p in enumerate(base):
        j=(i+1)%len(base)
        b.face([(x,y,z),base[j],p],"bark")
        b.face([p,base[j],top[j],top[i]],"bark")
    b.face(top,"bark")
    return True


def _tree(b, rng, pos, height, leafed, mounts=None, rec=None):
    x, y, z = pos
    wind = rng.uniform(.04, .13) * height
    bend = rng.uniform(-.06, .06) * height
    def trunk(t):
        return x + wind * t * t, y + bend * t, z + height * t
    if rec is not None:
        if not _trunk_base(b,x,y,z,height*.048,trunk(.61),height*.028,mounts,rec):
            b=_DRY
    else:
        b.taper(trunk(0),trunk(.61),height*.048,height*.028,"bark",6)
    b.taper(trunk(.61), trunk(1), height * .028, height * .005, "bark", 5)
    if not leafed:
        for j in range(rng.randrange(3, 5)):
            t = .32 + j * .14
            sx, sy, sz = trunk(t)
            a = j * 2.40 + rng.uniform(-.48, .48)
            reach = height * (1.0 - t) * rng.uniform(.28, .51)
            elbow = (sx + reach * cos(a), sy + reach * sin(a), sz + .09 * height)
            end = (elbow[0] + reach * .49 * cos(a + .28),
                   elbow[1] + reach * .49 * sin(a + .28), elbow[2] + .12 * height)
            b.taper((sx, sy, sz), elbow, height * .020, height * .009, "bark", 4)
            b.taper(elbow, end, height * .009, height * .001, "wood", 4)
        return
    # Independently swept boughs keep the outlines broken, even when trees overlap.
    offset = rng.uniform(0, TAU)
    for tier, t in enumerate((.29, .49, .67, .83)):
        sx, sy, sz = trunk(t)
        for branch in range(5 if tier < 2 else 4):
            a = offset + branch * TAU / 5 + tier * 1.37 + rng.uniform(-.17, .17)
            wing = (.41 - t * .25) * (1 - t) * height * rng.uniform(.67, 1.34)
            spread = wing * rng.uniform(.22,.42)
            hang = rng.uniform(.045,.15) * height
            outer = (sx + cos(a) * wing, sy + sin(a) * wing, sz - hang)
            left = (sx + cos(a) * wing * .62 - sin(a) * spread,
                    sy + sin(a) * wing * .62 + cos(a) * spread, sz - .11 * height)
            right = (sx + cos(a) * wing * .68 + sin(a) * spread,
                     sy + sin(a) * wing * .68 - cos(a) * spread, sz - .10 * height)
            crown = (sx + .06 * wind, sy + .05 * bend, sz + .16 * height)
            inner = (sx, sy, sz - .05 * height)
            mat = "moss" if tier > 1 and branch == 1 else "leaf"
            b.face([crown, outer, left], mat)
            b.face([crown, right, outer], mat)
            b.face([crown, left, inner], "leaf")
            b.face([crown, inner, right], "leaf")
            b.face([left, outer, right, inner], "leaf")


def _fir_profile(b, rng, angle, position, height, mounts=None, rec=None):
    """A thick, wind-shorn crown with a serrated outline, for a distant stand."""
    x, y, z = position
    tangent = (cos(angle), -sin(angle))
    radial = (sin(angle), cos(angle))
    width = height * rng.uniform(.14, .24)
    drift = rng.uniform(-.19, .19) * width
    start = rng.uniform(.09, .24)
    def point(t, side, depth):
        xoffset = side * width + t * t * drift
        return (x + tangent[0] * xoffset - radial[0] * depth,
                y + tangent[1] * xoffset - radial[1] * depth, z + t * height)
    # The two real vertices at the bottom of the profile trunk each meet earth.
    feet=[point(-.04,-.07,0),point(-.04,.07,0)]
    if rec is not None:
        levels=[mounts.height(px,py) for px,py,_ in feet]
        if any(h is None for h in levels):
            rec["status"]="outside"
            b=_DRY
        else:
            feet=[(p[0],p[1],h-.045) for p,h in zip(feet,levels)]
            for (px,py,pz),h in zip(feet,levels):
                mounts.contact(rec,px,py,pz,h)
    # Irregularly notched branch tiers; a protruding spine gives volume in profile.
    for side in (-1, 1):
        times = [start, .30, .36, .48, .55, .66, .72, .83, .87, 1.0]
        widths = [.26, .91, .46, .82, .35, .61, .24, .38, .12, .012]
        border = []
        spine = []
        for i, (t, w) in enumerate(zip(times, widths)):
            if i not in (0, len(times)-1):
                t += rng.uniform(-.019, .019)
                w *= rng.uniform(.84, 1.14)
            border.append(point(t, side * w, .04 * width))
            spine.append(point(t, 0.0, .49 * width * (1-t)))
        # Triangulate the serrations themselves. There is no full rectangular
        # card, and no extra loop at every needle tip. Three spine positions give
        # the low-cost silhouette a slight fold toward the observer.
        polygon = [spine[0], *border, spine[-1], spine[5]]
        if side < 0:
            polygon.reverse()
        projected = [((p[0]-x)*tangent[0]+(p[1]-y)*tangent[1],p[2]) for p in polygon]
        active = list(range(len(polygon)))
        def turn(i, j, k):
            a, c, d = projected[i], projected[j], projected[k]
            return (c[0]-a[0])*(d[1]-a[1])-(c[1]-a[1])*(d[0]-a[0])
        while len(active) > 3:
            for n, mid in enumerate(active):
                lo = active[n-1]
                hi = active[(n+1) % len(active)]
                if turn(lo,mid,hi) <= 1e-10:
                    continue
                if any(turn(lo,mid,i)>0 and turn(mid,hi,i)>0 and turn(hi,lo,i)>0
                       for i in active if i not in (lo,mid,hi)):
                    continue
                b.face([polygon[lo],polygon[mid],polygon[hi]], "leaf")
                active.pop(n)
                break
            else:
                raise ValueError("Could not triangulate a distant fir crown")
        b.face([polygon[i] for i in active], "leaf")
    # A thin trunk is only exposed under the first bough. Its end meets the crown.
    b.face([feet[0],feet[1],point(.36,.035,0),point(.36,-.035,0)],"bark")


def _planted(b, rng, mounts, angle, pos, height, leafed, distant):
    x,y,_ = pos
    rec = mounts.begin("distant_fir" if distant else "tree", "floor", x, y, height*.38)
    foot=0. if rec is None else mounts.height(x,y)-.05
    sink=_DRY if rec is None else b
    if distant:
        _fir_profile(sink,rng,angle,(x,y,foot+.04*height),height,mounts,rec)
    else:
        _tree(sink,rng,(x,y,foot),height,leafed,mounts,rec)


def _woods(b, rng, section, style, mounts):
    _, _, groups, count = _OPEN[style]
    rich = style in ("forest", "toll", "ridge", "mill", "future", "village")
    for group in range(groups):
        a = (group + rng.uniform(.18, .82)) * TAU / groups
        # High groups sit along the back of the scarp. The foreground stays open.
        band = 3 if group % 3 == 0 else 8
        for j in range(count):
            angle = a + rng.uniform(-.09, .09)
            r, _ = _on_band(section(angle), band, rng.uniform(.20, .84))
            scale = rng.uniform(5.1, 9.3) * (1.12 if j == 0 else .86)
            if style == "forest":
                scale *= 1.28
            base = _point(angle, r, 0)
            leafed = rich and rng.random() > .24
            _planted(b,rng,mounts,angle,base,scale,leafed,leafed and j>0)
    if not rich:
        return
    west = style in ("forest", "ridge", "toll")
    groves = 9 if west else 6
    for grove in range(groves):
        a = (grove + rng.uniform(.20, .83)) * TAU / groves
        # Outside the western track, clustered individuals overlap to read as woods.
        # Alternating depth leaves the near side of the paths and landmarks open.
        band = 3 if west and grove % 3 == 0 else (8 if grove % 2 else 10)
        for _ in range(rng.randrange(6, 10) if west else rng.randrange(5, 8)):
            angle = a + rng.uniform(-.10 if west else -.14, .10 if west else .14)
            r, _ = _on_band(section(angle), band, rng.uniform(.22, .85))
            height = rng.uniform(3.3, 6.3) if west else rng.uniform(2.8, 5.1)
            _planted(b,rng,mounts,angle,_point(angle,r,0),height,True,True)


def _crest_outcrops(b, section, mounts):
    # These scree-crowned pinnacles belong to the same distant massifs seen from
    # neighbouring scenes. Their compass bearings and shape persist across the map.
    rng = Random(880163)
    for deg in (18, 35, 65, 123, 158, 199, 252, 283, 310):
        a = deg * pi / 180
        for j in range(rng.randrange(2, 4)):
            angle = a + j * rng.uniform(.012, .035) - .027
            cross_section = section(angle, True)
            r, _ = max(cross_section[-8:], key=lambda p:p[1])
            size = rng.uniform(.65, 1.32)
            _rock(b, rng, _point(angle, r - .1, 0), 3.0 * size,
                  1.05 * size, rng.uniform(2.3, 4.5) * size, "slate", "slate",
                  -angle + rng.uniform(-.42,.42), 2, mounts=mounts, kind="summit_outcrop")


def _outer_crags(b, rng, section, style, mounts):
    for group in range(14):
        a = (group + rng.uniform(.14, .84)) * TAU / 14
        # Plunging, staggered beds cut diagonally into the scarp, rather than
        # perching freestanding boulders on a continuous artificial shelf.
        for j in range(rng.randrange(3, 6)):
            angle = a + j * rng.uniform(.013, .030)
            band = 4 + min(3, j)
            r, _ = _on_band(section(angle), band, rng.uniform(.05, .88))
            scale = rng.uniform(.67, 1.13) * (1.31 if j == 0 else 1.0)
            p = _point(angle, r, 0)
            rock = "basalt" if style == "kiln" else "slate"
            skin = _rock(b, rng, p, scale*rng.uniform(3.9,6.6), scale*rng.uniform(.83,1.61),
                         scale*rng.uniform(2.7,5.4), rock, rock, .5*pi-angle+rng.uniform(-.76,.34),
                         3, mounts=mounts,kind="bedded_crag")
            if style == "kiln" and j == 0 and group in (1, 3, 7):
                # A shard, barely incandescent where the basalt seam faces the valley.
                sx, sy = sin(angle), cos(angle)
                x, y, _ = p
                _attached_shard(b,mounts,skin,(x-1.18*sx,y-1.18*sy),
                                (-.22*sx+.52*sy,-.22*sy-.52*sx),1.3*scale,.073,"ember","kiln_seam",4)


def _attached_shard(b,mounts,skin,xy,lean,length,radius,material,kind,sides):
    """Pin a seam, crystal or fossil spur to its emitted host or the terrain."""
    if skin is None:
        return
    x,y=xy
    rec=mounts.begin(kind,"rock",x,y,hypot(*lean)+radius)
    if rec is None:
        return
    ground=mounts.height(x,y)
    support=_top_at(skin,x,y,ground)
    if support <= ground+1e-6:
        rec["attachment"]="floor"
    foot=support-min(.12,radius*.5)
    mounts.contact(rec,x,y,foot,support)
    b.taper((x,y,foot),(x+lean[0],y+lean[1],foot+length),radius,.020,material,sides)


def _hanging(b,mounts,angle,radial,scale,root):
    """A clinging root or dripstone is fastened to a real projecting rock lip."""
    sx,sy=sin(angle),cos(angle)
    x,y,_=_point(angle,radial,0)
    rec=mounts.begin("hanging_root" if root else "dripstone","overhang",x,y,4.2*scale)
    if rec is None:
        return
    ground=mounts.height(x,y)
    reach=2.15*scale
    fx,fy=x-reach*sx,y-reach*sy
    front_ground=mounts.height(fx,fy)
    if front_ground is None:
        rec["status"]="outside"
        return
    # The rear penetrates the graded bank. Its exposed nose carries the pendant;
    # its exact underside is horizontal at the front rib, so the two meshes meet.
    z0=ground-.19
    z1=max(ground+.35*scale,front_ground+.78*scale)
    a=(x+.62*scale*sy,y-.62*scale*sx,z0)
    c=(x-.62*scale*sy,y+.62*scale*sx,z0)
    f=(fx+.48*scale*sy,fy-.48*scale*sx,z1)
    g=(fx-.48*scale*sy,fy+.48*scale*sx,z1)
    bottom=[a,c,g,f];tops=[]
    for i,p in enumerate(bottom):
        tops.append((p[0],p[1],p[2]+(.52 if i<2 else .32)*scale))
    b.face(bottom,"basalt");b.face(tops[::-1],"slate")
    for i in range(4):
        j=(i+1)%4
        b.face([bottom[i],tops[i],tops[j],bottom[j]],"basalt")
    # The contact witness lies inside the front underside by 4 cm.
    start=(fx,fy,z1+.04)
    mounts.contact(rec,fx,fy,start[2],z1)
    rec["terrain_anchor"]=[float(x),float(y),float(z0),float(ground)]
    tx,ty=fx-.66*scale*sx,fy-.66*scale*sy
    tip_ground=mounts.height(tx,ty)
    length=min((2.55 if root else 1.7)*scale,max(.31,z1-(tip_ground if tip_ground is not None else front_ground)-.11))
    tip=(tx,ty,z1-length)
    if root:
        mid=(fx-.48*scale*sx+.15*sy,fy-.48*scale*sy-.15*sx,z1-.58*length)
        b.taper(start,mid,.17*scale,.085*scale,"bark",5)
        b.taper(mid,tip,.085*scale,.018,"bark",4)
    else:
        b.taper(start,tip,.45*scale,.036,"stone",6)


def _cave_details(b, rng, section, style, mounts):
    _, cut, base, vein = _CLOSED[style]
    for group in range(14):
        a = (group + rng.uniform(.22, .77)) * TAU / 14
        for j in range(rng.randrange(2, 5)):
            angle = a + rng.uniform(-.063, .063)
            profile=section(angle)
            r, floor = _on_band(profile,3,rng.uniform(.45,.89))
            scale=rng.uniform(.70,1.42)
            # Shapes keep the old designed proportions. Only their actual footing moves.
            column=(profile[10][1]-floor)*rng.uniform(.28,.68)
            x,y,_=_point(angle,r,0)
            pos=(x,y,0)
            sx,sy=sin(angle),cos(angle)
            if style=="bear":
                _rock(b,rng,pos,4.9*scale,2.9*scale,column*.48,base,cut,-angle+.4,3,
                      mounts=mounts,kind="den_outcrop")
                if j==0:
                    _hanging(b,mounts,angle,profile[8][0]-.30,scale,True)
            elif style=="rift":
                lean=rng.uniform(.65,2.2)
                skin=_rock(b,rng,pos,1.9*scale,1.1*scale,column*.72,base,"slate",-angle+.1,
                           mounts=mounts,kind="rift_outcrop")
                if j==0 or group%4==0:
                    _attached_shard(b,mounts,skin,(x-1.05*sx,y-1.05*sy),(-lean*sx,-lean*sy),
                                    column*.68,scale*.48,vein,"mineral_spike",5)
            elif style=="nest":
                skin=_rock(b,rng,pos,3.4*scale,1.65*scale,column*.48,"slate",cut,-angle+.16,3,
                           mounts=mounts,kind="nest_outcrop")
                if group%3==0 and j<2:
                    _attached_shard(b,mounts,skin,(x-1.1*sx,y-1.1*sy),(-1.9*sx,-1.9*sy),
                                    2.7*scale,.34*scale,vein,"fossil_spur",5)
            else:
                _rock(b,rng,pos,2.7*scale,2.0*scale,column,base,cut,-angle+.3,3,
                      mounts=mounts,kind="gallery_pillar")
                _hanging(b,mounts,angle,profile[8][0]+1.6,scale,False)
            if style=="vault" and group%4==0 and j==0:
                _rock(b,rng,(x-.45,y-.38,0),3.1*scale,1.65*scale,.22,"dark","water",-angle,
                      mounts=mounts,kind="water_deposit")


def _terrain_input(style,seed):
    if style not in _OPEN and style not in _CLOSED:
        raise ValueError("Unknown horizon terrain style: "+str(style))
    rng=Random(str(style)+":"+str(seed))
    phase=rng.uniform(0,TAU)
    func=_cave_section if style in _CLOSED else _open_section
    return rng,lambda angle,detail=False:func(angle,style,phase,detail)


def terrain_surface(b,style,seed):
    """Emit only the ring. It can now be graded to the landmark contact plans."""
    _,section=_terrain_input(style,seed)
    _rings(b,style,section)


def terrain_details(b,style,seed,height_at,blocked=None):
    """Attach each decoration after grading. Returns actual metre-space contacts."""
    rng,section=_terrain_input(style,seed)
    mounts=_Attachments(b,height_at,blocked)
    _talus(b,rng,section,style,mounts)
    if style in _CLOSED:
        _cave_details(b,rng,section,style,mounts)
    else:
        _outer_crags(b,rng,section,style,mounts)
        _crest_outcrops(b,section,mounts)
        _woods(b,rng,section,style,mounts)
    ends=[r["first_triangle"] for r in mounts.records[1:]]+[len(b.tris)]
    for rec,end in zip(mounts.records,ends):
        rec["triangle_count"]=end-rec["first_triangle"]
    return mounts.records


def terrain(b,style,seed):
    """Ungraded standalone landscape with its decorations attached to the same mesh."""
    from grounding import Surface
    terrain_surface(b,style,seed)
    surface=Surface(b.tris[:b.landscape_surface_count])
    return terrain_details(b,style,seed,surface.height)
