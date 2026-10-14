import os

import pytest

from delta.domain import DeltaError
from delta.io.hdiff import HDiffPatch


@pytest.fixture(scope="session")
def tools() -> HDiffPatch:
    """The pinned HDiffPatch on PATH. Missing tools skip locally and fail in CI."""
    try:
        return HDiffPatch.from_path()
    except DeltaError:
        if os.environ.get("CI"):
            raise
        pytest.skip("hdiffz/hpatchz not on PATH (python3 scripts/delta/run.py fetch-tools <dir>)")
