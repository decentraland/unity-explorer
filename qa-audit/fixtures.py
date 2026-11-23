import base64
import hashlib
import io
import json
import os
from pathlib import Path
import time
import uuid
import requests
from PIL import Image,ImageDraw
import test_account as accounts
import ethkeys

def wallets(folder):
    result = []
    for label in ("primary","friend"):
        key = accounts.random_key()
        result.append({"label": label, "address": ethkeys.address_of(key), "privateKey": hex(key)})
    path = folder / "private-wallets.json"
    path.write_text(json.dumps({"accounts": result}))
    os.environ.update(DCL_AUDIT_WALLETS=str(path), DCL_AUDIT_PRIMARY="primary", DCL_AUDIT_FRIEND="friend")
    return result

def cid(data):
    return "b"+base64.b32encode(bytes.fromhex("01551220")+hashlib.sha256(data).digest()).decode().lower().rstrip("=")

def picture(label, body):
    bg, fg = ("#104574","#ffcd48") if label=="friend" else ("#3a2256","#6be1bb")
    h=512 if body else 256
    im=Image.new("RGB",(256,h),bg)
    d=ImageDraw.Draw(im)
    d.ellipse((59,45,196,188),fill=fg)
    d.ellipse((82,87,105,111),fill=bg)
    d.ellipse((148,87,171,111),fill=bg)
    d.arc((94,112,161,153),0,180,fill=bg,width=5)
    d.rounded_rectangle((35,202,221,h+40),35,fill=fg)
    b=io.BytesIO()
    im.save(b,format="PNG")
    return b.getvalue()

def deploy_profile(account):
    prefix="urn:decentraland:off-chain:base-avatars:"
    label=account["label"]
    names=("f_sweater","f_jeans","bun_shoes","standard_hair","f_eyes_01","f_eyebrows_00","f_mouth_00")
    color=lambda r,g,b: {"color":{"r":r,"g":g,"b":b,"a":1}}
    if label=="friend":
        names=("f_sweater","f_jeans","bun_shoes","two_tails","f_eyes_01","f_eyebrows_00","f_mouth_00")
    avatar={"bodyShape":prefix+"BaseFemale","wearables":[prefix+n for n in names],"emotes":[{"slot":i,"urn":n} for i,n in enumerate(("wave","clap","dance"))],
            "eyes":color(.2,.35,.55),"skin":color(.6,.45,.35),"hair":color(.8,.28,.12) if label=="friend" else color(.3,.2,.12)}
    addr=account["address"].lower()
    profile={"name":"AuditFriend" if label=="friend" else "AuditExplorer","userId":addr,"ethAddress":addr,"version":1,
             "hasClaimedName":False,"hasConnectedWeb3":True,"description":"Temporary Unity QA profile.","tutorialStep":256,"avatar":avatar}
    entity={"version":"v3","type":"profile","pointers":[addr],"timestamp":int(time.time()*1000),"metadata":{"avatars":[profile]}}
    enc=json.dumps(entity,separators=(",",":")).encode()
    eid=cid(enc)
    chain=[{"type":"SIGNER","payload":addr,"signature":""},{"type":"ECDSA_SIGNED_ENTITY","payload":eid,"signature":ethkeys.personal_sign(int(account["privateKey"],16),eid)}]
    fields={"entityId":eid}
    for i,link in enumerate(chain):
        for k,v in link.items(): fields[f"authChain[{i}][{k}]"]=v
    files={eid:(eid,enc,"application/octet-stream")}
    for attempt in range(3):
        resp=requests.post("https://dcl.dev/content/entities",data=fields,files=files,timeout=45)
        if resp.status_code not in (429,503): break
        time.sleep(min(15, int(resp.headers.get("retry-after","4"))))
    if not resp.ok:
        text=resp.text[:350]
        print("Fixture deployment:",label,resp.status_code,text if "signature" not in text.lower() else "signature rejected")
        raise RuntimeError(f"Fixture deployment returned {resp.status_code}")
    print("Fixture deployed:",label,flush=True)
    for _ in range(12):
        resp=requests.get("https://dcl.dev/lambdas/profiles/"+addr,timeout=15)
        if resp.ok and any(p.get("name")==profile["name"] for p in resp.json().get("avatars",[])): return
        time.sleep(2)
    raise RuntimeError("Fixture not visible in Catalyst profiles")
