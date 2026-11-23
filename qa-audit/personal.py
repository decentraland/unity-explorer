"""Screen-level evidence collection on the hosted CI player using its actual UI."""
import ctypes
from ctypes import wintypes
import json
import os
import re
import subprocess
import tempfile
import time
from pathlib import Path
from urllib.error import URLError
from urllib.parse import urlsplit, urlunsplit
from urllib.request import Request, urlopen

from audit_input import key


def run(session, tool, capture, output_dir):
    out = Path(output_dir)
    out.mkdir(parents=True, exist_ok=True)
    records = []

    def record(**fields):
        records.append(fields)
        (out / "personal-observations.json").write_text(
            json.dumps(records, indent=2, ensure_ascii=False), encoding="utf-8")

    def capture_broken_destination(public, stem):
        from playwright.sync_api import Error, sync_playwright
        with sync_playwright() as service:
            browser = service.chromium.launch(channel="msedge", headless=True)
            page = browser.new_page(viewport={"width":1440,"height":900}, device_scale_factor=2, locale="en-US")
            try:
                page.goto(public, wait_until="domcontentloaded", timeout=18000)
            except Error as exc:
                record(action="Browser navigation failed", host=urlsplit(public).hostname,
                       reason=next((line[:180] for line in str(exc).splitlines() if "net::" in line), "Navigation failed"))
            try:
                page.wait_for_function("document.body && /DNS|site can.t be reached|page isn.t working|network|can.t reach this page|ERR_NAME_NOT_RESOLVED/i.test(document.body.innerText)",timeout=6500)
            except Error:
                page.wait_for_timeout(1500)
            dest = out / ("personal-" + stem + "-browser.png")
            page.screenshot(path=str(dest), animations="disabled")
            record(action="Browser destination screenshot", host=urlsplit(public).hostname, screenshot=dest.name,
                   body=page.locator("body").inner_text(timeout=5000)[:500])
            browser.close()

    def clipboard_text():
        user32 = ctypes.windll.user32
        kernel32 = ctypes.windll.kernel32
        user32.GetClipboardData.restype = wintypes.HANDLE
        kernel32.GlobalLock.argtypes = [wintypes.HANDLE]
        kernel32.GlobalLock.restype = ctypes.c_void_p
        kernel32.GlobalUnlock.argtypes = [wintypes.HANDLE]
        for _ in range(24):
            if user32.OpenClipboard(None):
                try:
                    handle = user32.GetClipboardData(13)
                    if handle:
                        ptr = kernel32.GlobalLock(handle)
                        if ptr:
                            try:
                                return ctypes.wstring_at(ptr).strip()
                            finally:
                                kernel32.GlobalUnlock(handle)
                finally:
                    user32.CloseClipboard()
            time.sleep(.2)
        return ""

    def elements():
        return tool(session, "ui_list", {"stack": "ugui", "checkOcclusion": True})["elements"]

    def find(paths, labels=(), scope="", kinds=("button", "toggle", "dropdown"), row=None):
        entries = elements() if row is None else row
        candidates = [e for e in entries if e.get("kind") in kinds and not e.get("occluded")
                      and re.search(scope, e.get("path", ""), re.I)]
        for label in labels:
            for e in candidates:
                if re.fullmatch(label, re.sub(r"<[^>]*>", "", e.get("label", "")).strip(), re.I):
                    return e
        for pattern in paths:
            for e in candidates:
                if re.search(pattern, e.get("path", ""), re.I):
                    return e
        return None

    def snap(name, expected="", wait=0.8):
        time.sleep(wait)
        rows = elements()
        relevant = [e for e in rows if not e.get("occluded")]
        path = capture("personal-" + name)
        visible = [e["path"] for e in relevant]
        match = any(re.search(expected, s, re.I) for s in visible) if expected else None
        record(screen=name, screenshot=Path(path).name if path else None, expected_path=expected,
               expected_visible=match, controls=[{"path": e.get("path"), "label": e.get("label", ""),
               "kind": e.get("kind")} for e in relevant][:200])
        return rows

    def click(name, entry, wait=.45):
        if entry is None:
            record(action=name, result="No matching visible control")
            return False
        try:
            tool(session, "ui_click", {"path": entry["path"]})
            record(action=name, target=entry["path"], result="Clicked")
        except Exception as exc:
            record(action=name, target=entry["path"], result=str(exc)[:300])
            return False
        time.sleep(wait)
        return True

    def press(name):
        key(name)
        time.sleep(.65)
        record(action="key", key=name)

    def cell_click(x, y, label):
        user32 = ctypes.windll.user32
        target = int(os.environ["AUDIT_PLAYER_PID"])
        callback = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
        windows = []
        @callback
        def choose(hwnd, _):
            pid = wintypes.DWORD()
            bounds = wintypes.RECT()
            user32.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
            if pid.value == target and user32.IsWindowVisible(hwnd) and user32.GetClientRect(hwnd, ctypes.byref(bounds)):
                windows.append((bounds.right * bounds.bottom, hwnd, bounds.right, bounds.bottom))
            return True
        user32.EnumWindows(choose, 0)
        if not windows:
            return False
        _, hwnd, width, height = max(windows, key=lambda item: item[0])
        user32.ShowWindow(hwnd, 5)
        user32.SetForegroundWindow(hwnd)
        point = wintypes.POINT(round(width*x), round(height*y))
        user32.ClientToScreen(hwnd, ctypes.byref(point))
        user32.SetCursorPos(point.x, point.y)
        time.sleep(.45)
        user32.mouse_event(2, 0, 0, 0, 0)
        time.sleep(.18)
        user32.mouse_event(4, 0, 0, 0, 0)
        time.sleep(1.3)
        record(action=label, client_width=width, client_height=height, relative_to_client=[x, y])
        return True

    def has(pattern):
        return any(re.search(pattern, e.get("path", ""), re.I) for e in elements() if not e.get("occluded"))

    def dismiss():
        for _ in range(3):
            state = elements()
            modal_close = find((r"PerformanceIssuePrompt/.*/CloseButton$", r"AltTesterPrefab/AltDialog/Dialog/CloseButton$",
                                r"(?:PhotoDetailUI|BackpackModal|ExplorePanel|ControlsPanel|ScreencaptureHUD|BugReport|EmotesWheel|CreditsTopUpModal).*?/(?:Button_Close|CloseButton|ExitButton)(?:/|$)", r"^PopupCloser/Button$"),
                               row=state)
            if modal_close and click("Close visible client panel", modal_close):
                continue
            if find((r"/ShopButton(?:/|$)", r"/DiscoverButton(?:/|$)", r"/SidebarProfileButton(?:/|$)"),
                    row=state) is not None and not any(
                re.search(r"/(?:PhotoDetailUI|EmotesWheel|ScreencaptureHUD|Settings\(Clone\)|Backpack\(Clone\)|CameraReel\(Clone\)|Shop\(Clone\)|ControlsPanel)(?:/|\()", e.get("path", ""), re.I)
                for e in state if not e.get("occluded")
            ):
                return
            press("ESC")

    def open_hotkey(shortcut, expected, *buttons):
        dismiss()
        if shortcut == "I":
            # The settings and backpack share ExplorePanel's top-level tab bar.
            if click("Open rail gear", find((r"/UpperLayout/SettingsButton$",))):
                click("Open full Settings", find((r"SystemMenu/TopMenuLayout/Settings$",), scope="Sidebar"))
            if click("Open Backpack tab", find((r"/Header/TabSelector/BackpackTab$",))):
                time.sleep(2)
                if has(expected):
                    return True
        press(shortcut)
        for _ in range(3):
            if has(expected):
                return True
            time.sleep(.8)
        if buttons:
            target = find(buttons)
            if target and click(f"Open via HUD after {shortcut} shortcut", target) and has(expected):
                return True
        rail = {
            "I": (r"/SidebarProfileButton/OpenProfileButton$", r"(?:ProfileMenu|SidebarProfileMenu).*?/[^/]*Backpack(?:/|$)", r".*Backpack.*"),
            "P": (r"/SettingsButton$", r"/SystemMenu/.*/Settings(?:/|$)", r"Settings.*"),
            "K": (r"/GalleryButton$", r"/GalleryMenu/.*/Gallery(?:/|$)", r"Gallery.*"),
            "C": (r"/GalleryButton$", r"/(?:GalleryMenu|CameraMenu)/.*/TakeShot(?:/|$)", r".*Take.*(?:Shot|Photo).*"),
            "H": (r"/SettingsButton$", r"/SystemMenu/.*/Controls(?:/|$)", r"Controls.*"),
        }.get(shortcut)
        if rail:
            parent, option, label = rail
            entry = find((option,), (label,), scope="Sidebar")
            if not entry:
                click("Open navigation menu for " + shortcut, find((parent,), scope="Sidebar"))
                entry = find((option,), (label,), scope="Sidebar")
            if click("Open section from navigation menu " + shortcut, entry):
                for _ in range(4):
                    if has(expected):
                        return True
                    time.sleep(.7)
        record(action="open", shortcut=shortcut, result="Expected screen marker not visible", marker=expected)
        return False

    def settings():
        opened = open_hotkey("P", r"(General|Graphics|Sound)SectionButton")
        snap("settings-overview", r"(General|Graphics|Sound)SectionButton")
        if not opened:
            return
        for section in ("General", "Graphics", "Sound", "Controls", "Chat", "Lobby"):
            if not click(f"Open {section} settings", find([section + "SectionButton"], [section, section + "s"], r"settings")):
                continue
            rows = snap("settings-" + section.lower(), section + r"Section/")
            if section in {"Chat", "Controls"}:
                toggle = find((section + r"Section/.*Toggle",), scope="settings", kinds=("toggle",), row=rows)
                if toggle and click("Change " + section + " toggle on disposable QA player", toggle):
                    snap("settings-" + section.lower() + "-changed", section + r"Section/")
                    click("Restore " + section + " toggle", toggle)
            scroll = find((section + r"Section/.+", r"Settings(?:\(Clone\))?/.+"), scope="settings", kinds=("scroll",), row=rows)
            if scroll and section in {"General", "Graphics", "Controls", "Chat"}:
                try:
                    result = tool(session, "ui_scroll", {"path": scroll["path"], "dy": 8})
                    record(action=f"Scroll {section} settings", result=result)
                    snap("settings-" + section.lower() + "-scroll")
                except Exception as exc:
                    record(action="Scroll " + section, result=str(exc)[:250])
        dismiss()

    def backpack():
        opened = open_hotkey("I", r"Backpack.*/(?:AvatarSlot|TabSelector|SearchBar|ToggleOutfits)",
                             r"(?:BackpackButton|BackpackOption)")
        snap("backpack", r"Backpack", 4)
        if not opened:
            return
        for title, target in (("body", "AvatarSlotBody"), ("hair", "AvatarSlotHair"), ("top", "AvatarSlotTop(?:/|$)")):
            if click("Open wearable category " + title, find([target], scope="backpack")):
                snap("wearables-" + title, r"backpack", 2.5)
        rows = elements()
        item = find((r"BackpackItem(?:\(Clone\)|\[\d+\])?(?:/)?$", r"BackpackItem.*(?:MainButton|ItemButton|SelectButton|HoverButton)"),
                    scope="backpack", row=rows)
        if click("Inspect wearable", item):
            snap("wearable-detail", "ItemInfoPanel", 2)
        if click("Open sort options", find([r"/FilterSelector(?:\(Clone\))?(?:/|$)"], [r"Filter\s*&\s*Sort"], "backpack")):
            snap("backpack-filter-sort", "Filter", .8)
            click("Close sort options", find([r"/FilterSelector(?:\(Clone\))?(?:/|$)"], [r"Filter\s*&\s*Sort"], "backpack"))
        search = find((r"SearchBar",), scope="backpack", kinds=("inputField", "input"))
        if search:
            try:
                tool(session, "ui_set_text", {"path": search["path"], "text": "qa-no-such-wearable-1281"})
                snap("backpack-search-empty", "backpack", 1.5)
                tool(session, "ui_set_text", {"path": search["path"], "text": ""})
            except Exception as exc:
                record(action="Backpack search", result=str(exc)[:250])
        if click("Open outfits", find([r"/ToggleOutfits(?:/|$)"], [r"Outfits"], "backpack")):
            snap("backpack-outfits", "Outfit", 1.5)
            click("Close outfits", find([r"/ToggleOutfits(?:/|$)"], [r"Outfits"], "backpack"))
        if click("Open backpack emotes", find([r"(TabSelector|SectionTabs?)/.*Emotes", r"/Emotes/(?:Button|Tab)$"], [r"Emotes"], "backpack")):
            snap("backpack-emotes", r"(SlotContainer|EmoteGrid|Emotes/)", 3)
        dismiss()

    def inventory():
        marker = r"Backpack.*/(?:AvatarSlot|TabSelector|SearchBar|ToggleOutfits)"
        if not open_hotkey("I", marker):
            snap("inventory-unreached")
            return
        click("Open upper-body wearables", find([r"AvatarSlotTop(?:/|$)"], scope="Backpack"))
        snap("inventory-tops-early", marker)
        snap("inventory-tops-30s", marker, 30)
        snap("inventory-tops-60s", marker, 30)
        cell_click(.50,.37,"Select the first upper-body item")
        snap("inventory-wearable-selected", marker)
        cell_click(.423,.180,"Open Saved Outfits")
        snap("inventory-saved-outfits", wait=2)
        if has("Outfit"):
            cell_click(.423,.180,"Close Saved Outfits")
        if not has(r"Backpack.*/(?:TabSelector|AvatarSlot)"):
            press("ESC")
        if not has(marker):
            open_hotkey("I", marker)
        if click("Open Emotes", find([r"TabSelector/.*Emotes"], ["Emotes"], "backpack")):
            snap("inventory-emotes-early", wait=.8)
            snap("inventory-emotes-30s", wait=30)
            snap("inventory-emotes-60s", wait=30)
            cell_click(.50,.37,"Select the first owned emote")
            snap("inventory-emote-selected", wait=2)
        temp = Path(os.environ.get("RUNNER_TEMP","."))
        logs = list(temp.glob("*/Player.log"))
        for log in sorted(logs, key=lambda p:p.stat().st_mtime, reverse=True)[:1]:
            lines=[]
            for line in log.read_text(encoding="utf-8", errors="replace").splitlines():
                if re.search(r"(?i)(assetbundle|thumbnail|wearable|emote|ab-cdn|/content/contents)",line) and re.search(r"(?i)(?:HTTP/1\.[01] [45]\d\d|failed to|cannot|timed out|could not|Curl error|HTTP/2 [45]\d\d|404 Not Found)",line):
                    line=re.sub(r"(?i)(https?://[^\s?'\"<>]+)\?[^\s'\"<>]*",r"\1?[redacted]",line)
                    line=re.sub(r"0x[0-9a-fA-F]{40,}", "[redacted]",line)
                    if not re.search("(?i)(authorization|signature|identity|private.?key|bearer|access.?token)",line) and line not in lines:
                        lines.append(line[:900])
            record(action="Asset request error observations", lines=lines[:35])

    def wheel():
        if open_hotkey("B", "EmoteWheel|EmotesWheel", "EmotesButton"):
            rows = snap("emote-wheel", "Emote.*Wheel", 2)
            slot = find((r"Wheel.*Slot(?:_?1\b|\b)",), scope="Wheel", row=rows)
            if click("Play emote from wheel", slot):
                snap("emote-playing", wait=1)
        else:
            snap("emote-wheel-unreached")
        dismiss()

    def camera():
        opened = open_hotkey("K", "CameraReel|ReelGallery")
        snap("camera-reel-before", "CameraReel|ReelGallery", 3)
        dismiss()
        camera_hud = "ScreencaptureHUD|Button_TakeScreenshot|Button_CameraControls"
        active = open_hotkey("C", camera_hud)
        rows = snap("camera", camera_hud, 1.5)
        if active:
            if click("Open camera controls", find([r"Button_CameraControls"], scope="Screencapture|Camera", row=rows)):
                snap("camera-controls", "Camera", 1)
                click("Close camera controls", find([r"Button_CameraControls"], scope="Camera"))
            if not click("Take in-world photo", find([r"Button_TakeScreenshot"], scope="Camera")):
                press("SPACE")
            snap("camera-after-shutter", wait=3)
        if active:
            press("ESC")
        open_hotkey("K", "CameraReel|ReelGallery")
        rows = snap("camera-reel-after", "CameraReel|ReelGallery", 5)
        thumbnail = find((r"/ThumbnailElement(?:\(Clone\)|\[\d+\])?(?:/|$)", r"ReelThumbnail(?:\(Clone\))?/"), scope="CameraReel|ReelGallery", row=rows)
        detail_open = click("Open captured image", thumbnail)
        if not detail_open and has("CameraReelGallery"):
            # Virtualized thumbnail rows sometimes expose only the surrounding scroll view.
            # Open the first populated gallery cell at the observed normalized player coordinates.
            listing = tool(session, "ui_list", {"stack":"ugui"})["elements"]
            thumbnail = find((r"ThumbnailElement|ReelThumbnail|ImageContainer.*/Image",), scope="CameraReelGallery", row=listing)
            if thumbnail:
                detail_open = click("Open virtualized gallery thumbnail", thumbnail)
            if not detail_open:
                try:
                    result = tool(session, "ui_drag", {"fromX":.094, "fromY":.216, "toX":.094, "toY":.216, "durationFrames":2, "path":"device"})
                    record(action="Click first gallery cell", result=result)
                    time.sleep(1.4)
                    detail_open = has("PhotoDetail")
                except Exception as exc:
                    record(action="Click first gallery cell", result=str(exc)[:180])
        if not detail_open and has("CameraReelGallery"):
            cell_click(.085,.20,"Select first populated gallery thumbnail")
            detail_open = has("PhotoDetail")
        if detail_open:
            snap("photo-detail", "PhotoDetail", 2)
            if click("Copy photo link", find((r"/CopyButton(?:/|$)",), scope="PhotoDetail")):
                clipboard = clipboard_text()
                link = urlsplit(clipboard)
                if link.scheme == "https" and link.netloc in {"reels.dcl.dev", "dcl.dev"} and not link.query:
                    snap("photo-copied-link", "PhotoDetail")
                    try:
                        with urlopen(Request(clipboard, headers={"User-Agent": "Mozilla/5.0"}), timeout=10) as resp:
                            record(action="Visit copied photo link", origin=link.netloc, status=resp.status)
                    except URLError as exc:
                        record(action="Visit copied photo link", origin=link.netloc, error=str(exc)[:200])
                        capture_broken_destination(clipboard, "photo-link")
        else:
            record(screen="photo-detail", result="No enabled gallery thumbnail after shutter attempt", reel_opened=opened, camera_opened=active)
        dismiss()

    def shop():
        dismiss()
        if not click("Open Shop", find((r"/ShopButton(?:\(Clone\))?(?:/|$)", r"/ShopTab(?:/|$)"), ["Shop"])):
            press("TAB")
            click("Open Shop from main navigation", find((r"/ShopTab", r"TabSelector.*Shop"), ["Shop"]))
        rows = snap("shop", "ShopMenu|Shop.*(?:SearchBar|CartButton)|ExploreWearablesBanner", 4)
        if find((r"ExploreWearablesBanner", r"ExploreEmotesBanner"), row=rows):
            if click("Open Credits top-up screen", find((r"/BuyCreditsButton(?:/|$)",), scope="Shop", row=rows)):
                snap("credits-top-up", "TopUp|Credit", 2)
                click("Close credits top-up", find((r"TopUp.*Close",), scope="Credit"))
            for group in ("Wearables", "Emotes"):
                if not has(r"Explore" + group + r"Banner"):
                    click("Reopen shop menu", find((r"/ShopButton(?:/|$)",), ["Shop"]))
                entry = find((r"Explore" + group + "Banner",), scope="Shop|Sidebar")
                if not entry:
                    continue
                url_file = Path(os.environ.get("USERPROFILE", "")) / "AppData/LocalLow/Decentraland/Explorer/auth-url.txt"
                previous = url_file.stat().st_mtime_ns if url_file.exists() else None
                if click("Open marketplace " + group, entry) and url_file.exists() and url_file.stat().st_mtime_ns != previous:
                    target = urlsplit(url_file.read_text(encoding="utf-8-sig").strip())
                    if target.scheme == "https" and target.hostname and ("marketplace" in target.path or target.path.endswith("/items")):
                        public = urlunsplit((target.scheme, target.netloc, target.path, target.query if re.fullmatch(r"category=\w+", target.query) else "", ""))
                        try:
                            with urlopen(Request(public, headers={"User-Agent": "Mozilla/5.0"}), timeout=12) as response:
                                record(action="Marketplace " + group, emitted_url=public, http_status=response.status, content_type=response.headers.get("Content-Type"))
                        except URLError as exc:
                            record(action="Marketplace " + group, emitted_url=public, error=str(exc)[:220])
        else:
            for sec in ("All", "Wearables", "Emotes", "Names", "Favorites"):
                if click("Open shop " + sec, find((r"TabSelector.*" + sec, r"/" + sec + r"Tab(?:/|$)"), [sec], "shop")):
                    rows = snap("shop-" + sec.lower(), "Shop", 3)
                    if sec in ("Wearables", "Emotes"):
                        card = find((r"/ShopItemCard(?:Wide)?(?:\(Clone\))?(?:\[\d+\])?/?$", r"/ShopItemCard.*(?:MainButton|CardButton|InspectButton)"), scope="Shop", row=rows)
                        if click("Open shop item detail", card):
                            snap("shop-" + sec.lower() + "-detail", "ItemPanel")
                            click("Close item details", find((r"ItemPanel.*Close",), scope="Shop"))
            if click("Open cart", find((r"/CartButton(?:/|$)",), scope="Shop")):
                snap("shop-cart", "Cart")
        dismiss()

    def support():
        if section != "faq":
            opened = open_hotkey("H", "ControlsPanel|ControlsView|KeyboardControls")
            snap("controls-help", "Controls", 1)
            if opened:
                dismiss()
        else:
            dismiss()
        for name, pattern, screen in (
            ("audio-menu", r"/Audio(?:Settings)?Button(?:/|$)", "AudioMenu|AudioSlider"),
            ("system-menu", r"/SettingsButton(?:/|$)", "SystemMenu"),
        ):
            if section == "faq" and name != "system-menu":
                continue
            target = find((pattern,), scope="Sidebar")
            if click("Open " + name, target):
                snap(name, screen)
                if name == "system-menu":
                    for label, link_path in (("FAQ", r"/FAQ(?:/|$)"), ("Contact Support", r"/ContactSupport(?:/|$)"), ("Discord", r"/Discord(?:/|$)")):
                        if not has("SystemMenu/.+(FAQ|Discord|ContactSupport)"):
                            click("Reopen system menu", find((r"/SettingsButton(?:/|$)",), scope="Sidebar"))
                        link = find((link_path,), [label], "SystemMenu")
                        if not link:
                            continue
                        file = Path(os.environ.get("USERPROFILE", "")) / "AppData/LocalLow/Decentraland/Explorer/auth-url.txt"
                        previous = file.stat().st_mtime_ns if file.exists() else None
                        if not click("Open " + label, link) or not file.exists() or file.stat().st_mtime_ns == previous:
                            continue
                        destination = urlsplit(file.read_text(encoding="utf-8-sig").strip())
                        if destination.scheme != "https" or destination.hostname not in {"dcl.dev", "docs.dcl.dev", "docs.decentraland.org", "discord.gg"}:
                            continue
                        public = urlunsplit((destination.scheme, destination.netloc, destination.path, "", ""))
                        try:
                            with urlopen(Request(public, headers={"User-Agent": "Mozilla/5.0"}), timeout=10) as resp:
                                record(action="Visit " + label, emitted_url=public, final_url=resp.geturl(), status=resp.status)
                        except URLError as exc:
                            record(action="Visit " + label, emitted_url=public, error=str(exc)[:200])
                            capture_broken_destination(public,"system-" + label.lower().replace(" ", "-"))
                    if not has("SystemMenu/.+(FAQ|Discord|ContactSupport)"):
                        click("Reopen System menu for report form", find((r"/SettingsButton(?:/|$)",), scope="Sidebar"))
                    if click("Open bug report form", find((r"ReportBug", r"BugReport"), ["Report a bug"], "systemmenu")):
                        snap("bug-report-form", "BugReport")
                else:
                    click("Close " + name, find((r"^PopupCloser/Button$",)) or target)
        dismiss()

    section = os.environ.get("AUDIT_SECTION", "").strip().lower()
    drivers = (backpack, wheel, settings, camera, shop, support, inventory)
    if section and section != "faq" and section not in {driver.__name__ for driver in drivers}:
        raise ValueError("Unknown personal screen section " + section)
    for driver in drivers:
        if not section and driver == inventory:
            continue
        if section and section != driver.__name__ and not (section == "backpack" and driver == wheel) and not (section == "faq" and driver == support):
            continue
        try:
            driver()
        except Exception as exc:
            record(section=driver.__name__, result="Could not complete", reason=str(exc)[:320])
            try:
                snap(driver.__name__ + "-interrupted")
            except Exception:
                pass
    return records
