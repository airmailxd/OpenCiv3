#!/usr/bin/env python3
"""Turn the training ladder's output (state.json + log.md) into one static HTML page.

    python tools/advanced-ai-training/dashboard.py --out C:\\Dev\\ladder-runs\\ladder

Reads state.json, log.md and the curated findings.md in --out, writes dashboard.html next to them
(--html to override). Read-only on the ladder files. Standard library only.
"""
import argparse
import html
import json
import os
import re
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from train import CIVS, DEFAULTS, RUNGS, spec, won  # noqa: E402

HEADING = re.compile(r"^## (\w+), round (\d+) — knobs: (.*)$")
HELD = re.compile(r"^- held-out (.+?): gain ([-+\d.]+)")
E = html.escape


def parse_log(text):
    """-> (knobs text per (rung, round), held-out gain per (rung, round, label))."""
    knobs, held, cur = {}, {}, None
    for line in text.splitlines():
        m = HEADING.match(line)
        if m:
            cur = (m.group(1), int(m.group(2)))
            knobs[cur] = m.group(3)
            continue
        m = HELD.match(line)
        if m and cur:
            held[cur + (m.group(1),)] = float(m.group(2))
    return knobs, held


def pct(x):
    return "—" if x is None else f"{x:.0%}"


def line_chart(points, target, rounds):
    """points: [(x, y, kind)] with kind 'round' or 'exam'. Inline SVG, y = win rate 0..100%."""
    w, h, l, r, t, b = 640, 240, 44, 16, 14, 30
    n = max(rounds, 1)
    X = lambda x: l + (w - l - r) * (x / n if n else 0)
    Y = lambda y: t + (h - t - b) * (1 - y)
    s = [f'<svg viewBox="0 0 {w} {h}" role="img" aria-label="Win rate by round">']
    for g in (0, .25, .5, .75, 1):
        s.append(f'<line class="grid" x1="{l}" x2="{w-r}" y1="{Y(g):.1f}" y2="{Y(g):.1f}"/>'
                 f'<text class="ax" x="{l-6}" y="{Y(g)+4:.1f}" text-anchor="end">{g:.0%}</text>')
    if target is not None:
        s.append(f'<line class="target" x1="{l}" x2="{w-r}" y1="{Y(target):.1f}" y2="{Y(target):.1f}"/>'
                 f'<text class="ax" x="{w-r}" y="{Y(target)-4:.1f}" text-anchor="end">target {target:.0%}</text>')
    rp = [p for p in points if p[2] == "round"]
    if len(rp) > 1:
        s.append('<polyline class="ln" points="' + " ".join(f"{X(x):.1f},{Y(y):.1f}" for x, y, _ in rp) + '"/>')
    for x, y, k in points:
        tip = f'{"Exam" if k == "exam" else "Round " + str(x)}: {y:.0%}'
        cls = "exam" if k == "exam" else "pt"
        s.append(f'<circle class="{cls}" cx="{X(x):.1f}" cy="{Y(y):.1f}" r="5"><title>{tip}</title></circle>')
    for x in range(n + 1):
        lab = "exam" if x == n and any(p[2] == "exam" for p in points) else str(x)
        s.append(f'<text class="ax" x="{X(x):.1f}" y="{h-10}" text-anchor="middle">{lab}</text>')
    s.append('</svg>')
    return "".join(s)


def latest_report(out):
    """-> (file name, text) of the newest benchmarks report under out/runs, or None."""
    runs = os.path.join(out, "runs")
    found = [os.path.join(runs, f) for f in os.listdir(runs) if f.endswith("-report.md")] if os.path.isdir(runs) else []
    if not found:
        return None
    path = max(found, key=os.path.getmtime)
    with open(path, encoding="utf-8") as f:
        return os.path.basename(path), f.read()


def benchmarks_html(report):
    """The newest benchmarks report's scorecard (and its loss autopsy) as HTML."""
    if not report:
        return '<p class="empty">No benchmarks report yet: one is written after each exam.</p>'
    name, text = report
    out = [f'<p class="note">From <code>runs/{E(name)}</code>.</p>']
    table, in_autopsy = [], False
    for line in text.splitlines():
        if line.startswith("|"):
            table.append([c.strip() for c in line.strip("|").split("|")])
        elif line.startswith("**"):
            out.append(f"<p><b>{E(line.strip('*'))}</b></p>")
        elif line.startswith("## Loss autopsy"):
            in_autopsy = True
            out.append("<h3>Loss autopsy</h3>")
        elif in_autopsy and line.startswith("### "):
            out.append(f"<h4>{E(line[4:])}</h4>")
        elif in_autopsy and line.startswith("- "):
            out.append(f"<div>{E(line[2:])}</div>")
        elif in_autopsy and line.strip():
            out.append(f"<p>{E(line)}</p>")
    if table:
        head, rows = table[0], [r for r in table[2:]]
        out.insert(2, "<table><tr>" + "".join(f"<th>{E(h)}</th>" for h in head) + "</tr>"
                   + "".join("<tr>" + "".join(f"<td>{E(c)}</td>" for c in r) + "</tr>" for r in rows) + "</table>")
    return "".join(out)


def findings_html(text):
    """The curated findings.md (written by hand as runs go) as HTML: ## headings, - bullets, paragraphs."""
    if not text:
        return '<p class="empty">No findings written yet: the CTO adds them to findings.md as runs finish.</p>'
    out = []
    for line in text.splitlines():
        if line.startswith("# "):
            continue
        if line.startswith("## "):
            out.append(f"<h3>{E(line[3:])}</h3>")
        elif line.startswith("- "):
            out.append(f"<div>&bull; {E(line[2:])}</div>")
        elif line.strip():
            out.append(f"<p>{E(line)}</p>")
    return "".join(out)


def build(state, log_text, mtime, report=None, findings=""):
    kn_by_round, held = parse_log(log_text)
    hist, exams = state.get("history", []), state.get("exams", [])
    cur_idx = state.get("rung", 0)
    cur_name = RUNGS[cur_idx][0] if cur_idx < len(RUNGS) else "finished"
    knobs = state.get("knobs", {})
    tabs = [("Overview", "overview"), ("Findings", "findings")] + [(n, n.lower()) for n, _, _ in RUNGS] + [("Benchmarks", "benchmarks")]

    per = {}
    for name, _, target in RUNGS:
        hs = [h for h in hist if h["rung"] == name]
        ex = next((e for e in reversed(exams) if e["rung"] == name), None)
        vals = [won(h["base"]) for h in hs] + [won(t[2]) for h in hs for t in h["tried"]]
        if ex:
            vals.append(won(ex["result"]))
        per[name] = dict(hs=hs, ex=ex, best=max(vals) if vals else None, target=target)

    def exam_text(p):
        if not p["ex"]:
            return "not reached" if not p["hs"] else "not taken yet"
        if p["ex"]["passed"] is None:
            return "reported only"
        return "passed" if p["ex"]["passed"] else "not passed"

    # Overview
    nondef = "".join(
        f'<span class="chip{" hot" if v != DEFAULTS.get(k) else ""}" title="default {DEFAULTS.get(k)}">{E(k)}={v:g}</span>'
        for k, v in knobs.items())
    rows = "".join(
        f'<tr><td>{n}</td><td>{pct(per[n]["best"])}</td><td>{pct(t)}</td>'
        f'<td class="{ {"passed": "ok", "not passed": "bad"}.get(exam_text(per[n]), "") }">{exam_text(per[n])}</td></tr>'
        for n, _, t in RUNGS)
    parts = {"overview": f"""
<div class="tiles"><div class="tile"><b>{E(cur_name)}</b><span>current difficulty</span></div>
<div class="tile"><b>{len(hist)}</b><span>rounds run in total</span></div>
<div class="tile"><b>{time.strftime('%Y-%m-%d %H:%M', time.localtime(mtime))}</b><span>last updated (state.json time)</span></div></div>
<h3>Current knobs <small>(highlighted = not the default)</small></h3><p>{nondef}</p>
<p class="note">Changed from defaults: {E(spec(knobs) or "nothing")}</p>
<h3>Best win rate per difficulty</h3>
<table><tr><th>Difficulty</th><th>Best win rate (any trial or exam)</th><th>Target</th><th>Exam</th></tr>{rows}</table>
<p class="note">Win rate = share of games an Advanced civ won (50% by chance, {CIVS} civs per game, half Advanced).</p>"""}

    for name, _, target in RUNGS:
        p = per[name]
        tgt = "no target (reported only)" if target is None else f"target {target:.0%}"
        if not p["hs"] and not p["ex"]:
            parts[name.lower()] = f'<p class="empty">Not reached yet. Exam {tgt}.</p>'
            continue
        hs, ex = p["hs"], p["ex"]
        train_best = max([won(h["base"]) for h in hs] + [won(t[2]) for h in hs for t in h["tried"]], default=None)
        exam_w = won(ex["result"]) if ex else None
        verdict = ""
        if ex:
            verdict = {"passed": '<b class="ok">PASSED</b>', "not passed": '<b class="bad">NOT PASSED</b>'}.get(exam_text(p), "reported only")
        tiles = f"""<div class="tiles"><div class="tile"><b>{pct(train_best)}</b><span>best on training seeds (round bases and tried changes)</span></div>
<div class="tile"><b>{pct(exam_w)}</b><span>exam (held-out seeds) vs {tgt} {verdict}</span></div>
<div class="tile"><b>{len(hs)}</b><span>rounds run</span></div></div>"""
        pts = [(h["round"], won(h["base"]), "round") for h in hs]
        n = (max(h["round"] for h in hs) + 1) if hs else 1
        if ex:
            pts.append((n, exam_w, "exam"))
        chart = line_chart(pts, target, n)
        trows = []
        for h in sorted(hs, key=lambda h: h["round"]):
            for gain, label, res in sorted(h["tried"], key=lambda t: -t[0]):
                hg = held.get((name, h["round"], label))
                kept = h["kept"] == label
                trows.append(
                    f'<tr><td>{h["round"]}</td><td>{E(label)}</td><td data-v="{gain}">{gain:+.2f}</td>'
                    f'<td data-v="{won(res)}">{pct(won(res))}</td>'
                    f'<td data-v="{"" if hg is None else hg}">{"—" if hg is None else f"{hg:+.2f}"}</td>'
                    f'<td class="{"ok" if kept else ""}">{"kept" if kept else "rejected"}</td></tr>')
        krows = "".join(
            f'<tr><td>{h["round"]}</td><td>{pct(won(h["base"]))}</td><td>{E(kn_by_round.get((name, h["round"]), "—"))}</td>'
            f'<td>{E(h["kept"] or "nothing kept")}</td></tr>' for h in sorted(hs, key=lambda h: h["round"]))
        parts[name.lower()] = f"""{tiles}
<h3>Win rate by round <small>(round base on training seeds; last point = exam on held-out seeds; dashed = target)</small></h3>{chart}
<h3>Ideas tested <small>({len(trows)}; click a header to sort; gain = rank gain on training seeds, win rate on training seeds, held-out gain from log.md)</small></h3>
<table class="sort"><tr><th>Round</th><th>Idea</th><th>Gain</th><th>Win rate (training)</th><th>Held-out gain</th><th>Outcome</th></tr>{"".join(trows)}</table>
<h3>Knobs in force at each round</h3>
<table><tr><th>Round</th><th>Base win rate</th><th>Knobs (non-default)</th><th>Kept after round</th></tr>{krows}</table>"""

    parts["benchmarks"] = benchmarks_html(report)
    parts["findings"] = findings_html(findings)
    nav = "".join(f'<button data-t="{i}"{" class=on" if k == 0 else ""}>{E(t)}</button>' for k, (t, i) in enumerate(tabs))
    body = "".join(f'<section id="{i}"{"" if k == 0 else " hidden"}><h2>{E(t)}</h2>{parts[i]}</section>' for k, (t, i) in enumerate(tabs))
    return PAGE.replace("@NAV@", nav).replace("@BODY@", body)


PAGE = """<!doctype html><html lang="en"><head><meta charset="utf-8"><title>Advanced AI training</title>
<meta name="viewport" content="width=device-width,initial-scale=1"><style>
:root{--bg:#fff;--fg:#1f2430;--mut:#667;--line:#dde1e8;--acc:#2b6cb0;--ok:#1a7f4b;--bad:#b42318;--hot:#fff4d6}
@media(prefers-color-scheme:dark){:root{--bg:#171a21;--fg:#e6e8ee;--mut:#9aa3b2;--line:#2c313c;--acc:#6aa6e8;--ok:#4cc38a;--bad:#f0786c;--hot:#4a3d10}}
body{font:14px system-ui,sans-serif;background:var(--bg);color:var(--fg);margin:0;padding:16px 24px;max-width:960px}
nav button{font:inherit;background:none;color:var(--fg);border:0;border-bottom:3px solid transparent;padding:8px 14px;cursor:pointer}
nav button.on{border-color:var(--acc);font-weight:600}nav{border-bottom:1px solid var(--line);margin-bottom:12px}
h2{margin:8px 0}h3{margin:20px 0 6px;font-size:15px}small{color:var(--mut);font-weight:400}
.tiles{display:flex;gap:12px;flex-wrap:wrap}.tile{border:1px solid var(--line);border-radius:6px;padding:10px 16px;min-width:170px}
.tile b{display:block;font-size:24px}.tile span{color:var(--mut);font-size:12px}
table{border-collapse:collapse;width:100%}th,td{text-align:left;padding:4px 8px;border-bottom:1px solid var(--line)}
th{color:var(--mut);font-weight:600}table.sort th{cursor:pointer}
.chip{display:inline-block;border:1px solid var(--line);border-radius:4px;padding:1px 6px;margin:2px}.chip.hot{background:var(--hot);font-weight:600}
.ok{color:var(--ok)}.bad{color:var(--bad)}.empty,.note{color:var(--mut)}
svg{width:100%;max-width:640px;height:auto}.grid{stroke:var(--line)}.ax{fill:var(--mut);font-size:11px}
.target{stroke:var(--bad);stroke-dasharray:5 4}.ln{fill:none;stroke:var(--acc);stroke-width:2}
.pt{fill:var(--acc);stroke:var(--bg);stroke-width:2}.exam{fill:var(--bg);stroke:var(--fg);stroke-width:2.5}
</style></head><body><h1>Advanced AI training</h1><nav>@NAV@</nav>@BODY@<script>
var B=document.querySelectorAll('nav button'),S=document.querySelectorAll('section');
B.forEach(function(b,i){b.onclick=function(){B.forEach(function(x,j){x.classList.toggle('on',i==j);S[j].hidden=i!=j})}});
document.querySelectorAll('table.sort').forEach(function(t){t.querySelectorAll('th').forEach(function(th,c){var d=1;th.onclick=function(){d=-d;
var rows=[].slice.call(t.rows,1);function v(r){var x=r.cells[c];var a=x.getAttribute('data-v');if(a===null)a=x.textContent;
if(a==='')return -1e9;var n=parseFloat(a);return isNaN(n)?a:n}
rows.sort(function(a,b){var p=v(a),q=v(b);return (p>q?1:p<q?-1:0)*d});rows.forEach(function(r){t.tBodies[0].appendChild(r)})}})});
</script></body></html>
"""


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True, help="ladder folder with state.json and log.md")
    ap.add_argument("--html", help="output file (default: dashboard.html in --out)")
    a = ap.parse_args()
    sp = os.path.join(a.out, "state.json")
    state = {}
    if os.path.exists(sp):  # a fresh ladder has no state.json until its first round ends
        with open(sp, encoding="utf-8") as f:
            state = json.load(f)
    lp = os.path.join(a.out, "log.md")
    log = open(lp, encoding="utf-8").read() if os.path.exists(lp) else ""
    fp = os.path.join(a.out, "findings.md")
    findings = open(fp, encoding="utf-8").read() if os.path.exists(fp) else ""
    out = a.html or os.path.join(a.out, "dashboard.html")
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        f.write(build(state, log, os.path.getmtime(sp if os.path.exists(sp) else lp), latest_report(a.out), findings))
    print(out)


if __name__ == "__main__":
    main()
