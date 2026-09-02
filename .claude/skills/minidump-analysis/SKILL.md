---
name: minidump-analysis
description: "Read call stacks out of the Windows minidumps attached to Explorer ANR reports in Sentry (UNITY-EXPLORER-PBX and friends). Use whenever a .dmp or .zip dump attachment needs analyzing, an ANR needs a call stack, someone asks where the main thread was stuck or who blocked it, or symbols need verifying against a dump before the names can be trusted. Covers the WinDbg + llvm-pdbutil workflow and a bundled headless parser that works with no Windows and no symbols."
user-invocable: true
---

# Minidump Analysis (ANR dumps)

## What these dumps are

`DclAnrIntegration` runs a watchdog thread that samples a UI heartbeat every 250 ms. At
**2500 / 3750 / 5000 ms** of main-thread silence it calls `MiniDumpWriteDump` on the process
and attaches the result to the Sentry event. So one ANR can carry up to three dumps, each a
snapshot of the *same* hang at a later moment.

Two constraints to know before you start:

- **Windows only.** `ThreadsDumpUtility` is `#if UNITY_STANDALONE_WIN`; macOS ANRs report
  `Report Error( MacOS doesn't support dumps )` and carry no attachment at all.
- **Sentry does not symbolicate these itself.** The dumps are zipped and attached as
  `AttachmentType.Default`, so Sentry stores them as inert blobs — every ANR event reads
  *No stacktrace available* no matter how many dumps it carries. You must analyze them by hand.
  (If a dump arrives as a raw `.dmp` attached as `AttachmentType.Minidump`, that changed —
  check whether Sentry now shows a thread list before doing this by hand.)

## Getting the attachment

Via the Sentry MCP, list then download:

```
execute_sentry_tool(name='get_event_attachment', arguments={
    organizationSlug: 'decentraland', projectSlug: 'unity-explorer', eventId: '<32-hex event id>' })
# then again with attachmentId: '<id>' to download
```

The download is saved to a local path the result names. `search_issue_events` with
`query: 'environment:production message:"*5000ms*"'` finds events that reached the deepest
threshold — those hung longest and have all three dumps.

## Fast path — headless triage (no Windows, no symbols)

```bash
python3 .claude/skills/minidump-analysis/scripts/triage.py 'dumps/*.zip'
python3 .claude/skills/minidump-analysis/scripts/pdbid.py dump.zip gameassembly
```

`triage.py` accepts `.dmp` or the Sentry `.zip` and prints, per dump:

- **exact session age at capture** (process creation time vs. dump time) — this is the single
  most useful discriminator: hangs inside the first ~3 minutes are load/startup problems,
  hangs in hours-old sessions are a different population entirely
- **the main thread's instruction pointer** as `module+offset`
- **a histogram of modules its stack touches**, plus coarse hints (`blocking DNS resolve`,
  `window / IME message loop`, `running managed code (not blocked)`, …)

Add `--frames` to list stack pointers in order.

**What it gives you:** which subsystem the main thread was in, and whether it was *blocked*
(rip in `ntdll`/`KERNELBASE`) or *burning CPU* in our own code (rip in `GameAssembly`, many
managed pointers). That is usually enough to route a bug.

**What it cannot give you:** function names. The stack listing is a scan for module-resident
pointers, not a real unwind — module attribution and `rip` are reliable, exact frame ordering
is not. For names you need the full path below.

## Full path — symbolicated stacks in WinDbg

Requires a Windows machine with [WinDbg](https://learn.microsoft.com/windows-hardware/drivers/debugger/)
and [LLVM](https://llvm.org) (`llvm-pdbutil`).

1. **Unzip the attachment** → a single `.dmp`.
2. **Identify the build.** The Sentry event's `release` tag (e.g. `v0.173.0-alpha-main`) names
   it. Cross-check against the dump itself: `pdbid.py dump.zip` prints each module's PDB name
   and signature, and `Decentraland.exe` resolves to e.g.
   `WindowsPlayer_player_Master_il2cpp_x64.pdb`.
3. **Download that build's debug symbols** — the `<artifact_name>_debug_symbols` artifact from
   the matching `build-unitycloud.yml` run. Unzipping gives
   `Decentraland_BackUpThisFolder_ButDontShipItWithYourGame/` containing `GameAssembly.pdb`.
4. **Verify the PDB matches the dump. Do not skip this.**

   ```powershell
   llvm-pdbutil.exe dump -summary GameAssembly.pdb | rg GUID
   #   GUID: {97FC336F-3751-455B-8532-C9023B963F2D}
   ```

   Compare against the dump's expected signature:

   ```bash
   python3 scripts/pdbid.py dump.zip gameassembly
   ```

   > **A PDB from the wrong build still loads and produces plausible-looking, completely wrong
   > names.** WinDbg will not warn you. GUID equality is the contract; this five-second check
   > saves hours of chasing functions that were never on the stack.

5. **Open the dump in WinDbg** (File → Open dump file) and dump every thread:

   ```
   ~*k
   ```

   Point the symbol path at both the build symbols and Microsoft's server — OS modules
   (`ntdll`, `KERNELBASE`, `dnsapi`) carry their own PDB signatures and resolve from the
   public server:

   ```
   .sympath+ srv*https://msdl.microsoft.com/download/symbols
   ```

## Reading an ANR stack

`~*` means every thread, `k` prints the call stack. **For an ANR always read all threads:**

- **Thread 0 (`"Unity Main Thread"`) shows _where_ the main thread is stuck.**
- **The other threads show _who_ blocked it.**

Frame 0 tells you the kind of stall:

| Frame 0 | Meaning |
|---|---|
| `ntdll!NtWaitForAlertByThreadId` → `RtlWaitOnAddress` → `KERNELBASE!WaitOnAddress` → `baselib!Baselib_SystemFutex_Wait` | futex/semaphore wait — the main thread is waiting on another thread |
| `ntdll!RtlSleepConditionVariableSRW` / `KERNELBASE!SleepConditionVariableSRW` | condition-variable wait |
| `dnsapi` + `rpcrt4` frames | blocking DNS resolution (`getaddrinfo`) |
| `KERNELBASE!RegQueryValueExA` / `NtQueryValueKey` | registry read |
| rip inside `GameAssembly` with a deep managed stack | not blocked at all — running our own IL2CPP code, i.e. genuinely slow work |

## Symbol availability — the practical limit

In `build-unitycloud.yml` the `_debug_symbols` artifact is uploaded **only when
`is_release_build == true`** and kept with **`retention-days: 7`**. So:

- ANRs from non-release builds have no downloadable symbols.
- ANRs older than a week may have none either, even for release builds.

The separate `sentry-cli debug-files upload build` step (gated on `sentry_enabled`) pushes
symbols to Sentry, which helps once dumps are attached as real minidumps but does nothing for
local WinDbg use.

## Known recurring findings

Recognize these rather than rediscovering them:

- **`MultiThreadSync.Acquire`** — mutex stall, spotted in ~15 dumps. Needs better
  synchronization (a channels approach was proposed).
- **`MinimumSpecsGuard.GetAllDrivesInfo`** → `GetDiskFreeSpaceEx`, a very heavy blocking
  native call that must not run on the main thread.
- **`PlayerPrefs.HasKey`** → `KERNELBASE!RegQueryValueExA` on every `SendPlayerNetMovement` —
  networking touching the registry every frame; wants a cache layer.
- **Avatar `TransformAccessArray`** — O(n) complexity on dispose.
- **`ENetTransport.ConnectAsync`** → `Address.SetHost` doing a blocking `getaddrinfo` on the
  main thread (`dnsapi` + `enet` + `rpcrt4` on the stack).

## Sanity check

`/anr-simulate 10000` (debug-only chat command) freezes the main thread for 10 s and crosses
all three thresholds. A symbolicated dump of it should show
`Thread::SleepInternal` → `AnrSimulateChatCommand_ExecuteCommandAsync` — a good end-to-end
check that your symbols and workflow are correct before trusting a real dump.

## Provenance

The WinDbg workflow, the GUID-verification gate and the thread-reading rules come from the
*Explorer Talks — dmp files* session (2026-06-11, presented by Nick Khalow) and its deck.
The bundled scripts were written and validated against 17 real `UNITY-EXPLORER-PBX`
attachments. The `creating-skills` eval cycle (baseline / with-skill / adversarial) has **not**
been run against this skill yet.