import json
import os
from pathlib import Path
import re
from urllib.parse import urlsplit, parse_qs
from playwright.sync_api import sync_playwright
import ethkeys

def wallet_handoff(url, account, out):
    assert urlsplit(url).netloc == "dcl.dev"
    request_id=urlsplit(url).path.split("/")[-1]
    with sync_playwright() as p:
        browser=p.chromium.launch(channel="msedge",headless=True)
        page=browser.new_page(viewport={"width":1280,"height":720})
        trace=[]
        def record(req,detail):
            parsed=urlsplit(req.url)
            if "identit" in parsed.path or "auth-api" in parsed.netloc:
                path=re.sub(r"[a-fA-F0-9-]{32,}","<id>",parsed.path)
                trace.append({"method":req.method,"origin":parsed.netloc,"path":path,"result":detail})
        page.on("requestfailed",lambda r:record(r,r.failure))
        page.on("response",lambda r:record(r.request,r.status))
        if os.environ.get("AUDIT_SLICE")=="authcontrol":
            def corrected(route):
                response=route.fetch()
                body=response.text().replace("https://auth-api.dcl.dev","https://dcl.dev/auth-api")
                headers={k:v for k,v in response.headers.items() if k.lower() not in ("content-encoding","content-length")}
                route.fulfill(status=response.status,headers=headers,body=body)
            page.route("https://dcl.dev/auth/requests/**",corrected)
        def request(source, args):
            if urlsplit(source["frame"].url).netloc!="dcl.dev": raise ValueError("Unexpected origin")
            method=args["method"]
            if method in ("eth_accounts","eth_requestAccounts"): return [account["address"]]
            if method=="eth_chainId": return "0x1"
            if method=="net_version": return "1"
            if method=="personal_sign":
                data, address=args["params"]
                msg=bytes.fromhex(data[2:]).decode()
                if address.lower()!=account["address"].lower() or not re.fullmatch(r"Decentraland Login\nEphemeral address: 0x[0-9a-fA-F]{40}\nExpiration: [0-9T:.Z-]+",msg):
                    raise ValueError("Not a test-wallet login request")
                return ethkeys.personal_sign(int(account["privateKey"],16),msg)
            raise ValueError("Unsupported QA wallet method")
        page.expose_binding("qaWalletRequest",request)
        page.add_init_script("window.ethereum={request:x=>window.qaWalletRequest(x),on(){},removeListener(){}};")
        page.goto(url,wait_until="domcontentloaded",timeout=45000)
        signin=page.get_by_role("button",name="Sign in with wallet")
        try:
            signin.wait_for(timeout=20000)
            page.screenshot(path=str(out/"auth-browser-wallet.jpg"),type="jpeg")
            captures=[]
            cdp=page.context.new_cdp_session(page)
            cdp.send("Page.enable")
            cdp.on("Page.frameRequestedNavigation",lambda e: captures.append(e.get("url","")) if e.get("url","").startswith("decentraland://") else None)
            signin.click()
            for _ in range(65):
                page.wait_for_timeout(500)
                anchors=page.locator('a[href^="decentraland:"]').all()
                callback=next((a.get_attribute("href") for a in anchors if "signin=" in (a.get_attribute("href") or "")),None)
                callback=callback or next((u for u in captures if "signin=" in u),None)
                if callback:
                    query=parse_qs(urlsplit(callback).query)
                    assert query.get("authRequestId")==[request_id] and query.get("signin")
                    page.screenshot(path=str(out/"auth-browser-return.jpg"),type="jpeg")
                    target=Path(os.environ["LOCALAPPDATA"])/"DecentralandLauncherLight"/"deeplink-bridge.json"
                    target.parent.mkdir(parents=True,exist_ok=True)
                    tmp=target.with_suffix(".tmp")
                    tmp.write_text(json.dumps({"deeplink":callback}))
                    tmp.replace(target)
                    print("Delivered the genuine site's callback to Unity's launcher bridge",flush=True)
                    return
            raise RuntimeError("Browser supplied no matching callback")
        except Exception:
            page.screenshot(path=str(out/"auth-browser-outcome.jpg"),type="jpeg")
            raise
        finally:
            (out/"browser-network.json").write_text(json.dumps(trace,indent=2))
            browser.close()
