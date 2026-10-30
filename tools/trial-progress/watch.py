#!/usr/bin/env python3
"""Trial progress viewer: one line per game being played, bar scaled to turn 450.

    python tools\\trial-progress\\watch.py [--root C:\\Dev\\ladder-runs] [--once]

Reads <C7_TRIAL_OUT>.progress.json files (written by AiTrialTest) updated in the
last 15 minutes; falls back to the tail of trial state logs for older runs.
Also writes <root>/progress.html (auto-refresh 5 s). Stdlib only, low priority.
"""
import argparse, html, json, os, re, sys, time

BAR_TURNS = 450
BAR_WIDTH = 40
RECENT_SECONDS = 15 * 60
TAIL_BYTES = 64 * 1024
MAX_DEPTH = 4
FINISHED_SHOWN = 3  # finished games listed per trial in the console


def lower_priority():
    try:
        if os.name == "nt":
            import ctypes
            BELOW_NORMAL = 0x00004000
            ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), BELOW_NORMAL)
        else:
            os.nice(10)
    except Exception:
        pass


def find_files(root, now):
    """Return (progress_files, state_logs) updated recently, depth-limited."""
    prog, logs = [], []
    root = os.path.abspath(root)
    base_depth = root.rstrip(os.sep).count(os.sep)
    for dirpath, dirnames, filenames in os.walk(root):
        if dirpath.count(os.sep) - base_depth >= MAX_DEPTH:
            dirnames[:] = []
        for name in filenames:
            low = name.lower()
            is_prog = low.endswith(".progress.json")
            is_log = (("state" in low and low.endswith(".log")) or low.endswith("-state.jsonl"))
            if not (is_prog or is_log):
                continue
            path = os.path.join(dirpath, name)
            try:
                if now - os.stat(path).st_mtime > RECENT_SECONDS:
                    continue
            except OSError:
                continue
            (prog if is_prog else logs).append(path)
    return prog, logs


def trial_name(path, root):
    rel = os.path.relpath(os.path.dirname(path), root)
    base = os.path.basename(path)
    for suffix in (".progress.json", ".log", ".jsonl"):
        if base.endswith(suffix):
            base = base[: -len(suffix)]
    return base if rel in (".", "") else rel.replace(os.sep, "/") + "/" + base


def read_progress(path, name):
    try:
        with open(path, "r", encoding="utf-8-sig") as f:
            d = json.load(f)
    except Exception:
        return None  # being replaced or not valid yet; try next refresh
    mult = 2 if d.get("paired") else 1
    games = int(d.get("games") or 0)
    rows = [dict(game=g.get("game", 0), run=g.get("run", "main"), turn=g.get("endTurn", 0),
                 result=g.get("result", "done")) for g in d.get("finished") or []]
    p = d.get("playing")
    if p:
        rows.append(dict(game=p.get("game", 0), run=p.get("run", "main"), turn=p.get("turn", 0), result=None))
    return dict(name=name, total=games * mult, games=games, turn_limit=d.get("turnLimit"),
                rows=rows, complete=bool(d.get("complete")), source="progress")


def read_tail(path):
    with open(path, "rb") as f:
        f.seek(0, os.SEEK_END)
        size = f.tell()
        f.seek(max(0, size - TAIL_BYTES))
        data = f.read()
    return data.decode("utf-8", "replace").splitlines()


def games_from_sibling(path):
    """Total games from a results.json next to the state log (first 4 KB only)."""
    d = os.path.dirname(path)
    for n in os.listdir(d):
        if n.lower().endswith(".json") and not n.lower().endswith(".progress.json"):
            try:
                with open(os.path.join(d, n), "r", encoding="utf-8-sig") as f:
                    head = f.read(4096)
                m = re.search(r'"games"\s*:\s*(\d+)', head)
                t = re.search(r'"turns"\s*:\s*(\d+)', head)
                if m:
                    return int(m.group(1)), (int(t.group(1)) if t else None)
            except OSError:
                pass
    return 0, None


def read_state_log(path, name):
    try:
        lines = read_tail(path)
    except OSError:
        return None
    last = None
    for line in reversed(lines):
        line = line.strip()
        if not line.startswith("{"):
            continue
        try:
            d = json.loads(line)
        except ValueError:
            continue  # first line of the tail may be cut
        if "game" in d and "turn" in d:
            last = d
            break
    if last is None:
        return None
    games, turn_limit = games_from_sibling(path)
    row = dict(game=last["game"], run=last.get("run", "main"), turn=last["turn"], result=None)
    return dict(name=name, total=games, games=games, turn_limit=turn_limit,
                rows=[row], complete=False, source="state log")


def collect(root):
    now = time.time()
    prog, logs = find_files(root, now)
    trials, covered = [], set()
    for p in sorted(prog):
        t = read_progress(p, trial_name(p, root))
        if t:
            trials.append(t)
            covered.add(os.path.dirname(p))
    for p in sorted(logs):
        if os.path.dirname(p) in covered:
            continue
        t = read_state_log(p, trial_name(p, root))
        if t:
            trials.append(t)
    return trials


def bar(turn, width=BAR_WIDTH):
    frac = max(0.0, min(1.0, turn / BAR_TURNS))
    n = int(round(frac * width))
    return "#" * n + "-" * (width - n)


def label(t, r):
    n = r["game"] + 1  # progress/state files count games from 0
    tot = t["games"]
    s = "game %d/%d" % (n, tot) if tot else "game %d" % n
    if r["run"] != "main":
        s += " (" + r["run"] + ")"
    return s


def trial_summary(t):
    done = sum(1 for r in t["rows"] if r["result"])
    if t["source"] == "state log":
        done = max(0, t["rows"][0]["game"])  # games before the current one are finished
        if t["games"] and t["rows"][0]["game"] >= t["games"]:
            done = t["games"]
    s = "%d/%d runs done" % (done, t["total"]) if t["total"] else "%d runs done" % done
    won = sum(1 for r in t["rows"] if r["result"] == "won")
    lost = sum(1 for r in t["rows"] if r["result"] == "lost")
    if won or lost:
        s += ", %d won %d lost" % (won, lost)
    return done, s


def visible_rows(t, all_rows):
    playing = [r for r in t["rows"] if not r["result"]]
    fin = [r for r in t["rows"] if r["result"]]
    if not all_rows:
        fin = fin[-FINISHED_SHOWN:]
    return fin + playing


def render_text(trials):
    out = []
    grand_done = grand_total = 0
    for t in trials:
        done, s = trial_summary(t)
        grand_done += done
        grand_total += t["total"]
        out.append("%s  [%s]" % (t["name"], t["source"]))
        hidden = len([r for r in t["rows"] if r["result"]]) - len([r for r in visible_rows(t, False) if r["result"]])
        if hidden > 0:
            out.append("  ... %d earlier finished games not shown" % hidden)
        for r in visible_rows(t, False):
            out.append("  %-26s turn %3d  [%s] %s" % (label(t, r), r["turn"], bar(r["turn"]), r["result"] or ""))
        out.append("  total: " + s)
    if not trials:
        out.append("No trial updated in the last 15 minutes.")
    else:
        out.append("")
        out.append("ALL TRIALS: %d/%d runs done" % (grand_done, grand_total))
    return "\n".join(out)


def render_html(trials, root):
    h = ['<!doctype html><html><head><meta charset="utf-8"><meta http-equiv="refresh" content="5">',
         "<title>Trial progress</title><style>",
         "body{font-family:Segoe UI,Arial,sans-serif;margin:20px;background:#fafafa;color:#222}",
         "h2{margin:18px 0 4px}table{border-collapse:collapse}td{padding:2px 10px}",
         ".b{width:300px;height:14px;background:#ddd;border-radius:3px}",
         ".b div{height:14px;background:#3a7bd5;border-radius:3px}",
         ".b div.d{background:#2e9e5b}.sum{color:#555}.won{color:#2e9e5b}.lost{color:#c0392b}",
         "</style></head><body><h1>Trial progress</h1>",
         "<p class='sum'>Bar full = turn %d. Updated %s.</p>" % (BAR_TURNS, time.strftime("%H:%M:%S"))]
    gd = gt = 0
    for t in trials:
        done, s = trial_summary(t)
        gd += done
        gt += t["total"]
        h.append("<h2>%s <small class='sum'>(%s)</small></h2><p class='sum'>%s</p><table>"
                 % (html.escape(t["name"]), t["source"], html.escape(s)))
        for r in visible_rows(t, True):
            pct = max(0, min(100, 100.0 * r["turn"] / BAR_TURNS))
            res = r["result"] or ""
            h.append("<tr><td>%s</td><td>turn %d</td><td><div class='b'><div class='%s' style='width:%.1f%%'></div></div></td>"
                     "<td class='%s'>%s</td></tr>" % (html.escape(label(t, r)), r["turn"], "d" if res else "", pct,
                                                      html.escape(res), html.escape(res)))
        h.append("</table>")
    if not trials:
        h.append("<p>No trial updated in the last 15 minutes.</p>")
    else:
        h.append("<h2>All trials: %d/%d runs done</h2>" % (gd, gt))
    h.append("</body></html>")
    return "\n".join(h)


def write_html(trials, root):
    path = os.path.join(root, "progress.html")
    tmp = path + ".tmp"
    try:
        with open(tmp, "w", encoding="utf-8", newline="\n") as f:
            f.write(render_html(trials, root))
        os.replace(tmp, path)
    except OSError as e:
        print("could not write %s: %s" % (path, e), file=sys.stderr)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--root", default=r"C:\Dev\ladder-runs")
    ap.add_argument("--once", action="store_true", help="print one snapshot and exit")
    ap.add_argument("--interval", type=float, default=5.0)
    a = ap.parse_args()
    lower_priority()
    os.system("") if os.name == "nt" else None  # enables ANSI escapes in the Windows console
    while True:
        trials = collect(a.root)
        text = render_text(trials)
        write_html(trials, a.root)
        if a.once:
            print(text)
            return
        sys.stdout.write("\x1b[2J\x1b[H" + "Trial progress (Ctrl+C to stop) " + time.strftime("%H:%M:%S") + "\n\n" + text + "\n")
        sys.stdout.flush()
        try:
            time.sleep(a.interval)
        except KeyboardInterrupt:
            return


if __name__ == "__main__":
    main()
