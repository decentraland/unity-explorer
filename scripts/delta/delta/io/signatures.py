"""Code-signature verification of the signed artifacts of a build."""

import shutil
import subprocess
from collections.abc import Callable
from functools import partial
from pathlib import Path

from returns.converters import maybe_to_result
from returns.iterables import Fold
from returns.maybe import Maybe
from returns.result import Result, Success, safe

from delta.domain import Platform, Problem, SignatureCheck, because
from delta.io.fs import scan_tree
from delta.transform import signed_artifacts

Verifier = Callable[[Path], Result[bool, Problem]]
"""Whether the artifact's signature verifies."""

_COMMANDS = {
    Platform.WINDOWS64: ("osslsigncode", "verify", "-in"),
    Platform.MACOS: ("codesign", "--verify", "--deep", "--strict"),
}


@safe((OSError,))
def _verifies(command: list[str]) -> bool:
    return subprocess.run(command, stdin=subprocess.DEVNULL, capture_output=True, check=False).returncode == 0  # noqa: S603


def system_verifier(platform: Platform) -> Result[Verifier, Problem]:
    """osslsigncode (Authenticode) for windows64, codesign for macos."""
    tool, *args = _COMMANDS[platform]

    def verifier(executable: str) -> Verifier:
        return lambda artifact: _verifies([executable, *args, str(artifact)]).alt(because(f"cannot run {tool}"))

    return maybe_to_result(
        Maybe.from_optional(shutil.which(tool)).map(verifier),
        Problem(message=f"'{tool}' not found on PATH"),
    )


def _check(verify: Verifier, root: Path, artifact: str) -> Result[SignatureCheck, Problem]:
    return verify(root / artifact).map(partial(_signature_check, artifact))


def _signature_check(artifact: str, valid: bool) -> SignatureCheck:  # noqa: FBT001 (mapped over a Result)
    return SignatureCheck(artifact=artifact, valid=valid)


def check_signatures(platform: Platform, root: Path, verify: Verifier) -> Result[tuple[SignatureCheck, ...], Problem]:
    empty: Result[tuple[SignatureCheck, ...], Problem] = Success(())
    return scan_tree(root).bind(
        lambda tree: Fold.collect([_check(verify, root, a) for a in signed_artifacts(platform, tree)], empty),
    )
