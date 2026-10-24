"""Bounded output and local stage evidence. No cache, scheduler or implicit retries."""
from pathlib import Path
from contextlib import contextmanager
from datetime import datetime, timezone
import hashlib
import json
import subprocess
import time


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def write_json(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + '.tmp')
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    temporary.replace(path)


def compact(value, limit=2048):
    """Never silently truncate JSON; refuse and let the caller supply a projection."""
    text = json.dumps(value, ensure_ascii=False, separators=(',', ':'))
    if len(text.encode('utf-8')) > limit:
        raise ValueError('SUMMARY_TOO_LARGE: return a projection and diagnostic path')
    return text


class Metrics:
    def __init__(self, directory):
        self.directory = Path(directory)
        self.directory.mkdir(parents=True, exist_ok=True)
        self.events = []
        self.unity_calls = 0

    @contextmanager
    def stage(self, name):
        event = dict(stage=name, startUtc=datetime.now(timezone.utc).isoformat(), attempt=1,
                     status='RUNNING', cacheHit=False, errorCode=None)
        start = time.monotonic()
        before = self.unity_calls
        try:
            yield event
            event['status'] = 'PASS'
        except Exception as error:
            event.update(status='FAILED', errorCode=str(error).split(':', 1)[0][:80])
            raise
        finally:
            event.update(endUtc=datetime.now(timezone.utc).isoformat(),
                         durationMs=round((time.monotonic()-start)*1000, 3),
                         unityCalls=self.unity_calls-before)
            self.events.append(event)
            with (self.directory/'stages.jsonl').open('a', encoding='utf-8') as stream:
                stream.write(json.dumps(event, ensure_ascii=False)+'\n')

    def process(self, name, args, cwd, *, unity=False, timeout=120):
        if unity:
            self.unity_calls += 1
        started = time.monotonic()
        stem = self.directory/name
        try:
            result = subprocess.run(args, cwd=cwd, capture_output=True, timeout=timeout)
            stdout, stderr = result.stdout, result.stderr
            exit_code = result.returncode
        except subprocess.TimeoutExpired as error:
            stdout, stderr, exit_code = error.stdout or b'', error.stderr or b'', -1
        stem.with_suffix('.stdout.log').write_bytes(stdout)
        stem.with_suffix('.stderr.log').write_bytes(stderr)
        write_json(stem.with_suffix('.process.json'), dict(
            attempt=1, durationMs=round((time.monotonic()-started)*1000, 3), exitCode=exit_code,
            unityCalls=int(unity), stdoutBytes=len(stdout), stderrBytes=len(stderr),
            logBytes=len(stdout)+len(stderr), diagnostic=str(stem.with_suffix('.stdout.log'))))
        if exit_code:
            raise RuntimeError('PROCESS_FAILED: '+name+'; diagnostics='+str(stem))
        return stdout.decode('utf-8-sig')
