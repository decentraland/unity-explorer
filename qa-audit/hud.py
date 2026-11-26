import json
import os
import re
import time
from audit_input import key

def run(session,tool,capture,out):
    report=[]
    def controls():
        return [r for r in tool(session,"ui_list",{"stack":"ugui","checkOcclusion":True})["elements"] if not r.get("occluded")]
    def click(pattern):
        rows=controls()
        entry=next((r for r in rows if re.search(pattern,r["path"],re.I) and r.get("kind") in ("button","toggle")),None)
        if not entry:
            report.append({"attempt":pattern,"result":"control not visible"})
            return False
        response=tool(session,"ui_click",{"path":entry["path"]})
        report.append({"attempt":pattern,"semantic":response})
        if response.get("ok") is False:
            response=tool(session,"ui_click",{"path":entry["path"],"device":True})
            report.append({"attempt":pattern,"device":response})
        if response.get("ok") is False:
            locations={"ContextMenuButton":(.162,.034),"PlaceNameAndOptions/Collapse":(.046,.034),"OpenProfileButton":(.016,.05)}
            point=next((xy for name,xy in locations.items() if name in entry["path"]),None)
            if point:
                x,y=point
                response=tool(session,"ui_drag",{"fromX":x,"fromY":y,"toX":x,"toY":y,"durationFrames":2,"path":"device"})
                report.append({"attempt":pattern,"pointer":response})
        time.sleep(1.3)
        return response.get("ok") is not False
    def record(name):
        time.sleep(1.0)
        capture(name)
        report.append({"screen":name,"controls":[{"path":r["path"],"label":r.get("label",""),"kind":r.get("kind")} for r in controls()]})
        (out/"hud-observations.json").write_text(json.dumps(report,indent=1))
    section=os.getenv("AUDIT_SECTION","")
    if section:
        record("hud-start")
    if section in ("","sky") and click("/SidebarSkyboxButton$"):
        record("skybox-widget")
        toggle=next((r["path"] for r in controls() if "SkyboxWidget" in r["path"] and r.get("kind")=="toggle"),None)
        if toggle and click(re.escape(toggle)+"$"):
            record("skybox-cycle-toggled")
            click(re.escape(toggle)+"$")
        if not click("/SidebarSkyboxButton$"): key("ESC")
    if section in ("","map") and click("/PlaceNameAndOptions/TopRightSection/ContextMenuButton$"):
        record("place-context-menu")
        if not click("^PopupCloser/Button$"): click("/PlaceNameAndOptions/TopRightSection/ContextMenuButton$")
    if section in ("","map") and click("/PlaceNameAndOptions/Collapse$"):
        record("minimap-collapsed")
        if not click("/PlaceNameAndOptions/Collapse$"): click("Minimap/.*/Expand$")
    if section in ("","voice") and click("/NearbyVoice.Button$"):
        record("nearby-voice-control")
        # Capture only the controls shown and close the widget; the hosted runner has no physical microphone.
        if not click("/NearbyVoice.Button$"): key("ESC")
    if section in ("","profile") and click("/OpenProfileButton$"):
        record("profile-sidebar-menu")
        # Reaching a confirmation screen uses only the disposable profile and never confirms logout.
        if click("/(?:LogOut|SignOut|LogOutOption|SignOutOption)(?:Button|/Button)?$"):
            record("logout-choice")
    if section:
        record("hud-end")
