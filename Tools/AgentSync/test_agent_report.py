"""Protocol tests: prevent mixed snapshots, partial replacements and secret uploads."""
import contextlib, io, json, tempfile, unittest
from pathlib import Path
from unittest.mock import patch
import agent_report as report


class ReportTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory()
        self.root=Path(self.temp.name)
        self.overrides=patch.multiple(report,ROOT=self.root,SYNC=self.root/'AgentSync')
        self.overrides.start()

    def tearDown(self):
        self.overrides.stop();self.temp.cleanup()

    def snapshot(self):
        folder=report.SYNC/'reports'/'test-report';folder.mkdir(parents=True)
        entries=[]
        for name in report.REPORT_NAMES[:-1]:
            data=('verified '+name+'\n').encode();(folder/name).write_bytes(data)
            entries.append(dict(name=name,sha256=report.sha(data)))
        state=dict(reportId='test-report',files=entries)
        (folder/'STATE.json').write_bytes(report.json_bytes(state))
        plan=dict(reportId='test-report',files=[dict(name=n,path=str(folder/n),sha256=report.sha((folder/n).read_bytes())) for n in report.REPORT_NAMES])
        path=report.SYNC/'SYNC_PLAN.json';path.write_bytes(report.json_bytes(plan))
        return folder,path,plan

    def test_valid_snapshot_then_changed_bytes_are_rejected(self):
        folder,path,_=self.snapshot()
        with contextlib.redirect_stdout(io.StringIO()):report.validate_outbound(path)
        (folder/'STATUS.md').write_text('changed after preparation')
        with self.assertRaisesRegex(RuntimeError,'Outbound validation'):report.validate_outbound(path)

    def test_mixed_report_versions_are_rejected(self):
        folder,path,plan=self.snapshot()
        state=json.loads((folder/'STATE.json').read_text());state['reportId']='other'
        (folder/'STATE.json').write_bytes(report.json_bytes(state))
        plan['files'][-1]['sha256']=report.sha((folder/'STATE.json').read_bytes());path.write_bytes(report.json_bytes(plan))
        with self.assertRaisesRegex(RuntimeError,'Mixed report'):report.validate_outbound(path)

    def test_report_cannot_upload_an_arbitrary_local_file(self):
        _,path,plan=self.snapshot()
        plan['files'][0]['path']=str(self.root/'private.txt');path.write_bytes(report.json_bytes(plan))
        with self.assertRaisesRegex(RuntimeError,'outside snapshot'):report.validate_outbound(path)

    def test_failed_atomic_replace_preserves_previous_state(self):
        target=self.root/'STATE.json';target.write_text('old complete state')
        with patch.object(report.os,'replace',side_effect=OSError('simulated locked target')):
            with self.assertRaises(OSError):report.atomic_write(target,'new state')
        self.assertEqual(target.read_text(),'old complete state')

    def test_generator_accepts_structured_evidence_and_publishes_valid_snapshot(self):
        observations=self.root/'observations.json'
        observations.write_text(json.dumps({'checks':[{'name':'upload','status':'PASS','evidence':{'reportId':'previous'}}]}))
        def fake_git(*args):return '' if args[0]=='ls-files' else 'test'
        with patch.object(report,'inventory',return_value={'levelManifests':[]}),patch.object(report,'validate_docs',return_value={'status':'PASS'}),patch.object(report,'changed_files',return_value=[]),patch.object(report,'git',side_effect=fake_git),contextlib.redirect_stdout(io.StringIO()):
            state=report.generate('test task','ready',observations,skip_editor=True)
            report.validate_outbound(report.SYNC/'SYNC_PLAN.json')
        self.assertEqual(json.loads((report.SYNC/'STATE.json').read_text())['reportId'],state['reportId'])
        self.assertIn('previous',(report.SYNC/'TESTS.md').read_text(encoding='utf-8'))

    def test_secret_formats_are_blocked_without_echoing_values(self):
        for value in ['123456789:'+('x'*35),'sk-'+('a'*35),'password='+('a'*16),'github_pat_'+('b'*35)]:
            self.assertFalse(report.clean_scan(value))
        self.assertTrue(report.clean_scan('headSha=79de77ed; status=PASS; token omitted'))

    def test_patch_excludes_game_assets_and_blocks_sensitive_text(self):
        (self.root/'Tools').mkdir();(self.root/'Tools'/'safe.py').write_text('print(1)\n')
        with patch.object(report.subprocess,'run',return_value=type('Result',(),{'returncode':1})()):
            text,included,omitted=report.make_patch(['Tools/safe.py','Assets/Scene.unity','Editor.log'])
            self.assertEqual(included,['Tools/safe.py']);self.assertEqual(len(omitted),2)
            self.assertIn('+print(1)',text)
            (self.root/'Tools'/'safe.py').write_text('password='+('x'*20))
            with self.assertRaisesRegex(RuntimeError,'Possible secret'):report.make_patch(['Tools/safe.py'])


if __name__=='__main__':unittest.main()
