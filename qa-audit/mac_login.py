"""Smoke-test the signed PR macOS download on a hosted Mac."""
import base64
import json
import os
from pathlib import Path
import plistlib
import struct
import subprocess
import tempfile
import time
from rpc_base import rpc,tool

out=Path("evidence/macos").resolve()
out.mkdir(parents=True,exist_ok=True)
def run():
    with tempfile.TemporaryDirectory(dir=os.environ["RUNNER_TEMP"]) as temp:
        folder=Path(temp)
        archive=folder/"player.zip"
        subprocess.run(["curl","-fsSL","--retry","3","-o",str(archive),os.environ["AUDIT_BUILD_URL"]],check=True)
        subprocess.run(["ditto","-x","-k",str(archive),str(folder)],check=True)
        package=folder/"build.tar"
        if package.is_file():
            subprocess.run(["tar","-xf",str(package),"-C",str(folder)],check=True)
        apps=[a for a in folder.rglob("*.app") if (a/"Contents/Info.plist").is_file() and (a/"Contents/MacOS").is_dir()]
        if not apps:
            raise RuntimeError(f"Mac archive layout: {list(p.name for p in folder.iterdir())[:15]}")
        app=min(apps,key=lambda p:len(str(p)))
        info=plistlib.loads((app/"Contents/Info.plist").read_bytes())
        exe=app/"Contents/MacOS"/info["CFBundleExecutable"]
        log=folder/"Player.log"
        proc=subprocess.Popen([str(exe),"--gateway","https://dcl.dev","--resolution","1920x1080","--windowed-mode","--skip-minimum-specs-screen","--mcp-port","8123","-logFile",str(log)])
        try:
            session=None
            for _ in range(100):
                if proc.poll() is not None: raise RuntimeError("macOS player exited")
                try:
                    _,session=rpc("initialize",{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"mac-login-qa","version":"1"}})
                    rpc("notifications/initialized",{},session,notify=True)
                    break
                except (OSError,RuntimeError): time.sleep(2)
            if not session: raise RuntimeError("Unity MCP did not start")
            captured=set()
            for _ in range(100):
                rows=tool(session,"ui_list",{"stack":"ugui"})["elements"]
                options=[("login-methods","Metamask.Button"),("welcome","LogInOrSignup")]
                for label,end in options:
                    target=next((e for e in rows if e.get("path","").endswith(end)),None)
                    if not target: continue
                    if label not in captured:
                        res,_=rpc("tools/call",{"name":"screenshot","arguments":{"maxWidth":1920,"quality":"png"}},session)
                        block=next(b for b in res["content"] if b["type"]=="image")
                        raw=base64.b64decode(block["data"])
                        assert raw[:8]==bytes.fromhex("89504e470d0a1a0a")
                        print("Captured",label,struct.unpack(">II",raw[16:24]),flush=True)
                        (out/(label+".png")).write_bytes(raw)
                        captured.add(label)
                    if label=="login-methods": return
                    tool(session,"ui_click",{"path":target["path"]})
                    break
                time.sleep(2)
            raise RuntimeError("Could not reach the login methods on macOS")
        finally:
            proc.terminate()
            try: proc.wait(timeout=20)
            except subprocess.TimeoutExpired: proc.kill();proc.wait()
            if log.exists() and not (out/"login-methods.png").is_file():
                for line in log.read_text(errors="replace").splitlines():
                    if any(x in line.lower() for x in ("failed to initialize graphics","no graphics device","creating metal","mcp server","metal gpu")): print(line[:220])
run()
