"""Civ3's default calendar (540 turns, turn 0 = 4000 BC), from
https://forums.civfanatics.com/threads/time-options-time-scale.456771/ and checked against a save
(COTM 176, I.Larkin: turn 126 = 30 BC).

turns   0-25  50 years (to 2750 BC)    turns  90-140 20 years (to 250 AD)
turns  25-50  40 years (to 1750 BC)    turns 140-240 10 years (to 1250 AD)
turns  50-90  25 years (to 750 BC)     turns 240-340  5 years (to 1750 AD)
                                       turns 340-440  2 years, 440-540 1 year (to 2050 AD)
"""
import re

STEPS = [(25, 50), (25, 40), (40, 25), (50, 20), (100, 10), (100, 5), (100, 2), (100, 1)]


def year_of_turn(turn):
    y, t = -4000, 0
    for n, step in STEPS:
        k = min(n, turn - t)
        if k <= 0:
            break
        y += k * step
        t += k
    return y


def turn_of_year(year):
    """The turn whose date is `year` (negative = BC), or the turn just after it."""
    y, t = -4000, 0
    for n, step in STEPS:
        end = y + n * step
        if year <= end:
            return t + -(-(year - y) // step)
        y, t = end, t + n
    return t


def parse_date(s):
    m = re.match(r"\s*(\d+)\s*(BC|AD)", s or "", re.I)
    if not m:
        return None
    v = int(m.group(1))
    return -v if m.group(2).upper() == "BC" else v


def label(turn):
    y = year_of_turn(turn)
    return f"{-y} BC" if y < 0 else f"{y} AD"


if __name__ == "__main__":
    assert turn_of_year(-30) == 126, turn_of_year(-30)
    assert year_of_turn(50) == -1750 and year_of_turn(100) == -550 and year_of_turn(150) == 350 and year_of_turn(200) == 850
    for t in (50, 75, 100, 125, 150, 175, 200, 250):
        print(t, label(t))
