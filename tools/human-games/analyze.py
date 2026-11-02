"""Turn the COTM result tables and the GotmReader rows into doc/human-benchmarks.json and a printed summary.

Usage: python -I analyze.py <cotm_all.json> <manifest.json> <reader.jsonl> <out.json>
"""
import json
import statistics as st
import sys
from collections import Counter, defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))  # -I leaves the script's directory off the path
from civ3_calendar import parse_date, turn_of_year

LEVELS = ["Warlord", "Regent", "Monarch", "Emperor", "Demigod", "Deity", "Sid"]
CHECKPOINTS = [50, 75, 100, 125, 150, 175, 200]
# Variant games (one-city, scenarios, mods, custom start rules): not comparable with a normal game.
VARIANT_FLAGS = ["obc", "one city", "occ", "can't build settler", "cannot build settler", "scenario", " mod ", "custom"]


def level(s):
    s = (s or "").lower()
    if s.startswith("moa"):  # COTM 15's page says "Moarch"
        return "Monarch"
    for lv in LEVELS:
        if s.startswith(lv.lower()[:4]):
            return lv
    return s or "?"


def is_variant(g):
    b = " " + ((g.get("blurb") or "") + " " + (g.get("ruleChanges") or "")).lower() + " "
    return any(f in b for f in VARIANT_FLAGS)


def q(xs):
    xs = sorted(xs)
    if not xs:
        return None
    if len(xs) < 4:
        return {"n": len(xs), "median": st.median(xs), "min": xs[0], "max": xs[-1]}
    qs = st.quantiles(xs, n=4)
    return {"n": len(xs), "median": st.median(xs), "q1": qs[0], "q3": qs[2], "min": xs[0], "max": xs[-1]}


def tables(games):
    """Victory types and finish turns from the published result tables (open class, normal games)."""
    out = {}
    by = defaultdict(list)
    for g in games:
        if "results" not in g or is_variant(g):
            continue
        for r in g["results"]:
            if r.get("class", "") != "":
                continue
            by[level(g.get("difficulty"))].append((g, r))
    for lv, rows in by.items():
        wins = [(g, r) for g, r in rows if r["outcome"] == "Victory"]
        cond = Counter(r["condition"] for _, r in wins)
        finish = defaultdict(list)
        for _, r in wins:
            y = parse_date(r["date"])
            if y is not None:
                finish[r["condition"]].append(turn_of_year(y))
        # the top third of each game's field (rank by Jason score)
        top = []
        for g in {id(g): g for g, _ in rows}.values():
            ranked = [r for r in g["results"] if r.get("class", "") == "" and r["outcome"] == "Victory"]
            top += ranked[: max(1, len(ranked) // 3)]
        topcond = Counter(r["condition"] for r in top)
        out[lv] = {
            "games": len({g["cotm"] for g, _ in rows}),
            "entries": len(rows),
            "wins": len(wins),
            "losses": sum(1 for _, r in rows if r["outcome"] == "Loss"),
            "victoryShare": {k: round(v / len(wins), 3) for k, v in cond.most_common()} if wins else {},
            "conquestOrDominationShare": round((cond["Conquest"] + cond["Domination"]) / len(wins), 3) if wins else None,
            "topThirdConquestOrDominationShare": round((topcond["Conquest"] + topcond["Domination"]) / len(top), 3) if top else None,
            "topThirdEntries": len(top),
            "finishTurn": {k: q(v) for k, v in finish.items()},
        }
    return out


def saves(manifest, rows, games):
    gi = {g["cotm"]: g for g in games}
    by_file = {}
    for r in rows:
        by_file[r["file"]] = r
    out = []
    for m in manifest:
        r = next((x for x in rows if x.get("_path") == m["path"]), None)
        if r is None:
            continue
        g = gi[m["cotm"]]
        row = {
            "cotm": m["cotm"], "player": m["player"], "rank": m["rank"], "fieldSize": m["fieldSize"],
            "difficulty": level(g.get("difficulty")), "civ": r.get("humanCiv"),
            "map": f'{r.get("mapWidth")}x{r.get("mapHeight")}',
            "topology": g.get("topology"), "rivals": sum(1 for c in r.get("civs", []) if not c["human"]),
            "victory": m["condition"], "date": m["date"], "lastTurn": r.get("turn"),
            "error": r.get("error"),
        }
        if "series" in r:
            ser = {p["turn"]: p for p in r["series"]}
            last = r["series"][-1]
            for t in CHECKPOINTS:
                p = ser.get(t)
                if p is None and t > last["turn"]:
                    continue  # game over before this turn
                if p:
                    row[f"cities{t}"] = p["cities"]
                    row[f"captured{t}"] = p["captured"]
                    row[f"founded{t}"] = p["founded"]
                    row[f"aiCitiesMean{t}"] = p["aiCitiesMean"]
                    row[f"aiCitiesMax{t}"] = p["aiCitiesMax"]
                    row[f"tiles{t}"] = p["tiles"]
                    if "power" in p and p.get("aiPowerMax"):
                        row[f"powerRatio{t}"] = round(p["power"] / p["aiPowerMax"], 2)
            for key, t in (r.get("reach") or {}).items():
                row[key] = t
            row["citiesFinal"] = last["cities"]
            row["foundedTotal"] = last["founded"]
            row["capturedTotal"] = last["captured"]
            row["otherGainedTotal"] = last["otherGained"]
            row["lostTotal"] = last["lost"]
            row["firstCaptureTurn"] = r.get("firstCaptureTurn")
            row["firstLossTurn"] = r.get("firstLossTurn")
            row["citiesCheck"] = r.get("citiesCheck")
            row["governmentFinal"] = r.get("humanGovernment")
            row["unitsFinal"] = r.get("humanUnits")
            # last founding turn, from the per-turn series isn't kept; approximate with the checkpoints
            row["conquestShareFinal"] = round(last["captured"] / max(1, last["founded"] + last["captured"] + last["otherGained"]), 2)
            caps = r.get("captures", [])
            civ_caps = [c for c in caps if c[1] not in ("none", "?", row["civ"])]
            row["firstCivCaptureTurn"] = civ_caps[0][0] if civ_caps else None
            ft = r.get("foundTurns") or []
            fc = row["firstCivCaptureTurn"]
            if fc:
                row["foundedBeforeFirstCapture"] = sum(1 for t in ft if t < fc)
                row["foundedInPrev25"] = sum(1 for t in ft if fc - 25 <= t < fc)
                row["foundedInNext50"] = sum(1 for t in ft if fc <= t < fc + 50)
            row["capturesFromNoCiv"] = sum(1 for c in caps if c[1] == "none")  # barbarian towns (no territory)
            row["firstCaptures"] = caps[:8]
            row["lastFoundTurn"] = r.get("lastFoundTurn")
            row["foundTurns"] = r.get("foundTurns")
        out.append(row)
    return out


def summarise(rows):
    by = defaultdict(list)
    for r in rows:
        if r.get("error") or "citiesFinal" not in r:
            continue
        by[r["difficulty"]].append(r)
    out = {}
    for lv, rs in by.items():
        s = {"saves": len(rs), "games": len({r["cotm"] for r in rs})}
        for t in CHECKPOINTS:
            for key in ("cities", "captured", "aiCitiesMean", "powerRatio"):
                xs = [r[f"{key}{t}"] for r in rs if f"{key}{t}" in r]
                if xs:
                    s[f"{key}{t}"] = q(xs)
            s[f"stillPlaying{t}"] = sum(1 for r in rs if f"cities{t}" in r)
        s["firstCaptureTurn"] = q([r["firstCaptureTurn"] for r in rs if r["firstCaptureTurn"] and r["firstCaptureTurn"] > 0])
        s["firstCivCaptureTurn"] = q([r["firstCivCaptureTurn"] for r in rs if r.get("firstCivCaptureTurn")])
        s["capturedBeforeT100Share"] = round(sum(1 for r in rs if r.get("captured100", 0) > 0) / len(rs), 2)
        # cities founded and captured in each 25-turn window (only saves still playing at the window's end)
        win = {}
        prev = 0
        for t in CHECKPOINTS:
            f = [r[f"founded{t}"] - (r.get(f"founded{prev}", 0) if prev else 0) for r in rs if f"founded{t}" in r]
            c = [r[f"captured{t}"] - (r.get(f"captured{prev}", 0) if prev else 0) for r in rs if f"captured{t}" in r]
            win[f"{prev}-{t}"] = {"n": len(f), "foundedMean": round(st.mean(f), 1) if f else None,
                                 "capturedMean": round(st.mean(c), 1) if c else None}
            prev = t
        s["perWindow"] = win
        for key in ("foundedBeforeFirstCapture", "foundedInPrev25", "foundedInNext50"):
            s[key] = q([r[key] for r in rs if key in r])
        for n in (5, 10, 20, 30):
            xs = [r.get(f"city{n}turn") for r in rs]
            s[f"city{n}turn"] = q([x for x in xs if x])
            s[f"city{n}never"] = sum(1 for x in xs if not x)
        s["neverCaptured"] = sum(1 for r in rs if r["capturedTotal"] == 0)
        s["lastFoundTurn"] = q([r["lastFoundTurn"] for r in rs if r.get("lastFoundTurn")])
        s["foundedTotal"] = q([r["foundedTotal"] for r in rs])
        s["conquestShareFinal"] = q([r["conquestShareFinal"] for r in rs])
        s["governmentFinal"] = dict(Counter(r["governmentFinal"] for r in rs))
        out[lv] = s
    return out


def main():
    games = json.load(open(sys.argv[1], encoding="utf-8"))
    manifest = json.load(open(sys.argv[2], encoding="utf-8"))
    rows = []
    for line in open(sys.argv[3], encoding="utf-8"):
        if line.strip():
            rows.append(json.loads(line))
    # reader rows carry only the file name; match by the manifest order (one save per directory)
    names = {m["path"].replace("\\", "/").split("/")[-2]: m["path"] for m in manifest}
    for r in rows:
        r["_path"] = names.get(r.get("dir"), None)
    per_save = saves(manifest, rows, games)
    result = {
        "about": "Strong-human benchmark from CivFanatics Civ3 Conquests COTM. See doc/human-benchmarks.md.",
        "calendar": "turn 0 = 4000 BC; tools/human-games/civ3_calendar.py",
        "resultTables": tables(games),
        "saveSummary": summarise(per_save),
    }
    # Per difficulty, the medians under the names benchmarks.py's ROWS use (doc/human-benchmarks.md, "Wiring")
    result["targets"] = {lv: {k: v[k]["median"] for k in ("cities50", "cities100", "city5turn", "city10turn") if v.get(k)}
                         for lv, v in result["saveSummary"].items()}
    # The summary indented, the per-save rows one to a line
    head = json.dumps(result, indent=1, ensure_ascii=False)
    rows_text = ",\n".join("  " + json.dumps(r, ensure_ascii=False) for r in per_save)
    with open(sys.argv[4], "w", encoding="utf-8", newline="\n") as f:
        f.write(head[:-2] + ',\n "saves": [\n' + rows_text + "\n ]\n}\n")
    print(json.dumps(result["resultTables"], indent=1))
    print(json.dumps(result["saveSummary"], indent=1))


if __name__ == "__main__":
    main()
