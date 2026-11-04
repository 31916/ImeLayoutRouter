"""Real Chrome/Edge, Windows IME, and the edition's own field probe and monitor. CI only."""
import ctypes
from ctypes import wintypes as W
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import time
from uuid import uuid4

if os.environ.get("GITHUB_ACTIONS") != "true" or os.environ.get("RUNNER_ENVIRONMENT") != "github-hosted":
    raise SystemExit("Requires a disposable GitHub-hosted Windows runner")
from playwright.sync_api import sync_playwright

user32 = ctypes.WinDLL("user32", use_last_error=True)
kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
user32.GetForegroundWindow.restype = W.HWND
user32.GetKeyboardLayout.argtypes = [W.DWORD]
user32.GetKeyboardLayout.restype = W.HANDLE
user32.GetKeyboardLayoutList.argtypes = [ctypes.c_int, ctypes.POINTER(W.HANDLE)]
user32.GetWindowThreadProcessId.argtypes = [W.HWND, ctypes.POINTER(W.DWORD)]
user32.GetWindowThreadProcessId.restype = W.DWORD
user32.GetWindowTextW.argtypes = [W.HWND, W.LPWSTR, ctypes.c_int]
user32.PostMessageW.argtypes = [W.HWND, W.UINT, W.WPARAM, W.LPARAM]
user32.keybd_event.argtypes = [W.BYTE, W.BYTE, W.DWORD, ctypes.c_size_t]
kernel32.OpenProcess.argtypes = [W.DWORD, W.BOOL, W.DWORD]
kernel32.OpenProcess.restype = W.HANDLE
kernel32.QueryFullProcessImageNameW.argtypes = [W.HANDLE, W.DWORD, W.LPWSTR, ctypes.POINTER(W.DWORD)]
kernel32.CloseHandle.argtypes = [W.HANDLE]
imm32 = ctypes.WinDLL("imm32", use_last_error=True)
imm32.ImmGetDefaultIMEWnd.argtypes = [W.HWND]
imm32.ImmGetDefaultIMEWnd.restype = W.HWND
user32.SendMessageTimeoutW.argtypes = [W.HWND, W.UINT, W.WPARAM, W.LPARAM, W.UINT, W.UINT,
                                       ctypes.POINTER(ctypes.c_size_t)]


class GuiInfo(ctypes.Structure):
    _fields_ = [("size", W.DWORD), ("flags", W.DWORD)] + [
        (name, W.HWND) for name in ("active", "focus", "capture", "menu", "move", "caret")
    ] + [("bounds", W.RECT)]


def open_ime(hwnd, thread):
    info = GuiInfo()
    info.size = ctypes.sizeof(info)
    deadline = time.monotonic() + 6
    next_request = 0
    stable_since = None
    while time.monotonic() < deadline:
        if user32.GetForegroundWindow() != hwnd:
            raise AssertionError("Browser lost focus while waiting for Japanese input")
        open_status = ctypes.c_size_t()
        ime = (imm32.ImmGetDefaultIMEWnd(info.focus) if user32.GetGUIThreadInfo(thread, ctypes.byref(info))
               and info.focus else None)
        ready = ime and user32.SendMessageTimeoutW(ime, 0x283, 5, 0, 2, 200, ctypes.byref(open_status)) and open_status.value
        if ready:
            stable_since = stable_since or time.monotonic()
            if time.monotonic() - stable_since >= 0.3:
                return
        else:
            stable_since = None
            if time.monotonic() >= next_request:
                key(hwnd, 0x16)  # VK_IME_ON. No word or test character is retried.
                next_request = time.monotonic() + 0.6
        time.sleep(0.08)
    raise AssertionError(f"Japanese IME did not open in the browser; HKL={user32.GetKeyboardLayout(thread):#x}")


def until(check, label, seconds=8):
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        if result := check():
            return result
        time.sleep(0.08)
    raise AssertionError(label)


def owned_window(title, channel):
    hwnd = user32.GetForegroundWindow()
    text = ctypes.create_unicode_buffer(512)
    if not hwnd or not user32.GetWindowTextW(hwnd, text, len(text)) or title not in text.value:
        return None
    process_id = W.DWORD()
    thread = user32.GetWindowThreadProcessId(hwnd, ctypes.byref(process_id))
    process = kernel32.OpenProcess(0x1000, False, process_id.value)
    if not process:
        raise ctypes.WinError(ctypes.get_last_error())
    try:
        path = ctypes.create_unicode_buffer(1024)
        length = W.DWORD(len(path))
        if not kernel32.QueryFullProcessImageNameW(process, 0, path, ctypes.byref(length)):
            raise ctypes.WinError(ctypes.get_last_error())
        if Path(path.value).name.lower() != channel + ".exe":
            raise AssertionError("Fixture title does not belong to the expected browser")
    finally:
        kernel32.CloseHandle(process)
    return hwnd, thread


def key(hwnd, vk):
    if user32.GetForegroundWindow() != hwnd:
        raise AssertionError("Browser lost foreground; stopping before input")
    user32.keybd_event(vk, 0, 0, 0)
    user32.keybd_event(vk, 0, 2, 0)
    time.sleep(0.11)


def japanese_hkl():
    layouts = (W.HANDLE * 64)()
    count = user32.GetKeyboardLayoutList(len(layouts), layouts)
    print("Windows layouts:", [hex(hkl) for hkl in layouts[:count]])
    return next(hkl for hkl in layouts[:count] if hkl & 0xffff == 0x411)


def select_source(hwnd, thread, hkl):
    if user32.GetForegroundWindow() != hwnd:
        raise AssertionError("Browser lost focus before requesting its source IME")
    if not user32.PostMessageW(hwnd, 0x50, 0, hkl):  # User's explicit return to Japanese.
        raise ctypes.WinError(ctypes.get_last_error())
    until(lambda: user32.GetKeyboardLayout(thread) == hkl, "Japanese source HKL was not selected")


def japanese_word(page, hwnd, thread, hkl):
    page.locator("#normal").click()
    select_source(hwnd, thread, hkl)
    open_ime(hwnd, thread)
    page.evaluate("normal.value=''; window.events=[]")
    for char in "NIHONN":
        key(hwnd, ord(char))
    try:
        page.wait_for_function("normal.value==='にほん' && events.includes('compositionupdate')", timeout=5000)
        key(hwnd, 0x0d)
        page.wait_for_function("normal.value==='にほん' && events.includes('compositionend')", timeout=5000)
    except Exception:
        print("Fixed local fixture state:", page.evaluate("({value:normal.value, events, focus:document.activeElement.id})"))
        raise
    print("PASS native Japanese preedit and commit after selecting the source")


def metadata(dll, hwnd, field, expected):
    deadline = time.monotonic() + 4
    last = ""
    while True:
        last = subprocess.run(["dotnet", dll, "--field-metadata", str(hwnd)], capture_output=True,
                              text=True, encoding="utf-8", errors="replace", timeout=14, check=True).stdout
        if f"result={expected} " in last or time.monotonic() >= deadline:
            print(f"Metadata for {field}:\n{last.rstrip()}")
            return f"result={expected} " in last


def exercise(page, channel, dll, revision, root):
    title = f"Numeric IME fixture {uuid4().hex}"
    file = root / "input.html"
    file.write_text(f"""<!doctype html><meta charset=utf-8><title>{title}</title>
        <style>input{{display:block;font-size:20px;margin:5px}}</style>
        <input id=normal><input id=email type=email><input id=number type=number>
        <input id=numeric inputmode=numeric><input id=decimal inputmode=decimal>
        <script>window.events=[];for(const t of ['compositionupdate','compositionend'])
          document.addEventListener(t, e=>events.push(e.type));</script>""", encoding="utf-8")
    page.goto(file.as_uri())
    page.bring_to_front()
    page.locator("#normal").click()
    hwnd, thread = until(lambda: owned_window(title, channel), "Fixture is not the active native browser")
    source = japanese_hkl()
    japanese_word(page, hwnd, thread, source)
    if not metadata(dll, hwnd, "normal", "Text"):
        raise AssertionError("Normal input has no positive focused text metadata")
    log_file = root / "monitor.txt"
    failures = []
    with log_file.open("w", encoding="utf-8") as output:
        monitor = subprocess.Popen(["dotnet", dll, "--browser", str(hwnd)], stdout=output, stderr=subprocess.STDOUT)
        try:
            def read_target():
                if monitor.poll() is not None:
                    raise AssertionError(f"Routing monitor exited: {monitor.returncode}")
                match = re.search(r"^TARGET [^\n]+ ([0-9A-Fa-f]+)\s*$", log_file.read_text("utf-8", errors="replace"), re.M)
                return int(match[1], 16) if match else None
            target = until(read_target, "Monitor has no enabled target keyboard layout")
            for field in ("email", "number", "numeric", "decimal"):
                try:
                    box = page.locator("#" + field)
                    box.click()
                    page.wait_for_function("id => document.activeElement.id === id && document.hasFocus()", arg=field)
                    direct = metadata(dll, hwnd, field, "Direct")
                    routed = until(lambda: user32.GetKeyboardLayout(thread) == target,
                                   f"{field}: target HKL {target:#x} not selected; current {user32.GetKeyboardLayout(thread):#x}", 4)
                    digits = "12" if field == "number" else "0012"
                    for digit in digits:
                        key(hwnd, ord(digit))
                    until(lambda: box.input_value() == digits, "ASCII digits were not preserved")
                    if not direct or not routed:
                        raise AssertionError("Direct focused metadata or routing missing")
                    print(f"PASS {field}: HKL={target:#x}, value={digits}")
                except AssertionError as exc:
                    failures.append((field, str(exc)))
                    print("FAIL", field, str(exc))
                japanese_word(page, hwnd, thread, source)
        finally:
            monitor.terminate()
            monitor.wait(timeout=8)
            print("Monitor transitions:\n" + log_file.read_text("utf-8", errors="replace"))
    if failures and (revision != "previous" or any(name == "email" for name, _ in failures)):
        raise AssertionError(f"Failed {revision} edition: {failures}")
    print(f"{revision} numeric evidence: {failures or 'all passed'}")


if __name__ == "__main__":
    channel, dll, revision = sys.argv[1:]
    if channel not in ("chrome", "msedge") or revision not in ("previous", "current"):
        raise ValueError("Unknown fixture arguments")
    with tempfile.TemporaryDirectory(dir=os.environ["RUNNER_TEMP"]) as tmp, sync_playwright() as playwright:
        root = Path(tmp)
        context = playwright.chromium.launch_persistent_context(root / "profile", channel=channel,
                                                                headless=False, viewport={"width": 850, "height": 620})
        try:
            exercise(context.pages[0], channel, str(Path(dll).resolve(strict=True)), revision, root)
        finally:
            context.close()
