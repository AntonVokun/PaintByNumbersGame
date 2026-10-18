"""Fast output/source-integrity checks; no Editor or Play claims."""
from pathlib import Path
import argparse, hashlib, json
import numpy as np
from PIL import Image
from prepare_level import interior_uncovered, representative_color, review_palette, runs_and_samples, contours


def verify(manifest):
    p=Path(manifest)
    m=json.loads(p.read_text(encoding='utf-8'))
    root=Path(m['artFolder'])
    expected={m['outlineFile'],m['colorPreviewFile'],m['bwPreviewFile']}|{g['file'] for g in m['groups']}
    actual={str(f.relative_to(root)).replace('\\','/') for f in root.rglob('*.png')}
    assert expected==actual, f'Unexpected/missing game PNGs: {expected^actual}'
    assert len(m['groups'])==m['colors'] and len({g['id'] for g in m['groups']})==m['colors']
    mode=m.get('groupingMode','rgb')
    assert mode in ('rgb','layer'), 'Unknown grouping mode'
    assert [g['id'] for g in m['groups']]==list(range(1,m['colors']+1)), 'Group IDs must match hitmask indices'
    for f in m['originalFiles']:
        assert hashlib.sha256(Path(f['file']).read_bytes()).hexdigest()==f['sha256'], 'Original changed: '+f['file']
    alpha=[]
    for name in expected:
        with Image.open(root/name) as image:
            assert image.size==(m['width'],m['height']), 'Canvas mismatch: '+name
            assert image.mode=='RGBA', 'Lost RGBA: '+name
    for g in m['groups']:
        file=root/g['file']
        assert hashlib.sha256(file.read_bytes()).hexdigest()==g['sha256'], 'Shifted/changed pixels: '+g['file']
        a=np.array(Image.open(file))
        assert a[:,:,3].min()==0 and a[:,:,3].max()==255, 'Lost transparency: '+g['file']
        kind=g.get('type','flat')
        assert kind in ('flat','gradient'), 'Unknown group type'
        assert (kind=='gradient')==(mode=='layer'), 'Grouping mode/type mismatch'
        if kind=='gradient':
            records=g['sources']
            assert records and records[0]['source']==g['source'], 'Invalid gradient source mapping'
            reconstructed=None
            for record in records:
                source=(p.parent/'Original'/record['source']).resolve()
                assert source.is_relative_to((p.parent/'Original').resolve()), 'Source outside archive'
                assert hashlib.sha256(source.read_bytes()).hexdigest()==record['sha256'], 'Gradient source bytes changed'
                pixels=np.array(Image.open(source).convert('RGBA'))
                assert hashlib.sha256(pixels.tobytes()).hexdigest()==record['rgbaSha256'], 'Source pixel hash changed'
                if reconstructed is None:reconstructed=pixels.copy()
                else:
                    assert not ((reconstructed[:,:,3]>0)&(pixels[:,:,3]>0)).any(), 'Overlapping merged alpha'
                    reconstructed[pixels[:,:,3]>0]=pixels[pixels[:,:,3]>0]
            assert g['sourceSha256']==records[0]['sha256'], 'Primary source hash mismatch'
            if len(records)==1:
                assert g['sha256']==records[0]['sha256'] and g['mergeMethod']=='source_bytes', 'Single gradient PNG bytes changed'
            else:
                assert g['mergeMethod']=='disjoint_rgba_union', 'Unsupported merge method'
            assert np.array_equal(a,reconstructed), 'Gradient RGB/alpha changed'
            assert hashlib.sha256(a.tobytes()).hexdigest()==g['rgbaSha256'], 'Gradient pixel hash mismatch'
            assert g['paletteMethod'] in ('explicit','opaque_median_nearest'), 'Unknown palette method'
            expected_color=m.get('groupConfig',{}).get('colors',{}).get(g['source']) if g['paletteMethod']=='explicit' else representative_color(a)
            assert g['color']==expected_color, 'Representative palette mismatch'
        else:
            assert np.all(a[:,:,:3][a[:,:,3]==255]==g['color']), 'Palette color mismatch: '+g['file']
        alpha.append(a[:,:,3])
    alpha=np.stack(alpha)
    outline=np.array(Image.open(root/m['outlineFile']))
    assert not ((alpha>=200).sum(0)>1).any(), 'Opaque color overlap'
    assert not interior_uncovered((alpha.max(0)==0)&(outline[:,:,3]==0)).any(), 'Uncovered interior pixels'
    if mode=='layer':
        source_names=[r['source'] for g in m['groups'] for r in g['sources']]
        assert len(set(source_names))==len(source_names), 'A gradient source was split into multiple groups'
        archived_zones={Path(f['file']).resolve().relative_to((p.parent/'Original').resolve()).as_posix() for f in m['originalFiles'] if Path(f['file']).name.startswith('Zone_')}
        assert set(source_names)==archived_zones, 'A gradient source was lost'
        actual_merges=[tuple(r['source'] for r in g['sources']) for g in m['groups'] if len(g['sources'])>1]
        assert sorted(actual_merges)==sorted(tuple(v) for v in m.get('groupConfig',{}).get('mergeGroups',[])), 'Merge configuration mismatch'
        pinned=m.get('groupConfig',{}).get('sourceSha256')
        if pinned is not None:
            assert pinned=={r['source']:r['sha256'] for g in m['groups'] for r in g['sources']}, 'Reviewed sources changed'
        assert m['paletteReview']==review_palette(m['groups'],m.get('groupConfig',{})), 'Palette review mismatch'
    composed=Image.new('RGBA',(m['width'],m['height']),'white')
    for g in m['groups']:composed=Image.alpha_composite(composed,Image.open(root/g['file']))
    composed=Image.alpha_composite(composed,Image.fromarray(outline))
    assert np.array_equal(np.array(composed),np.array(Image.open(root/m['colorPreviewFile']))), 'Preview does not match layers'
    bw=Image.alpha_composite(Image.new('RGBA',(m['width'],m['height']),'white'),Image.fromarray(outline))
    assert np.array_equal(np.array(bw),np.array(Image.open(root/m['bwPreviewFile']))), 'BW preview does not match outline'
    mask=Path(m['hitmaskFile']).read_bytes()
    assert len(mask)==m['width']*m['height']*m['hitmaskBytesPerPixel'], 'Invalid hitmask'
    expected_mask=np.where(alpha.max(0)>=200,alpha.argmax(0)+1,0).astype('<u2').tobytes()
    assert mask==expected_mask,'Hitmask does not match prepared zones'
    geometry=json.loads(Path(m['geometryFile']).read_text(encoding='utf-8'))
    assert {g['id'] for g in geometry['groups']}=={g['id'] for g in m['groups']}, 'Missing collider geometry'
    assert all(g['paths'] and g['samples'] for g in geometry['groups']), 'Empty collider/region samples'
    if mode=='layer':
        owner=alpha.argmax(0)
        for index,g in enumerate(m['groups']):
            mask=(alpha[index]>=128)&(owner==index)
            shape=next(v for v in geometry['groups'] if v['id']==g['id'])
            assert shape['samples']==runs_and_samples(mask) and len(shape['samples'])==g['regions'], 'Gradient islands changed'
            assert shape['paths']==[dict(points=p) for p in contours(mask)], 'Gradient alpha geometry mismatch'
    result=dict(status='PASS',canvas=[m['width'],m['height']],colors=m['colors'],regions=m['regions'],
                originals=len(m['originalFiles']),gamePngs=len(expected),scope='files only; Editor/Play/Android separate')
    print(json.dumps(result,ensure_ascii=False))
    return result


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('manifest')
    verify(parser.parse_args().manifest)
