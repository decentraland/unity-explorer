"""Pure transformations between domain values. No file system, process or network access."""

import re
from collections.abc import Callable
from typing import Any

from pydantic import ValidationError, validate_call

from delta.domain import (
    STRICT,
    ApplyDiff,
    ApplyWhole,
    BaseDelta,
    BaseResult,
    DeletePath,
    DeltaError,
    DeltaPlan,
    DiffJob,
    DiffPatch,
    GitHubRelease,
    Job,
    MakeSymlink,
    Manifest,
    Mismatch,
    Platform,
    PublishedRelease,
    RegularFile,
    ReleaseState,
    ReleaseTag,
    RelPath,
    SetMode,
    SignatureCheck,
    Step,
    Symlink,
    Tree,
    WholeFile,
    WholeJob,
    ZipRef,
    version_of,
)


def pure[**P, R](function: Callable[P, R]) -> Callable[P, R]:
    """Validates arguments and the return value against the annotations, strictly."""
    validated: Callable[P, R] = validate_call(config=STRICT, validate_return=True)(function)
    return validated


# --- planning a patch set ---------------------------------------------------------------

_HASHED_BUNDLE = re.compile(r"_[0-9a-f]{32}\.bundle$")


def _bundle_key(path: RelPath) -> str | None:
    """`dir/name` for `dir/name_<32 hex>.bundle`, else None."""
    return _HASHED_BUNDLE.sub("", path) if _HASHED_BUNDLE.search(path) else None


@pure
def plan_delta(base: Tree, new: Tree) -> DeltaPlan:
    """Changed files are patched from the same path; a bundle renamed by its content hash is
    patched from the one base bundle of the same name that left; everything else that is
    new or changed ships whole. `deleted` holds every base path absent from the new build."""
    base_files = {f.path: f for f in base.files()}
    new_entries = new.by_path()
    renamed_away: dict[str, list[RelPath]] = {}
    for path in base_files:
        key = _bundle_key(path)
        if key is not None and not isinstance(new_entries.get(path), RegularFile):
            renamed_away.setdefault(key, []).append(path)

    jobs: list[Job] = []
    for file in new.files():
        old = base_files.get(file.path)
        if old is not None:
            if old.sha256 != file.sha256:
                jobs.append(DiffJob(path=file.path, from_path=file.path))
            continue
        key = _bundle_key(file.path)
        candidates = renamed_away.get(key, []) if key is not None else []
        if len(candidates) == 1:
            jobs.append(DiffJob(path=file.path, from_path=candidates[0]))
        else:
            jobs.append(WholeJob(path=file.path))

    deleted = tuple(entry.path for entry in base.root if entry.path not in new_entries)
    return DeltaPlan(deleted=deleted, jobs=tuple(jobs))


@pure
def needs_whole_trial(file_size: int, diff_size: int) -> bool:
    """A patch at least half the file's size may lose to encoding the file alone."""
    return diff_size * 2 >= file_size


@pure
def prefer_whole(diff_size: int, whole_size: int) -> bool:
    return whole_size < diff_size


@pure
def patch_path(base: ReleaseTag, path: RelPath) -> RelPath:
    return f"from/{base}/{path}.hdiff"


@pure
def base_delta(plan: DeltaPlan, encoded: tuple[DiffPatch | WholeFile, ...]) -> BaseDelta:
    planned = [job.path for job in plan.jobs]
    if [item.path for item in encoded] != planned:
        msg = "encoded patches do not match the plan"
        raise DeltaError(msg)
    return BaseDelta(
        deleted=plan.deleted,
        patches=tuple(item for item in encoded if isinstance(item, DiffPatch)),
        whole=tuple(item for item in encoded if isinstance(item, WholeFile)),
    )


@pure
def assemble_manifest(
    version: ReleaseTag,
    platform: Platform,
    zip_ref: ZipRef,
    files: Tree,
    results: tuple[BaseResult, ...],
) -> Manifest:
    tags = [result.base for result in results]
    if len(tags) != len(set(tags)):
        msg = f"duplicate base in {tags}"
        raise DeltaError(msg)
    return Manifest(
        version=version,
        platform=platform,
        zip=zip_ref,
        files=files,
        bases={result.base: result.delta for result in results},
    )


# --- applying a patch set ---------------------------------------------------------------


@pure
def application_steps(manifest: Manifest, base: ReleaseTag) -> tuple[Step, ...]:
    """The launcher's order: deletes first, so a case-only rename or a directory replaced by
    a symlink cannot remove new files; then patches (read from the pristine base) and whole
    files; then every symlink and mode of `files`, as unchanged files can change them too."""
    delta = manifest.bases.get(base)
    if delta is None:
        msg = f"manifest has no base '{base}'"
        raise DeltaError(msg)
    steps: list[Step] = [DeletePath(path=path) for path in delta.deleted]
    steps += [
        ApplyDiff(source=p.from_path, patch=p.patch, patch_sha256=p.patch_sha256, path=p.path) for p in delta.patches
    ]
    steps += [ApplyWhole(patch=w.patch, patch_sha256=w.patch_sha256, path=w.path) for w in delta.whole]
    for entry in manifest.files.root:
        if isinstance(entry, Symlink):
            steps.append(MakeSymlink(path=entry.path, target=entry.symlink))
        else:
            steps.append(SetMode(path=entry.path, mode=entry.mode))
    return tuple(steps)


def _comparable(entry: RegularFile | Symlink, *, compare_modes: bool) -> dict[str, Any]:
    fields = entry.model_dump()
    if not compare_modes:
        fields.pop("mode", None)
    return fields


@pure
def compare_trees(want: Tree, got: Tree, *, compare_modes: bool) -> tuple[Mismatch, ...]:
    wanted, found = want.by_path(), got.by_path()
    missing = [Mismatch(kind="missing", path=p) for p in wanted if p not in found]
    unexpected = [Mismatch(kind="unexpected", path=p) for p in found if p not in wanted]
    different = [
        Mismatch(kind="different", path=p)
        for p, entry in wanted.items()
        if p in found
        and _comparable(entry, compare_modes=compare_modes) != _comparable(found[p], compare_modes=compare_modes)
    ]
    return tuple(missing + unexpected + different)


# --- releases ---------------------------------------------------------------------------


@pure
def published_explorer_releases(releases: tuple[GitHubRelease, ...]) -> tuple[PublishedRelease, ...]:
    """Published `v*` releases (no drafts, no pre-releases, which /releases/latest never
    serves). Avatar Preview Renderer tags are ignored; a malformed `v*` tag is an error."""
    published: list[PublishedRelease] = []
    for release in releases:
        if release.draft or release.prerelease or not release.tag_name.startswith("v"):
            continue
        if release.published_at is None:
            msg = f"{release.tag_name} is published without a publish time"
            raise DeltaError(msg)
        try:
            published.append(PublishedRelease(tag=release.tag_name, published_at=release.published_at))
        except ValidationError as error:
            msg = f"malformed release tag {release.tag_name!r}"
            raise DeltaError(msg) from error
    return tuple(published)


@pure
def pick_bases(releases: tuple[PublishedRelease, ...], count: int, current: ReleaseTag) -> tuple[ReleaseTag, ...]:
    """The `count` most recently published releases other than `current`."""
    newest_first = sorted(releases, key=lambda r: r.published_at, reverse=True)
    return tuple(r.tag for r in newest_first if r.tag != current)[:count]


@pure
def prune_targets(
    releases: tuple[PublishedRelease, ...],
    keep: int,
    window: int,
    live: ReleaseTag | None,
) -> tuple[ReleaseTag, ...]:
    """Releases `keep` .. `keep + window - 1` by descending version, never `live`. Version
    order, so a late publish of an older tag cannot evict a newer release's series."""
    by_version = sorted(releases, key=lambda r: version_of(r.tag), reverse=True)
    return tuple(r.tag for r in by_version[keep : keep + window] if r.tag != live)


@pure
def release_state(exit_code: int, stdout: str, stderr: str) -> ReleaseState:
    """Reads `gh api repos/<repo>/releases/tags/<tag>`, which serves published releases and
    drafts. Only a 404 means missing; any other failure is an error, never "unpublished"."""
    if exit_code != 0:
        if "HTTP 404" in stderr:
            return ReleaseState.MISSING
        msg = f"release lookup failed: {stderr.strip()}"
        raise DeltaError(msg)
    release = GitHubRelease.model_validate_json(stdout)
    return ReleaseState.DRAFT if release.draft else ReleaseState.PUBLISHED


# --- signatures -------------------------------------------------------------------------


@pure
def signed_artifacts(platform: Platform, tree: Tree) -> tuple[RelPath, ...]:
    """windows64: every `*.exe` file. macos: every top-level `*.app` bundle."""
    if platform is Platform.WINDOWS64:
        return tuple(f.path for f in tree.files() if f.path.endswith(".exe"))
    apps = {entry.path.split("/", 1)[0] for entry in tree.root if "/" in entry.path}
    return tuple(sorted(app for app in apps if app.endswith(".app")))


@pure
def signature_differences(
    new: tuple[SignatureCheck, ...],
    rebuilt: tuple[SignatureCheck, ...],
) -> tuple[str, ...]:
    wanted = {c.artifact: c.valid for c in new}
    found = {c.artifact: c.valid for c in rebuilt}
    return tuple(
        f"{artifact}: new build {_verdict(valid=wanted.get(artifact))}, rebuilt {_verdict(valid=found.get(artifact))}"
        for artifact in sorted(wanted.keys() | found.keys())
        if wanted.get(artifact) != found.get(artifact)
    )


def _verdict(*, valid: bool | None) -> str:
    return "absent" if valid is None else "ok" if valid else "invalid"


# --- reporting --------------------------------------------------------------------------


def _megabytes(size: int) -> str:
    return f"{size / 1048576:.1f} MB"


@pure
def size_line(result: BaseResult, new_files: Tree) -> str:
    delta = result.delta
    download = sum(p.patch_size for p in delta.patches) + sum(w.patch_size for w in delta.whole)
    build = sum(f.size for f in new_files.files())
    return (
        f"{result.base}: {len(delta.patches)} patched, {len(delta.whole)} whole, "
        f"{len(delta.deleted)} deleted; download {download} bytes, new build {build} bytes"
    )


@pure
def job_summary(manifest: Manifest, failed: tuple[ReleaseTag, ...]) -> str:
    lines = [
        f"### Delta patches: {manifest.platform.value} -> {manifest.version}",
        "",
        "| From | Patched | Whole | Deleted | Download | Full zip | Saving |",
        "|---|---|---|---|---|---|---|",
    ]
    for tag, delta in manifest.bases.items():
        download = sum(p.patch_size for p in delta.patches) + sum(w.patch_size for w in delta.whole)
        saving = round(100 - download * 100 / manifest.zip.size) if manifest.zip.size else 0
        lines.append(
            f"| {tag} | {len(delta.patches)} | {len(delta.whole)} | {len(delta.deleted)} "
            f"| {_megabytes(download)} | {_megabytes(manifest.zip.size)} | {saving}% |"
        )
    if failed:
        lines += ["", "Left out after a failure: " + " ".join(failed)]
    return "\n".join(lines) + "\n"
