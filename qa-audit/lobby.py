"""Inspect the optional native lobby and its layout editor in the downloaded player."""
import json
import os
import re
import subprocess
import time

import win32api
import win32con
import win32gui
import win32process
from PIL import Image

from audit_input import key


OCR = r"""
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null=[Windows.Storage.StorageFile, Windows.Storage, ContentType=WindowsRuntime]
$null=[Windows.Graphics.Imaging.BitmapDecoder, Windows.Graphics.Imaging, ContentType=WindowsRuntime]
$null=[Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType=WindowsRuntime]
$async=[System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
    $_.Name -eq 'AsTask' -and $_.IsGenericMethod -and $_.GetParameters().Count -eq 1 -and
    $_.GetParameters()[0].ParameterType.Name.Split([char]96)[0] -eq 'IAsyncOperation'
} | Select-Object -First 1
function Complete($op,$type) {
    $task=$async.MakeGenericMethod($type).Invoke($null,@($op))
    $task.Wait()
    $task.Result
}
$file=Complete ([Windows.Storage.StorageFile]::GetFileFromPathAsync($env:LOBBY_SCREEN)) ([Windows.Storage.StorageFile])
$stream=Complete ($file.OpenReadAsync()) ([Windows.Storage.Streams.IRandomAccessStreamWithContentType])
$decoder=Complete ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
$bitmap=Complete ($decoder.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])
$engine=[Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()
$result=Complete ($engine.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult])
$words=foreach($line in $result.Lines) { foreach($word in $line.Words) {
    $r=$word.BoundingRect
    [pscustomobject]@{text=$word.Text; x=$r.X; y=$r.Y; w=$r.Width; h=$r.Height}
}}
ConvertTo-Json -InputObject @($words) -Compress
"""


def screen_words(path):
    result = subprocess.run(["powershell.exe", "-NonInteractive", "-NoProfile", "-Command", OCR],
                            env={**os.environ, "LOBBY_SCREEN": str(path.resolve())},
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=50, check=True)
    return json.loads(result.stdout.decode(errors="replace"))


def press_screen(x, y):
    windows = []
    pid = int(os.environ["AUDIT_PLAYER_PID"])
    def find(hwnd, _):
        if win32gui.IsWindowVisible(hwnd) and win32process.GetWindowThreadProcessId(hwnd)[1] == pid:
            windows.append(hwnd)
    win32gui.EnumWindows(find, None)
    window = max(windows, key=lambda hwnd: win32gui.GetClientRect(hwnd)[2])
    _, _, width, height = win32gui.GetClientRect(window)
    target = win32gui.ClientToScreen(window, (round(x*width), round(y*height)))
    try:
        win32gui.SetForegroundWindow(window)
    except OSError:
        pass
    win32api.SetCursorPos(target)
    time.sleep(0.7)
    win32api.mouse_event(win32con.MOUSEEVENTF_LEFTDOWN, 0, 0)
    time.sleep(1.0)
    win32api.mouse_event(win32con.MOUSEEVENTF_LEFTUP, 0, 0)
    time.sleep(2.5)


def run(session, tool, capture, out):
    observations = []
    def record(label):
        path = capture(label)
        with Image.open(path) as frame:
            width, height = frame.size
        words = screen_words(path)
        observations.append({"screen": label, "words": words})
        (out/"lobby-observations.json").write_text(json.dumps(observations, indent=1))
        return words, width, height

    def word(words, label, max_y):
        return next((w for w in words if w["text"].strip(".,:[]").lower() == label and w["y"] < max_y), None)

    def click_word(entry, width, height, offset=0):
        press_screen((entry["x"] + entry["w"]*.5 + offset) / width, (entry["y"] + entry["h"]*.5) / height)

    next_open = 0
    deadline = time.monotonic() + 270
    while time.monotonic() < deadline:
        rows = tool(session,"ui_list",{"stack":"ugui"})["elements"]
        button = next((r for r in rows if r.get("kind")=="button" and r.get("interactable", True)
                       and "settings" not in r["path"].lower() and
                       ((r.get("label") or "").strip().lower() == "lobby" or
                        re.search(r"/(?:GoTo|Open)?Lobby(?:Button|Tab)?$",r["path"],re.I))), None)
        if button:
            tool(session, "ui_click", {"path":button["path"]})
            time.sleep(4)
        words, width, height = record("lobby-entry")
        title = word(words, "lobby", height*.15)
        if title and word(words, "places", height*.18) and word(words, "events", height*.18):
            break
        if time.monotonic() >= next_open:
            gear = next((r for r in rows if r["path"].endswith("/UpperLayout/SettingsButton")), None)
            settings = next((r for r in rows if r["path"].endswith("/SystemMenu/TopMenuLayout/Settings")), None)
            if not button and (gear or settings):
                if not settings:
                    tool(session, "ui_click", {"path":gear["path"]})
                    time.sleep(2)
                    rows=tool(session, "ui_list", {"stack":"ugui"})["elements"]
                    settings=next((r for r in rows if r["path"].endswith("/SystemMenu/TopMenuLayout/Settings")),None)
                if settings:
                    tool(session, "ui_click", {"path":settings["path"]})
                    time.sleep(2)
                    rows=tool(session,"ui_list",{"stack":"ugui"})["elements"]
                    (out/"explore-buttons.json").write_text(json.dumps([r for r in rows if r.get("kind")=="button" and ("Header" in r["path"] or "Lobby" in r["path"])]))
                    capture("lobby-explore-menu")
            next_open=time.monotonic()+18
        time.sleep(4)
    else:
        raise RuntimeError("No visible Lobby heading with its navigation controls")
    time.sleep(40)
    words, width, height = record("lobby-settled")
    title=word(words, "lobby", height*.15)
    if title:
        # The pencil is anchored to the right of the Lobby title in LobbyTopBar.uss.
        for dx in (title["w"]*.5 + title["h"]*1.45, title["w"]*.5 + title["h"]*2.15):
            click_word(title, width, height, dx)
            editor, _, _ = record("lobby-edit-layout")
            cancel = word(editor, "cancel", height)
            if cancel:
                click_word(cancel, width, height)
                record("lobby-layout-cancel")
                break
    words, width, height = record("lobby-before-places")
    places = word(words, "places", height*.13)
    if places:
        click_word(places, width, height)
        time.sleep(12)
        record("lobby-places")
        key("ESC")
        time.sleep(4)
        record("lobby-return")
