#!/usr/bin/env python3
"""Lists the Genesis City scenes the abgen registry does not know.

Walks every parcel of the city in the same 60-pointer batches the client uses, asks the catalyst
and the abgen registry which scenes cover them, and reports the scenes only the catalyst has,
with their state on the abgen CDN. Writes abgen-missing.json and abgen-missing.csv.

  python3 scripts/abgen_coverage.py                  # whole city
  python3 scripts/abgen_coverage.py -32 -47 7 -8     # a parcel region: x0 y0 x1 y1
"""
import csv
import datetime
import json
import sys
import urllib.error
import urllib.request

CATALYST = "https://peer.decentraland.org/content/entities/active"
REGISTRY = "https://asset-bundle-registry-abgen.decentraland.org/entities/active"
CDN = "https://abgen-cdn.decentraland.org"
BATCH = 60
CITY = (-150, -150, 163, 158)
HEADERS = {"content-type": "application/json", "user-agent": "abgen-coverage/1"}


def post(url, pointers):
    request = urllib.request.Request(url, data=json.dumps({"pointers": pointers}).encode(), headers=HEADERS)
    for attempt in range(3):
        try:
            with urllib.request.urlopen(request, timeout=60) as response:
                body = json.load(response)
                return body if isinstance(body, list) else body.get("entities", [])
        except (urllib.error.URLError, TimeoutError) as error:
            if attempt == 2:
                raise
            print(f"retrying {url}: {error}", file=sys.stderr)
    return []


def head(url):
    try:
        with urllib.request.urlopen(urllib.request.Request(url, method="HEAD", headers=HEADERS), timeout=30) as response:
            return response.status
    except urllib.error.HTTPError as error:
        return error.code
    except (urllib.error.URLError, TimeoutError):
        return 0


def main():
    x0, y0, x1, y1 = (int(v) for v in sys.argv[1:5]) if len(sys.argv) == 5 else CITY
    pointers = [f"{x},{y}" for x in range(x0, x1 + 1) for y in range(y0, y1 + 1)]
    catalyst, registry = {}, set()
    batches = range(0, len(pointers), BATCH)

    for index, start in enumerate(batches):
        batch = pointers[start:start + BATCH]

        for entity in post(CATALYST, batch):
            metadata = entity["metadata"]
            catalyst[entity["id"]] = {
                "id": entity["id"],
                "title": metadata.get("display", {}).get("title", ""),
                "sdk": metadata.get("runtimeVersion") or "6",
                "base": metadata.get("scene", {}).get("base", ""),
                "parcels": len(entity["pointers"]),
                "deployed": datetime.datetime.fromtimestamp(entity.get("timestamp", 0) / 1000, datetime.UTC).date().isoformat(),
            }

        for entity in post(REGISTRY, batch):
            registry.add(entity["id"])

        print(f"\r{index + 1}/{len(batches)} batches, {len(catalyst)} scenes, {len(catalyst) - len(catalyst.keys() & registry)} missing so far", end="", file=sys.stderr)

    print(file=sys.stderr)
    missing = [scene for scene_id, scene in catalyst.items() if scene_id not in registry]

    for scene in missing:
        scene["manifest"] = head(f"{CDN}/manifest/{scene['id']}_windows.json")
        scene["descriptor"] = head(f"{CDN}/LOD/lods-unity/manifests/{scene['id']}_InitialSceneState.json")

    missing.sort(key=lambda scene: (-scene["parcels"], scene["deployed"]))

    with open("abgen-missing.json", "w") as output:
        json.dump({"region": [x0, y0, x1, y1], "catalystScenes": len(catalyst), "registryScenes": len(catalyst) - len(missing), "missing": missing}, output, indent=2)

    with open("abgen-missing.csv", "w", newline="") as output:
        writer = csv.DictWriter(output, fieldnames=["id", "title", "sdk", "base", "parcels", "deployed", "manifest", "descriptor"])
        writer.writeheader()
        writer.writerows(missing)

    parcels = sum(scene["parcels"] for scene in missing)
    sdk6 = sum(1 for scene in missing if scene["sdk"] != "7")
    print(f"catalyst scenes: {len(catalyst)}")
    print(f"missing from abgen registry: {len(missing)} ({sdk6} sdk6, {len(missing) - sdk6} sdk7), {parcels} parcels")
    print(f"with a manifest on the cdn anyway: {sum(1 for s in missing if s['manifest'] == 200)}, with a descriptor: {sum(1 for s in missing if s['descriptor'] == 200)}")
    print("written abgen-missing.json and abgen-missing.csv")


if __name__ == "__main__":
    main()
