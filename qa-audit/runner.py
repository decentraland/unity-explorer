"""Hosted CI player navigation. Only --gateway selects the deployment."""
import base64
import importlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import time
from urllib.parse import urlsplit
from zipfile import ZipFile
import traceback

sys.path.insert(0,str(Path(".claude/skills/pulse-face-to-face/scripts").resolve()))
from rpc_base import rpc,tool
from fixtures import wallets,deploy_profile
from browser_wallet import wallet_handoff
import test_account
from types import SimpleNamespace

slice_name=os.environ["AUDIT_SLICE"]
out=Path("evidence")/slice_name
out.mkdir(parents=True,exist_ok=True)
session=None

def capture(label):
    label=re.sub(r"[^a-zA-Z0-9._-]","-",label)
    result,_=rpc("tools/call",{"name":"screenshot","arguments":{"maxWidth":1280,"quality":"jpg"}},session)
    image=next((b for b in result["content"] if b["type"]=="image"),None)
    if image is None: raise RuntimeError("Player screenshot: "+str(result.get("content"))[:260])
    path=out/(label+".jpg")
    path.write_bytes(base64.b64decode(image["data"]))
    return path

def rows():
    return tool(session,"ui_list",{"stack":"ugui"})["elements"]

def paths(name,els):
    (out/(name+"-controls.json")).write_text(json.dumps([{k:v for k,v in e.items() if k in ("kind","path","label","interactable")} for e in els],indent=1))

def button(els,token,kind="button"):
    return next((e for e in els if token.lower() in e.get("path","").lower() and e.get("kind")==kind and e.get("interactable",True)),None)

def click(e):
    tool(session,"ui_click",{"path":e["path"]})
    time.sleep(1.3)

def initialize(process):
    global session
    for _ in range(100):
        if process.poll() is not None: raise RuntimeError("Player exited before MCP")
        try:
            _,session=rpc("initialize",{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"screen-audit","version":"1"}})
            rpc("notifications/initialized",{},session,notify=True)
            return
        except (OSError,RuntimeError): time.sleep(2)
    raise RuntimeError("Player did not initialize MCP")

def launch(exe,folder,log):
    args=[str(exe),"-force-d3d11","-force-driver-type-warp","-screen-fullscreen","0","-screen-width","1280","-screen-height","720","-logFile",str(log),
          "--gateway","https://dcl.dev","--skip-minimum-specs-screen","--mcp-port","8123","--alttester"]
    process=subprocess.Popen(args,cwd=folder)
    os.environ["AUDIT_PLAYER_PID"]=str(process.pid)
    initialize(process)
    return process

def stop(process):
    if process.poll() is None: process.terminate()
    process.wait(timeout=35)

def wallet_login(process,acct):
    urlfile=Path(os.environ["USERPROFILE"])/"AppData/LocalLow/Decentraland/Explorer/auth-url.txt"
    seen=set()
    for _ in range(75):
        els=rows()
        if urlfile.exists():
            url=urlfile.read_text(encoding="utf-8-sig").strip()
            assert urlsplit(url).netloc=="dcl.dev" and "bridgeOnly" in url
            capture("auth-wallet-pending")
            wallet_handoff(url,acct,out)
            return
        for token,shot in (("Metamask","auth-wallet-choices"),("LogInOrSignup","auth-welcome"),("MoreOptions","auth-login-methods")):
            match=button(els,token)
            if match and token not in seen:
                capture(shot); paths(shot,els); click(match); seen.add(token); break
        time.sleep(1)
    capture("auth-wait");paths("auth-wait",rows())
    raise RuntimeError("Wallet handoff was not reached")

def wait_world(process,auth_only):
    seen=set()
    good=0
    for i in range(120):
        if process.poll() is not None: raise RuntimeError("Player exited during sign-in")
        els=rows()
        keys=[e.get("path","").lower() for e in els]
        if any("selectavatar" in k and "continue" in k for k in keys):
            if "avatar" not in seen:
                capture("onboarding-avatar"); paths("onboarding-avatar",els); seen.add("avatar")
                cont=button(els,"ContinueButton")
                if cont: click(cont)
        elif any("terms" in k and "toggle" in k for k in keys):
            capture("onboarding-profile");paths("onboarding-profile",els)
            if auth_only: return "wallet-authenticated; new-account profile editor"
            raise RuntimeError("Prepared profile was not found by Unity")
        elif any("jumpin" in k for k in keys):
            jump=button(els,"JumpIn")
            if jump and "lobby" not in seen:
                capture("existing-account-lobby");paths("existing-account-lobby",els);click(jump);seen.add("lobby")
        else:
            s=tool(session,"get_player_state",{})
            if s.get("address") and not any("authenticationscreen" in k for k in keys):
                good+=1
                if good>=3:
                    capture("world-entry");paths("world-entry",els)
                    return "signed-in world"
            else: good=0
        if i in (8,32,65,105):
            capture("progress-"+str(i));paths("progress-"+str(i),els)
        time.sleep(1.4)
    capture("world-entry-timeout");paths("world-entry-timeout",rows())
    raise RuntimeError("World or onboarding not reached")

def report_errors(log, name):
    if not log.exists(): return
    lines=set()
    for line in log.read_text(encoding="utf-8",errors="replace").splitlines():
        if not re.search(r"\b(?:HTTP/[12.]+ [45]\d\d|[A-Za-z.]{2,}Exception\b|Cannot resolve|failed to (?:load|download|fetch)|Could not resolve host|response code: [45]\d\d)",line,re.I): continue
        if any(part in line.lower() for part in ("signature","ephemeral","authchain","identityid","privatekey","token=","bearer","stacktrace")): continue
        line=re.sub(r"0x[0-9a-fA-F]{40,}|[0-9a-fA-F]{8}-[0-9a-fA-F-]{27,}", "<id>", line)
        line=re.sub(r"\?[^\s)]+","?<query>",line)
        lines.add(line[:280])
    (out/(name+"-errors.txt")).write_text("\n".join(sorted(lines)[:240]),encoding="utf-8")

def main():
    with tempfile.TemporaryDirectory(dir=os.environ["RUNNER_TEMP"]) as td:
        root=Path(td); zip_path=root/"windows.zip";exe=root/"Decentraland.exe"
        subprocess.run(["curl.exe","-fsSL","--retry","3","--max-time","260","-o",str(zip_path),os.environ["AUDIT_BUILD_URL"]],check=True)
        with ZipFile(zip_path) as z:z.extractall(root)
        primary,friend=wallets(root)
        if slice_name!="auth": deploy_profile(primary)
        if slice_name=="social": deploy_profile(friend)
        if slice_name!="auth":
            test_account.unity_login(SimpleNamespace(wallets=Path(os.environ["DCL_AUDIT_WALLETS"]),label="primary",hours=72,slot=0,prefs_dir=None))
        log=root/"Player.log"
        p=launch(exe,root,log)
        try:
            if slice_name=="auth": wallet_login(p,primary)
            result=wait_world(p,slice_name=="auth")
            (out/"login-result.txt").write_text(result)
            print("Authenticated state:",result,flush=True)
            if slice_name!="auth":
                importlib.import_module(slice_name).run(session,tool,capture,out)
        except Exception:
            try: capture("last-screen"); paths("last-screen",rows())
            except Exception: pass
            raise
        finally:
            stop(p);report_errors(log,"player")

if __name__=="__main__":
    main()
