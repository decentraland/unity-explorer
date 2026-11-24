---
name: pulse-face-to-face
description: Face-to-face run of the Unity explorer and the Bevy web explorer on one Pulse deployment. Use when comparing the two clients' multiplayer, pointing Unity at a custom realm's Pulse, or finding why a remote player is not drawn there.
---

# Pulse face-to-face

A **face-to-face** puts this client and the Bevy web explorer (the `decentraland/dcl-one` repository) on the same parcel of one deployment, each signed in with its own account, and compares what each receives over Pulse and draws. It was first run against a dcl-one deployment whose Pulse is not the org's; `realm.example.org` and `realtime.example.org` stand for that deployment's realm and Pulse host below.

Keep wallets, identities and the user's own address out of the repository, commits, PR text and saved logs.

## 1. Read the deployment

- `GET <realm>/about`: `configurations.realmName` (the realm name clients send to Pulse), `comms.adapter`, and the `content.publicUrl` and `lambdas.publicUrl` hosts.
- The Pulse host: open the deployment's web client and read `pulseServer` from the address it rewrites itself to.
- A parcel to meet on. A custom deployment may have no asset bundles for Genesis City, so pick a parcel whose scene this client loads from the realm (`--realm-scene-pointers`).

Done when you hold the realm URL, realm name, Pulse host, gatekeeper URL and parcel.

## 2. One account per client

Two clients on one account fight: the second sign-in takes the rooms and the first shows "this identity is connected elsewhere". Create an account per client, in a file outside the repository:

```bash
python -I .claude/skills/pulse-face-to-face/scripts/test_account.py create <wallets.json> unity-1
python -I .claude/skills/pulse-face-to-face/scripts/test_account.py create <wallets.json> web-1
```

A client draws a remote player only from a profile on the catalyst it reads. The web client creates one at an account's first sign-in, so sign **every** account into the web client once, the Unity one included:

```bash
python -I .claude/skills/pulse-face-to-face/scripts/test_account.py web-login-line <wallets.json> unity-1 <line-file> \
  --name f2funity1 --look 23 --realm https://realm.example.org --position 100,100
```

Send the file's one line as a console command through dcl-one's web driver (`rendering/bevy-explorer/source/local-harness/web-driver/driver.mjs`: `driver.console(line)`), then delete the file. The web client refuses a second account after it enters a realm: use a fresh page per account.

Done when `GET <realm>/lambdas/profiles/<address>` answers with a profile for each account.

## 3. Sign Unity in

With the client closed, store the account's identity in its prefs file, the way explorer-automation's `mf account login` does:

```bash
python -I .claude/skills/pulse-face-to-face/scripts/test_account.py unity-login <wallets.json> unity-1
```

The prefs file is shared by every build on the machine, so this replaces the developer's own sign-in. The script saves it first; `unity-restore <wallets.json>` puts it back when the run is over. Tell the user before the first run.

## 4. Launch Unity

Use the PR's CI player. For dcl.dev, Unity supplies the deployment settings:

```powershell
.\Decentraland.exe --gateway https://dcl.dev --mcp-port 8123
```

For a custom realm whose services come from a separate environment, supply its settings explicitly:

```bash
Decentraland.exe --realm https://realm.example.org --position 100,100 \
  --gatekeeper-url https://realm.example.org/comms-gatekeeper \
  --pulse true --pulse-server realtime.example.org --realm-scene-pointers \
  --debug --landscape-terrain-enabled false --skip-version-check --mcp-port 8123
```

See the [command-line reference](../../../docs/app-arguments.md) for the individual overrides.

A "You are about to use a custom Catalyst" prompt waits at start-up, and the MCP port answers only after it: bring the window to the front and click **CONTINUE** with an operating-system click (the first click may only focus the window). Then drive the client through its MCP server ([`docs/mcp-automation.md`](../../../docs/mcp-automation.md)).

Done when `get_player_state` answers and a screenshot shows the account's name tag in the world. If `--position` was ignored, `move_to` with world coordinates (parcel × 16).

## 5. Bring the web client

Open the deployment's web client on a fresh page, on its own account (step 2's line for `web-1`, or a person signing in by hand), and walk it next to Unity: `/walk_player_to <x> <y> <z> <seconds>` in scene coordinates, z positive. In a browser the driver does not own, console commands go into the chat box, since the app frame is cross-origin to the page.

## 6. Compare

Read both sides at the same moment and save a screenshot from each client.

| Question | Unity | Bevy web |
|---|---|---|
| On Pulse? | Debug panel (bug button, bottom right) → Pulse: Transport State `Connected`, Authenticated `True` | `/pulse_state`: `authenticated` |
| Receives the other? | Pulse: Bytes and Pkts Received rise while the other walks | `/transport_state`: Pulse `players`; `/foreign_movement`: the other's name and position |
| Shares the rooms? | Room: Island → Remote Participants | `/room_diagnostics island`: `remoteCount` |
| Draws the other? | Screenshot: avatar with name tag | Screenshot: avatar with name tag |

A face-to-face is done when every row holds for both clients, or the first row that fails is named with its cause from the table below.

For a crowd, dcl-one's `comms-e2e` scenario `crowd-visit` sends bots to the same realm and parcel; its bots deploy their own profiles, so both clients draw them.

## When a row fails

| Symptom | Cause | Check or fix |
|---|---|---|
| Unity shows the welcome screen after `unity-login` | The stored identity has a day or less left, which the client treats as expired | Keep `--hours` above 24 (default 168) |
| Unity receives the other on Pulse and in the rooms, draws nothing | No profile reached it. Launch with `--use-log-matrix` and category `PROFILE` ([`docs/override-debug-log-matrix.md`](../../../docs/override-debug-log-matrix.md)): `ProfileRepository: get finished … profile:` ends empty | The account has no profile on the realm's catalyst (step 2), or the build reads profiles only from the centralized endpoint (`use-centralized-profiles`) and lacks the catalyst fallback: update the build |
| Web client lists a player it does not draw; `/foreign_movement` says "Waiting for the player profile" | Same: no profile on the catalyst the web client reads | Step 2 |
| "this identity is connected elsewhere" | Two clients share an account | Step 2, then reload the displaced client |
| Pulse handshake refused, "timestamp is ahead of this server" | The machine's clock runs ahead of the server's by more than its window | Compare the clock with NTP; dcl-one's Pulse tolerates 60 s |
| Both authenticated, neither receives the other | Different realm names, or separate islands | Compare `/about`'s realm name with what each client sent; `/room_diagnostics island` and Unity's Room: Island name the island |
| Unity stalls at LoadLandscape | Terrain generation on a deployment without its data | `--debug --landscape-terrain-enabled false` |
