"""Screen-level evidence collection on the hosted CI player using its actual UI."""
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

    def has(pattern):
        return any(re.search(pattern, e.get("path", ""), re.I) for e in elements() if not e.get("occluded"))

    def dismiss():
        for _ in range(3):
            state = elements()
            modal_close = find((r"(?:PhotoDetailUI|BackpackModal|ExplorePanel|ControlsPanel|ScreencaptureHUD|BugReport|EmotesWheel|CreditsTopUpModal).*?/(?:Button_Close|CloseButton)(?:/|$)",),
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
            scroll = find((section + r"Section/.+",), scope="settings", kinds=("scroll",), row=rows)
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
        if click("Open captured image", thumbnail):
            snap("photo-detail", "PhotoDetail", 2)
            if click("Copy photo link", find((r"/CopyButton(?:/|$)",), scope="PhotoDetail")):
                clipboard = subprocess.run(["powershell.exe", "-NoProfile", "-Command", "Get-Clipboard -Raw"],
                                           capture_output=True, text=True, timeout=8).stdout.strip()
                link = urlsplit(clipboard)
                if link.scheme == "https" and link.netloc in {"reels.dcl.dev", "dcl.dev"} and not link.query:
                    try:
                        with urlopen(Request(clipboard, headers={"User-Agent": "Mozilla/5.0"}), timeout=10) as resp:
                            record(action="Visit copied photo link", origin=link.netloc, status=resp.status)
                    except URLError as exc:
                        record(action="Visit copied photo link", origin=link.netloc, error=str(exc)[:200])
                    snap("photo-copied-link", "PhotoDetail")
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
        opened = open_hotkey("H", "ControlsPanel|ControlsView|KeyboardControls")
        snap("controls-help", "Controls", 1)
        if opened:
            dismiss()
        for name, pattern, screen in (
            ("audio-menu", r"/Audio(?:Settings)?Button(?:/|$)", "AudioMenu|AudioSlider"),
            ("system-menu", r"/SettingsButton(?:/|$)", "SystemMenu"),
        ):
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
                            edge = Path(os.environ.get("PROGRAMFILES(X86)", "C:/Program Files (x86)")) / "Microsoft/Edge/Application/msedge.exe"
                            if edge.is_file():
                                # Capture the browser's own error page with no saved auth or browser state.
                                with tempfile.TemporaryDirectory(ignore_cleanup_errors=True) as profile:
                                    dest = (out / ("personal-system-" + label.lower().replace(" ", "-") + "-browser.png")).resolve()
                                    try:
                                        subprocess.run([str(edge), "--headless=new", "--disable-gpu", "--no-first-run",
                                            "--disable-extensions", "--hide-scrollbars", "--window-size=1280,800",
                                            "--user-data-dir=" + profile, "--screenshot=" + str(dest), public],
                                            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=18, check=False)
                                        if dest.is_file():
                                            record(action="Browser error screenshot", emitted_url=public, screenshot=dest.name)
                                    except (OSError, subprocess.TimeoutExpired):
                                        pass
                    if not has("SystemMenu/.+(FAQ|Discord|ContactSupport)"):
                        click("Reopen System menu for report form", find((r"/SettingsButton(?:/|$)",), scope="Sidebar"))
                    if click("Open bug report form", find((r"ReportBug", r"BugReport"), ["Report a bug"], "systemmenu")):
                        snap("bug-report-form", "BugReport")
                else:
                    click("Close " + name, target)
        dismiss()

    for driver in (backpack, settings, camera, shop, support):
        try:
            driver()
        except Exception as exc:
            record(section=driver.__name__, result="Could not complete", reason=str(exc)[:320])
            try:
                snap(driver.__name__ + "-interrupted")
            except Exception:
                pass
    return records
