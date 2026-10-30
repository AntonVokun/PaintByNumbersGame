"""Synthetic regression checks for strict flat-color preflight; no Unity needed."""
import tempfile
import unittest
from pathlib import Path

import numpy as np
from PIL import Image
from check_flat_source import validate


class FlatSourceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.side = 36
        blank = np.zeros((self.side, self.side, 4), np.uint8)
        ink = blank.copy()
        ink[::3, :, :] = [20, 18, 18, 255]
        Image.fromarray(ink, 'RGBA').save(self.root / 'outlines.png')
        for idx in range(12):
            zone = blank.copy()
            zone[idx * 3 + 1:(idx + 1) * 3, :] = [(idx + 1) * 17, idx * 19, 50, 255]
            Image.fromarray(zone, 'RGBA').save(self.root / f'Zone_{idx + 1:03d}.png')

    def tearDown(self):
        self.temp.cleanup()

    def modify(self, name, changes):
        path = self.root / name
        data = np.asarray(Image.open(path)).copy()
        changes(data)
        Image.fromarray(data, 'RGBA').save(path)

    def test_valid_sources_and_matching_preview(self):
        self.assertEqual(validate(self.root, self.side)['status'], 'PASS')
        composed = Image.new('RGBA', (self.side, self.side), 'white')
        for path in sorted(self.root.glob('Zone_*.png')):
            composed = Image.alpha_composite(composed, Image.open(path).convert('RGBA'))
        composed = Image.alpha_composite(composed, Image.open(self.root / 'outlines.png').convert('RGBA'))
        composed.save(self.root / 'Preview_Color.png')
        self.assertEqual(validate(self.root, self.side)['preview'], 'EXACT')

    def test_gradient_rejected(self):
        self.modify('Zone_001.png', lambda a: a.__setitem__((1, 0, slice(None, 3)), [1, 2, 3]))
        with self.assertRaisesRegex(ValueError, 'exactly one opaque RGB'):
            validate(self.root, self.side)

    def test_overlap_rejected(self):
        self.modify('Zone_002.png', lambda a: a.__setitem__((1, 0), [34, 19, 50, 255]))
        with self.assertRaisesRegex(ValueError, 'Overlapping zones'):
            validate(self.root, self.side)

    def test_partial_alpha_rejected(self):
        self.modify('Zone_001.png', lambda a: a.__setitem__((1, 0, 3), 128))
        with self.assertRaisesRegex(ValueError, 'alpha must be 0 or 255'):
            validate(self.root, self.side)

    def test_duplicate_rgb_rejected(self):
        self.modify('Zone_002.png', lambda a: a.__setitem__((slice(4, 6), slice(None), slice(None, 3)), [17, 0, 50]))
        with self.assertRaisesRegex(ValueError, 'duplicate RGB'):
            validate(self.root, self.side)

    def test_missing_contour_rejected(self):
        # Remove ink separating two colors: detect the newly exposed color contact.
        self.modify('outlines.png', lambda a: a.__setitem__((3, slice(None)), [0, 0, 0, 0]))
        self.modify('Zone_002.png', lambda a: a.__setitem__((3, slice(None)), [34, 19, 50, 255]))
        with self.assertRaisesRegex(ValueError, 'Unoutlined contacts'):
            validate(self.root, self.side)


if __name__ == '__main__':
    unittest.main()
