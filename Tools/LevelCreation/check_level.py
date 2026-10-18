"""Fast output/source-integrity checks; no Editor or Play claims."""
from pathlib import Path
import argparse, hashlib, json
import numpy as np
from PIL import Image


def verify(manifest):
    p=Path(manifest)
    m=json.loads(p.read_text(encoding='utf-8'))
    root=Path(m['artFolder'])
    expected={m['outlineFile'],m['colorPreviewFile'],m['bwPreviewFile']}|{g['file'] for g in m['groups']}
    actual={str(f.relative_to(root)).replace('\\','/') for f in root.rglob('*.png')}
    assert expected==actual, f'Unexpected/missing game PNGs: {expected^actual}'
    assert len(m['groups'])==m['colors'] and len({g['id'] for g in m['groups']})==m['colors']
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
        assert np.all(a[:,:,:3][a[:,:,3]==255]==g['color']), 'Palette color mismatch: '+g['file']
        alpha.append(a[:,:,3])
    alpha=np.stack(alpha)
    outline=np.array(Image.open(root/m['outlineFile']))
    assert not ((alpha>=200).sum(0)>1).any(), 'Opaque color overlap'
    assert not ((alpha.max(0)==0)&(outline[:,:,3]==0)).any(), 'Uncovered pixels'
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
    result=dict(status='PASS',canvas=[m['width'],m['height']],colors=m['colors'],regions=m['regions'],
                originals=len(m['originalFiles']),gamePngs=len(expected),scope='files only; Editor/Play/Android separate')
    print(json.dumps(result,ensure_ascii=False))
    return result


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('manifest')
    verify(parser.parse_args().manifest)
