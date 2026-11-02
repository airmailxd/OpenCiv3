#!/usr/bin/env python3
"""Summarize an OpenCiv3 play log: how a human's choices compare with the Advanced AI's.

    python tools/play-log/summarize.py <play-log.jsonl> [more logs] [--bin 50] [--civ Rome]

A play log is written with Settings > Developer > Record play log (see
doc/play-log.md). For each range of turns it prints how often the human chose
what the Advanced AI would have (research, production, government, tax rates),
the most common disagreements, how the human's city sites score against the
AI's best, and the human's benchmark measures next to the Advanced AI's
typical values from doc/ai-roadmap.md. Plain Python 3, no packages needed.
"""
import argparse
import collections
import json
import sys

# The Advanced AI's typical values, from doc/ai-roadmap.md (Track 2: Regent
# trials, 100x100 Continents, 8 civs, 200 turns) and doc/human-play-own-saves.md.
ADVANCED = {
    "cities50": 4,
    "cities100": 11.5,
    "cities150": 16.7,
    "cities200": 17.4,
    "govTurn": 125,        # all-Advanced games leave Despotism about turn 125 (ZUR-193)
    "techs100": 14.5,      # 14-15.5 (ZUR-204, ZUR-209)
    "workersPerCity100": 1.0,  # workers-1-2: one per city while expanding
    "military100": 46,     # 45-48 at turns 100-150
    "military150": 47,
}


def load(paths):
    rows = []
    for path in paths:
        with open(path, encoding="utf-8") as f:
            for line in f:
                line = line.strip()
                if line:
                    rows.append(json.loads(line))
    return rows


def kind(row):
    return row.get("type", "state")


def bin_label(turn, size):
    start = (turn // size) * size
    return start, f"{start}-{start + size - 1}"


def pct(agree, total):
    return f"{agree}/{total} ({100 * agree / total:.0f}%)" if total else "-"


def agreement(actions, size):
    """-> {bin start: {decision: [agree, total]}} and the bin labels."""
    table = collections.defaultdict(lambda: collections.defaultdict(lambda: [0, 0]))
    labels = {}
    # Tax: the last slider change of each turn is the rate the human settled on.
    last_slider = {}
    for a in actions:
        if a.get("msg") == "MsgChangeSliders" and "shadow" in a:
            last_slider[(a["civ"], a["turn"])] = a
    for a in actions:
        shadow = a.get("shadow")
        if shadow is None:
            continue
        msg = a.get("msg")
        decision = {
            "MsgChooseResearch": "research",
            "MsgChooseProduction": "production",
            "SelectGovernmentMsg": "government",
            "StartGovernmentTransitionMsg": "revolution",
            "MsgChangeSliders": "tax",
        }.get(msg)
        if decision is None:
            continue
        if decision == "tax" and last_slider.get((a["civ"], a["turn"])) is not a:
            continue
        agree = shadow.get("wouldRevolt") if decision == "revolution" else shadow.get("agree")
        start, label = bin_label(a["turn"], size)
        labels[start] = label
        cell = table[start][decision]
        cell[0] += 1 if agree else 0
        cell[1] += 1
    return table, labels


def disagreements(actions, msg, human_key, ai_key="pick", limit=8):
    pairs = collections.Counter()
    for a in actions:
        if a.get("msg") != msg or "shadow" not in a or a["shadow"].get("agree"):
            continue
        human = a.get(human_key) if human_key in a else a["shadow"].get(human_key)
        pairs[(human, a["shadow"].get(ai_key))] += 1
    return pairs.most_common(limit)


def benchmarks(states):
    """-> {civ: {measure: value}} from the human's state rows."""
    by_civ = collections.defaultdict(list)
    for s in states:
        if s.get("ai") == "Human":
            by_civ[s["civ"]].append(s)
    out = {}
    for civ, rows in by_civ.items():
        rows.sort(key=lambda r: r["turn"])
        last = rows[-1]["turn"]

        def at(turn):
            if last < turn:
                return None
            return max((r for r in rows if r["turn"] <= turn), key=lambda r: r["turn"], default=None)

        m = {}
        for t in (50, 100, 150, 200):
            r = at(t)
            m[f"cities{t}"] = r["cities"] if r else None
        start = rows[0].get("government")
        m["govTurn"] = next((r["turn"] for r in rows if r.get("government") != start), None)
        r100 = at(100)
        m["techs100"] = r100["techs"] if r100 else None
        m["workersPerCity100"] = round(r100["workers"] / r100["cities"], 2) if r100 and r100["cities"] else None
        m["military100"] = r100["military"] if r100 else None
        r150 = at(150)
        m["military150"] = r150["military"] if r150 else None
        m["lastTurn"] = last
        m["last"] = rows[-1]
        out[civ] = m
    return out


def show(v):
    if v is None:
        return "-"
    return f"{v:.2f}".rstrip("0").rstrip(".") if isinstance(v, float) else str(v)


def report(rows, size, civ=None):
    if civ:
        rows = [r for r in rows if r.get("civ") in (None, civ) or kind(r) == "header"]
    headers = [r for r in rows if kind(r) == "header"]
    actions = [r for r in rows if kind(r) == "action"]
    states = [r for r in rows if kind(r) == "state"]
    fights = [r for r in rows if kind(r) == "combat"]
    out = []

    humans = sorted({s["civ"] for s in states if s.get("ai") == "Human"} | {a["civ"] for a in actions})
    turns = [r["turn"] for r in states + actions]
    out.append(f"Play log: {len(headers)} session(s), game {', '.join(sorted({str(h.get('game')) for h in headers})) or '?'}, "
               f"human {', '.join(humans) or '?'}, turns {min(turns) if turns else '?'}-{max(turns) if turns else '?'}, "
               f"{len(actions)} actions, {len(fights)} fights.")
    if headers:
        h = headers[0]
        out.append(f"Difficulty {h.get('difficulty')}, map {h.get('map')}, "
                   f"civs: {', '.join(c['civ'] + ' (' + c['ai'] + ')' for c in h.get('civs', []))}.")
    out.append("")

    # Agreement.
    table, labels = agreement(actions, size)
    decisions = ["research", "production", "government", "revolution", "tax"]
    out.append("Human vs Advanced AI: how often the human chose what the AI would have")
    out.append("(revolution: revolts the AI would also have started now; tax: the last rates set each turn)")
    out.append("")
    out.append("| Turns | " + " | ".join(decisions) + " |")
    out.append("|---|" + "---:|" * len(decisions))
    totals = collections.defaultdict(lambda: [0, 0])
    for start in sorted(table):
        cells = []
        for d in decisions:
            agree, total = table[start][d]
            totals[d][0] += agree
            totals[d][1] += total
            cells.append(pct(agree, total))
        out.append(f"| {labels[start]} | " + " | ".join(cells) + " |")
    out.append("| all | " + " | ".join(pct(*totals[d]) for d in decisions) + " |")
    out.append("")

    # Disagreements.
    out.append("Most common disagreements (human -> Advanced AI):")
    for title, msg, key in (("production", "MsgChooseProduction", "producibleName"),
                            ("research", "MsgChooseResearch", "human"),
                            ("government", "SelectGovernmentMsg", "government")):
        pairs = disagreements(actions, msg, key)
        if pairs:
            out.append(f"  {title}: " + "; ".join(f"{h} -> {a} x{n}" for (h, a), n in pairs))
    shares = [a["shadow"]["humanShare"] for a in actions
              if a.get("msg") == "MsgChooseResearch" and a.get("shadow", {}).get("humanShare") is not None]
    if shares:
        out.append(f"  research: the AI's own roll would have picked the human's tech {100 * sum(shares) / len(shares):.0f}% of the time on average")
    out.append("")

    # City sites.
    sites = [a for a in actions if a.get("msg") == "MsgBuildCity" and "shadow" in a]
    if sites:
        out.append("City sites (the Advanced AI's site score; best = best site it knew of from that settler):")
        scored = [s for s in sites if s["shadow"].get("siteScore") is not None and s["shadow"].get("bestScore")]
        for s in sites:
            sh = s["shadow"]
            pick = sh.get("aiPick") or {}
            out.append(f"  turn {s['turn']} {s.get('name')} at {s.get('at')}: score {show(sh.get('siteScore'))} "
                       f"(rank {show(sh.get('siteRank'))} of {sh.get('candidates')}), best {show(sh.get('bestScore'))}; "
                       f"AI would go to {pick.get('at', '-')} ({show(pick.get('distance'))} tiles, score {show(pick.get('score'))})")
        if scored:
            ratio = sum(s["shadow"]["siteScore"] / s["shadow"]["bestScore"] for s in scored) / len(scored)
            top = sum(1 for s in scored if s["shadow"].get("siteRank") == 1)
            out.append(f"  mean human/best score {ratio:.2f}; the AI's best site {top} of {len(scored)} times; "
                       f"{len(sites) - len(scored)} sites the AI wouldn't have counted")
        out.append("")

    # Benchmarks.
    bench = benchmarks(states)
    if bench:
        names = ["cities50", "cities100", "cities150", "cities200", "govTurn", "techs100",
                 "workersPerCity100", "military100", "military150"]
        out.append("Benchmarks (the human's; Advanced = the AI's typical values in doc/ai-roadmap.md):")
        out.append("")
        out.append("| Measure | " + " | ".join(bench) + " | Advanced |")
        out.append("|---|" + "---:|" * (len(bench) + 1))
        for n in names:
            out.append(f"| {n} | " + " | ".join(show(m[n]) for m in bench.values()) + f" | {show(ADVANCED.get(n))} |")
        out.append("")
        for c, m in bench.items():
            last = m["last"]
            out.append(f"  {c} at turn {m['lastTurn']}: {last['cities']} cities, {last['citizens']} citizens, "
                       f"{last['techs']} techs, {last['military']} military, {last['workers']} workers, "
                       f"{last['government']}, score {show(last['score'])}"
                       + (f", at war with {', '.join(last['atWarWith'])}" if last.get("atWarWith") else ""))
            if m["govTurn"] is None:
                out.append(f"  ({c} never changed government: govTurn counts the first change, anarchy included, as in AiTrialTest)")
        out.append("")

    # Fights.
    if fights:
        attacks = [f for f in fights if f.get("humanAttacks")]
        won = sum(1 for f in attacks if f.get("result") in ("DefenderKilled", "DefenderRetreated"))
        defended = [f for f in fights if not f.get("humanAttacks")]
        held = sum(1 for f in defended if f.get("result") in ("AttackerKilled", "AttackerRetreated"))
        line = f"Fights: {len(attacks)} attacks by the human, {won} won"
        if attacks:
            line += f" (mean win chance by the AI's estimate {sum(f.get('winChance', 0) for f in attacks) / len(attacks):.2f})"
        line += f"; {len(defended)} attacks on the human, {held} held."
        out.append(line)
    return "\n".join(out)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("logs", nargs="+", help="play log files (.jsonl)")
    ap.add_argument("--bin", type=int, default=50, help="turns per range (default 50)")
    ap.add_argument("--civ", help="only this human civ (in a hotseat or LAN game)")
    a = ap.parse_args()
    print(report(load(a.logs), max(1, a.bin), a.civ))


if __name__ == "__main__":
    sys.exit(main())
