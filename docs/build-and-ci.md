# Build Automation & CI

The Unity project is built automatically on certain triggers by a combination of GitHub workflows, Python wrappers, and the [Unity Cloud Build](https://cloud.unity.com/) system.

## Workflows & Triggers

There are two main workflows that handle all major builds:
- `build-unitycloud`
- `build-release-main`

The main workflow is `build-unitycloud`, which is automatically triggered by pushes to `dev`, by pull requests, by the merge queue, or by a manual workflow dispatch call.
By default, PRs marked as draft will not trigger the build. If this is something you want, you need to add a label `force-build` to this PR.

The `build-release-main` workflow wraps `build-unitycloud` for releases; it is triggered by pushes to `main` and by manual workflow dispatch.
Release, hotfix, and `main` builds share a single stable cache target per platform — the *release pool* — instead of a per-version target, so the cache is reused across releases without blocking `main`. See [Cache](#cache).

## GitHub Workflow

The workflow file (and any actions used by it) is mostly a wrapper of the Python handler which communicates with the [Unity DevOps Build API](https://build-api.cloud.unity3d.com/docs/1.0.0/index.html).

It takes care of triggering builds, handling parameters, getting dynamic information from the repository (like tags and secrets), and executing the Python handler script. It also waits for the cloud build to finish, uploading all logs and artefacts from the cloud build to the workflow run.

## Python Handler

Located in `scripts/cloudbuild`, the Python handler is mostly contained inside `build.py` with a `utils.py` extension for extra functionality.

All requirements are listed in `requirements.txt` and are automatically installed by the GitHub Workflow.

The script expects specific environment variables to be set and accepts some arguments.
These are all currently set by the workflow:
- `API_KEY`: Unity Cloud API Key (_secret_)
- `ORG_ID`: Unity Cloud Organization ID (_sensitive_)
- `PROJECT_ID`: Unity Cloud Project ID (_sensitive_)
- `POLL_TIME`: Time to wait in seconds before checking the API for any build status updates (while building)
- `TARGET`: Template build config to use for builds
- `BRANCH_NAME`: Name of the branch that triggered this build
- `CLEAN_BUILD`: Triggers a clean build that forces a reimport of library folder, and not using any caching
- `CACHE_STRATEGY`: Sets the target's `remoteCacheStrategy` — whether a target reuses **its own** cache between builds (options: `none`, `library`, `workspace`, `inherit`). The `build-unitycloud` workflow defaults it to `library`; release builds use `library`. See [Cache](#cache)
- `COMMIT_SHA`: The SHA value of the commit that this build is triggered on
- `BUILD_OPTIONS`: Any Unity BuildOptions to define for the build
- `PARAM_<NAME>`: Any ENV variables starting with `PARAM_` will be passed to Unity without the prefix to be used with `Editor.CloudBuild.Parameters[]`

Arguments include:
- `--resume`: If set, tries to find the build executed in the same runner and track it, instead of creating a new one
- `--cancel`: If set, tries to cancel a build executed in the same runner
- `--delete`: If set, tries to delete a build target on Unity Cloud side; executed by `pr-closure.yml` when a PR is closed

In generic terms, the Python handler goes through the following steps in order. If any of them fail, the entire script fails. Some steps may error out without failing immediately, but mark the build as unhealthy, which eventually fails:
1. Execute resume or cancel logic if requested
2. Check if the build config (target) exists on Unity Cloud
   * If new target: clone the given build config (target) for new build
   * If target exists: reuse the same build target (this optimises caching)
3. Set build parameters
4. Start the build
5. Poll the API for build status
6. Download artifacts
7. Download logs
8. Cleanup

The Python handler is built to run inside a fully configured GitHub Workflow. It could also run locally, but that is not an expected usage.

## Unity Cloud

The Unity Cloud Build (Unity Cloud DevOps) is the environment where builds execute. It is used directly by the Python handler and is not meant to be used manually.

### Concurrency

Only one build config can run at the same time. Therefore a "template" config build (named `@T_<TARGET_NAME>`) must be created for each target.

Each build clones this template and renames it based on the branch (see [Cache](#cache) for the full naming scheme). If a build is already running for the same target and a new commit is applied, the previous build is cancelled automatically.

Because `main`, `release/*`, and `hotfix/*` builds all share one *release pool* target per platform, builds on those branches serialize against each other — a new one cancels a pending one on the same target. This is fine for the sequential release/hotfix cadence, but two of them running at once on the same platform will contend.

### Storage

All build artefacts are stored in the GitHub workflow run after the build is finished. This allows deleting old builds from the cloud to save space. The entire build must be deleted, as there's no way to delete just the artefacts.

### Cache

Caching is what keeps build times down: the bulk of a cold build is shader-variant compilation (historically ~55 min of an ~82 min release build). Two **independent** Unity Cloud mechanisms control it — don't confuse them:

| Mechanism | Scope | What it does |
|---|---|---|
| `remoteCacheStrategy` (set via `CACHE_STRATEGY`) | one target, across its own builds | Whether a target keeps and reuses **its own** Library/shader cache between consecutive builds. Options: `none`, `library`, `workspace`, `inherit`. |
| `buildTargetCopyCache` | one-time copy **from another target** | The **only** way cache crosses targets: a one-shot copy of a source target's cache into a target when it is created/updated. Not a live mirror — after the copy the two targets diverge. |

**There is no implicit cross-target sharing.** Targets do not share cache because they have the same Unity version, the same platform, or belong to the same Unity Cloud **build-target group** (groups are organizational only — they carry no cache semantics). The single cross-target channel is the explicit `buildTargetCopyCache` copy in `build.py`.

#### Target naming *is* cache identity

Cache lives per target, so the target a build uses determines the cache it uses. `clone_current_target` derives the name from the branch:

| Branch | Target name | Cache source |
|---|---|---|
| `dev` | `{platform}-dev` (e.g. `windows64-dev`) | its own |
| feature / PR | `{platform}-{sanitized-branch}` | seeded once from `{platform}-dev` |
| `main`, `release/*`, `hotfix/*` | `{platform}-release[-{install_source}]` — the **shared release pool** | its own, shared across all release/hotfix/main builds |

`{install_source}` is appended only when it is not `launcher` (e.g. `-epic`), so each distinct artifact gets its own pool: `windows64-release`, `windows64-release-epic`, `macos-release`, …

#### The shared release pool

`main`, `release/*`, and `hotfix/*` builds all resolve to **one stable target per platform + install source**, so the Library + shader cache is **maintained and reused across releases** instead of compiled cold every time. Properties:

- **Keyed on the branch, not on `IS_RELEASE_BUILD`.** Release builds run with that flag unset, so the branch name is the reliable discriminator. dev and feature branches are deliberately *not* in the pool.
- **Cold genesis, isolated from dev.** The pool target is created with **no** `buildTargetCopyCache`, so it never copies dev's cache. Its first build is cold (and populates the cache); every later release reuses the pool's own cache. dev/feature targets keep their own caches and never reference the pool — isolation is bidirectional.
- **Protected from deletion.** `pr-closure.yml` runs `build.py --delete` on every closed PR; `delete_current_target` refuses to delete the pool targets so the cross-release cache is never wiped.

Turned on from `build-release-main.yml` via `cache_strategy: library` + `clean_build: false`.

#### Where `buildTargetCopyCache` is applied

`generate_body` strips it from every request body first; `clone_current_target` then re-adds it in only two cases — and only one of those copies from a *different* target:

| Situation | `buildTargetCopyCache` |
|---|---|
| New target, release pool (cold genesis) | *not set* — no copy |
| New target, dev / feature | `{platform}-dev` — **copy from dev** (the only cross-target copy) |
| Existing target (any branch) | the target **itself** — a self-reference ("reuse my own"), not a cross-target pull |

So the one line that copies *from another target* is the dev seed for a fresh dev/feature target; the release pool never reaches it.

#### Confirming cache behavior from CI

Two signals per build, no extra tooling:

- **Which pool a build used** — `build.py` logs `Updated name for target: <target>` in the *Execute Unity Cloud build* step. The same target name across branches means the same cache.
- **Whether cache was actually reused** — the *Generate Shader Compilation Report* step (and its uploaded artifact) prints `Remote Cache Hits: N` and `Total Variants Compiled: N`. Non-zero hits with ~0 variants compiled = warm; `0` hits with many variants = cold.

A new pool target whose **first** build shows `Remote Cache Hits: 0` and whose **next** build shows non-zero hits is simultaneously the proof of reuse and the proof of isolation (it can only reuse what it built itself).

To change the template defaults, run the template config (`@T_<TARGET_NAME>`) manually in the Unity Cloud Build UI.

### Rebuilding

If a build fails, the auto-generated config build is not removed from the cloud, which allows any re-runs from GitHub to use the exact same cache and settings as before.
If you need to run a clean build you can trigger the build with the `CLEAN_BUILD` param.

---

See also: [Troubleshooting Missing Docker Images](troubleshooting-missing-docker-images.md) | [Unity Upgrades](unity-upgrades.md)
