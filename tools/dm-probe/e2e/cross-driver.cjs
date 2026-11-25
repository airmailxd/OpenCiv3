// A smoke driver for the AirGames launcher (loaded by app/main/main.js through AIRGAMES_SMOKE_DRIVER) for the
// end-to-end messages check: it logs in through the page as CROSS_USER, then takes orders on a loopback HTTP port
// (CROSS_PORT) and answers with what the launcher's own messages client (app/main/messages.js) did and heard.
'use strict';
const { app } = require('electron');
const http = require('node:http');
const USER = process.env.CROSS_USER;
const PASSWORD = process.env.CROSS_PASSWORD;
const PORT = Number(process.env.CROSS_PORT);
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
module.exports = function drive(main) {
  app.whenReady().then(async () => {
    let win;
    for (let i = 0; i < 100 && !(win = main.window()); i++) await sleep(50);
    const wc = win.webContents;
    const js = (code) => wc.executeJavaScript(code, true);
    const q = (s) => JSON.stringify(s);
    async function waitFor(sel, timeout = 15000) {
      const end = Date.now() + timeout;
      while (Date.now() < end) { if (await js(`!!document.querySelector(${q(sel)})`)) return; await sleep(100); }
      throw new Error(`timed out waiting for ${sel}`);
    }
    async function type(sel, v) { await waitFor(sel); await js(`(() => { const e = document.querySelector(${q(sel)}); e.focus(); e.value = ${q(v)}; e.dispatchEvent(new Event('input', { bubbles: true })); })()`); }
    const events = [];
    main.messages.on((ev) => { if (ev.kind !== 'state') { events.push({ at: Date.now(), ...ev }); console.log(`EV ${JSON.stringify(ev)}`); } });
    try {
      await waitFor('body[data-ready="1"]');
      await waitFor('[data-t="login-form"]');
      await type('[data-t="username"]', USER);
      await type('[data-t="password"]', PASSWORD);
      await js(`document.querySelector('[data-t="submit"]').click()`);
      await waitFor('[data-t="home"]');
      for (let i = 0; i < 300 && !main.messages.snapshot().live; i++) await sleep(100);
    } catch (e) { console.log(`CROSS_FAIL ${e.stack || e}`); app.exit(1); return; }
    const body = (req) => new Promise((ok) => { let s = ''; req.on('data', (d) => { s += d; }); req.on('end', () => ok(s ? JSON.parse(s) : {})); });
    http.createServer(async (req, res) => {
      let out;
      try {
        const u = new URL(req.url, 'http://x');
        if (u.pathname === '/send') { const b = await body(req); out = await main.messages.send(b.to, b.text); }
        else if (u.pathname === '/ignore') { const b = await body(req); out = await main.messages.ignore(b.name, b.on); }
        else if (u.pathname === '/events') out = events.filter((e) => e.at > Number(u.searchParams.get('after') || 0));
        else if (u.pathname === '/state') out = main.messages.snapshot();
        else if (u.pathname === '/ui') {
          const n = u.searchParams.get('name');
          out = await js(`(() => { const t = document.querySelector('[data-t="dm-tab"][data-name=${q(n)}]'); return { tab: !!t, text: t ? t.textContent : '' }; })()`);
        } else if (u.pathname === '/quit') { res.end('{}'); setTimeout(() => app.exit(0), 200); return; }
        else { res.statusCode = 404; out = {}; }
      } catch (e) { res.statusCode = 500; out = { error: String(e.stack || e) }; }
      res.setHeader('Content-Type', 'application/json');
      res.end(JSON.stringify(out));
    }).listen(PORT, '127.0.0.1', () => console.log(`CROSS_READY ${USER} live=${main.messages.snapshot().live}`));
  });
};
