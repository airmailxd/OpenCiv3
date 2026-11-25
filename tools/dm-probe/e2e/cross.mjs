// CHAT.md's end-to-end check across the three clients on one running server (SERVER):
//   the AirGames launcher (Electron, C:\Dev\airgames, its own messages client, driven by cross-driver.cjs) as lena,
//   OpenCiv's DirectMessages client (C:\Dev\C3-wt\chat-fix\tools\dm-probe) as otto,
//   Roma itself (Godot, ui/game.gd --check=hub --parts=dm, C:\Dev\roma) as hubcheck, twice: whispering with lena, then otto.
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import readline from 'node:readline';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { runCheck } from 'file:///C:/Dev/roma/godot/tools/check_run.mjs';

const SERVER = process.env.SERVER || 'http://127.0.0.1:5094';
const OUT = process.env.CROSS_OUT;
const SHOTS_REL = process.env.ROMA_SHOTS_REL;
const ONLY = process.env.CROSS_ONLY || 'A,launcher,openciv';
fs.mkdirSync(OUT, { recursive: true });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const results = [];
function check(ok, what) { results.push({ ok: !!ok, what }); console.log(`${ok ? 'ok  ' : 'FAIL'} ${what}`); }
async function until(fn, ms = 15000) { const end = Date.now() + ms; while (Date.now() < end) { const v = await fn(); if (v) return v; await sleep(150); } return null; }
const children = [];

// ---- the launcher
const require = createRequire('C:/Dev/airgames/package.json');
const electron = require('electron');
const LPORT = 5197;
const lenv = { ...process.env, AIRGAMES_SERVER: SERVER, AIRGAMES_HOME: path.join(OUT, 'home-lena'), AIRGAMES_NO_AUTOUPDATE: '1', AIRGAMES_SMOKE: '1',
  AIRGAMES_SMOKE_DRIVER: path.join(path.dirname(fileURLToPath(import.meta.url)), 'cross-driver.cjs'), AIRGAMES_NODE: process.execPath,
  CROSS_USER: 'lena', CROSS_PASSWORD: 'cross-pass-1', CROSS_PORT: String(LPORT) };
delete lenv.ELECTRON_RUN_AS_NODE;
const lproc = spawn(electron, ['C:/Dev/airgames'], { env: lenv, stdio: ['ignore', 'pipe', 'pipe'] });
children.push(lproc);
let lready = false;
const llog = fs.createWriteStream(path.join(OUT, 'launcher.log'));
for (const s of [lproc.stdout, lproc.stderr]) s.on('data', (d) => { llog.write(d); if (String(d).includes('CROSS_READY')) lready = true; if (String(d).includes('CROSS_FAIL')) console.log(String(d)); });
const L = async (p, body) => (await fetch(`http://127.0.0.1:${LPORT}${p}`, body ? { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) } : {})).json();

// ---- OpenCiv's client
const oproc = spawn('dotnet', ['C:/Dev/C3-wt/chat-fix/tools/dm-probe/bin/Debug/net8.0/DmProbe.dll', '--server', SERVER, '--user', 'otto', '--password', 'cross-pass-1'], { stdio: ['pipe', 'pipe', 'pipe'] });
children.push(oproc);
const oevents = [];
const olog = fs.createWriteStream(path.join(OUT, 'openciv.log'));
readline.createInterface({ input: oproc.stdout }).on('line', (l) => { olog.write(l + '\n'); if (l.startsWith('{')) oevents.push({ at: Date.now(), ...JSON.parse(l) }); });
oproc.stderr.on('data', (d) => olog.write(d));
const O = (cmd) => oproc.stdin.write(cmd + '\n');
const oSaw = (pred, after = 0) => oevents.find((e) => e.at >= after && pred(e));
const lSaw = async (pred, after = 0) => (await L(`/events?after=${after}`)).find(pred);

let code = 1;
try {
  check(await until(() => lready, 60000), 'launcher (lena) logged in through its page, live socket open');
  const st = await L('/state');
  check(st.live, 'launcher: messages.snapshot().live');
  check(await until(() => oSaw((e) => e.ev === 'ready'), 30000), 'OpenCiv client (otto) signed in, live socket open');

  if (ONLY.includes('A')) {
    // ---- A: launcher <-> OpenCiv
    let t = Date.now();
    let r = await L('/send', { to: 'otto', text: 'A1 launcher to openciv' });
    check(r.ok, `launcher sends to otto (${JSON.stringify(r.message || r.error)})`);
    check(await until(() => oSaw((e) => e.ev === 'Message' && e.from === 'lena' && e.body === 'A1 launcher to openciv' && !e.fromMe, t)), 'OpenCiv receives it on its live socket as "[From lena]: A1 launcher to openciv"');
    t = Date.now();
    O('w lena A2 openciv to launcher');
    check(await until(() => oSaw((e) => e.ev === 'Message' && e.fromMe && e.body === 'A2 openciv to launcher', t)), 'OpenCiv /w lena: shown back as [To lena]');
    check(await until(() => lSaw((e) => e.kind === 'dm' && e.message.from === 'otto' && e.message.text === 'A2 openciv to launcher' && !e.fromMe, t)), 'launcher receives it on its live socket');
    check(await until(async () => (await L('/ui?name=otto')).tab), "launcher's page shows a conversation tab for otto");
    t = Date.now();
    O('r A3 openciv reply via /r');
    check(await until(() => lSaw((e) => e.kind === 'dm' && e.message.from === 'otto' && e.message.text === 'A3 openciv reply via /r', t)), 'OpenCiv /r (to lena, who last messaged) reaches the launcher');
    // ignore set in the launcher
    r = await L('/ignore', { name: 'otto', on: true });
    check(r.ok, 'launcher ignores otto');
    t = Date.now();
    O('w lena A4 should be refused');
    const refused = await until(() => oSaw((e) => e.ev === 'Notice', t));
    check(refused && refused.text === "lena isn't taking messages.", `OpenCiv told: "${refused && refused.text}"`);
    await sleep(1500);
    check(!(await lSaw((e) => e.kind === 'dm' && e.message.text === 'A4 should be refused', t)), 'launcher got nothing from otto while ignoring');
    r = await L('/ignore', { name: 'otto', on: false });
    t = Date.now();
    O('w lena A5 after unignore');
    check(r.ok && await until(() => lSaw((e) => e.kind === 'dm' && e.message.text === 'A5 after unignore', t)), 'launcher unignores otto; his next message arrives');
    // ignore set in OpenCiv
    t = Date.now();
    O('ignore lena');
    check(await until(() => oSaw((e) => e.ev === 'Notice' && e.text === "You're ignoring lena.", t)), 'OpenCiv /ignore lena');
    r = await L('/send', { to: 'otto', text: 'A6 should be refused' });
    check(!r.ok && r.status === 403 && r.error === "otto isn't taking messages.", `launcher refused: ${r.status} "${r.error}"`);
    t = Date.now();
    O('unignore lena');
    check(await until(() => oSaw((e) => e.ev === 'Notice' && e.text === "You're no longer ignoring lena.", t)), 'OpenCiv /unignore lena');
  }

  // ---- B, C: Roma with each
  for (const [peer, kind] of [['lena', 'launcher'], ['otto', 'openciv']]) {
    if (!ONLY.includes(kind)) continue;
    const send = async (text) => {
      if (kind === 'launcher') { const x = await L('/send', { to: 'hubcheck', text }); return { ok: x.ok, why: x.error }; }
      const at = Date.now();
      O(`w hubcheck ${text}`);
      const e = await until(() => oSaw((e) => (e.ev === 'Notice') || (e.ev === 'Message' && e.fromMe && e.body === text), at), 8000);
      return { ok: !!e && e.ev === 'Message', why: e && e.ev === 'Notice' ? e.text : null };
    };
    const heard = async (pred, after) => kind === 'launcher'
      ? (await L(`/events?after=${after}`)).filter((e) => e.kind === 'dm' && !e.fromMe && e.message.from === 'hubcheck').map((e) => e.message.text).find(pred)
      : (oSaw((e) => e.ev === 'Message' && !e.fromMe && e.from === 'hubcheck' && pred(e.body), after) || {}).body;
    const left = await send(`Waiting for you at the Forum (from ${kind})`);
    check(left.ok, `${kind} (${peer}) leaves hubcheck a message before Roma joins`);
    const t0 = Date.now();
    const answered = new Set();
    let thanks = false, done = false;
    const probes = [];
    const responder = (async () => {
      let k = 0, nextProbe = 0;
      while (!done) {
        const ping = await heard((x) => /^ping \d+$/.test(x) && !answered.has(x), t0);
        if (ping) { answered.add(ping); const s = await send(`pong ${ping.slice(5)}`); console.log(`${kind}: heard "${ping}" from Roma, answered (${s.ok})`); }
        if (!thanks && await heard((x) => /^thanks \d+$/.test(x), t0)) { thanks = true; console.log(`${kind}: heard Roma's /r thanks`); }
        if (thanks && Date.now() >= nextProbe) { nextProbe = Date.now() + 2000; const s = await send(`probe ${++k}`); probes.push({ k, at: Date.now(), ...s }); }
        await sleep(300);
      }
    })();
    const run = await runCheck(['--check=hub', `--hub=${SERVER}`, '--hub_profile=check', '--topdown=1', '--parts=dm', `--dm_bot=${peer}`, '--bots=0',
      ...(SHOTS_REL ? [`--pictures_dir=${SHOTS_REL}/${kind}`] : [])], { size: [1920, 1080], log: path.join(OUT, `roma_${kind}.txt`) });
    await sleep(4500);                       // (a probe or two after the game has gone: /unignore was its last act)
    done = true;
    await responder;
    let rc = null;
    for (const l of run.lines) { const m = /^ROMA_HUB_CHECK (.*)$/.exec(l); if (m) rc = JSON.parse(m[1]); }
    for (const l of run.lines) if (/^(ok |FAIL|note)/.test(l)) console.log(`   roma: ${l}`);
    check(rc && rc.failed === 0 && rc.passed > 0, `Roma (real game, hub socket) whispering with ${peer}: ${rc ? `${rc.passed} passed, ${rc.failed} failed` : 'no result'}`);
    check(answered.size > 0, `${kind} received Roma's "/w ${peer} ping" on its live socket and answered through its own client`);
    check(thanks, `${kind} received Roma's /r`);
    if (kind === 'launcher') check((await L('/ui?name=hubcheck')).tab, "launcher's page shows a conversation tab for hubcheck");
    const seq = probes.map((p) => (p.ok ? '+' : p.why === "hubcheck isn't taking messages." ? 'x' : `?(${p.why})`)).join('');
    console.log(`${kind} probes (+ delivered, x refused as ignored): ${seq}`);
    check(/^\+*x+\++$/.test(seq), `Roma's /ignore ${peer} refused ${kind}'s messages ("hubcheck isn't taking messages.") and /unignore let them through again`);
  }
  code = results.every((x) => x.ok) ? 0 : 1;
} catch (e) {
  console.log(`ERROR ${e.stack || e}`);
} finally {
  try { O('quit'); } catch {}
  try { await L('/quit'); } catch {}
  await sleep(1500);
  for (const c of children) if (c.exitCode === null) c.kill();
}
console.log(`${results.length} checks, ${results.filter((x) => !x.ok).length} failed`);
process.exit(code);
