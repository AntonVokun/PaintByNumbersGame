"""Read-only preflight for NEW flat paint-by-number sources.

Legacy RGB-splitting/gradient layers remain supported by the existing importer
when explicitly selected; this command never changes image files.
"""
import argparse
import json
import re
from pathlib import Path

import numpy as np
from PIL import Image


def validate(source: Path, expected_size: int = 1254):
    if not source.is_dir():
        raise ValueError(f"Source folder does not exist: {source}")
    zones = sorted(p for p in source.rglob('Zone_*.png') if p.is_file())
    outlines = sorted(p for p in source.rglob('outlines.png') if p.is_file())
    if not zones or len(outlines) != 1:
        raise ValueError(f"Expected Zone_*.png and exactly one outlines.png; found {len(zones)} zones, {len(outlines)} outlines")
    names = [p.name for p in zones]
    if len(names) != len(set(names)):
        raise ValueError('Ambiguous duplicate Zone_XXX.png names in source')
    zone_ids = []
    for name in names:
        match = re.fullmatch(r'Zone_(\d{3})\.png', name)
        if not match:
            raise ValueError(f'Invalid zone file name: {name}')
        zone_ids.append(int(match.group(1)))
    if sorted(zone_ids) != list(range(1, len(zones) + 1)):
        raise ValueError('Zone IDs must be consecutive, starting from 001')
    if not 12 <= len(zones) <= 45:
        raise ValueError(f'New artwork expects 12–45 logical colors, found {len(zones)}')

    with Image.open(outlines[0]) as im:
        if im.mode != 'RGBA' or im.size != (expected_size, expected_size):
            raise ValueError(f'Outline must be RGBA {expected_size}x{expected_size}')
        ink = np.asarray(im).copy()
    h, w = ink.shape[:2]
    coverage = np.zeros((h, w), dtype=np.uint8)
    group_ids = np.zeros((h, w), dtype=np.uint16)
    color_set = set()
    preview = Image.new('RGBA', (w, h), 'white')
    for group_idx, path in enumerate(zones, 1):
        with Image.open(path) as image:
            if image.mode != 'RGBA' or image.size != (w, h):
                raise ValueError(f'{path.name}: expected full-canvas RGBA {w}x{h}')
            rgba = np.asarray(image).copy()
        alpha = rgba[:, :, 3]
        if not np.all((alpha == 0) | (alpha == 255)):
            raise ValueError(f'{path.name}: alpha must be 0 or 255')
        mask = alpha == 255
        if not mask.any() or (~mask).sum() == 0:
            raise ValueError(f'{path.name}: empty zone or missing transparent exterior')
        distinct = np.unique(rgba[mask, :3], axis=0)
        if len(distinct) != 1:
            raise ValueError(f'{path.name}: expected exactly one opaque RGB, found {len(distinct)}')
        color = tuple(int(n) for n in distinct[0])
        if color in color_set:
            raise ValueError(f'{path.name}: duplicate RGB {color}; merge islands into a single zone')
        color_set.add(color)
        coverage[mask] += 1
        group_ids[mask] = group_idx
        preview = Image.alpha_composite(preview, Image.fromarray(rgba, 'RGBA'))
    if (coverage > 1).any():
        raise ValueError(f'Overlapping zones: {int(np.count_nonzero(coverage > 1))} pixels')
    if ((coverage != 0) & (ink[:, :, 3] != 0)).any():
        raise ValueError('Paint zones overlap nontransparent outlines')
    # Different playable colors must have a real opaque outline pixel between them.
    # Visible color-to-color adjacency is not a contour, even for perfect flat RGB.
    horizontal = ((group_ids[:, :-1] > 0) & (group_ids[:, 1:] > 0)
                  & (group_ids[:, :-1] != group_ids[:, 1:]))
    vertical = ((group_ids[:-1, :] > 0) & (group_ids[1:, :] > 0)
                & (group_ids[:-1, :] != group_ids[1:, :]))
    contacts = int(np.count_nonzero(horizontal) + np.count_nonzero(vertical))
    if contacts:
        raise ValueError(f'Unoutlined contacts between different paint zones: {contacts} pixel edges')
    # Use the same enclosed-hole definition as the existing importer.
    from prepare_level import interior_uncovered
    holes = interior_uncovered((coverage == 0) & (ink[:, :, 3] == 0))
    if holes.any():
        raise ValueError(f'Uncovered enclosed interior: {int(holes.sum())} pixels')
    preview = Image.alpha_composite(preview, Image.fromarray(ink, 'RGBA'))
    preview_files = sorted(p for p in source.rglob('Preview_Color.png') if p.is_file())
    if len(preview_files) > 1:
        raise ValueError('Ambiguous duplicate Preview_Color.png files')
    if preview_files:
        with Image.open(preview_files[0]) as ref:
            if ref.size != (w, h) or not np.array_equal(np.asarray(ref.convert('RGB')), np.asarray(preview.convert('RGB'))):
                raise ValueError('Flat preview does not match assembled zones + outline')
    return dict(status='PASS', scope='flat source preflight only', canvas=[w, h],
                colors=len(zones), unique_rgb=len(color_set), overlaps=0,
                unoutlined_contacts=0,
                enclosed_holes=0, preview='EXACT' if preview_files else 'NOT_PROVIDED',
                unity='NOT_RUN', android='NOT_RUN')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', required=True, help='Folder or unique Exchange subfolder name')
    parser.add_argument('--expected-size', type=int, default=1254)
    parser.add_argument('--exchange-root', default=r'C:\Users\Public\Documents\PaintByNumbers Exchange\уровни')
    args = parser.parse_args()
    from prepare_level import discover
    result = validate(discover(args.source, args.exchange_root), args.expected_size)
    print(json.dumps(result, ensure_ascii=False))
