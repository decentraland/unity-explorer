"""Optional two-client QA lane. Both participants are this runner's test fixtures."""
import base64
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import time
from types import SimpleNamespace

import social
import test_account


OCR = """
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Storage.StorageFile, Windows.Storage, ContentType=WindowsRuntime]
$null = [Windows.Graphics.Imaging.BitmapDecoder, Windows.Graphics.Imaging, ContentType=WindowsRuntime]
$null = [Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType=WindowsRuntime]
$method = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
    $_.Name -eq 'AsTask' -and $_.IsGenericMethod -and $_.GetParameters().Count -eq 1 -and
    $_.GetParameters()[0].ParameterType.Name.Split([char]96)[0] -eq 'IAsyncOperation'
} | Select-Object -First 1
function Complete($op, $type) {
    $task = $method.MakeGenericMethod($type).Invoke($null,@($op))
    $task.Wait()
    $task.Result
}
$file = Complete ([Windows.Storage.StorageFile]::GetFileFromPathAsync($env:AUDIT_PHOTO)) ([Windows.Storage.StorageFile])
$stream = Complete ($file.OpenReadAsync()) ([Windows.Storage.Streams.IRandomAccessStreamWithContentType, Windows.Storage.Streams, ContentType=WindowsRuntime])
$decoder = Complete ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
$bitmap = Complete ($decoder.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap, Windows.Graphics.Imaging, ContentType=WindowsRuntime])
$engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()
$recognized = Complete ($engine.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult, Windows.Foundation, ContentType=WindowsRuntime])
$recognized.Text
"""


def words(path):
    # Read only the captured player. Export the test-token matches, never the full OCR text.
    from PIL import Image, ImageOps
    original = Path(path)
    scaled = original.with_suffix(".ocr.bmp")
    try:
        text = []
        with Image.open(original) as image:
            bottom = image.crop((0, int(image.height*.34), int(image.width*.72), image.height))
            for view, factor in ((image, 2), (ImageOps.invert(ImageOps.grayscale(bottom)), 4)):
                view.resize((view.width*factor, view.height*factor), Image.Resampling.LANCZOS).convert("RGB").save(scaled)
                proc = subprocess.run(["powershell.exe", "-NoProfile", "-NonInteractive", "-Command", OCR],
                    capture_output=True, timeout=30, env={**os.environ, "AUDIT_PHOTO":str(scaled.resolve())})
                if proc.returncode == 0:
                    text.append(proc.stdout.decode(errors="replace").lower())
        return re.sub("[^a-z0-9]", "", "".join(text))
    except (OSError, subprocess.TimeoutExpired):
        return ""
    finally:
        scaled.unlink(missing_ok=True)


def capture_full(host, label):
    host.clear_overlays()
    time.sleep(.55)
    name = re.sub("[^a-zA-Z0-9._-]", "-", label)
    raw, _ = host.rpc("tools/call", {"name":"screenshot", "arguments":{"maxWidth":1920, "quality":"png"}}, host.session)
    frame = next(item for item in raw["content"] if item["type"] == "image")
    data = base64.b64decode(frame["data"])
    if not data.startswith(b"\x89PNG\r\n\x1a\n"):
        raise ValueError("Player screenshot did not contain a PNG")
    path = host.out / (name + ".png")
    path.write_bytes(data)
    return path


class Player:
    def __init__(self, host, exe, work, out, port, label):
        self.host, self.port, self.out = host, port, out / label
        self.out.mkdir(exist_ok=True)
        self.session = None
        self.process = None
        self.log = work / ("pair-" + label + ".log")
        args = [str(exe), "-force-d3d11", "-force-driver-type-warp", "-screen-fullscreen", "0",
                "-screen-width", "1920", "-screen-height", "1080", "--resolution", "1920x1080",
                "--windowed-mode", "-logFile", str(self.log),
                "--gateway", "https://dcl.dev", "--multi-instance", "--skip-minimum-specs-screen",
                "--mcp-port", str(port)]
        self.process = subprocess.Popen(args, cwd=work)
        try:
            self.select()
            host.initialize(self.process)
            self.session = host.session
            self.driver = social.Session(self.session, host.tool, host.capture, self.out)
        except BaseException:
            host.stop(self.process)
            raise

    def select(self):
        os.environ.update(AUDIT_MCP_PORT=str(self.port), AUDIT_PLAYER_PID=str(self.process.pid))
        self.host.out = self.out
        self.host.session = self.session

    def world(self, account):
        self.select()
        self.host.wait_world(self.process, False)
        data = self.host.tool(self.session, "get_player_state", {})
        return (data.get("address") or "").lower() == account["address"].lower()

    def shot(self, label):
        self.select()
        return self.host.capture(label)

    def dm(self, account, peer):
        self.select()
        if not self.driver.open_owned_friend((account, peer)):
            return False
        if not self.driver.hover([r"/friendlistuser$"]):
            return False
        if not self.driver.click([r"/friendlistuser/(?:.*/)?chatbutton$"]):
            return False
        end = time.monotonic() + 20
        while time.monotonic() < end:
            paths = [self.driver.path(n) for n in self.driver.elements()]
            has_private = any("privatevoicecall_button" in p or "toolbarviewitem_private" in p for p in paths)
            members = any("/chatdefaulttitlebarview/" in p.replace("[mb] ", "") and "[btn] members" in p for p in paths)
            if has_private and not members:
                self.shot("pair-direct-channel")
                return True
            time.sleep(.8)
        self.shot("pair-channel-not-confirmed")
        return False

    def passport(self, account, peer):
        self.select()
        if not self.driver.open_owned_friend((account, peer)):
            return False
        if not self.driver.click([r"/friendlistuser$"]):
            return False
        arrived = bool(self.driver.choose([r"/passport/.*overviewsectionbutton$"], 10))
        self.driver.snap("pair-peer-passport", r"/passport/.*overviewsectionbutton$", 5)
        self.driver.close(r"/passport")
        return arrived

    def submit(self, message):
        self.select()
        field = self.driver.choose([r"/chatinputbox/custominputfield$"])
        if not field:
            return False
        result = self.host.tool(self.session, "ui_set_text", {"path":field["path"], "text":message, "submit":True})
        return result.get("ok") is not False

    def observe(self, label, expected):
        clean = re.sub("[^a-z0-9]", "", expected.lower())
        for _ in range(3):
            time.sleep(2)
            path = self.shot(label)
            if clean in words(path):
                return True
        return False


def run_clients(exe, work, out):
    host = sys.modules["__main__"]
    previous_capture = host.capture
    host.capture = lambda label: capture_full(host, label)
    out = Path(out)
    report = {"sound":"Hosted runner has no microphone; this lane tests call UI/signaling."}
    current = []
    try:
        pair = social.owns_pair()
        test_account.unity_login(SimpleNamespace(wallets=Path(os.environ["DCL_AUDIT_WALLETS"]),
                                 label="friend", hours=72, slot=1, prefs_dir=None))
        one = Player(host, exe, work, out, 8123, "primary")
        current.append(one)
        two = Player(host, exe, work, out, 8124, "friend")
        current.append(two)
        report["primary_verified"], report["friend_verified"] = one.world(pair[0]), two.world(pair[1])
        if not report["primary_verified"] or not report["friend_verified"]:
            return
        report["online_peer_passport"] = one.passport(*pair)
        first, second = one.dm(*pair), two.dm(*reversed(pair))
        report["private_channel_primary"], report["private_channel_peer"] = first, second
        if not (first and second):
            return
        report["primary_submitted"] = one.submit("QA blue umbrella")
        if report["primary_submitted"]:
            report["peer_saw_blue_umbrella"] = two.observe("pair-message-received", "blue umbrella")
        report["peer_submitted"] = two.submit("QA gold ribbon")
        if report["peer_submitted"]:
            report["primary_saw_gold_ribbon"] = one.observe("pair-reply-received", "gold ribbon")
        one.select()
        report["call_clicked"] = one.driver.click([r"/privatevoicecall_button(?:/button)?$", r"/privatevoicecall_button/.*/button$"])
        time.sleep(2)
        one.shot("pair-call-initiator")
        two.shot("pair-call-recipient")
        accept = two.driver.choose([r"/(?:[^/]*(?:incomingcall|incomingvoice)[^/]*)/.*(?:acceptbutton|accept|answerbutton)$"], 12)
        report["incoming_call_control"] = bool(accept)
        if accept:
            two.shot("pair-call-incoming")
            result = host.tool(two.session, "ui_click", {"path":accept["path"]})
            report["accepted_call"] = result.get("ok") is not False
            time.sleep(4)
            two.shot("pair-call-recipient-after-answer")
            one.shot("pair-call-initiator-after-answer")
            one.driver.click([r"/[^/]*(?:incall|voicechat)[^/]*/.*(?:endcallbutton|hangupbutton|disconnectbutton)$"], 1)
    finally:
        for client in reversed(current):
            client.select()
            client.driver.flush()
            host.stop(client.process)
            host.report_errors(client.log, "pair")
        (out / "pair-report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
        host.capture = previous_capture
