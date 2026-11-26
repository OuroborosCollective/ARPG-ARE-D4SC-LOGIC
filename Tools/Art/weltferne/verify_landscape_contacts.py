"""Independently reopen Unity WFH1 geometry and verify natural floor, rock and roof contacts."""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np

from verify_grounding import Rays, read_native, terrain_hit

# 2 mm accommodates float32 positions at 160 m and very small clipped triangles.
EPS = .002
SPIKES = {"kiln_seam": 4, "mineral_spike": 5, "fossil_spur": 5}
HANGERS = {"hanging_root": 5, "dripstone": 6}
STONES = {"talus", "summit_outcrop", "bedded_crag", "den_outcrop", "rift_outcrop",
          "nest_outcrop", "gallery_pillar", "water_deposit"}


def _need(value, why):
    if not value:
        raise ValueError(why)


def _rim(tris):
    """The open basal polygon of a bedrock outcrop is its only unpaired edge loop."""
    points, index = np.unique(tris.reshape(-1,3), axis=0, return_inverse=True)
    index=index.reshape(-1,3)
    edges=np.stack((index,np.roll(index,-1,axis=1)),axis=-1).reshape(-1,2)
    edges.sort(axis=1)
    pairs,counts=np.unique(edges,axis=0,return_counts=True)
    _need(np.all((counts==1)|(counts==2)), "non-manifold bedrock")
    boundary=pairs[counts==1]
    ids,incidence=np.unique(boundary,return_counts=True)
    _need(5<=len(ids)<=7 and np.all(incidence==2), "broken bedrock foot loop")
    return points[ids]


def _feet(kind, tris):
    if kind in STONES:
        return _rim(tris)
    if kind=="tree":
        # The tree's first shell is its rooted trunk. Its six downward cap faces
        # share their first vertex; the neighbouring outward side faces do not.
        cap=tris[:18:3]
        _need(len(cap)==6 and np.max(abs(cap[:,0]-cap[0,0]))<EPS, "tree missing basal fan")
        coords=np.unique(cap.reshape(-1,3),axis=0)
        _need(len(coords)==7, "tree root fan lacks its perimeter")
        return coords
    if kind=="distant_fir":
        return tris[-2,:2]
    sides=(SPIKES|HANGERS)[kind]
    # A taper's attachment is the centroid of the actual polygonal cap. It can
    # legitimately lie inside a face. Hanging pieces follow their shelf's 12 faces.
    start=12 if kind in HANGERS else 0
    verts=np.unique(tris[start:start+sides-2].reshape(-1,3),axis=0)
    _need(len(verts)==sides,"incomplete attachment cap")
    return verts.mean(axis=0,keepdims=True)


def _near_pad(center,radius,pads):
    for pad in pads:
        points=np.asarray(pad,float)
        if np.any(center<points.min(axis=0)-radius) or np.any(center>points.max(axis=0)+radius):
            continue
        edge=np.roll(points,-1,axis=0)-points
        toward=center-points
        turn=edge[:,0]*toward[:,1]-edge[:,1]*toward[:,0]
        if np.all(turn>=-EPS) or np.all(turn<=EPS):
            return True
        t=np.clip(np.einsum("ij,ij->i",toward,edge)/np.einsum("ij,ij->i",edge,edge),0,1)
        if np.min(np.linalg.norm(toward-t[:,None]*edge,axis=1)) < radius-EPS:
            return True
    return False


def _stats():
    return dict(placed=0, blocked=0, outside=0, contacts=0, hostContacts=0,
                roofAnchors=0, missing=0, maxAirborneM=0., maxEmbeddingM=0.,
                maxRecordedHeightDifferenceM=0.)


def _reduce(dest, source):
    for key,value in source.items():
        if key.startswith("max"):
            dest[key]=max(dest[key],value)
        else:
            dest[key]+=value


def check_details(rec,row,path):
    """Return aggregate metre-space evidence, or raise on a missing or incorrect attachment."""
    xyz,_,sha=read_native(Path(path))
    _need(row["id"]==rec["id"] and len(xyz)==row["triangles"] and sha==row["sha256"],
          "WFH1 receipt does not match natural-contact scene")
    art=xyz[:,:,[0,2,1]].astype(float)
    start=int(row["surfaceTriangles"]); end=int(row["terrainTriangles"])
    _need(0<start<end<=len(art),"invalid natural triangle partition")
    terrain=Rays(art[:start])
    pads=[p["flatPad"] for p in row["grading"]["placements"]]
    stats={}
    last_host=None
    for data in row["details"]:
        kind=data["kind"]; cls=data["attachment"]; status=data["status"]
        ident=rec["id"]+"/"+kind+f"@{start}"
        num=data["triangle_count"]
        _need(kind in STONES|set(SPIKES)|set(HANGERS)|{"tree","distant_fir"}, ident+" unknown kind")
        _need(cls in {"floor","overhang","rock"} and status in {"placed","blocked","outside"},
              ident+" unknown attachment or state")
        _need(data["first_triangle"]==start and 0<=num<=end-start,ident+" non-contiguous range")
        counters=stats.setdefault(kind,_stats());counters[status]+=1
        if status!="placed":
            _need(num==0 and not data["contacts"],ident+" excluded object emitted geometry or contacts")
            last_host=None
            continue
        center=np.array((data["x"],data["y"]))
        _need(num>0 and not _near_pad(center,float(data["radius"]),pads),
              ident+" empty or intruding on an architectural pad")
        group=art[start:start+num]
        coords=_feet(kind,group)
        declared=np.asarray(data["contacts"],float)
        _need(declared.shape==(len(coords),4), ident+" incomplete or extra foot witnesses")
        # Compare coordinates to the *independently* extracted rim or cap. Neither
        # a stale height record nor a record sampled on the wrong triangle can pass.
        match=np.linalg.norm(coords[:,None,:]-declared[None,:,:3],axis=2)
        idx=match.argmin(axis=1)
        _need(len(np.unique(idx))==len(coords) and np.max(match[np.arange(len(idx)),idx])<EPS,
              ident+" missing emitted foot vertex/cap")
        if kind in HANGERS:
            _need(cls=="overhang",ident+" hanger treated as floor decoration")
            shelf=Rays(group[:2])
            a,b=group[0,:2]
            anchor=(a+b)/2
            recorded=np.asarray(data.get("terrain_anchor",[]),float)
            _need(recorded.shape==(4,) and np.linalg.norm(anchor-recorded[:3])<EPS,
                  ident+" rear shelf anchor absent from physical mesh")
            support=terrain_hit(terrain,anchor[:2])[1]
            _need(abs(support-recorded[3])<EPS and abs(support-anchor[2]-.19)<EPS,
                  ident+" shelf not joined to the ground")
            counters["roofAnchors"]+=1
        for pt,di in zip(coords,idx):
            ref=declared[di,3]
            surface_z=terrain_hit(terrain,pt[:2])[1]
            if kind in HANGERS:
                direct=terrain_hit(shelf,pt[:2])[1]
                clear=direct-pt[2]  # positive gap beneath the soffit
                counters["hostContacts"]+=1
                _need(abs(clear+.04)<EPS,ident+" pendant not embedded in its actual underside")
            elif kind in SPIKES:
                _need(last_host is not None,ident+" unattached shard has no preceding stone")
                hits=last_host.hits(pt[:2])
                direct=max(surface_z, hits[:,1].max()) if len(hits) else surface_z
                floor_only=direct<=surface_z+EPS
                _need((cls=="floor" and floor_only) or (cls=="rock" and len(hits)>0 and hits[:,1].max()>=surface_z-EPS),
                      ident+" wrong support kind")
                clear=pt[2]-direct;counters["hostContacts"]+=int(cls=="rock")
            else:
                _need(cls=="floor",ident+" unexpectedly free of terrain")
                direct=surface_z;clear=pt[2]-direct
            diff=abs(ref-direct)
            counters["contacts"]+=1
            counters["maxAirborneM"]=max(counters["maxAirborneM"],float(clear))
            counters["maxEmbeddingM"]=max(counters["maxEmbeddingM"],float(-clear))
            counters["maxRecordedHeightDifferenceM"]=max(counters["maxRecordedHeightDifferenceM"],float(diff))
            _need(diff<EPS and -.302<=clear<=EPS,
                  f"{ident} invalid support at {pt.tolist()}: actual clearance {clear:.5f}, reference error {diff:.5f}")
        last_host=Rays(group) if kind in STONES else None
        start+=num
    _need(start==end and stats,rec["id"]+" incomplete decoration partition")
    totals=_stats()
    for entry in stats.values():_reduce(totals,entry)
    _need(totals["placed"]>0 and totals["contacts"]>=totals["placed"],"empty decoration coverage")
    return dict(id=rec["id"], wfhSHA256=sha, byKind=stats, totals=totals)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for flag in ("catalog","report","live","studies","out"):
        parser.add_argument("--"+flag,type=Path,required=True)
    args=parser.parse_args()
    catalog=json.loads(args.catalog.read_text()); rows={r["id"]:r for r in json.loads(args.report.read_text())}
    _need({r["id"] for r in catalog}==set(rows),"incomplete level catalogue or report")
    scenes=[]; failures=[]
    for rec in catalog:
        key=rec["id"]
        where=args.studies if rec["planned"] else args.live
        try:
            scenes.append(check_details(rec,rows[key],where/(key+".bytes")))
        except (ValueError,KeyError,OSError) as exc:
            failures.append(dict(id=key,error=str(exc)))
    combined=_stats();by_kind={}
    for scene in scenes:
        _reduce(combined,scene["totals"])
        for kind,item in scene["byKind"].items():_reduce(by_kind.setdefault(kind,_stats()),item)
    report=dict(format="ash-crypt-natural-contacts-v1",units="metres; east, north, up",
                ok=not failures,reportSHA256=hashlib.sha256(args.report.read_bytes()).hexdigest(),
                catalogSHA256=hashlib.sha256(args.catalog.read_bytes()).hexdigest(),
                verifierSHA256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
                totals=combined,byKind=by_kind,scenes=scenes,failures=failures)
    args.out.write_text(json.dumps(report,separators=(",",":"),allow_nan=False)+"\n")
    print(json.dumps(dict(scenes=len(scenes),totals=combined,failures=failures),ensure_ascii=False))
    if failures:raise SystemExit(1)


if __name__=="__main__":
    main()
