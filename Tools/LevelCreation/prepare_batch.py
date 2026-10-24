"""Prepare source copies only. Never imports assets, creates scenes or accesses progress."""
from pathlib import Path
import argparse
import contextlib
import io
import json
import os
import shutil
import time
import sys
import numpy as np
from PIL import Image
import prepare_level as prep
from check_level import verify
from pipeline_io import Metrics, compact, digest, write_json


def compose(layers, outline):
    result = Image.new('RGBA', (outline.shape[1], outline.shape[0]), 'white')
    for layer in layers:
        result = Image.alpha_composite(result, Image.fromarray(layer))
    return np.array(Image.alpha_composite(result, Image.fromarray(outline)))


def hidden_overlap_fix(layers, outline):
    """Only change alpha behind fully opaque outline, preserving owner geometry and composite."""
    alpha = np.stack([v[:, :, 3] for v in layers])
    owner = alpha.argmax(0)
    hidden = ((alpha >= 200).sum(0) > 1) & (outline[:, :, 3] == 255)
    copies = [v.copy() for v in layers]
    for index, value in enumerate(copies):
        value[:, :, 3][hidden & (owner != index)] = 0
    after = np.stack([v[:, :, 3] for v in copies])
    if not np.array_equal(compose(layers, outline), compose(copies, outline)):
        raise ValueError('FIX_NOT_PIXEL_EXACT')
    for i in range(len(layers)):
        if not np.array_equal((alpha[i] >= 128) & (owner == i), (after[i] >= 128) & (after.argmax(0) == i)):
            raise ValueError('FIX_CHANGED_GEOMETRY')
        if not np.array_equal(layers[i][:, :, :3], copies[i][:, :, :3]):
            raise ValueError('FIX_CHANGED_RGB')
    return copies, int(hidden.sum())


def analyze(layers, outline, names):
    alpha = np.stack([v[:, :, 3] for v in layers])
    owner = alpha.argmax(0)
    groups = []
    for i, (name, rgba) in enumerate(zip(names, layers)):
        colors = np.unique(rgba[:, :, :3][rgba[:, :, 3] == 255], axis=0)
        samples = prep.runs_and_samples((alpha[i] >= 128) & (owner == i))
        groups.append(dict(id=i+1, source=name, color=prep.representative_color(rgba),
                           opaqueColors=len(colors), regions=len(samples)))
    pairs = []
    for i, a in enumerate(groups):
        for b in groups[:i]:
            delta = float(np.linalg.norm(prep.palette_lab(a['color'])-prep.palette_lab(b['color'])))
            if delta < 6:
                pairs.append(dict(ids=[b['id'], a['id']], sources=[b['source'], a['source']],
                                  colors=[b['color'], a['color']], deltaE=round(delta, 6)))
    return dict(width=outline.shape[1], height=outline.shape[0], colors=len(groups),
                regions=sum(g['regions'] for g in groups), gradientLayers=sum(g['opaqueColors'] > 1 for g in groups),
                overlaps=int(((alpha >= 200).sum(0) > 1).sum()),
                uncovered=int(prep.interior_uncovered((alpha.max(0) == 0) & (outline[:, :, 3] == 0)).sum()),
                closePairs=pairs, groups=groups)


def write_review(destination, layers, outline):
    """Candidate previews and bounded conflict coordinates, never a ready manifest."""
    folder = Path(destination)/'Review'
    folder.mkdir(exist_ok=True)
    Image.fromarray(compose(layers, outline)).save(folder/'Preview_Color.png')
    Image.alpha_composite(Image.new('RGBA', (outline.shape[1], outline.shape[0]), 'white'),
                          Image.fromarray(outline)).save(folder/'Preview_BW.png')
    alpha = np.stack([v[:, :, 3] for v in layers])
    conflict = (alpha >= 200).sum(0) > 1
    solid = alpha[:, conflict] >= 200
    pairs = []
    for i in range(len(layers)):
        for j in range(i):
            count = int((solid[i] & solid[j]).sum())
            if count:
                pairs.append(dict(groups=[j+1, i+1], pixels=count))
    uncovered = prep.interior_uncovered((alpha.max(0) == 0) & (outline[:, :, 3] == 0))
    write_json(folder/'conflicts.json', dict(coordinates='zero-based, top-left',
        overlapSamples=prep.runs_and_samples(conflict)[:20],
        overlapPairs=sorted(pairs, key=lambda p: -p['pixels'])[:20],
        gapSamples=prep.runs_and_samples(uncovered)[:20], readiness='MANUAL_REVIEW; candidate previews only'))


def process_set(source, destination, key, metrics):
    source, destination = Path(source).resolve(), Path(destination).resolve()
    if destination == source or destination.is_relative_to(source):
        raise ValueError('OUTPUT_INSIDE_SOURCE')
    destination.mkdir(parents=True, exist_ok=False)
    # Select one canonical top-level set. Nested alternative exports are not mixed into it.
    files = sorted(f for f in source.iterdir() if f.is_file())
    originals = {f.name: digest(f) for f in files}
    for f in files:
        target = destination/'Original'/f.name
        target.parent.mkdir(exist_ok=True)
        shutil.copy2(f, target)
        work = destination/'WorkingSource'/f.name
        work.parent.mkdir(exist_ok=True)
        shutil.copy2(f, work)
    work = destination/'WorkingSource'
    zones = sorted(work.glob('Zone_*.png'))
    outlines = list(work.glob('*outline*.png'))
    result = dict(name=source.name, source=str(source), selectedVariant='top-level Zone_*.png set',
                  status='BLOCKED', readyForLevel=False, fixes=[], originalHashes=originals)
    try:
        if not zones or len(outlines) != 1:
            raise ValueError('AMBIGUOUS_CANONICAL_SET')
        outline = np.array(Image.open(outlines[0]).convert('RGBA'))
        layers = []
        for f in zones:
            with Image.open(f) as image:
                if image.mode != 'RGBA' or image.size != (outline.shape[1], outline.shape[0]):
                    raise ValueError('CANVAS_OR_RGBA_MISMATCH: '+f.name)
                layers.append(np.array(image))
        if any(v[:, :, 3].min() != 0 or v[:, :, 3].max() != 255 for v in layers):
            raise ValueError('ALPHA_EXTENT_INVALID')
        before = analyze(layers, outline, [f.name for f in zones])
        if outlines[0].name != 'outlines.png':
            shutil.copy2(outlines[0], work/'outlines.png')
            assert digest(outlines[0]) == digest(work/'outlines.png')
            # The old alias is archived, but only the canonical outline participates in preparation.
            (work/outlines[0].name).unlink()
            result['fixes'].append(dict(file='outlines.png', action='byte-exact canonical alias of '+outlines[0].name,
                                        safety='Original filename and bytes retained in Original; exactly one outline in selected set'))
        fixed, hidden_count = hidden_overlap_fix(layers, outline)
        for f, old, new in zip(zones, layers, fixed):
            if not np.array_equal(old, new):
                Image.fromarray(new).save(f)
                result['fixes'].append(dict(file=f.name, action='alpha-only ownership behind alpha255 outline',
                                            pixels=int((old[:, :, 3] != new[:, :, 3]).sum()),
                                            safety='Exact full composite and alpha128 owner geometry verified; all RGB unchanged'))
        after = analyze(fixed, outline, [f.name for f in zones])
        result.update(before=before, after=after, hiddenOverlapPixelsFixed=hidden_count,
                      exactCompositePreserved=True, ownerGeometryPreserved=True)
        config = dict(groupingMode='layer', sourceSha256={f.name: digest(f) for f in zones})
        write_json(destination/'group-config.json', config)
        if after['overlaps'] or after['uncovered'] or after['closePairs']:
            write_review(destination, fixed, outline)
            result.update(status='MANUAL_REVIEW', reason='Visible coverage/palette needs reviewed decision; no groups merged')
        else:
            previous = Path.cwd()
            try:
                os.chdir(destination)
                with contextlib.redirect_stdout(io.StringIO()) as captured:
                    prepared = Path('Prepared')/key
                    prep.prepare(work, prepared, 'Prepared_'+key+'_000', source.name,
                                 grouping_mode='layer', group_config=config)
                    verify(Path('ArtSources')/key/'level_manifest.json')
                (destination/'preparation.log').write_text(captured.getvalue(), encoding='utf-8')
            finally:
                os.chdir(previous)
            result.update(status='AUTO_FIX' if result['fixes'] else 'PASS', readyForLevel=True,
                          manifest=str(destination/'ArtSources'/key/'level_manifest.json'),
                          identity='Prepared-only placeholder; bind requested level number during later authorised import')
    except (ValueError, AssertionError, OSError) as error:
        result.update(status='MANUAL_REVIEW' if 'review' in str(error).lower() else 'BLOCKED', reason=str(error)[:500])
    finally:
        unchanged = all(digest(source/n) == h and digest(destination/'Original'/n) == h for n,h in originals.items())
        result['originalsUnchanged'] = unchanged
        if not unchanged:
            raise RuntimeError('ORIGINAL_PRESERVATION_FAILED')
        write_json(destination/'result.json', result)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--exchange-root', default=r'C:\Users\Public\Documents\PaintByNumbers Exchange\уровни')
    parser.add_argument('--output', required=True, help='New preparation directory, outside active Assets and original sources')
    parser.add_argument('--project-root', default=str(Path(__file__).resolve().parents[2]))
    args = parser.parse_args()
    output = Path(args.output).resolve()
    source_root = Path(args.exchange_root).resolve()
    project = Path(args.project_root).resolve()
    if output.is_relative_to(source_root) or output.is_relative_to(project/'Assets'):
        raise ValueError('UNSAFE_PREPARATION_OUTPUT')
    if output.exists():
        raise ValueError('OUTPUT_EXISTS: use a fresh directory; no automatic overwrite')
    output.mkdir(parents=True)
    metrics, results = Metrics(output/'diagnostics'), []
    for key, token in [('Meduza', 'медуза'), ('Domik', 'домик'), ('Tea', 'чаепитие')]:
        candidates = [d for d in source_root.iterdir() if d.is_dir() and token in d.name.casefold()]
        if len(candidates) != 1:
            results.append(dict(name=key, status='BLOCKED', readyForLevel=False, reason='AMBIGUOUS_SOURCE_FOLDER'))
            continue
        with metrics.stage(key) as event:
            result = process_set(candidates[0], output/key, key, metrics)
            results.append(result)
            event.update(readiness=result['status'], inputFilesUnique=len(result['originalHashes']),
                         stdoutBytes=0, stderrBytes=0,
                         logBytes=sum(f.stat().st_size for f in (output/key).rglob('*.json')))
    summary = [dict(name=r['name'], status=r['status'], colors=r.get('after', {}).get('colors'),
                    regions=r.get('after', {}).get('regions'), ready=r['readyForLevel'], fixes=len(r.get('fixes', [])))
               for r in results]
    write_json(output/'batch-result.json', dict(results=results, metrics=metrics.events, unityCalls=0,
               usage={k:'NOT_AVAILABLE' for k in ['input_tokens','output_tokens','cached_input_tokens','reasoning_tokens']}))
    print(compact(dict(status='PREPARED_WITH_REVIEW' if any(not r['readyForLevel'] for r in results) else 'PASS',
                       sets=summary, unityCalls=0, diagnostic=str(output/'batch-result.json'))))


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    main()
