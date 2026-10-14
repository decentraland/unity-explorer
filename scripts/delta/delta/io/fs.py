"""File system operations on builds: hashing, scanning, extracting and replaying steps."""

import hashlib
import os
import shutil
import stat
import sys
import tarfile
import tempfile
import zipfile
from collections.abc import Callable, Iterator
from pathlib import Path, PurePosixPath

from pydantic import TypeAdapter, ValidationError

from delta.domain import (
    ApplyDiff,
    ApplyWhole,
    DeletePath,
    DeltaError,
    MakeSymlink,
    RegularFile,
    RelPath,
    SetMode,
    Sha256,
    Step,
    Symlink,
    Tree,
    ZipRef,
)

IS_WINDOWS = sys.platform == "win32"
_CHUNK = 1 << 20

_REL_PATH: TypeAdapter[str] = TypeAdapter(RelPath)

Patcher = Callable[[Path, Path, Path], None]
"""hpatchz: (old file, patch, out file)."""


def sha256_file(path: Path) -> Sha256:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while chunk := stream.read(_CHUNK):
            digest.update(chunk)
    return digest.hexdigest()


def zip_ref(path: Path) -> ZipRef:
    return ZipRef(path=path.name, size=path.stat().st_size, sha256=sha256_file(path))


def _walk(root: Path) -> Iterator[Path]:
    """Files and symlinks under `root`, never following symlinks."""
    for entry in os.scandir(root):
        path = Path(entry.path)
        if entry.is_symlink() or entry.is_file(follow_symlinks=False):
            yield path
        elif entry.is_dir(follow_symlinks=False):
            yield from _walk(path)


def _physical_target_dir(link: Path, target: str) -> Path:
    """The real folder a link's target resolves into. The lexical Tree check misses a climb
    through another symlink (d/x -> up/../.. with d/up -> ..), so the folder must exist."""
    target_dir = target if link.is_dir() else str(PurePosixPath(target).parent)
    folder = link.parent / target_dir
    if not folder.is_dir():
        msg = f"symlink target folder does not exist: {link} -> {target}"
        raise DeltaError(msg)
    return folder.resolve()


def scan_tree(root: Path) -> Tree:
    """Describes a build folder. Fails on any path or symlink that could leave `root`."""
    if not root.is_dir():
        msg = f"not a directory: {root}"
        raise DeltaError(msg)
    real_root = root.resolve()
    entries: list[RegularFile | Symlink] = []
    try:
        for path in _walk(root):
            rel = path.relative_to(root).as_posix()
            if path.is_symlink():
                target = os.readlink(path)  # noqa: PTH115 (Path.readlink normalizes the target text)
                link = Symlink(path=rel, symlink=target)
                if not _physical_target_dir(path, target).is_relative_to(real_root):
                    msg = f"symlink leaves the build: {rel} -> {target}"
                    raise DeltaError(msg)
                entries.append(link)
            else:
                info = path.stat()
                entries.append(
                    RegularFile(
                        path=rel,
                        size=info.st_size,
                        sha256=sha256_file(path),
                        mode=f"{stat.S_IMODE(info.st_mode):03o}",
                    ),
                )
        return Tree(tuple(sorted(entries, key=lambda e: e.path)))
    except ValidationError as error:
        raise DeltaError(str(error)) from error


def ensure_empty_dir(path: Path) -> Path:
    path.mkdir(parents=True, exist_ok=True)
    if any(path.iterdir()):
        msg = f"not empty: {path}"
        raise DeltaError(msg)
    return path.resolve()


# --- release zips -----------------------------------------------------------------------


def _extract_zip(archive: Path, out: Path) -> None:
    """Like `unzip`: restores unix permission bits and symlinks recorded in the zip."""
    with zipfile.ZipFile(archive) as zipped:
        for info in zipped.infolist():
            name = info.filename.rstrip("/")
            try:
                _REL_PATH.validate_python(name)
            except ValidationError as error:
                msg = f"unsafe zip entry: {info.filename!r}"
                raise DeltaError(msg) from error
            target = out / name
            unix_mode = info.external_attr >> 16 if info.create_system == 3 else 0  # noqa: PLR2004 (3 = unix)
            if info.is_dir():
                target.mkdir(parents=True, exist_ok=True)
                continue
            target.parent.mkdir(parents=True, exist_ok=True)
            if stat.S_ISLNK(unix_mode):
                target.symlink_to(zipped.read(info).decode())
                continue
            with zipped.open(info) as source, target.open("wb") as sink:
                shutil.copyfileobj(source, sink, _CHUNK)
            if unix_mode and not IS_WINDOWS:
                target.chmod(stat.S_IMODE(unix_mode) & 0o777)


def extract_build(archive: Path, out: Path) -> Path:
    """Extracts a release zip and returns the build root. Windows zips hold the build
    directly; macOS zips hold `build.tar` (keeping modes and symlinks) with a `build/`
    folder, which is the root."""
    if not archive.is_file():
        msg = f"zip not found: {archive}"
        raise DeltaError(msg)
    out = ensure_empty_dir(out)
    try:
        _extract_zip(archive, out)
        if [p.name for p in out.iterdir()] == ["build.tar"]:
            with tarfile.open(out / "build.tar") as tar:
                tar.extractall(out, filter="data")
            (out / "build.tar").unlink()
    except (zipfile.BadZipFile, tarfile.TarError, OSError) as error:
        msg = f"cannot extract {archive}: {error}"
        raise DeltaError(msg) from error
    build = out / "build"
    return build if [p.name for p in out.iterdir()] == ["build"] and build.is_dir() else out


# --- replaying steps --------------------------------------------------------------------


def _remove(path: Path) -> None:
    if path.is_symlink() or path.is_file():
        path.unlink()
    elif path.is_dir():
        shutil.rmtree(path)


def _remove_empty_parents(path: Path, root: Path) -> None:
    folder = path.parent
    while folder != root and folder.is_dir() and not any(folder.iterdir()):
        folder.rmdir()
        folder = folder.parent


def _apply_patch(old: Path, patch: Path, sha256: Sha256, target: Path, patcher: Patcher) -> None:
    if not patch.is_file():
        msg = f"missing patch: {patch}"
        raise DeltaError(msg)
    if sha256_file(patch) != sha256:
        msg = f"patch sha256 mismatch: {patch}"
        raise DeltaError(msg)
    with tempfile.TemporaryDirectory() as scratch:
        out = Path(scratch) / "out"
        patcher(old, patch, out)
        target.parent.mkdir(parents=True, exist_ok=True)
        _remove(target)
        shutil.move(out, target)


def replay(steps: tuple[Step, ...], base: Path, patches: Path, rebuild: Path, patcher: Patcher) -> None:
    """Copies `base` into the empty `rebuild` folder and applies `steps` to it. Patches read
    from the untouched `base`."""
    shutil.copytree(base, rebuild, symlinks=True, dirs_exist_ok=True)
    with tempfile.TemporaryDirectory() as scratch:
        empty = Path(scratch) / "empty"
        empty.touch()
        for step in steps:
            target = rebuild / step.path
            match step:
                case DeletePath():
                    _remove(target)
                    _remove_empty_parents(target, rebuild)
                case ApplyDiff(source=source, patch=patch, patch_sha256=sha256):
                    _apply_patch(base / source, patches / patch, sha256, target, patcher)
                case ApplyWhole(patch=patch, patch_sha256=sha256):
                    _apply_patch(empty, patches / patch, sha256, target, patcher)
                case MakeSymlink(target=link_target):
                    target.parent.mkdir(parents=True, exist_ok=True)
                    _remove(target)
                    target.symlink_to(link_target)
                case SetMode(mode=mode):
                    if not IS_WINDOWS:
                        target.chmod(int(mode, 8))


def write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8", newline="\n")


def remove_paths(*paths: Path) -> None:
    for path in paths:
        _remove(path)
