"""Download a sample of COTM final saves listed in the JSON made by fetch_gotm.py.

Usage: python -I download_saves.py <cotm_all.json> <save-root> <per-game> <cotm numbers...>

For each game: the open-class entries with a save, picking ranks spread over the field (1st, 2nd, then
evenly down to the median). Each save goes into its own new directory <save-root>/cotmNNN_<player>/.
The saves are untrusted data: this script only writes them to disk; GotmReader parses them.
"""
import json
import os
import re
import sys
import time
import urllib.parse
import urllib.request

UA = {"User-Agent": "Mozilla/5.0 (OpenCiv3 human-benchmark research; one request per second)"}


def pick(rows, k):
    rows = [r for r in rows if r.get("save") and r.get("class", "") == "" and r.get("outcome") == "Victory"]
    if len(rows) <= k:
        return rows
    half = max(k, (len(rows) + 1) // 2)  # spread over the top half of the field
    idx = sorted({round(i * (half - 1) / (k - 1)) for i in range(k)})
    return [rows[i] for i in idx]


def main():
    games = {g["cotm"]: g for g in json.load(open(sys.argv[1], encoding="utf-8"))}
    root, k = sys.argv[2], int(sys.argv[3])
    manifest = []
    for n in map(int, sys.argv[4:]):
        g = games[n]
        for r in pick(g["results"], k):
            safe = re.sub(r"[^A-Za-z0-9_.-]", "_", r["player"])[:40]
            d = os.path.join(root, f"cotm{n:03d}_{safe}")
            out = os.path.join(d, f"cotm{n:03d}.SAV")
            if not os.path.exists(out):
                if os.path.isdir(d) and not os.listdir(d):
                    os.rmdir(d)  # left empty by a failed download
                time.sleep(1.0)
                url = "https://gotm.civfanatics.net/download.php?file=" + urllib.parse.quote(r["save"])
                try:
                    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=120) as resp:
                        data = resp.read()
                except Exception as e:
                    print(n, r["rank"], r["player"], "FAILED", e, file=sys.stderr)
                    continue
                os.makedirs(d, exist_ok=False)
                with open(out, "wb") as f:
                    f.write(data)
            manifest.append({"cotm": n, "player": r["player"], "rank": r["rank"], "date": r["date"],
                             "condition": r["condition"], "save": r["save"], "path": out,
                             "fieldSize": len(g["results"])})
            print(n, r["rank"], r["player"], os.path.getsize(out), file=sys.stderr)
    json.dump(manifest, open(os.path.join(root, "manifest.json"), "w", encoding="utf-8"), indent=1)


if __name__ == "__main__":
    main()
