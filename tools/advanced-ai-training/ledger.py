#!/usr/bin/env python3
"""The benchmark ledger: a lasting record of what each AI change did to every benchmark.

One JSON object per line (LF endings) in a .jsonl file: <out>/ledger.jsonl
for a training run, doc/benchmark-ledger.jsonl for the kept history.

Schema of one entry (entry_from_trial):
    date        ISO date the entry was made
    commit      short git hash of the code the trials ran on
    ticket      ticket id the change belongs to ("" if none)
    change      {"label": "BuildScience+", "before": "<knobs>", "after": "<knobs>"}
    difficulty  the Standard AIs' difficulty index (from the trial file)
    seed        first game seed; games, turns: as in the trial file
    mode        "paired" or "all-civs" (see meta)
    kept        true/false when train.py decided; absent otherwise
    measures    {name: {adv, std, diff, se, delta, delta_se}} for every
                benchmarks.ROWS name the candidate trial has. adv, std, diff,
                se are the candidate's own row; delta = candidate diff - baseline
                diff (what the change did), delta_se = both SEs combined.
    verdict     {name: "helps" | "harms" | "none"}: delta beyond 2 delta_se,
                read with the row's lower-is-better flag
    basis       {name: baseline adv / std} (null if std is 0)

A line with "recheck_of" (an entry_id) and "reason" instead of measures marks
that entry stale; an entry with "recheck_of" next to its measures is the
re-check's own result (train.py).

meta keys (all optional): ticket, change, difficulty, seed, games, turns, mode,
kept, date, commit. Those given override what is read from the trial files.

Standard library only. Self-test: python tools/advanced-ai-training/ledger.py --self-test
"""
import datetime
import json
import math
import os
import re
import subprocess
import sys
import tempfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import benchmarks  # noqa: E402

LINE = re.compile(r"^(\w+)\s+([-\d.]+) vs\s+([-\d.]+)\s+([-+\d.]+) \+- ([\d.]+)")
HEAD = re.compile(r"(\w+)=(\S+)")
SEED = re.compile(r"^game \d+ seed (\d+)")
SIGMAS = 2.0


def read_trial(text):
    """-> (rows {name: {adv, std, diff, se}}, header {games, turns, difficulty, seed})."""
    rows, head = {}, {}
    for line in text.splitlines():
        m = LINE.match(line)
        if m:
            rows[m[1]] = {"adv": float(m[2]), "std": float(m[3]),
                          "diff": float(m[4].replace("-+", "-")), "se": float(m[5])}
        elif "seed" not in head and SEED.match(line):
            head["seed"] = int(SEED.match(line)[1])
        elif line.startswith("scenario="):
            for k, v in HEAD.findall(line):
                if k in ("games", "turns", "difficulty") and v.isdigit():
                    head[k] = int(v)
    return rows, head


def git_commit():
    try:
        out = subprocess.run(["git", "rev-parse", "--short", "HEAD"], capture_output=True, text=True,
                             cwd=os.path.dirname(os.path.abspath(__file__)), check=False)
        return out.stdout.strip()
    except OSError:
        return ""


def verdict_for(delta, delta_se, lower_better):
    if delta_se <= 0 or abs(delta) < SIGMAS * delta_se:
        return "none"
    return "harms" if (delta > 0) == lower_better else "helps"


def entry_from_trial(base_txt, cand_txt, meta=None):
    """Two trial outputs (the text train.parse reads) -> one ledger entry."""
    meta = dict(meta or {})
    base, _ = read_trial(base_txt)
    cand, head = read_trial(cand_txt)
    measures, verdict, basis = {}, {}, {}
    for name, _label, lower in benchmarks.ROWS:
        c = cand.get(name)
        if c is None:
            continue
        b = base.get(name)
        m = dict(c)
        if b is not None:
            m["delta"] = round(c["diff"] - b["diff"], 4)
            m["delta_se"] = round(math.hypot(c["se"], b["se"]), 4)
            verdict[name] = verdict_for(m["delta"], m["delta_se"], lower)
            basis[name] = round(b["adv"] / b["std"], 4) if b["std"] else None
        else:
            verdict[name], basis[name] = "none", None
        measures[name] = m
    entry = {"date": meta.pop("date", datetime.date.today().isoformat()),
             "commit": meta.pop("commit", None) or git_commit(),
             "ticket": meta.pop("ticket", ""),
             "change": meta.pop("change", {"label": "", "before": "", "after": ""}),
             "difficulty": meta.pop("difficulty", head.get("difficulty")),
             "seed": meta.pop("seed", head.get("seed")),
             "games": meta.pop("games", head.get("games")),
             "turns": meta.pop("turns", head.get("turns")),
             "mode": meta.pop("mode", "paired")}
    if "kept" in meta:
        entry["kept"] = meta.pop("kept")
    entry.update(meta)
    entry.update(measures=measures, verdict=verdict, basis=basis)
    return entry


def append(path, entry):
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    with open(path, "a", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(entry) + "\n")


def load(path):
    """-> entries in file order; a missing file or a bad line is skipped."""
    out = []
    if not path or not os.path.exists(path):
        return out
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line:
                try:
                    out.append(json.loads(line))
                except ValueError:
                    pass
    return out


def entry_id(e):
    """A stable name for an entry: date, commit and change label (what recheck_of points at)."""
    return f"{e.get('date', '')}-{e.get('commit') or ''}-{e.get('change', {}).get('label', '')}"


def movers(entries, measure):
    """Entries that moved `measure` at 2 SE, newest first (date, then file order)."""
    hit = [(e.get("date", ""), i, e) for i, e in enumerate(entries)
           if e.get("verdict", {}).get(measure) in ("helps", "harms")]
    return [e for _, _, e in sorted(hit, key=lambda t: (t[0], t[1]), reverse=True)]


def self_test():
    here = os.path.join(os.path.dirname(os.path.abspath(__file__)), "testdata")
    base = open(os.path.join(here, "trial-base.txt"), encoding="utf-8").read()
    cand = open(os.path.join(here, "trial-cand.txt"), encoding="utf-8").read()
    meta = {"ticket": "ZUR-0", "date": "2026-10-30", "commit": "abc1234",
            "change": {"label": "BuildScience+", "before": "", "after": "BuildScience=75"}}
    e = entry_from_trial(base, cand, meta)
    assert e["games"] == 16 and e["turns"] == 200 and e["difficulty"] == 2 and e["seed"] == 1100, e
    assert e["mode"] == "paired" and e["commit"] == "abc1234"
    assert "rank" in e["measures"] and "bombarders" in e["measures"] and "midtechs" not in e["measures"]
    assert e["measures"]["score"]["adv"] == 1146.1
    # Every verdict must follow from its delta, whatever the fixtures hold.
    lower = {n: lo for n, _, lo in benchmarks.ROWS}
    for n, m in e["measures"].items():
        if "delta" in m:
            assert e["verdict"][n] == verdict_for(m["delta"], m["delta_se"], lower[n]), n
    assert e["basis"]["score"] > 0 and e["basis"]["warships"] is None
    assert verdict_for(5, 1, True) == "harms" and verdict_for(-5, 1, True) == "helps"
    assert verdict_for(5, 1, False) == "helps" and verdict_for(1.9, 1, False) == "none"
    assert verdict_for(-5, 1, False) == "harms" and verdict_for(5, 0, False) == "none"
    e2 = dict(e, date="2026-10-31", verdict=dict(e["verdict"], techs="helps"))
    e3 = dict(e, date="2026-10-29", verdict=dict(e["verdict"], techs="harms"))
    e4 = dict(e, date="2026-11-01", verdict=dict(e["verdict"], techs="none"))
    with tempfile.TemporaryDirectory() as d:
        p = os.path.join(d, "sub", "ledger.jsonl")
        for x in (e3, e2, e4):
            append(p, x)
        raw = open(p, "rb").read()
        assert b"\r" not in raw and raw.endswith(b"\n") and raw.count(b"\n") == 3
        got = load(p)
        assert got == [e3, e2, e4]
        assert [m["date"] for m in movers(got, "techs")] == ["2026-10-31", "2026-10-29"]
        assert movers(got, "gold") == [] and load(os.path.join(d, "none.jsonl")) == []
    print("ledger self-test passed")


if __name__ == "__main__":
    if "--self-test" in sys.argv:
        self_test()
    else:
        print(__doc__)
