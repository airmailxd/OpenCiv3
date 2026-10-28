"""Drives a running Civilization III window: finds it, brings it to the
front, takes screenshots of its client area, and clicks and types in it.
Positions are given as fractions of the client area (0..1), so they hold
whatever size the window is. Windows only; needs nothing but Pillow.
"""
import ctypes
import ctypes.wintypes as W
import time

from PIL import ImageGrab

user32 = ctypes.WinDLL('user32', use_last_error=True)
user32.SetProcessDPIAware()

TITLE = "Sid Meier's Civilization III: Complete"

INPUT_MOUSE, INPUT_KEYBOARD = 0, 1
MOUSEEVENTF_MOVE, MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP, MOUSEEVENTF_ABSOLUTE = 0x1, 0x2, 0x4, 0x8000
KEYEVENTF_KEYUP, KEYEVENTF_UNICODE = 0x2, 0x4


class MOUSEINPUT(ctypes.Structure):
    _fields_ = [('dx', W.LONG), ('dy', W.LONG), ('mouseData', W.DWORD), ('dwFlags', W.DWORD),
                ('time', W.DWORD), ('dwExtraInfo', ctypes.POINTER(W.ULONG))]


class KEYBDINPUT(ctypes.Structure):
    _fields_ = [('wVk', W.WORD), ('wScan', W.WORD), ('dwFlags', W.DWORD), ('time', W.DWORD),
                ('dwExtraInfo', ctypes.POINTER(W.ULONG))]


class _U(ctypes.Union):
    _fields_ = [('mi', MOUSEINPUT), ('ki', KEYBDINPUT)]


class INPUT(ctypes.Structure):
    _fields_ = [('type', W.DWORD), ('u', _U)]


def _send(*inputs):
    arr = (INPUT * len(inputs))(*inputs)
    user32.SendInput(len(inputs), arr, ctypes.sizeof(INPUT))


def window():
    hwnd = user32.FindWindowW(None, TITLE)
    if not hwnd:
        raise RuntimeError('Civ3 is not running (no window titled %r)' % TITLE)
    return hwnd


def client_rect(hwnd=None):
    """(left, top, width, height) of the client area, in screen pixels."""
    hwnd = hwnd or window()
    r = W.RECT()
    user32.GetClientRect(hwnd, ctypes.byref(r))
    pt = W.POINT(0, 0)
    user32.ClientToScreen(hwnd, ctypes.byref(pt))
    return pt.x, pt.y, r.right - r.left, r.bottom - r.top


def focus(hwnd=None):
    hwnd = hwnd or window()
    SW_RESTORE = 9
    if user32.IsIconic(hwnd):
        user32.ShowWindow(hwnd, SW_RESTORE)
    # Windows only lets the foreground window hand focus on; a stray Alt
    # press makes this process count as having had input.
    key(0x12)
    user32.SetForegroundWindow(hwnd)
    time.sleep(0.2)


def screenshot(hwnd=None):
    # A grab fails while Windows shows its secure desktop (a UAC prompt, the
    # lock screen): wait it out, for up to ten minutes.
    for attempt in range(600):
        try:
            x, y, w, h = client_rect(hwnd)
            return ImageGrab.grab(bbox=(x, y, x + w, y + h), all_screens=True)
        except OSError:
            time.sleep(1)
    raise RuntimeError('screen grabs keep failing')


def to_screen(fx, fy, hwnd=None):
    x, y, w, h = client_rect(hwnd)
    return int(x + fx * w), int(y + fy * h)


def click(fx, fy, hwnd=None, pause=0.15):
    sx, sy = to_screen(fx, fy, hwnd)
    vw, vh = user32.GetSystemMetrics(78), user32.GetSystemMetrics(79)
    vx, vy = user32.GetSystemMetrics(76), user32.GetSystemMetrics(77)
    ax = int((sx - vx) * 65535 / max(1, vw - 1))
    ay = int((sy - vy) * 65535 / max(1, vh - 1))
    VIRTUALDESK = 0x4000
    flags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | VIRTUALDESK
    _send(INPUT(INPUT_MOUSE, _U(mi=MOUSEINPUT(ax, ay, 0, flags, 0, None))))
    time.sleep(0.05)
    _send(INPUT(INPUT_MOUSE, _U(mi=MOUSEINPUT(ax, ay, 0, flags | MOUSEEVENTF_LEFTDOWN, 0, None))))
    time.sleep(0.05)
    _send(INPUT(INPUT_MOUSE, _U(mi=MOUSEINPUT(ax, ay, 0, flags | MOUSEEVENTF_LEFTUP, 0, None))))
    time.sleep(pause)


def key(vk, down=True, up=True):
    if down:
        _send(INPUT(INPUT_KEYBOARD, _U(ki=KEYBDINPUT(vk, 0, 0, 0, None))))
    if up:
        _send(INPUT(INPUT_KEYBOARD, _U(ki=KEYBDINPUT(vk, 0, KEYEVENTF_KEYUP, 0, None))))
    time.sleep(0.04)


def chord(*vks):
    """Keys held together, as Ctrl+Shift+Q: (0x11, 0x10, ord('Q'))."""
    for vk in vks:
        key(vk, up=False)
    for vk in reversed(vks):
        key(vk, down=False)


def type_text(text):
    """Types characters (digits and letters) by their virtual keys, which
    older games read more reliably than Unicode input."""
    for ch in text:
        vk = user32.VkKeyScanW(ord(ch)) & 0xFF
        shift = (user32.VkKeyScanW(ord(ch)) >> 8) & 1
        if shift:
            chord(0x10, vk)
        else:
            key(vk)


VK_RETURN, VK_ESCAPE, VK_BACK, VK_DELETE, VK_END, VK_HOME = 0x0D, 0x1B, 0x08, 0x2E, 0x23, 0x24
VK_CONTROL, VK_SHIFT, VK_MENU = 0x11, 0x10, 0x12


# DxWnd, which runs Civ3 in a window: its list of programs, Civ3 the first.
DXWND_TITLE = 'DXWnd'
MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP = 0x8, 0x10


def _press_at(sx, sy, down, up, times=1):
    vw, vh = user32.GetSystemMetrics(78), user32.GetSystemMetrics(79)
    vx, vy = user32.GetSystemMetrics(76), user32.GetSystemMetrics(77)
    ax = int((sx - vx) * 65535 / max(1, vw - 1))
    ay = int((sy - vy) * 65535 / max(1, vh - 1))
    flags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | 0x4000
    _send(INPUT(INPUT_MOUSE, _U(mi=MOUSEINPUT(ax, ay, 0, flags, 0, None))))
    time.sleep(0.1)
    for _ in range(times):
        _send(INPUT(INPUT_MOUSE, _U(mi=MOUSEINPUT(ax, ay, 0, flags | down, 0, None))))
        time.sleep(0.05)
        _send(INPUT(INPUT_MOUSE, _U(mi=MOUSEINPUT(ax, ay, 0, flags | up, 0, None))))
        time.sleep(0.08)


def dxwnd_icon():
    """The screen position of the first program's icon in DxWnd's list."""
    hwnd = user32.FindWindowW(None, DXWND_TITLE)
    if not hwnd:
        raise RuntimeError('DxWnd is not running')
    if user32.IsIconic(hwnd):
        user32.ShowWindow(hwnd, 9)
    key(0x12)
    user32.SetForegroundWindow(hwnd)
    time.sleep(0.3)
    x, y, w, h = client_rect(hwnd)
    return x + 40, y + 18


def dxwnd_kill():
    """Kills the program DxWnd runs (its right-click menu's Kill)."""
    sx, sy = dxwnd_icon()
    _press_at(sx, sy, MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP)
    time.sleep(0.5)
    # Kill is the menu's last item, 219 pixels below the click.
    _press_at(sx + 45, sy + 219, MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP)
    # "Do you want to kill the "Civilization III" task?": Yes.
    for _ in range(20):
        time.sleep(0.25)
        dialog = user32.FindWindowW(None, 'Warning')
        if dialog:
            key(0x12)
            user32.SetForegroundWindow(dialog)
            time.sleep(0.3)
            key(ord('Y'))
            break
    # With nothing left to kill DxWnd may answer with a message of its own:
    # dismiss whatever dialog is left.
    time.sleep(1)
    for title in ('Warning', 'DxWnd', 'DXWnd Warning', 'Error'):
        dialog = user32.FindWindowW('#32770', title)
        if dialog:
            user32.SetForegroundWindow(dialog)
            time.sleep(0.2)
            key(0x0D)
    time.sleep(1)


def dxwnd_run():
    """Starts Civ3 through DxWnd (a double click on its icon)."""
    sx, sy = dxwnd_icon()
    _press_at(sx, sy, MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP, times=2)


def close():
    """Asks Civ3's window to close (WM_CLOSE, as Alt+F4). After a hang
    this is what closes it: taskkill can't, and leaves a zombie holding
    the window. Returns whether the window has gone."""
    WM_CLOSE = 0x10
    for _ in range(3):
        hwnd = user32.FindWindowW(None, TITLE)
        if not hwnd:
            return True
        user32.PostMessageW(hwnd, WM_CLOSE, 0, 0)
        for _ in range(20):
            time.sleep(0.5)
            if not user32.FindWindowW(None, TITLE):
                return True
    return not user32.FindWindowW(None, TITLE)


def is_open():
    return bool(user32.FindWindowW(None, TITLE))


def dismiss_steam_error():
    """Presses OK on Civ3's "Steam must be running to play this game
    (SteamAPI_Init() failed)" box, which it shows when started without
    Steam (always in the VMs); Civ3 then starts regardless. Returns whether
    there was one."""
    dialog = user32.FindWindowW('#32770', 'Fatal Error')
    if not dialog:
        return False
    key(0x12)
    user32.SetForegroundWindow(dialog)
    time.sleep(0.2)
    key(VK_RETURN)
    time.sleep(0.5)
    return True
