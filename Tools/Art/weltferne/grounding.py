"""Seat rigid horizon assemblies on one continuous, locally graded terrain mesh.

Coordinate system: X east, Y north, Z up, metres. This is an offline compiler
stage. It does not change any travel/collision function or add a foundation mesh.
"""

from dataclasses import dataclass
from math import cos, radians, sin

import numpy as np


INSET_M = .018
SOIL_MARGIN_M = .65
GRADE_CELL_M = 2.10
EPS = 1.e-8


def _cross(a, b):
    return a[..., 0] * b[..., 1] - a[..., 1] * b[..., 0]


class Surface:
    """Exact vertical intersection with a single-valued, triangulated terrain."""

    def __init__(self, triangles):
        self.triangles = np.asarray(triangles, dtype=float)
        tri = self.triangles
        if tri.ndim != 3 or tri.shape[1:] != (3, 3) or not len(tri):
            raise ValueError("A nonempty triangle surface is required")
        self.a = tri[:, 0]
        self.ab = tri[:, 1] - self.a
        self.ac = tri[:, 2] - self.a
        det = _cross(self.ab, self.ac)
        if np.any(det <= EPS):
            raise ValueError("The terrain must have positive projected triangle areas")
        self.inv = 1.0 / det
        self.lower = tri[:, :, :2].min(axis=1)
        self.upper = tri[:, :, :2].max(axis=1)
        self.step = 4.0
        self.origin = self.lower.min(axis=0) - .03
        self.size = np.floor((self.upper.max(axis=0) - self.origin) / self.step).astype(int) + 1
        bins = [[] for _ in range(int(self.size.prod()))]
        low = ((self.lower-self.origin) / self.step).astype(int)
        high = ((self.upper-self.origin) / self.step).astype(int)
        for t, ((x, y), (x1, y1)) in enumerate(zip(low, high)):
            for ix in range(x, x1+1):
                for iy in range(y, y1+1):
                    bins[iy*self.size[0]+ix].append(t)
        self.bins = np.full((len(bins), max(map(len,bins))), -1, dtype=np.int32)
        for i, bucket in enumerate(bins):
            self.bins[i,:len(bucket)] = bucket

    def heights(self, points):
        points = np.asarray(points, dtype=float).reshape(-1,2)
        if not len(points):
            return np.empty(0)
        if len(points) > 1600:
            return np.concatenate([self.heights(points[i:i+1600]) for i in range(0,len(points),1600)])
        cells = np.floor((points-self.origin)/self.step).astype(int)
        in_grid = ((cells >= 0) & (cells < self.size)).all(axis=1)
        cells = np.clip(cells,0,self.size-1)
        ids = self.bins[cells[:,1]*self.size[0]+cells[:,0]]
        safe = np.maximum(ids,0)
        delta = points[:,None,:]-self.a[safe,:2]
        u = _cross(delta,self.ac[safe,:2])*self.inv[safe]
        v = _cross(self.ab[safe,:2],delta)*self.inv[safe]
        hit = (ids >= 0) & (u >= -EPS) & (v >= -EPS) & (u+v <= 1.+EPS) & in_grid[:,None]
        j = np.argmax(hit,axis=1)
        i = np.arange(len(points))
        k = safe[i,j]
        z = self.a[k,2] + self.ab[k,2]*u[i,j] + self.ac[k,2]*v[i,j]
        return np.where(hit.any(axis=1),z,np.nan)

    def height(self, x, y):
        z = self.heights([[x,y]])[0]
        return float(z) if np.isfinite(z) else None

    def line_clearance(self, start, end):
        """Minimum vertical clearance on all triangle portions traversed in XY.

        Both the ray height and one terrain facet are affine on each clipped
        interval. Testing its two ends therefore also tests every interior point.
        """
        a,b = np.asarray(start,dtype=float),np.asarray(end,dtype=float)
        ids = np.flatnonzero(((self.upper>=np.minimum(a[:2],b[:2])-EPS) &
                             (self.lower<=np.maximum(a[:2],b[:2])+EPS)).all(axis=1))
        if not len(ids):
            return None
        d = b-a
        offset = a[:2]-self.a[ids,:2]
        u = _cross(offset,self.ac[ids,:2])*self.inv[ids]
        v = _cross(self.ab[ids,:2],offset)*self.inv[ids]
        du = _cross(d[:2],self.ac[ids,:2])*self.inv[ids]
        dv = _cross(self.ab[ids,:2],d[:2])*self.inv[ids]
        lower = np.zeros(len(ids))
        upper = np.ones(len(ids))
        valid = np.ones(len(ids),bool)
        for initial,change in ((u,du),(v,dv),(1-u-v,-du-dv)):
            positive,negative = change > 1.e-12,change < -1.e-12
            valid &= positive | negative | (initial>=-EPS)
            cut = np.divide(-initial,change,out=np.zeros_like(change),where=positive|negative)
            lower = np.where(positive,np.maximum(lower,cut),lower)
            upper = np.where(negative,np.minimum(upper,cut),upper)
        valid &= lower <= upper+EPS
        if not valid.any():
            return None
        z0 = self.a[ids,2]+u*self.ab[ids,2]+v*self.ac[ids,2]
        slope = du*self.ab[ids,2]+dv*self.ac[ids,2]
        return float(np.minimum(a[2]-z0+lower*(d[2]-slope),
                                a[2]-z0+upper*(d[2]-slope))[valid].min())


def _hull(points):
    """Monotone convex hull, CCW; collinear intermediate vertices are discarded."""
    xy = np.unique(np.asarray(points,dtype=float).reshape(-1,2).round(7),axis=0).tolist()
    def chain(seq):
        v = []
        for p in seq:
            while len(v) >= 2:
                a,b = v[-2:]
                if (b[0]-a[0])*(p[1]-a[1])-(b[1]-a[1])*(p[0]-a[0]) > 1.e-8:
                    break
                v.pop()
            v.append(p)
        return v
    return np.array(chain(xy)[:-1]+chain(reversed(xy))[:-1])


def _planes(poly):
    edges = np.roll(poly,-1,axis=0)-poly
    normals = np.stack((edges[:,1],-edges[:,0]),axis=1)
    normals /= np.linalg.norm(normals,axis=1)[:,None]
    return normals,(normals*poly).sum(axis=1)


def _expand(poly, margin):
    # Minkowski sum with a short octagon avoids long mitres on narrow rock fins.
    t = np.arange(8)*np.pi/4.
    disk = np.stack((np.cos(t),np.sin(t)),axis=1)*(margin / np.cos(np.pi/8.))
    return _hull(poly[:,None,:]+disk)


def _rotate(points, angle):
    c,s = cos(angle),sin(angle)
    return points @ np.array([[c,s],[-s,c]])


def _samples(poly, step=2.6):
    pts = [*poly, *(.5*(poly+np.roll(poly,-1,axis=0))), poly.mean(axis=0)]
    small,large = poly.min(axis=0),poly.max(axis=0)
    x = np.arange(small[0]+step/2,large[0],step)
    y = np.arange(small[1]+step/2,large[1],step)
    if len(x) and len(y):
        grid = np.stack(np.meshgrid(x,y),axis=-1).reshape(-1,2)
        n,h = _planes(poly)
        pts.extend(grid[((grid@n.T-h).max(axis=1)<=EPS)])
    return np.asarray(pts)


def _separation(a,b):
    # Positive if some separating axis leaves daylight between these polygons.
    sep = -np.inf
    for one,two in ((a,b),(b,a)):
        n,h = _planes(one)
        sep = max(sep,((two@n.T).min(axis=0)-h).max())
    return float(sep)


def _visible_margin(surface, direction, distance, height, near_edge, exact=False):
    # Height of a line from eye to the recognisable upper body of the landmark.
    # Its own rear retaining soil does not count as an intervening ridge.
    if exact and near_edge-1.2>42.0:
        return surface.line_clearance([*(direction*42.),3.0+42.0*(height-3.)/distance],
            [*(direction*(near_edge-1.2)),3.0+(near_edge-1.2)*(height-3.)/distance])
    radii = np.arange(42., near_edge-1.2, 1.0)
    if not len(radii):
        return None
    ground = surface.heights(radii[:,None]*direction)
    valid = np.isfinite(ground)
    if not valid.any():
        return None
    ray = 3.0 + radii*(height-3.0)/distance
    return float((ray[valid]-ground[valid]).min())


@dataclass
class Placement:
    kind: str
    bearing: float
    original_distance: float
    distance: float
    scale: float
    turn: float
    x: float
    y: float
    origin_z: float
    pad_z: float
    footprint: np.ndarray
    pad: np.ndarray
    outer: np.ndarray
    width: float
    height: float
    local_base: float
    before_min: float
    before_max: float
    original_min: float
    original_max: float
    near_edge: float
    original_screening: object
    screening: object = None
    residual: float = 0.
    triangle_start: int = 0
    triangle_count: int = 0


def _candidates(mark, asset, surface):
    vertices = np.asarray(asset.tris,dtype=float).reshape(-1,3)
    lo,hi = float(vertices[:,2].min()),float(vertices[:,2].max())
    # Authoring baseline is local ground zero. Roots extending down from that
    # baseline stay buried; a model whose whole base starts above it is lowered.
    local_base = max(0.,lo)
    foot = vertices[vertices[:,2] <= max(.5,lo+.5),:2]
    local_hull = _hull(foot)
    if len(local_hull) < 3:
        raise ValueError(mark['kind']+" has no non-collinear ground footprint")
    scale,angle = float(mark["scale"]),radians(mark["bearing"])
    direction = np.array([sin(angle),cos(angle)])
    rot = radians(mark["turn"]-mark["bearing"])
    local_hull = _rotate(local_hull*scale,rot)
    pad_local = _expand(local_hull,SOIL_MARGIN_M)
    width = float(np.clip(2.9 + .15*np.max(np.ptp(pad_local,axis=0)),3.8,6.8))
    outer_local = _expand(pad_local,width)
    sample = _samples(pad_local)
    orig = float(mark["distance"])
    original = surface.heights(sample+orig*direction)
    orig_valid = original[np.isfinite(original)]
    if not len(orig_valid):
        raise ValueError(mark['kind']+" is entirely outside the authored landscape")
    original_screening = _visible_margin(surface,direction,orig,
        (local_base+(hi-local_base)*.78)*scale,orig+float((local_hull@direction).min()),True)
    # Small sites carry less disruptive earthwork than long exposed arcades.
    # The original radius wins when it seats and the ground is already gentle.
    shifts = [0.]
    for delta in (1.,2.,3.,4.,5.,6.,8.,10.,12.,14.,16.,18.,20.,22.,24.,27.,30.,33.,36.,39.):
        if delta <= max(22.,min(39.,orig*.35)):
            shifts.extend((-delta,delta))
    candidates = []
    for shift in shifts:
        distance = orig+shift
        center = distance*direction
        pad = pad_local + center
        outer = outer_local + center
        z = surface.heights(sample+center)
        outer_z = surface.heights(outer)
        if not (np.isfinite(z).all() and np.isfinite(outer_z).all()):
            continue
        low,high = float(z.min()),float(z.max())
        pad_z = float((np.median(z)+.5*(high+low))*.5)
        near = distance+float((local_hull@direction).min())
        margin = _visible_margin(surface,direction,distance,pad_z+(hi-local_base)*scale*.78,near)
        span = high-low
        # Penalise large cuts and screening rather than shifting on every mild grade.
        cost = .29*abs(shift) + .36*max(0.,span-1.65) + 1.35*max(0.,span-3.2)
        cost += 1.35*max(0.,span-6.) + 1.25*max(0.,.35-(margin if margin is not None else 10.))
        p = Placement(mark['kind'],mark['bearing'],orig,distance,scale,mark['turn'],
            float(center[0]),float(center[1]),pad_z-INSET_M-local_base*scale,pad_z,
            local_hull+center,pad,outer,width,(hi-local_base)*scale,local_base,low,high,
            float(orig_valid.min()),float(orig_valid.max()),near,original_screening)
        candidates.append((cost,p))
    if not candidates:
        raise ValueError("No site entirely on the landscape for "+mark["kind"])
    return sorted(candidates,key=lambda item:(item[0],abs(item[1].distance-orig),item[1].distance))


def _arrange(all_sites):
    """Solve physically conflicting neighbours together, before changing the ground.

    All alternatives for one assembly share a rotation and footprint shape. SAT
    separation between every pair of translations can therefore be tabulated
    cheaply, including the unlucky case of two adjoining structures on one ray.
    """
    count = len(all_sites)
    adjacent = [set() for _ in range(count)]
    relation = {}
    for i,a_sites in enumerate(all_sites):
        a0 = a_sites[0][1]
        centres_a = np.array([[p.x,p.y] for _,p in a_sites])
        shape_a = a0.pad - centres_a[0]
        n_a,h_a = _planes(shape_a)
        for j in range(i):
            b_sites = all_sites[j]
            b0 = b_sites[0][1]
            centres_b = np.array([[p.x,p.y] for _,p in b_sites])
            shape_b = b0.pad-centres_b[0]
            n_b,h_b = _planes(shape_b)
            delta = centres_b[None,:,:]-centres_a[:,None,:]
            sep_a = (delta@n_a.T+np.min(shape_b@n_a.T,axis=0)-h_a).max(axis=2)
            sep_b = (-delta@n_b.T+np.min(shape_a@n_b.T,axis=0)-h_b).max(axis=2)
            sep = np.maximum(sep_a,sep_b)
            penalties = 1.15*np.maximum(0.,1.9-sep)
            penalties[sep < .45] = np.inf
            if np.any(penalties>EPS):
                relation[i,j],relation[j,i] = penalties,penalties.T
                adjacent[i].add(j)
                adjacent[j].add(i)
    assignment = {}
    unvisited = set(range(count))
    while unvisited:
        i = min(unvisited)
        component,stack = set(),[i]
        while stack:
            k = stack.pop()
            if k in component:
                continue
            component.add(k)
            stack.extend(adjacent[k]-component)
        unvisited -= component
        if len(component)==1:
            assignment[i] = 0
            continue
        order = sorted(component,key=lambda k:(-len(adjacent[k]),len(all_sites[k]),k))
        tail = np.cumsum([all_sites[k][0][0] for k in order][::-1])[::-1]
        best=[np.inf,None]
        def visit(pos,chosen,cost):
            if pos==len(order):
                if cost<best[0]-EPS:
                    best[:] = [cost,dict(chosen)]
                return
            if cost+tail[pos]>=best[0]-EPS:
                return
            k=order[pos]
            for a,(score,_) in enumerate(all_sites[k]):
                if cost+score+(tail[pos+1] if pos+1<len(order) else 0.)>=best[0]-EPS:
                    break
                pair=sum(relation[k,j][a,v] for j,v in chosen.items() if j in adjacent[k])
                if not np.isfinite(pair):
                    continue
                chosen[k]=a
                visit(pos+1,chosen,cost+score+pair)
                del chosen[k]
        visit(0,{},0.)
        if best[1] is None:
            raise ValueError("No supported collision-free arrangement for "+
                             ", ".join(all_sites[k][0][1].kind for k in order))
        assignment.update(best[1])
    return [all_sites[i][assignment[i]][1] for i in range(count)]


def _bounds_mask(txy, poly):
    return ((txy.max(axis=1)>=poly.min(axis=0)-EPS) &
            (txy.min(axis=1)<=poly.max(axis=0)+EPS)).all(axis=1)


def _tri_intersects(tri,poly):
    if not len(tri):
        return np.empty(0,dtype=bool)
    # Separating axes of BOTH convex figures. Includes touching support edges.
    n,h = _planes(poly)
    outside_pad = ((tri @ n.T - h).min(axis=1) > EPS).any(axis=1)
    q = np.roll(tri,-1,axis=1)-tri
    side = _cross(q[:,:,None,:],poly[None,None,:,:]-tri[:,:,None,:])
    outside_tri = (side.max(axis=2) < -EPS).any(axis=1)
    return ~(outside_pad | outside_tri)


def _refine(vertices, faces, tags, pads):
    """Conforming red/green midpoint splits on all edges marked by a collar."""
    for _ in range(5):
        xy = vertices[faces,:2]
        refine = np.zeros(len(faces),bool)
        for p in pads:
            candidates = np.flatnonzero(_bounds_mask(xy,p.outer))
            local = xy[candidates]
            inside_outer = _tri_intersects(local,p.outer)
            n,h = _planes(p.pad)
            inside_pad = (local@n.T-h).max(axis=(1,2)) <= EPS
            refine[candidates[inside_outer & ~inside_pad]] = True
        if not refine.any():
            break
        # Every marked edge splits on both its incident triangles; no hanging
        # vertices or overlapping fragments are created at an unchanged neighbour.
        e = np.stack((faces,np.roll(faces,-1,axis=1)),axis=2)
        sorted_edges = np.sort(e[refine],axis=2).reshape(-1,2)
        candidates = np.unique(sorted_edges,axis=0)
        length = np.linalg.norm(vertices[candidates[:,0],:2]-vertices[candidates[:,1],:2],axis=1)
        cut = candidates[length > GRADE_CELL_M]
        if not len(cut):
            break
        mids = {tuple(edge):len(vertices)+i for i,edge in enumerate(cut)}
        added = .5*(vertices[cut[:,0]]+vertices[cut[:,1]])
        vertices = np.concatenate((vertices,added))
        fresh,materials = [],[]
        for f,tag in zip(faces,tags):
            a,b,c = map(int,f)
            ab,bc,ca = [mids.get((min(x,y),max(x,y)),-1) for x,y in ((a,b),(b,c),(c,a))]
            ncuts = int(ab>=0)+int(bc>=0)+int(ca>=0)
            if ncuts == 0:
                children = [(a,b,c)]
            elif ncuts == 3:
                children = [(a,ab,ca),(ab,b,bc),(ca,bc,c),(ab,bc,ca)]
            elif ncuts == 1:
                if bc>=0: a,b,c,ab = b,c,a,bc
                elif ca>=0: a,b,c,ab = c,a,b,ca
                children = [(a,ab,c),(ab,b,c)]
            else:
                if ab<0: a,b,c,ab,bc = b,c,a,bc,ca
                elif bc<0: a,b,c,ab,bc = c,a,b,ca,ab
                children = [(b,bc,ab),(ab,bc,c),(ab,c,a)]
            fresh.extend(children)
            materials.extend([tag]*len(children))
        faces,tags = np.array(fresh,dtype=int),np.array(materials,dtype=int)
    return vertices,faces,tags


def _grade(before, tags, pads):
    vertices,idx = np.unique(before.triangles.reshape(-1,3),axis=0,return_inverse=True)
    faces = idx.reshape(-1,3)
    vertices,faces,tags = _refine(vertices,faces,np.asarray(tags),pads)
    z0 = vertices[:,2].copy()
    points,tri = vertices[:,:2],vertices[faces,:2]
    weights,hard = [],[]
    earthworks = np.zeros(len(faces),dtype=bool)
    for p in pads:
        n,h = _planes(p.pad)
        # All triangles touching the flat polygon have all three vertices at the
        # same height. This is a proof over the whole sole, not three test stakes.
        ids = np.flatnonzero(_bounds_mask(tri,p.pad))
        flat_faces = ids[_tri_intersects(tri[ids],p.pad)]
        lock = np.unique(faces[flat_faces])
        hard.append(lock)
        w = np.zeros(len(vertices))
        region = np.flatnonzero(_bounds_mask(tri,p.outer))
        outer_n,outer_h = _planes(p.outer)
        fully_inside = (tri[region]@outer_n.T-outer_h).max(axis=(1,2)) <= EPS
        earthworks[region[fully_inside]] = True
        ii = np.unique(faces[region[fully_inside]])
        if len(ii):
            distance = np.maximum(0.,(points[ii]@n.T-h).max(axis=1))
            t = np.clip(1.-distance/p.width,0.,1.)
            w[ii] = t*t*(3.-2.*t)
            # Preserve every triangle which straddles the feather's outer edge,
            # including any neighbouring interpolation on the old terrain mesh.
            boundary_vertices = np.unique(faces[region[~fully_inside]])
            w[boundary_vertices] = 0.
        w[lock] = 1.
        weights.append(w)
    # Very close terraces may share a mesh vertex even when their footings do
    # not intersect. Make these genuine adjoining sites one continuous grade.
    groups = list(range(len(pads)))
    def root(i):
        while groups[i] != i:
            i = groups[i]
        return i
    for i,l in enumerate(hard):
        for j in range(i):
            if np.intersect1d(l,hard[j],assume_unique=True).size:
                groups[root(i)] = root(j)
    for group in {root(i) for i in range(len(pads))}:
        members = [i for i in range(len(pads)) if root(i)==group]
        height = float(np.mean([pads[i].pad_z for i in members]))
        for i in members:
            p = pads[i]
            p.pad_z,p.origin_z = height,height-INSET_M-p.local_base*p.scale
    total = np.sum(weights,axis=0)
    strength = np.max(weights,axis=0)
    target = np.array([p.pad_z for p in pads]) @ np.asarray(weights)
    target = np.divide(target,total,out=z0.copy(),where=total>0)
    vertices[:,2] = z0 + strength*(target-z0)
    for p,l in zip(pads,hard):
        vertices[l,2] = p.pad_z
    dz = vertices[:,2]-z0
    outer_delta = float(np.max(np.abs(dz[faces[~earthworks]]))) if (~earthworks).any() else 0.
    if outer_delta > EPS:
        raise ValueError("Horizon grading would move terrain beyond its feather collars")
    shaped = Surface(vertices[faces])
    area = .5*float(np.sum(_cross(before.ab,before.ac)))
    area_delta = .5*float(np.sum(_cross(shaped.ab,shaped.ac)))-area
    if abs(area_delta) > 1.e-5:
        raise ValueError("The graded terrain's projected area changed")
    return shaped,tags,dict(maxCutM=float(max(0.,-dz.min())),maxFillM=float(max(0.,dz.max())),
        alteredVertices=int(np.count_nonzero(np.abs(dz)>EPS)), unchangedOutsideMaxDeltaM=outer_delta,
        sourceProjectedAreaM2=area, projectedAreaDeltaM2=area_delta)


class GroundPlan:
    def __init__(self,before,surface,placements,stats):
        self.before,self.surface,self.placements,self.stats = before,surface,placements,stats
        self._exclusions = [(_planes(p.pad),p.pad.min(axis=0),p.pad.max(axis=0)) for p in placements]

    def blocked(self,x,y,radius=0.):
        xy = np.asarray([x,y])
        for (n,h),low,high in self._exclusions:
            if np.all(xy >= low-radius) and np.all(xy <= high+radius) and np.max(n@xy-h)<=radius+EPS:
                return True
        return False

    def append(self,b,assets):
        for p in self.placements:
            asset = assets[p.kind]
            xyz = np.asarray(asset.tris,dtype=float)*p.scale
            xyz[:,:,:2] = _rotate(xyz[:,:,:2],radians(p.turn-p.bearing))+[p.x,p.y]
            xyz[:,:,2] += p.origin_z
            p.triangle_start = len(b.tris)
            p.triangle_count = len(xyz)
            b.tris.extend(xyz.tolist())
            b.tags.extend(asset.tags)
            b.ranges.append((p.kind,len(b.tris)))

    def report(self):
        def record(p):
            return dict(kind=p.kind,bearing=p.bearing,originalDistance=p.original_distance,distance=p.distance,
                scale=p.scale,turn=p.turn,center=[p.x,p.y],originZ=p.origin_z,padZ=p.pad_z,
                footprint=p.footprint.tolist(),flatPad=p.pad.tolist(),outer=p.outer.tolist(),
                triangleStart=p.triangle_start,triangleCount=p.triangle_count,beforeMin=p.before_min,
                beforeMax=p.before_max,originalNominalGroundMin=p.original_min,
                originalNominalGroundMax=p.original_max,afterMaxFootResidual=p.residual,
                screeningClearanceM=p.screening,originalScreeningClearanceM=p.original_screening)
        return dict(sourceTriangles=len(self.before.triangles),gradedTriangles=len(self.surface.triangles),
            maxRadiusChangeM=max(abs(p.distance-p.original_distance) for p in self.placements),
            placements=[record(p) for p in self.placements],**self.stats)


def prepare(builder, marks, assets):
    """Grade the terrain, leaving details and actual model emission to the caller."""
    count = builder.landscape_surface_count
    if len(builder.tris) != count:
        raise ValueError("Generate only the continuous terrain before preparing the placements")
    before = Surface(builder.tris)
    sites = [_candidates(mark,assets[mark['kind']],before) for mark in marks]
    placements = _arrange(sites)
    after,tags,stats = _grade(before,builder.tags,placements)
    for p in placements:
        sample = after.heights(_samples(p.pad,1.6))
        if not np.isfinite(sample).all():
            raise ValueError(p.kind+" has unsupported ground contact")
        p.residual = float(np.max(np.abs(sample-p.pad_z)))
        if p.residual > 1.e-5:
            raise ValueError(p.kind+" was left on an uneven footing")
        p.screening = _visible_margin(after,np.array([sin(radians(p.bearing)),cos(radians(p.bearing))]),
                                     p.distance,p.pad_z+p.height*.78,p.near_edge,True)
    builder.tris = after.triangles.tolist()
    builder.tags = tags.tolist()
    builder.landscape_surface_count = len(builder.tris)
    builder.terrain_count = len(builder.tris)
    return GroundPlan(before,after,placements,stats)
