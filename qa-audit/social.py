"""Exercise visible social UI in a signed-in CI player. Writes only sanitized QA evidence."""
import hashlib
import json
import os
from pathlib import Path
import re
import time
from urllib.error import HTTPError
from urllib.parse import urlsplit
from urllib.request import Request, urlopen


ORIGIN = "https://dcl.dev"


def request_json(method, path, account=None, data=None):
    headers = {"User-Agent": "UnityExplorerGatewayQA/1", "Accept": "application/json"}
    if account is not None:
        import ethkeys
        stamp = str(time.time_ns() // 1_000_000)
        payload = f"{method}:{urlsplit(path).path}:{stamp}:{{}}".lower()
        signature = ethkeys.personal_sign(int(account["privateKey"], 16), payload)
        headers.update({"x-identity-timestamp": stamp, "x-identity-metadata": "{}",
                        "x-identity-auth-chain-0": json.dumps({"type": "SIGNER", "payload": account["address"], "signature": ""}),
                        "x-identity-auth-chain-1": json.dumps({"type": "ECDSA_SIGNED_ENTITY", "payload": payload, "signature": signature})})
    if data is not None:
        headers["Content-Type"] = "application/json"
    req = Request(ORIGIN + path, method=method, headers=headers,
                  data=None if data is None else json.dumps(data).encode())
    try:
        with urlopen(req, timeout=12) as response:
            buf = response.read(2 * 1024 * 1024)
            try:
                return response.status, json.loads(buf)
            except ValueError:
                return response.status, {}
    except HTTPError as e:
        return e.code, {}
    except OSError:
        return 0, {}


def image_facts(value):
    if not isinstance(value, str) or not value:
        return {"representation": "missing"}
    parts = urlsplit(value)
    if parts.scheme == "https" and parts.hostname in {"dcl.dev", "profile-images.decentraland.org", "profile-images.dcl.dev"}:
        local = value
        representation = "HTTPS URL: " + parts.hostname
    elif re.fullmatch(r"(Qm[1-9A-HJ-NP-Za-km-z]{44}|ba[a-z2-7]{25,110})", value):
        local = ORIGIN + "/content/contents/" + value
        representation = "bare content ID"
    elif parts.scheme == "https":
        return {"representation": "external HTTPS URL", "host": parts.hostname}
    else:
        return {"representation": "relative path or other value"}
    try:
        with urlopen(Request(local, headers={"User-Agent": "UnityExplorerGatewayQA/1"}), timeout=10) as response:
            data = response.read(2 * 1024 * 1024)
            return {"representation": representation, "http": response.status, "mime": response.headers.get_content_type(),
                    "is_png": data.startswith(b"\x89PNG\r\n\x1a\n"), "sha256": hashlib.sha256(data).hexdigest()}
    except HTTPError as e:
        return {"representation": representation, "http": e.code}
    except OSError:
        return {"representation": representation, "http": 0}


def owns_pair():
    file = os.getenv("DCL_AUDIT_WALLETS")
    if not file:
        return None
    accounts = {a["label"]: a for a in json.loads(Path(file).read_text(encoding="utf-8-sig"))["accounts"]}
    a, b = (accounts[os.environ[k]] for k in ("DCL_AUDIT_PRIMARY", "DCL_AUDIT_FRIEND"))
    if a["address"].lower() == b["address"].lower():
        raise ValueError("Test fixture needs distinct wallets")
    return a, b


def social_readback(a, b, facts):
    peer = b["address"].lower()
    status, data = request_json("GET", "/social-api/v2/friends", a)
    rows = data.get("data", {}).get("results", [])
    facts["membership"] = {"http": status, "count": len(rows), "contains_fixture": any(
        (r.get("address") or "").lower() == peer for r in rows)}
    status, data = request_json("GET", "/social-api/v1/friends", a)
    rows = [r for r in data.get("friends", []) if (r.get("address") or "").lower() == peer]
    facts["social_enrichment"] = {"http": status, "contains_fixture": bool(rows),
                                  "has_name": bool(rows and rows[0].get("name")),
                                  "picture": image_facts(rows[0].get("avatarUrl") if rows else None)}
    status, data = request_json("GET", "/lambdas/profiles/" + peer)
    profiles = [r for r in data.get("avatars", []) if (r.get("userId") or "").lower() == peer]
    face = profiles[0].get("avatar", {}).get("snapshots", {}).get("face256") if profiles else None
    facts["catalyst_profile"] = {"http": status, "fixture_found": bool(profiles), "face256": image_facts(face)}
    if face:
        uri = urlsplit(face)
        if uri.hostname == "profile-images.decentraland.org" and uri.path.startswith("/entities/"):
            facts["same_image_on_gateway"] = image_facts(ORIGIN + "/profile-images" + uri.path)
    status, active = request_json("POST", "/content/entities/active", data={"pointers": [peer]})
    entities = active if isinstance(active, list) else []
    profiles = (entities[0].get("metadata", {}).get("avatars", []) if entities else [])
    face = profiles[0].get("avatar", {}).get("snapshots", {}).get("face256") if profiles else None
    facts["active_profile_entity"] = {"http": status, "profile_count": len(profiles),
                                       "has_face256": bool(face), "face256": image_facts(face)}


def send_key(vk):
    from audit_input import key
    key({0x1b: "ESCAPE", 0x0d: "ENTER"}.get(vk, chr(vk)))
    return True


class Session:
    def __init__(self, session, tool, capture, output_dir):
        self.session, self.tool, self.capture = session, tool, capture
        self.output = Path(output_dir)
        self.output.mkdir(parents=True, exist_ok=True)
        self.report = {"fixture": {}, "screens": []}
        self.controls = []

    def flush(self):
        (self.output / "social-audit.json").write_text(json.dumps(self.report, indent=2), encoding="utf-8")

    def elements(self):
        listing = self.tool(self.session, "ui_list", {"stack": "ugui", "checkOcclusion": True})
        self.controls = listing.get("elements", [])
        return self.controls

    @staticmethod
    def path(node):
        return "/" + re.sub(r"\[\d+\]", "", node.get("path", "")).strip("/").lower()

    def clear_transients(self):
        # MCP is listening while the player still shows the loading screen. Ignore behind-screen controls.
        for _ in range(70):
            nodes = self.elements()
            covering = next((n for n in nodes if re.search(r"/(?:altdialog/dialog|performanceissueprompt/.+)/closebutton$",
                                                          self.path(n)) and not n.get("occluded")), None)
            if covering:
                self.tool(self.session, "ui_click", {"path": covering["path"]})
                time.sleep(.45)
                continue
            if not any("/sceneloadingscreen/content/" in self.path(n) for n in nodes):
                return
            time.sleep(1)

    def choose(self, patterns, wait=2, label=None):
        self.clear_transients()
        end = time.monotonic() + wait
        while True:
            elements = self.elements()
            for pattern in patterns:
                hits = [node for node in elements if re.search(pattern, self.path(node), re.I)
                        and not node.get("occluded", False)
                        and node.get("kind") not in ("scroll", "scrollrect")]
                if label:
                    hits = [n for n in hits if label in str(n.get("label", "")).lower()]
                if hits:
                    return hits[0]
            if time.monotonic() >= end:
                return None
            time.sleep(.35)

    def click(self, patterns, wait=2, label=None):
        node = self.choose(patterns, wait, label)
        if node is None:
            return False
        try:
            self.tool(self.session, "ui_click", {"path": node["path"]})
            time.sleep(.65)
            return True
        except Exception:
            return False

    def snap(self, name, marker, settle=1.3):
        time.sleep(settle)
        elems = self.elements()
        paths = [self.path(e) for e in elems]
        matched = [p for p in paths if re.search(marker, p, re.I)]
        unique = sorted({re.sub(r"0x[a-f0-9]{12,}", "<wallet>", p, flags=re.I) for p in paths})
        record = {"screen": name, "view_controls_found": bool(matched), "controls": unique}
        self.report["screens"].append(record)
        self.flush()
        shot = self.capture("social-" + name)
        if shot:
            record["screenshot"] = Path(shot).name
        self.flush()
        return bool(matched)

    def close(self, scope):
        if not any(re.search(scope, self.path(n), re.I) for n in self.elements()):
            return
        if not self.click([scope + r".*/(?:button_close|closebutton|\[btn\] close|close|background_close)$"], .5):
            send_key(0x1b)

    def open(self, patterns, key=None):
        if self.click(patterns):
            return True
        return bool(key and send_key(key))

    def friends(self, pair):
        facts = self.report["fixture"]
        if pair:
            a, b = pair
            status, data = request_json("GET", "/social-api/v2/friends", a)
            old = data.get("data", {}).get("results", [])
            has_friend = any(r.get("address", "").lower() == b["address"].lower() for r in old)
            facts["initial_membership_http"] = status
            if not has_friend:
                result, _ = request_json("POST", "/social-api/v1/friendship-requests", b, {"address": a["address"]})
                facts["request_http"] = result
            status, data = request_json("GET", "/social-api/v2/friendship-requests?direction=received", a)
            rows = data.get("data", {}).get("results", [])
            owned_request = len(rows) == 1 and rows[0].get("address", "").lower() == b["address"].lower()
            facts["received_request"] = {"http": status, "only_fixture_pending": owned_request}
        friends = r"(?:/friends?panel(?:/|$)|/friendssection(?:/|$))"
        self.open([r"/(?:sidebarfriendsbutton|friendsbutton)(?:/button)?$"], ord("L"))
        self.snap("friends-initial", friends)
        self.click([friends + r".*requestsbutton(?:/button)?$"])
        self.snap("friend-requests", r"/requestspanel/", 2)
        if pair and not has_friend and owned_request:
            facts["clicked_accept"] = self.click([r"/requestuser[^/]*/(?:.*/)?acceptbutton$"])
            social_readback(a, b, facts)
            if not facts["membership"]["contains_fixture"]:
                status, _ = request_json("PATCH", "/social-api/v1/friendship-requests/" + b["address"], a, {"intention": "accepted"})
                facts["fallback_accept_http"] = status
        if pair:
            social_readback(a, b, facts)
        self.click([friends + r".*blockedbutton(?:/button)?$"])
        self.snap("blocked", r"/blockedpanel/")
        # Reopen so Unity does a fresh RPC page fetch even if acceptance used the HTTP companion route.
        self.close(friends)
        self.open([r"/(?:sidebarfriendsbutton|friendsbutton)(?:/button)?$"], ord("L"))
        self.click([friends + r".*friendsbutton(?:/button)?$"])
        self.snap("friends-accepted", friends, 3)
        friend_row = [r"/friendlistuser[^/]*$", r"/friendlistuser[^/]*/(?:.*/)?(?:mainbutton|userbutton)$"]
        facts["visible_friend_rows"] = sum(any(re.search(pattern, self.path(e)) for pattern in friend_row)
                                           for e in self.controls)
        if pair and facts.get("membership", {}).get("contains_fixture") and facts["membership"]["count"] == 1:
            node = self.choose(friend_row)
            if node:
                self.tool(self.session, "ui_hover", {"path": node["path"]})
                time.sleep(.4)
            if self.click([r"/friendlistuser[^/]*/(?:.*/)?chatbutton$"]):
                self.choose([r"/chatpanel/.*(?:privatevoicecall_button|chatconversationstoolbarviewitem_private)$"], 4)
                self.snap("direct-message", r"/chatpanel/.*(?:chatnputboxmask|chatconversationstoolbarviewitem_private|privatevoicecall_button)")
                self.close(r"/chatpanel")
                self.open([r"/(?:sidebarfriendsbutton|friendsbutton)(?:/button)?$"], ord("L"))
            if self.click(friend_row):
                self.snap("friend-passport", r"/passport/", 8)
                self.close(r"/passport")
        self.close(friends)

    def passport(self):
        self.open([r"/sidebarprofilebutton/openprofilebutton$", r"/sidebarprofilebutton(?:/button)?$", r"/profilewidget$"])
        self.snap("profile-menu", r"/profilemenu|/previewprofilebutton$")
        self.click([r"/previewprofilebutton(?:/button)?$", r"/viewprofilebutton$"])
        self.snap("own-passport-overview", r"/passport/", 8)
        for label in ("Badges", "Photos", "Creations"):
            pat = r"/passport/.*" + label.lower() + r"sectionbutton(?:/button)?$"
            if self.click([pat]):
                self.snap("own-passport-" + label.lower(), r"/passport/")
        self.close(r"/passport")

    def chat(self):
        self.open([r"/sidebarchatbutton(?:/button)?$"], 0x0D)
        self.click([r"/chatconversationstoolbarviewitem$"], .6)
        self.snap("nearby-chat", r"/chatinputbox/.+|/chatconversationstoolbar")
        if self.click([r"/chatinputbox/.*emojibutton$", r"/chatpanel/.*reactionbutton$"]):
            self.snap("chat-emoji", r"/emoji[^/]*/")
            self.click([r"/chatinputbox/.*emojibutton$"], .4)
        if self.click([r"/(?:\[btn\]\s*)?members$", r"/nearbyinfocontainer[^/]*/.*membersbutton$"]):
            self.snap("chat-members", r"/member|participant|channelinfo")
            self.close(r"/(?:channel|member)")
        self.close(r"/chatpanel")

    def notifications(self):
        self.open([r"/(?:sidebar)?notificationsbutton(?:/button)?$"])
        self.snap("notifications", r"/notificationsmenu(?:/|$)", 1.6)
        self.click([r"/(?:sidebar)?notificationsbutton(?:/button)?$"], .4) or self.close(r"/notificationsmenu")

    def communities(self):
        self.clear_transients()
        send_key(ord("O"))
        if not self.choose([r"/communitiessection/.*(?:invitesandrequestsbutton|searchbar|communityresultcard)$"], 2):
            self.click([r"/discoverbutton$", r"/communitiestab$"])
            self.click([r"/discovermenu/.*/communities(?:/button)?$", r"/communities(?:/button)?$", r"/communitiestab$"])
        self.choose([r"/communityresultcard(?:/header)?$", r"/communityresultcard/.+viewbutton$"], 15)
        self.snap("communities-browse", r"/communitiessection/|/communitiesbrowser", 2)
        if self.click([r"/communityresultcard(?:/header)?$", r"/communityresultcard/.*viewbutton$", r"/communityresultcard/(?:header|button)$"]):
            self.snap("community-announcements", r"/communitycard/")
            for label in ("Members", "Places", "Photos"):
                if self.click([r"/communitycard/.*" + label.lower() + r"sectionbutton(?:/button)?$"]):
                    self.snap("community-" + label.lower(), r"/communitycard/")
            self.close(r"/communitycard")
        if self.click([r"/invitesandrequestsbutton(?:/button)?$"]):
            self.snap("community-invites-requests", r"/invitesandrequestssection|/invitesandrequestsbutton")
            self.click([r"/invitesandrequestssection/.*backbutton$"])
        if self.click([r"/createcommunitybutton(?:/button)?$"]):
            self.snap("create-community", r"/(?:communitycreationwizard|createoreditcommunitypanel|createcommunity)")
            self.close(r"/(?:communitycreationwizard|createoreditcommunitypanel)")
        self.close(r"/explorepanel")


def run(session, tool, capture, output_dir):
    driver = Session(session, tool, capture, output_dir)
    pair = None
    try:
        pair = owns_pair()
        player = tool(session, "get_player_state", {})
        if pair and (player.get("address") or "").lower() != pair[0]["address"].lower():
            driver.report["fixture"]["reason"] = "Player does not match the owned primary fixture"
            pair = None
        elif not pair:
            driver.report["fixture"]["reason"] = "No twin-wallet fixture supplied"
    except Exception as e:
        pair = None
        driver.report["fixture"]["reason"] = "Unavailable: " + type(e).__name__
    for name, fn in [("friends", lambda: driver.friends(pair)), ("notifications", driver.notifications),
                     ("chat", driver.chat), ("passport", driver.passport), ("communities", driver.communities)]:
        try:
            driver.clear_transients()
            fn()
        except Exception as e:
            driver.report["screens"].append({"screen": name, "driver_error": type(e).__name__})
            try:
                driver.capture("social-" + name + "-driver-stop")
                send_key(0x1B)
            except Exception:
                pass
        finally:
            driver.flush()
    return driver.report
