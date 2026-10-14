"""Shared builders for domain values and build folders in tests."""

import hashlib
import os
import stat
import sys
import zipfile
from pathlib import Path

import pytest

from delta.domain import RegularFile, Symlink, Tree

POSIX_ONLY = pytest.mark.skipif(sys.platform == "win32", reason="symlinks and modes need a POSIX file system")


def regular(path: str, content: bytes = b"x", mode: str = "644") -> RegularFile:
    return RegularFile(path=path, size=len(content), sha256=hashlib.sha256(content).hexdigest(), mode=mode)


def link(path: str, target: str) -> Symlink:
    return Symlink(path=path, symlink=target)


def tree(*entries: RegularFile | Symlink) -> Tree:
    return Tree(tuple(sorted(entries, key=lambda e: e.path)))


def write(path: Path, content: bytes) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(content)
    return path


def zip_dir(source: Path, archive: Path) -> Path:
    """Like `zip -ry`: stores unix modes and symlinks as links."""
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as zipped:
        for path in sorted(source.rglob("*")):
            name = path.relative_to(source).as_posix()
            if path.is_symlink():
                info = zipfile.ZipInfo(name)
                info.create_system = 3
                info.external_attr = (stat.S_IFLNK | 0o777) << 16
                zipped.writestr(info, os.readlink(path))  # noqa: PTH115 (Path.readlink normalizes the target text)
            elif path.is_file():
                zipped.write(path, name)
    return archive
