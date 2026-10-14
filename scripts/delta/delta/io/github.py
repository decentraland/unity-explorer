"""GitHub releases through the `gh` CLI (authenticated by GH_TOKEN)."""

import os
import shutil
import subprocess

from pydantic import TypeAdapter

from delta.domain import DeltaError, GitHubRelease, ReleaseState, ReleaseTag
from delta.transform import release_state

_RELEASES: TypeAdapter[tuple[GitHubRelease, ...]] = TypeAdapter(tuple[GitHubRelease, ...])


def repository() -> str:
    repo = os.environ.get("GITHUB_REPOSITORY", "")
    if not repo:
        msg = "GITHUB_REPOSITORY is not set"
        raise DeltaError(msg)
    return repo


def _gh(*args: str) -> subprocess.CompletedProcess[str]:
    gh = shutil.which("gh")
    if gh is None:
        msg = "'gh' not found on PATH"
        raise DeltaError(msg)
    return subprocess.run([gh, *args], stdin=subprocess.DEVNULL, capture_output=True, text=True, check=False)  # noqa: S603


def list_releases(repo: str) -> tuple[GitHubRelease, ...]:
    result = _gh("api", f"repos/{repo}/releases?per_page=100")
    if result.returncode != 0:
        msg = f"listing releases failed: {result.stderr.strip()}"
        raise DeltaError(msg)
    return _RELEASES.validate_json(result.stdout)


def lookup_release(repo: str, tag: ReleaseTag) -> ReleaseState:
    result = _gh("api", f"repos/{repo}/releases/tags/{tag}")
    return release_state(result.returncode, result.stdout, result.stderr)
