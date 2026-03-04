# Build Automation & CI

The Unity project is built automatically on certain triggers by a combination of GitHub workflows, Python wrappers, and the [Unity Cloud Build](https://cloud.unity.com/) system.

## Workflows & Triggers

There are two main workflows that handle all major builds:
- `build-unitycloud`
- `build-release-weekly`

The main workflow is `build-unitycloud`, which will be automatically triggered by commits and PRs to `main`, or by a manual workflow dispatch call.
By default, PRs marked as draft will not trigger the build. If this is something you want, you need to add a label `force-build` to this PR.

The `build-release-weekly` workflow mostly wraps & handles `build-unitycloud` for release, and currently it's triggered manually with a workflow dispatch call.
The release build would create a new target based on the version, which means that this does not block `main` branch and merging to main can continue.

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
- `CACHE_STRATEGY`: Defines what strategy we want to use for caching (available options: `none`, `library`, `workspace`, `inherit`). This affects the speed of next build. By default it's set to `workspace`
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

Each build currently clones this template and renames it using the format `TARGET_NAME-BRANCH`.
If a build is already triggered for the same target and a new commit is applied, it will cancel the previous build automatically.

### Storage

All build artefacts are stored in the GitHub workflow run after the build is finished. This allows deleting old builds from the cloud to save space. The entire build must be deleted, as there's no way to delete just the artefacts.

### Cache

Cache usage and level can be controlled by the template config build or by the `CACHE_STRATEGY` param.
To update the cache or change settings of the template, the template itself must be run manually in the Unity Cloud Build UI.

### Rebuilding

If a build fails, the auto-generated config build is not removed from the cloud, which allows any re-runs from GitHub to use the exact same cache and settings as before.
If you need to run a clean build you can trigger the build with the `CLEAN_BUILD` param.

---

See also: [Troubleshooting Missing Docker Images](troubleshooting-missing-docker-images.md) | [Unity Upgrades](unity-upgrades.md)
