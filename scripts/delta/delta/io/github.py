"""GitHub releases through the `gh` CLI (authenticated by GH_TOKEN)."""

import os
import shutil
import subprocess

from pydantic import TypeAdapter, ValidationError
from returns.converters import maybe_to_result
from returns.maybe import Maybe
from returns.result import Result, safe

from delta.domain import GitHubRelease, Problem, ReleaseState, ReleaseTag, because, fail
from delta.transform import release_state

_RELEASES: TypeAdapter[tuple[GitHubRelease, ...]] = TypeAdapter(tuple[GitHubRelease, ...])


def repository() -> Result[str, Problem]:
    return maybe_to_result(
        Maybe.from_optional(os.environ.get("GITHUB_REPOSITORY") or None),
        Problem(message="GITHUB_REPOSITORY is not set"),
    )


@safe((OSError,))
def _execute(gh: str, args: tuple[str, ...]) -> subprocess.CompletedProcess[str]:
    return subprocess.run([gh, *args], stdin=subprocess.DEVNULL, capture_output=True, text=True, check=False)  # noqa: S603


def _gh(*args: str) -> Result[subprocess.CompletedProcess[str], Problem]:
    return maybe_to_result(Maybe.from_optional(shutil.which("gh")), Problem(message="'gh' not found on PATH")).bind(
        lambda gh: _execute(gh, args).alt(because("cannot run gh"))
    )


def list_releases(repo: str) -> Result[tuple[GitHubRelease, ...], Problem]:
    return _gh("api", f"repos/{repo}/releases?per_page=100").bind(
        lambda done: safe((ValidationError,))(_RELEASES.validate_json)(done.stdout).alt(
            because("invalid releases list")
        )
        if done.returncode == 0
        else fail(f"listing releases failed: {done.stderr.strip()}"),
    )


def lookup_release(repo: str, tag: ReleaseTag) -> Result[ReleaseState, Problem]:
    return _gh("api", f"repos/{repo}/releases/tags/{tag}").bind(
        lambda done: release_state(done.returncode, done.stdout, done.stderr),
    )
