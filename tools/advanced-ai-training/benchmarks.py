#!/usr/bin/env python3
"""Say where the Advanced AI falls short, from one trial's output.

    python tools/advanced-ai-training/benchmarks.py --json exam.json --state exam-state.jsonl \\
        [--targets doc/suede-benchmarks.md] [--out <dir>] [--ledger <file>]

--json and --state are AiTrialTest's C7_TRIAL_JSON and C7_TRIAL_STATE files.
Writes <name>-report.md (name = the JSON file's name without .json; next to
it unless --out) with:

* a scorecard: each measure for Advanced and Standard, and Suede's figure
  where the targets file has one (blank otherwise);
* a loss autopsy: for each game no Advanced civ won, the winner and the
  three largest gaps between the best Advanced civ and the winner at turns
  50, 100 and 150;
* the biggest gaps: the six measures where Advanced is furthest behind
  Standard (in standard errors), each with up to three changes from the
  benchmark ledger (ledger.py) that moved it. The ledger is --ledger, else
  <out>/ledger.jsonl, else doc/benchmark-ledger.jsonl.

The targets file is read line by line: a line that starts (after any
"|", "-", "*" or backticks) with a measure's name and then a number gives
that measure's target, in the units of the scorecard ("workersPerCity100"
and "killRatio" are x10, as AiTrialTest counts them). Anything else is
ignored. Standard library only.
"""
import argparse
import json
import os
import re
import sys

# The scorecard's rows: AiTrialTest measure, what it is, and whether
# a lower figure is better.
ROWS = [
    ("rank", "Place by score (1 = first)", True),
    ("score", "Score at the end", False),
    ("govTurn", "Turn it left Despotism", True),
    ("despotism", "Turns under Despotism's tile penalty", True),
    ("anarchy", "Turns in anarchy", True),
    ("city5turn", "Turn it had 5 cities", True),
    ("city10turn", "Turn it had 10 cities", True),
    ("cities50", "Cities at turn 50", False),
    ("cities100", "Cities at turn 100", False),
    ("techs100", "Techs at turn 100", False),
    ("workersPerCity100", "Workers per city at turn 100 (x10)", False),
    ("techGold", "Gold received for techs sold", False),
    ("killRatio", "Kills per unit lost (x10)", False),
    ("cities", "Cities at the end", False),
    ("citizens", "Citizens at the end", False),
    ("techs", "Techs at the end", False),
    ("military", "Military strength at the end", False),
    ("captured", "Cities captured", False),
    ("lost", "Cities lost", True),
    ("gold", "Gold at the end", False),
    ("beakers", "Beakers per turn at the end", False),
    ("warships", "Warships at the end", False),
    ("bombarders", "Bombard units at the end", False),
]

# What the autopsy compares at each checkpoint: state-log field, label, and
# the least the larger side must be for a gap to count (a 1-against-0 gap in
# a count is noise).
CHECKPOINTS = (50, 100, 150)
COMPARED = [
    ("cities", "cities", 3),
    ("citizens", "citizens", 5),
    ("techs", "techs", 4),
    ("military", "military units", 4),
    ("workers", "workers", 3),
    ("ships", "ships", 3),
    ("gold", "gold", 50),
    ("culture", "culture", 20),
    ("score", "score", 50),
]


def load_targets(path):
    names = {r[0] for r in ROWS}
    targets = {}
    if not path:
        return targets
    if not os.path.exists(path):
        print(f"benchmarks: no targets file at {path}; the Suede column stays empty", file=sys.stderr)
        return targets
    with open(path, encoding="utf-8") as f:
        for line in f:
            m = re.match(r"^[\s|*\-`]*(\w+)[\s|`:=]+(-?\d+(?:\.\d+)?)", line)
            if m and m[1] in names:
                targets[m[1]] = float(m[2])
    return targets


def load_state(path):
    """-> {game: {turn: [row, ...]}} for the main run only."""
    games = {}
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            row = json.loads(line)
            if row.get("run", "main") != "main":
                continue
            games.setdefault(row["game"], {}).setdefault(row["turn"], []).append(row)
    return games


def num(x):
    if x is None:
        return ""
    return f"{x:.0f}" if abs(x) >= 100 or x == int(x) else f"{x:.1f}"


def scorecard(summary, targets):
    by_name = {s["measure"]: s for s in summary}
    lines = ["| Measure | Advanced | Standard | Suede |", "|---|---:|---:|---:|"]
    for name, label, _ in ROWS:
        s = by_name.get(name)
        if s is None:
            continue
        lines.append(f"| {label} (`{name}`) | {num(s['trialMean'])} | {num(s['othersMean'])} | {num(targets.get(name))} |")
    return lines


LEDGER_DEFAULT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "doc", "benchmark-ledger.jsonl")
GAPS = 6
MOVERS = 3


def gaps(summary, targets):
    """-> [(gap in SE, measure, label, advanced, standard, suede or None)], worst first, top GAPS.

    A gap is how far Advanced is on the wrong side of Standard, in standard errors.
    """
    by_name = {s["measure"]: s for s in summary}
    out = []
    for name, label, lower in ROWS:
        s = by_name.get(name)
        if s is None or not s.get("standardError"):
            continue
        behind = s["difference"] if lower else -s["difference"]
        if behind > 0:
            out.append((behind / s["standardError"], name, label, s["trialMean"], s["othersMean"], targets.get(name)))
    return sorted(out, key=lambda g: -g[0])[:GAPS]


def gaps_section(summary, targets, ledger_paths):
    import ledger
    entries = []
    for p in ledger_paths:
        entries = ledger.load(p)
        if entries:
            break
    top = gaps(summary, targets)
    if not top:
        return ["Advanced is level with or ahead of Standard on every measure."]
    lines = ["Measures where Advanced is furthest behind Standard, in standard errors; "
             "the changes listed are the latest the ledger says moved that measure at 2 SE.", ""]
    for gap, name, label, adv, std, suede in top:
        vs = f"; Suede {num(suede)}" if suede is not None else ""
        lines.append(f"### {label} (`{name}`): {gap:.1f} SE behind")
        lines.append(f"Advanced {num(adv)}, Standard {num(std)}{vs}.")
        found = ledger.movers(entries, name)[:MOVERS]
        if not found:
            lines.append("- No ledger entry moved this measure." if entries else "- The ledger has no entries yet.")
        for e in found:
            ch = e.get("change", {}).get("label") or "?"
            kept = {True: ", kept", False: ", not kept"}.get(e.get("kept"), "")
            lines.append(f"- {e.get('date', '?')} {e.get('ticket') or ''} {ch}: {e['verdict'][name]}{kept} (commit {e.get('commit') or '?'})"
                         .replace("  ", " "))
        lines.append("")
    return lines


def final_turn(turns):
    return max(turns)


def best(rows, key=lambda r: r["score"]):
    return max(rows, key=key)


def autopsy(games):
    """-> (games won by Advanced, games played, [lines for each lost game])."""
    won, lost_lines = 0, []
    for g in sorted(games):
        turns = games[g]
        last = final_turn(turns)
        final = turns[last]
        # The winner is the highest score at the end; a fallen civ scores 0.
        winner = best(final)
        advanced = [r for r in final if r["trial"]]
        if not advanced:
            continue
        top_adv = best(advanced)
        if winner["trial"]:
            won += 1
            continue
        seed = winner.get("seed")
        lost_lines.append(f"### Game {g} (seed {seed}): {winner['civ']} won"
                          f" with {winner['score']} points; best Advanced civ {top_adv['civ']} had {top_adv['score']}")
        gaps = []
        for t in CHECKPOINTS:
            if t > last:
                continue
            w = next((r for r in turns[t] if r["civ"] == winner["civ"]), None)
            a = next((r for r in turns[t] if r["civ"] == top_adv["civ"]), None)
            if not w or not a:
                continue
            for field, label, floor in COMPARED:
                wv, av = w[field], a[field]
                if max(wv, av) >= floor and wv > av:
                    gaps.append(((wv - av) / max(wv, 1), t, label, wv, av))
            if a["government"] != w["government"] and a["government"] == "Despotism":
                gaps.append((1.0, t, "government", w["government"], a["government"]))
        if not gaps:
            lost_lines.append("- No gap of note at turns 50, 100 or 150 (the game may have ended earlier).")
        for share, t, label, wv, av in sorted(gaps, key=lambda x: -x[0])[:3]:
            if label == "government":
                lost_lines.append(f"- Turn {t}, government: {winner['civ']} was in {wv}, {top_adv['civ']} still in {av}.")
            else:
                lost_lines.append(f"- Turn {t}, {label}: {winner['civ']} {num(wv)} against {top_adv['civ']} {num(av)}"
                                  f" ({share:.0%} behind).")
        lost_lines.append("")
    return won, len(games), lost_lines


def report(json_path, state_path, targets_path=None, out_dir=None, ledger_path=None):
    """Write the report; -> its path."""
    with open(json_path, encoding="utf-8") as f:
        data = json.load(f)
    games = load_state(state_path)
    targets = load_targets(targets_path)
    name = os.path.splitext(os.path.basename(json_path))[0]
    out = os.path.join(out_dir or os.path.dirname(os.path.abspath(json_path)), f"{name}-report.md")
    st = data.get("settings", {})
    won, played, lost_lines = autopsy(games)
    text = [f"# Benchmarks: {name}", "",
            f"{data.get('gamesPlayed', played)} games, {st.get('turns', '?')} turns, {st.get('civs', '?')} civs, "
            f"difficulty {st.get('difficulty', '?')}, first seed {st.get('firstSeed', '?')}. "
            f"Advanced is the trial half, Standard the other.", ""]
    if played:
        text += [f"**Advanced finished first in {won} of {played} games ({won / played:.0%}).**", ""]
    text += ["## Scorecard", ""] + scorecard(data.get("summary", []), targets) + [""]
    if not targets:
        text += ["_No Suede figures yet (no targets file, or none of its lines name a measure)._", ""]
    out_folder = out_dir or os.path.dirname(os.path.abspath(json_path))
    text += ["## Biggest gaps", ""]
    text += gaps_section(data.get("summary", []), targets,
                         [ledger_path or os.path.join(out_folder, "ledger.jsonl"), LEDGER_DEFAULT])
    text += [""]
    text += ["## Loss autopsy", ""]
    text += lost_lines if lost_lines else ["Advanced won every game." if played else "No state log rows."]
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(text) + "\n")
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--json", required=True, help="the trial's C7_TRIAL_JSON file")
    ap.add_argument("--state", required=True, help="the trial's C7_TRIAL_STATE file")
    ap.add_argument("--targets", help="Suede's figures, e.g. doc/suede-benchmarks.md")
    ap.add_argument("--out", help="folder for the report (default: next to the JSON)")
    ap.add_argument("--ledger", help="benchmark ledger (default: <out>/ledger.jsonl, then doc/benchmark-ledger.jsonl)")
    a = ap.parse_args()
    print(report(a.json, a.state, a.targets, a.out, a.ledger))


if __name__ == "__main__":
    main()
