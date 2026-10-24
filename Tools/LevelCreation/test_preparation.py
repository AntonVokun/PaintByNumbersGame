"""Small synthetic regression fixtures for the image-preparation safety contract."""
import contextlib, io, json, os, tempfile, unittest
from pathlib import Path
import numpy as np
from PIL import Image
from prepare_level import prepare, palette_lab
from check_level import verify


class PreparationTests(unittest.TestCase):
    def setUp(self):
        self.old_cwd=Path.cwd()
        parent=self.old_cwd/'Logs/LevelCreation/tests'
        parent.mkdir(parents=True,exist_ok=True)
        self.temp=tempfile.TemporaryDirectory(dir=parent)
        self.root=Path(self.temp.name).resolve()
        assert self.root.is_relative_to(parent.resolve())
        os.chdir(self.root)
        self.source=Path('source');self.source.mkdir()
        self.output=Path('Assets/_Project/Art/Levels/Level_003')
        red=np.zeros((24,32,4),dtype=np.uint8)
        red[:,:16]=[220,70,50,255]
        blue=np.zeros_like(red);blue[:,16:]=[30,120,230,255]
        blue[3:7,20:24]=[20,190,80,255]
        blue[16:20,27:31]=[20,190,80,255]
        outline=np.zeros_like(red);outline[:,15]=[0,0,0,255]
        for name,data in [('Zone_001.png',red),('Zone_002.png',blue),('outlines.png',outline)]:
            Image.fromarray(data).save(self.source/name)
        preview=Image.new('RGBA',(32,24),'white')
        for data in [red,blue,outline]:preview=Image.alpha_composite(preview,Image.fromarray(data))
        preview.save(self.source/'preview.png')

    def tearDown(self):
        os.chdir(self.old_cwd)
        assert self.root.is_relative_to((self.old_cwd/'Logs/LevelCreation/tests').resolve())
        self.temp.cleanup()

    def run_prepare(self):
        with contextlib.redirect_stdout(io.StringIO()):
            prepare(self.source,self.output,'Level_003_New','Fixture')

    def layer_prepare(self, config=None):
        with contextlib.redirect_stdout(io.StringIO()):
            prepare(self.source,self.output,'Level_003_New','Fixture',grouping_mode='layer',group_config=config)

    def refresh_preview(self):
        preview=Image.new('RGBA',(32,24),'white')
        for f in sorted(self.source.glob('Zone_*.png')):
            preview=Image.alpha_composite(preview,Image.open(f).convert('RGBA'))
        preview=Image.alpha_composite(preview,Image.open(self.source/'outlines.png').convert('RGBA'))
        preview.save(self.source/'preview.png')

    def test_full_canvas_split_holes_and_safe_rerun(self):
        self.run_prepare()
        manifest=Path('ArtSources/Level_003/level_manifest.json')
        m=json.loads(manifest.read_text(encoding='utf-8'))
        self.assertEqual((m['colors'],m['regions']),(3,4))
        geometry=json.loads(Path(m['geometryFile']).read_text())
        self.assertEqual(sum(len(g['paths']) for g in geometry['groups']),6)
        before={str(p):p.read_bytes() for p in self.root.rglob('*') if p.is_file()}
        self.run_prepare()
        self.assertEqual(before,{str(p):p.read_bytes() for p in self.root.rglob('*') if p.is_file()})
        with contextlib.redirect_stdout(io.StringIO()):verify(manifest)

    def test_canvas_mismatch_fails_without_game_output(self):
        Image.new('RGBA',(31,24),(220,70,50,255)).save(self.source/'Zone_001.png')
        with self.assertRaisesRegex(ValueError,'Canvas mismatch'):self.run_prepare()
        self.assertFalse(self.output.exists())

    def test_transparent_exterior_passes_but_enclosed_gap_fails(self):
        file=self.source/'Zone_002.png'
        blue=np.array(Image.open(file));blue[:,-1]=0
        Image.fromarray(blue).save(file)
        self.run_prepare()
        with contextlib.redirect_stdout(io.StringIO()):
            verify('ArtSources/Level_003/level_manifest.json')
        blue[10,22]=0
        Image.fromarray(blue).save(file)
        with self.assertRaisesRegex(ValueError,'1 uncovered pixels'):
            self.run_prepare()

    def test_changed_original_is_never_overwritten(self):
        self.run_prepare()
        archive=Path('ArtSources/Level_003/Original/preview.png')
        before=archive.read_bytes()
        image=Image.open(self.source/'preview.png').copy();image.putpixel((0,0),(1,2,3,255));image.save(self.source/'preview.png')
        with self.assertRaisesRegex(ValueError,'Differing existing file'):self.run_prepare()
        self.assertEqual(archive.read_bytes(),before)

    def test_gradient_exact_bytes_alpha_islands_and_rerun(self):
        f=self.source/'Zone_001.png';a=np.array(Image.open(f));a[:,:,:3]=[17,19,23]
        for y in range(24):
            for x in range(16):a[y,x,:3]=[80+x*5,30+y*4,19]
        a[:,8,3]=0;Image.fromarray(a).save(f)
        o=np.array(Image.open(self.source/'outlines.png'));o[:,8]=[0,0,0,255];Image.fromarray(o).save(self.source/'outlines.png');self.refresh_preview()
        source_bytes={f.name:f.read_bytes() for f in self.source.glob('Zone_*.png')}
        self.layer_prepare()
        manifest=Path('ArtSources/Level_003/level_manifest.json');m=json.loads(manifest.read_text(encoding='utf-8'))
        self.assertEqual((m['colors'],m['regions']),(2,3))
        self.assertEqual([g['type'] for g in m['groups']],['gradient','gradient'])
        for g in m['groups']:self.assertEqual((self.output/g['file']).read_bytes(),source_bytes[g['source']])
        with contextlib.redirect_stdout(io.StringIO()):verify(manifest.resolve())
        before={str(p):p.read_bytes() for p in self.root.rglob('*') if p.is_file()};self.layer_prepare()
        self.assertEqual(before,{str(p):p.read_bytes() for p in self.root.rglob('*') if p.is_file()})

    def test_multishade_layer_requires_explicit_mode(self):
        f=self.source/'Zone_001.png';a=np.array(Image.open(f))
        for y in range(24):
            for x in range(16):a[y,x,:3]=[x*11,y*9,50]
        Image.fromarray(a).save(f);self.refresh_preview()
        with self.assertRaisesRegex(ValueError,'opaque colors'):self.run_prepare()
        self.assertFalse(self.output.exists());self.layer_prepare()

    def test_near_palette_requires_bound_explicit_approval(self):
        f=self.source/'Zone_002.png';a=np.array(Image.open(f));a[a[:,:,3]>0,:3]=[221,71,51];Image.fromarray(a).save(f);self.refresh_preview()
        with self.assertRaisesRegex(ValueError,'Palette review required'):self.layer_prepare()
        self.assertFalse(self.output.exists())
        cfg={'approvedClosePairs':[{'sources':['Zone_001.png','Zone_002.png'],'colors':[[220,70,50],[221,71,51]]}]}
        self.layer_prepare(cfg)
        with contextlib.redirect_stdout(io.StringIO()):verify('ArtSources/Level_003/level_manifest.json')
        cfg['approvedClosePairs'][0]['colors'][0]=[1,2,3]
        with self.assertRaisesRegex(ValueError,'Stale'):self.layer_prepare(cfg)

    def test_explicit_palette_and_mode_change_safety(self):
        self.layer_prepare({'colors':{'Zone_001.png':[230,80,50]}})
        with contextlib.redirect_stdout(io.StringIO()):verify('ArtSources/Level_003/level_manifest.json')
        before={str(p):p.read_bytes() for p in self.root.rglob('*') if p.is_file()}
        with self.assertRaisesRegex(ValueError,'palette changed'):self.layer_prepare({'colors':{'Zone_001.png':[200,60,40]}})
        self.assertEqual(before,{str(p):p.read_bytes() for p in self.root.rglob('*') if p.is_file()})
        with self.assertRaisesRegex(ValueError,'Differing existing file|identity changed|grouping mode'):self.run_prepare()

    def test_disjoint_merge_preserves_every_visible_rgba_and_original_file(self):
        for f in self.source.glob('Zone_*.png'):
            a=np.array(Image.open(f));a[0,:,3]=0;Image.fromarray(a).save(f)
        o=np.array(Image.open(self.source/'outlines.png'));o[0]=[255,255,255,255];Image.fromarray(o).save(self.source/'outlines.png');self.refresh_preview()
        originals={f.name:f.read_bytes() for f in self.source.glob('Zone_*.png')}
        self.layer_prepare({'mergeGroups':[['Zone_001.png','Zone_002.png']]})
        m=json.loads(Path('ArtSources/Level_003/level_manifest.json').read_text(encoding='utf-8'));self.assertEqual(m['colors'],1)
        image=np.array(Image.open(self.output/m['groups'][0]['file']))
        for f in self.source.glob('Zone_*.png'):
            a=np.array(Image.open(f));np.testing.assert_array_equal(image[a[:,:,3]>0],a[a[:,:,3]>0])
            self.assertEqual((Path('ArtSources/Level_003/Original')/f.name).read_bytes(),originals[f.name])
        with contextlib.redirect_stdout(io.StringIO()):verify('ArtSources/Level_003/level_manifest.json')

    def test_merge_overlap_and_repeated_members_are_rejected_before_output(self):
        f=self.source/'Zone_002.png';a=np.array(Image.open(f));a[:,15]=[10,20,200,10];Image.fromarray(a).save(f)
        with self.assertRaisesRegex(ValueError,'overlapping nonzero alpha'):self.layer_prepare({'mergeGroups':[['Zone_001.png','Zone_002.png']]})
        self.assertFalse(self.output.exists())
        with self.assertRaisesRegex(ValueError,'multiple merged groups'):self.layer_prepare({'mergeGroups':[['Zone_001.png','Zone_002.png'],['Zone_001.png','Zone_002.png']]})

    def test_source_review_hashes_and_unknown_configuration_are_rejected(self):
        with self.assertRaisesRegex(ValueError,'Reviewed source hashes'):self.layer_prepare({'sourceSha256':{}})
        with self.assertRaisesRegex(ValueError,'Invalid group configuration'):self.layer_prepare({'unknown':True})
        with self.assertRaisesRegex(ValueError,'existing source layers'):self.layer_prepare({'colors':{'missing.png':[1,2,3]}})

    def test_old_manifest_without_group_type_remains_valid(self):
        self.run_prepare();path=Path('ArtSources/Level_003/level_manifest.json');m=json.loads(path.read_text(encoding='utf-8'));m.pop('groupingMode')
        for g in m['groups']:g.pop('type')
        path.write_text(json.dumps(m),encoding='utf-8')
        with contextlib.redirect_stdout(io.StringIO()):verify(path)

    def test_gradient_geometry_corruption_is_detected(self):
        self.layer_prepare();manifest=Path('ArtSources/Level_003/level_manifest.json');m=json.loads(manifest.read_text(encoding='utf-8'))
        file=Path(m['geometryFile']);geometry=json.loads(file.read_text());geometry['groups'][0]['paths'][0]['points'][0]['x']+=1;file.write_text(json.dumps(geometry))
        with self.assertRaisesRegex(AssertionError,'alpha geometry mismatch'):
            with contextlib.redirect_stdout(io.StringIO()):verify(manifest)

    def test_invalid_approval_and_configuration_types_fail_cleanly(self):
        with self.assertRaisesRegex(ValueError,'Invalid group configuration'):self.layer_prepare([])
        with self.assertRaisesRegex(ValueError,'approvedClosePairs'):self.layer_prepare({'approvedClosePairs':'yes'})
        with self.assertRaisesRegex(ValueError,'Palette RGB'):self.layer_prepare({'colors':{'Zone_001.png':[256,0,0]}})

    def test_perceptual_palette_candidates_do_not_equate_brown_and_purple(self):
        self.assertGreater(np.linalg.norm(palette_lab([169,123,71])-palette_lab([115,28,234])),6)
        self.assertGreater(np.linalg.norm(palette_lab([169,123,71])-palette_lab([163,127,84])),6)
        self.assertLess(np.linalg.norm(palette_lab([231,221,201])-palette_lab([221,207,193])),6)


if __name__=='__main__':unittest.main()
