"""Exercise the public world, map and discovery screens of the running CI player."""
import json
import re
import time
from pathlib import Path
from audit_input import key


def run(session, tool, capture, output_dir):
    output_dir = Path(output_dir)
    rows = []
    active = []

    def call(name, **args):
        return tool(session, name, args)

    def controls():
        return call("ui_list", stack="ugui").get("elements", [])

    def match(pattern, elements=None, kind=None):
        for e in controls() if elements is None else elements:
            if e.get("disabled") or (kind and e.get("kind") != kind):
                continue
            if re.search(pattern, e.get("path", ""), re.I):
                return e
        return None

    def press(pattern, operation="ui_click", **args):
        elements = controls()
        candidates = [e for e in elements if re.search(pattern, e.get("path",""), re.I) and not e.get("disabled")]
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
                time.sleep(.65)
                return
            except Exception as ex:
                failures.append(str(ex)[:160])
        raise RuntimeError("; ".join(failures)[:300])

    def record(label, action=None, expect=None, wait=1.8):
        active.clear()
        errors = []
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
            try: press(r"(ExploreSectionModal|Navmap|PlaceDetailPanel|EventDetailPanel).*/(CloseButton|Close|BackButton)$")
            except Exception: key("esc")
            time.sleep(.8)

    def open_panel(shortcut, marker):
        if not match(marker):
            key(shortcut)
            time.sleep(1.2)

    def snapshot_scene(label):
        result={}
        for name in ("get_scene_state","get_scene_content_stats","get_scene_content_breakdown"):
            try: result[name]=call(name)
            except Exception as ex: result[name]={"error":str(ex)[:250]}
        (output_dir/("world-scene-"+label+".json")).write_text(json.dumps(result,indent=2),encoding="utf-8")

    def zoom():
        if match(r"/FiltersContainer/"):
            try: press(r"/OpenLayersPanel(/|$)")
            except Exception: pass
        for _ in range(4):
            press(r"/Zoom/Plus(/|$)")

    def choose_satellite():
        if not match(r"/FiltersContainer/.*/Satellite(/|$)"):
            press(r"/OpenLayersPanel(/|$)")
        press(r"/FiltersContainer/.*/Satellite(/|$)")

    map_marker=r"/(OpenLayersPanel|PlacesAndEventsPanel|NavigationPanel)(/|$)"
    places_marker=r"/(Places_Header|PlacesResults|TabSelector/(Explore|Favorites|Recent|MyPlaces))(/|$)"
    events_marker=r"/(EventsCalendar|EventsByDay|Day[1-5]SelectorButton|GoToTodayButton)(/|$)"
    snapshot_scene("spawn")
    record("01-hud-spawn",wait=2)
    record("02-map-parcels",lambda:open_panel("m",map_marker),map_marker,3)
    record("03-map-layers",lambda:press(r"/OpenLayersPanel(/|$)"),r"/FiltersContainer/")
    record("04-map-satellite",choose_satellite,map_marker,3)
    record("05-map-satellite-zoom",zoom,map_marker,4)
    def map_search():
        press(r"/(SearchBar|SearchPlaces)(/|$).*",operation="ui_set_text",text="Genesis")
    record("06-map-search",map_search,map_marker,3)
    def leave_map():
        if match(map_marker):
            key("m")
            time.sleep(.8)
        close(map_marker)
    leave_map()
    record("07-places-browse",lambda:open_panel("z",places_marker),places_marker,4)
    record("08-place-details",lambda:press(r"/(PlaceCard(?:\(Clone\))?)(?:$|/MainButton$|/.*InfoButton$)"),r"/PlaceDetailPanel|/PlaceDetails|/DetailPanel",2)
    if match(r"/PlaceDetailPanel|/PlaceDetails|/DetailPanel"):
        close(r"/PlaceDetailPanel|/PlaceDetails|/DetailPanel")
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
    record("14-events-calendar",lambda:open_panel("x",events_marker),events_marker,4)
    record("15-events-next-page",lambda:press(r"/EventsCalendar.*/RightArrowButton(?:/|$)"),events_marker)
    record("16-events-by-day",lambda:press(r"/Day[1-5]SelectorButton(?:/|$)"),r"/(EventsByDay|GoToNextDayButton)(/|$)",3)
    record("17-events-next-day",lambda:press(r"/GoToNextDayButton(?:/|$)"),r"/(EventsByDay|GoToNextDayButton)(/|$)")
    if match(r"/EventCard(_Big|_Small|_Banner|\(Clone\)).*"):
        record("18-event-details",lambda:press(r"/EventCard(?:_Big|_Small|_Banner)(?:\(Clone\))?(?:$|/MainButton$|/.*(InfoButton|DetailButton)$)"),r"/EventDetailPanel")
    close(events_marker)
    def travel():
        try: call("teleport",x=-1,y=-1,waitForReady=False)
        except (ValueError,TypeError): pass  # This tool returns text when the request is accepted.
        deadline=time.monotonic()+52
        while time.monotonic()<deadline:
            state=call("get_scene_state")
            if state.get("currentParcel")=={"x":-1,"y":-1} and (state.get("scene") or {}).get("isReady"): return
            time.sleep(2)
        raise TimeoutError("The second parcel did not report a ready scene within 52 seconds")
    record("19-second-scene",travel,wait=2)
    snapshot_scene("second-scene")
    return rows
