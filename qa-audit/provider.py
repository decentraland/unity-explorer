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
    entered=False
    for _ in range(100):
        items=host.rows()
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
        if not selected:
            target=next((item for item in items if item.get("path","").lower().endswith("/"+name+".button") and item.get("kind")=="button"),None)
            if target:
                time.sleep(.8)
                host.capture("provider-native")
                host.click(target);selected=True
            elif not entered and (welcome:=host.button(items,"LogInOrSignup")):
                host.click(welcome);entered=True
            elif not expanded and (more:=host.button(items,"MoreOptions.Button")):
                host.click(more);expanded=True
        time.sleep(1.1)
    host.capture("provider-last")
    host.paths("provider-last",host.rows())
    raise RuntimeError("Provider did not produce browser destination")
