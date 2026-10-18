(async function syncAgentReport(tools, options = {}) {
  // Run inside the host agent's tools environment, not Node or a background daemon.
  const root = options.projectRoot;
  if (!root || !/^[A-Za-z]:\\/.test(root)) throw new Error('Pass the absolute projectRoot.');
  const quote = value => "'" + String(value).replace(/'/g, "''") + "'";
  const local = name => root + '\\AgentSync\\' + name;
  // SHA256 over UTF-8: the host isolate has no Node/crypto dependency. Avoid returning
  // a large local patch through a token-limited shell just to compare its bytes.
  function sha256(value) {
    const bytes = [];
    for (const char of value) {
      const c = char.codePointAt(0);
      if (c < 128) bytes.push(c);
      else if (c < 2048) bytes.push(192 | c >> 6, 128 | c & 63);
      else if (c < 65536) bytes.push(224 | c >> 12, 128 | c >> 6 & 63, 128 | c & 63);
      else bytes.push(240 | c >> 18, 128 | c >> 12 & 63, 128 | c >> 6 & 63, 128 | c & 63);
    }
    const bitLength = bytes.length * 8;
    bytes.push(128);
    while (bytes.length % 64 !== 56) bytes.push(0);
    bytes.push(0,0,0,0,bitLength >>> 24,bitLength >>> 16 & 255,bitLength >>> 8 & 255,bitLength & 255);
    const k = [0x428a2f98,0x71374491,0xb5c0fbcf,0xe9b5dba5,0x3956c25b,0x59f111f1,0x923f82a4,0xab1c5ed5,0xd807aa98,0x12835b01,0x243185be,0x550c7dc3,0x72be5d74,0x80deb1fe,0x9bdc06a7,0xc19bf174,0xe49b69c1,0xefbe4786,0x0fc19dc6,0x240ca1cc,0x2de92c6f,0x4a7484aa,0x5cb0a9dc,0x76f988da,0x983e5152,0xa831c66d,0xb00327c8,0xbf597fc7,0xc6e00bf3,0xd5a79147,0x06ca6351,0x14292967,0x27b70a85,0x2e1b2138,0x4d2c6dfc,0x53380d13,0x650a7354,0x766a0abb,0x81c2c92e,0x92722c85,0xa2bfe8a1,0xa81a664b,0xc24b8b70,0xc76c51a3,0xd192e819,0xd6990624,0xf40e3585,0x106aa070,0x19a4c116,0x1e376c08,0x2748774c,0x34b0bcb5,0x391c0cb3,0x4ed8aa4a,0x5b9cca4f,0x682e6ff3,0x748f82ee,0x78a5636f,0x84c87814,0x8cc70208,0x90befffa,0xa4506ceb,0xbef9a3f7,0xc67178f2];
    const h = [0x6a09e667,0xbb67ae85,0x3c6ef372,0xa54ff53a,0x510e527f,0x9b05688c,0x1f83d9ab,0x5be0cd19];
    const rotate = (x,n) => x >>> n | x << 32-n;
    for (let offset=0; offset<bytes.length; offset+=64) {
      const w = new Uint32Array(64);
      for (let i=0;i<16;i++) w[i] = bytes[offset+4*i]<<24 | bytes[offset+4*i+1]<<16 | bytes[offset+4*i+2]<<8 | bytes[offset+4*i+3];
      for (let i=16;i<64;i++) w[i] = (w[i-16] + (rotate(w[i-15],7)^rotate(w[i-15],18)^w[i-15]>>>3) + w[i-7] + (rotate(w[i-2],17)^rotate(w[i-2],19)^w[i-2]>>>10)) >>> 0;
      let [a,b,c,d,e,f,g,j] = h;
      for (let i=0;i<64;i++) {
        const t1 = (j + (rotate(e,6)^rotate(e,11)^rotate(e,25)) + (e&f ^ ~e&g) + k[i] + w[i]) >>> 0;
        const t2 = ((rotate(a,2)^rotate(a,13)^rotate(a,22)) + (a&b ^ a&c ^ b&c)) >>> 0;
        j=g;g=f;f=e;e=(d+t1)>>>0;d=c;c=b;b=a;a=(t1+t2)>>>0;
      }
      [a,b,c,d,e,f,g,j].forEach((v,i) => h[i]=(h[i]+v)>>>0);
    }
    return h.map(v=>v.toString(16).padStart(8,'0')).join('');
  }
  async function shell(command) {
    const result = await tools.exec_command({cmd: command, workdir: root, max_output_tokens: 16000});
    if (result.exit_code !== 0) throw new Error('AgentSync local step failed; inspect locally. No raw output published.');
    return result.output.trim();
  }
  async function readText(path) { return shell('Get-Content -LiteralPath ' + quote(path) + ' -Raw -Encoding UTF8'); }
  async function readJson(path) { return JSON.parse(await readText(path)); }
  async function writeJson(path, value) {
    const temporary = path + '.sync.tmp';
    const chars = Array.from(JSON.stringify(value, null, 2) + '\n');
    for (let offset=0; offset<chars.length; offset+=6000) {
      await shell('[IO.File]::' + (offset ? 'AppendAllText' : 'WriteAllText') + '(' + quote(temporary) + ',' + quote(chars.slice(offset,offset+6000).join('')) + ',[Text.UTF8Encoding]::new($false))');
    }
    await shell('if(Test-Path -LiteralPath ' + quote(path) + '){[IO.File]::Replace(' + quote(temporary) + ',' + quote(path) + ',[NullString]::Value)}else{[IO.File]::Move(' + quote(temporary) + ',' + quote(path) + ')}');
  }
  function unpack(result) {
    if (result.isError) throw new Error('Drive connector failed; latest STATE has not been advanced.');
    const data = result.structuredContent;
    if (!data || data.success === false) throw new Error('Drive returned no usable metadata.');
    return data;
  }
  const filesIn = async url => unpack(await tools.mcp__codex_apps__google_drive_list_folder({url, top_k: 100})).files;
  async function observed(id) {
    const metadata = unpack(await tools.mcp__codex_apps__google_drive_get_file_metadata({fileId: id}));
    if (!metadata.id || !metadata.url) throw new Error('Drive metadata lacks an observed file URL.');
    return metadata;
  }
  async function fetched(file) {
    const data = unpack(await tools.mcp__codex_apps__google_drive_fetch({url: file.url}));
    if (typeof data.content !== 'string') throw new Error('Readable Drive file text is unavailable.');
    return data.content;
  }
  if (options.generate !== false) {
    await shell('& ' + quote(root + '\\Tools\\Generate-AgentReport.ps1') + ' -Task ' + quote(options.task || 'Project status') + ' -Status ' + quote(options.status || 'ready_for_review'));
  }
  await shell('python Tools/AgentSync/agent_report.py --validate-outbound');
  const plan = await readJson(local('SYNC_PLAN.json'));
  const binding = await readJson(local('drive-binding.json'));
  const state = await readJson(plan.statePath);
  if (!binding.folderId || !binding.reportsFolderId || state.reportId !== plan.reportId) throw new Error('Missing Drive binding or mixed report versions.');
  const saveBinding = () => writeJson(local('drive-binding.json'), binding);
  binding.reportFolders ||= {};
  let folder = binding.reportFolders[plan.reportId];
  if (!folder) {
    const existing = (await filesIn(binding.reportsFolderUrl)).filter(f => f.title === plan.reportId);
    if (existing.length > 1) throw new Error('Duplicate report folders; resolve before publishing.');
    if (existing.length) folder = existing[0];
    else folder = unpack(await tools.mcp__codex_apps__google_drive_create_folder({name: plan.reportId, parent_folder: binding.reportsFolderId}));
    if (!folder.id) throw new Error('Drive folder creation returned no ID.');
    if (!folder.url) folder = await observed(folder.id);
    binding.reportFolders[plan.reportId] = {id: folder.id, url: folder.url};
    await saveBinding();
  }
  const children = await filesIn(folder.url);
  const rootChildren = await filesIn(binding.folderUrl);
  async function put(name, path, parentId, listing, expectedId) {
    const matches = listing.filter(f => f.title === name);
    if (matches.length > 1) throw new Error('Duplicate Drive file: ' + name);
    const id = expectedId || matches[0]?.id;
    // Readable fetch returned empty for application/json. Keep JSON bytes as text/plain.
    const args = {file_uri: path, mime_type: name.endsWith('.json') || name.endsWith('.patch') ? 'text/plain' : 'text/markdown'};
    const response = id
      ? await tools.mcp__codex_apps__google_drive_update_file({...args, fileId: id, name})
      : await tools.mcp__codex_apps__google_drive_upload_file({...args, file_name: name, parent_folder_id: parentId});
    const data = unpack(response);
    const result = await observed(id || data.id);
    if (!id) listing.push(result);
    return result;
  }
  const uploaded = [];
  for (const file of plan.files) {
    // Snapshot folders are immutable: a rerun reads existing bytes instead of replacing them.
    let remote = children.find(f => f.title === file.name);
    if (children.filter(f => f.title === file.name).length > 1) throw new Error('Duplicate snapshot file.');
    if (!remote) remote = await put(file.name, file.path, folder.id, children);
    if (sha256(await fetched(remote)) !== file.sha256) throw new Error('Drive readback mismatch: ' + file.name);
    uploaded.push({...file, id: remote.id, url: remote.url, readback: 'PASS'});
  }
  const statusFile = plan.files.find(f => f.name === 'STATUS.md');
  const rootStatus = await put('STATUS.md', statusFile.path, binding.folderId, rootChildren, binding.rootStatusId);
  binding.rootStatusId = rootStatus.id;
  await saveBinding();
  if (sha256(await fetched(rootStatus)) !== statusFile.sha256) throw new Error('Root STATUS readback mismatch.');
  const publication = {...state, sync: {mode: 'connected Google Drive via host agent', status: 'uploaded',
    folderUrl: binding.folderUrl, reportFolder: {id: folder.id, url: folder.url},
    snapshotReadback: 'PASS', snapshotState: uploaded.find(f => f.name === 'STATE.json'),
    publishedAtUtc: new Date().toISOString()}};
  publication.files = state.files.map(f => ({...f, id: uploaded.find(u => u.name === f.name).id, url: uploaded.find(u => u.name === f.name).url}));
  const rootStatePath = local('PUBLISHED_STATE.json');
  await writeJson(rootStatePath, publication);
  // Publication marker LAST: readers follow only its versioned URLs, never root convenience files.
  const rootState = await put('STATE.json', rootStatePath, binding.folderId, rootChildren, binding.rootStateId);
  binding.rootStateId = rootState.id;
  await saveBinding();
  const readback = JSON.parse(await fetched(rootState));
  if (JSON.stringify(readback) !== JSON.stringify(publication)) throw new Error('Root STATE readback mismatch.');
  const receipt = {reportId: plan.reportId, verifiedAtUtc: new Date().toISOString(), folderUrl: binding.folderUrl,
    rootState: {id: rootState.id, url: rootState.url, modifiedTime: rootState.modified_time},
    files: uploaded.map(({name,sha256,id,url,readback}) => ({name,sha256,id,url,readback})), status: 'PASS'};
  await writeJson(local('SYNC_RECEIPT.json'), receipt);
  return {status: receipt.status, reportId: plan.reportId, files: uploaded.length,
    rootStateId: rootState.id, modifiedTime: rootState.modified_time, folderUrl: binding.folderUrl};
})
