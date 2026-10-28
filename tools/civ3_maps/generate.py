"""Makes Civilization III maps in bulk, for comparing OpenCiv3's map
generator with Civ3's (EngineTests/GameData/MapGenParityTest.cs): for each
seed and setting it starts a new game in a running Civ3, set to that world,
and keeps the 4000 BC autosave under a name that says what it is:

    <rules>_<size>_<landform><water>_<climate>-<temperature>-<age>_s<seed>.SAV
    e.g. STD_Standard_Pan70_Nrm-Tmp-4B_s000123.SAV

    python tools/civ3_maps/generate.py --out=D:/civ3_maps/std --seeds=1-100
    python tools/civ3_maps/generate.py --out=... --seeds=1-100 --landforms=Pan,Con,Arc --water=80,70,60

or, faster, with Quick Start, which plays the last New Game's settings
from a random seed: each map type starts with a New Game, then Quick
Starts, until every type has --count maps (counting those made already):

    python tools/civ3_maps/generate.py --out=... --count=300 --block=50

Quick Start maps are saved as ..._t<time>.SAV; their seed is only in the
save, so MapGenParityTest.RenameQuickStartMaps renames them to _s<seed>
(and checks their settings are the ones in the name).

It drives Civ3 with the mouse and keyboard (civ3win.py), so the PC can't be
used while it runs. Civ3 must be running and at its main menu, windowed, with
the standard rules (New Game, not a scenario). Maps already made are
skipped, so a run can be stopped and started again. A screen it doesn't
recognize is saved to <out>/_errors/ and it goes back to the main menu.

Positions are fractions of Civ3's 1600x1200 screens (they scale with the
window).
"""
import os
import random
import shutil
import subprocess
import sys
import time

import civ3win as c

# How Civ3 is started again after it hangs: 'dxwnd' (DxWnd's first program,
# which runs it in a window), 'steam', or 'exe' (CIV3_EXE itself, as in the
# map-making VMs, tools/civ3_maps/vm). Each can be set in the environment.
LAUNCH = os.environ.get('CIV3_LAUNCH', 'dxwnd')
STEAM_URL = 'steam://rungameid/3910'
CIV3_EXE = os.environ.get('CIV3_EXE', r"C:\Civ3\Conquests\Civ3Conquests.exe")
# How long a screen may take to come up. Civ3 loads its rules before the
# world setup, slowly when several VMs share the host.
SCREEN_SECONDS = 60
# A game that hasn't started in this long has hung.
HANG_SECONDS = 120
# Civ3 wears out after a hundred or so games in one run (its text turns to
# "-Nil-" and it hangs), so it is restarted after this many.
GAMES_PER_RUN = 80

CIV3_AUTOSAVES = os.environ.get('CIV3_AUTOSAVES', r"D:\Steam\steamapps\common\Sid Meier's Civilization III Complete\Conquests\Saves\Auto")


def P(x, y):
    return x / 1600, y / 1200


MENU_NEW_GAME = P(330, 533)
MENU_QUICK_START = P(322, 598)
SEED_FIELD = P(1190, 53)
OK = P(1515, 1157)
SIZES = {'Tiny': P(360, 212), 'Small': P(360, 247), 'Standard': P(360, 281), 'Large': P(360, 315), 'Huge': P(360, 350)}
# MDJ's world sizes (1v1, 4/6/7/8 Player), in the same places.
SIZES.update({'1v1': SIZES['Tiny'], '4P': SIZES['Small'], '6P': SIZES['Standard'], '7P': SIZES['Large'], '8P': SIZES['Huge']})
# The landform thumbnails: Pangaea, Continents, Archipelago, each at 80, 70
# and 60% water.
LANDFORM_X = {'Pan': [292, 419, 545], 'Con': [672, 798, 925], 'Arc': [1051, 1178, 1305]}
WATER_INDEX = {80: 0, 70: 1, 60: 2}
LANDFORM_Y = 492
CLIMATE = {'Ari': P(190, 970), 'Nrm': P(317, 970), 'Wet': P(444, 970)}
TEMPERATURE = {'Wrm': P(669, 970), 'Tmp': P(796, 970), 'Col': P(922, 970)}
AGE = {'3B': P(1147, 970), '4B': P(1273, 970), '5B': P(1400, 970)}
CIV_ROME = P(344, 200)
# MDJ (--rules=MDJ) is started as a scenario, from Civ-Content's Load
# Scenario list, where it is; and always with a random civ: MDJ's own notes
# say "the game may crash if you pick a civ. Choose random civ instead."
# (Picking one gives the player a civ the scenario has no record for: Civ3
# 0x574080 crashes looking up its free advances.)
MENU_CIV_CONTENT = P(322, 795)
SCENARIO_MDJ = P(304, 856)
CIV_RANDOM = P(403, 698)
QUIT_YES = P(566, 350)
QUIT_OK = P(1004, 394)

# The screens are told apart by a few pixels (in 1600x1200), sampled from
# each (tools/civ3_maps/_cal).
def pixel(img, x, y):
    w, h = img.size
    return img.getpixel((int(x * w / 1600), int(y * h / 1200)))[:3]


def near(a, b, tol=30):
    return all(abs(p - q) <= tol for p, q in zip(a, b))


def at_main_menu(img):
    # The steel of the menu's frame.
    return near(pixel(img, 500, 1000), (74, 82, 82)) and near(pixel(img, 800, 580), (115, 115, 115))


def at_world_setup(img):
    # The seed field, and the screen's plain parchment at the left and the
    # bottom, the same at 1600x1200 and 1024x768. (Not the climate pictures:
    # the one chosen lights up, which a pixel there took for another screen.)
    return (near(pixel(img, 1250, 53), (255, 247, 222)) and near(pixel(img, 60, 400), (123, 123, 99), 20)
            and near(pixel(img, 800, 1180), (231, 206, 165), 20))


def at_player_setup(img):
    # The civilization list's parchment, and the difficulty bar. (Not the
    # leader portrait, which is whichever civ the screen opens on.)
    return near(pixel(img, 160, 400), (255, 247, 206)) and near(pixel(img, 250, 1100), (173, 214, 214))


def dawn_popup(img):
    # The popup's parchment over the black of the unexplored map.
    return near(pixel(img, 800, 60), (239, 239, 198)) and pixel(img, 160, 400) == (0, 0, 0)


def at_load_scenario(img):
    # The dark list of scenarios, and the parchment below the notes.
    return (near(pixel(img, 160, 400), (115, 107, 90), 20) and near(pixel(img, 1300, 940), (239, 222, 181), 20)
            and near(pixel(img, 1200, 1000), (239, 222, 181), 20))


def scenario_intro(img):
    # A scenario's introduction (MDJ's "Medieval Japan"), lower than the
    # Dawn popup: its parchment, with the unexplored map's black above and
    # to the left.
    return (near(pixel(img, 1100, 700), (247, 231, 198), 20) and near(pixel(img, 900, 60), (0, 0, 0), 15)
            and near(pixel(img, 160, 400), (0, 0, 0), 15))


def game_started(img):
    return dawn_popup(img) or scenario_intro(img)


def wait_for(test, timeout, step=0.5):
    end = time.time() + timeout
    while time.time() < end:
        c.dismiss_steam_error()
        img = c.screenshot()
        if test(img):
            return img
        time.sleep(step)
    return None


def name_of(rules, size, landform, water, climate, temperature, age, seed):
    return f'{rules}_{size}_{landform}{water}_{climate}-{temperature}-{age}_s{seed:06d}.SAV'


def back_to_menu():
    c.chord(c.VK_CONTROL, c.VK_SHIFT, ord('Q'))
    time.sleep(0.8)
    c.click(*QUIT_YES)
    time.sleep(0.3)
    c.click(*QUIT_OK)
    return wait_for(at_main_menu, 20) is not None


def latest_autosave(after):
    best = None
    for name in os.listdir(CIV3_AUTOSAVES):
        path = os.path.join(CIV3_AUTOSAVES, name)
        if name.upper().endswith('.SAV') and os.path.getmtime(path) >= after:
            if best is None or os.path.getmtime(path) > os.path.getmtime(best):
                best = path
    return best


def make_map(out, rules, size, landform, water, climate, temperature, age, seed):
    """Makes one map; returns its path, or None if a screen wasn't as
    expected (the screen is saved to <out>/_errors)."""
    def fail(why):
        os.makedirs(os.path.join(out, '_errors'), exist_ok=True)
        c.screenshot().save(os.path.join(out, '_errors', f's{seed:06d}_{landform}{water}_{why}.png'))
        print(f'  seed {seed} {landform}{water}: {why}', flush=True)
        return None

    if wait_for(at_main_menu, SCREEN_SECONDS) is None:
        return fail('no-menu')
    # The first click only highlights the item, unless it already is.
    item = MENU_CIV_CONTENT if rules == 'MDJ' else MENU_NEW_GAME
    c.click(*item)
    time.sleep(0.3)
    if at_main_menu(c.screenshot()):
        c.click(*item)
    if rules == 'MDJ':
        if wait_for(at_load_scenario, SCREEN_SECONDS) is None:
            return fail('no-load-scenario')
        c.click(*SCENARIO_MDJ)
        time.sleep(0.5)
        c.click(*OK)
    if wait_for(at_world_setup, SCREEN_SECONDS) is None:
        return fail('no-world-setup')
    c.click(*SIZES[size])
    c.click(LANDFORM_X[landform][WATER_INDEX[water]] / 1600, LANDFORM_Y / 1200)
    c.click(*CLIMATE[climate])
    c.click(*TEMPERATURE[temperature])
    c.click(*AGE[age])
    c.click(*SEED_FIELD)
    c.key(c.VK_END)
    for _ in range(12):
        c.key(c.VK_BACK)
    c.type_text(str(seed))
    c.click(*OK)
    if wait_for(at_player_setup, SCREEN_SECONDS) is None:
        return fail('no-player-setup')
    c.click(*(CIV_RANDOM if rules == 'MDJ' else CIV_ROME))
    started = time.time()
    c.click(*OK)
    return keep_autosave(out, started, name_of(rules, size, landform, water, climate, temperature, age, seed), fail)


def quick_map(out, rules, size, landform, water, climate, temperature, age):
    """Makes one map with Quick Start, from the last New Game's settings."""
    name = f'{rules}_{size}_{landform}{water}_{climate}-{temperature}-{age}_t{int(time.time() * 1000)}.SAV'

    def fail(why):
        os.makedirs(os.path.join(out, '_errors'), exist_ok=True)
        c.screenshot().save(os.path.join(out, '_errors', f'quick_{landform}{water}_{why}_{int(time.time())}.png'))
        print(f'  quick {landform}{water}: {why}', flush=True)
        return None

    if wait_for(at_main_menu, SCREEN_SECONDS) is None:
        return fail('no-menu')
    started = time.time()
    # The first click only highlights the item, unless it already is.
    c.click(*MENU_QUICK_START)
    time.sleep(0.3)
    if at_main_menu(c.screenshot()):
        c.click(*MENU_QUICK_START)
    return keep_autosave(out, started, name, fail)


def keep_autosave(out, started, name, fail):
    """Once a game is started: dismisses the Dawn popup and keeps the
    autosave written then under the name."""
    # Civ3 sometimes hangs making a map (at 95%); see restart_civ3.
    if wait_for(game_started, HANG_SECONDS, step=0.3) is None:
        return fail('no-dawn')
    c.key(c.VK_RETURN)
    path = None
    end = time.time() + 60
    while time.time() < end and path is None:
        time.sleep(0.3)
        path = latest_autosave(started)
    if path is None:
        return fail('no-autosave')
    # Wait for the save to be written in full.
    size0 = -1
    while os.path.getsize(path) != size0:
        size0 = os.path.getsize(path)
        time.sleep(0.4)
    dest = os.path.join(out, name)
    shutil.copyfile(path, dest)
    return dest


def parse_range(text):
    out = []
    for part in text.split(','):
        if '-' in part:
            a, b = part.split('-')
            out.extend(range(int(a), int(b) + 1))
        else:
            out.append(int(part))
    return out


def main(argv):
    opts = dict(a[2:].split('=', 1) for a in argv if a.startswith('--') and '=' in a)
    out = opts.get('out') or sys.exit('--out=<folder> needed')
    rules = opts.get('rules', 'STD')
    size = opts.get('size', 'Standard')
    seeds = parse_range(opts.get('seeds', '1-10'))
    landforms = opts.get('landforms', 'Pan,Con,Arc').split(',')
    waters = [int(w) for w in opts.get('water', '80,70,60').split(',')]
    climate, temperature, age = opts.get('climate', 'Nrm'), opts.get('temperature', 'Tmp'), opts.get('age', '4B')
    os.makedirs(out, exist_ok=True)
    if opts.get('remote') == '1':
        remote(out)
        return
    if 'count' in opts:
        # Each of these may be a list (--sizes=Tiny,Small), and every mix of
        # them is a map type.
        def listed(plural, singular, default):
            return opts.get(plural, opts.get(singular, default)).split(',')
        quick_run(out, rules, listed('sizes', 'size', 'Standard'), landforms, waters,
                  listed('climates', 'climate', 'Nrm'), listed('temperatures', 'temperature', 'Tmp'),
                  listed('ages', 'age', '4B'), int(opts['count']), int(opts.get('block', 50)))
        return
    jobs = [(lf, w, s) for s in seeds for lf in landforms for w in waters]
    todo = [j for j in jobs if not os.path.exists(os.path.join(out, name_of(rules, size, j[0], j[1], climate, temperature, age, j[2])))]
    print(f'{len(jobs)} maps, {len(jobs) - len(todo)} made already, {len(todo)} to make', flush=True)
    c.focus()
    t0 = time.time()
    done = 0
    for lf, w, s in todo:
        t = time.time()
        path = make_map(out, rules, size, lf, w, climate, temperature, age, s)
        if path is not None:
            done += 1
            print(f'  {os.path.basename(path)} in {time.time() - t:.0f}s', flush=True)
        if not back_to_menu():
            # Try harder: dismiss whatever is up, then again.
            c.key(c.VK_RETURN)
            time.sleep(1)
            if not back_to_menu():
                os.makedirs(os.path.join(out, '_errors'), exist_ok=True)
                c.screenshot().save(os.path.join(out, '_errors', f'stuck_s{s:06d}.png'))
                sys.exit(f'stuck after seed {s}; see {out}/_errors')
    print(f'made {done} in {time.time() - t0:.0f}s', flush=True)


def restart_civ3(out, why):
    """Kills Civ3 and starts it again, back to its main menu. Civ3 now
    and then hangs for good making a map (the progress bar at 95%)."""
    print(f'  restarting Civ3 ({why})', flush=True)
    with open(os.path.join(out, 'restarts.txt'), 'a') as f:
        f.write(f'{time.strftime("%Y-%m-%d %H:%M:%S")} {why}\n')
    for attempt in range(3):
        if c.is_open():
            c.close()
        # DxWnd's Kill after the close (the owner's request): DxWnd keeps
        # tracking a Civ3 that hung, and can't start another until it lets
        # the old one go.
        if LAUNCH == 'dxwnd':
            c.dxwnd_kill()
        # In a VM, also any Civ3 still waiting behind its Steam box (it has no
        # main window yet), so only one is ever started.
        if c.is_open() or LAUNCH == 'exe':
            subprocess.run(['taskkill', '/F', '/IM', 'Civ3Conquests.exe'], capture_output=True)
            time.sleep(3)
        time.sleep(2)
        if LAUNCH == 'dxwnd':
            c.dxwnd_run()
        elif LAUNCH == 'exe':
            subprocess.Popen([CIV3_EXE], cwd=os.path.dirname(CIV3_EXE))
        else:
            os.startfile(STEAM_URL)
        end = time.time() + 120
        while time.time() < end:
            time.sleep(1)
            c.dismiss_steam_error()
            try:
                if at_main_menu(c.screenshot()):
                    c.focus()
                    return True
            except Exception:
                pass  # no window yet
    sys.exit(f'Civ3 did not come back after {why}')


def back_or_restart(out, what):
    """Back to the main menu; restarts Civ3 if it can't get there.
    Returns False if Civ3 was restarted (so its last settings are lost)."""
    try:
        if back_to_menu():
            return True
        # Try harder: dismiss whatever is up, then again.
        c.key(c.VK_RETURN)
        time.sleep(1)
        if back_to_menu():
            return True
    except Exception:
        pass  # Civ3's window has gone
    os.makedirs(os.path.join(out, '_errors'), exist_ok=True)
    try:
        c.screenshot().save(os.path.join(out, '_errors', f'stuck_{int(time.time())}.png'))
    except Exception:
        pass
    restart_civ3(out, f'stuck after {what}')
    return False


def remote(out):
    """Remote control of a VM's Civ3, for working out new screens without
    the host's mouse (--remote=1): runs the commands in <out>/cmd.txt, one
    a line, then renames it cmd.done.txt and writes what happened to
    cmd.log. Positions are fractions of Civ3's window (or of the screen):

        launch | close | shot <name> | click <fx> <fy> | key <vk> | type <text>
        chord <vk> <vk>... | sleep <seconds> | ini <key>=<value>

    A shot is saved as <out>/<name>.png (the whole screen)."""
    from PIL import ImageGrab
    cmd, log = os.path.join(out, 'cmd.txt'), os.path.join(out, 'cmd.log')
    ini_path = os.path.join(os.path.dirname(CIV3_EXE), 'conquests.ini')
    print('remote control: waiting for cmd.txt', flush=True)
    while True:
        time.sleep(1)
        c.dismiss_steam_error()
        if not os.path.exists(cmd):
            continue
        with open(cmd) as f:
            lines = [line.strip() for line in f if line.strip()]
        os.replace(cmd, os.path.join(out, 'cmd.done.txt'))
        with open(log, 'a') as f:
            for line in lines:
                word, *args = line.split()
                try:
                    if word == 'launch':
                        restart_civ3(out, 'remote')
                    elif word == 'close':
                        c.close()
                    elif word == 'shot':
                        ImageGrab.grab(all_screens=True).save(os.path.join(out, args[0] + '.png'))
                    elif word == 'click':
                        c.click(float(args[0]), float(args[1]))
                    elif word == 'key':
                        c.key(int(args[0], 0))
                    elif word == 'type':
                        c.type_text(' '.join(args))
                    elif word == 'chord':
                        c.chord(*[int(a, 0) for a in args])
                    elif word == 'sleep':
                        time.sleep(float(args[0]))
                    elif word == 'ini':
                        k, v = ' '.join(args).split('=', 1)
                        with open(ini_path, encoding='latin-1') as g:
                            text = g.read().splitlines()
                        text = [f'{k}={v}' if t.split('=', 1)[0] == k else t for t in text]
                        with open(ini_path, 'w', encoding='latin-1') as g:
                            g.write('\n'.join(text) + '\n')
                    f.write(f'{time.strftime("%H:%M:%S")} ok {line}\n')
                except Exception as e:
                    f.write(f'{time.strftime("%H:%M:%S")} failed {line}: {e}\n')
            f.write(f'{time.strftime("%H:%M:%S")} done\n')


def wait_while_paused(out):
    """Waits, between maps, while a file named PAUSE is in the output
    folder, so someone can use the PC (or a VM's window) without the script
    clicking under them."""
    if not os.path.exists(os.path.join(out, 'PAUSE')):
        return
    print('  paused (delete PAUSE to go on)', flush=True)
    while os.path.exists(os.path.join(out, 'PAUSE')):
        time.sleep(2)
    print('  going on', flush=True)
    if c.is_open():
        c.focus()


def quick_run(out, rules, sizes, landforms, waters, climates, temperatures, ages, count, block):
    """Until each map type (each mix of the sizes, landforms, waters,
    climates, temperatures and ages given) has count maps: a New Game of
    the type from a random seed, then up to block - 1 Quick Starts, type
    after type."""
    def prefix(t):
        size, lf, w, cl, te, ag = t
        return f'{rules}_{size}_{lf}{w}_{cl}-{te}-{ag}_'

    def made(t):
        p = prefix(t)
        return sum(1 for n in os.listdir(out) if n.startswith(p) and n.upper().endswith('.SAV'))

    types = [(size, lf, w, cl, te, ag) for size in sizes for lf in landforms for w in waters
             for cl in climates for te in temperatures for ag in ages]
    print(f'{len(types)} map types, {count} maps each; {sum(min(count, made(t)) for t in types)} of {len(types) * count} made', flush=True)
    if c.is_open():
        c.focus()
    else:
        restart_civ3(out, 'not running')
    t0 = time.time()
    done = 0
    rng = random.Random()
    games = 0
    while True:
        todo = [t for t in types if made(t) < count]
        if not todo:
            break
        for t in todo:
            n = min(block, count - made(t))
            started = time.time()
            # A New Game sets the type, then Quick Starts repeat it. After
            # a restart the type is set again with another New Game.
            new_game = True
            for _ in range(n):
                wait_while_paused(out)
                if new_game:
                    seed = rng.randint(1, 2 ** 31 - 1)
                    path = make_map(out, rules, *t, seed)
                    what = f'{prefix(t)} seed {seed}'
                else:
                    path = quick_map(out, rules, *t)
                    what = f'{prefix(t)} quick start'
                if path is not None:
                    done += 1
                    # Quick Start goes back to Civ3's own rules, so a
                    # scenario's maps are all new games.
                    new_game = rules == 'MDJ'
                games += 1
                if not back_or_restart(out, what):
                    new_game = True
                    games = 0
                elif games >= GAMES_PER_RUN:
                    restart_civ3(out, f'{games} games in this run')
                    new_game = True
                    games = 0
            print(f'  {prefix(t)}: {made(t)} maps ({n} in {time.time() - started:.0f}s); {done} made in {time.time() - t0:.0f}s', flush=True)
    print(f'made {done} in {time.time() - t0:.0f}s', flush=True)


if __name__ == '__main__':
    main(sys.argv[1:])
