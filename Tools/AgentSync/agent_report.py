"""Local, bounded project inventory and atomic agent reports. Standard library only."""
from pathlib import Path
from datetime import datetime, timezone
import argparse, difflib, hashlib, json, os, re, shutil, subprocess, sys, uuid

ROOT = Path(__file__).resolve().parents[2]
SYNC = ROOT / 'AgentSync'
REPO_URL = 'https://github.com/AntonVokun/PaintByNumbersGame'
REPORT_NAMES = ('STATUS.md','CHANGES.md','TESTS.md','NEXT_STEPS.md','CODE_DIFF.patch','STATE.json')
SECRET_PATTERNS = [
    re.compile(r'\b\d{6,12}:[A-Za-z0-9_-]{25,}\b'),
    re.compile(r'\bgh[pousr]_[A-Za-z0-9_]{20,}\b'),
    re.compile(r'\bgithub_pat_[A-Za-z0-9_]{20,}\b'),
    re.compile(r'\bsk-[A-Za-z0-9_-]{20,}\b'),
    re.compile(r'\bya29\.[A-Za-z0-9_-]{20,}\b'),
    re.compile(r'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----'),
    re.compile(r'''(?im)\b(?:password|client_secret|access_token|refresh_token|api_key|bot_token|telegram_bot_token)\b\s*[=:]\s*["']?([A-Za-z0-9_./:+-]{12,})'''),
]


def atomic_write(path, data):
    path=Path(path);path.parent.mkdir(parents=True,exist_ok=True)
    if isinstance(data,str):data=data.encode('utf-8')
    temporary=path.with_name('.'+path.name+'.'+uuid.uuid4().hex+'.tmp')
    temporary.write_bytes(data)
    os.replace(temporary,path)


def json_bytes(value):return (json.dumps(value,ensure_ascii=False,indent=2)+'\n').encode('utf-8')
def sha(data):return hashlib.sha256(data).hexdigest()
def read_json(path,default=None):return json.loads(Path(path).read_text(encoding='utf-8-sig')) if Path(path).exists() else default
def clean_scan(text):return not any(p.search(text) for p in SECRET_PATTERNS)
def rel(path):return Path(path).relative_to(ROOT).as_posix()


def git(*args):
    r=subprocess.run(['git','-c','core.quotepath=false',*args],cwd=ROOT,capture_output=True,timeout=30)
    if r.returncode:raise RuntimeError('Git command failed: '+args[0])
    return r.stdout.decode('utf-8').rstrip('\r\n')


def changed_files():
    tracked=git('diff','--name-only','-z','HEAD').split('\0')
    untracked=git('ls-files','--others','--exclude-standard','-z').split('\0')
    return sorted(set(p for p in tracked+untracked if p))


def read_scene(path, script_guids):
    text=path.read_text(encoding='utf-8-sig')
    chunks=re.split(r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\n',text,flags=re.M)
    docs={int(chunks[i+1]):(int(chunks[i]),chunks[i+2]) for i in range(1,len(chunks),3)}
    def field(body,key):
        m=re.search(r'^  '+re.escape(key)+r': (.*)$',body,re.M);return m.group(1) if m else ''
    names={i:field(b,'m_Name') for i,(kind,b) in docs.items() if kind==1}
    roots=[];custom=[];palette=[];zones=[];next_scene=None;prefabs=[];cards=[];reset_scenes=[]
    for i,(kind,b) in docs.items():
        if kind in (4,224) and field(b,'m_Father')=='{fileID: 0}':
            m=re.search(r'm_GameObject: \{fileID: (-?\d+)\}',b)
            if m and names.get(int(m.group(1))):roots.append(names[int(m.group(1))])
        if kind==114:
            m=re.search(r'm_Script: \{fileID:.*?guid: (\w+)',b)
            script=script_guids.get(m.group(1)) if m else None
            if script:custom.append(script)
            if script and script.endswith('/PaintZone.cs'):zones.append(int(field(b,'colorId')))
            if script and script.endswith('/SelectColor.cs'):palette.append(int(field(b,'colorId')))
            if script and script.endswith('/PaintController.cs'):next_scene=field(b,'nextLevelSceneName')
            if script and script.endswith('/LevelCardUI.cs'):cards.append(field(b,'levelSceneName'))
            if script and script.endswith('/MainMenuController.cs'):
                array=re.search(r'^  levelSceneNames:\n((?:  - .*\n)+)',b,re.M)
                if array:reset_scenes=re.findall(r'^  - (.+)$',array.group(1),re.M)
        if kind==1001:
            m=re.search(r'm_SourcePrefab: \{fileID:.*?guid: (\w+)',b)
            if m:prefabs.append(m.group(1))
    return dict(path=rel(path),sha256=sha(path.read_bytes()),roots=sorted(roots),
        components=sorted(set(custom)),paintZones=len(zones),paletteButtons=len(palette),
        colorIds=sorted(set(zones)),nextScene=next_scene,prefabGuids=sorted(set(prefabs)),levelCards=cards,resetScenes=reset_scenes)


def inventory():
    guids={}
    for file in (ROOT/'Assets/_Project').rglob('*.cs.meta'):
        m=re.search(r'^guid: (\w+)',file.read_text(encoding='utf-8-sig'),re.M)
        if m:guids[m.group(1)]=rel(Path(str(file)[:-5]))
    build=(ROOT/'ProjectSettings/EditorBuildSettings.asset').read_text(encoding='utf-8-sig')
    enabled={p for state,p in re.findall(r'- enabled: (\d+)\n\s+path: (.+)',build) if state=='1'}
    scenes=[read_scene(p,guids) for p in sorted((ROOT/'Assets/_Project/Scenes').glob('*.unity'))]
    for s in scenes:s['enabled']=s['path'] in enabled
    manifests=[]
    for path in sorted((ROOT/'ArtSources').glob('*/level_manifest.json')):
        m=read_json(path)
        manifests.append({k:m.get(k) for k in ('sceneName','displayName','artFolder','originalImage','sourceFolder','width','height','colors','regions','validation')})
        level=manifests[-1];level['path']=rel(path)
        scene=next((s for s in scenes if Path(s['path']).stem==m['sceneName']),None)
        menu=next((s for s in scenes if Path(s['path']).stem=='MainMenu'),{})
        level['scene']=scene
        level['importedResources']=[m['artFolder']+'/'+f for f in [m['outlineFile'],m['colorPreviewFile'],m['bwPreviewFile'],*[g['file'] for g in m['groups']]]]
        level['menuCardPresent']=m['sceneName'] in menu.get('levelCards',[])
        level['resetRegistered']=m['sceneName'] in menu.get('resetScenes',[])
        level['buildEnabled']=bool(scene and scene['enabled'])
    result=dict(schema=1,unityVersion=re.search(r'm_EditorVersion: (.+)',(ROOT/'ProjectSettings/ProjectVersion.txt').read_text()).group(1),
        scenes=scenes,scripts=sorted(guids.values()),prefabs=sorted(rel(p) for p in (ROOT/'Assets/_Project').rglob('*.prefab')),
        resources=sorted(rel(p) for p in (ROOT/'Assets/_Project/Art').rglob('*.png')),
        packages=read_json(ROOT/'Packages/manifest.json')['dependencies'],levelManifests=manifests)
    atomic_write(ROOT/'docs/generated/PROJECT_INVENTORY.json',json_bytes(result))
    return result


def validate_docs():
    files=[ROOT/'AGENTS.md',ROOT/'README.md',*sorted((ROOT/'docs').rglob('*.md')),
           *sorted(ROOT.glob('LEVEL_CREATION_*.md'))]
    broken=[];links=0;paths=0
    for path in files:
        if not path.exists():broken.append(rel(path));continue
        text=path.read_text(encoding='utf-8-sig')
        for target in re.findall(r'\[[^\]]*\]\(([^)]+)\)',text):
            target=target.strip('<>').split('#')[0]
            if not target or re.match(r'^[a-z]+://|^mailto:',target):continue
            links+=1
            if not (path.parent/target).is_file():broken.append(rel(path)+' -> '+target)
        for target in re.findall(r'`([^`\n]+)`',text):
            if not target.startswith(('Assets/','Tools/','ProjectSettings/','Packages/','docs/')):continue
            if any(c in target for c in ('*','<','>','(',')','NNN')):continue
            if not re.search(r'\.(cs|json|prefab|unity|ps1|py|md|js|asset)$',target):continue
            paths+=1
            if not (ROOT/target).exists():broken.append(rel(path)+' -> '+target)
    result=dict(status='PASS' if not broken else 'FAIL',documents=len(files),localLinks=links,literalPaths=paths,broken=broken)
    if broken:raise RuntimeError('Documentation paths: '+json.dumps(result,ensure_ascii=True))
    return result


EDITOR_CODE=r'''var t=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.LogEntries");object[] c={0,0,0};t.GetMethod("GetCountsByType",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic).Invoke(null,c);var b=UnityEditor.Build.Reporting.BuildReport.GetLatestReport();return new{compiling=UnityEditor.EditorApplication.isCompiling,updating=UnityEditor.EditorApplication.isUpdating,playing=UnityEngine.Application.isPlaying,console=new{errors=c[0],warnings=c[1],logs=c[2]},openScenes=Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount).Select(i=>UnityEngine.SceneManagement.SceneManager.GetSceneAt(i)).Select(s=>new{s.path,s.isDirty,s.isLoaded}).ToArray(),lastBuild=b==null?null:new{result=b.summary.result.ToString(),errors=b.summary.totalErrors,warnings=b.summary.totalWarnings}};'''


def editor_snapshot():
    try:
        r=subprocess.run(['unity','command','eval','--project-path',str(ROOT),'--caller','plugin','--skill','unity-cli',
            '--format','json','--timeout','15','--','--code',EDITOR_CODE,'--timeout','8000'],cwd=ROOT,capture_output=True,timeout=22)
        envelope=json.loads(r.stdout.decode('utf-8-sig'))
        result=envelope.get('data',{}).get('result',{})
        if not envelope.get('success') or not result.get('success'):return dict(available=False,reason='Unity read command unavailable; do not infer Editor is closed')
        return dict(available=True,**result['result'])
    except (OSError,subprocess.TimeoutExpired,ValueError,KeyError):
        return dict(available=False,reason='Unity read command unavailable; no raw output exported')


def make_patch(paths,max_bytes=200000):
    output=[];included=[];omitted=[];used=0
    for name in paths:
        path=ROOT/name
        eligible=(name in ('AGENTS.md','README.md','.gitignore') or name.startswith(('docs/','Tools/','LEVEL_CREATION_','Assets/_Project/Editor/')))
        if not eligible or path.suffix.lower() not in ('.md','.py','.ps1','.js','.json','.cs','.gitignore') and name!='.gitignore':
            omitted.append(dict(path=name,reason='outside text allowlist'));continue
        if not path.is_file() or path.stat().st_size>95000:
            omitted.append(dict(path=name,reason='missing/oversize; use file list'));continue
        data=path.read_bytes()
        if b'\0' in data:omitted.append(dict(path=name,reason='binary'));continue
        text=data.decode('utf-8-sig')
        if not clean_scan(text):raise RuntimeError('Possible secret in outbound text; file withheld: '+name)
        tracked=subprocess.run(['git','ls-files','--error-unmatch','--',name],cwd=ROOT,capture_output=True).returncode==0
        if tracked:patch=git('diff','--no-ext-diff','--no-color','HEAD','--',name)+'\n'
        else:
            lines=text.replace('\r\n','\n').splitlines()
            patch=f'diff --git a/{name} b/{name}\nnew file mode 100644\n--- /dev/null\n+++ b/{name}\n@@ -0,0 +1,{len(lines)} @@\n'+''.join('+'+line+'\n' for line in lines)
        if not clean_scan(patch):raise RuntimeError('Possible secret in patch; file withheld: '+name)
        size=len(patch.encode('utf-8'))
        if used+size>max_bytes:omitted.append(dict(path=name,reason='total patch budget'));continue
        output.append(patch);used+=size;included.append(name)
    return ''.join(output),included,omitted


def generate(task,status,observations_path,skip_editor=False):
    facts=read_json(observations_path,{})
    now=datetime.now(timezone.utc);report_id=now.strftime('%Y%m%dT%H%M%SZ')+'-'+uuid.uuid4().hex[:8]
    inv=inventory();doc_result=validate_docs();changed=changed_files()
    editor=dict(available=False,reason='explicitly skipped') if skip_editor else editor_snapshot()
    patch,included,omitted=make_patch(changed)
    checks=facts.get('checks',[])
    for check in checks:
        check['currentArtifactsMatch']=all((ROOT/p).is_file() and sha((ROOT/p).read_bytes())==h for p,h in check.get('artifactHashes',{}).items()) if check.get('artifactHashes') else None
        if check.get('artifactHashes') and not check['currentArtifactsMatch']:check['status']='stale'
    state=dict(schema=1,projectId='PaintByNumbersGame',repository=REPO_URL,reportId=report_id,
        generatedAtUtc=now.isoformat(),generatedAtMoscow=now.astimezone(timezone(__import__('datetime').timedelta(hours=3))).isoformat(),
        timezone='Europe/Moscow',branch=git('branch','--show-current'),headSha=git('rev-parse','HEAD'),
        dirty=bool(changed),task=task,taskStatus=status,gitBase='HEAD',baseSha=git('rev-parse','HEAD'),
        remote=facts.get('remote',{}),editor=editor,documentation=doc_result,checks=checks,
        issues=facts.get('issues',[]),nextSteps=facts.get('nextSteps',[]),changedFiles=changed,
        patch=dict(included=included,omitted=omitted,bytes=len(patch.encode('utf-8')),secretScan='PASS',notFullProject=True),
        levels=inv['levelManifests'],sync=dict(mode='connected Google Drive via host agent',status='prepared_not_published'),files=[])
    for level in state['levels']:
        level['lastImport']=read_json(SYNC/'level-imports'/(level['sceneName']+'.json'))
    title=f'Отчёт {report_id}\nВремя: {state["generatedAtMoscow"]} (Europe/Moscow)\nВетка: {state["branch"]}\nHEAD: {state["headSha"]}\n'
    status_text='# STATUS\n\n'+title+f'\nЗадача: {task}\nСтатус: {status}\nНезакоммиченные изменения: {len(changed)} файлов.\n\n'
    status_text+='## Выполнено\n\n'+''.join('- '+x+'\n' for x in facts.get('completed',[]))
    status_text+='\n## Проблемы и ограничения\n\n'+''.join('- '+x+'\n' for x in state['issues'])
    status_text+='\nЛокальный отчёт не равен опубликованному GitHub и не предоставляет доступ к Editor.\n'
    changes_text='# CHANGES\n\n'+title+'\nИзменения относительно локального HEAD (tracked + untracked):\n\n'+''.join('- `'+p+'`\n' for p in changed)
    changes_text+='\nНазначение задачи: '+task+'\n'
    for label,prefix,suffix in [('Затронутые сцены','Assets/','.unity'),('Затронутые скрипты','Assets/','.cs')]:
        selected=[p for p in changed if p.startswith(prefix) and p.endswith(suffix)]
        changes_text+='\n'+label+' (включая ранее незакоммиченные): '+', '.join(selected)+'\n'
    changes_text+='\nСозданные файлы (untracked):\n\n'+''.join('- `'+p+'`\n' for p in git('ls-files','--others','--exclude-standard','-z').split('\0') if p)
    changes_text+='\nВлияние/границы текущей задачи: '+facts.get('impact','Не определены автоматически; читать scope проверок.')+'\n'
    changes_text+='\nCODE_DIFF.patch содержит только выбранные текстовые файлы; сцены, PNG, .meta, исходники графики и большие файлы перечислены, но не передаются.\n'
    changes_text+='\nПропущено из patch: '+str(len(omitted))+' файлов. Точная база: '+state['baseSha']+'.\n'
    tests_text='# TESTS\n\n'+title+'\n## Текущие автоматические наблюдения\n\n```json\n'+json.dumps(dict(editor=editor,documentation=doc_result),ensure_ascii=False,indent=2)+'\n```\n'
    tests_text+='\n## Выполненные ранее / отдельные проверки\n\n'
    for check in checks:
        evidence=check.get('evidence','')
        if not isinstance(evidence,str):evidence=json.dumps(evidence,ensure_ascii=False)
        tests_text+='- '+check.get('name','check')+': '+check.get('status','unknown')+'; '+check.get('scope','')+'; источник: '+evidence+'\n'
    tests_text+='\nПодготовка файлов, статическая проверка, Play Mode, Android build и тест на устройстве — разные проверки.\n'
    tests_text+='\n## Уровни из манифестов и сохранённых сцен\n\n'
    for level in state['levels']:
        tests_text+=f'- {level["sceneName"]}: {level["width"]}×{level["height"]}, {level["colors"]} групп, {level["regions"]} островков; PaintZone={level["scene"]["paintZones"] if level["scene"] else "missing"}; menu={level["menuCardPresent"]}, reset={level["resetRegistered"]}, build={level["buildEnabled"]}. Source: {level["sourceFolder"]}. Подробные пути и результаты — STATE.json.\n'
    next_text='# NEXT_STEPS\n\n'+title+'\n'+''.join('- '+x+'\n' for x in state['nextSteps'])
    bodies={'STATUS.md':status_text,'CHANGES.md':changes_text,'TESTS.md':tests_text,'NEXT_STEPS.md':next_text,'CODE_DIFF.patch':patch}
    for name,body in bodies.items():
        if not clean_scan(body):raise RuntimeError('Possible secret in generated '+name)
        state['files'].append(dict(name=name,path='reports/'+report_id+'/'+name,sha256=sha(body.encode('utf-8')),bytes=len(body.encode('utf-8'))))
    state_data=json_bytes(state)
    if not clean_scan(state_data.decode('utf-8')):raise RuntimeError('Possible secret in generated STATE')
    staging=SYNC/('.staging-'+report_id);staging.mkdir(parents=True)
    for name,body in bodies.items():(staging/name).write_text(body,encoding='utf-8',newline='\n')
    (staging/'STATE.json').write_bytes(state_data)
    report_dir=SYNC/'reports'/report_id;report_dir.parent.mkdir(parents=True,exist_ok=True)
    os.replace(staging,report_dir)
    for name in REPORT_NAMES[:-1]:atomic_write(SYNC/name,(report_dir/name).read_bytes())
    atomic_write(SYNC/'STATE.json',state_data)  # Commit marker last. Readers use immutable paths in it.
    # P0 index only; full evidence and immutable snapshot format stay unchanged.
    sys.path.insert(0,str(ROOT/'Tools/LevelCreation'))
    from inspect_pipeline import lite_from_state
    atomic_write(SYNC/'STATE_LITE.json',json_bytes(lite_from_state(state)))
    plan=dict(schema=1,reportId=report_id,projectRoot=str(ROOT),statePath=str(report_dir/'STATE.json'),
        files=[dict(name=n,path=str(report_dir/n),sha256=sha((report_dir/n).read_bytes())) for n in REPORT_NAMES])
    atomic_write(SYNC/'SYNC_PLAN.json',json_bytes(plan))
    print(json.dumps(dict(reportId=report_id,files=len(REPORT_NAMES),changedFiles=len(changed),patchFiles=len(included),
        patchBytes=state['patch']['bytes'],secretScan='PASS',editorAvailable=editor['available'],sync='prepared; run connector publisher'),ensure_ascii=True))
    return state


def validate_outbound(plan_path):
    plan=read_json(plan_path)
    allowed=(SYNC/'reports'/plan['reportId']).resolve()
    if not allowed.is_relative_to((SYNC/'reports').resolve()):raise RuntimeError('Invalid report path')
    if len(plan['files'])!=len(REPORT_NAMES) or {f['name'] for f in plan['files']}!=set(REPORT_NAMES):raise RuntimeError('Unexpected outbound files')
    for f in plan['files']:
        path=Path(f['path']).resolve()
        if path.parent!=allowed or path.name!=f['name']:raise RuntimeError('Outbound file outside snapshot')
        data=path.read_bytes()
        if len(data)>1000000 or sha(data)!=f['sha256'] or not clean_scan(data.decode('utf-8')):raise RuntimeError('Outbound validation failed: '+f['name'])
    state=read_json(allowed/'STATE.json')
    if state['reportId']!=plan['reportId']:raise RuntimeError('Mixed report versions')
    if len(state['files'])!=5 or {f['name'] for f in state['files']}!=set(REPORT_NAMES[:-1]):raise RuntimeError('Invalid state file list')
    for f in state['files']:
        if sha((allowed/f['name']).read_bytes())!=f['sha256']:raise RuntimeError('Snapshot checksum mismatch')
    print(json.dumps(dict(status='PASS',reportId=plan['reportId'],files=len(plan['files']))))


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--inventory',action='store_true')
    parser.add_argument('--validate-docs',action='store_true')
    parser.add_argument('--validate-outbound',action='store_true')
    parser.add_argument('--task',default='Project status')
    parser.add_argument('--status',default='ready_for_review')
    parser.add_argument('--observations',default=str(SYNC/'observations.json'))
    parser.add_argument('--skip-editor',action='store_true')
    args=parser.parse_args()
    try:
        if args.inventory:
            inv=inventory();print(json.dumps(dict(scenes=len(inv['scenes']),scripts=len(inv['scripts']),prefabs=len(inv['prefabs']))))
        elif args.validate_docs:print(json.dumps(validate_docs()))
        elif args.validate_outbound:validate_outbound(SYNC/'SYNC_PLAN.json')
        else:generate(args.task,args.status,args.observations,args.skip_editor)
    except Exception as e:
        print(str(e),file=sys.stderr);sys.exit(1)
