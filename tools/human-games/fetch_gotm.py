"""Fetch CivFanatics Conquests COTM game pages and result tables into one JSON file.

Usage: python -I fetch_gotm.py <cache-dir> <out.json> [first] [last]

<cache-dir> holds the downloaded HTML (untrusted data; only parsed with regexes here).
Game N is month 30000+N on gotm.civfanatics.net. Requests are paced (1 s apart) and cached.
"""
import html
import json
import os
import re
import sys
import time
import urllib.request

BASE = "https://gotm.civfanatics.net"
UA = {"User-Agent": "Mozilla/5.0 (OpenCiv3 human-benchmark research; one request per second)"}


def fetch(url, path):
    if os.path.exists(path) and os.path.getsize(path) > 0:
        return open(path, encoding="utf-8", errors="replace").read()
    time.sleep(1.0)
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=60) as r:
        data = r.read()
    with open(path, "wb") as f:
        f.write(data)
    return data.decode("utf-8", errors="replace")


def text(s):
    s = re.sub(r"(?s)<script.*?</script>|<style.*?</style>|<!--.*?-->", "", s)
    s = re.sub(r"(?i)<br\s*/?>", "\n", s)
    s = re.sub(r"<[^>]+>", " ", s)
    s = html.unescape(s)
    return re.sub(r"[ \t\r\f\v]+", " ", s).strip()


def details(page):
    t = re.sub(r"\s+:", ":", text(page))
    t = re.sub(r"\s+", " ", t)
    out = {}
    m = re.search(r"Civ ?3 Conquests COTM (\d+) ?\(([^,]+), (\w+) level\)", t)
    if m:
        out["title"] = m.group(0)
        out["titleCiv"], out["titleDifficulty"] = m.group(2), m.group(3)
    for key, pat in [
        ("civ", r"Civilization: ([^\n]+?) Unique Unit"),
        ("uniqueUnit", r"Unique Unit: (.+?) Difficulty:"),
        ("difficulty", r"Difficulty: (\w+)"),
        ("mapSize", r"Map size: (.+?) Map topology:"),
        ("worldSize", r"World size: (\w+)"),
        ("landmass", r"Landmass: (\w+)"),
        ("topology", r"Map topology: (.+?) Environment:"),
        ("environment", r"Environment: (.+?) Geology:"),
        ("geology", r"Geology: (.+?) Rivals:"),
        ("rivals", r"Rivals: (.+?) Barbarians:"),
        ("barbarians", r"Barbarians: (\w+)"),
        ("started", r"Game started: ([^,]+), finished"),
        ("finished", r"finished: (\d+ \w+ \d{4})"),
        ("version", r"require version ([\d.]+\w?) of Civilization III ?(\w*)"),
    ]:
        m = re.search(pat, t, re.S)
        if m:
            out[key] = m.group(1).strip()
    m = re.search(r"(Game info|Scope of the Game)(.{0,2500})", t, re.S)
    if m:
        out["blurb"] = re.sub(r"\s+", " ", m.group(2)).strip()
    for k, alt in (("difficulty", "titleDifficulty"), ("mapSize", "worldSize"), ("topology", "landmass")):
        if not out.get(k):
            out[k] = out.get(alt)
    m = re.search(r"Rule Changes(.{0,1200}?)(As always|New Players)", t, re.S)
    if not m:
        m = re.search(r"(Civ3 COTM .{0,1500}?)(Scope of the Game|Game info)", t, re.S)
    if m:
        out["ruleChanges"] = re.sub(r"\s+", " ", m.group(1)).strip()
    return out


def results(page):
    rows = []
    part = page.split("Game Results", 1)[-1]
    for tr in re.findall(r"(?s)<tr style='height:32px[^>]*>(.*?)</tr>", part):
        cells = re.findall(r"(?s)<td[^>]*>(.*?)</td>", tr)
        if len(cells) < 9 or text(cells[0]) == "Rank":
            continue
        save = re.search(r"download\.php\?file=([^'\"]+)", tr)
        cls = re.search(r"<td class='(\w*)'; style='text-align:left;'>", tr)
        rows.append({
            "rank": text(cells[0]),
            "player": text(cells[1]),
            "score": text(cells[2]),
            "date": text(cells[3]),
            "condition": text(cells[4]),
            "outcome": text(cells[5]),
            "jason": text(cells[6]),
            "time": text(cells[8]),
            "class": cls.group(1) if cls else "",
            "save": save.group(1) if save else None,
        })
    return rows


def main():
    cache, out = sys.argv[1], sys.argv[2]
    first = int(sys.argv[3]) if len(sys.argv) > 3 else 1
    last = int(sys.argv[4]) if len(sys.argv) > 4 else 176
    os.makedirs(cache, exist_ok=True)
    games = []
    for n in range(first, last + 1):
        month = 30000 + n
        try:
            d = details(fetch(f"{BASE}/game-info/game-details.php?month={month}", os.path.join(cache, f"det_{month}.html")))
            r = results(fetch(f"{BASE}/results/index.php?month={month}", os.path.join(cache, f"res_{month}.html")))
        except Exception as e:  # keep going; note the failure
            games.append({"cotm": n, "error": str(e)})
            continue
        games.append({"cotm": n, "month": month, **d, "results": r})
        print(n, d.get("difficulty"), d.get("mapSize"), d.get("topology"), len(r), file=sys.stderr)
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        json.dump(games, f, indent=1)


if __name__ == "__main__":
    main()
