"""HDiffPatch: the pinned download and the hdiffz/hpatchz processes."""

import os
import platform
import shutil
import subprocess
import time
import urllib.request
import zipfile
from dataclasses import dataclass
from functools import partial
from pathlib import Path

from returns.converters import maybe_to_result
from returns.maybe import Maybe
from returns.result import Result, Success, safe

from delta.domain import Problem, because, fail
from delta.io.fs import remove_paths, sha256_file, with_scratch

VERSION = "v5.1.3"
# (system, machine or "*") -> (release asset, sha256 of its zip)
PINNED = {
    ("Linux", "x86_64"): ("linux64", "628963bf2ee9108a97260fa5eef44acd9ec94369b76090a957c9182b3abbb558"),
    ("Darwin", "*"): ("macos", "9e9d7318db1ea5607dbdde41e6614cfe5b73c589034b38ee91a79f4ca80a940c"),
    ("Windows", "*"): ("windows64", "77f141386e5d8f785c1c846e10fbbc19b6c05aa00e3f59cc44670fb3f0e2ae94"),
}
# Patch size over speed; identical across runs.
HDIFFZ_ARGS = ("-m-6", "-c-lzma2-9-16m")
_DOWNLOAD_ATTEMPTS = 3


def _pinned() -> Result[tuple[str, str], Problem]:
    system, machine = platform.system(), platform.machine()
    return maybe_to_result(
        Maybe.from_optional(PINNED.get((system, machine)) or PINNED.get((system, "*"))),
        Problem(message=f"no pinned HDiffPatch build for {system}/{machine}"),
    )


@safe((OSError,))
def _download_once(url: str, out: Path) -> None:
    with urllib.request.urlopen(url, timeout=60) as response, out.open("wb") as sink:  # noqa: S310 (fixed https URL)
        shutil.copyfileobj(response, sink)


def _retry(url: str, out: Path, attempt: int, _previous: Exception) -> Result[None, Exception]:
    time.sleep(2 * attempt)
    return _download_once(url, out)


def _download(url: str, out: Path) -> Result[None, Problem]:
    """Up to _DOWNLOAD_ATTEMPTS tries; `lash` runs the next one only after a Failure."""
    result: Result[None, Exception] = _download_once(url, out)
    for attempt in range(2, _DOWNLOAD_ATTEMPTS + 1):
        result = result.lash(partial(_retry, url, out, attempt))
    return result.alt(because(f"cannot download {url}"))


def _ensure_archive(archive: Path, sha256: str) -> Result[None, Problem]:
    cached = archive.is_file() and sha256_file(archive) == Success(sha256)
    downloaded = (
        Success(None)
        if cached
        else _download(f"https://github.com/sisong/HDiffPatch/releases/download/{VERSION}/{archive.name}", archive)
    )
    return downloaded.bind(lambda _: sha256_file(archive)).bind(
        lambda actual: Success(None)
        if actual == sha256
        else remove_paths(archive).bind(lambda _: fail(f"HDiffPatch sha256 mismatch: got {actual}, want {sha256}")),
    )


@safe((zipfile.BadZipFile, OSError))
def _unpack(archive: Path, tools_dir: Path, bin_dir: Path) -> bool:
    """Unpacks the archive; True when it held the expected bin folder."""
    shutil.rmtree(bin_dir, ignore_errors=True)
    with zipfile.ZipFile(archive) as zipped:
        zipped.extractall(tools_dir)
    if not bin_dir.is_dir():
        return False
    for tool in bin_dir.iterdir():
        if tool.name.startswith(("hdiffz", "hpatchz")):
            tool.chmod(0o755)
    return True


@safe((OSError,))
def _make_dir(path: Path) -> None:
    path.mkdir(parents=True, exist_ok=True)


def _install(tools_dir: Path, asset: str, sha256: str) -> Result[Path, Problem]:
    archive = tools_dir / f"hdiffpatch_{VERSION}_bin_{asset}.zip"
    bin_dir = tools_dir / asset
    return (
        _make_dir(tools_dir)
        .alt(because(f"cannot create {tools_dir}"))
        .bind(lambda _: _ensure_archive(archive, sha256))
        .bind(lambda _: _unpack(archive, tools_dir, bin_dir).alt(because(f"cannot unpack {archive}")))
        .bind(
            lambda unpacked: Success(bin_dir.resolve())
            if unpacked
            else fail(f"unexpected HDiffPatch archive layout: no {asset}/ folder"),
        )
    )


def fetch_tools(tools_dir: Path) -> Result[Path, Problem]:
    """Installs the pinned HDiffPatch build for this OS into `tools_dir`, checked against its
    pinned sha256, and returns the folder holding hdiffz and hpatchz."""
    return _pinned().bind(lambda pinned: _install(tools_dir, *pinned))


@safe((OSError,))
def _execute(args: list[str]) -> subprocess.CompletedProcess[str]:
    return subprocess.run(args, stdin=subprocess.DEVNULL, capture_output=True, text=True, check=False)  # noqa: S603


def _run(args: list[str], what: str) -> Result[None, Problem]:
    return (
        _execute(args)
        .alt(because(f"cannot run {args[0]}"))
        .bind(
            lambda done: Success(None) if done.returncode == 0 else fail(f"{what} failed:\n{done.stdout}{done.stderr}"),
        )
    )


def _tool(env: str, name: str) -> Result[str, Problem]:
    return maybe_to_result(
        Maybe.from_optional(shutil.which(os.environ.get(env, name))),
        Problem(
            message=f"{name} not found (set {env} or put it on PATH: python3 scripts/delta/run.py fetch-tools <dir>)"
        ),
    )


@dataclass(frozen=True)
class HDiffPatch:
    hdiffz: str
    hpatchz: str

    @classmethod
    def from_path(cls) -> Result["HDiffPatch", Problem]:
        """`$HDIFFZ`/`$HPATCHZ`, else hdiffz/hpatchz on PATH."""
        return Result.do(
            cls(hdiffz, hpatchz) for hdiffz in _tool("HDIFFZ", "hdiffz") for hpatchz in _tool("HPATCHZ", "hpatchz")
        )

    def diff(self, old: Path, new: Path, patch: Path) -> Result[None, Problem]:
        return _run([self.hdiffz, "-f", *HDIFFZ_ARGS, str(old), str(new), str(patch)], f"hdiffz for {new}")

    def patch(self, old: Path, patch: Path, out: Path) -> Result[None, Problem]:
        return _run([self.hpatchz, "-f", str(old), str(patch), str(out)], f"hpatchz for {patch}")

    def diff_checked(self, old: Path, new: Path, patch: Path) -> Result[None, Problem]:
        """Creates the patch and proves it reproduces `new`."""
        return self.diff(old, new, patch).bind(
            lambda _: with_scratch(lambda scratch: _round_trip(self, old, new, patch, scratch / "roundtrip")),
        )


def _round_trip(tools: HDiffPatch, old: Path, new: Path, patch: Path, out: Path) -> Result[None, Problem]:
    return Result.do(
        None
        for _ in tools.patch(old, patch, out)
        for rebuilt in sha256_file(out)
        for expected in sha256_file(new)
        for _ in (Success(None) if rebuilt == expected else fail(f"patch round-trip mismatch for {new}"))
    )
