"""Real Chrome/Edge, Windows IME, and the edition's own field probe and monitor. CI only."""
import ctypes
from ctypes import wintypes as W
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import threading
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


def fixture_html(file, title):
    file.write_text(f"""<!doctype html><meta charset=utf-8><title>{title}</title>
        <style>input{{display:block;font-size:20px;margin:5px}}</style>
        <input id=normal><input id=email type=email><input id=number type=number>
        <input id=numeric inputmode=numeric><input id=decimal inputmode=decimal>
        <input id=pin pattern="[0-9]+"><input id=mixed pattern="[A-Z0-9]+">
        <script>window.events=[];for(const t of ['compositionupdate','compositionend'])
          document.addEventListener(t, e=>events.push(e.type));</script>""", encoding="utf-8")


def exercise(page, channel, dll, revision, root):
    title = f"Numeric IME fixture {uuid4().hex}"
    file = root / "input.html"
    fixture_html(file, title)
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
            for field in ("email", "number", "numeric", "decimal", "pin", "mixed"):
                try:
                    box = page.locator("#" + field)
                    box.click()
                    page.wait_for_function("id => document.activeElement.id === id && document.hasFocus()", arg=field)
                    direct = metadata(dll, hwnd, field, "Direct")
                    # Chromium may expose a DOM hint only to the optional extension.
                    # The separate installed-extension test requires actual target
                    # routing for those fields. Here we check the addon-free contract.
                    implicit = field in ("numeric", "decimal", "pin")
                    if field == "mixed" or (implicit and not direct):
                        if direct:
                            raise AssertionError("Alphanumeric field misclassified as numeric")
                        page.wait_for_timeout(1100)
                        if user32.GetKeyboardLayout(thread) != source:
                            raise AssertionError("Non-numeric or ambiguous field changed source layout")
                        print(f"PASS {field}: no direct hint in native metadata; source remains selected")
                        continue
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
                finally:
                    japanese_word(page, hwnd, thread, source)
        finally:
            monitor.terminate()
            monitor.wait(timeout=8)
            print("Monitor transitions:\n" + log_file.read_text("utf-8", errors="replace"))
    if failures and (revision != "previous" or any(name == "email" for name, _ in failures)):
        raise AssertionError(f"Failed {revision} edition: {failures}")
    print(f"{revision} numeric evidence: {failures or 'all passed'}")


def setting(app, edition, target):
    filename = "settings-v1.json" if edition == "Simple" else "settings-v2.json"
    path = Path(os.environ["LOCALAPPDATA"]) / "ImeLayoutRouter" / filename
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists():
        raise AssertionError("This disposable user already has settings")
    path.write_text(json.dumps({
        "Version": 3, "Source": {"LanguageId": 0x411,
            "Clsid": "D5A86FD5-5308-47EA-AD16-9C4EB160EC3C", "ProfileGuid": "773EB24E-CA1D-4B1B-B420-FA985BB0B80D"},
        "Target": {"LanguageId": target & 0xffff, "Hkl": target}
    }), encoding="utf-8")
    return subprocess.Popen([app])


def press_browser_button(hwnd, labels, approved=None):
    # Select a real visible UIA button inside the disposable browser's process.
    from pywinauto import Desktop, mouse
    pid = W.DWORD()
    user32.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
    desktop = Desktop(backend="uia")
    seen = set()
    def find():
        for top in desktop.windows(process=pid.value, visible_only=True):
            for button in top.descendants(control_type="Button"):
                label = button.window_text()
                seen.add(label)
                if label in labels and button.is_visible() and button.is_enabled():
                    return button
        if approved and approved():
            return True
        return None
    try:
        button = until(find, f"Browser permission UI button {labels!r} unavailable", 15)
    except AssertionError:
        print("Available button labels in this fixture browser:", sorted(seen))
        raise
    if button is True:
        print("The browser granted this site without a separate native confirmation dialog")
        return
    point = button.rectangle().mid_point()
    user32.WindowFromPoint.argtypes = [W.POINT]
    user32.WindowFromPoint.restype = W.HWND
    owner = W.DWORD()
    user32.GetWindowThreadProcessId(user32.WindowFromPoint(W.POINT(point.x, point.y)), ctypes.byref(owner))
    if owner.value != pid.value:
        raise AssertionError("Permission button is obscured by another process")
    mouse.click(coords=(point.x, point.y))


def toggle_site(context, page, hwnd, ext_id, enable):
    tab = context.new_cdp_session(page)
    target = tab.send("Target.getTargetInfo")["targetInfo"]["targetId"]
    tab.detach()
    browser = context.browser.new_browser_cdp_session()
    try:
        browser.send("Extensions.triggerAction", {"id": ext_id, "targetId": target})
    finally:
        browser.detach()
    labels = ({"Enable on this site", "このサイトで有効にする"} if enable
              else {"Disable on this site", "このサイトで無効にする"})
    origin = page.evaluate("location.origin")
    def granted():
        return extension_worker(context, ext_id).evaluate("""async origin => {
          const pattern = new URL(origin);
          return !!(await chrome.storage.local.get('site:'+origin))['site:'+origin] &&
            await chrome.permissions.contains({origins:[pattern.protocol+'//'+pattern.hostname+'/*']})
        }""", origin)
    press_browser_button(hwnd, labels)
    if enable:
        press_browser_button(hwnd, {"Allow", "許可", "Allow access", "アクセスを許可"}, granted)
    until(lambda: granted() == enable, "The production popup did not update its origin's opt-in and permission")
    # The production popup remains open after a toggle. A native click on
    # the page returns focus without directing Escape to an unrelated window.
    page.bring_to_front()
    page.locator("#normal").click()
    print("PASS production popup", "enabled" if enable else "disabled", "only", origin)


def extension_worker(context, ext_id):
    prefix = "chrome-extension://" + ext_id + "/"
    for worker in context.service_workers:
        if worker.url.startswith(prefix):
            return worker
    return context.wait_for_event("serviceworker", predicate=lambda w: w.url.startswith(prefix), timeout=8000)


def addon(context, channel, dll, app, edition, root):
    app = Path(app).resolve(strict=True)
    ext_id = "feaapoghfchepkmogdmdkcedfimopbpl"
    ext = (app.parent / "browser-extension").resolve(strict=True)
    manifest = json.loads((app.parent / "numeric-host.json").read_text(encoding="utf-8-sig"))
    assert manifest["allowed_origins"] == ["chrome-extension://" + ext_id + "/"]
    assert (app.parent / manifest["path"]).resolve(strict=True) == app
    # CDP's explicit extension debugging session uses a temporary test
    # browser and DevTools pipe, never a shared browser profile.
    browser = context.browser.new_browser_cdp_session()
    try:
        loaded = browser.send("Extensions.loadUnpacked", {"path": str(ext)})
        assert loaded["id"] == ext_id, loaded
    finally:
        browser.detach()
    print("Installed real packaged extension in", channel, ext_id)
    web = root / "www"
    web.mkdir()
    title = f"Numeric addon fixture {uuid4().hex}"
    fixture_html(web / "input.html", title)
    server = ThreadingHTTPServer(("127.0.0.1", 0), partial(SimpleHTTPRequestHandler, directory=str(web)))
    threading.Thread(target=server.serve_forever, daemon=True).start()
    url = f"http://127.0.0.1:{server.server_port}/input.html"
    page = context.pages[0]
    router = None
    try:
        page.goto(url)
        page.bring_to_front()
        page.locator("#normal").click()
        hwnd, thread = until(lambda: owned_window(title, channel), "Own extension fixture browser unavailable")
        source = japanese_hkl()
        japanese_word(page, hwnd, thread, source)
        layouts = (W.HANDLE * 64)()
        count = user32.GetKeyboardLayoutList(len(layouts), layouts)
        target = next(h for h in layouts[:count] if h & 0xffff == 0x409)
        router = setting(app, edition, target)
        page.wait_for_timeout(1200)
        assert router.poll() is None, "Production tray process exited"

        def field_check(field, enabled):
            page.locator("#" + field).click()
            native_direct = metadata(dll, hwnd, field, "Direct")
            if enabled or native_direct:
                until(lambda: user32.GetKeyboardLayout(thread) == target,
                      f"{field}: packaged app did not route to target {target:#x}")
                digits = "0012" if field != "number" else "12"
                page.evaluate("id => document.getElementById(id).value=''", field)
                for ch in digits:
                    key(hwnd, ord(ch))
                until(lambda: page.locator("#"+field).input_value() == digits,
                      f"{field}: target did not preserve the real input")
                print(f"PASS field={field} enabled={enabled} nativeDirect={native_direct} target={target:#x} value={digits}")
            else:
                page.wait_for_timeout(1100)
                assert user32.GetKeyboardLayout(thread) == source, f"Unapproved {field} unexpectedly changed the layout"
                print("PASS inactive or mixed field retained source", field)
            japanese_word(page, hwnd, thread, source)
            assert router.poll() is None

        field_check("email", False)  # Verify the configured, packaged monitor is running.
        field_check("numeric", False)
        toggle_site(context, page, hwnd, ext_id, True)
        for field in ("number", "numeric", "decimal", "pin"):
            field_check(field, True)
        field_check("mixed", False)
        page.goto(url.replace("127.0.0.1", "localhost"))
        page.locator("#normal").click()
        field_check("numeric", False)
        page.goto(url)
        page.locator("#normal").click()
        field_check("pin", True)
        toggle_site(context, page, hwnd, ext_id, False)
        field_check("numeric", False)
        print("PASS packaged native host, real browser extension, origin opt-in and revoke")
    finally:
        if router is not None:
            router.terminate()
            router.wait(timeout=8)
        server.shutdown()
        server.server_close()


if __name__ == "__main__":
    channel, dll, revision, *params = sys.argv[1:]
    if channel not in ("chrome", "msedge") or revision not in ("previous", "current", "addon"):
        raise ValueError("Unknown fixture arguments")
    with tempfile.TemporaryDirectory(dir=os.environ["RUNNER_TEMP"]) as tmp, sync_playwright() as playwright:
        root = Path(tmp)
        context = playwright.chromium.launch_persistent_context(root / "profile", channel=channel,
            headless=False, viewport={"width": 850, "height": 620},
            args=["--enable-unsafe-extension-debugging"] if revision == "addon" else [],
            ignore_default_args=["--disable-extensions"] if revision == "addon" else [])
        try:
            dll = str(Path(dll).resolve(strict=True))
            if revision == "addon":
                app, edition = params
                addon(context, channel, dll, app, edition, root)
            else:
                exercise(context.pages[0], channel, dll, revision, root)
        finally:
            context.close()
