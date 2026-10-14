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
    |                         delta: [windows64 on ubuntu, macos on macos]
    |                                download zips -> make-delta -> verify -> signatures
    |                                -> upload patches -> upload manifest (last)
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
- Each run first removes `<tag>/delta/<platform>/` left by an earlier run of the same unpublished
  tag, whose zip the new build has just overwritten.
- `latest.json` is unchanged and stays the only switch that makes a version live.
- [`build-release-delta.yml`](../.github/workflows/build-release-delta.yml) also has a
  `workflow_dispatch` (`tag`, `base_count`) for regenerating a release that is **not published yet**.

## S3 layout

```
@dcl/unity-explorer/releases/
    latest.json                                    unchanged
    <tag>/Decentraland_<target>.zip                unchanged: fresh installs and fallback
    <tag>/delta/<platform>/manifest.json           uploaded last
    <tag>/delta/<platform>/from/<base tag>/<path>.hdiff
```

`<platform>` is `windows64` or `macos`. The manifest URL is derived by convention; a 404 means "no
delta, use the zip".

**Immutable once published.** Patches are uploaded with a one-year immutable `Cache-Control`. The
manifest is uploaded without it: a rebuild of a still-unpublished tag rewrites it under the same
key, and a CDN edge must not keep serving the old one. A stale cached patch is harmless, because
its `patch_sha256` no longer matches the new manifest. Nothing is rewritten after the release is
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
          "patch": "from/v0.186.0-alpha/GameAssembly.dll.hdiff",
          "patch_size": 8347352,
          "patch_sha256": "..."
        }
      ],
      "whole": [
        {
          "path": "Decentraland_Data/StreamingAssets/aa/catalog.hash",
          "patch": "from/v0.186.0-alpha/Decentraland_Data/StreamingAssets/aa/catalog.hash.hdiff",
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
  size and not smaller than the file encoded alone. They are HDiffPatch patches against an **empty**
  file, so `hpatch` is the only codec the launcher needs.
- `deleted`: base paths absent from the new build, including the old names of renamed bundles.
- Unchanged files appear only in `files`.

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
   files into place, apply every `files[]` `symlink` and `mode` (unchanged files included), verify
   `files[]` again, and keep the previous files until the new version launches (rollback). The
   order matters: on case-insensitive file systems a case-only
   rename (`Foo.dll` -> `foo.dll`) deleted after the swap removes the new file, and a directory
   replaced by a symlink (`Versions/Current` -> `A`) deleted after the swap removes files in the
   symlink's target. `run.py verify` replays this order.
6. Leave files that are not in the manifest alone (logs, launcher-owned files).
7. Reject the manifest (fall back to the zip and report it) if any `path`, `from_path`, `deleted`
   entry or `patch` is absolute, contains an empty, `.` or `..` segment, or contains `\`, `:` or a
   control character (NUL included), and if a `symlink` target is absolute, contains `\`, `:` or a
   control character, or resolves outside the install folder. On NTFS those characters reach
   outside the folder or into another file (`..\x.dll`, `C:x.dll`, `\\host\share`,
   `Decentraland.exe:ads`). CI generation (the `RelPath` and `Tree` types) already refuses such
   builds; the launcher checks again because the manifest is not signed.

Patching uses HDiffPatch's `hpatch` (MIT, C), ideally linked into the launcher so no extra executable
needs code signing.

## Scripts

All delta logic lives in [`scripts/delta/`](../scripts/delta/), a Python 3.12 tool run the same way
in CI and locally (`python3 scripts/delta/run.py <command>`, `--help` lists them). It is split by
what the code may touch:

| Module | Holds | Why |
|---|---|---|
| `delta/domain.py` | the domain values: `RelPath`, `ReleaseTag`, `Sha256`, the `RegularFile \| Symlink` entry union, `Tree`, `DiffPatch`, `WholeFile`, `BaseDelta`, `Manifest`, the application `Step` union, releases | strict, frozen pydantic models that reject unknown fields: a value that exists is valid, so unsafe paths, malformed tags and inconsistent manifests cannot be built |
| `delta/transform.py` | pure functions: planning a patch set, the whole-file rule, assembling the manifest, the launcher's application order, comparing trees, picking and pruning releases, signature comparison, the job summary | no I/O; arguments and results are validated against their types at every call |
| `delta/io/` | `fs` (hashing, scanning, extracting, replaying steps), `hdiff` (pinned download, hdiffz/hpatchz), `github` (`gh api`), `signatures` (osslsigncode, codesign) | the only code touching files, processes or the network |
| `delta/pipeline.py` | `make_delta`, `verify_delta`, `delta_for_base`, `build_manifest` | joins I/O and transformations into the steps the workflows run |
| `delta/cli.py` | the commands below | argument parsing and output only |

Each command takes explicit arguments, writes only to the given output folder or to stdout, logs
to stderr and exits non-zero on failure. Only `fetch-tools`, `check-unpublished`, `bases` and
`prune` touch the network.

| Command | Input | Output | Why |
|---|---|---|---|
| `fetch-tools` | `<tools dir>` | pinned HDiffPatch for this OS, sha256-checked; prints the bin folder | identical patches in CI and locally |
| `extract` | `<zip> <out dir>` | prints the build root | handles the macOS `build.tar` inside the zip, keeping modes and symlinks |
| `tree` | `<dir>` | `files[]` JSON on stdout | one definition of paths, hashes, modes and symlinks; refuses paths or symlinks that reach outside the build |
| `make-delta` | `<base dir> <new dir> <base tag> <out dir>` | `from/<base tag>/**.hdiff`, `base-<tag>.json` | the patches for one base, each round-trip checked |
| `manifest` | `<version> <platform> <new dir> <zip> <base-*.json>...` | `manifest.json` on stdout | one place for the manifest format |
| `signatures` | `<platform> <build root>` | `<path> ok\|invalid` per `.exe` (osslsigncode) or `.app` (codesign) | proves signatures survive patching |
| `delta-for-base` | `<version> <platform> <new root> <new zip> <base tag> <base zip> <out dir>` | `from/<base tag>/`, `base-<tag>.json`; removed again on any failure | the whole per-base pipeline (extract, make, verify, signatures) the workflow loops over |
| `verify` | `<base dir> <delta dir> <manifest> <base tag> <rebuild dir>` | rebuilt tree; exit 0 only on an exact match | replays the launcher before anything is published |
| `bases` | `<n> <tag>`, `gh` with `GH_TOKEN` | JSON array of tags | which releases to diff from |
| `prune` | `<keep> <window> [--live <tag>]`, `gh` with `GH_TOKEN` | tags, one per line | which `delta/` folders to remove |
| `check-unpublished` | `<tag>`, `gh` with `GH_TOKEN` | exit 0 only for a missing or draft release | the immutability guard; an API error is never read as "unpublished" |
| `summary` | `<manifest> [failed base]...` | markdown | the workflow's job summary |

Local run (macOS, Linux or Windows), from the repo root:

```bash
pip install -r scripts/delta/requirements.txt
export PATH="$(python3 scripts/delta/run.py fetch-tools /tmp/delta-tools):$PATH"
OLD=$(python3 scripts/delta/run.py extract v0.185.0-alpha/Decentraland_windows64.zip /tmp/old)
NEW=$(python3 scripts/delta/run.py extract v0.186.0-alpha/Decentraland_windows64.zip /tmp/new)
python3 scripts/delta/run.py make-delta "$OLD" "$NEW" v0.185.0-alpha /tmp/out
python3 scripts/delta/run.py manifest v0.186.0-alpha windows64 "$NEW" \
  v0.186.0-alpha/Decentraland_windows64.zip /tmp/out/base-v0.185.0-alpha.json > /tmp/out/manifest.json
python3 scripts/delta/run.py verify "$OLD" /tmp/out /tmp/out/manifest.json v0.185.0-alpha /tmp/rebuild
```

Checks, from `scripts/delta/` after `pip install -r requirements-dev.txt`: `ruff check .`,
`ruff format --check .`, `mypy .` (strict, with the pydantic plugin) and `python3 -m pytest`.
`tests/test_domain.py` and `tests/test_transform.py` cover the types and the pure functions,
`tests/test_releases.py` the release selection and the published-tag guard, and
`tests/test_pipeline.py` synthetic builds through the real HDiffPatch: changed, added, deleted and
renamed files, case-only renames, the whole-file fallback, paths with spaces, modes, symlinks
replacing directories and the reverse, symlinks leaving the build, tampered patches, modified
bases, the macOS `build.tar`-in-zip layout, and `delta_for_base` with a stubbed signature check
(success, signature mismatch, corrupt base zip). All run in
[`ci-scripts-tests.yml`](../.github/workflows/ci-scripts-tests.yml) on Linux and macOS, also when
the workflows that drive the tool change.

## Costs

- CI: per platform, about 1.5-2.5 minutes of `hdiffz` per base plus downloading each base zip from
  S3. With N = 4 bases (two weeks at two releases a week) that is roughly 15-20 minutes, on a macOS
  runner for the macOS leg.
- S3: two patch series of about N x 30 MB each per platform (~240 MB for N = 4). Zips are unchanged.

## Not yet measured

- macOS patch sizes, and patch sizes for bases several versions behind.
- Peak memory of `hdiffz -m-6` on the runners. If it runs out, switch large files to `-s` (stream
  mode) or use a bigger runner.
