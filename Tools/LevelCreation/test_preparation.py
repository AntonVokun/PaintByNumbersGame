"""Small synthetic regression fixtures for the image-preparation safety contract."""
import contextlib, io, json, os, tempfile, unittest
from pathlib import Path
import numpy as np
from PIL import Image
from prepare_level import prepare
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

    def test_changed_original_is_never_overwritten(self):
        self.run_prepare()
        archive=Path('ArtSources/Level_003/Original/preview.png')
        before=archive.read_bytes()
        image=Image.open(self.source/'preview.png').copy();image.putpixel((0,0),(1,2,3,255));image.save(self.source/'preview.png')
        with self.assertRaisesRegex(ValueError,'Differing existing file'):self.run_prepare()
        self.assertEqual(archive.read_bytes(),before)


if __name__=='__main__':unittest.main()
