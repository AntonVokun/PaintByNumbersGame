"""Prepare aligned, full-canvas coloring layers. Requires Pillow and numpy.
Never crops, resizes, or modifies source files. See LEVEL_CREATION_GUIDE.md.
"""
from pathlib import Path
import argparse
import hashlib
import json
import io
from collections import defaultdict
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


def prepare(source, output, scene_name, display_name, validate_only=False):
    source, output = Path(source).resolve(), Path(output).resolve()
    outlines = list(source.rglob('outlines.png'))
    if len(outlines) != 1:
        raise ValueError(f'Expected one unambiguous outlines.png; found {len(outlines)}')
    outline_path = outlines[0]
    zone_files = sorted(outline_path.parent.glob('Zone_*.png'))
    if not zone_files:
        raise ValueError('Expected outlines.png and Zone_*.png; single-image segmentation needs reviewed masks first.')
    outline = np.array(Image.open(outline_path).convert('RGBA'))
    h, w = outline.shape[:2]
    groups, sources = [], []
    for file in zone_files:
        rgba = np.array(Image.open(file).convert('RGBA'))
        if rgba.shape != outline.shape:
            raise ValueError(f'Canvas mismatch: {file.name}: {rgba.shape}, expected {outline.shape}')
        alpha = rgba[:, :, 3]
        if alpha.min() != 0 or alpha.max() != 255:
            raise ValueError(f'Missing transparent exterior or opaque interior: {file.name}')
        colors, counts = np.unique(rgba[:, :, :3][alpha == 255], axis=0, return_counts=True)
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
                regions=0, samples=[], paths=[])))
        sources.append(dict(file=file.name, sha256=sha(file), opaqueColors=len(colors)))
    alpha = np.stack([a[:, :, 3] for a, _ in groups])
    overlap = int(((alpha >= 200).sum(axis=0) > 1).sum())
    uncovered = int(((alpha.max(axis=0) == 0) & (outline[:, :, 3] == 0)).sum())
    if overlap or uncovered:
        raise ValueError(f'Invalid layer coverage: {overlap} overlapping solid pixels, {uncovered} uncovered pixels')
    # Antialiased shared borders under the opaque outline belong to one collider only.
    owner = alpha.argmax(axis=0)
    for index, (image, group) in enumerate(groups):
        mask = (image[:, :, 3] >= 128) & (owner == index)
        samples = runs_and_samples(mask)
        group.update(regions=len(samples), samples=samples,
                     paths=[dict(points=p) for p in contours(mask)])
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
        levelNumber=int(''.join(c for c in scene_name if c.isdigit())), artFolder=output.relative_to(Path.cwd()).as_posix(),
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
    if not validate_only:
        for original in source.rglob('*'):
            if original.is_file() and original.suffix.lower() in ('.ai','.png','.jpg','.jpeg','.svg','.psd','.pdf','.tif','.tiff'):
                checked_write(archive/'Original'/original.relative_to(source),original.read_bytes())
        for image, group in groups:
            group['file'] = f"Zones/Zone_{group['id']:03d}.png"
            checked_write(output/group['file'],png_bytes(Image.fromarray(image)))
            group['sha256'] = sha(output / group['file'])
            data['groups'].append({k:v for k,v in group.items() if k not in ('paths','samples')})
        checked_write(output/data['outlineFile'],png_bytes(Image.fromarray(outline)))
        checked_write(output/data['colorPreviewFile'],png_bytes(composed))
        checked_write(output/data['bwPreviewFile'],png_bytes(bw))
        checked_write(hitmask_file,np.where(alpha.max(axis=0) >= 200, owner + 1, 0).astype('<u2').tobytes())
        checked_write(data['geometryFile'],json.dumps(dict(groups=[dict(id=g['id'],paths=g['paths'],samples=g['samples']) for _,g in groups]),separators=(',', ':')).encode('utf-8'))
        manifest=archive/'level_manifest.json'
        # Keep recorded Unity/Play/build outcomes on a byte-identical preparation rerun.
        if manifest.exists():
            old=json.loads(manifest.read_text(encoding='utf-8'))
            for field in ('sceneName','width','height','pixelsPerUnit','colors','regions'):
                if old.get(field) != data[field]:
                    raise ValueError(f'Manifest identity changed: {field}; review before updating')
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
    args = parser.parse_args()
    prepare(discover(args.source,args.exchange_root), args.output, args.scene, args.title, args.validate_only)
