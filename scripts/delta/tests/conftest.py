import os

import pytest
from returns.pipeline import is_successful

from delta.io.hdiff import HDiffPatch
from tests.builders import problem_of


@pytest.fixture(scope="session")
def tools() -> HDiffPatch:
    """The pinned HDiffPatch on PATH. Missing tools skip locally and fail in CI."""
    found = HDiffPatch.from_path()
    if is_successful(found):
        return found.unwrap()
    if os.environ.get("CI"):
        pytest.fail(problem_of(found))
    pytest.skip("hdiffz/hpatchz not on PATH (python3 scripts/delta/run.py fetch-tools <dir>)")
