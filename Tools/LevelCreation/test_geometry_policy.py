from pathlib import Path
import sys,unittest,importlib.util,numpy as np
from prepare_level import contours,geometry_paths
class GeometryRegression(unittest.TestCase):
 def test_legacy_no_option_is_identical(self):
  rng=np.random.default_rng(7)
  for _ in range(12):
   mask=rng.random((24,31))>.52
   self.assertEqual(contours(mask),geometry_paths(mask))
 def test_collinear_subdivision_preserves_original_vertices_and_area(self):
  mask=np.zeros((40,180),bool);mask[1:35,2:178]=1;mask[9:16,11:145]=0;mask[37,150:153]=1
  base=contours(mask);sub=geometry_paths(mask,16)
  self.assertEqual(len(base),len(sub))
  def area(v):return sum(a['x']*b['y']-b['x']*a['y'] for a,b in zip(v,v[1:]+v[:1]))/2
  for a,b in zip(base,sub):
   self.assertTrue(all(v in b for v in a));self.assertAlmostEqual(area(a),area(b),places=8)
   self.assertTrue(all(np.hypot(q['x']-p['x'],q['y']-p['y'])<=16.000001 for p,q in zip(b,b[1:]+b[:1])))
 def test_rectangles_exact_disjoint_cover_with_holes(self):
  rng=np.random.default_rng(61)
  for _ in range(12):
   mask=rng.random((35,40))>.4; count=np.zeros(mask.shape,int)
   for p in geometry_paths(mask,rectangles=True):
    a,b,c,d=p;count[round(a['y']):round(c['y']),round(a['x']):round(c['x'])]+=1
    self.assertEqual(round(a['x'])+.01,a['x']);self.assertEqual(round(c['x'])-.01,c['x'])
   np.testing.assert_array_equal(count,mask.astype(int))
 def test_invalid_values(self):
  for value in [0,-1,257,True,1.5,'32']:
   with self.assertRaises(ValueError):geometry_paths(np.ones((2,2),bool),value)
if __name__=='__main__':unittest.main()
