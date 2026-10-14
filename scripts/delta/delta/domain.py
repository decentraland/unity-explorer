"""Domain values of a delta update.

Every model is frozen, strict (no type coercion), rejects unknown fields and is revalidated
whenever it is passed into another model, so an instance that exists is a valid value. Values
built from outside data go through `parse`, which returns a `Result` instead of raising.
The JSON shapes are the wire format read by the launcher (docs/delta-updates.md).
"""

import re
from collections.abc import Callable, Mapping
from datetime import datetime
from enum import StrEnum
from typing import Annotated, Literal, NamedTuple, Self

from pydantic import (
    AfterValidator,
    BaseModel,
    ConfigDict,
    Discriminator,
    Field,
    RootModel,
    StringConstraints,
    TypeAdapter,
    ValidationError,
    model_validator,
)
from pydantic import Tag as UnionCase
from returns.maybe import Maybe, Nothing, Some
from returns.result import Failure, Result, safe

STRICT = ConfigDict(
    strict=True,
    frozen=True,
    extra="forbid",
    validate_default=True,
    revalidate_instances="always",
    allow_inf_nan=False,
    use_enum_values=False,
)


class Value(BaseModel):
    model_config = STRICT


class Problem(Value):
    """Why an operation could not produce its value."""

    message: str


def fail(message: str) -> Failure[Problem]:
    return Failure(Problem(message=message))


def because(context: str) -> Callable[[Exception], Problem]:
    """Maps an exception caught by `returns.result.safe` to a Problem."""
    return lambda error: Problem(message=f"{context}: {error}")


def parse[M: BaseModel](model: type[M], data: object) -> Result[M, Problem]:
    return safe((ValidationError,))(model.model_validate)(data).alt(because(f"invalid {model.__name__}"))


def parse_json[M: BaseModel](model: type[M], text: str) -> Result[M, Problem]:
    return safe((ValidationError,))(model.model_validate_json)(text).alt(because(f"invalid {model.__name__}"))


def parse_as[T](adapter: TypeAdapter[T], data: object) -> Result[T, Problem]:
    return safe((ValidationError,))(adapter.validate_python)(data).alt(because("invalid value"))


def _enforce[T](value: T, problem: Maybe[str]) -> T:
    """Pydantic's validator protocol: a validator rejects a value by raising ValueError. The
    checks below return their problem; this is the only place one becomes an exception."""
    match problem:
        case Some(reason):
            raise ValueError(reason)
        case _:
            return value


def _checked[T](check: Callable[[T], Maybe[str]]) -> AfterValidator:
    return AfterValidator(lambda value: _enforce(value, check(value)))


# --- scalar values --------------------------------------------------------------------

# Control characters, `\` (a Windows separator) and `:` (drive letters, NTFS streams).
_FORBIDDEN_CHARS = re.compile(r"[\x00-\x1f\x7f\\:]")


def _rel_path_problem(value: str) -> Maybe[str]:
    if _FORBIDDEN_CHARS.search(value):
        return Some(f"unsupported character in path: {value!r}")
    if any(segment in {"", ".", ".."} for segment in value.split("/")):
        return Some(f"unsafe path: {value!r}")
    return Nothing


def _symlink_target_problem(value: str) -> Maybe[str]:
    if _FORBIDDEN_CHARS.search(value):
        return Some(f"unsupported character in symlink target: {value!r}")
    if not value or value.startswith("/"):
        return Some(f"absolute or empty symlink target: {value!r}")
    return Nothing


def _release_tag_problem(value: str) -> Maybe[str]:
    return Some(f"not a release tag: {value!r}") if ".." in value else Nothing


RelPath = Annotated[str, _checked(_rel_path_problem)]
"""A path relative to a build root, `/`-separated, that cannot leave the root."""

SymlinkTarget = Annotated[str, _checked(_symlink_target_problem)]
"""A relative symlink target; whether it stays inside the build is a Tree invariant."""

ReleaseTag = Annotated[
    str,
    StringConstraints(pattern=r"^v[0-9]+\.[0-9]+\.[0-9]+(-[A-Za-z0-9.]+)?$"),
    _checked(_release_tag_problem),
]
Sha256 = Annotated[str, StringConstraints(pattern=r"^[0-9a-f]{64}$")]
FileMode = Annotated[str, StringConstraints(pattern=r"^[0-7]{3,4}$")]
ByteCount = Annotated[int, Field(ge=0)]
FileName = Annotated[str, StringConstraints(pattern=r"^[A-Za-z0-9_.-]+$")]


class Platform(StrEnum):
    WINDOWS64 = "windows64"
    MACOS = "macos"


class Version(NamedTuple):
    major: int
    minor: int
    patch: int


def version_of(tag: ReleaseTag) -> Version:
    major, minor, patch = tag[1:].split("-", 1)[0].split(".")
    return Version(int(major), int(minor), int(patch))


# --- build trees ----------------------------------------------------------------------


class RegularFile(Value):
    path: RelPath
    size: ByteCount
    sha256: Sha256
    mode: FileMode


class Symlink(Value):
    path: RelPath
    symlink: SymlinkTarget


def _entry_case(value: object) -> str:
    if isinstance(value, Mapping):
        return "symlink" if "symlink" in value else "file"
    return "symlink" if isinstance(value, Symlink) else "file"


Entry = Annotated[
    Annotated[RegularFile, UnionCase("file")] | Annotated[Symlink, UnionCase("symlink")],
    Discriminator(_entry_case),
]


def lexically_escapes(link_path: RelPath, target: SymlinkTarget) -> bool:
    """True when `target`, resolved from the link's folder without following links, climbs
    above the build root."""
    depth = link_path.count("/")
    for segment in target.split("/"):
        if segment == "..":
            depth -= 1
            if depth < 0:
                return True
        elif segment not in {"", "."}:
            depth += 1
    return False


def _tree_problem(entries: tuple[RegularFile | Symlink, ...]) -> Maybe[str]:
    paths = [entry.path for entry in entries]
    if paths != sorted(set(paths)):
        return Some("tree entries must be sorted by path and unique")
    listed = set(paths)
    for path in paths:
        parent = path.rpartition("/")[0]
        while parent:
            if parent in listed:
                return Some(f"{path} is inside {parent}, which is a file or symlink")
            parent = parent.rpartition("/")[0]
    for entry in entries:
        if isinstance(entry, Symlink) and lexically_escapes(entry.path, entry.symlink):
            return Some(f"symlink leaves the build: {entry.path} -> {entry.symlink}")
    return Nothing


class Tree(RootModel[tuple[Entry, ...]]):
    """Every file and symlink of a build, sorted by path. Directories are implied."""

    model_config = ConfigDict(strict=True, frozen=True, validate_default=True, revalidate_instances="always")

    @model_validator(mode="after")
    def _valid(self) -> Self:
        return _enforce(self, _tree_problem(self.root))

    def by_path(self) -> dict[RelPath, RegularFile | Symlink]:
        return {entry.path: entry for entry in self.root}

    def files(self) -> tuple[RegularFile, ...]:
        return tuple(entry for entry in self.root if isinstance(entry, RegularFile))


# --- release zips ---------------------------------------------------------------------


class ZipFolder(Value):
    kind: Literal["folder"] = "folder"
    path: RelPath


class ZipRegularFile(Value):
    kind: Literal["file"] = "file"
    path: RelPath
    mode: FileMode


class ZipSymlink(Value):
    kind: Literal["symlink"] = "symlink"
    path: RelPath
    target: SymlinkTarget


ZipMember = Annotated[ZipFolder | ZipRegularFile | ZipSymlink, Field(discriminator="kind")]


# --- patch sets and the manifest ------------------------------------------------------


class DiffPatch(Value):
    """`path` is rebuilt by applying `patch` to `from_path` of the base."""

    path: RelPath
    from_path: RelPath
    from_sha256: Sha256
    patch: RelPath
    patch_size: ByteCount
    patch_sha256: Sha256


class WholeFile(Value):
    """`path` is rebuilt by applying `patch` to an empty file."""

    path: RelPath
    patch: RelPath
    patch_size: ByteCount
    patch_sha256: Sha256


class BaseDelta(Value):
    deleted: tuple[RelPath, ...]
    patches: tuple[DiffPatch, ...]
    whole: tuple[WholeFile, ...]

    @model_validator(mode="after")
    def _valid(self) -> Self:
        targets = [p.path for p in self.patches] + [w.path for w in self.whole]
        unique = len(targets) == len(set(targets))
        return _enforce(self, Nothing if unique else Some("a path is rebuilt more than once"))


class DiffJob(Value):
    kind: Literal["diff"] = "diff"
    path: RelPath
    from_path: RelPath


class WholeJob(Value):
    kind: Literal["whole"] = "whole"
    path: RelPath


Job = Annotated[DiffJob | WholeJob, Field(discriminator="kind")]


class DeltaPlan(Value):
    """What a patch set must hold before any patch is computed."""

    deleted: tuple[RelPath, ...]
    jobs: tuple[Job, ...]


class BaseResult(Value):
    """The patch set from one base release, before it joins the manifest."""

    base: ReleaseTag
    delta: BaseDelta


class ZipRef(Value):
    path: FileName
    size: ByteCount
    sha256: Sha256


class Manifest(Value):
    version: ReleaseTag
    platform: Platform
    zip: ZipRef
    files: Tree
    bases: Mapping[ReleaseTag, BaseDelta]

    @model_validator(mode="after")
    def _valid(self) -> Self:
        return _enforce(self, _manifest_problem(self.version, self.files, self.bases))


def _manifest_problem(version: str, files: Tree, bases: Mapping[str, BaseDelta]) -> Maybe[str]:
    if version in bases:
        return Some(f"{version} cannot be its own base")
    entries = files.by_path()
    for tag, delta in bases.items():
        for path in delta.deleted:
            if path in entries:
                return Some(f"{tag}: {path} is deleted but listed in files")
        for target in [p.path for p in delta.patches] + [w.path for w in delta.whole]:
            if not isinstance(entries.get(target), RegularFile):
                return Some(f"{tag}: {target} is rebuilt but is not a regular file in files")
    return Nothing


# --- releases -------------------------------------------------------------------------


class GitHubRelease(BaseModel):
    """One item of `GET /repos/{repo}/releases`; the other API fields are ignored."""

    model_config = ConfigDict(strict=True, frozen=True, extra="ignore")

    tag_name: str
    draft: bool
    prerelease: bool
    published_at: datetime | None


class PublishedRelease(Value):
    tag: ReleaseTag
    published_at: datetime


class ReleaseState(StrEnum):
    MISSING = "missing"
    DRAFT = "draft"
    PUBLISHED = "published"


# --- signatures -----------------------------------------------------------------------


class SignatureCheck(Value):
    artifact: RelPath
    valid: bool


# --- application steps (the launcher contract) ----------------------------------------


class DeletePath(Value):
    op: Literal["delete"] = "delete"
    path: RelPath


class ApplyDiff(Value):
    op: Literal["diff"] = "diff"
    source: RelPath
    patch: RelPath
    patch_sha256: Sha256
    path: RelPath


class ApplyWhole(Value):
    op: Literal["whole"] = "whole"
    patch: RelPath
    patch_sha256: Sha256
    path: RelPath


class MakeSymlink(Value):
    op: Literal["symlink"] = "symlink"
    path: RelPath
    target: SymlinkTarget


class SetMode(Value):
    op: Literal["mode"] = "mode"
    path: RelPath
    mode: FileMode


Step = Annotated[DeletePath | ApplyDiff | ApplyWhole | MakeSymlink | SetMode, Field(discriminator="op")]


class Mismatch(Value):
    kind: Literal["missing", "unexpected", "different"]
    path: RelPath
