"""Follow the native provider button to the public site's first interactive screen."""
from pathlib import Path
from urllib.parse import parse_qs,urlsplit
import json
import os
import time
from playwright.sync_api import sync_playwright

def run(host,name):
    file=Path(os.environ["USERPROFILE"])/"AppData/LocalLow/Decentraland/Explorer/auth-url.txt"
    selected=False
    expanded=False
    clicked=0
    for _ in range(100):
        items=host.rows()
        if name=="otpform":
            if selected:
                if time.monotonic()-clicked>22:
                    host.capture("email-after-submit")
                    host.paths("email-after-submit",items)
                    return
                time.sleep(1)
                continue
            field=next((e for e in items if e.get("kind")=="inputField" and "EmailInputField" in e.get("path","")),None)
            if field:
                host.tool(host.session,"ui_set_text",{"path":field["path"],"text":"unity-gateway-qa@example.invalid"})
                host.capture("email-valid-format")
                items=host.rows()
                next_button=next((e for e in items if e.get("kind")=="button" and ("Email" in e.get("path","") and e.get("label","").lower()=="next")),None)
                if next_button: host.click(next_button)
                else: host.tool(host.session,"ui_set_text",{"path":field["path"],"text":"unity-gateway-qa@example.invalid","submit":True})
                selected=True;clicked=time.monotonic()
                continue
        if selected and file.is_file():
            url=file.read_text(encoding="utf-8-sig").strip()
            parts=urlsplit(url)
            assert parts.scheme=="https" and parts.netloc=="dcl.dev"
            query=parse_qs(parts.query)
            with sync_playwright() as service:
                browser=service.chromium.launch(channel="msedge")
                page=browser.new_page(viewport={"width":1440,"height":900},device_scale_factor=2)
                response=page.goto(url,wait_until="networkidle",timeout=45000)
                page.screenshot(path=str(host.out/"provider-browser.png"))
                details={"native_button":name,"query_method":query.get("loginMethod"),"origin":parts.netloc,"status":response.status,"visible_text":page.locator("body").inner_text()[:650]}
                (host.out/"provider.json").write_text(json.dumps(details))
                browser.close()
            return
        if not selected or time.monotonic()-clicked>6:
            target=next((item for item in items if item.get("path","").lower().endswith("/"+name+".button") and item.get("kind")=="button"),None)
            if target:
                time.sleep(.8)
                host.capture("provider-native")
                host.click(target);selected=True;clicked=time.monotonic()
            elif (welcome:=host.button(items,"LogInOrSignup")):
                time.sleep(1);host.click(welcome);expanded=False
            elif (not expanded or time.monotonic()-clicked>8) and (more:=host.button(items,"MoreOptions.Button")):
                host.click(more);expanded=True
                clicked=time.monotonic()
        time.sleep(1.1)
    host.capture("provider-last")
    host.paths("provider-last",host.rows())
    raise RuntimeError("Provider did not produce browser destination")
