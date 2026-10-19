"""P0 contracts: no live Editor, scenes or PlayerPrefs used by these fixtures."""
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
import numpy as np
from PIL import Image, ImageDraw
from pipeline_io import compact, Metrics
from inspect_pipeline import inspect, lite_from_state
from prepare_batch import hidden_overlap_fix, compose, process_set


class P0Tests(unittest.TestCase):
    def test_compact_refuses_large_unicode_and_preserves_json(self):
        self.assertEqual(json.loads(compact({'status':'PASS'})), {'status':'PASS'})
        with self.assertRaisesRegex(ValueError, 'SUMMARY_TOO_LARGE'):
            compact({'data':'я'*1500})

    def test_lite_omits_big_inventory_and_limits_history(self):
        value=lite_from_state(dict(projectId='Game',issues=['x'*900]*30,nextSteps=['n'*999],
                                   checks=[{'huge':'x'*40000}],changedFiles=['a']*1000))
        self.assertNotIn('checks',value)
        self.assertNotIn('changedFiles',value)
        self.assertLessEqual(len(compact(value).encode()),2048)

    def test_manual_review_is_a_blocker_not_silent_readiness(self):
        value=lite_from_state(dict(checks=[dict(name='Source requires artistic review',status='MANUAL_REVIEW')]))
        self.assertEqual(value['blockers'],['Source requires artistic review'])

    def test_unavailable_git_is_not_a_clean_state_and_inspect_does_not_write(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);(root/'AGENTS.md').write_text('instructions')
            before=list(root.rglob('*'))
            with patch('inspect_pipeline.subprocess.run',side_effect=RuntimeError('GIT_UNAVAILABLE')):
                value=inspect(root)
            self.assertEqual(value['gitStatus'],'NOT_AVAILABLE')
            self.assertIsNone(value['changedSinceReport'])
            self.assertEqual(before,list(root.rglob('*')))

    def test_metrics_records_failure_and_unity_calls_without_stdout(self):
        with tempfile.TemporaryDirectory() as directory:
            metrics=Metrics(directory)
            with self.assertRaises(ValueError):
                with metrics.stage('negative'):
                    metrics.unity_calls+=1
                    raise ValueError('EXPECTED_FAILURE: detail')
            row=json.loads((Path(directory)/'stages.jsonl').read_text())
            self.assertEqual((row['status'],row['unityCalls'],row['attempt']),('FAILED',1,1))
            self.assertFalse(row['cacheHit'])

    def fixture(self, root, alias=False, visible_overlap=False, close=False):
        root.mkdir()
        outline=Image.new('RGBA',(32,24));ImageDraw.Draw(outline).rectangle((1,1,30,22),outline='black')
        a=np.zeros((24,32,4),dtype=np.uint8);b=a.copy()
        a[2:22,2:16]=[240,20,30,255]
        b[2:22,16:30]=[242,20,30,255] if close else [20,80,240,255]
        if visible_overlap:b[8,8]=[20,80,240,255]
        else:
            b[5,8]=[20,80,240,255];outline.putpixel((8,5),(0,0,0,255))
        outline.save(root/('outlines-3.png' if alias else 'outlines.png'))
        Image.fromarray(a).save(root/'Zone_001.png');Image.fromarray(b).save(root/'Zone_002.png')
        Image.fromarray(compose([a,b],np.array(outline))).save(root/'preview.png')
        return [a,b],np.array(outline)

    def test_hidden_overlap_changes_no_rgb_composite_or_geometry(self):
        with tempfile.TemporaryDirectory() as directory:
            layers,outline=self.fixture(Path(directory)/'source')
            fixed,count=hidden_overlap_fix(layers,outline)
            self.assertEqual(count,1)
            np.testing.assert_array_equal(compose(layers,outline),compose(fixed,outline))
            np.testing.assert_array_equal(layers[1][:,:,:3],fixed[1][:,:,:3])

    def test_alias_and_hidden_fix_prepare_valid_bundle_and_preserve_originals(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);self.fixture(root/'source',alias=True)
            before={p.name:p.read_bytes() for p in (root/'source').iterdir()}
            result=process_set(root/'source',root/'out','Fixture',Metrics(root/'metrics'))
            self.assertEqual(result['status'],'AUTO_FIX')
            self.assertTrue(result['readyForLevel'])
            self.assertTrue(Path(result['manifest']).exists())
            self.assertEqual(before,{p.name:p.read_bytes() for p in (root/'source').iterdir()})
            self.assertTrue((root/'out/Original/outlines-3.png').exists())

    def test_visible_overlap_stays_manual_not_silently_removed(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);self.fixture(root/'source',visible_overlap=True)
            result=process_set(root/'source',root/'out','Fixture',Metrics(root/'metrics'))
            self.assertEqual(result['status'],'MANUAL_REVIEW')
            self.assertEqual(result['after']['overlaps'],1)
            self.assertFalse(result['readyForLevel'])

    def test_close_palette_requires_review_and_keeps_both_layers(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);self.fixture(root/'source',close=True)
            result=process_set(root/'source',root/'out','Fixture',Metrics(root/'metrics'))
            self.assertEqual(result['status'],'MANUAL_REVIEW')
            self.assertEqual(result['after']['colors'],2)
            self.assertTrue(result['after']['closePairs'])

    def test_output_cannot_be_source(self):
        with tempfile.TemporaryDirectory() as directory:
            source=Path(directory)/'source';source.mkdir()
            with self.assertRaisesRegex(ValueError,'OUTPUT_INSIDE_SOURCE'):
                process_set(source,source/'child','Bad',Metrics(Path(directory)/'logs'))


if __name__=='__main__':unittest.main()
