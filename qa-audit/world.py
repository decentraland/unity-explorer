"""Exercise the public world, map and discovery screens of the running CI player."""
import json
import os
import re
import time
from pathlib import Path
from audit_input import key


def run(session, tool, capture, output_dir):
    output_dir = Path(output_dir)
    section = os.getenv("AUDIT_SECTION", "").strip().lower()
    if section not in ("", "all", "map", "places", "events", "scenes"):
        raise ValueError("Unknown world audit section " + section)
    rows = []
    active = []

    def call(name, **args):
        return tool(session, name, args)

    def controls():
        return call("ui_list", stack="ugui",checkOcclusion=True).get("elements", [])

    def match(pattern, elements=None, kind=None):
        for e in controls() if elements is None else elements:
            if e.get("disabled") or e.get("occluded") or (kind and e.get("kind") != kind):
                continue
            if re.search(pattern, e.get("path", ""), re.I):
                return e
        return None

    def press(pattern, operation="ui_click", **args):
        elements = controls()
        candidates = [e for e in elements if re.search(pattern, e.get("path",""), re.I) and not e.get("disabled")]
        candidates.sort(key=lambda e:bool(e.get("occluded")))
        if operation == "ui_set_text":
            candidates = [e for e in candidates if "input" in e.get("kind","").lower()]
        if not candidates:
            raise LookupError("No live control for " + pattern)
        failures = []
        for e in candidates[:10]:
            try:
                result = call(operation, path=e["path"], **args)
                if isinstance(result,dict) and result.get("ok") is False:
                    raise RuntimeError(str(result.get("blockedBy") or result.get("warning") or "unconsumed input"))
                active.append(e["path"])
                time.sleep(1.4)
                return
            except Exception as ex:
                failures.append(str(ex)[:160])
        raise RuntimeError("; ".join(failures)[:300])

    def record(label, action=None, expect=None, wait=1.8):
        active.clear()
        errors = []
        if match(r"^PerformanceIssuePrompt/.*/CloseButton$"):
            try:
                if match(r"^PerformanceIssuePrompt/.*/DontShowThisAgainToggle$"):
                    press(r"^PerformanceIssuePrompt/.*/DontShowThisAgainToggle$")
                press(r"^PerformanceIssuePrompt/.*/CloseButton$")
            except Exception: pass
        if action:
            try: action()
            except Exception as ex: errors.append(str(ex)[:300])
        time.sleep(wait)
        try:
            items=controls()
            summary=[(e.get("kind"),e.get("path","")) for e in items]
            if expect and not match(expect,items):
                errors.append("Expected visible controls matching " + expect)
        except Exception as ex:
            summary=[]
            errors.append(str(ex)[:200])
        try:
            shot=Path(capture("world-"+label)).name
        except Exception as ex:
            shot=None
            errors.append("capture: "+str(ex)[:150])
        row={"screen": label, "evidence":shot, "interactions":active.copy(), "controls":summary, "errors":errors}
        rows.append(row)
        (output_dir/"world-audit.json").write_text(json.dumps(rows,indent=2),encoding="utf-8")
        print("world audit",label,"capture",shot,"errors",errors,flush=True)
        return row

    def close(expected):
        if match(expected):
            try: press(r"(ExploreSectionModal|Navmap|PlaceDetailPanel|EventDetailPanel).*/(CloseButton|Button_Close|Close|BackButton)$")
            except Exception: key("esc")
            time.sleep(.8)

    def open_panel(shortcut, marker):
        tab = {"m":"Map", "x":"Events", "z":"Places"}[shortcut]
        tab_path = r"/Header/TabSelector/"+tab+"Tab$"
        if match(marker): return
        if match(tab_path):
            press(tab_path)
            return
        key(shortcut)
        time.sleep(1.2)
        if match(marker): return
        if shortcut=='m':
            try: press(r"/Minimap/.*/MinimapRendererButton$")
            except Exception: pass
            if not match(marker):
                try: press(r"/UpperLayout/MapButton$")
                except Exception: pass
        else:
            entry = r"/DiscoverMenu/(?:.*/)?"+tab+r"(?:/|$)"
            try:
                if not match(entry): press(r"/UpperLayout/DiscoverButton$")
                press(entry)
            except Exception: pass
        if match(marker): return
        if not match(tab_path):
            press(r"/UpperLayout/SettingsButton$")
            press(r"/SystemMenu/(?:.*/)?Settings(?:Button)?$")
        press(tab_path)

    def snapshot_scene(label):
        result={}
        for name in ("get_scene_state","get_scene_content_stats","get_scene_content_breakdown"):
            try: result[name]=call(name)
            except ValueError:
                from rpc_base import rpc
                response,_=rpc("tools/call",{"name":name,"arguments":{}},session)
                result[name]=[b.get("text","")[:8000] for b in response.get("content",[]) if b["type"]=="text"]
            except Exception as ex: result[name]={"error":str(ex)[:250]}
        (output_dir/("world-scene-"+label+".json")).write_text(json.dumps(result,indent=2),encoding="utf-8")

    def zoom():
        if match(r"/FiltersContainer/"):
            try: press(r"/OpenLayersPanel(/|$)")
            except Exception: pass
        for _ in range(8):
            if not match(r"/Zoom/Plus(/|$)"): break
            press(r"/Zoom/Plus(/|$)")
            time.sleep(.8)

    def layers():
        if not match(r"/FiltersContainer/.*/Satellite(/|$)"):
            press(r"/OpenLayersPanel(/|$)")

    def choose_type(name):
        layers()
        press(r"/FiltersContainer/.*/"+name+r"(?:/|$)")

    map_marker=r"(?:^|/)(OpenLayersPanel|PlacesAndEventsPanel|NavigationPanel)(?:/|$)"
    places_marker=r"(?:^|/)(Places_Header|PlacesResults|TabSelector/(Explore|Favorites|Recent|MyPlaces))(?:/|$)"
    events_marker=r"(?:^|/)(EventsCalendar|EventsByDay|Day[1-5]SelectorButton|GoToTodayButton)(?:/|$)"
    place_detail=r"(?:^|/)(PlaceDetailPanel|PlaceDetails|DetailPanel)(?:/|$)"
    event_detail=r"(?:^|/)(EventDetailPanel|EventDetails)(?:/|$)"
    until=time.monotonic()+90
    while time.monotonic()<until:
        state=call("get_scene_state")
        if not state.get("loadingScreenOn") and (state.get("scene") or {}).get("isReady"): break
        time.sleep(2)
    snapshot_scene("spawn")
    record("01-hud-spawn",wait=2)
    def map_screens():
        record("02-map-on-open",lambda:open_panel("m",map_marker),map_marker,3)
        record("02b-map-parcels",lambda:choose_type("Parcel"),r"/FiltersContainer/",3)
        record("03-map-layers",layers,r"/FiltersContainer/")
        record("04-map-satellite",lambda:choose_type("Satellite"),map_marker,3)
        record("05-map-satellite-zoom",zoom,map_marker,10)
        from rpc_base import rpc
        try:
            stats,_=rpc("tools/call",{"name":"get_performance_stats","arguments":{"sampleSeconds":3}},session)
            metrics=stats.get("structuredContent") or [v.get("text","") for v in stats.get("content",[]) if v["type"]=="text"]
            (output_dir/"world-map-frame-stats.json").write_text(json.dumps(metrics,indent=2),encoding="utf-8")
        except Exception as ex: print("Map frame sample unavailable:",str(ex)[:120],flush=True)
        def map_search():
            field = r"/Navmap/PlacesAndEventsPanel/SearchBarContainer/SearchBar$"
            press(field)
            press(field,operation="ui_set_text",text="Genesis",submit=True)
        record("06-map-search",map_search,map_marker,3)
        def select_search_result():
            items=controls()
            candidates=[item for item in items if "genesis" in item.get("label","").lower() and item.get("kind")=="button" and not item.get("occluded")]
            if candidates:
                press("^"+re.escape(candidates[0]["path"])+"$")
            else:
                press(r"/SearchPlaces/.*/PlaceSearchEntry(?:\(Clone\))?(?:\[\d+\])?$")
        record("06b-map-place-overview",select_search_result,r"/(StartNavigation|ExitNavigation|JumpIn)$",4)
        if match(r"/(StartNavigation|ExitNavigation|JumpIn)$"):
            for tab in ("Photos","Events","Overview"):
                picker=r"/PlacesAndEventsPanel/.*/"+tab+r"$"
                if match(picker): record("06c-map-place-"+tab.lower(),lambda p=picker:press(p),map_marker,3)
            if match(r"/StartNavigation$"):
                record("06d-map-route",lambda:press(r"/StartNavigation$"),r"/(ExitNavigation|DestinationInfoElement)/?",2)
                if match(r"/ExitNavigation$"): press(r"/ExitNavigation$")
        def leave_map():
            if match(map_marker):
                key("m")
                time.sleep(.8)
            close(map_marker)
        leave_map()

    def places_screens():
        record("07-places-browse",lambda:open_panel("z",places_marker),places_marker,4)
        record("08-place-details",lambda:press(r"/PlaceCard(?:\[\d+\]|\(Clone\))?(?:$|/MainButton$|/.*Info(?:Button)?$)"),place_detail,2)
        if match(place_detail):
            close(place_detail)
            open_panel("z",places_marker)
        record("09-places-favorites",lambda:press(r"/TabSelector/Favorites(?:/|$)"),places_marker)
        record("10-places-recent",lambda:press(r"/TabSelector/Recent(?:/|$)"),places_marker)
        record("11-places-mine",lambda:press(r"/TabSelector/MyPlaces(?:/|$)"),places_marker)
        def place_search():
            press(r"/TabSelector/Explore(?:/|$)")
            press(r"/SearchBar(?:/|$).*",operation="ui_set_text",text="Genesis")
        record("12-places-search",place_search,places_marker,3)
        record("13-places-filters",lambda:press(r"/Places_FilterSelector(?:/[^/]*)?$"),places_marker)
        close(places_marker)

    def events_screens():
        record("14-events-calendar",lambda:open_panel("x",events_marker),events_marker,4)
        record("15-events-next-page",lambda:press(r"/EventsCalendar.*/RightArrowButton(?:/|$)"),events_marker)
        record("16-events-by-day",lambda:press(r"/Day[1-5]SelectorButton(?:/|$)"),r"/(EventsByDay|GoToNextDayButton)(/|$)",3)
        record("17-events-next-day",lambda:press(r"/GoToNextDayButton(?:/|$)"),r"/(EventsByDay|GoToNextDayButton)(/|$)")
        if match(r"/EventCard(_Big|_Small|_Banner|\(Clone\)).*"):
            record("18-event-details",lambda:press(r"/EventCard(?:_Big|_Small|_Banner)(?:\[\d+\]|\(Clone\))?(?:$|/MainButton$|/.*(Info|Detail)(?:Button)?$)"),event_detail)
        close(events_marker)

    def scene_screens():
        def travel(x,y,expected_base):
            try: call("teleport",x=x,y=y,waitForReady=False)
            except (ValueError,TypeError): pass  # This tool returns text when the request is accepted.
            deadline=time.monotonic()+130
            while time.monotonic()<deadline:
                state=call("get_scene_state")
                scene=state.get("scene") or {}
                if not state.get("loadingScreenOn") and scene.get("baseParcel")==expected_base and scene.get("isReady") and scene.get("assetsLoadingConcluded"): return
                time.sleep(2)
            raise TimeoutError(f"Parcel {x},{y} did not report completed loading within 130 seconds")
        def fixed_view(x,y,z,tx,ty,tz):
            call("set_camera_pose",x=x,y=y,z=z,lookAtX=tx,lookAtY=ty,lookAtZ=tz,fov=65)
        record("19-second-scene",lambda:travel(-1,-1,{"x":-3,"y":-2}),wait=8)
        snapshot_scene("second-scene")
        record("19b-genesis-overview",lambda:fixed_view(5,24,-30,5,3,24),wait=2)
        try: call("set_camera_mode",mode="third_person")
        except Exception: pass
        record("20-metazoo-bundles",lambda:travel(95,98,{"x":95,"y":98}),wait=8)
        snapshot_scene("metazoo")
        record("20b-metazoo-overview",lambda:fixed_view(1528,21,1575,1556,3,1605),wait=2)

    for name, execute in (("map",map_screens),("places",places_screens),("events",events_screens),("scenes",scene_screens)):
        if section in ("", "all", name): execute()
    return rows
