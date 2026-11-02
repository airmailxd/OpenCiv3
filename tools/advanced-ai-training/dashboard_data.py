"""Data files for the Advanced AI benchmark dashboard.

Writes three JSON files (arrays of row objects) into --out:
  batches.json  every trial batch found: ladder exams, ladder candidate runs
                and hand-run trials, with its win rate
  weights.json  every Advanced tuning knob and behaviour: the running ladder's
                current value next to the Development default
  ladder.json   the running ladder's status line

Win rate is the share of games an Advanced (trial) civ finished first. A
results file gives the mean "first" per trial civ; it is multiplied by the
number of trial civs per game, which the file doesn't record but follows from
exactly one civ finishing first per game: n*trial + (civs-n)*others = 1.

Usage: python dashboard_data.py --runs C:/Dev/ladder-runs --ladder C:/Dev/ladder-runs/ladder-2026-10-30 --out <dir>
"""
import argparse
import ast
import datetime
import glob
import json
import os
import re

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))


def iso(path):
    return datetime.datetime.fromtimestamp(os.path.getmtime(path)).strftime("%Y-%m-%dT%H:%M")


def trial_civs(first, civs):
    t, o = first["trialMean"], first["othersMean"]
    if abs(t - o) < 1e-9:
        return civs // 2
    n = (1 - civs * o) / (t - o)
    return max(1, min(civs, round(n)))


def summary_measure(summary, name):
    return next((m for m in summary if m.get("measure") == name), None)


def json_batches(runs_dir, ladder_dir):
    rows = []
    for path in glob.glob(os.path.join(runs_dir, "**", "*.json"), recursive=True):
        in_ladder = os.path.commonpath([os.path.abspath(path), os.path.abspath(ladder_dir)]) == os.path.abspath(ladder_dir)
        try:
            with open(path, encoding="utf-8") as f:
                d = json.load(f)
        except (OSError, ValueError, UnicodeDecodeError):
            continue
        if not isinstance(d, dict) or "settings" not in d or not isinstance(d.get("summary"), list):
            continue
        s = d["settings"]
        # Skip test-sized runs (e.g. exam files a unit test once overwrote
        # with a 2-game, 5-turn run).
        if (s.get("turns") or 0) < 50 or (s.get("games") or 0) < 4:
            continue
        first = summary_measure(d["summary"], "first")
        if not first:
            continue
        civs = s.get("civs", 8)
        # A paired run's other arm is all Standard, so the one-first-per-game
        # identity doesn't hold there; paired runs put half the civs on trial.
        n = civs // 2 if s.get("paired") else trial_civs(first, civs)
        games = s.get("games") or d.get("gamesPlayed") or 0
        score = summary_measure(d["summary"], "score") or {}
        rank = summary_measure(d["summary"], "rank") or {}
        exps = ",".join(s.get("experiments") or []) or "defaults"
        rel = os.path.relpath(path, runs_dir).replace("\\", "/")
        mode = "one Advanced civ" if n == 1 else ("all civs" if s.get("all") else f"{n} Advanced civs")
        if s.get("paired"):
            mode += ", paired"
        rows.append({
            "id": rel,
            "kind": "Ladder exam" if in_ladder else "Trial",
            "date": iso(path),
            "name": (os.path.basename(path)[:-5] if in_ladder else f"{os.path.dirname(rel) or '.'}: {exps}"),
            "folder": os.path.dirname(rel),
            "experiments": exps,
            "games": games,
            "difficulty": s.get("difficulty"),
            "firstSeed": s.get("firstSeed"),
            "mode": mode,
            "advancedPerGame": n,
            # With every civ on trial a win rate says nothing.
            "winRate": None if s.get("all") else round(first["trialMean"] * n, 4),
            "chance": round(n / civs, 4),
            "firstPerCiv": round(first["trialMean"], 4),
            "scoreDiff": round(score.get("difference", 0), 1),
            "scoreSe": round(score.get("standardError", 0), 1),
            "rankDiff": round(rank.get("difference", 0), 2),
            "advanced": bool(s.get("advanced")),
            "paired": bool(s.get("paired")),
            "source": "C:/Dev/ladder-runs/" + rel,
        })
    return rows


TXT_FIRST = re.compile(r"^first\s+([\d.]+)\s+vs\s+([\d.]+)\s+([+-][\d.]+)\s+\+-\s+([\d.]+)", re.M)
TXT_SCORE = re.compile(r"^score\s+([\d.]+)\s+vs\s+([\d.]+)\s+([+-][\d.]+)\s+\+-\s+([\d.]+)", re.M)
TXT_RANK = re.compile(r"^rank\s+([\d.]+)\s+vs\s+([\d.]+)\s+([+-][\d.]+)\s+\+-\s+([\d.]+)", re.M)
TXT_HEAD = re.compile(r"games=(\d+).*?civs=(\d+).*?difficulty=(\d+)")


def ladder_runs(ladder_dir):
    rows = []
    for path in glob.glob(os.path.join(ladder_dir, "runs", "*.txt")):
        try:
            with open(path, encoding="utf-8", errors="replace") as f:
                text = f.read()
        except OSError:
            continue
        m = TXT_FIRST.search(text)
        head = TXT_HEAD.search(text)
        if not m or not head:
            continue
        games, civs, diff = int(head.group(1)), int(head.group(2)), int(head.group(3))
        n = civs // 2  # ladder runs are paired, half the civs Advanced
        sc, rk = TXT_SCORE.search(text), TXT_RANK.search(text)
        tag = os.path.basename(path)[:-4]
        rows.append({
            "id": "ladder/" + tag,
            "kind": "Ladder candidate",
            "date": iso(path),
            "name": tag,
            "folder": "ladder runs",
            "experiments": tag,
            "games": games,
            "difficulty": diff,
            "firstSeed": None,
            "mode": f"{n} Advanced civs, paired",
            "advancedPerGame": n,
            # The text output rounds "first" to 0.1 per civ (steps of 40%
            # with 4 Advanced civs), too coarse to show as a win rate.
            "winRate": None,
            "chance": round(n / civs, 4),
            "firstPerCiv": float(m.group(1)),
            "scoreDiff": float(sc.group(3)) if sc else None,
            "scoreSe": float(sc.group(4)) if sc else None,
            "rankDiff": float(rk.group(3)) if rk else None,
            "advanced": True,
            "paired": True,
            "source": os.path.abspath(path).replace("\\", "/"),
        })
    return rows


def ladder_exams(state, ladder_dir):
    rows = []
    for e in state.get("exams", []):
        r = e.get("result", {})
        first = r.get("first") or {}
        n = 4
        tag = f"{e.get('rung')} exam, cycle {e.get('cycle')} round {e.get('round')}"
        rows.append({
            "id": f"exam/{e.get('rung')}-c{e.get('cycle')}-r{e.get('round')}",
            "kind": "Ladder exam",
            "date": None,
            "name": tag,
            "folder": "ladder exams",
            "experiments": "ladder knobs at the time",
            "games": 16,
            "difficulty": 2,
            "firstSeed": None,
            "mode": "4 Advanced civs, paired",
            "advancedPerGame": n,
            "winRate": round(first.get("adv", 0) * n, 4),
            "chance": 0.5,
            "firstPerCiv": first.get("adv"),
            "scoreDiff": (r.get("score") or {}).get("diff"),
            "scoreSe": (r.get("score") or {}).get("se"),
            "rankDiff": (r.get("rank") or {}).get("diff"),
            "advanced": True,
            "paired": True,
            "passed": e.get("passed"),
            "source": os.path.abspath(os.path.join(ladder_dir, "state.json")).replace("\\", "/"),
        })
    return rows


def train_constants():
    tree = ast.parse(open(os.path.join(REPO, "tools", "advanced-ai-training", "train.py"), encoding="utf-8").read())
    # Only these constant assignments, in order: DEFAULTS is built from the
    # lists before it, so they are evaluated rather than read as literals.
    wanted = ("STEPS", "LIMITS", "BEHAVIOURS", "ON_BY_DEFAULT", "DEFAULTS")
    body = [n for n in tree.body if isinstance(n, ast.Assign) and len(n.targets) == 1
            and isinstance(n.targets[0], ast.Name) and n.targets[0].id in wanted]
    scope = {"__builtins__": {"int": int}}
    exec(compile(ast.Module(body=body, type_ignores=[]), "train.py", "exec"), scope)
    return {k: scope[k] for k in wanted if k in scope}


def knob_descriptions():
    consts = {}
    for path in glob.glob(os.path.join(REPO, "C7Engine", "AI", "**", "*.cs"), recursive=True):
        cls = os.path.basename(path)[:-3]
        for m in re.finditer(r'const string (\w+) = "([\w-]+)"', open(path, encoding="utf-8", errors="replace").read()):
            consts[f"{cls}.{m.group(1)}"] = m.group(2)
    text = open(os.path.join(REPO, "C7Engine", "AI", "Advanced", "AdvancedTuning.cs"), encoding="utf-8").read()
    desc = {}
    for m in re.finditer(r'new\(\s*("([\w-]+)"|([\w.]+)),[^"]*?((?:"(?:[^"\\]|\\.)*"\s*\+?\s*)+)\)', text):
        name = m.group(2) or consts.get(m.group(3), m.group(3))
        parts = re.findall(r'"((?:[^"\\]|\\.)*)"', m.group(4))
        desc[name] = "".join(parts)
    return desc


def weights(state):
    c = train_constants()
    desc = knob_descriptions()
    defaults = dict(c.get("DEFAULTS", {}))
    for b in c.get("BEHAVIOURS", []):
        defaults.setdefault(b, 1 if b in c.get("ON_BY_DEFAULT", set()) else 0)
    current = state.get("knobs", {})
    rows = []
    for name in sorted(set(defaults) | set(current)):
        if name.startswith("Weight"):
            kind = "Category weight"
        elif name in c.get("BEHAVIOURS", []):
            kind = "Behaviour"
        else:
            kind = "Number"
        lo, hi = c.get("LIMITS", {}).get(name, (0, 1) if kind == "Behaviour" else (None, None))
        cur = current.get(name)
        rows.append({
            "name": name,
            "kind": kind,
            "ladder": cur,
            "default": defaults.get(name),
            "changed": cur is not None and defaults.get(name) is not None and cur != defaults.get(name),
            "searched": cur is not None,
            "min": lo,
            "max": hi,
            "description": desc.get(name, ""),
        })
    return rows


def ladder_status(ladder_dir, state):
    line = ""
    prog = os.path.join(ladder_dir, "progress.txt")
    if os.path.exists(prog):
        lines = [l.strip() for l in open(prog, encoding="utf-8", errors="replace") if l.strip()]
        line = lines[-1] if lines else ""
    beat = {}
    hb = os.path.join(ladder_dir, "heartbeat.json")
    if os.path.exists(hb):
        try:
            beat = json.load(open(hb, encoding="utf-8"))
        except ValueError:
            pass
    return [{
        "rung": ["Regent", "Monarch", "Emperor", "Demigod", "Deity"][state.get("rung", 0)] if isinstance(state.get("rung"), int) else state.get("rung"),
        "cycle": state.get("cycle"),
        "round": state.get("round"),
        "exams": len(state.get("exams", [])),
        "status": line,
        "heartbeat": beat.get("time") or beat.get("at") or iso(hb) if os.path.exists(hb) else None,
    }]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--runs", required=True)
    ap.add_argument("--ladder", required=True)
    ap.add_argument("--out", required=True)
    a = ap.parse_args()
    state = json.load(open(os.path.join(a.ladder, "state.json"), encoding="utf-8"))
    batches = json_batches(a.runs, a.ladder) + ladder_runs(a.ladder)
    os.makedirs(a.out, exist_ok=True)
    for name, rows in (("batches", batches), ("weights", weights(state)), ("ladder", ladder_status(a.ladder, state))):
        with open(os.path.join(a.out, name + ".json"), "w", encoding="utf-8", newline="\n") as f:
            json.dump(rows, f, indent=0)
        print(name, len(rows))


if __name__ == "__main__":
    main()
