"""Read-only compact context. Detailed files remain on disk, no inventory refresh."""
from pathlib import Path
import argparse
import json
import os
import re
import subprocess
import sys
from pipeline_io import compact, digest, write_json


def lite_from_state(state):
    return dict(schema=1, projectId=state.get('projectId'), branch=state.get('branch'),
                headSha=state.get('headSha'), dirty=state.get('dirty'), reportId=state.get('reportId'),
                generatedAtUtc=state.get('generatedAtUtc'), taskStatus=state.get('taskStatus'),
                lastCompletedStage='publish' if state.get('sync',{}).get('status')=='uploaded' else 'report',
                blockers=[str(x.get('name',''))[:100] for x in state.get('checks',[]) if x.get('status') in ('FAIL','FAILED','BLOCKED','BLOCKED_SOURCE','MANUAL_REVIEW','stale')][:3],
                nextAction='Read current user task; expand only linked diagnostics when needed',
                historicalNextAction=str(next(iter(state.get('nextSteps', [])), 'Not recorded'))[:120],
                detailRefs=dict(state='AgentSync/STATE.json', tests='AgentSync/TESTS.md'))


def inspect(root, with_unity=False, metrics=None):
    root = Path(root).resolve()
    env = dict(os.environ, GIT_OPTIONAL_LOCKS='0')
    def git(*args):
        result = subprocess.run(['git', *args], cwd=root, env=env, capture_output=True, timeout=20)
        if result.returncode:
            raise RuntimeError('GIT_UNAVAILABLE')
        return result.stdout.decode('utf-8').strip()
    state_path = root/'AgentSync/STATE.json'
    state = json.loads(state_path.read_text(encoding='utf-8-sig')) if state_path.exists() else {}
    result = lite_from_state(state)
    result['requestedAction'] = 'Use current user task; historical nextAction is only a reference'
    try:
        head = git('rev-parse', 'HEAD')
        dirty = git('status', '--porcelain=v1', '-uall')
        result.update(headSha=head, branch=git('branch', '--show-current'), dirty=bool(dirty),
                      changedFiles=len(dirty.splitlines()) if dirty else 0,
                      changedSinceReport=head != state.get('headSha') or bool(dirty))
    except (RuntimeError, OSError, subprocess.SubprocessError):
        result.update(gitStatus='NOT_AVAILABLE', changedSinceReport=None)
        result['blockers'].insert(0, 'GIT_UNAVAILABLE: do not infer a clean project')
    result['unityCalls'] = int(with_unity)
    result['agentsSha256'] = digest(root/'AGENTS.md') if (root/'AGENTS.md').exists() else None
    version = root/'ProjectSettings/ProjectVersion.txt'
    result['unityVersion'] = re.search(r'm_EditorVersion: (.*)', version.read_text()).group(1) if version.exists() else None
    settings = root/'ProjectSettings/ProjectSettings.asset'
    match = re.search(r'^  bundleVersion: (.*)$', settings.read_text(encoding='utf-8'), re.M) if settings.exists() else None
    result['projectVersion'] = match.group(1) if match else None
    if with_unity:
        command = ['unity', 'status', '--format', 'json']
        try:
            raw = metrics.process('editor-status', command, root, unity=True, timeout=25) if metrics else subprocess.run(command, cwd=root, capture_output=True, timeout=25, check=True).stdout.decode('utf-8-sig')
            envelope = json.loads(raw)
            instances = [v for v in envelope.get('data', {}).get('instances', []) if Path(v['project']).resolve() == root]
            result['editor'] = dict(available=bool(instances), state=instances[0]['state'] if instances else 'NOT_AVAILABLE')
        except (OSError, ValueError, RuntimeError, subprocess.SubprocessError):
            result['editor'] = dict(available=False, state='NOT_AVAILABLE')
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', default=str(Path(__file__).resolve().parents[2]))
    parser.add_argument('--with-unity', action='store_true')
    parser.add_argument('--output', help='Optional explicit detailed snapshot path; never writes by default')
    args = parser.parse_args()
    result = inspect(args.project_root, args.with_unity)
    if args.output:
        write_json(args.output, result)
    print(compact(result))


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    main()
