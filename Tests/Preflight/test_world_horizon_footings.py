"""Reopen the shipped Unity meshes and measure the reported house and stilt failures."""
import json
import math
import struct
import sys
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
AUTHORING = ROOT / 'Tools/Art/weltferne'
sys.path.insert(0, str(AUTHORING))
from ground_anchors import sill_probes, support_probes


TRIANGLE = struct.Struct('<fff4xfff4xfff4x')
CASES = {'bellcourt': 'village', 'freight': 'village', 'ash_road': 'granary'}


def heights(triangles, east, north):
    """Barycentric intersections with the actual mesh, in Unity's Y-up axes."""
    hits = []
    for x, y, z, bx, by, bz, cx, cy, cz in triangles:
        if not min(x, bx, cx) <= east <= max(x, bx, cx) or not min(z, bz, cz) <= north <= max(z, bz, cz):
            continue
        dx, dz, ex, ez = bx-x, bz-z, cx-x, cz-z
        determinant = dx*ez-dz*ex
        if abs(determinant) < 1e-9:
            continue
        u = ((east-x)*ez-(north-z)*ex)/determinant
        v = (dx*(north-z)-dz*(east-x))/determinant
        if min(u,v) >= -1e-6 and u+v <= 1.000001:
            hits.append(y + u*(by-y) + v*(cy-y))
    return hits


class HorizonFootings(unittest.TestCase):
    def test_house_corners_thresholds_and_all_six_granary_feet(self):
        index = json.loads((AUTHORING/'footing-index.json').read_text())
        self.assertEqual(set(CASES), set(index))
        for scene, kind in CASES.items():
            raw = (ROOT/'Assets/Resources/Weltferne'/(scene+'.bytes')).read_bytes()
            magic, count = struct.unpack_from('<4sI', raw)
            self.assertEqual((magic, len(raw)), (b'WFH1', 8+count*TRIANGLE.size))
            partition = index[scene]
            member = partition['member']
            self.assertEqual(kind, member['kind'])
            scale = member['scale']
            angle = math.radians(member['turn'] - member['bearing'])
            first, length = member['firstTriangle'], member['triangleCount']
            self.assertLessEqual(partition['surfaceTriangles'], first)
            self.assertLessEqual(first+length, count)
            data = memoryview(raw)[8:]
            terrain = list(TRIANGLE.iter_unpack(data[:partition['surfaceTriangles']*TRIANGLE.size]))
            building = list(TRIANGLE.iter_unpack(data[first*TRIANGLE.size:(first+length)*TRIANGLE.size]))
            for role, probes in (('foot',support_probes(kind)),('sill',sill_probes(kind))):
                self.assertTrue(probes)
                for label, x, z, authored_height in probes:
                    with self.subTest(scene=scene, role=role, point=label):
                        east = member['x'] + scale*(math.cos(angle)*x - math.sin(angle)*z)
                        north = member['y'] + scale*(math.sin(angle)*x + math.cos(angle)*z)
                        ground = heights(terrain, east, north)
                        faces = heights(building, east, north)
                        self.assertTrue(ground and faces, 'Missing terrain or structural face under probe')
                        expected = member['originZ'] + scale*authored_height
                        actual = min(faces, key=lambda h: abs(h-expected))
                        self.assertAlmostEqual(actual, expected, delta=.005)
                        clearance = actual - max(ground)
                        if role == 'foot':
                            self.assertLessEqual(clearance, .03, 'Foot floating above the actual ground')
                            burial = max(0., -authored_height*scale)
                            self.assertGreaterEqual(clearance, -burial-.05, 'Foot buried beyond its designed sole')
                        else:
                            self.assertGreater(clearance, .04, 'Threshold hidden by terrain')
