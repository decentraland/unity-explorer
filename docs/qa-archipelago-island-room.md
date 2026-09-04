# QA: Archipelago — the Island room

Archipelago governs exactly one thing in the client: the **Island room**, the global comms room that carries remote avatars. Gatekeeper's **Scene room** is a separate path and appears here only where the two interact — which they do constantly, so it cannot be ignored.

Every step states what a pass looks like (✅) and what to treat as a failure (❌). Report any ❌ with the values from Part 2 and the realm details from Part 5.

---

## Part 1 — Build, launch, and set up the debug menu

### Step 1. Use a build that has the room indicator

The per-room state glyphs every test below is read from come from `feat/room-indicator-livekit-presence`. Use that branch, or `dev` once it has merged.

```bash
git checkout feat/room-indicator-livekit-presence && git pull
```

- ✅ **Room: Info** (Step 7) has an `Avatars on LiveKit` row. It only exists with the change.
- ❌ It does not. On a build without it a Pulse-carried avatar reports `Pulse` and nothing else, and Tests 2, 3 and 4 cannot be executed at all. Do not report results from such a build.

### Step 2. Launch against **zone**, with debug enabled

```
decentraland.exe --dclenv zone --debug
```

- `--dclenv zone` points every backend at `decentraland.zone`. **The server side of this feature is deployed to zone only** — on `org` there is nothing to test.
- `--debug` builds the room widgets. Without it they do not exist.

- ✅ The client reaches world and the debug panel is visible.
- ❌ No panel (see `--debug` above), or you land on an org realm — zone is a separate account space on Sepolia, so **log in with a zone/test wallet**, not a mainnet one.

### Step 3. Confirm both flags actually took

Type `/app-args` in chat.

- ✅ The listing shows `dclenv` set to `zone` and `debug` present.
- ❌ Either is missing. The client is not testing what you think it is — fix the launch command and restart. Do not proceed.

### Step 4. Learn the panel toggle

Type `/debug` in chat.

- ✅ The panel hides; typing `/debug` again shows it.
- ❌ `Unknown command` — the debug flag did not apply. Return to Step 2.

### Step 5. Confirm the room widgets exist

Type `/debug help` in chat.

- ✅ The listing includes **Room: Island**, **Room: Scene** and **Room: Info**.
- ❌ Any of the three is missing. Stop — the rest of this document cannot be executed. Report the build and branch.

### Step 6. Open the Island widget

Expand **Room: Island**.

- ✅ It shows the rows listed in Part 2, plus a button reading **Deactivate**.
- ❌ The widget is present but empty, or the button reads **Activate** at startup — the room should start activated.

### Step 7. Turn on the room indicators

Expand **Room: Info** and enable **Show Room Indicator**.

- ✅ Each remote avatar's nametag gains a debug line naming the rooms that account for it, each prefixed by a state glyph — `🟢Gatekeeper 🔗Island ⚡Pulse` and combinations of it. Part 3 is the legend. Your own avatar is not tagged.
- ❌ No debug line appears while other avatars are visible, or a tag reads `None` for an avatar that is on screen.

> Leave this toggle on for the whole session. Every test below is read from it.

---

## Part 2 — Reading "Room: Island"

| Row | Healthy value | What it means |
| --- | --- | --- |
| `Room State` | `Running` | The room's lifecycle |
| `Connecting State` | `ConnConnected` | **The LiveKit session — the field that says comms actually works** |
| `Attempt to Connect` | not stuck retrying | A connection attempt in progress |
| `Connection Loop` | healthy | The loop that sends position heartbeats |
| `Connection Quality` | `Excellent` / `Good` | LiveKit's own quality signal |
| `Remote Participants` | tracks nearby players | Peers in your island |
| `Room Sid` | `RM_…` | **The island you are in. Changes on reassignment** |
| `Self Sid` | `PA_…` | Your participant id |

`Not connected` in the Sid rows only means `Room State` is not `Running` yet — read `Room State` first.

---

## Part 3 — Reading the room indicator

Needed to judge Tests 2–4.

The tag above a remote avatar names every room that accounts for it, each prefixed by a glyph. The glyph separates two facts that are **not** the same thing:

| Glyph | Means |
| --- | --- |
| `🟢` | LiveKit's participant roster lists the wallet in that room **and** that room's data channel delivered its profile announcement |
| `🔗` | LiveKit lists the wallet in that room, but it never announced over it |
| `👻` | an announcement arrived from a wallet the roster no longer lists — stale, or a room hand-off in flight |
| `⚡` | the profile arrived over Pulse. Pulse publishes no roster, so it has this one state only |

So `🟢Gatekeeper 🔗Island ⚡Pulse` reads: *in the Scene room and announcing over it; in the Island room but silent on it; and carried by Pulse.*

**Which glyph is normal depends on the transport, and both are healthy.** With Pulse active — the default whenever the `pulse` feature flag is on — no client announces its profile over LiveKit at all, so LiveKit rooms read `🔗` and every avatar carries `⚡`. Launch with `--pulse false` and the LiveKit announcements come back, so the rooms read `🟢` and no avatar carries `⚡`. Only `👻` is worth reporting on sight.

> The glyph is what makes this test possible under Pulse. Before it, a Pulse-carried avatar reported `Pulse` and nothing else, so the tag could not tell you whether the avatar was on LiveKit at all.

**Who owns the avatar.** The rooms **overlap on purpose**. A player in your scene is normally seen by both. The client keeps **one avatar per wallet** and records the sources as a set of flags:

- The first source to see a wallet creates the avatar.
- A second source seeing the same wallet only adds its flag — **no duplicate avatar**.
- A source dropping the wallet removes its flag. The avatar is destroyed **only when the last flag goes**.

Under Pulse, Pulse is normally that last flag: it creates the avatar and it is what keeps it alive. Archipelago is still the safety net at scene borders in the sense that matters here — the avatar must not blink out when the Scene room drops the player.

**Counting.** `Room: Info` reconciles the avatar roster against LiveKit's:

| Row | Means |
| --- | --- |
| `Active Avatars` | avatars the client is showing |
| `Avatars on LiveKit` | of those, how many LiveKit also lists in the Island or Scene room |
| `Avatars off LiveKit` | the remainder — an avatar with no LiveKit session behind it |
| `LiveKit w/o Avatar` | participants in the Island or Scene room with no avatar |

The last two are **not failures on their own**: the Island room covers a wider area than the transport that creates avatars, so it legitimately lists people you cannot see, and the reverse happens briefly during hand-offs. The load-bearing reading is `Avatars on LiveKit`: it must be non-zero whenever both rooms are `Running` and other players are nearby. Zero there while `Remote Participants` is non-zero means none of the avatars on screen has a LiveKit session behind it.

---

## Part 4 — Tests

### Test 1 — Connects on login

1. Log in and stand still.
2. Read **Room: Island**.

- ✅ Within a few seconds: `Room State: Running`, `Connecting State: ConnConnected`, `Room Sid` populated.
- ❌ `Connecting State` never reaches `ConnConnected`; `Room Sid` stays empty beyond ~30 s; or `Attempt to Connect` cycles forever. **Do Part 5 before filing** — an unassigned island looks exactly like this and is a backend state, not a client bug.

### Test 2 — Avatars arrive tagged, both rooms on

1. Go where other players are (Genesis Plaza).
2. Read the nametag tags and both widgets.

- ✅ Players **in your scene** carry both a `Gatekeeper` and an `Island` entry; players **outside your scene** carry `Island` only. Both LiveKit rooms show the same glyph as each other — `🔗` with Pulse on, `🟢` with `--pulse false` — and with Pulse on every avatar also carries `⚡Pulse`. `Remote Participants` is non-zero and `Avatars on LiveKit` is non-zero.
- ❌ Two avatars for one player; an on-screen avatar tagged `None`; **`Avatars on LiveKit` is 0** while `Remote Participants` is not; nobody carries an `Island` entry while players are clearly outside your scene; or an avatar carries `Island` with no Pulse entry and no `Gatekeeper` entry while Pulse is on.

### Test 3 — Scene-border handoff

1. With another player, walk together across a scene border.
2. Watch their nametag tag and their avatar continuously.

- ✅ The `Gatekeeper` entry appears and disappears as the Scene room picks them up or drops them, while the `Island` entry stays, and **the avatar never disappears, reloads or flickers**. A `👻` on the dropped room for a second or two during the hand-off is expected.
- ❌ The avatar vanishes and respawns at the border; the nametag blanks; wearables reload; the tag drops to `None` while the player is still visible; or a `👻Gatekeeper` sticks for more than a few seconds after they left your scene.

### Test 4 — Prove each room's contribution with Deactivate

Do this while a player **in your scene** is visible and carries both a `Gatekeeper` and an `Island` entry.

**Read the expected outcome off the transport first.** Deactivating a LiveKit room removes its entry from the tag either way; whether the *avatar* survives depends on who created it. With **Pulse on**, Pulse owns every avatar, so no LiveKit room can remove one — the tag is the whole result. With **`--pulse false`**, LiveKit owns them and step 4 clears the screen. Run this test whichever way you are already testing, and use the matching column.

1. In **Room: Scene**, press **Deactivate**. Confirm `Room State: Stopped`, `Connection Loop: Stopped`, `Attempt to Connect: None`.
   - ✅ Their `Gatekeeper` entry disappears and **the avatar stays**.
   - ❌ The `Gatekeeper` entry survives a stopped room; or (with `--pulse false`) the avatar disappears.
2. Press **Activate** on Room: Scene and wait for the `Gatekeeper` entry to come back.
3. Press **Deactivate** on **Room: Island**.
   - ✅ Their `Island` entry disappears and **the avatar stays**. With `--pulse false`, players *outside* your scene disappear; with Pulse on they stay, now with no `Island` entry.
   - ❌ The `Island` entry survives a stopped room; or, with `--pulse false`, a player outside your scene is still visible.
4. Deactivate **both**.
   - ✅ Every tag drops to `⚡Pulse` alone, and `Avatars on LiveKit` drops to `0`. With `--pulse false`, all remote avatars disappear instead.
   - ❌ Any LiveKit entry remains on any tag; `Avatars on LiveKit` stays above 0; or, with `--pulse false`, a remote avatar remains.
5. Re-activate both before continuing.

> While a room is deactivated, do not file bugs about what it carries — with Room: Scene off, in-scene voice and scene streams are expected to be dead.

### Test 5 — Island reassignment while moving

1. Deactivate **Room: Scene** and leave it off, so avatar churn is Archipelago's alone. Deactivation is sticky: it survives scene changes, teleports and realm changes.
2. Walk a long distance in a straight line, watching `Room Sid`.

- ✅ `Room Sid` changes at least once. After each change `Connecting State` returns to `ConnConnected` and the set of avatars carrying an `Island` entry turns over — old ones lose it, new ones gain it. A brief flicker during the switch is normal. With Pulse on, avatars from the old island may remain on screen without an `Island` entry; that is Pulse's own area of interest, not a stale island.
- ❌ `Room Sid` never changes over a long traverse; `Connecting State` does not return to `ConnConnected` after a change; or avatars keep an `Island` entry long after the sid changed.

### Test 6 — Realm change and teleport

1. Keep Room: Scene deactivated.
2. Teleport to another realm or a world, then back.

- ✅ The room restarts: `Room State` leaves `Running` and returns to it with a **new** `Room Sid`. No avatar from the previous realm still carries an `Island` entry.
- ❌ `Room State` never returns to `Running`; the old `Room Sid` persists into the new realm; or an avatar from the previous realm keeps its `Island` entry.

### Test 7 — Reconnect after network loss

1. Disable the network for ~20 s.
2. Re-enable it and watch **Room: Island** without touching anything.

- ✅ `Connecting State` leaves `ConnConnected`, then recovers on its own. Recovery can take ~15–20 s: the client retries on a 5 s backoff and forces a fresh handshake after 3 attempts.
- ❌ It never recovers without a client restart; it recovers but avatars never come back; or `Connection Loop` stays stopped.

### Test 8 — Same wallet elsewhere

1. Log in with the **same wallet** on a second machine.
2. Watch the first client.

- ✅ The first session is kicked and `Room State` leaves `Running`.
- ❌ The first client hangs silently in `Running` with no participants; or both sessions stay connected.

---

## Part 5 — Triage before filing

**An unassigned island is indistinguishable from a client hang.** The server sends nothing unprompted after the handshake, so with no island assigned the client sits on a healthy connection receiving nothing. Rule that out first.

### Step 1. Check the realm the explorer actually uses (zone)

```bash
curl -s https://realm-provider-ea.decentraland.zone/main/about
```

Read `healthy`, `acceptingUsers` and the `comms` block.

- If `healthy: false` or `acceptingUsers: false` → the realm's comms are down. **Not a client bug.**
- ⚠️ This is a **different host** from `peer.decentraland.zone`, which serves a different realm. The explorer resolves Genesis from `realm-provider-ea`; checking the wrong host gives a healthy answer about a realm nobody is on.

### Step 2. Decide what you have

- **Backend or environment:** no island was ever assigned — `Room Sid` stayed empty all session.
- **Client bug worth filing:** an island *was* assigned (`Room Sid` populated at least once) and the client still failed to reach `ConnConnected`, or failed to recover in Tests 3–7.

### Step 3. Include in the report

- All rows from **Room: Island** (Part 2), plus the same from **Room: Scene** if the test involved both.
- The realm name and `comms` block from Step 1.
- Whether the room indicator was on, and the exact tag text on the affected avatar, glyphs included.
- The four `Room: Info` counters, and whether the session ran with Pulse on or `--pulse false`.
- Build number, and whether `--debug` or a Debug build was used.
