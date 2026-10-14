"""HDiffPatch: the pinned download and the hdiffz/hpatchz processes."""

import os
import platform
import shutil
import subprocess
import tempfile
import time
import urllib.request
import zipfile
from dataclasses import dataclass
from pathlib import Path

from delta.domain import DeltaError
from delta.io.fs import sha256_file

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


def fetch_tools(tools_dir: Path) -> Path:
    """Installs the pinned HDiffPatch build for this OS into `tools_dir`, checked against its
    pinned sha256, and returns the folder holding hdiffz and hpatchz."""
    system, machine = platform.system(), platform.machine()
    pinned = PINNED.get((system, machine)) or PINNED.get((system, "*"))
    if pinned is None:
        msg = f"no pinned HDiffPatch build for {system}/{machine}"
        raise DeltaError(msg)
    asset, sha256 = pinned
    tools_dir.mkdir(parents=True, exist_ok=True)
    archive = tools_dir / f"hdiffpatch_{VERSION}_bin_{asset}.zip"
    if not archive.is_file() or sha256_file(archive) != sha256:
        url = f"https://github.com/sisong/HDiffPatch/releases/download/{VERSION}/{archive.name}"
        _download(url, archive)
    actual = sha256_file(archive)
    if actual != sha256:
        archive.unlink()
        msg = f"HDiffPatch sha256 mismatch: got {actual}, want {sha256}"
        raise DeltaError(msg)
    bin_dir = tools_dir / asset
    shutil.rmtree(bin_dir, ignore_errors=True)
    with zipfile.ZipFile(archive) as zipped:
        zipped.extractall(tools_dir)
    if not bin_dir.is_dir():
        msg = f"unexpected HDiffPatch archive layout: no {asset}/ folder"
        raise DeltaError(msg)
    for tool in bin_dir.iterdir():
        if tool.name.startswith(("hdiffz", "hpatchz")):
            tool.chmod(0o755)
    return bin_dir.resolve()


def _download(url: str, out: Path) -> None:
    for attempt in range(1, _DOWNLOAD_ATTEMPTS + 1):
        try:
            with urllib.request.urlopen(url, timeout=60) as response, out.open("wb") as sink:  # noqa: S310 (fixed https URL)
                shutil.copyfileobj(response, sink)
        except OSError:
            if attempt == _DOWNLOAD_ATTEMPTS:
                raise
            time.sleep(2 * attempt)
        else:
            return


def _run(args: list[str], what: str) -> None:
    result = subprocess.run(args, stdin=subprocess.DEVNULL, capture_output=True, text=True, check=False)  # noqa: S603
    if result.returncode != 0:
        msg = f"{what} failed:\n{result.stdout}{result.stderr}"
        raise DeltaError(msg)


@dataclass(frozen=True)
class HDiffPatch:
    hdiffz: str
    hpatchz: str

    @classmethod
    def from_path(cls) -> "HDiffPatch":
        """`$HDIFFZ`/`$HPATCHZ`, else hdiffz/hpatchz on PATH."""
        hdiffz = shutil.which(os.environ.get("HDIFFZ", "hdiffz"))
        hpatchz = shutil.which(os.environ.get("HPATCHZ", "hpatchz"))
        if hdiffz is None or hpatchz is None:
            msg = "hdiffz and hpatchz must be on PATH (python3 scripts/delta/run.py fetch-tools <dir>)"
            raise DeltaError(msg)
        return cls(hdiffz, hpatchz)

    def diff(self, old: Path, new: Path, patch: Path) -> None:
        _run([self.hdiffz, "-f", *HDIFFZ_ARGS, str(old), str(new), str(patch)], f"hdiffz for {new}")

    def patch(self, old: Path, patch: Path, out: Path) -> None:
        _run([self.hpatchz, "-f", str(old), str(patch), str(out)], f"hpatchz for {patch}")

    def diff_checked(self, old: Path, new: Path, patch: Path) -> None:
        """Creates the patch and proves it reproduces `new`."""
        self.diff(old, new, patch)
        with tempfile.TemporaryDirectory() as scratch:
            roundtrip = Path(scratch) / "roundtrip"
            self.patch(old, patch, roundtrip)
            if sha256_file(roundtrip) != sha256_file(new):
                msg = f"patch round-trip mismatch for {new}"
                raise DeltaError(msg)
