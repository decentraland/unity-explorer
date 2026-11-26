"""Check the pre-login minimum requirements UI from a fresh CI player installation."""
import ctypes
import json
import os
from pathlib import Path
import subprocess
import time
from urllib.parse import urlsplit

from PIL import ImageGrab
from playwright.sync_api import Error, sync_playwright
import win32gui
import win32process

from lobby import press_screen, screen_words


def run(exe, folder, out):
    ctypes.windll.user32.SetProcessDPIAware()
    player = subprocess.Popen([str(exe), "-force-d3d11", "-force-driver-type-warp", "-screen-fullscreen", "0",
                               "-screen-width", "1920", "-screen-height", "1080", "--resolution", "1920x1080",
                               "--windowed-mode", "--gateway", "https://dcl.dev", "--forceMinimumSpecsScreen", "--alttester",
                               "-logFile", str(folder/"specs-player.log")], cwd=folder)
    os.environ["AUDIT_PLAYER_PID"] = str(player.pid)
    result = {}

    def snapshot(label):
        candidates=[]
        def select(hwnd, _):
            if win32process.GetWindowThreadProcessId(hwnd)[1] == player.pid and win32gui.IsWindowVisible(hwnd):
                candidates.append(hwnd)
        win32gui.EnumWindows(select, None)
        if not candidates: return [], 1, 1
        hwnd = max(candidates,key=lambda h:win32gui.GetClientRect(h)[2])
        _, _, w, h = win32gui.GetClientRect(hwnd)
        if w < 500 or h < 200: return [], 1, 1
        left, top = win32gui.ClientToScreen(hwnd,(0,0))
        shot = ImageGrab.grab(bbox=(left,top,left+w,top+h),all_screens=True)
        dest=out/(label+".png")
        shot.save(dest)
        return screen_words(dest), w, h

    try:
        deadline=time.monotonic()+240
        while time.monotonic()<deadline and player.poll() is None:
            words, w, h=snapshot("specs-screen")
            if any("alttester" in row["text"].casefold() for row in words):
                # The player's instrumentation overlay covers Read More at startup.
                # Use its observed close icon in the 1920x1080 player window.
                press_screen(.5985, .177)
                continue
            read=next((row for row in words if row["text"].casefold()=="read"),None)
            more=next((row for row in words if row["text"].casefold()=="more"),None)
            if read and more and abs(read["y"]-more["y"])<h*.035: break
            time.sleep(3)
        else:
            result["outcome"]="Could not locate Read More in captured player"
            return

        cache=Path(os.environ["USERPROFILE"])/"AppData/LocalLow/Decentraland/Explorer/auth-url.txt"
        old=cache.stat().st_mtime_ns if cache.exists() else None
        press_screen((read["x"]+read["w"]*.5)/w,(read["y"]+read["h"]*.5)/h)
        for _ in range(20):
            if cache.exists() and cache.stat().st_mtime_ns!=old: break
            time.sleep(.5)
        if not cache.exists() or cache.stat().st_mtime_ns==old:
            result["outcome"]="The click produced no launcher bridge URL"
            return
        url=cache.read_text(encoding="utf-8-sig").strip()
        parts=urlsplit(url)
        if parts.scheme!="https" or parts.hostname not in ("dcl.dev","docs.dcl.dev","docs.decentraland.org"):
            raise RuntimeError("Unexpected destination for Read More")
        result["url"]=url
        with sync_playwright() as p:
            browser=p.chromium.launch(channel="msedge", headless=True)
            page=browser.new_page(viewport={"width":1440,"height":900},device_scale_factor=2)
            try:
                response=page.goto(url,wait_until="domcontentloaded",timeout=25000)
                result["status"]=response.status
                if response.ok and parts.fragment:
                    heading = page.locator("[id='" + parts.fragment.replace("'", "") + "']").first
                    heading.wait_for(state="visible",timeout=12000)
                    result["heading"]=heading.inner_text()
                    result["heading_in_view"]=heading.evaluate("(e) => {const r=e.getBoundingClientRect(); return r.bottom>0 && r.top<innerHeight}")
            except Error as exc:
                result["browser_error"]=str(exc).splitlines()[0]
            page.wait_for_timeout(2000)
            result["page_title"]=page.title()
            page.screenshot(path=str(out/"specs-read-more-browser.png"))
            browser.close()
        continue_word=next((row for row in words if row["text"].casefold()=="continue"),None)
        if continue_word:
            press_screen((continue_word["x"]+continue_word["w"]*.5)/w,(continue_word["y"]+continue_word["h"]*.5)/h)
            time.sleep(18)
            snapshot("specs-after-continue")
    finally:
        (out/"specs-result.json").write_text(json.dumps(result,indent=2))
        if player.poll() is None:
            player.terminate()
            player.wait(timeout=35)
