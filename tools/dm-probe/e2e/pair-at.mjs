// chat-check.mjs --real's pair, pointed at an already running server (CHECK_SERVER), not one it starts.
// The launchers and tools/chat-driver.cjs are C:\Dev\airgames' own; nothing there is changed.
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
const root = 'C:/Dev/airgames';
const require = createRequire(path.join(root, 'package.json'));
const electron = require('electron');
const server = process.env.CHECK_SERVER || 'http://127.0.0.1:5094';
const OUT = process.env.CHECK_OUT;
const PASSWORD = 'chat-pass-1';
function launcher(role, home) {
  return new Promise((resolve) => {
    const env = { ...process.env };
    delete env.ELECTRON_RUN_AS_NODE;
    Object.assign(env, { AIRGAMES_SERVER: server, AIRGAMES_HOME: home, AIRGAMES_NO_AUTOUPDATE: '1', AIRGAMES_SMOKE: '1',
      AIRGAMES_SMOKE_DRIVER: path.join(root, 'tools', 'chat-driver.cjs'), AIRGAMES_CHAT_ROLE: role, AIRGAMES_SHOTS: path.join(OUT, 'shots'),
      AIRGAMES_NODE: process.execPath, AIRGAMES_STANDIN_BUILD_TOTAL: '4', AIRGAMES_STANDIN_BUILD_MS: '50' });
    const child = spawn(electron, [root], { env, stdio: ['ignore', 'pipe', 'pipe'] });
    let result = null;
    const timer = setTimeout(() => { console.log(`${role} took too long`); child.kill(); }, 4 * 60 * 1000);
    const lines = (d) => { for (const line of String(d).split(/\r?\n/)) { if (!line.trim()) continue; if (line.startsWith('SMOKE_RESULT')) result = line;
      if (/^\s|^[a-z]|^SMOKE/.test(line) && !/DevTools|GPU|gpu_|ERROR:|WARNING:/.test(line)) console.log(`[${role}] ${line}`); } };
    child.stdout.on('data', lines); child.stderr.on('data', lines);
    child.once('exit', (code) => { clearTimeout(timer); resolve({ code, result }); });
  });
}
async function as(token, method, p, body) {
  const r = await fetch(`${server}${p}`, { method, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) }, body: body ? JSON.stringify(body) : undefined });
  const t = await r.text(); return { status: r.status, data: t ? JSON.parse(t) : null };
}
fs.mkdirSync(path.join(OUT, 'shots'), { recursive: true });
const tok = {};
for (const n of ['alice', 'bob']) tok[n] = (await as(null, 'POST', '/api/v1/sessions', { username: n, password: PASSWORD })).data.token;
console.log('friend request', (await as(tok.alice, 'POST', '/api/v1/friends/requests', { username: 'bob' })).status,
  'accept', (await as(tok.bob, 'POST', '/api/v1/friends/requests/alice/accept')).status);
const [a, b] = await Promise.all([launcher('pair-a', path.join(OUT, 'home-a')), launcher('pair-b', path.join(OUT, 'home-b'))]);
let n = 0, ok = true;
for (const [r, who] of [[a, 'pair-a'], [b, 'pair-b']]) {
  if (r.code !== 0 || !r.result || !r.result.includes('pass')) { ok = false; console.log(`${who} FAIL: ${r.result || `exited ${r.code}`}`); }
  else n += Number(/pass (\d+)/.exec(r.result)[1]);
}
console.log(ok ? `CHAT PASS: ${n} checks` : 'CHAT FAIL');
process.exit(ok ? 0 : 1);
