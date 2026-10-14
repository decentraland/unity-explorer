"""Code-signature verification of the signed artifacts of a build."""

import shutil
import subprocess
from collections.abc import Callable
from pathlib import Path

from delta.domain import DeltaError, Platform, SignatureCheck
from delta.io.fs import scan_tree
from delta.transform import signed_artifacts

Verifier = Callable[[Path], bool]

_COMMANDS = {
    Platform.WINDOWS64: ("osslsigncode", "verify", "-in"),
    Platform.MACOS: ("codesign", "--verify", "--deep", "--strict"),
}


def system_verifier(platform: Platform) -> Verifier:
    """osslsigncode (Authenticode) for windows64, codesign for macos."""
    tool, *args = _COMMANDS[platform]
    executable = shutil.which(tool)
    if executable is None:
        msg = f"'{tool}' not found on PATH"
        raise DeltaError(msg)

    def verify(artifact: Path) -> bool:
        result = subprocess.run(  # noqa: S603
            [executable, *args, str(artifact)],
            stdin=subprocess.DEVNULL,
            capture_output=True,
            check=False,
        )
        return result.returncode == 0

    return verify


def check_signatures(platform: Platform, root: Path, verify: Verifier) -> tuple[SignatureCheck, ...]:
    return tuple(
        SignatureCheck(artifact=artifact, valid=verify(root / artifact))
        for artifact in signed_artifacts(platform, scan_tree(root))
    )
