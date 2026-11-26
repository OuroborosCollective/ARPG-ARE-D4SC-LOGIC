"""Geometry regressions on the real Ashen Crypt art catalogue; no Unity runtime claims."""
import hashlib
import unittest

import numpy as np

import build
import grounding
import landscape
from world_catalog import catalog


def compile_seats(key):
    rec = next(item for item in catalog if item["id"] == key)
    b = build.Builder()
    seed = int.from_bytes(hashlib.sha256(("weltferne/1/"+key).encode()).digest()[:4],"little")
    landscape.terrain_surface(b,rec["style"],seed)
    marks = rec["landmarks"] + rec["dressing"]
    assets = {kind:build.make_landmark(kind) for kind in dict.fromkeys(m["kind"] for m in marks)}
    plan = grounding.prepare(b,marks,assets)
    plan.append(b,assets)
    return b,plan


def boundary_lines(triangles):
    vertices,ids = np.unique(triangles.reshape(-1,3),axis=0,return_inverse=True)
    faces = ids.reshape(-1,3)
    edges = np.stack((faces,np.roll(faces,-1,axis=1)),axis=2).reshape(-1,2)
    keys,which,counts = np.unique(np.sort(edges,axis=1),axis=0,return_inverse=True,return_counts=True)
    if np.any(counts > 2):
        raise AssertionError("Overlapping terrain faces or a non-manifold edge")
    signs = np.where(edges[:,0]<edges[:,1],1,-1)
    signed = np.bincount(which,weights=signs)
    if np.any(signed[counts==2] != 0):
        raise AssertionError("Adjacent terrain triangles have inconsistent winding")
    return vertices[keys[counts==1]]


class TestHorizonGrounding(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.builder,cls.plan = compile_seats("forecourt")

    def test_old_abbey_and_bell_were_buried_or_floating(self):
        bell,abbey = [next(p for p in self.plan.placements if p.kind==k) for k in ("bell","abbey")]
        self.assertLess(bell.original_max,-.25)
        self.assertGreater(abbey.original_min,4.)
        for p in (bell,abbey):
            points = grounding._samples(p.pad,.71)
            self.assertLess(np.max(np.abs(self.plan.surface.heights(points)-p.pad_z)),1.e-6)
            self.assertAlmostEqual(p.pad_z-p.origin_z,p.local_base*p.scale+grounding.INSET_M)

    def test_closed_annulus_and_earthworks_boundary_survive(self):
        before = self.plan.before
        after = self.plan.surface
        b0,b1 = boundary_lines(before.triangles),boundary_lines(after.triangles)
        # No new edges facing open air may appear at a cut or feather. The two
        # original perimeter loops may be split into shorter collinear segments.
        segments = b0[:,1] - b0[:,0]
        denominator = np.sum(segments*segments,axis=1)
        for point in b1.mean(axis=1):
            t = np.clip(((point-b0[:,0])*segments).sum(axis=1)/denominator,0.,1.)
            nearest = b0[:,0] + segments*t[:,None]
            self.assertLess(np.linalg.norm(nearest-point,axis=1).min(),1.e-7)
        self.assertAlmostEqual(np.linalg.norm(b0[:,1]-b0[:,0],axis=1).sum(),
                               np.linalg.norm(b1[:,1]-b1[:,0],axis=1).sum(),places=7)
        self.assertEqual(self.plan.stats["unchangedOutsideMaxDeltaM"],0.)
        self.assertIsNone(after.height(0.,0.))

    def test_same_bearing_and_append_ranges_match_real_models(self):
        original = next(item for item in catalog if item["id"]=="forecourt")
        for p,m in zip(self.plan.placements,original["landmarks"]+original["dressing"]):
            theta = np.deg2rad(m["bearing"])
            np.testing.assert_allclose([p.x,p.y],p.distance*np.array([np.sin(theta),np.cos(theta)]))
            shape = self.builder.tris[p.triangle_start:p.triangle_start+p.triangle_count]
            self.assertGreater(len(shape),0)
            self.assertEqual(p.triangle_count,len(build.make_landmark(p.kind).tris))
            self.assertGreater(p.screening,0.)
            for q in self.plan.placements:
                if p is not q:
                    self.assertGreaterEqual(grounding._separation(p.pad,q.pad),.45-1.e-6)

    def test_arrangement_search_resolves_the_hidden_crypt_approach(self):
        _,plan = compile_seats("hidden_crypt")
        wall = next(p for p in plan.placements if p.kind=="ossuary_wall")
        self.assertLess(wall.residual,1.e-7)
        self.assertEqual(wall.bearing,343)
        self.assertGreater(wall.distance,wall.original_distance)
        # Trees with a canopy over the wall's actual base must be excluded.
        self.assertTrue(plan.blocked(*wall.pad.mean(axis=0),2.1))


if __name__ == "__main__":
    unittest.main()
