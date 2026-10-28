"""A view-only live view of the map-making VMs, and a watchdog for them.

The page shows each VM's screen as a motion-JPEG stream, several frames a
second, three across and two down, with its progress (maps made of its
share, the time left at its pace), its last log line and what the watchdog
last did for it. Nothing on the page can reach the VMs.

The watchdog (see watchdog()) helps a VM that stops making maps: it
restarts the VM's script and Civ3, then resets the VM, and starts one that
isn't running. Its actions go to <corpus>/watchdog.log.

    python tools/civ3_maps/vm/watch_live.py [--port=8770] [--fps=4] [--watchdog=0]

then open http://127.0.0.1:8770 (this PC only).
"""
import glob
import io
import json
import os
import re
import subprocess
import sys
import tempfile
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

from PIL import Image

VBOX = r'C:\Program Files\Oracle\VirtualBox\VBoxManage.exe'
CORPUS = r'C:\Dev\civ3_map_corpus'
WIDTH = 800  # frames are shrunk to this width

frames = {}       # VM name -> latest JPEG bytes
lock = threading.Condition()


def running_vms():
    out = subprocess.run([VBOX, 'list', 'runningvms'], capture_output=True, text=True).stdout
    return sorted(re.findall(r'^"(civ3gen-[^"]+)"', out, re.M), key=lambda n: (len(n), n))


def grab(name, fps):
    path = os.path.join(tempfile.gettempdir(), f'watch_{name}.png')
    while True:
        t = time.time()
        r = subprocess.run([VBOX, 'controlvm', name, 'screenshotpng', path], capture_output=True)
        if r.returncode == 0:
            try:
                img = Image.open(path).convert('RGB')
                img = img.resize((WIDTH, int(img.height * WIDTH / img.width)))
                buf = io.BytesIO()
                img.save(buf, 'JPEG', quality=70)
                with lock:
                    frames[name] = buf.getvalue()
                    lock.notify_all()
            except OSError:
                pass
        time.sleep(max(0, 1 / fps - (time.time() - t)))


def all_vms():
    out = subprocess.run([VBOX, 'list', 'vms'], capture_output=True, text=True).stdout
    return sorted(re.findall(r'^"(civ3gen-\d+)"', out, re.M), key=lambda n: (len(n), n))


def progress(name):
    """(maps made, maps to make or None, last log line) of a VM's run."""
    folder = os.path.join(CORPUS, f'vm_{name}')
    maps = len(glob.glob(os.path.join(folder, '*.SAV')))
    total, log = None, ''
    try:
        with open(os.path.join(folder, 'run.log'), errors='replace') as f:
            lines = [line.strip() for line in f if line.strip()]
        log = lines[-1] if lines else ''
        for line in reversed(lines):
            m = re.search(r' of (\d+) made$', line)
            if m:
                total = int(m.group(1))
                break
    except OSError:
        pass
    return maps, total, log


# The watchdog (the owner's timings): a VM that makes no map for STALL
# seconds has its Python and Civ3 stopped, and run_vm.cmd's loop starts them
# again with the current scripts; again every RESTARTED seconds while there's
# still no map. Only a VM that has made no map for RESET seconds is reset.
# A VM that isn't running is started. After a reset or a start, and when the
# watchdog itself starts, a VM has BOOT seconds to log on and make its first
# map. A VM with a PAUSE file, or done, is left be.
STALL = 120
RESTARTED = 180
RESET = 600
BOOT = 600
watch = {}        # VM name -> {count, changed, stage, acted, note, history}
WATCHDOG_LOG = os.path.join(CORPUS, 'watchdog.log')


def note(name, text):
    line = f'{time.strftime("%H:%M:%S")} {name}: {text}'
    watch[name]['note'] = line
    with open(WATCHDOG_LOG, 'a') as f:
        f.write(line + '\n')


def guest(name, exe, *args):
    return subprocess.run([VBOX, 'guestcontrol', name, 'run', '--username', 'civ3', '--password', 'civ3',
                           '--wait-stdout', '--timeout', '30000', '--exe', exe, '--', *args],
                          capture_output=True, text=True, timeout=60)


def python_running(name):
    """Whether the VM's run_vm.cmd loop has a Python running (True if the
    guest can't be asked, so it isn't reset for that alone)."""
    try:
        r = guest(name, r'C:\Windows\System32\tasklist.exe', '/FI', 'IMAGENAME eq python.exe')
        return 'python.exe' in r.stdout
    except subprocess.TimeoutExpired:
        return True


def watchdog():
    while True:
        now = time.time()
        running = set(running_vms())
        for name in all_vms():
            # A VM seen for the first time may be booting.
            w = watch.setdefault(name, {'count': -1, 'changed': now, 'stage': 0, 'acted': now,
                                        'grace': now + BOOT, 'resets': 0, 'note': '', 'history': []})
            folder = os.path.join(CORPUS, f'vm_{name}')
            if os.path.exists(os.path.join(folder, 'PAUSE')):
                w['changed'] = now
                continue
            if name not in running:
                subprocess.run([VBOX, 'startvm', name, '--type', 'headless'], capture_output=True)
                note(name, 'not running: started it')
                w.update(changed=now, stage=0, grace=now + BOOT)
                continue
            count, total, _ = progress(name)
            w['history'] = [(t, c) for t, c in w['history'] if now - t < 1800] + [(now, count)]
            if count != w['count']:
                w.update(count=count, changed=now, stage=0, grace=0)
                continue
            if total is not None and count >= total:
                continue
            if now < w['grace']:
                continue
            idle = now - w['changed']
            if idle > RESET:
                subprocess.run([VBOX, 'controlvm', name, 'reset'], capture_output=True)
                w['resets'] += 1
                note(name, f'no map for {idle / 60:.0f} min after {w["stage"]} script restarts: reset the VM'
                     f' (reset {w["resets"]})' + ('; it keeps stalling, worth a look' if w['resets'] >= 3 else ''))
                w.update(stage=0, acted=now, changed=now, grace=now + BOOT)
                continue
            # stage: the script restarts since the last map.
            due = idle > STALL if w['stage'] == 0 else now - w['acted'] > RESTARTED
            if not due:
                continue
            if w['stage'] > 0 and not python_running(name):
                note(name, f'no map for {idle / 60:.0f} min and its script did not come back; '
                     f'reset at {RESET // 60} min')
                w['acted'] = now
                continue
            try:
                guest(name, r'C:\Windows\System32\taskkill.exe', '/F', '/IM', 'python.exe')
                guest(name, r'C:\Windows\System32\taskkill.exe', '/F', '/IM', 'Civ3Conquests.exe')
                w['stage'] += 1
                note(name, f'no map for {idle / 60:.0f} min: restarted its script and Civ3 (try {w["stage"]})')
            except subprocess.TimeoutExpired:
                note(name, f'no map for {idle / 60:.0f} min; the guest did not answer')
            w['acted'] = now
        time.sleep(30)


def status():
    result = []
    now = time.time()
    for name in sorted(frames, key=lambda n: (len(n), n)):
        maps, total, log = progress(name)
        w = watch.get(name, {})
        # The rate over the last half hour, for the time left.
        history = w.get('history', [])
        eta = None
        if total and len(history) > 1 and history[-1][1] > history[0][1]:
            rate = (history[-1][1] - history[0][1]) / (history[-1][0] - history[0][0])
            eta = int((total - maps) / rate / 60) if maps < total else 0
        result.append({'name': name, 'maps': maps, 'total': total, 'eta': eta, 'log': log,
                       'idle': int((now - w['changed']) / 60) if 'changed' in w else 0,
                       'watchdog': w.get('note', '')})
    return result


# Three across and two down, all on one screen: each cell takes a sixth of
# the window, and its stream shrinks to fit under its caption.
PAGE = """<!doctype html><meta charset="utf-8"><title>Civ3 VMs</title>
<style>html,body{height:100%;margin:0}
body{background:#111;color:#ddd;font:12px sans-serif;overflow:hidden}
#g{display:grid;grid-template-columns:repeat(3,1fr);grid-template-rows:repeat(2,1fr);gap:6px;
  height:100vh;padding:6px;box-sizing:border-box}
#g>div{display:flex;flex-direction:column;min-height:0;min-width:0}
.cap{white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
small{color:#9a9}.wd{color:#e9b44c}.idle{color:#e66}
.bar{height:4px;background:#333;margin:2px 0}.bar i{display:block;height:100%;background:#5a9;width:0}
img{flex:1;min-height:0;width:100%;object-fit:contain;background:#000;display:block}</style>
<div id="g"></div>
<script>
async function tick(){
  let s=await (await fetch('/status')).json();
  let g=document.getElementById('g');
  for (let v of s) {
    let d=document.getElementById(v.name);
    if (!d) {
      d=document.createElement('div'); d.id=v.name;
      d.innerHTML='<div class="cap"><b></b> <span></span> <span class="idle"></span></div>'
        +'<div class="bar"><i></i></div><div class="cap"><small></small></div>'
        +'<div class="cap wd"></div><img src="/stream/'+v.name+'">';
      g.appendChild(d);
    }
    d.querySelector('b').textContent=v.name;
    let done=v.total ? v.maps+' of '+v.total+' maps' : v.maps+' maps';
    if (v.eta!==null && v.eta!==undefined) done+=' Â· ~'+(v.eta>=60 ? Math.floor(v.eta/60)+' h '+(v.eta%60)+' min' : v.eta+' min')+' left';
    d.querySelector('span').textContent=done;
    d.querySelector('.idle').textContent=v.idle>=2 ? 'no new map for '+v.idle+' min' : '';
    d.querySelector('.bar i').style.width=(v.total ? Math.min(100, 100*v.maps/v.total) : 0)+'%';
    d.querySelector('small').textContent=v.log;
    d.querySelector('.wd').textContent=v.watchdog ? 'watchdog: '+v.watchdog : '';
  }
}
tick(); setInterval(tick, 3000);
</script>"""


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass

    def do_GET(self):
        if self.path == '/':
            body = PAGE.encode()
            self.send_response(200)
            self.send_header('Content-Type', 'text/html; charset=utf-8')
            self.send_header('Content-Length', str(len(body)))
            self.end_headers()
            self.wfile.write(body)
        elif self.path == '/status':
            body = json.dumps(status()).encode()
            self.send_response(200)
            self.send_header('Content-Type', 'application/json')
            self.send_header('Content-Length', str(len(body)))
            self.end_headers()
            self.wfile.write(body)
        elif self.path.startswith('/stream/'):
            name = self.path[len('/stream/'):]
            self.send_response(200)
            self.send_header('Content-Type', 'multipart/x-mixed-replace; boundary=frame')
            self.end_headers()
            last = None
            try:
                while True:
                    with lock:
                        lock.wait_for(lambda: frames.get(name) is not last, timeout=5)
                        frame = frames.get(name)
                    if frame is None or frame is last:
                        continue
                    last = frame
                    self.wfile.write(b'--frame\r\nContent-Type: image/jpeg\r\nContent-Length: '
                                     + str(len(frame)).encode() + b'\r\n\r\n' + frame + b'\r\n')
            except (BrokenPipeError, ConnectionResetError, ConnectionAbortedError):
                pass
        else:
            self.send_error(404)


def main(argv):
    opts = dict(a[2:].split('=', 1) for a in argv if a.startswith('--') and '=' in a)
    port, fps = int(opts.get('port', 8770)), float(opts.get('fps', 4))
    started = set()

    def watch_for_vms():
        while True:
            for name in running_vms():
                if name not in started:
                    started.add(name)
                    threading.Thread(target=grab, args=(name, fps), daemon=True).start()
            time.sleep(10)

    threading.Thread(target=watch_for_vms, daemon=True).start()
    if opts.get('watchdog', '1') != '0':
        threading.Thread(target=watchdog, daemon=True).start()
    ThreadingHTTPServer(('127.0.0.1', port), Handler).serve_forever()


if __name__ == '__main__':
    main(sys.argv[1:])
