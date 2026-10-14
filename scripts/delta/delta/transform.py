"""Pure transformations between domain values. No file system, process or network access,
and no exceptions: a transformation that can fail returns a `Result`."""

import re
from collections.abc import Callable
from typing import Any

from pydantic import ConfigDict, validate_call
from returns.converters import maybe_to_result
from returns.iterables import Fold
from returns.maybe import Maybe, Nothing, Some
from returns.result import Result, Success

from delta.domain import (
    STRICT,
    ApplyDiff,
    ApplyWhole,
    BaseDelta,
    BaseResult,
    DeletePath,
    DeltaPlan,
    DiffJob,
    DiffPatch,
    GitHubRelease,
    Job,
    MakeSymlink,
    Manifest,
    Mismatch,
    Platform,
    Problem,
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
    ZipFolder,
    ZipMember,
    ZipRef,
    ZipSymlink,
    fail,
    lexically_escapes,
    parse,
    parse_json,
    version_of,
)

# Result and Maybe containers are checked by type; their payloads are validated models.
_PURE = ConfigDict(**STRICT, arbitrary_types_allowed=True)


def pure[**P, R](function: Callable[P, R]) -> Callable[P, R]:
    """Validates arguments and the return value against the annotations, strictly. A
    mismatch is a programming error (like a panic), not a Result."""
    validated: Callable[P, R] = validate_call(config=_PURE, validate_return=True)(function)
    return validated


# --- release zips -----------------------------------------------------------------------


@pure
def zip_layout_problem(members: tuple[ZipMember, ...]) -> Maybe[str]:
    """Extraction writes inside the out folder only if no member lies under a file or symlink
    member (which would write through the link) and no symlink climbs out lexically."""
    leaves = {m.path for m in members if not isinstance(m, ZipFolder)}
    for member in members:
        parent = member.path.rpartition("/")[0]
        while parent:
            if parent in leaves:
                return Some(f"zip entry {member.path} lies under the file or symlink {parent}")
            parent = parent.rpartition("/")[0]
        if isinstance(member, ZipSymlink) and lexically_escapes(member.path, member.target):
            return Some(f"zip symlink leaves the build: {member.path} -> {member.target}")
    return Nothing


# --- planning a patch set ---------------------------------------------------------------

_HASHED_BUNDLE = re.compile(r"_[0-9a-f]{32}\.bundle$")


def _bundle_key(path: RelPath) -> Maybe[str]:
    """`dir/name` for `dir/name_<32 hex>.bundle`."""
    return Some(_HASHED_BUNDLE.sub("", path)) if _HASHED_BUNDLE.search(path) else Nothing


def _renamed_from(path: RelPath, renamed_away: dict[str, list[RelPath]]) -> Maybe[RelPath]:
    """The one base bundle of the same name that left the build."""
    return _bundle_key(path).bind(
        lambda key: Some(renamed_away[key][0]) if len(renamed_away.get(key, [])) == 1 else Nothing,
    )


def _job(
    file: RegularFile, base_files: dict[RelPath, RegularFile], renamed_away: dict[str, list[RelPath]]
) -> Maybe[Job]:
    match Maybe.from_optional(base_files.get(file.path)):
        case Some(old):
            return Nothing if old.sha256 == file.sha256 else Some(DiffJob(path=file.path, from_path=file.path))
        case _:
            return Some(
                _renamed_from(file.path, renamed_away)
                .map(lambda from_path: DiffJob(path=file.path, from_path=from_path))
                .value_or(WholeJob(path=file.path)),
            )


@pure
def plan_delta(base: Tree, new: Tree) -> DeltaPlan:
    """Changed files are patched from the same path; a bundle renamed by its content hash is
    patched from the one base bundle of the same name that left; everything else that is
    new or changed ships whole. `deleted` holds every base path absent from the new build."""
    base_files = {f.path: f for f in base.files()}
    new_entries = new.by_path()
    renamed_away: dict[str, list[RelPath]] = {}
    for path in base_files:
        if not isinstance(new_entries.get(path), RegularFile):
            for key in _bundle_key(path).map(lambda k: [k]).value_or([]):
                renamed_away.setdefault(key, []).append(path)

    jobs = tuple(
        job for file in new.files() for job in _job(file, base_files, renamed_away).map(lambda j: [j]).value_or([])
    )
    deleted = tuple(entry.path for entry in base.root if entry.path not in new_entries)
    return DeltaPlan(deleted=deleted, jobs=jobs)


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
def base_delta(plan: DeltaPlan, encoded: tuple[DiffPatch | WholeFile, ...]) -> Result[BaseDelta, Problem]:
    if [item.path for item in encoded] != [job.path for job in plan.jobs]:
        return fail("encoded patches do not match the plan")
    return parse(
        BaseDelta,
        {
            "deleted": plan.deleted,
            "patches": tuple(item for item in encoded if isinstance(item, DiffPatch)),
            "whole": tuple(item for item in encoded if isinstance(item, WholeFile)),
        },
    )


@pure
def assemble_manifest(
    version: ReleaseTag,
    platform: Platform,
    zip_ref: ZipRef,
    files: Tree,
    results: tuple[BaseResult, ...],
) -> Result[Manifest, Problem]:
    tags = [result.base for result in results]
    if len(tags) != len(set(tags)):
        return fail(f"duplicate base in {tags}")
    return parse(
        Manifest,
        {
            "version": version,
            "platform": platform,
            "zip": zip_ref,
            "files": files,
            "bases": {result.base: result.delta for result in results},
        },
    )


# --- applying a patch set ---------------------------------------------------------------


def _steps(manifest: Manifest, delta: BaseDelta) -> tuple[Step, ...]:
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


@pure
def application_steps(manifest: Manifest, base: ReleaseTag) -> Result[tuple[Step, ...], Problem]:
    """The launcher's order: deletes first, so a case-only rename or a directory replaced by
    a symlink cannot remove new files; then patches (read from the pristine base) and whole
    files; then every symlink and mode of `files`, as unchanged files can change them too."""
    return maybe_to_result(
        Maybe.from_optional(manifest.bases.get(base)).map(lambda delta: _steps(manifest, delta)),
        Problem(message=f"manifest has no base '{base}'"),
    )


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


def _published(release: GitHubRelease) -> Result[PublishedRelease, Problem]:
    return (
        maybe_to_result(Maybe.from_optional(release.published_at), Problem(message="published without a publish time"))
        .bind(lambda at: parse(PublishedRelease, {"tag": release.tag_name, "published_at": at}))
        .alt(lambda problem: Problem(message=f"malformed release {release.tag_name!r}: {problem.message}"))
    )


@pure
def published_explorer_releases(releases: tuple[GitHubRelease, ...]) -> Result[tuple[PublishedRelease, ...], Problem]:
    """Published `v*` releases (no drafts, no pre-releases, which /releases/latest never
    serves). Avatar Preview Renderer tags are ignored; a malformed `v*` tag is a Problem."""
    candidates = [r for r in releases if not r.draft and not r.prerelease and r.tag_name.startswith("v")]
    empty: Result[tuple[PublishedRelease, ...], Problem] = Success(())
    return Fold.collect([_published(r) for r in candidates], empty)


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
    live: Maybe[ReleaseTag],
) -> tuple[ReleaseTag, ...]:
    """Releases `keep` .. `keep + window - 1` by descending version, never `live`. Version
    order, so a late publish of an older tag cannot evict a newer release's series."""
    by_version = sorted(releases, key=lambda r: version_of(r.tag), reverse=True)
    return tuple(r.tag for r in by_version[keep : keep + window] if live != Some(r.tag))


@pure
def release_state(exit_code: int, stdout: str, stderr: str) -> Result[ReleaseState, Problem]:
    """Reads `gh api repos/<repo>/releases/tags/<tag>`, which serves published releases only:
    a draft is a 404 like a missing tag. Only a 404 means unpublished; any other failure is a
    Problem, never "unpublished"."""
    if exit_code != 0:
        if "HTTP 404" in stderr:
            return Success(ReleaseState.MISSING)
        return fail(f"release lookup failed: {stderr.strip()}")
    return parse_json(GitHubRelease, stdout).map(
        lambda release: ReleaseState.DRAFT if release.draft else ReleaseState.PUBLISHED,
    )


# --- signatures -------------------------------------------------------------------------


@pure
def signed_artifacts(platform: Platform, tree: Tree) -> tuple[RelPath, ...]:
    """windows64: every `*.exe` file. macos: every top-level `*.app` bundle."""
    if platform is Platform.WINDOWS64:
        return tuple(f.path for f in tree.files() if f.path.endswith(".exe"))
    apps = {entry.path.split("/", 1)[0] for entry in tree.root if "/" in entry.path}
    return tuple(sorted(app for app in apps if app.endswith(".app")))


def _verdict(valid: Maybe[bool]) -> str:
    return valid.map(lambda ok: "ok" if ok else "invalid").value_or("absent")


@pure
def signature_differences(
    new: tuple[SignatureCheck, ...],
    rebuilt: tuple[SignatureCheck, ...],
) -> tuple[str, ...]:
    wanted = {c.artifact: c.valid for c in new}
    found = {c.artifact: c.valid for c in rebuilt}
    return tuple(
        f"{artifact}: new build {_verdict(Maybe.from_optional(wanted.get(artifact)))}, "
        f"rebuilt {_verdict(Maybe.from_optional(found.get(artifact)))}"
        for artifact in sorted(wanted.keys() | found.keys())
        if wanted.get(artifact) != found.get(artifact)
    )


# --- reporting --------------------------------------------------------------------------


def _megabytes(size: int) -> str:
    return f"{size / 1048576:.1f} MB"


def _download_size(delta: BaseDelta) -> int:
    return sum(p.patch_size for p in delta.patches) + sum(w.patch_size for w in delta.whole)


@pure
def size_line(result: BaseResult, new_files: Tree) -> str:
    delta = result.delta
    build = sum(f.size for f in new_files.files())
    return (
        f"{result.base}: {len(delta.patches)} patched, {len(delta.whole)} whole, "
        f"{len(delta.deleted)} deleted; download {_download_size(delta)} bytes, new build {build} bytes"
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
        download = _download_size(delta)
        saving = round(100 - download * 100 / manifest.zip.size) if manifest.zip.size else 0
        lines.append(
            f"| {tag} | {len(delta.patches)} | {len(delta.whole)} | {len(delta.deleted)} "
            f"| {_megabytes(download)} | {_megabytes(manifest.zip.size)} | {saving}% |",
        )
    if failed:
        lines += ["", "Left out after a failure: " + " ".join(failed)]
    return "\n".join(lines) + "\n"
