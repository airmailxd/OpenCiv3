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

Results: <dir>/state.json (the knobs and every run) and <dir>/log.md.
"""
import argparse
import json
import os
import re
import subprocess
import sys
import time
from concurrent.futures import ThreadPoolExecutor

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
}
BEHAVIOURS = ["wonder-timing", "wonder-value", "war-production", "war-no-peace",
              "leader-hurry-best", "lean-military", "no-ai-luxury"]
DEFAULTS = {"FocusPoints": 15, "TargetStrengthRatio": 1.75, "ThreatStrengthRatio": 1.5,
            "BehindInTechShare": 0.9, "ExpandSettlers": 85, "ExpandAggression": -1,
            "BuildScience": 65, "BuildProduction": 65, "WinningOffense": 80,
            "PreyAggression": 1, "PlanEveryTurns": 5, **{b: 0 for b in BEHAVIOURS}}
# Each rung: its name, the Standard AIs' difficulty (an index into the
# difficulties) and the share of exam games an Advanced civ must win to
# move up (the owner's bars, 2026-10-29). Deity's exam is only reported.
RUNGS = [("Regent", 2, 1.00), ("Monarch", 3, 0.90), ("Emperor", 4, 0.75), ("Demigod", 5, 0.50), ("Deity", 6, None)]

LINE = re.compile(r"^(\w+)\s+([-\d.]+) vs\s+([-\d.]+)\s+([-+\d.]+) \+- ([\d.]+)")


def spec(knobs):
    return ",".join(f"{k}={v:g}" for k, v in sorted(knobs.items()) if v != DEFAULTS[k])


# Paired trials (the default) cancel map luck for comparing knobs; the exam
# is unpaired, so its "first" column is the Advanced civs' own wins.
def run_trial(args, knobs, difficulty, seed, tag, paired=True):
    out = os.path.join(args.out, "runs", f"{tag}.txt")
    if os.path.exists(out) and "total ms" in open(out, encoding="utf-8").read():
        return parse(out)
    env = dict(os.environ,
               C7_TRIAL_OUT=out, C7_TRIAL_EXPERIMENTS="", C7_TRIAL_ADVANCED="1", C7_TRIAL_PAIRED="1" if paired else "0",
               C7_TRIAL_GAMES=str(args.games), C7_TRIAL_DIFFICULTY=str(difficulty), C7_TRIAL_SEED=str(seed),
               C7_TRIAL_TURNS=str(args.turns), C7_TRIAL_CIVS=str(CIVS), C7_ADVANCED_TUNING=spec(knobs))
    with open(out + ".log", "w", encoding="utf-8") as log:
        subprocess.run(["dotnet", "test", "EngineTests", "--no-build", "--filter", "FullyQualifiedName~AiTrialTest"],
                       env=env, stdout=log, stderr=subprocess.STDOUT, check=False)
    if not os.path.exists(out):
        raise RuntimeError(f"trial {tag} wrote no results; see {out}.log")
    return parse(out)


def parse(path):
    r = {}
    for line in open(path, encoding="utf-8"):
        m = LINE.match(line)
        if m:
            r[m[1]] = {"adv": float(m[2]), "std": float(m[3]), "diff": float(m[4]), "se": float(m[5])}
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


# "first" is per civ; half the civs are Advanced, so the share of games an
# Advanced civ won is first x civs / 2 (a half by chance alone).
CIVS = 8


def won(r):
    return r["first"]["adv"] * CIVS / 2


def summary(r):
    return (f"rank {r['rank']['diff']:+.2f}±{r['rank']['se']:.2f}, score {r['score']['diff']:+.0f}±{r['score']['se']:.0f}, "
            f"Advanced won {won(r):.0%} of games (50% by chance)")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--resume", action="store_true")
    ap.add_argument("--games", type=int, default=16)
    ap.add_argument("--turns", type=int, default=200)
    ap.add_argument("--parallel", type=int, default=8)
    ap.add_argument("--train-seed", type=int, default=1000)
    ap.add_argument("--holdout-seed", type=int, default=7000)
    ap.add_argument("--rounds", type=int, default=6, help="most search rounds per rung")
    args = ap.parse_args()
    os.makedirs(os.path.join(args.out, "runs"), exist_ok=True)

    state_path = os.path.join(args.out, "state.json")
    if args.resume and os.path.exists(state_path):
        state = json.load(open(state_path, encoding="utf-8"))
    else:
        state = {"knobs": dict(DEFAULTS), "rung": 0, "round": 0, "history": []}
        open(os.path.join(args.out, "log.md"), "w", encoding="utf-8").write("# Advanced AI training\n\n")
    if subprocess.run(["dotnet", "build", "EngineTests"], capture_output=True).returncode != 0:
        sys.exit("build failed")

    pool = ThreadPoolExecutor(args.parallel)
    while state["rung"] < len(RUNGS):
        name, difficulty, bar = RUNGS[state["rung"]]
        knobs = state["knobs"]
        while state["round"] < args.rounds:
            rnd = state["round"]
            prefix = f"{name}-r{rnd}"
            note(args, f"\n## {name}, round {rnd} — knobs: {spec(knobs) or 'defaults'}")
            started = time.time()
            base = pool.submit(run_trial, args, knobs, difficulty, args.train_seed, f"{prefix}-base")
            tries = [(label, k, pool.submit(run_trial, args, k, difficulty, args.train_seed, f"{prefix}-{label}"))
                     for label, k in candidates(knobs)]
            base = base.result()
            note(args, f"- current (training seeds): {summary(base)}")
            scored = []
            for label, k, fut in tries:
                r = fut.result()
                scored.append((objective(r) - objective(base), label, k, r))
            scored.sort(key=lambda t: t[0], reverse=True)
            for gain, label, _, r in scored[:5]:
                note(args, f"- {label}: gain {gain:+.2f} ({summary(r)})")
            note(args, f"- ({len(scored)} changes tried in {(time.time() - started) / 60:.0f} min)")

            # Confirm the best few on held-out seeds; keep the first that holds.
            kept = None
            for gain, label, k, _ in scored[:3]:
                if gain <= 0:
                    break
                hb = run_trial(args, knobs, difficulty, args.holdout_seed, f"{prefix}-holdout-base")
                hc = run_trial(args, k, difficulty, args.holdout_seed, f"{prefix}-holdout-{label}")
                hgain = objective(hc) - objective(hb)
                note(args, f"- held-out {label}: gain {hgain:+.2f} ({summary(hc)})")
                if hgain > 0:
                    kept = (label, k)
                    break
            state["history"].append({"rung": name, "round": rnd, "base": base,
                                     "tried": [(g, l, r) for g, l, _, r in scored], "kept": kept[0] if kept else None})
            state["round"] += 1
            if kept is None:
                note(args, "- nothing held up on held-out seeds: the search has settled")
                save(args, state)
                break
            knobs = state["knobs"] = kept[1]
            note(args, f"- kept {kept[0]}")
            save(args, state)

        # The rung's exam, on held-out seeds.
        exam = run_trial(args, knobs, difficulty, args.holdout_seed + 500, f"{name}-exam", paired=False)
        if bar is None:
            note(args, f"\n**{name} exam** (last rung): {summary(exam)}")
            state.setdefault("exams", []).append({"rung": name, "knobs": dict(knobs), "result": exam, "passed": None})
            save(args, state)
            break
        passed = won(exam) >= bar - 1e-9 and exam["rank"]["diff"] < -2 * exam["rank"]["se"]
        note(args, f"\n**{name} exam** (needs {bar:.0%} won): {summary(exam)} — {'PASSED' if passed else 'not passed'}")
        state.setdefault("exams", []).append({"rung": name, "knobs": dict(knobs), "result": exam, "passed": passed})
        if not passed:
            note(args, "Stopping: the Advanced AI needs better play, not just better knobs, to pass this rung.")
            save(args, state)
            return
        state["rung"] += 1
        state["round"] = 0
        save(args, state)
    note(args, "\nThe ladder is done.")


if __name__ == "__main__":
    main()
