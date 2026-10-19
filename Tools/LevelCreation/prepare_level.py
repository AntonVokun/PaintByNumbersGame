"""Prepare aligned, full-canvas coloring layers. Requires Pillow and numpy.
Never crops, resizes, or modifies source files. See LEVEL_CREATION_GUIDE.md.
"""
from pathlib import Path
import argparse
import hashlib
import json
import io
from collections import defaultdict, deque
import numpy as np
from PIL import Image


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def checked_write(path, content):
    path = Path(path)
    if path.exists():
        if path.read_bytes() != content:
            raise ValueError(f'Differing existing file; review before updating (meta/GUID retained): {path}')
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(content)


def png_bytes(image):
    stream = io.BytesIO()
    image.save(stream, format='PNG')
    return stream.getvalue()


def discover(source, exchange_root):
    supplied = Path(source)
    if supplied.is_dir():
        return supplied.resolve()
    if supplied.is_file():
        return supplied.resolve().parent
    root = Path(exchange_root)
    matches = [p for p in root.iterdir() if p.is_dir() and source.casefold() in p.name.casefold()]
    if len(matches) != 1:
        raise ValueError(f'Expected exactly one source folder for {source!r}; candidates: {[p.name for p in matches]}')
    return matches[0].resolve()


def runs_and_samples(mask):
    """4-connected visible islands; one guaranteed interior pixel per island."""
    parent, sizes, best = [], [], []
    previous = []

    def root(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i

    for y, row in enumerate(mask):
        changes = np.diff(np.pad(row.astype(np.int8), (1, 1)))
        starts, ends = np.where(changes == 1)[0], np.where(changes == -1)[0]
        current = []
        j = 0
        for x0, x1 in zip(starts.tolist(), ends.tolist()):
            i = len(parent)
            parent.append(i)
            sizes.append(x1 - x0)
            best.append((x1 - x0, (x0 + x1 - 1) // 2, y))
            while j < len(previous) and previous[j][1] <= x0:
                j += 1
            k = j
            while k < len(previous) and previous[k][0] < x1:
                r = root(previous[k][2])
                q = root(i)
                if r != q:
                    parent[r] = q
                    sizes[q] += sizes[r]
                    best[q] = max(best[q], best[r])
                k += 1
            current.append((x0, x1, i))
        previous = current
    return [dict(x=best[i][1], y=best[i][2], pixels=sizes[i])
            for i in range(len(parent)) if root(i) == i]


def interior_uncovered(missing):
    """Ignore transparent exterior connected to the canvas edge; reject enclosed gaps."""
    interior = missing.copy()
    h, w = interior.shape
    queue = deque()
    def visit(y, x):
        if interior[y, x]:
            interior[y, x] = False
            queue.append((y, x))
    for x in range(w):
        visit(0, x)
        visit(h - 1, x)
    for y in range(h):
        visit(y, 0)
        visit(y, w - 1)
    while queue:
        y, x = queue.popleft()
        if y > 0: visit(y - 1, x)
        if y + 1 < h: visit(y + 1, x)
        if x > 0: visit(y, x - 1)
        if x + 1 < w: visit(y, x + 1)
    return interior


def simplify_open(points, epsilon):
    if len(points) <= 2:
        return points
    pts = np.asarray(points, dtype=float)
    a, b = pts[0], pts[-1]
    v = b - a
    if np.dot(v, v) == 0:
        distance = np.linalg.norm(pts - a, axis=1)
    else:
        t = np.clip((pts - a) @ v / np.dot(v, v), 0, 1)
        distance = np.linalg.norm(pts - (a + t[:, None] * v), axis=1)
    index = int(np.argmax(distance))
    if distance[index] <= epsilon:
        return [points[0], points[-1]]
    return simplify_open(points[:index + 1], epsilon)[:-1] + simplify_open(points[index:], epsilon)


def contours(mask):
    """Pixel-cell boundaries, including holes with opposite winding. 0.45px tolerance."""
    edges = defaultdict(list)
    h, w = mask.shape
    # Directed edges keep filled pixels on the right in image coordinates.
    padded = np.pad(mask, 1)
    for dy, dx, side in [(-1, 0, 0), (0, 1, 1), (1, 0, 2), (0, -1, 3)]:
        boundary = mask & ~padded[1 + dy:1 + dy + h, 1 + dx:1 + dx + w]
        ys, xs = np.where(boundary)
        for y, x in zip(ys.tolist(), xs.tolist()):
            a, b = [((x, y), (x + 1, y)), ((x + 1, y), (x + 1, y + 1)),
                    ((x + 1, y + 1), (x, y + 1)), ((x, y + 1), (x, y))][side]
            edges[a].append((b, side))
    paths = []
    while edges:
        start = next(iter(edges))
        point, direction = edges[start].pop()
        if not edges[start]:
            del edges[start]
        path = [start]
        while point != start:
            path.append(point)
            choices = edges[point]
            # At a diagonal contact follow the right turn to avoid crossing paths.
            rank = {1: 0, 0: 1, 3: 2, 2: 3}
            k = min(range(len(choices)), key=lambda i: rank[(choices[i][1] - direction) % 4])
            next_point, direction = choices.pop(k)
            if not choices:
                del edges[point]
            point = next_point
        path = [p for i, p in enumerate(path)
                if ((p[0] - path[i - 1][0]) * (path[(i + 1) % len(path)][1] - p[1])
                    != (p[1] - path[i - 1][1]) * (path[(i + 1) % len(path)][0] - p[0]))]
        if len(path) < 3:
            continue
        split = max(range(len(path)), key=lambda i: (path[i][0]-path[0][0])**2 + (path[i][1]-path[0][1])**2)
        reduced = simplify_open(path[:split + 1], 0.45)[:-1] + simplify_open(path[split:] + [path[0]], 0.45)[:-1]
        if len(reduced) >= 3:
            paths.append([dict(x=x, y=y) for x, y in reduced])
    return paths


def geometry_paths(mask, max_edge_pixels=None, rectangles=False):
    """Opt-in native geometry repairs; preserve every source pixel center.

    Long, nearly parallel edges can produce native sliver triangles. Use only
    for explicitly reviewed source layers; the legacy path list stays exact
    when this option is absent. No pixels, islands or group IDs are changed.
    """
    if rectangles:
        # Maximal equal horizontal runs, inset .01px to avoid native touching-path artefacts.
        # PNGs are untouched; every occupied pixel center remains inside its rectangle.
        active = {}; result = []
        for y in range(mask.shape[0] + 1):
            row = mask[y] if y < mask.shape[0] else np.zeros(mask.shape[1], bool)
            edges = np.flatnonzero(np.diff(np.r_[False, row, False].astype(np.int8)))
            current = {(int(a), int(b)) for a, b in zip(edges[::2], edges[1::2])}
            for (a, b), top in list(active.items()):
                if (a, b) not in current:
                    result.append([dict(x=a+.01,y=top+.01),dict(x=b-.01,y=top+.01),dict(x=b-.01,y=y-.01),dict(x=a+.01,y=y-.01)])
                    del active[(a, b)]
            for run in sorted(current):
                active.setdefault(run, y)
        return result
    paths = contours(mask)
    if max_edge_pixels is None:
        return paths
    if type(max_edge_pixels) is not int or not 1 <= max_edge_pixels <= 256:
        raise ValueError('geometryMaxEdgePixels must be an integer in 1..256')
    result = []
    for path in paths:
        points = []
        for a, b in zip(path, path[1:] + path[:1]):
            count = max(1, int(np.ceil(np.hypot(b['x'] - a['x'], b['y'] - a['y']) / max_edge_pixels)))
            for i in range(count):
                points.append(dict(x=a['x'] + (b['x'] - a['x']) * i / count,
                                   y=a['y'] + (b['y'] - a['y']) * i / count))
        result.append(points)
    return result


def palette_rgb(value):
    if not isinstance(value, list) or len(value) != 3 or any(type(v) is not int or not 0 <= v <= 255 for v in value):
        raise ValueError('Palette RGB must contain three integers in 0..255')
    return value


def representative_color(rgba):
    """Nearest actual opaque RGB to the per-channel median; deterministic and robust."""
    pixels = rgba[:, :, :3][rgba[:, :, 3] == 255]
    if not len(pixels):
        raise ValueError('No opaque pixels for a representative palette color')
    colors = np.unique(pixels, axis=0)
    median = np.median(pixels, axis=0)
    return colors[((colors - median) ** 2).sum(axis=1).argmin()].tolist()


def palette_lab(rgb):
    """sRGB -> CIELAB, D65 white; used only for palette review, never artwork."""
    c = np.asarray(rgb, dtype=float) / 255
    c = np.where(c <= .04045, c / 12.92, ((c + .055) / 1.055) ** 2.4)
    xyz = np.array([[.4124564,.3575761,.1804375], [.2126729,.7151522,.0721750], [.0193339,.1191920,.9503041]]) @ c
    xyz /= [.95047, 1, 1.08883]
    f = np.where(xyz > (6/29)**3, np.cbrt(xyz), xyz / (3*(6/29)**2) + 4/29)
    return np.array([116*f[1]-16, 500*(f[0]-f[1]), 200*(f[1]-f[2])])


def review_palette(groups, config):
    """Near palette candidates are not duplicates. Only explicit config can merge."""
    by_source = {g['source']: g for g in groups}
    approvals = {}
    approved = config.get('approvedClosePairs', [])
    if not isinstance(approved, list) or any(not isinstance(item, dict) for item in approved):
        raise ValueError('approvedClosePairs must be a list of explicit approval objects')
    for item in approved:
        names, colors = item.get('sources'), item.get('colors')
        if not isinstance(names, list) or len(names) != 2 or any(not isinstance(n,str) for n in names) or len(set(names)) != 2 or any(n not in by_source for n in names):
            raise ValueError('Close-pair approval must name two current source layers')
        if not isinstance(colors, list) or len(colors) != 2:
            raise ValueError('Close-pair approval must bind both exact palette RGB values')
        colors = [palette_rgb(c) for c in colors]
        if any(by_source[n]['color'] != c for n, c in zip(names, colors)):
            raise ValueError('Stale close-pair approval: palette RGB changed')
        key = tuple(sorted(names))
        if key in approvals:
            raise ValueError('Duplicate close-pair approval')
        approvals[key] = item
    close = []
    for index, a in enumerate(groups):
        for b in groups[index + 1:]:
            distance = float(np.linalg.norm(palette_lab(a['color']) - palette_lab(b['color'])))
            if distance < 6:
                close.append(dict(sources=[a['source'], b['source']], colors=[a['color'], b['color']],
                                  distance=round(distance, 6), approved=tuple(sorted([a['source'], b['source']])) in approvals))
    if any(not pair['approved'] for pair in close):
        raise ValueError('Palette review required; approve exact pairs or explicitly set palette RGB: ' + json.dumps(close))
    return dict(metric='CIE76 sRGB D65 (review candidates, not duplicates)', minDistance=6, closePairs=close, status='PASS')


def prepare(source, output, scene_name, display_name, validate_only=False, grouping_mode=None, group_config=None):
    config = {} if group_config is None else group_config
    if not isinstance(config, dict) or set(config) - {'groupingMode', 'colors', 'approvedClosePairs', 'sourceSha256', 'mergeGroups', 'geometryMaxEdgePixels', 'geometryRectangles'}:
        raise ValueError('Invalid group configuration')
    grouping_mode = grouping_mode or config.get('groupingMode', 'rgb')
    if grouping_mode not in ('rgb', 'layer'):
        raise ValueError('Grouping mode must be rgb or layer')
    if config.get('groupingMode', grouping_mode) != grouping_mode:
        raise ValueError('Conflicting grouping modes')
    if grouping_mode == 'rgb' and config:
        raise ValueError('Palette/layer configuration requires explicit layer mode')
    source, output = Path(source).resolve(), Path(output).resolve()
    outlines = list(source.rglob('outlines.png'))
    if len(outlines) != 1:
        raise ValueError(f'Expected one unambiguous outlines.png; found {len(outlines)}')
    outline_path = outlines[0]
    zone_files = sorted(outline_path.parent.glob('Zone_*.png'))
    if not zone_files:
        raise ValueError('Expected outlines.png and Zone_*.png; single-image segmentation needs reviewed masks first.')
    names = {f.relative_to(source).as_posix() for f in zone_files}
    geometry_edges = config.get('geometryMaxEdgePixels', {})
    if not isinstance(geometry_edges, dict) or set(geometry_edges) - names:
        raise ValueError('Geometry subdivision must name existing source layers')
    if any(type(v) is not int or not 1 <= v <= 256 for v in geometry_edges.values()):
        raise ValueError('geometryMaxEdgePixels must contain integers in 1..256')
    rectangle_sources = config.get('geometryRectangles', [])
    if not isinstance(rectangle_sources, list) or any(not isinstance(n, str) or n not in names for n in rectangle_sources) or len(set(rectangle_sources)) != len(rectangle_sources):
        raise ValueError('geometryRectangles must list distinct existing source layers')
    overrides = config.get('colors', {})
    if not isinstance(overrides, dict) or set(overrides) - names:
        raise ValueError('Palette overrides must name existing source layers')
    for color in overrides.values():
        palette_rgb(color)
    pinned = config.get('sourceSha256')
    if pinned is not None and (not isinstance(pinned, dict) or set(pinned) != names or any(pinned[f.relative_to(source).as_posix()] != sha(f) for f in zone_files)):
        raise ValueError('Reviewed source hashes changed or layer set differs')
    merge_groups = config.get('mergeGroups', [])
    if not isinstance(merge_groups, list):
        raise ValueError('mergeGroups must be a list of explicit source-layer lists')
    merged_names = set()
    order = {f.relative_to(source).as_posix(): i for i, f in enumerate(zone_files)}
    for members in merge_groups:
        if not isinstance(members, list) or len(members) < 2 or any(not isinstance(n, str) or n not in names for n in members) or len(set(members)) != len(members):
            raise ValueError('Merge must name at least two different existing source layers')
        if merged_names.intersection(members):
            raise ValueError('A source layer cannot belong to multiple merged groups')
        positions = [order[n] for n in members]
        if positions != list(range(min(positions), max(positions)+1)):
            raise ValueError('Merge sources must be consecutive in original layer order')
        merged_names.update(members)
    if set(overrides).intersection(merged_names - {v[0] for v in merge_groups}):
        raise ValueError('A merged palette override must use its first source layer')
    if (set(geometry_edges) | set(rectangle_sources)).intersection(merged_names - {v[0] for v in merge_groups}):
        raise ValueError('Merged geometry setting must use its first source layer')
    outline = np.array(Image.open(outline_path).convert('RGBA'))
    h, w = outline.shape[:2]
    groups, sources = [], []
    pending_merge = {members[0]: members for members in merge_groups}
    skipped_merge_members = merged_names - set(pending_merge)
    for file in zone_files:
        if file.relative_to(source).as_posix() in skipped_merge_members:
            continue
        if grouping_mode == 'layer':
            with Image.open(file) as image:
                if image.mode != 'RGBA':
                    raise ValueError(f'{file.name}: layer mode requires RGBA PNG for exact byte preservation')
        rgba = np.array(Image.open(file).convert('RGBA'))
        if rgba.shape != outline.shape:
            raise ValueError(f'Canvas mismatch: {file.name}: {rgba.shape}, expected {outline.shape}')
        alpha = rgba[:, :, 3]
        if alpha.min() != 0 or alpha.max() != 255:
            raise ValueError(f'Missing transparent exterior or opaque interior: {file.name}')
        colors, counts = np.unique(rgba[:, :, :3][alpha == 255], axis=0, return_counts=True)
        sources.append(dict(file=file.name, sha256=sha(file), opaqueColors=len(colors)))
        if grouping_mode == 'layer':
            name = file.relative_to(source).as_posix()
            members = pending_merge.get(name, [name])
            source_records = [dict(source=name, sha256=sha(file), rgbaSha256=hashlib.sha256(rgba.tobytes()).hexdigest())]
            for member in members[1:]:
                member_file = source/member
                with Image.open(member_file) as im:
                    if im.mode != 'RGBA':
                        raise ValueError(f'{member}: merged layer requires RGBA')
                    extra = np.array(im)
                if extra.shape != rgba.shape or extra[:,:,3].min() != 0 or extra[:,:,3].max() != 255:
                    raise ValueError(f'{member}: incompatible merged canvas/alpha')
                if ((rgba[:,:,3] > 0) & (extra[:,:,3] > 0)).any():
                    raise ValueError('Merge has overlapping nonzero alpha; exact per-source RGBA cannot be preserved')
                source_records.append(dict(source=member, sha256=sha(member_file), rgbaSha256=hashlib.sha256(extra.tobytes()).hexdigest()))
                rgba[extra[:,:,3] > 0] = extra[extra[:,:,3] > 0]
                sources.append(dict(file=member_file.name, sha256=sha(member_file), opaqueColors=len(np.unique(extra[:,:,:3][extra[:,:,3]==255],axis=0))))
            color = overrides.get(name, representative_color(rgba))
            groups.append((rgba, dict(id=len(groups)+1, source=name, color=color,
                type='gradient', paletteMethod='explicit' if name in overrides else 'opaque_median_nearest',
                sourceSha256=sha(file), rgbaSha256=hashlib.sha256(rgba.tobytes()).hexdigest(),
                sources=source_records, mergeMethod='disjoint_rgba_union' if len(members)>1 else 'source_bytes',
                regions=0, samples=[], paths=[])))
            continue
        # Ready flat artwork: refuse ambiguous gradients instead of silently quantizing.
        if len(colors) > 64:
            raise ValueError(f'{file.name}: {len(colors)} opaque colors; explicit grouping/gradient configuration required.')
        colors = colors[np.argsort(-counts)]
        distances = ((rgba[:, :, None, :3].astype(np.int32) - colors[None, None].astype(np.int32)) ** 2).sum(axis=3)
        labels = distances.argmin(axis=2)
        for k, rgb in enumerate(colors):
            image = rgba.copy()
            image[:, :, 3] = np.where(labels == k, alpha, 0)
            # Color bleed under fully transparent pixels; no matte-white fringes.
            image[:, :, :3] = rgb
            groups.append((image, dict(id=len(groups)+1, source=file.relative_to(source).as_posix(), color=rgb.tolist(),
                type='flat', regions=0, samples=[], paths=[])))
    palette_review = review_palette([g for _, g in groups], config) if grouping_mode == 'layer' else None
    alpha = np.stack([a[:, :, 3] for a, _ in groups])
    overlap = int(((alpha >= 200).sum(axis=0) > 1).sum())
    uncovered = int(interior_uncovered((alpha.max(axis=0) == 0) & (outline[:, :, 3] == 0)).sum())
    if overlap or uncovered:
        raise ValueError(f'Invalid layer coverage: {overlap} overlapping solid pixels, {uncovered} uncovered pixels')
    # Antialiased shared borders under the opaque outline belong to one collider only.
    owner = alpha.argmax(axis=0)
    for index, (image, group) in enumerate(groups):
        mask = (image[:, :, 3] >= 128) & (owner == index)
        samples = runs_and_samples(mask)
        group.update(regions=len(samples), samples=samples,
                     paths=[dict(points=p) for p in geometry_paths(mask, geometry_edges.get(group['source']), group['source'] in rectangle_sources)])
    composed = Image.new('RGBA', (w, h), 'white')
    for image, _ in groups:
        composed = Image.alpha_composite(composed, Image.fromarray(image))
    composed = Image.alpha_composite(composed, Image.fromarray(outline))
    bw = Image.alpha_composite(Image.new('RGBA', (w, h), 'white'), Image.fromarray(outline))
    archive = Path('ArtSources') / output.name
    hitmask_file = (archive / 'Working/hitmask.bytes').as_posix()
    # Select the closest supplied full-canvas color preview, never use it to realign masks.
    candidates=[]
    for file in source.rglob('*.png'):
        if file in zone_files or file == outline_path:
            continue
        im=Image.open(file).convert('RGB')
        if im.size == (w,h):
            difference=np.abs(np.array(im).astype(float)-np.array(composed.convert('RGB')).astype(float)).mean()
            candidates.append((float(difference),file.relative_to(source).as_posix()))
    candidates.sort()
    if not candidates or candidates[0][0] > 15:
        raise ValueError('No color reference consistent with prepared layers; review source set')
    original_files=[dict(file=(archive/'Original'/f.relative_to(source)).as_posix(),sha256=sha(f))
        for f in sorted(source.rglob('*')) if f.is_file() and f.suffix.lower() in ('.ai','.png','.jpg','.jpeg','.svg','.psd','.pdf','.tif','.tiff')]
    data = dict(schema=1, sceneName=scene_name, displayName=display_name, width=w, height=h,
        pixelsPerUnit=100, pivot=dict(x=0.5, y=0.5), sourceFolder=str(source),
        levelNumber=int(''.join(c for c in scene_name if c.isdigit())), artFolder=output.relative_to(Path.cwd().resolve()).as_posix(),
        originalImage=(archive/'Original'/candidates[0][1]).as_posix(),
        outlineFile='Outline/outlines.png', colorPreviewFile='Previews/Preview_Color.png',
        bwPreviewFile='Previews/Preview_BW.png', hitmaskFile=hitmask_file,
        geometryFile=(archive/'Working/geometry.json').as_posix(), hitmaskBytesPerPixel=2,
        originalFiles=original_files,
        sourceFiles=sources + [dict(file=outline_path.name, sha256=sha(outline_path))],
        colors=len(groups), regions=sum(g['regions'] for _, g in groups),
        solidOverlapPixels=overlap, uncoveredPixels=uncovered,
        validation=dict(preparedFiles='PASS',canvas='PASS',alpha='PASS',overlap='PASS',coverage='PASS',
                        unityStatic='run separately',playMode='run separately',androidBuild='run separately'), groups=[])
    data['groupingMode'] = grouping_mode
    if grouping_mode == 'layer':
        data['groupConfig'] = config
        data['paletteReview'] = palette_review
    manifest=archive/'level_manifest.json'
    old=json.loads(manifest.read_text(encoding='utf-8')) if manifest.exists() else None
    if old is not None:
        for field in ('sceneName','width','height','pixelsPerUnit','colors','regions'):
            if old.get(field) != data[field]:
                raise ValueError(f'Manifest identity changed: {field}; review before updating')
        if old.get('groupingMode','rgb') != grouping_mode or [(g.get('type','flat'),g['color']) for g in old['groups']] != [(g['type'],g['color']) for _,g in groups]:
            raise ValueError('Existing grouping mode or palette changed; review migration before updating')
    if not validate_only:
        for original in source.rglob('*'):
            if original.is_file() and original.suffix.lower() in ('.ai','.png','.jpg','.jpeg','.svg','.psd','.pdf','.tif','.tiff'):
                checked_write(archive/'Original'/original.relative_to(source),original.read_bytes())
        for image, group in groups:
            group['file'] = f"Zones/Zone_{group['id']:03d}.png"
            exact_source = group['type'] == 'gradient' and len(group['sources']) == 1
            checked_write(output/group['file'],(source/group['source']).read_bytes() if exact_source else png_bytes(Image.fromarray(image)))
            group['sha256'] = sha(output / group['file'])
            data['groups'].append({k:v for k,v in group.items() if k not in ('paths','samples')})
        checked_write(output/data['outlineFile'],png_bytes(Image.fromarray(outline)))
        checked_write(output/data['colorPreviewFile'],png_bytes(composed))
        checked_write(output/data['bwPreviewFile'],png_bytes(bw))
        checked_write(hitmask_file,np.where(alpha.max(axis=0) >= 200, owner + 1, 0).astype('<u2').tobytes())
        checked_write(data['geometryFile'],json.dumps(dict(groups=[dict(id=g['id'],paths=g['paths'],samples=g['samples']) for _,g in groups]),separators=(',', ':')).encode('utf-8'))
        # Keep recorded Unity/Play/build outcomes on a byte-identical preparation rerun.
        if old is not None:
            if [(g['id'],g['file'],g['sha256']) for g in old['groups']] != [(g['id'],g['file'],g['sha256']) for g in data['groups']]:
                raise ValueError('Existing manifest describes different sprite contents; review before updating')
            data['validation']=old.get('validation',data['validation'])
        # Only metadata may change here: all sprite files and immutable identity were checked.
        manifest.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(dict(canvas=[w,h], colors=len(groups), regions=data['regions'],
        colliderPaths=sum(len(g['paths']) for _,g in groups), overlaps=overlap, uncovered=uncovered,
        output=str(output), validationOnly=validate_only), ensure_ascii=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', required=True)
    parser.add_argument('--exchange-root', default=r'C:\Users\Public\Documents\PaintByNumbers Exchange\уровни')
    parser.add_argument('--output', required=True)
    parser.add_argument('--scene', required=True)
    parser.add_argument('--title', required=True)
    parser.add_argument('--validate-only', action='store_true')
    parser.add_argument('--grouping-mode', choices=('rgb','layer'), help='Explicit layer mode preserves each source PNG as one group; default rgb splits flat colors.')
    parser.add_argument('--group-config', help='JSON palette overrides, reviewed source hashes and exact close-pair approvals.')
    args = parser.parse_args()
    config = json.loads(Path(args.group_config).read_text(encoding='utf-8-sig')) if args.group_config else None
    prepare(discover(args.source,args.exchange_root), args.output, args.scene, args.title, args.validate_only, args.grouping_mode, config)
