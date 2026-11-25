"""Run the Backpack scenario only on an ephemeral GitHub Actions Windows runner."""

import base64
import ctypes
from datetime import datetime, timezone
from io import BytesIO
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import time
from types import SimpleNamespace
from zipfile import ZipFile

from PIL import Image
import win32api
import win32con
import win32gui
import win32process
import requests

from catalog_frames import catalog_cells, clap_detail, wearable_cards

BASE = Path(__file__).resolve().parent
sys.path[:0] = [str(Path("reference/qa-audit").resolve()),
                str(Path("reference/.claude/skills/pulse-face-to-face/scripts").resolve())]
from audit_input import key
from fixtures import deploy_profile, wallets
from rpc_base import rpc, tool
import test_account

OUT = Path("evidence").resolve()
OUT.mkdir(exist_ok=True)
METRICS = {"renderer": os.environ["CATALOG_RENDERER"], "build_url": os.environ["AUDIT_BUILD_URL"],
           "product_commit": os.environ["PRODUCT_SHA"], "actions_run_id": os.environ["GITHUB_RUN_ID"],
           "utc_start": datetime.now(timezone.utc).isoformat(), "events": [], "pages": {}}
CLOCK = time.monotonic
CLAP = Image.open(BASE / "clap-title-mask.png").convert("L")


def record(**event):
    METRICS["events"].append(event)
    (OUT / "measurements.json").write_text(json.dumps(METRICS, indent=2), encoding="utf-8")


class Player:
    def __init__(self, process):
        self.process = process
        self.session = None
        for _ in range(120):
            if process.poll() is not None:
                raise RuntimeError("Player exited before its automation interface was ready")
            try:
                initialized, self.session = rpc("initialize", {"protocolVersion": "2025-06-18",
                    "capabilities": {}, "clientInfo": {"name": "backpack-emotes-2068", "version": "1"}})
                METRICS["unity_server_info"] = initialized.get("serverInfo", {})
                rpc("notifications/initialized", {}, self.session, notify=True)
                break
            except (OSError, RuntimeError):
                time.sleep(1.5)
        else:
            raise RuntimeError("The player did not initialize")
        version = str(METRICS["unity_server_info"].get("version", ""))
        if not re.search(r"-"+METRICS["product_commit"][:7]+r"(?:[0-9a-f]*$|[-+])", version):
            raise RuntimeError(f"Player-reported build version does not match the expected commit: {version}")

    def call(self, name, **args):
        return tool(self.session, name, args)

    def rows(self):
        return self.call("ui_list", stack="ugui", checkOcclusion=True)["elements"]

    def button(self, pattern, label=None, rows=None):
        rows = self.rows() if rows is None else rows
        return next((r for r in rows if re.search(pattern, r.get("path", ""), re.I) and
                     not r.get("occluded") and (label is None or r.get("label") == label)), None)

    def click(self, pattern, label=None, wait=6):
        until = CLOCK() + wait
        while CLOCK() < until:
            found = self.button(pattern, label)
            if found:
                return self.press_button(found)
            time.sleep(.25)
        raise RuntimeError("Visible control missing: " + pattern + " " + (label or ""))

    def press_button(self, row):
        start = CLOCK()
        response = self.call("ui_click", path=row["path"])
        if response.get("ok") is False:
            raise RuntimeError("Click failed: " + row["path"])
        record(action="ui_click", path=row["path"], label=row.get("label"),
               monotonic_start=start, monotonic_end=CLOCK())
        return start

    def screenshot(self):
        start = CLOCK()
        payload, _ = rpc("tools/call", {"name": "screenshot",
            "arguments": {"maxWidth": 1920, "quality": "png"}}, self.session)
        end = CLOCK()
        block = next(b for b in payload["content"] if b["type"] == "image")
        data = base64.b64decode(block["data"], validate=True)
        image = Image.open(BytesIO(data)).convert("RGB")
        METRICS["native_capture_size"] = list(image.size)
        if image.size != (1920,1080):
            if abs(image.width/image.height - 16/9) > .01:
                (OUT / "unexpected-native-resolution.png").write_bytes(data)
                raise RuntimeError(f"Unexpected player aspect ratio {image.size}")
            # Retain the original PNG bytes while using the same normalized
            # geometry for measurement on a smaller 16:9 hardware desktop.
            image = image.resize((1920,1080),Image.Resampling.LANCZOS)
        return start, end, image, data

    def save_screen(self, label):
        start, end, image, data = self.screenshot()
        (OUT / (label + ".png")).write_bytes(data)
        record(screen=label, monotonic_start=start, monotonic_end=end)
        return image

    def cell(self, x=.50, y=.335):
        if METRICS["renderer"] == "T4":
            # The Azure GPU desktop can keep the Win32 pointer pinned even when
            # Unity renders normally. Ask Unity's own raycaster which card is
            # under the image point, then send it exactly one left click.
            hit = self.call("ui_drag", fromX=x, fromY=y, toX=x, toY=y,
                            path="device", durationFrames=2, rightButton=True)
            target = hit.get("pointerOver", {}).get("start", "")
            start = CLOCK()
            if "/Emotes/FullContainer/" in target and re.search(r"/(?:BackpackItem|BackpackEmoteGridItem)(?:\[|\(|/|$)", target):
                result = self.call("ui_click", path=target)
                record(action="catalog_cell", image_point=[x,y], path=target,
                       click_ok=result.get("ok"), monotonic_start=start, monotonic_end=CLOCK())
            else:
                record(action="catalog_cell_pending", image_point=[x,y], hit=target, monotonic_start=start)
            return start
        windows = []
        def collect(hwnd, _):
            if win32gui.IsWindowVisible(hwnd) and win32process.GetWindowThreadProcessId(hwnd)[1] == self.process.pid:
                _, _, w, h = win32gui.GetClientRect(hwnd)
                if w and h:
                    windows.append((w*h, hwnd, w, h))
        win32gui.EnumWindows(collect, None)
        if not windows:
            raise RuntimeError("Player window missing")
        _, hwnd, width, height = max(windows)
        win32gui.ShowWindow(hwnd, win32con.SW_SHOW)
        # SetForegroundWindow occasionally denies focus on fresh hosted desktops.
        # This is the same foreground request used by the original input driver.
        try:
            win32gui.SetForegroundWindow(hwnd)
        except Exception:
            pass
        xy = (round(width*x), round(height*y))
        win32api.SetCursorPos(win32gui.ClientToScreen(hwnd, xy))
        time.sleep(.18)
        start = CLOCK()
        win32api.mouse_event(win32con.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0)
        time.sleep(.15)
        win32api.mouse_event(win32con.MOUSEEVENTF_LEFTUP, 0, 0, 0, 0)
        time.sleep(.18)
        win32api.SetCursorPos(win32gui.ClientToScreen(hwnd, (round(width*.77), round(height*.82))))
        return start

    def ready(self, address):
        deadline = CLOCK() + 260
        while CLOCK() < deadline:
            if self.process.poll() is not None:
                raise RuntimeError("Player exited during sign-in")
            try:
                rows = self.rows()
            except (RuntimeError, OSError):
                time.sleep(1)
                continue
            overlay = self.button(r"AltDialog/Dialog/CloseButton$|PerformanceIssuePrompt/.*/CloseButton$|(?:tutorial|onboard|tour).*/(?:skip|dismiss|close)Button$", rows=rows)
            if overlay:
                self.press_button(overlay)
                time.sleep(.3)
                continue
            jump = self.button(r"/JumpIn(?:Button)?$", rows=rows)
            if jump:
                self.press_button(jump)
                time.sleep(2)
                continue
            try:
                state = self.call("get_player_state")
            except (RuntimeError, OSError):
                time.sleep(1)
                continue
            if (state.get("address") or "").lower() == address.lower() and self.button(r"/UpperLayout/SettingsButton$", rows=rows):
                scene = self.call("get_scene_state")
                if scene.get("loadingScreenOn") is False:
                    self.save_screen("signed-in-world")
                    return
            time.sleep(1.5)
        raise RuntimeError("Signed-in HUD was not ready")

    def backpack(self):
        if not self.button(r"Backpack/Header/TabSelector/Emotes$"):
            if self.button(r"/UpperLayout/SettingsButton$"):
                self.click(r"/UpperLayout/SettingsButton$")
                self.click(r"SystemMenu/TopMenuLayout/Settings$")
            self.click(r"/Header/TabSelector/BackpackTab$")
        self.click(r"Backpack/Header/TabSelector/Avatar$")

    def page(self, name, start, expected, select=False, deadline=150):
        page = {"requested_icons": expected, "samples": [], "click_seconds": []}
        METRICS["pages"][name] = page
        last_seen = None
        checkpoint_at = iter([2, 5, 10, 20, 30, 60, 90, 120, 150, 180, 210])
        checkpoint = next(checkpoint_at, None)
        last_click = -5
        stale_details = False
        while CLOCK() - start < deadline:
            a, b, img, raw = self.screenshot()
            lo, hi = round(a-start, 3), round(b-start, 3)
            icons, labels = catalog_cells(img)
            details = clap_detail(img, CLAP)
            sample = {"request_s": lo, "received_s": hi, "icon_slots": icons,
                      "centered_text_slots": labels, "clap_detail": details}
            pages = self.rows()
            sample["pagination"] = sorted(r.get("label") for r in pages if "/Emotes/FullContainer/PageSelector/PagesContainer/PageButton" in r.get("path", "") and not r.get("occluded"))
            page["samples"].append(sample)
            state = (tuple(icons), tuple(labels), details, tuple(sample["pagination"]))
            keep = last_seen != state
            if checkpoint is not None and hi >= checkpoint:
                keep = True
                while checkpoint is not None and hi >= checkpoint:
                    checkpoint = next(checkpoint_at, None)
            if keep:
                filename = f"{name}-{len(page['samples']):03d}-{hi:06.2f}s.png"
                (OUT / filename).write_bytes(raw)
                sample["screenshot"] = filename
            last_seen = state
            if sample["pagination"] == ["1", "2"] and "two_page_controls_s" not in page:
                page["two_page_controls_s"] = round(CLOCK()-start, 3)
            if icons and "first_icon_by_s" not in page:
                page["first_icon_by_s"] = hi
            if (icons or labels) and "first_name_or_icon_by_s" not in page:
                page["first_name_or_icon_by_s"] = hi
            if sorted(icons + labels) == list(range(1, expected+1)) and "all_named_or_pictured_by_s" not in page:
                page["all_named_or_pictured_by_s"] = hi
            if icons == list(range(1, expected+1)) and "full_icons_by_s" not in page:
                page["full_icons_by_s"] = hi
                (OUT / (name+"-full.png")).write_bytes(raw)
            if select and details and page["click_seconds"] and "clap_details_by_s" not in page:
                page["clap_details_by_s"] = hi
                (OUT / (name+"-clap.png")).write_bytes(raw)
            record(page=name, sample=sample)
            if "full_icons_by_s" in page and "two_page_controls_s" in page and (not select or "clap_details_by_s" in page):
                break
            if select and details and not page["click_seconds"] and not stale_details:
                # A reopened inspector can retain the last item. Require a real
                # change back to Clap before attributing the result to this click.
                stale_details = True
                page["cleared_preexisting_details_at_s"] = round(self.cell(.579,.335)-start, 3)
            if select and not details and CLOCK()-start-last_click >= 2.5:
                last_click = self.cell()-start
                page["click_seconds"].append(round(last_click, 3))
            time.sleep(.45 if hi<12 else 1.4)
        if page["samples"] and "full_icons_by_s" not in page:
            (OUT / (name+"-last-observation.png")).write_bytes(raw)
            page["last_observation_s"] = hi
        print(name, {k:v for k,v in page.items() if k!="samples"}, flush=True)
        return page

    def suite(self, log):
        self.backpack()
        start = self.click(r"AvatarSlotTop/Button$")
        for sec in (1, 30):
            time.sleep(max(0, start+sec-CLOCK()))
            image = self.save_screen(f"wearables-upper-{sec}s")
            record(control="upper-body", observation_by_s=round(CLOCK()-start, 3), icon_slots=wearable_cards(image))
        start = self.click(r"Backpack/Header/TabSelector/Emotes$")
        cold = self.page("cold-page1", start, 16, select=True, deadline=210)
        if self.button(r"/Emotes/FullContainer/PageSelector/PagesContainer/PageButton", "2"):
            if "full_icons_by_s" not in cold and cold["samples"]:
                missing = sorted(set(range(1,17)) - set(cold["samples"][-1]["icon_slots"]))
                if missing:
                    slot = missing[0]-1
                    self.cell(.50+.079*(slot%4), .335+.144*(slot//4))
                    time.sleep(4)
                    self.save_screen(f"selected-card-without-silhouette-slot-{slot+1}")
            # Capture the real avatar through several frames following a fresh Clap selection.
            cry_start = self.cell(.579, .335)
            # Cry is a configured base emote that the prepared account never equipped.
            # Leave time for its first animation download. Retain individual poses.
            for i, offset in enumerate((.5, 1.3, 3, 5, 8, 15, 25)):
                time.sleep(max(0, cry_start + offset - CLOCK()))
                self.save_screen(f"unequipped-cry-{i+1}")
            self.cell()
            for i in range(5):
                self.save_screen(f"clap-animation-{i+1}")
                time.sleep(.3)
            if not METRICS["product_commit"].startswith("7308da1"):
                # The visible base catalog can finish before the owned provider.
                # Keep this request alive long enough to measure finalization.
                try:
                    with log.open(encoding="utf-8", errors="replace") as player_log:
                        while True:
                            final = next((line for line in player_log if "[BackpackEmoteTiming]" in line
                                          and " page=1 " in line and " stage=catalog-complete " in line), None)
                            if final or CLOCK() - start >= 210:
                                break
                            time.sleep(1)
                    record(cold_page_catalog_finalization_observed=bool(final), observation_by_s=round(CLOCK()-start, 3))
                except OSError as exc:
                    record(live_player_log_read_error=type(exc).__name__)
            start = self.click(r"/Emotes/FullContainer/PageSelector/PagesContainer/PageButton", "2")
            self.page("cold-page2", start, 5)
            self.cell()
            time.sleep(1)
            self.save_screen("page2-first-item-selected")
            start = self.click(r"/Emotes/FullContainer/PageSelector/PagesContainer/PageButton", "1")
            self.page("cached-page1", start, 16, deadline=60)
            self.cell(.579, .335)
            time.sleep(1)
            self.save_screen("other-emote-before-warm-reopen")
        self.click(r"Backpack/Header/TabSelector/Avatar$")
        self.click(r"ExplorePanelUI/AnimationContainer/Header/CloseButton$")
        self.backpack()
        start = self.click(r"Backpack/Header/TabSelector/Emotes$")
        self.page("warm-reopen-page1", start, 16, select=True, deadline=90)
        try:
            self.click(r"ExplorePanelUI/AnimationContainer/Header/CloseButton$")
            key("B")
            time.sleep(.6)
            if not self.button(r"EmotesWheelHUD/Content/Slot1/Container/RarityContainer$"):
                self.click(r"/SidebarView/BottomLayout/EmotesButton$")
                time.sleep(.6)
            self.save_screen("wheel-open")
            self.click(r"EmotesWheelHUD/Content/Slot1/Container/RarityContainer$")
            for i in range(3):
                self.save_screen(f"wheel-clap-{i+1}")
                time.sleep(.2)
        except RuntimeError as exc:
            record(wheel_error=str(exc))
        for name in ("cold-page1", "cold-page2", "warm-reopen-page1"):
            if "full_icons_by_s" not in METRICS["pages"].get(name, {}):
                raise RuntimeError("Did not capture all expected thumbnails on " + name)
        for name in ("cold-page1", "warm-reopen-page1"):
            if "clap_details_by_s" not in METRICS["pages"][name]:
                raise RuntimeError("Did not observe Clap details on " + name)


def renderer_and_timings(log):
    lines = []
    timing = []
    if log.exists():
        for line in log.read_text(encoding="utf-8", errors="replace").splitlines():
            if re.search(r"^\s*(?:Renderer:|Vendor:|VRAM:|Driver:)", line):
                lines.append(line[:700])
            if "[BackpackEmoteTiming]" in line and not re.search(r"(?i)(token=|signature|0x[0-9a-f]{40}|https?://.*?\?)", line):
                timing.append(line[line.index("[BackpackEmoteTiming]"):][:800])
    (OUT/"player-renderer-and-timings.txt").write_text("\n".join(lines), encoding="utf-8")
    (OUT/"backpack-emote-timings.txt").write_text("\n".join(timing), encoding="utf-8")
    METRICS["native_timing_records"] = len(timing)
    return lines


def wait_for_archive(url):
    # CI may finish installing the runner while the official build is still
    # uploading. Verify the source returns an actual ZIP before downloading it.
    until = CLOCK()+720
    separator = "&" if "?" in url else "?"
    while CLOCK()<until:
        probe = f"{url}{separator}qa-probe={os.environ['GITHUB_RUN_ID']}-{int(CLOCK())}"
        try:
            with requests.get(probe,headers={"Range":"bytes=0-3","User-Agent":"curl/8.9"},timeout=12,stream=True) as r:
                if r.status_code in (200,206) and r.raw.read(4)==b"PK\x03\x04":
                    record(official_zip_available_utc=datetime.now(timezone.utc).isoformat(),
                           archive_etag=r.headers.get("ETag"), archive_content_range=r.headers.get("Content-Range"))
                    return
        except requests.RequestException:
            pass
        time.sleep(10)
    raise RuntimeError("The exact official Windows archive did not become available")


def main():
    assert os.environ.get("GITHUB_ACTIONS") == "true"
    assert os.environ["CATALOG_RENDERER"] in ("WARP", "T4")
    url = os.environ["AUDIT_BUILD_URL"]
    sha = os.environ["PRODUCT_SHA"]
    assert re.fullmatch(r"[0-9a-f]{40}", sha)
    assert url.startswith("https://explorer-artifacts.decentraland.org/@dcl/unity-explorer/branch/")
    assert re.search(r"/(?:pr|wd)-\d+-"+sha[:7]+r"/Decentraland_windows64\.zip(?:\?ci=\d+)?$", url)
    frequency, counter = ctypes.c_longlong(), ctypes.c_longlong()
    ctypes.windll.kernel32.QueryPerformanceFrequency(ctypes.byref(frequency))
    ctypes.windll.kernel32.QueryPerformanceCounter(ctypes.byref(counter))
    METRICS["clock"] = {"windows_counter_hz": frequency.value, "windows_counter": counter.value,
                        "python_monotonic_s": CLOCK(), "python_clock": time.get_clock_info("monotonic").implementation}
    try:
        for i in range(1000):
            mode = win32api.EnumDisplaySettings(None, i)
            if (mode.PelsWidth, mode.PelsHeight, mode.BitsPerPel) == (1920, 1080, 32):
                win32api.ChangeDisplaySettings(mode, 0)
                break
    except Exception:
        pass
    with tempfile.TemporaryDirectory(dir=os.environ["RUNNER_TEMP"]) as temp:
        root = Path(temp)
        archive = root/"player.zip"
        wait_for_archive(url)
        subprocess.run(["curl.exe", "-fsSL", "--retry", "3", "--max-time", "500", "-o", str(archive), url], check=True)
        with archive.open("rb") as stream:
            from hashlib import file_digest
            METRICS["windows_zip_sha256"] = file_digest(stream, "sha256").hexdigest()
        with ZipFile(archive) as z:
            z.extractall(root)
        prefs = test_account.default_prefs_dir() / "userdata_0.json"
        if prefs.exists() and test_account.read_prefs(prefs).get("Strings", {}).get(test_account.IDENTITY_KEY):
            raise RuntimeError("Runner already has an identity. The suite requires a disposable profile.")
        primary, _ = wallets(root)
        deploy_profile(primary)
        test_account.unity_login(SimpleNamespace(wallets=Path(os.environ["DCL_AUDIT_WALLETS"]), label="primary", hours=48, slot=0, prefs_dir=None))
        log = root/"Player.log"
        args = [str(root/"Decentraland.exe"), "-force-d3d11"]
        if METRICS["renderer"] == "WARP":
            args.append("-force-driver-type-warp")
        args.extend(["-screen-fullscreen","0","-screen-width","1920","-screen-height","1080","--resolution","1920x1080","--windowed-mode","-logFile",str(log),
                     "--gateway","https://dcl.dev","--skip-minimum-specs-screen","--mcp-port","8123","--alttester"])
        p = subprocess.Popen(args, cwd=root)
        os.environ["AUDIT_PLAYER_PID"] = str(p.pid)
        player = None
        try:
            player = Player(p)
            player.ready(primary["address"])
            player.suite(log)
        except Exception:
            if player is not None and p.poll() is None:
                try:
                    player.save_screen("last-screen")
                except Exception:
                    pass
            raise
        finally:
            if p.poll() is None:
                p.terminate()
                p.wait(timeout=35)
            lines = renderer_and_timings(log)
            record(renderer_lines=[s for s in lines if "Renderer:" in s], utc_end=datetime.now(timezone.utc).isoformat())
        adapter = r"Tesla T4|NVIDIA T4" if METRICS["renderer"] == "T4" else r"Microsoft Basic Render Driver|Microsoft WARP"
        if not any(re.search(r"Renderer:.*(?:"+adapter+r")", line, re.I) for line in lines):
            raise RuntimeError("Unity did not report the expected visual adapter")
        if not sha.startswith("7308da1") and not METRICS["native_timing_records"]:
            raise RuntimeError("No catalog timing records were emitted by the repaired player")


if __name__ == "__main__":
    main()
