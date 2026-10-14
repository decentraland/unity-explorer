# Delta Updates

Launcher users update by downloading small binary patches instead of the full release zip. For each
Explorer release, CI publishes per-file [HDiffPatch](https://github.com/sisong/HDiffPatch) patches
from each of the last N published releases, plus a manifest. The launcher always targets the latest
release: it applies the patch set for its installed version when one exists, and falls back to the
full zip otherwise.

Epic builds are excluded: the Epic store ships its own updates.

## Why per-file binary patches

Measured on v0.185.0-alpha -> v0.186.0-alpha (Windows, 868.9 MB extracted, 130 files):

| Update method                                 | Download    | vs. full zip |
|-----------------------------------------------|-------------|--------------|
| Full zip                                      | 458.6 MB    | -            |
| Only the changed files, whole                 | ~200 MB     | -56%         |
| Content-defined chunking (bitar/desync-like)  | ~186 MB     | -59%         |
| zstd `--patch-from` per file (-3 / -19)       | 97 / 57 MB  | -79% / -88%  |
| **HDiffPatch per file (lzma2)**               | **30.7 MB** | **-93%**     |
| HDiffPatch over the two zips                  | 182.0 MB    | -60%         |

- Diff **extracted files**, never archives: zip deflate scrambles the bytes.
- Chunking gains almost nothing: IL2CPP shifts offsets throughout `GameAssembly.dll`, and the
  Addressables bundles are LZ4HC-compressed. A bsdiff-class differ is needed.
- Patches rebuild byte-identical files, so code signatures stay valid.
- About 16 of 130 files change per release. Three are bundles renamed by their content hash
  (`<name>_<32 hex>.bundle`), which are paired with the old bundle of the same `<name>`.

## Pipeline

```
build-release-main.yml (push to main)
    |
    +--> build              build-unitycloud.yml: build, sign, zip -> S3 releases/<tag>/
    |
    +--> delta              build-release-delta.yml (needs: build)
    |                         plan:  refuse a published tag, pick the last N published releases
    |                         delta: [windows64 on ubuntu, macos on macos], a state machine
    |                                drained in YAML slices (`delta run --until <state>`),
    |                                see "Lifecycle" below; the manifest goes out last
    v  (workflow_run: success)
build-release-main-page.yml  -> draft GitHub release
    |
    v  (human publishes)
s3-latest-release.yml       -> releases/latest.json, then prune old delta/ folders
```

- Delta generation is a job of `build-release-main.yml`, not a separate `workflow_run` workflow. It
  receives the exact `tag_version` the build used (the version action derives the next tag from git
  tags, which the page workflow is about to create), and the draft release only appears once the
  patches are uploaded.
- It never blocks a release: both jobs are `continue-on-error`. A failure shows as an error
  annotation, and launchers fall back to the zip and report it. A base whose patches fail to
  generate or verify is left out of the manifest; the other bases still ship. A **cancelled** delta
  job (or a lost runner) is different: the run ends `cancelled` and no draft release is created. Use
  "Re-run failed jobs" on the run, or draft the release manually.
- A new launcher zip invalidates the patch set made for the previous zip of the same unpublished
  tag and platform, so the zip upload in `build-unitycloud.yml` removes `<tag>/delta/<platform>/`
  before it replaces the zip (Epic zips have no patches). Each delta run removes the same folder
  again as a second line of defence.
- `latest.json` is unchanged and stays the only switch that makes a version live.
- [`build-release-delta.yml`](../.github/workflows/build-release-delta.yml) also has a
  `workflow_dispatch` (`tag`, `base_count`) for regenerating a release that is **not published yet**.

## S3 layout

```
@dcl/unity-explorer/releases/
    latest.json                                    unchanged
    <tag>/Decentraland_<target>.zip                unchanged: fresh installs and fallback
    <tag>/delta/<platform>/manifest.json           uploaded last
    <tag>/delta/<platform>/from/<base tag>/<build id>/<path>.hdiff
```

`<platform>` is `windows64` or `macos`; `<build id>` is the first eight hex digits of the release
zip's sha256. The manifest URL is derived by convention; a 404 means "no delta, use the zip". The
launcher never derives a patch URL: it follows the `patch` strings of the manifest.

**Immutable once published.** Patches are uploaded with a one-year immutable `Cache-Control`. The
manifest is uploaded without it: a rebuild of a still-unpublished tag rewrites it under the same
key, and a CDN edge must not keep serving the old one. Patch keys include the build id, so a
rebuild of an unpublished tag writes its patches under new keys and an edge that cached the previous
build's patches is never asked for them again. Nothing is rewritten after the release is
published: the workflow refuses to generate or upload for a published tag. A missing or broken delta set after publication is not fixed
in place; launchers fall back to the zip until the next release.

**Retention: always exactly two series.** When release X is published (not on later edits),
`s3-latest-release.yml` deletes `releases/<X-2>/delta/` (and a few older ones, to clean up after
missed runs), never the live release's. The patches
into X (live) and into X-1 (just replaced) stay, so:
- a launcher that read the old `latest.json` right before the switch still finds its patches for a
  whole release cycle, so nothing has to track in-flight downloads;
- a rollback by [`on-delete-release.yml`](../.github/workflows/on-delete-release.yml) to X-1 finds
  its patches still there;
- the rule depends only on version order (a late publish of an older tag cannot evict a newer
  release's series). A hotfix shortens the window to hours, still far longer than any download.

Deleting a published release also removes its `delta/` folder in `on-delete-release.yml`, since the
prune only ranks releases that still exist. Its zip stays. GitHub sends no `deleted` event for
drafts: a draft's folder is replaced by the next build of the same tag, and a tag that is never
published keeps its folder until it is removed by hand.

Publishing a release as a pre-release removes its `delta/` folder right away in
`s3-latest-release.yml`: `/releases/latest` never serves it and the prune never ranks it. Turning
it into a full release later does not regenerate the patches; launchers use the zip for it.

## Manifest

[`releases/latest.json`](../.github/workflows/s3-latest-release.yml) stays
`{ "version", "timestamp" }`. The delta manifest is a separate file per release and platform:

```json
{
  "version": "v0.187.0-alpha",
  "platform": "windows64",
  "zip": { "path": "Decentraland_windows64.zip", "size": 458623479, "sha256": "..." },
  "files": [
    { "path": "GameAssembly.dll", "size": 110796288, "sha256": "...", "mode": "644" },
    { "path": "Decentraland.app/Contents/Frameworks/X.framework/Versions/Current", "symlink": "A" }
  ],
  "bases": {
    "v0.186.0-alpha": {
      "deleted": ["Decentraland_Data/StreamingAssets/aa/StandaloneWindows64/ui_assets_all_<old>.bundle"],
      "patches": [
        {
          "path": "GameAssembly.dll",
          "from_path": "GameAssembly.dll",
          "from_sha256": "...",
          "patch": "from/v0.186.0-alpha/1a2b3c4d/GameAssembly.dll.hdiff",
          "patch_size": 8347352,
          "patch_sha256": "..."
        }
      ],
      "whole": [
        {
          "path": "Decentraland_Data/StreamingAssets/aa/catalog.hash",
          "patch": "from/v0.186.0-alpha/1a2b3c4d/Decentraland_Data/StreamingAssets/aa/catalog.hash.hdiff",
          "patch_size": 58,
          "patch_sha256": "..."
        }
      ]
    }
  }
}
```

- `version` and the `bases` keys are release tags, exactly as the launcher stores its version.
- `zip`: the published full zip the manifest was generated from.
- `files`: the complete new build, sorted by path.
  - `mode`: POSIX permission bits. Required on macOS (the build ships as a tar inside the zip, and
    `.app` executables need their executable bit); informational on Windows.
  - `symlink`: symlink entries with their target, no size or hash (`.app` frameworks use them).
- `patches`: changed files, patched from `from_path` in the base. `from_path` differs from `path`
  for renamed bundles. `patch` is relative to the manifest's folder.
- `whole`: files with no base counterpart, and files whose patch is at least half the file's raw
  size and larger than the file encoded alone (a tie keeps the patch). They are HDiffPatch patches against an **empty**
  file, so `hpatch` is the only codec the launcher needs.
- `deleted`: base paths absent from the new build, including the old names of renamed bundles.
- Unchanged files appear only in `files`.
- Folders exist only through the entries under them. A build with an empty folder is refused at
  generation time, so a patched install never lacks a folder the zip would have created.

## Launcher contract

Implemented in the launcher repo:

1. Read `latest.json`. If the installed version equals it, stop.
2. Fetch `releases/<version>/delta/<platform>/manifest.json`.
3. If the installed version is in `bases` and every `from_sha256` matches the local file:
   1. check free disk space;
   2. download the patches and `whole` files (checking `patch_sha256`) and apply them into a staging
      folder holding only the changed and new files;
   3. verify every `files[].sha256` across staging plus the untouched files. Modes and symlinks
      come from `files[]`, not from `bases`: a file whose content is unchanged can still change its
      `mode`, and a symlink can change its target, with no `patches`/`whole` entry.
4. Otherwise **explicitly fall back to the full zip**. Every fallback except "installed version too
   old" (expected) also sends a loud error to Sentry, e.g.
   `log::error!("delta update failed, falling back to zip: {}", reason)`, with the installed
   version, the target version and the reason. A missing manifest means CI delta generation failed
   for that release.
5. Delete the base's `deleted` paths (and the directories they leave empty), then swap the staged
   files into place, replacing (never writing through) any existing file or symlink at a written
   path (a symlink in the base can be a regular file in the new build at the same path, and the
   reverse), apply every `files[]` `symlink` and `mode` (unchanged files included), verify
   `files[]` again, and keep the previous files until the new version launches (rollback). The
   order matters: on case-insensitive file systems a case-only
   rename (`Foo.dll` -> `foo.dll`) deleted after the swap removes the new file, and a directory
   replaced by a symlink (`Versions/Current` -> `A`) deleted after the swap removes files in the
   symlink's target. A `deleted` entry can be a symlink to a directory (`Lib -> Fw/Versions/A`):
   remove the link itself, never recurse into it, or the files of its target go too. The tool's
   `ReplayPatches` cmd replays this order.
6. Leave files that are not in the manifest alone (logs, launcher-owned files).
7. Reject the manifest (fall back to the zip and report it) if any `path`, `from_path`, `deleted`
   entry or `patch` is absolute, contains an empty, `.` or `..` segment, a segment ending in `.` or a
   space, a Windows device name (`CON`, `PRN`, `AUX`, `NUL`, `COM1`-`COM9`, `LPT1`-`LPT9`, with or
   without an extension), or contains `\`, `:`, `<`, `>`, `"`, `|`, `?`, `*` or a control character
   (NUL included), and if a `symlink` target is absolute, contains any of those characters or a
   control character, or resolves outside the install folder. On NTFS those characters reach
   outside the folder or into another file (`..\x.dll`, `C:x.dll`, `\\host\share`,
   `Decentraland.exe:ads`). CI generation (the `RelPath` and `Tree` types) already refuses such
   builds; the launcher checks again because the manifest is not signed.

Patching uses HDiffPatch's `hpatch` (MIT, C), ideally linked into the launcher so no extra executable
needs code signing.

## The delta tool

All delta logic lives in [`scripts/delta/`](../scripts/delta/), a Rust crate built with the
toolchain pinned in its `rust-toolchain.toml` (`cargo build --release`, then `delta --help`). The
domain follows *Domain Modeling Made Functional*: simple types that can only hold valid values, one
type per lifecycle state, errors as values. The run is an Elm-style machine in the shape of the
client's `SelfProfile` FSM: `init(request)` and `update(run, msg)` return the next state and the
cmds that state needs, as a pair; a `Msg` is a fact from I/O; the executor runs a `Cmd` and
answers with the next `Msg`; `Cmd::Complete` asks the runtime to stop.

### Vocabulary

| Term | Meaning |
|---|---|
| Release | The release the patches lead to. |
| Base | An earlier published release a patch set starts from. |
| Build / Tree / Node | An extracted release folder; every file and link in it by `RelPath`; one `File { size, sha256, mode }` or `Link { target }`. |
| Plan / Job | What one base needs: deleted paths, and per changed file a `Diff { from }` or `Whole`. |
| Patch / Whole / Delta | One encoded file against a base file or an empty file; the patch set of one base. |
| Manifest | The launcher contract above; `ManifestDto` is its exact JSON, kept apart from the domain type. |
| Drop | A base taken out of the run with its reason; the other bases continue. |
| Run / State | The model of the machine: the current state and the bases dropped so far. |
| Msg | A fact from I/O, past tense (`ArchivesDownloaded`, `PatchesReplayed`, `IntegrityFailure`). |
| Cmd | What the machine wants done (`DownloadArchives`, `ReplayPatches`, `Complete`). |

### Modules

| Module | Holds |
|---|---|
| `builds` | `RelPath`, `Sha256`, `Mode` (validated with `nutype`), `Node`, `Tree` (refuses entries inside a file or link and links leaving the build), archive member checks, scanning and safe extraction. |
| `releases` | `Tag` (semver, ordered by version), `Platform`, `Bases<T>` (never empty), the GitHub release DTO, picking bases, pruning, the published-tag lookup. |
| `patching` | `plan`, the whole-file rule, `Delta`, the launcher's replay order (`replay_actions`), `differences`, encoding and replaying with `HDiffPatch`. |
| `publishing` | `Manifest` (refuses inconsistent bases), `ManifestDto`, bucket keys, the upload layout. |
| `run::state` | The `State` enum in lifecycle order, its payloads, `DropReason`, the `Run` model. |
| `run::fsm` | `Msg`, `Cmd`, `init`, `update` and `judge`. Pure: no file system, processes or environment. |
| `run::execute` | `execute(cmd, io) -> Msg`: the only impure part of a run. It never sees the state. |
| `run::runtime` | The drain loop between them, and the document holding `{ run, cmds }`. |
| `io` / `shell` | The traits `Releases`, `Bucket`, `Signer` and the concrete `HDiffPatch`; their implementations with `gh`, `aws`, `@dcl/cdn-uploader`, `hdiffz`/`hpatchz`, `osslsigncode`/`codesign`, and with local folders for tests and local runs. |

### Lifecycle

```
Requested -> Unpublished -> Cleared -> Downloaded -> Extracted -> Scanned -> Encoded
          -> Verified -> Prepared -> Rechecked -> PatchesUploaded -> ReadyToPublish -> Published
any       -> Failed (terminal)
```

| State | Cmd it issues | Msg it waits for | Decided in `update` on that msg | Next |
|---|---|---|---|---|
| `Requested` | `RecheckRelease` | `ReleaseRechecked` | a published release fails the run before anything is removed | `Unpublished` |
| `Unpublished` | `RemoveStalePatches` | `StalePatchesRemoved` | the download keys | `Cleared` |
| `Cleared` | `DownloadArchives` | `ArchivesDownloaded` | a base without a zip is dropped | `Downloaded` |
| `Downloaded` | `ExtractArchives` | `ArchivesExtracted` | a base that fails is dropped | `Extracted` |
| `Extracted` | `ScanBuilds` | `BuildsScanned` | a base that fails is dropped; the `Plan` of every base | `Scanned` |
| `Scanned` | `EncodePatches` | `PatchesEncoded` | a base that fails is dropped; the replay order | `Encoded` |
| `Encoded` | `ReplayPatches` | `PatchesReplayed` | `judge`: the rebuilt build must equal the release, signatures included; the `Manifest` and its JSON | `Verified` |
| `Verified` | `PrepareUploadFolder` | `UploadFolderPrepared` | - | `Prepared` |
| `Prepared` | `RecheckRelease` | `ReleaseRechecked` | still unpublished and the same zip `ETag`, or the run fails | `Rechecked` |
| `Rechecked` | `UploadPatches` (immutable) | `PatchesUploaded` | - | `PatchesUploaded` |
| `PatchesUploaded` | `RecheckRelease` | `ReleaseRechecked` | the same check again, seconds before the manifest goes out | `ReadyToPublish` |
| `ReadyToPublish` | `PublishManifest` (revalidated) | `ManifestPublished` | - | `Published`, `Complete` |
| any | | `IntegrityFailure`, or a msg that does not fit | the reason | `Failed`, `Complete` |

`Requested` is created by `delta create-run` from the request (tag, platform, zip, bucket prefix,
bases, work folder). `RecheckRelease` runs three times: before anything is removed from the
bucket, before the patches go out, and again before the manifest, so a publish or a rebuild during
the run never loses a live release's patches and never gets this run's manifest. No base left at any point fails the run. Patch keys carry
the build id (`from/<base>/<build id>/<path>.hdiff`), so a rebuild never reuses a key.

`delta run run.json [--until <state>]` drains the document like `FsmRuntime.Drain`:

```
(run, cmds) = load(run.json)                  the state and the cmds it was paused with; already at or past `until`: stop
loop:
  cmd = cmds.pop_front()                      Complete: stop, exit by the state (Published 0, Failed 1)
  msg = execute(cmd, io)                      impure; a cmd that cannot run at all answers IntegrityFailure
  (run, next) = update(run, msg)              pure; failed bases dropped; cmds.extend(next)
  save(run.json, run, cmds)                   atomic replace, after every transition
  if run.state == until: stop, exit 0         the next `run` resumes from here
```

Only a document paused by `--until` or ended by a msg can be resumed. A cmd interrupted mid-execution
may have consumed its inputs (extraction removes the zips, replay removes the base builds), so a
killed step is not resumed on its work folder: create a new run.

The YAML keeps one step per phase with its own timeout, each `delta run --until <state>`; the
machine does not know the steps exist, and a re-run of a slice whose state is already reached does
nothing. Every value type is validated when it is read back from the document too (`Tree`, `Delta`,
`Manifest`, `Bases` deserialize through their constructors), and tool output in a failure reason is
redacted of the bucket name and the access key id before it reaches the document and the summary. Every msg, state and cmd is logged, the dropped bases with
their reasons, and `delta summary run.json` renders the job summary. The workflow keeps the
document as an artifact. Bucket names and tokens are read from the environment when a call runs
and never reach the document. Executors clean up what they consumed (extraction removes the zip,
replay removes the base build and its rebuild) and what failed; they never decide a transition.

Errors are values. A problem with one base drops it (`DropReason`); a problem with the run fails it
(`Failed { reason }`), and a failed run answers every further msg with `Complete`. Clippy denies
`unwrap`, `expect`, `panic`, indexing and unchecked arithmetic everywhere, tests included. The
crate is single-threaded: bases are processed one after another.

### Local run

`--local <folder>` uses a folder as the bucket (`<folder>/<bucket prefix>/<tag>/<zip>`), with
nothing published and every signature valid. In CI the uploader is installed from the lockfile in
`scripts/delta/uploader` and named by `CDN_UPLOADER`; a local run without it uses `npx` with the
pinned version, which only matters for runs against the real bucket:

```bash
cd scripts/delta && cargo build --release && DELTA=$PWD/target/release/delta
export PATH="$("$DELTA" fetch-tools /tmp/delta-tools):$PATH"
mkdir -p /tmp/bucket/releases/v0.185.0-alpha /tmp/bucket/releases/v0.186.0-alpha
cp v0.185.0-alpha/Decentraland_windows64.zip /tmp/bucket/releases/v0.185.0-alpha/
cp v0.186.0-alpha/Decentraland_windows64.zip /tmp/bucket/releases/v0.186.0-alpha/
"$DELTA" create-run /tmp/run.json --tag v0.186.0-alpha --platform windows64 --zip Decentraland_windows64.zip \
  --bucket-prefix releases --bases '["v0.185.0-alpha"]' --work /tmp/delta-work
"$DELTA" run /tmp/run.json --local /tmp/bucket
"$DELTA" summary /tmp/run.json
```

The manifest lands in `/tmp/bucket/releases/v0.186.0-alpha/delta/windows64/`.

### Tests

`cargo test` runs the unit tests next to each module (every rule of the simple types, `plan`, the
whole-file rule, the replay order, `differences`, archive member checks, base picking and pruning,
signature changes, `Manifest` rules, the summary), the lifecycle tests of `run::fsm` (every
transition from `Requested` to `Published` driven with hand-written msgs and no disk, asserting the
state and the cmd that come back as a pair: a msg out of order, an integrity failure, both recheck
refusals, a base dropped, a base listed nowhere, every base dropped, a failed run staying failed,
and the JSON round trip of the run and its cmd at every state), and `tests/run.rs`: synthetic
builds drained through the real `hdiffz`/`hpatchz` and a folder bucket, covering changed, added,
deleted, renamed and case-renamed files, the whole-file fallback, paths with spaces, modes, links
replacing folders and the reverse, a corrupt, a missing and an unsafe base zip (dropped while the
others ship), changed signatures, a base that only lost files (an empty patch set), a zip replaced
while the run was paused at `Prepared`, the macOS `build.tar` layout, a missing release zip failing
before the `--until` state, and the command line pausing at `--until` and resuming to completion.
The launcher's JSON contract is pinned by a literal fixture in `publishing/contract.rs`. Without
`HDiffPatch` the tests that need it are skipped locally and fail in CI.
[`ci-scripts-tests.yml`](../.github/workflows/ci-scripts-tests.yml) runs `cargo fmt --check`,
`cargo clippy -D warnings` and `cargo test` on Linux and macOS.

## Costs

- CI: per platform, about 1.5-2.5 minutes of `hdiffz` per base plus downloading each base zip from
  S3. With N = 4 bases (two weeks at two releases a week) that is roughly 15-20 minutes, on a macOS
  runner for the macOS leg, plus about a minute to build the tool (cached between runs).
- S3: two patch series of about N x 30 MB each per platform (~240 MB for N = 4). Zips are unchanged.

## Not yet measured

- macOS patch sizes, and patch sizes for bases several versions behind.
- Peak memory of `hdiffz -m-6` on the runners. If it runs out, switch large files to `-s` (stream
  mode) or use a bigger runner.
