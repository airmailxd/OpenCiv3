"""Train the Advanced AI by self-play: tune its knobs in AI trials.

The ladder: the Advanced AI always plays with no difficulty bonuses
(Regent's, see GameData.AiBonusDifficulty), against Standard AIs at
Regent, then Monarch, Emperor, Demigod and Deity. At each rung a
coordinate search tries one change at a time (each knob up or down a step,
each on-trial behaviour on or off) in paired AI trials on training seeds,
keeps the best change only if it also helps on held-out seeds, and repeats
until nothing helps. The rung is passed when, in an exam on other held-out
seeds (an unpaired trial, so wins are counted directly), an Advanced civ
finishes first by score in at least the rung's share of games (see RUNGS;
half by chance alone) and the Advanced civs rank clearly better; then the
next rung starts from the knobs it ended with. Deity is the last rung.

Each trial is EngineTests' AiTrialTest with C7_TRIAL_ADVANCED=1 and
C7_TRIAL_PAIRED=1: half the civs Advanced, and the same seeds replayed
with none, so map luck cancels out.

Usage (from the repo root):
    python tools/advanced-ai-training/train.py --out <dir>
    python tools/advanced-ai-training/train.py --out <dir> --resume

With --nonstop the ladder never stops on a failed exam: it starts a new
cycle on the same rung (new training and held-out seeds, +100 per cycle)
and exits with code 75, so run-training.ps1 -Nonstop can move to the latest
code and call it again. An exam also follows every kept change. Tuning
specs in <dir>/ideas.txt (one per line, # for comments) are tried as extra
candidates, once per cycle. Unless --no-steer, each round tries first the
candidates that the benchmark ledger says helped one of the 3 worst measures
of the latest exam (the keep rule is unchanged), and after each exam a kept
ledger entry whose Advanced/Standard ratio on a measure it moved has shifted from the ratio the change produced
by more than 2 exam SE is marked stale (a "recheck_of" ledger line) and its
reversal queued in ideas.txt as "# recheck <id>" (at most 2 per cycle).

Results: <dir>/ledger.jsonl (what each held-out check did to every measure; see ledger.py), <dir>/state.json (the knobs and every run), <dir>/log.md and one
line per round or exam in <dir>/progress.txt.
While it runs, <dir>/heartbeat.json says it is alive and <dir>/dashboard.html
is rebuilt every --dashboard-every seconds (see dashboard.py).

Map profiles (the Advanced AI's knobs for a kind of map; see
C7Engine/AI/Advanced/AdvancedProfiles.cs and doc/ai-roadmap.md Track 2):
--world (Tiny, Small, Standard, Large, Huge), --landform (Pangaea,
Continents, Archipelago) and --water (60, 70, 80) play every trial on that
kind of map (C7_TRIAL_WORLD, _LANDFORM, _WATER), with the world size's
Civ3 default civ count (4, 6, 8, 12, 16) unless --civs; --count 1 measures
a lone Advanced civ (C7_TRIAL_COUNT). --profile <key> (e.g. Huge/Pangaea/70)
tunes that profile: the map comes from the key unless given, and a fresh
ladder starts from the profile's values in the profiles file. Trials always
run with C7_ADVANCED_PROFILE=none, so the knobs searched are the only
tuning in play. A resumed ladder keeps the map it started with. Once the
ladder has settled:
    python tools/advanced-ai-training/train.py --out <dir> --export-profile Huge/Pangaea/70
writes the ladder's kept knobs, where they differ from the defaults, into
C7/Lua/civ3/advanced-ai-profiles.json (--profiles-file for another), in
place of that key's profile; commit the file to ship it. Use a new --out
for each kind of map: runs are cached by name in <dir>/runs.
"""
import argparse
import json
import os
import re
import subprocess
import sys
import threading
import time
from concurrent.futures import ThreadPoolExecutor

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import benchmarks  # noqa: E402
import ledger  # noqa: E402

# Step sizes for the numeric knobs (see C7Engine/AI/Advanced/AdvancedTuning.cs).
STEPS = {
    "FocusPoints": 5,
    "TargetStrengthRatio": 0.25,
    "ThreatStrengthRatio": 0.25,
    "BehindInTechShare": 0.05,
    "ExpandSettlers": 10,
    "ExpandAggression": 1,
    "BuildScience": 10,
    "BuildProduction": 10,
    "WinningOffense": 10,
    "PreyAggression": 1,
    "PlanEveryTurns": 2,
    "WeightExpansion": 5,
    "WeightWorkers": 5,
    "WeightResearch": 5,
    "WeightMilitary": 5,
    "WeightDiplomacy": 5,
    "WeightInfrastructure": 5,
    "WeightWonders": 5,
}
LIMITS = {
    "FocusPoints": (0, 60),
    "TargetStrengthRatio": (1.0, 4.0),
    "ThreatStrengthRatio": (1.0, 4.0),
    "BehindInTechShare": (0.5, 1.0),
    "ExpandSettlers": (50, 100),
    "ExpandAggression": (-2, 2),
    "BuildScience": (0, 100),
    "BuildProduction": (0, 100),
    "WinningOffense": (50, 100),
    "PreyAggression": (-2, 2),
    "PlanEveryTurns": (1, 15),
    "WeightExpansion": (0, 100),
    "WeightWorkers": (0, 100),
    "WeightResearch": (0, 100),
    "WeightMilitary": (0, 100),
    "WeightDiplomacy": (0, 100),
    "WeightInfrastructure": (0, 100),
    "WeightWonders": (0, 100),
}
BEHAVIOURS = ["wonder-timing", "wonder-value", "war-production", "war-no-peace",
              "leader-hurry-best", "lean-military", "no-ai-luxury", "snipe-workers",
              "lux-slider-high", "site-food", "site-tight-7", "site-tight-12", "trade-gpt",
              "railroad-first", "philosophy-first", "early-rush", "archer-rush", "phase-plan",
              "workers-1-2", "early-government", "weights-by-state",
              "granary-pump", "republic-route", "tech-swap",
              "lean-garrison", "grow-to-settle", "prey-war", "horde-war", "fast-expand"]
ON_BY_DEFAULT = {"lean-military", "lux-slider-high", "site-food", "site-tight-7",
                  "workers-1-2"}
DEFAULTS = {"FocusPoints": 15, "TargetStrengthRatio": 1.75, "ThreatStrengthRatio": 1.75,
            "BehindInTechShare": 0.9, "ExpandSettlers": 85, "ExpandAggression": -1,
            "BuildScience": 55, "BuildProduction": 65, "WinningOffense": 80,
            "PreyAggression": 1, "PlanEveryTurns": 5,
            **{f"Weight{w}": 50 for w in ("Expansion", "Workers", "Research", "Military", "Diplomacy", "Infrastructure", "Wonders")}, **{b: int(b in ON_BY_DEFAULT) for b in BEHAVIOURS}}
# Each rung: its name, the Standard AIs' difficulty (an index into the
# difficulties) and the share of exam games an Advanced civ must win to
# move up (the owner's bars, 2026-10-29). Deity's exam is only reported.
RUNGS = [("Regent", 2, 1.00), ("Monarch", 3, 0.90), ("Emperor", 4, 0.75), ("Demigod", 5, 0.50), ("Deity", 6, None)]

LINE = re.compile(r"^(\w+)\s+([-\d.]+) vs\s+([-\d.]+)\s+([-+\d.]+) \+- ([\d.]+)")


NONSTOP_EXIT = 75

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
PROFILES_FILE = os.path.join(REPO, "C7", "Lua", "civ3", "advanced-ai-profiles.json")
# Civ3's world sizes and the default number of civs on each (the BIQ's WSIZ;
# doc/civ3-compendium/07-diplomacy-victory-misc.md section 10).
WORLD_CIVS = {"Tiny": 4, "Small": 6, "Standard": 8, "Large": 12, "Huge": 16}
LANDFORMS = ("Archipelago", "Continents", "Pangaea")
WATERS = (60, 70, 80)


def parse_key(key):
    """A profile key, 'Huge/Pangaea/70' in any order and case -> {"world", "landform", "water"} (those it names)."""
    out = {}
    for part in (p.strip() for p in key.split("/")):
        world = next((w for w in WORLD_CIVS if w.lower() == part.lower()), None)
        landform = next((l for l in LANDFORMS if l.lower() == part.lower()), None)
        if world:
            field, value = "world", world
        elif landform:
            field, value = "landform", landform
        elif part.isdigit() and int(part) in WATERS:
            field, value = "water", int(part)
        else:
            raise ValueError(f"profile key {key!r}: {part!r} is no world size {tuple(WORLD_CIVS)}, landform {LANDFORMS} or water {WATERS}")
        if field in out:
            raise ValueError(f"profile key {key!r} names the {field} twice")
        out[field] = value
    return out


def canonical_key(key):
    """'70/pangaea/HUGE' -> 'Huge/Pangaea/70'."""
    k = parse_key(key)
    return "/".join(str(k[f]) for f in ("world", "landform", "water") if f in k)


def map_settings(args):
    """The ladder's map from --profile/--world/--landform/--water/--civs/--count: {} for today's default map."""
    m = {}
    try:
        if args.profile:
            m["profile"] = canonical_key(args.profile)
            m.update(parse_key(args.profile))
        if args.world:
            m["world"] = parse_key(args.world)["world"]
        if args.landform:
            m["landform"] = parse_key(args.landform)["landform"]
        if args.water:
            m["water"] = parse_key(str(args.water))["water"]
    except (ValueError, KeyError) as e:
        raise SystemExit(f"bad map option: {e}")
    if args.civs:
        m["civs"] = args.civs
    elif "world" in m:
        m["civs"] = WORLD_CIVS[m["world"]]
    if args.count:
        m["count"] = args.count
    return m


def use_map(m):
    """Sets CIVS and COUNT for the ladder's map ({}, or a state without "map": today's 8 civs, half Advanced)."""
    global CIVS, COUNT
    m = m or {}
    CIVS = int(m.get("civs", 8))
    COUNT = int(m.get("count", 0))


def map_env(m):
    """The trial settings for the ladder's map; none for today's default map."""
    env = {}
    for field, var in (("world", "C7_TRIAL_WORLD"), ("landform", "C7_TRIAL_LANDFORM"), ("water", "C7_TRIAL_WATER"), ("count", "C7_TRIAL_COUNT")):
        if field in m:
            env[var] = str(m[field])
    return env


def map_text(m):
    if not m:
        return "the default map, 8 civs"
    parts = [str(m[f]) for f in ("world", "landform", "water") if f in m]
    return (("/".join(parts) or "the default map") + f", {m.get('civs', 8)} civs"
            + (f", {m['count']} Advanced" if m.get("count") else "") + (f", profile {m['profile']}" if m.get("profile") else ""))


def same_key(a, b):
    try:
        return parse_key(a) == parse_key(b)
    except ValueError:
        return False


def load_profiles(path):
    if not os.path.exists(path):
        return {"profiles": {}}
    with open(path, encoding="utf-8") as f:
        doc = json.load(f)
    doc.setdefault("profiles", {})
    return doc


def profile_knobs(path, key):
    """The knobs a fresh ladder for a profile starts from: the defaults, with the profile's values if it has any yet."""
    found = next((v for k, v in load_profiles(path)["profiles"].items() if same_key(k, key)), {})
    unknown = [k for k in found if k not in DEFAULTS]
    if unknown:
        raise SystemExit(f"profile {key} in {path} has knobs train.py doesn't search: {', '.join(unknown)}")
    return {**DEFAULTS, **{k: int(v) if k in BEHAVIOURS else v for k, v in found.items()}}


def number(v):
    return int(v) if float(v).is_integer() else v


def export_profile(path, key, knobs):
    """Writes the knobs' differences from the defaults as profile `key`, in place of it -> (key written, its knobs)."""
    key = canonical_key(key)
    doc = load_profiles(path)
    profiles = {k: v for k, v in doc["profiles"].items() if not same_key(k, key)}
    profiles[key] = {k: number(v) for k, v in sorted(knobs.items()) if k in DEFAULTS and v != DEFAULTS[k]}
    doc["profiles"] = dict(sorted(profiles.items()))
    tmp = path + ".tmp"
    with open(tmp, "w", encoding="utf-8", newline="\n") as f:
        json.dump(doc, f, indent=2)
        f.write("\n")
    os.replace(tmp, path)
    return key, profiles[key]


def spec(knobs):
    return ",".join(f"{k}={v:g}" for k, v in sorted(knobs.items()) if v != DEFAULTS[k])


# Paired trials (the default) cancel map luck for comparing knobs; the exam
# is unpaired, so its "first" column is the Advanced civs' own wins.
def run_trial(args, knobs, difficulty, seed, tag, paired=True, detail=False):
    out = os.path.join(args.out, "runs", f"{tag}.txt")
    if os.path.exists(out) and "total ms" in open(out, encoding="utf-8").read():
        return parse(out)
    env = dict(os.environ, **trial_env(args, knobs, difficulty, seed, out, paired))
    if detail:
        # The exam also writes the state log that benchmarks.py reads.
        env.update(C7_TRIAL_STATE=out[:-4] + "-state.jsonl")
        if os.path.exists(env["C7_TRIAL_STATE"]):
            os.remove(env["C7_TRIAL_STATE"])
    with open(out + ".log", "w", encoding="utf-8") as log:
        subprocess.run(["dotnet", "test", "EngineTests", "--no-build", "--filter", "FullyQualifiedName~AiTrialTest.CompareAiWithAndWithoutExperiments"],
                       env=env, stdout=log, stderr=subprocess.STDOUT, check=False)
    if not os.path.exists(out):
        raise RuntimeError(f"trial {tag} wrote no results; see {out}.log")
    return parse(out)


def trial_env(args, knobs, difficulty, seed, out, paired):
    return dict(C7_TRIAL_OUT=out, C7_TRIAL_EXPERIMENTS="", C7_TRIAL_ADVANCED="1", C7_TRIAL_PAIRED="1" if paired else "0",
                C7_TRIAL_GAMES=str(args.games), C7_TRIAL_DIFFICULTY=str(difficulty), C7_TRIAL_SEED=str(seed),
                C7_TRIAL_TURNS=str(args.turns), C7_TRIAL_CIVS=str(CIVS), C7_ADVANCED_TUNING=spec(knobs),
                # The knobs searched are the only tuning: no map profile from
                # advanced-ai-profiles.json underneath them.
                C7_ADVANCED_PROFILE="none",
                # Every run writes its JSON: the .txt rounds each measure to one
                # decimal, which made "first" (and so the win rate) step by 40%.
                C7_TRIAL_JSON=out[:-4] + ".json",
                **map_env(getattr(args, "map", None) or {}))


def write_report(args, tag):
    """The exam's benchmarks report, linked from log.md. A failure here must not stop the ladder."""
    base = os.path.join(args.out, "runs", tag)
    try:
        path = benchmarks.report(base + ".json", base + "-state.jsonl", args.targets,
                                 ledger_path=os.path.join(args.out, "ledger.jsonl"))
        note(args, f"- benchmarks report: [{os.path.relpath(path, args.out)}]({os.path.relpath(path, args.out).replace(os.sep, '/')})")
    except Exception as e:  # noqa: BLE001
        note(args, f"- benchmarks report not written: {e}")


def record(args, prefix, label, before, after, difficulty, seed, kept, recheck_of=None):
    """Append the held-out check of one candidate to <out>/ledger.jsonl. A failure here must not stop the ladder."""
    try:
        runs = os.path.join(args.out, "runs")
        texts = [open(os.path.join(runs, f"{prefix}-holdout-{t}.txt"), encoding="utf-8").read() for t in ("base", label)]
        meta = {"ticket": args.ticket, "change": {"label": label, "before": spec(before), "after": spec(after)},
                "difficulty": difficulty, "seed": seed, "games": args.games, "turns": args.turns,
                "mode": "paired", "kept": kept}
        if recheck_of:
            meta["recheck_of"] = recheck_of
        ledger.append(os.path.join(args.out, "ledger.jsonl"), ledger.entry_from_trial(texts[0], texts[1], meta))
    except Exception as e:  # noqa: BLE001
        note(args, f"- ledger entry not written: {e}")


def parse(path):
    r = {}
    for line in open(path, encoding="utf-8"):
        m = LINE.match(line)
        if m:
            # The trial writes a tiny negative difference as "-+0.00".
            r[m[1]] = {"adv": float(m[2]), "std": float(m[3]), "diff": float(m[4].replace("-+", "-")), "se": float(m[5])}
    # The run's JSON, when there is one, has the exact means: the .txt rounds
    # them to one decimal, so "first" per civ read 0.2 for anything from 0.15
    # to 0.25 and the win rate (first x 4) could only be 0, 40, 80 or 120%.
    exact = path[:-4] + ".json"
    if os.path.exists(exact):
        try:
            with open(exact, encoding="utf-8") as f:
                summary_rows = json.load(f).get("summary") or []
            for s in summary_rows:
                if s.get("measure") in r:
                    r[s["measure"]].update(adv=s["trialMean"], std=s["othersMean"], diff=s["difference"], se=s["standardError"])
        except (OSError, ValueError, KeyError, TypeError):
            pass
    return r


def objective(r):
    # Finishing better ranked is the aim; score breaks near-ties.
    return -r["rank"]["diff"] + r["score"]["diff"] / 500


def candidates(knobs):
    for k, step in STEPS.items():
        lo, hi = LIMITS[k]
        for d in (-step, step):
            v = round(knobs[k] + d, 4)
            if lo <= v <= hi:
                yield f"{k}{'+' if d > 0 else '-'}", {**knobs, k: v}
    for b in BEHAVIOURS:
        yield f"{b}{'-off' if knobs[b] else '-on'}", {**knobs, b: 0 if knobs[b] else 1}


def note(args, text):
    print(text, flush=True)
    with open(os.path.join(args.out, "log.md"), "a", encoding="utf-8") as f:
        f.write(text + "\n")


def save(args, state):
    with open(os.path.join(args.out, "state.json"), "w", encoding="utf-8") as f:
        json.dump(state, f, indent=1)


# "first" is per civ, averaged over the Advanced civs. Half the civs are
# Advanced, or --count of them, so the share of games an Advanced civ won is
# first x the Advanced civs per game (a half by chance alone, with half).
# use_map sets these for the ladder's map.
CIVS = 8
COUNT = 0


def advanced_per_game():
    return min(COUNT, CIVS / 2) if COUNT > 0 else CIVS / 2


def won(r):
    return r["first"]["adv"] * advanced_per_game()


def chance():
    """The share of games an Advanced civ wins by chance alone."""
    return advanced_per_game() / CIVS


def summary(r):
    return (f"rank {r['rank']['diff']:+.2f}±{r['rank']['se']:.2f}, score {r['score']['diff']:+.0f}±{r['score']['se']:.0f}, "
            f"Advanced won {won(r):.0%} of games ({chance():.0%} by chance)")


class Monitor:
    """Daemon thread: heartbeat.json every few seconds, dashboard.html every `every` seconds (0 = off)."""

    def __init__(self, out, every):
        self.out, self.every, self.started = out, every, time.time()
        self.stop = threading.Event()
        self.thread = threading.Thread(target=self.loop, daemon=True)

    def beat(self, **extra):
        hb = {"pid": os.getpid(), "started": self.started, "beat": time.time(), **extra}
        tmp = os.path.join(self.out, "heartbeat.json.tmp")
        with open(tmp, "w", encoding="utf-8") as f:
            json.dump(hb, f)
        os.replace(tmp, os.path.join(self.out, "heartbeat.json"))

    def rebuild(self):
        try:
            import dashboard
            dashboard.write_dashboard(self.out)
        except Exception as e:  # a dashboard fault never stops training
            print(f"dashboard rebuild failed: {e!r}", flush=True)

    def loop(self):
        last = 0.0
        while True:
            try:
                self.beat()
            except OSError as e:
                print(f"heartbeat failed: {e!r}", flush=True)
            if self.every > 0 and time.time() - last >= self.every:
                self.rebuild()
                last = time.time()
            if self.stop.wait(min(15, self.every) if self.every > 0 else 15):
                return

    def start(self):
        self.beat()
        self.thread.start()

    def finish(self, error=None):
        self.stop.set()
        self.thread.join(5)
        self.beat(finished=time.time(), **({"error": error} if error else {}))
        if self.every > 0:
            self.rebuild()


def progress(args, state, name, rnd, kept):
    """One line per round and exam, for the daily report."""
    exams = state.get("exams", [])
    latest = f"{won(exams[-1]['result']):.0%}" if exams else "none yet"
    line = (f"{time.strftime('%Y-%m-%d %H:%M')} | rung {name} | cycle {state['cycle']} | round {rnd} | "
            f"kept {kept or 'none'} | latest exam win rate {latest}")
    with open(os.path.join(args.out, "progress.txt"), "a", encoding="utf-8", newline="\n") as f:
        f.write(line + "\n")


def read_ideas(args, knobs, tried):
    """Idea-queue lines not yet tried this cycle, as (label, spec, knobs). Re-read every round."""
    path = os.path.join(args.out, "ideas.txt")
    if not os.path.exists(path):
        return []
    found = []
    for line in open(path, encoding="utf-8"):
        text = line.split("#", 1)[0].strip()
        if not text:
            continue
        try:
            parsed = {}
            for part in text.split(","):
                k, v = part.split("=", 1)
                k = k.strip()
                if k not in DEFAULTS:
                    raise ValueError(f"unknown knob {k}")
                parsed[k] = float(v)
            if any(float(knobs[k]) != v for k, v in parsed.items()):
                canon = ",".join(f"{k}={v:g}" for k, v in sorted(parsed.items()))
                if canon not in tried and all(canon != f[1] for f in found):
                    found.append(("idea-" + canon.replace(",", "+"), canon,
                                  {**knobs, **{k: int(v) if k in BEHAVIOURS else v for k, v in parsed.items()}}))
                continue
            canon = text  # already what the knobs are: nothing to try
        except ValueError as e:
            canon = text
            if text not in tried:
                note(args, f"- idea ignored ({e}): {text}")
        if canon not in tried:
            tried.append(canon)
    return found


STEER_WORST = 3
RECHECKS_PER_CYCLE = 2


def exam_summary(result):
    """An exam's parsed rows in the shape benchmarks.gaps reads."""
    return [{"measure": m, "trialMean": r["adv"], "othersMean": r["std"], "difference": r["diff"], "standardError": r["se"]}
            for m, r in result.items()]


def ledger_entries(args):
    """The kept history first, then this run's own ledger."""
    return ledger.load(benchmarks.LEDGER_DEFAULT) + ledger.load(os.path.join(args.out, "ledger.jsonl"))


def steer_order(plan, entries, exam):
    """plan: [(label, knobs)] in today's order -> (plan with the candidates that helped a worst measure first, one log line)."""
    worst = [g for g in benchmarks.gaps(exam_summary(exam["result"]), {})][:STEER_WORST]
    if not worst:
        return plan, "- order: default (Advanced is level with Standard on every measure of the latest exam)"
    names = [g[1] for g in worst]
    helped = {}
    for e in entries:
        label = e.get("change", {}).get("label")
        for m in names:
            if label and e.get("verdict", {}).get(m) == "helps" and m not in helped.setdefault(label, []):
                helped[label].append(m)
    plan = sorted(plan, key=lambda p: p[0] not in helped)  # stable: the rest keep today's order
    first = [f"{l} (helped {'/'.join(helped[l])})" for l, _ in plan if l in helped]
    gaps_text = ", ".join(f"{g[1]} {g[0]:.1f} SE" for g in worst)
    return plan, (f"- order: steered by the latest exam's worst gaps ({gaps_text}); first: "
                  + (", ".join(first) if first else "none, no ledger entry helped them; today's order"))


def spec_dict(text):
    out = {}
    for part in text.split(","):
        if "=" in part:
            k, v = part.split("=", 1)
            out[k.strip()] = float(v)
    return out


def reversal(change):
    """The idea-queue spec that undoes a ledger change: each knob it moved goes back to its value before."""
    before, after = spec_dict(change.get("before", "")), spec_dict(change.get("after", ""))
    parts = []
    for k in sorted(set(before) | set(after)):
        b, a = before.get(k, DEFAULTS.get(k)), after.get(k, DEFAULTS.get(k))
        if k in DEFAULTS and b != a:
            parts.append(f"{k}={b:g}")
    return ",".join(parts)


def stale_entries(entries, exam, difficulty):
    """Kept entries whose Advanced/Standard ratio, on a measure they moved, is now more than 2 exam SE from
    the ratio the change itself produced (the entry's measures adv / std; basis is the ratio before it).

    -> [(entry, [(measure, produced ratio, ratio now, shift in SE)])], largest shift first. Entries from another
    difficulty and ones already marked stale are left out.
    """
    marked = {e["recheck_of"] for e in entries if e.get("recheck_of") and "reason" in e}
    out = []
    for e in entries:
        if "recheck_of" in e or not e.get("kept") or ledger.entry_id(e) in marked or e.get("difficulty") != difficulty:
            continue
        shifts = []
        for m, v in e.get("verdict", {}).items():
            r, got = exam.get(m), e.get("measures", {}).get(m)
            if v not in ("helps", "harms") or not got or not got.get("std") or r is None or not r["std"] or r["se"] <= 0:
                continue
            basis = got["adv"] / got["std"]
            ratio, se = r["adv"] / r["std"], r["se"] / abs(r["std"])
            if abs(ratio - basis) > ledger.SIGMAS * se:
                shifts.append((m, basis, ratio, abs(ratio - basis) / se))
        if shifts:
            out.append((e, shifts))
    return sorted(out, key=lambda t: -max(s[3] for s in t[1]))


def self_test():
    def entry(**kw):
        return dict({"date": "2026-10-30", "commit": "abc", "change": {"label": "X"}, "difficulty": 2, "kept": True,
                     "verdict": {"techs": "helps", "gold": "helps"}, "basis": {"techs": 1.0, "gold": 1.0},
                     "measures": {"techs": {"adv": 12.0, "std": 10.0}, "gold": {"adv": 5.0, "std": 0}}}, **kw)
    at = lambda adv: {"techs": {"adv": adv, "std": 10.0, "se": 0.5}, "gold": {"adv": 5.0, "std": 0, "se": 1.0}}
    e = entry()
    assert stale_entries([e], at(12.0), 2) == []  # (a) at the post-change ratio: not stale
    got = stale_entries([e], at(10.0), 2)  # (b) back at basis: stale; gold (std 0) skipped
    assert len(got) == 1 and [s[0] for s in got[0][1]] == ["techs"], got
    marked = {"recheck_of": ledger.entry_id(e), "reason": "x"}
    assert stale_entries([e, marked], at(10.0), 2) == []  # (c) already marked
    assert stale_entries([e], at(10.0), 1) == []  # other difficulty
    map_self_test()
    print("train self-test passed")


def map_self_test():
    import tempfile
    from types import SimpleNamespace

    def opts(**kw):
        return SimpleNamespace(**{**dict(world=None, landform=None, water=None, civs=None, count=None, profile=None,
                                         games=16, turns=200), **kw})

    knobs = dict(DEFAULTS)
    # No map options: today's trial settings, 8 civs, half Advanced, no map variables.
    a = opts()
    a.map = map_settings(a)
    assert a.map == {}
    use_map(a.map)
    env = trial_env(a, knobs, 2, 1000, "x/t.txt", True)
    assert env["C7_TRIAL_CIVS"] == "8" and env["C7_ADVANCED_PROFILE"] == "none" and env["C7_ADVANCED_TUNING"] == ""
    assert not {"C7_TRIAL_WORLD", "C7_TRIAL_LANDFORM", "C7_TRIAL_WATER", "C7_TRIAL_COUNT"} & set(env), env
    r = {"first": {"adv": 0.125}}
    assert won(r) == 0.5 and chance() == 0.5
    use_map(None)  # a state.json from before map options
    assert CIVS == 8 and COUNT == 0
    # A world size brings its Civ3 default civ count; --civs beats it.
    a = opts(world="huge", landform="pangaea", water=80)
    a.map = map_settings(a)
    assert a.map == {"world": "Huge", "landform": "Pangaea", "water": 80, "civs": 16}, a.map
    use_map(a.map)
    env = trial_env(a, knobs, 2, 1000, "x/t.txt", True)
    assert (env["C7_TRIAL_WORLD"], env["C7_TRIAL_LANDFORM"], env["C7_TRIAL_WATER"], env["C7_TRIAL_CIVS"]) == ("Huge", "Pangaea", "80", "16")
    assert won(r) == 1.0  # 8 Advanced civs
    assert map_settings(opts(world="Tiny", civs=6)) == {"world": "Tiny", "civs": 6}
    # --count 1: one Advanced civ a game, whose "first" is the win rate.
    a = opts(world="Small", count=1)
    a.map = map_settings(a)
    use_map(a.map)
    assert trial_env(a, knobs, 2, 1000, "x/t.txt", False)["C7_TRIAL_COUNT"] == "1"
    assert won({"first": {"adv": 0.4}}) == 0.4 and abs(chance() - 1 / 6) < 1e-9
    # A profile key gives the map unless the options say otherwise.
    a = opts(profile="70/archipelago", world="Large")
    assert map_settings(a) == {"profile": "Archipelago/70", "landform": "Archipelago", "water": 70, "world": "Large", "civs": 12}
    for bad in ("Pangaea/Continents", "Gigantic", "65", "Huge//Pangaea"):
        try:
            parse_key(bad)
            raise AssertionError(bad)
        except ValueError:
            pass
    assert canonical_key("70/pangaea/HUGE") == "Huge/Pangaea/70"
    use_map({})
    # Export: the differences from the defaults, replacing the key's profile
    # whatever its spelling, other profiles kept; a fresh ladder starts from it.
    with tempfile.TemporaryDirectory() as d:
        path = os.path.join(d, "profiles.json")
        assert profile_knobs(path, "Pangaea") == DEFAULTS  # no file yet
        with open(path, "w", encoding="utf-8") as f:
            json.dump({"profiles": {"pangaea": {"FocusPoints": 5}, "Huge": {"fast-expand": 1}}}, f)
        assert profile_knobs(path, "PANGAEA") == {**DEFAULTS, "FocusPoints": 5}
        key, diff = export_profile(path, "Pangaea", {**DEFAULTS, "FocusPoints": 20, "BehindInTechShare": 0.85, "fast-expand": 1})
        assert key == "Pangaea" and diff == {"BehindInTechShare": 0.85, "FocusPoints": 20, "fast-expand": 1}, diff
        doc = json.load(open(path, encoding="utf-8"))
        assert doc["profiles"] == {"Huge": {"fast-expand": 1}, "Pangaea": diff}, doc
        assert b"\r" not in open(path, "rb").read()
        assert export_profile(path, "Huge", DEFAULTS)[1] == {}  # tuned back to the defaults
        assert profile_knobs(path, "Pangaea")["fast-expand"] == 1 and isinstance(profile_knobs(path, "Pangaea")["fast-expand"], int)


def queue_rechecks(args, state, exam, difficulty, key):
    """After an exam: mark stale entries in the ledger and put their reversals in ideas.txt (at most 2 per cycle)."""
    if args.no_steer:
        return
    try:
        room = RECHECKS_PER_CYCLE - state.setdefault("rechecks", {}).get(key, 0)
        for e, shifts in stale_entries(ledger_entries(args), exam, difficulty)[:max(room, 0)]:
            rev = reversal(e.get("change", {}))
            if not rev:
                continue
            eid = ledger.entry_id(e)
            why = "; ".join(f"{m} ratio {b:.2f} -> {r:.2f} ({s:.1f} SE)" for m, b, r, s in shifts)
            ledger.append(os.path.join(args.out, "ledger.jsonl"),
                          {"date": time.strftime("%Y-%m-%d"), "commit": ledger.git_commit(), "ticket": args.ticket,
                           "recheck_of": eid, "reason": f"ratios shifted past 2 exam SE since the entry: {why}"})
            with open(os.path.join(args.out, "ideas.txt"), "a", encoding="utf-8", newline="\n") as f:
                f.write(f"{rev}  # recheck {eid}\n")
            state["rechecks"][key] = state["rechecks"].get(key, 0) + 1
            note(args, f"- recheck queued for {eid}: {why}; reversal {rev}")
    except Exception as ex:  # noqa: BLE001
        note(args, f"- recheck not queued: {ex}")


def recheck_ids(args):
    """{idea spec: ledger entry id} for the ideas.txt lines marked '# recheck <id>'."""
    path = os.path.join(args.out, "ideas.txt")
    out = {}
    if os.path.exists(path):
        for line in open(path, encoding="utf-8"):
            text, _, comment = line.partition("#")
            m = re.match(r"\s*recheck\s+(\S+)", comment)
            if m:
                try:
                    out[",".join(f"{k}={v:g}" for k, v in sorted(spec_dict(text).items()))] = m[1]
                except ValueError:
                    pass
    return out


def last_exam(state, name, cycle, knobs):
    for e in reversed(state.get("exams", [])):
        if e["rung"] == name and e.get("cycle", 0) == cycle and e["knobs"] == knobs:
            return e
    return None


def main():
    if "--self-test" in sys.argv:
        return self_test()
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--resume", action="store_true")
    ap.add_argument("--games", type=int, default=16)
    ap.add_argument("--turns", type=int, default=200)
    ap.add_argument("--parallel", type=int, default=8)
    ap.add_argument("--train-seed", type=int, default=1000)
    ap.add_argument("--holdout-seed", type=int, default=7000)
    ap.add_argument("--exam-seed", type=int, default=7500, help="fixed across cycles, so exams stay comparable")
    ap.add_argument("--nonstop", action="store_true",
                    help=f"a failed exam starts a new cycle (new seeds) and exits with code {NONSTOP_EXIT}, never stops")
    ap.add_argument("--targets", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "doc", "suede-benchmarks.md"),
                    help="Suede's figures for the benchmarks report (see benchmarks.py)")
    ap.add_argument("--ticket", default="", help="ticket id recorded in ledger.jsonl entries")
    ap.add_argument("--no-steer", action="store_true",
                    help="today's behaviour: candidates in fixed order, no stale-entry re-checks")
    ap.add_argument("--rounds", type=int, default=6, help="most search rounds per rung and cycle")
    ap.add_argument("--dashboard-every", type=int, default=60, help="seconds between dashboard rebuilds, 0 = off")
    ap.add_argument("--world", help="Tiny, Small, Standard, Large or Huge (C7_TRIAL_WORLD); default: the trial's 100x100 map")
    ap.add_argument("--landform", help="Pangaea, Continents or Archipelago (C7_TRIAL_LANDFORM); default Continents")
    ap.add_argument("--water", type=int, help="60, 70 or 80 (C7_TRIAL_WATER); default 70")
    ap.add_argument("--civs", type=int, help="civs per game; default the world size's Civ3 default (4, 6, 8, 12, 16), else 8")
    ap.add_argument("--count", type=int, help="Advanced civs per game (C7_TRIAL_COUNT); default half the civs")
    ap.add_argument("--profile", help="the map profile this ladder tunes, e.g. Huge/Pangaea/70: its map unless given, "
                                      "and a fresh ladder starts from its values")
    ap.add_argument("--export-profile", metavar="KEY",
                    help="write the ladder's kept knobs (differences from the defaults) to the profiles file as KEY, and exit")
    ap.add_argument("--profiles-file", default=PROFILES_FILE, help="the profiles file (default C7/Lua/civ3/advanced-ai-profiles.json)")
    args = ap.parse_args()
    if args.export_profile:
        return export_command(args)
    args.map = map_settings(args)
    os.makedirs(os.path.join(args.out, "runs"), exist_ok=True)
    monitor = Monitor(args.out, args.dashboard_every)
    monitor.start()
    error = None
    try:
        ladder(args)
    except SystemExit as e:
        if e.code != NONSTOP_EXIT:  # a cycle end is not an error
            error = repr(e)
        raise
    except BaseException as e:
        error = repr(e)
        raise
    finally:
        monitor.finish(error)


def export_command(args):
    state_path = os.path.join(args.out, "state.json")
    if not os.path.exists(state_path):
        sys.exit(f"no {state_path}: nothing to export")
    with open(state_path, encoding="utf-8") as f:
        state = json.load(f)
    try:
        key, knobs = export_profile(args.profiles_file, args.export_profile, state["knobs"])
    except ValueError as e:
        sys.exit(str(e))
    m = state.get("map") or {}
    trained = {f: m[f] for f in ("world", "landform", "water") if f in m}
    if any(parse_key(key).get(f) != v for f, v in trained.items()) or not trained:
        print(f"note: the ladder in {args.out} trained on {map_text(m)}, not exactly {key}")
    print(f"profile {key} written to {args.profiles_file}: {', '.join(f'{k}={v:g}' for k, v in knobs.items()) or 'the defaults'}")


def ladder(args):

    state_path = os.path.join(args.out, "state.json")
    if args.resume and os.path.exists(state_path):
        state = json.load(open(state_path, encoding="utf-8"))
        for k, v in DEFAULTS.items():  # knobs added since the state was saved
            state["knobs"].setdefault(k, v)
        # A resumed ladder keeps the map it started with.
        if args.map and args.map != state.get("map"):
            note(args, f"- the map options are ignored: this ladder plays on {map_text(state.get('map'))}")
        args.map = state.get("map") or {}
    else:
        knobs = profile_knobs(args.profiles_file, args.map["profile"]) if args.map.get("profile") else dict(DEFAULTS)
        state = {"knobs": knobs, "rung": 0, "round": 0, "history": []}
        # Only a ladder on another map says so: today's state.json is unchanged.
        if args.map:
            state["map"] = args.map
        open(os.path.join(args.out, "log.md"), "w", encoding="utf-8").write("# Advanced AI training\n\n")
        if args.map:
            note(args, f"Map: {map_text(args.map)}; starting knobs: {spec(knobs) or 'defaults'}\n")
    use_map(args.map)
    state.setdefault("cycle", 0)
    if subprocess.run(["dotnet", "build", "EngineTests"], capture_output=True).returncode != 0:
        sys.exit("build failed")

    pool = ThreadPoolExecutor(args.parallel)
    while state["rung"] < len(RUNGS):
        name, difficulty, bar = RUNGS[state["rung"]]
        cycle = state["cycle"]
        knobs = state["knobs"]
        # Cycle 0 keeps the first ladder's file tags and seeds.
        cyc = f"-c{cycle}" if cycle else ""
        train_seed = args.train_seed + 100 * cycle
        holdout_seed = args.holdout_seed + 100 * cycle
        tried_ideas = state.setdefault("ideas_tried", {}).setdefault(f"{name}-c{cycle}", [])
        if cycle:
            note(args, f"\n# {name}, cycle {cycle}: training seeds {train_seed}, held-out seeds {holdout_seed}")
        while True:
            rnd = state["round"]
            if rnd < args.rounds and not state.get("settled"):
                prefix = f"{name}{cyc}-r{rnd}"
                note(args, f"\n## {name}, cycle {cycle}, round {rnd} — knobs: {spec(knobs) or 'defaults'}")
                started = time.time()
                ideas = read_ideas(args, knobs, tried_ideas)
                plan = list(candidates(knobs)) + [(label, k) for label, _, k in ideas]
                if not args.no_steer:
                    if state.get("exams"):
                        plan, why = steer_order(plan, ledger_entries(args), state["exams"][-1])
                    else:
                        why = "- order: default (no exam yet)"
                    note(args, why)
                base = pool.submit(run_trial, args, knobs, difficulty, train_seed, f"{prefix}-base")
                tries = [(label, k, pool.submit(run_trial, args, k, difficulty, train_seed, f"{prefix}-{label}"))
                         for label, k in plan]
                idea_specs = {label: canon for label, canon, _ in ideas}
                rechecks = recheck_ids(args)
                base = base.result()
                note(args, f"- current (training seeds): {summary(base)}")
                scored = []
                for label, k, fut in tries:
                    r = fut.result()
                    scored.append((objective(r) - objective(base), label, k, r))
                scored.sort(key=lambda t: t[0], reverse=True)
                for gain, label, _, r in scored[:5]:
                    note(args, f"- {label}: gain {gain:+.2f} ({summary(r)})")
                for gain, label, _, r in scored:
                    if label.startswith("idea-") and not any(label == s[1] for s in scored[:5]):
                        note(args, f"- {label}: gain {gain:+.2f} ({summary(r)})")
                note(args, f"- ({len(scored)} changes tried in {(time.time() - started) / 60:.0f} min)")

                # Confirm the best few on held-out seeds; keep the first that holds.
                kept = None
                for gain, label, k, _ in scored[:3]:
                    if gain <= 0:
                        break
                    hb = run_trial(args, knobs, difficulty, holdout_seed, f"{prefix}-holdout-base")
                    hc = run_trial(args, k, difficulty, holdout_seed, f"{prefix}-holdout-{label}")
                    hgain = objective(hc) - objective(hb)
                    note(args, f"- held-out {label}: gain {hgain:+.2f} ({summary(hc)})")
                    record(args, prefix, label, knobs, k, difficulty, holdout_seed, hgain > 0,
                           rechecks.get(idea_specs.get(label)))
                    if hgain > 0:
                        kept = (label, k)
                        break
                tried_ideas.extend(c for _, c, _ in ideas)
                state["history"].append({"rung": name, "cycle": cycle, "round": rnd, "base": base,
                                         "tried": [(g, l, r) for g, l, _, r in scored], "kept": kept[0] if kept else None})
                state["round"] += 1
                if kept is None:
                    note(args, "- nothing held up on held-out seeds: the search has settled")
                    state["settled"] = True
                else:
                    knobs = state["knobs"] = kept[1]
                    note(args, f"- kept {kept[0]}")
                save(args, state)
                progress(args, state, name, rnd, kept[0] if kept else None)

            # The exam, on fixed held-out seeds: after every kept change, and
            # once the search settles or runs out of rounds.
            done = max(state["round"] - 1, 0)
            exam_needed = last_exam(state, name, cycle, knobs) is None
            if exam_needed:
                tag = f"{name}-c{cycle}-r{done}-exam"
                exam = run_trial(args, knobs, difficulty, args.exam_seed, tag, paired=False, detail=True)
                write_report(args, tag)
                passed = None if bar is None else won(exam) >= bar - 1e-9 and exam["rank"]["diff"] < -2 * exam["rank"]["se"]
                needs = "last rung" if bar is None else f"needs {bar:.0%} won"
                verdict = "" if passed is None else f" — {'PASSED' if passed else 'not passed'}"
                note(args, f"\n**{name} exam, cycle {cycle} round {done}** ({needs}): {summary(exam)}{verdict}")
                state.setdefault("exams", []).append({"rung": name, "cycle": cycle, "round": done, "knobs": dict(knobs),
                                                      "result": exam, "passed": passed})
                queue_rechecks(args, state, exam, difficulty, f"{name}-c{cycle}")
                save(args, state)
                last = state["history"][-1] if state["history"] else {}
                progress(args, state, name, f"{done} exam", last.get("kept") if last.get("rung") == name and last.get("cycle", 0) == cycle else None)
            e = last_exam(state, name, cycle, knobs)
            if e["passed"]:
                state["rung"] += 1
                state["round"] = 0
                state["settled"] = False
                save(args, state)
                break
            if state["round"] >= args.rounds or state.get("settled"):
                if bar is None and not args.nonstop:
                    break
                if args.nonstop:
                    state["cycle"] += 1
                    state["round"] = 0
                    state["settled"] = False
                    save(args, state)
                    note(args, f"\nCycle {cycle} of {name} is over without passing the exam: cycle {state['cycle']} starts on new seeds, with the latest code.")
                    sys.exit(NONSTOP_EXIT)
                note(args, "Stopping: the Advanced AI needs better play, not just better knobs, to pass this rung.")
                save(args, state)
                return
    note(args, "\nThe ladder is done.")


if __name__ == "__main__":
    main()
