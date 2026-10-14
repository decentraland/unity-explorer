"""File system operations on builds: hashing, scanning, extracting and replaying steps.

Every operation returns a `Result`; `safe` turns the OSError of a raw call into a Problem."""

import hashlib
import os
import shutil
import stat
import sys
import tarfile
import tempfile
import zipfile
from collections.abc import Callable
from functools import partial
from pathlib import Path, PurePosixPath

from pydantic import TypeAdapter
from returns.iterables import Fold
from returns.maybe import Some
from returns.result import Result, Success, safe

from delta.domain import (
    ApplyDiff,
    ApplyWhole,
    DeletePath,
    MakeSymlink,
    Problem,
    RegularFile,
    SetMode,
    Sha256,
    Step,
    Symlink,
    Tree,
    ZipFolder,
    ZipMember,
    ZipRef,
    ZipRegularFile,
    ZipSymlink,
    because,
    fail,
    parse,
    parse_as,
)
from delta.transform import zip_layout_problem

IS_WINDOWS = sys.platform == "win32"
_CHUNK = 1 << 20
_UNIX = 3  # ZipInfo.create_system of archives made on unix

Patcher = Callable[[Path, Path, Path], Result[None, Problem]]
"""hpatchz: (old file, patch, out file)."""

_ZIP_MEMBER: TypeAdapter[ZipMember] = TypeAdapter(ZipMember)


# --- hashing and scanning ---------------------------------------------------------------


@safe((OSError,))
def _hash(path: Path) -> Sha256:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while chunk := stream.read(_CHUNK):
            digest.update(chunk)
    return digest.hexdigest()


def sha256_file(path: Path) -> Result[Sha256, Problem]:
    return _hash(path).alt(because(f"cannot hash {path}"))


@safe((OSError,))
def _size(path: Path) -> int:
    return path.stat().st_size


def zip_ref(path: Path) -> Result[ZipRef, Problem]:
    return Result.do(
        ZipRef(path=path.name, size=size, sha256=sha256)
        for size in _size(path).alt(because(f"cannot read {path}"))
        for sha256 in sha256_file(path)
    )


@safe((OSError,))
def _walk(root: Path) -> list[Path]:
    """Files and symlinks under `root`, never following symlinks."""
    found: list[Path] = []
    folders = [root]
    while folders:
        for entry in os.scandir(folders.pop()):
            path = Path(entry.path)
            if entry.is_symlink() or entry.is_file(follow_symlinks=False):
                found.append(path)
            elif entry.is_dir(follow_symlinks=False):
                folders.append(path)
    return found


@safe((OSError,))
def _read_link(path: Path) -> str:
    return os.readlink(path)  # noqa: PTH115 (Path.readlink normalizes the target text)


def _inside(root: Path, path: Path, link: Symlink) -> Result[Symlink, Problem]:
    """The lexical Tree check misses a climb through another symlink (d/x -> up/../.. with
    d/up -> ..), so the target's folder must exist and resolve inside the build."""
    target_dir = link.symlink if path.is_dir() else str(PurePosixPath(link.symlink).parent)
    folder = path.parent / target_dir
    if not folder.is_dir():
        return fail(f"symlink target folder does not exist: {link.path} -> {link.symlink}")
    if not folder.resolve().is_relative_to(root.resolve()):
        return fail(f"symlink leaves the build: {link.path} -> {link.symlink}")
    return Success(link)


def _symlink_entry(root: Path, path: Path, rel: str) -> Result[Symlink, Problem]:
    return (
        _read_link(path)
        .alt(because(f"cannot read the link {rel}"))
        .bind(lambda target: parse(Symlink, {"path": rel, "symlink": target}))
        .bind(lambda link: _inside(root, path, link))
    )


@safe((OSError,))
def _stat(path: Path) -> os.stat_result:
    return path.stat()


def _file_entry(path: Path, rel: str) -> Result[RegularFile, Problem]:
    return Result.do(
        file
        for info in _stat(path).alt(because(f"cannot read {rel}"))
        for sha256 in sha256_file(path)
        for file in parse(
            RegularFile,
            {"path": rel, "size": info.st_size, "sha256": sha256, "mode": f"{stat.S_IMODE(info.st_mode):03o}"},
        )
    )


def _entry(root: Path, path: Path) -> Result[RegularFile | Symlink, Problem]:
    rel = path.relative_to(root).as_posix()
    entry: Result[RegularFile | Symlink, Problem] = (
        _symlink_entry(root, path, rel) if path.is_symlink() else _file_entry(path, rel)
    )
    return entry


def scan_tree(root: Path) -> Result[Tree, Problem]:
    """Describes a build folder. Fails on any path or symlink that could leave `root`."""
    if not root.is_dir():
        return fail(f"not a directory: {root}")
    empty: Result[tuple[RegularFile | Symlink, ...], Problem] = Success(())
    return (
        _walk(root)
        .alt(because(f"cannot list {root}"))
        .bind(lambda paths: Fold.collect([_entry(root, p) for p in paths], empty))
        .bind(lambda entries: parse(Tree, tuple(sorted(entries, key=lambda e: e.path))))
    )


@safe((OSError,))
def _make_dir(path: Path) -> bool:
    """Creates `path`; True when it is empty."""
    path.mkdir(parents=True, exist_ok=True)
    return not any(path.iterdir())


def ensure_empty_dir(path: Path) -> Result[Path, Problem]:
    return (
        _make_dir(path)
        .alt(because(f"cannot create {path}"))
        .bind(lambda empty: Success(path.resolve()) if empty else fail(f"not empty: {path}"))
    )


# --- release zips -----------------------------------------------------------------------


def _member(info: zipfile.ZipInfo, read: Callable[[], bytes]) -> Result[ZipMember, Problem]:
    path = info.filename.rstrip("/")
    unix_mode = info.external_attr >> 16 if info.create_system == _UNIX else 0
    if info.is_dir():
        data: dict[str, object] = {"kind": "folder", "path": path}
    elif stat.S_ISLNK(unix_mode):
        data = {"kind": "symlink", "path": path, "target": read().decode(errors="replace")}
    else:
        data = {"kind": "file", "path": path, "mode": f"{(stat.S_IMODE(unix_mode) & 0o777) or 0o644:03o}"}
    return parse_as(_ZIP_MEMBER, data).alt(
        lambda problem: Problem(message=f"unsafe zip entry {info.filename!r}: {problem.message}"),
    )


@safe((zipfile.BadZipFile, OSError))
def _list_zip(archive: Path) -> list[Result[ZipMember, Problem]]:
    with zipfile.ZipFile(archive) as zipped:
        return [_member(info, partial(zipped.read, info)) for info in zipped.infolist()]


def _layout_ok(members: tuple[ZipMember, ...]) -> Result[tuple[ZipMember, ...], Problem]:
    match zip_layout_problem(members):
        case Some(problem):
            return fail(problem)
        case _:
            return Success(members)


def _checked_members(listed: list[Result[ZipMember, Problem]]) -> Result[tuple[ZipMember, ...], Problem]:
    empty: Result[tuple[ZipMember, ...], Problem] = Success(())
    return Fold.collect(listed, empty).bind(_layout_ok)


@safe((zipfile.BadZipFile, OSError))
def _unzip(archive: Path, out: Path, members: tuple[ZipMember, ...]) -> None:
    """Writes members checked by `zip_layout_problem`: none lies under a file or symlink
    member, so no write follows a link."""
    with zipfile.ZipFile(archive) as zipped:
        for member in members:
            target = out / member.path
            match member:
                case ZipFolder():
                    target.mkdir(parents=True, exist_ok=True)
                case ZipSymlink(target=link_target):
                    target.parent.mkdir(parents=True, exist_ok=True)
                    target.symlink_to(link_target)
                case ZipRegularFile(mode=mode):
                    target.parent.mkdir(parents=True, exist_ok=True)
                    with zipped.open(member.path) as source, target.open("wb") as sink:
                        shutil.copyfileobj(source, sink, _CHUNK)
                    if not IS_WINDOWS:
                        target.chmod(int(mode, 8))


@safe((tarfile.TarError, OSError))
def _untar_if_present(out: Path) -> None:
    """macOS zips hold only `build.tar`. The `data` filter refuses absolute paths, `..` and
    links leaving `out`."""
    tar_path = out / "build.tar"
    if [p.name for p in out.iterdir()] != ["build.tar"]:
        return
    with tarfile.open(tar_path) as tar:
        tar.extractall(out, filter="data")
    tar_path.unlink()


@safe((OSError,))
def _build_root(out: Path) -> Path:
    build = out / "build"
    return build if [p.name for p in out.iterdir()] == ["build"] and build.is_dir() else out


def extract_build(archive: Path, out_dir: Path) -> Result[Path, Problem]:
    """Extracts a release zip and returns the build root. Windows zips hold the build
    directly; macOS zips hold `build.tar` (keeping modes and symlinks) with a `build/`
    folder, which is the root. Every member is checked before anything is written."""
    if not archive.is_file():
        return fail(f"zip not found: {archive}")
    cannot_extract = because(f"cannot extract {archive}")
    return Result.do(
        root
        for out in ensure_empty_dir(out_dir)
        for members in _list_zip(archive).alt(cannot_extract).bind(_checked_members)
        for _ in _unzip(archive, out, members).alt(cannot_extract)
        for _ in _untar_if_present(out).alt(cannot_extract)
        for root in _build_root(out).alt(cannot_extract)
    )


# --- replaying steps --------------------------------------------------------------------


@safe((OSError,))
def _remove(path: Path) -> None:
    if path.is_symlink() or path.is_file():
        path.unlink()
    elif path.is_dir():
        shutil.rmtree(path)


@safe((OSError,))
def _remove_empty_parents(path: Path, root: Path) -> None:
    folder = path.parent
    while folder != root and folder.is_dir() and not any(folder.iterdir()):
        folder.rmdir()
        folder = folder.parent


@safe((OSError,))
def _place(produced: Path, target: Path) -> None:
    target.parent.mkdir(parents=True, exist_ok=True)
    if target.is_symlink() or target.is_file():
        target.unlink()
    elif target.is_dir():
        shutil.rmtree(target)
    shutil.move(produced, target)


@safe((OSError,))
def _link(target: Path, link_target: str) -> None:
    target.parent.mkdir(parents=True, exist_ok=True)
    if target.is_symlink() or target.is_file():
        target.unlink()
    elif target.is_dir():
        shutil.rmtree(target)
    target.symlink_to(link_target)


@safe((OSError,))
def _chmod(target: Path, mode: str) -> None:
    if not IS_WINDOWS:
        target.chmod(int(mode, 8))


def _check_sha256(path: Path, expected: Sha256) -> Result[None, Problem]:
    return sha256_file(path).bind(
        lambda actual: Success(None) if actual == expected else fail(f"patch sha256 mismatch: {path}"),
    )


def _apply_patch(
    old: Path,
    patch: Path,
    sha256: Sha256,
    target: Path,
    scratch: Path,
    patcher: Patcher,
) -> Result[None, Problem]:
    if not patch.is_file():
        return fail(f"missing patch: {patch}")
    out = scratch / "out"
    return (
        _check_sha256(patch, sha256)
        .bind(lambda _: patcher(old, patch, out))
        .bind(lambda _: _place(out, target).alt(because(f"cannot write {target}")))
    )


def _replay_step(
    step: Step, base: Path, patches: Path, rebuild: Path, scratch: Path, patcher: Patcher
) -> Result[None, Problem]:
    target = rebuild / step.path
    cannot_change = because(f"cannot update {target}")
    match step:
        case DeletePath():
            return _remove(target).bind(lambda _: _remove_empty_parents(target, rebuild)).alt(cannot_change)
        case ApplyDiff(source=source, patch=patch, patch_sha256=sha256):
            return _apply_patch(base / source, patches / patch, sha256, target, scratch, patcher)
        case ApplyWhole(patch=patch, patch_sha256=sha256):
            return _apply_patch(scratch / "empty", patches / patch, sha256, target, scratch, patcher)
        case MakeSymlink(target=link_target):
            return _link(target, link_target).alt(cannot_change)
        case SetMode(mode=mode):
            return _chmod(target, mode).alt(cannot_change)


def _next_step(
    step: Step,
    base: Path,
    patches: Path,
    rebuild: Path,
    scratch: Path,
    patcher: Patcher,
    _previous: None,
) -> Result[None, Problem]:
    return _replay_step(step, base, patches, rebuild, scratch, patcher)


@safe((OSError,))
def _prepare_rebuild(base: Path, rebuild: Path, scratch: Path) -> None:
    shutil.copytree(base, rebuild, symlinks=True, dirs_exist_ok=True)
    (scratch / "empty").touch()


def replay(
    steps: tuple[Step, ...],
    base: Path,
    patches: Path,
    rebuild: Path,
    scratch: Path,
    patcher: Patcher,
) -> Result[None, Problem]:
    """Copies `base` into the empty `rebuild` folder and applies `steps` to it, stopping at the
    first Problem. Patches read from the untouched `base`; `scratch` is an empty work folder."""
    replayed = _prepare_rebuild(base, rebuild, scratch).alt(because(f"cannot copy {base}"))
    for step in steps:
        replayed = replayed.bind(partial(_next_step, step, base, patches, rebuild, scratch, patcher))
    return replayed


@safe((OSError,))
def _write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8", newline="\n")


def write_text(path: Path, text: str) -> Result[None, Problem]:
    return _write(path, text).alt(because(f"cannot write {path}"))


def remove_paths(*paths: Path) -> Result[None, Problem]:
    empty: Result[tuple[None, ...], Problem] = Success(())
    return Fold.collect([_remove(p).alt(because(f"cannot remove {p}")) for p in paths], empty).map(lambda _: None)


@safe((OSError,))
def _temp_dir() -> Path:
    return Path(tempfile.mkdtemp(prefix="delta-"))


def scratch_dir() -> Result[Path, Problem]:
    """A new empty folder; the caller removes it with `remove_paths`."""
    return _temp_dir().alt(because("cannot create a scratch folder"))


def with_scratch[T](action: Callable[[Path], Result[T, Problem]]) -> Result[T, Problem]:
    """Runs `action` with a new empty folder, removed afterwards whatever the outcome."""
    return scratch_dir().bind(lambda scratch: _keep(action(scratch), remove_paths(scratch)))


def _keep[T](result: Result[T, Problem], cleanup: Result[None, Problem]) -> Result[T, Problem]:
    """`result`, or the cleanup's Problem when only the cleanup failed."""
    return result.bind(lambda value: cleanup.map(lambda _: value))


def file_size(path: Path) -> Result[int, Problem]:
    return _size(path).alt(because(f"cannot read {path}"))


@safe((OSError,))
def _replace(source: Path, target: Path) -> None:
    target.parent.mkdir(parents=True, exist_ok=True)
    source.replace(target)


def move(source: Path, target: Path) -> Result[None, Problem]:
    return _replace(source, target).alt(because(f"cannot move {source} to {target}"))


@safe((OSError,))
def _make_parent(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)


def make_parent(path: Path) -> Result[None, Problem]:
    return _make_parent(path).alt(because(f"cannot create the folder of {path}"))


@safe((OSError, UnicodeDecodeError))
def _read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def read_text(path: Path) -> Result[str, Problem]:
    return _read(path).alt(because(f"cannot read {path}"))
