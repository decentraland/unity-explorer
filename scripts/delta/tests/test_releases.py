import pytest
from pydantic import TypeAdapter

from delta.domain import DeltaError, GitHubRelease, PublishedRelease, ReleaseState
from delta.transform import pick_bases, prune_targets, published_explorer_releases, release_state

RELEASES: TypeAdapter[tuple[GitHubRelease, ...]] = TypeAdapter(tuple[GitHubRelease, ...])


def _releases(json: str) -> tuple[PublishedRelease, ...]:
    return published_explorer_releases(RELEASES.validate_json(json))


# API order (created_at) deliberately differs from published_at order.
LISTED = _releases("""[
  {"tag_name": "v0.188.0-alpha", "draft": true, "prerelease": false, "published_at": null},
  {"tag_name": "v0.186.0-alpha", "draft": false, "prerelease": false, "published_at": "2026-10-08T14:33:06Z"},
  {"tag_name": "avatar-preview-renderer/v3.4.0", "draft": false, "prerelease": false,
   "published_at": "2026-10-09T10:00:00Z"},
  {"tag_name": "v0.188.0-rc1", "draft": false, "prerelease": true, "published_at": "2026-10-13T12:00:00Z"},
  {"tag_name": "v0.187.0-alpha", "draft": false, "prerelease": false, "published_at": "2026-10-13T09:00:00Z",
   "name": "ignored", "assets": []},
  {"tag_name": "v0.185.0-alpha", "draft": false, "prerelease": false, "published_at": "2026-10-06T14:22:59Z"},
  {"tag_name": "v0.184.0-alpha", "draft": false, "prerelease": false, "published_at": "2026-10-01T19:18:45Z"},
  {"tag_name": "v0.183.0-alpha", "draft": false, "prerelease": false, "published_at": "2026-09-29T15:45:19Z"}
]""")

# v0.187 was published after v0.188.
OUT_OF_ORDER = _releases("""[
  {"tag_name": "v0.189.0-alpha", "draft": false, "prerelease": false, "published_at": "2026-10-20T10:00:00Z"},
  {"tag_name": "v0.187.0-alpha", "draft": false, "prerelease": false, "published_at": "2026-10-16T10:00:00Z"},
  {"tag_name": "v0.188.0-alpha", "draft": false, "prerelease": false, "published_at": "2026-10-15T10:00:00Z"},
  {"tag_name": "v0.186.10-alpha", "draft": false, "prerelease": false, "published_at": "2026-10-08T10:00:00Z"},
  {"tag_name": "v0.186.9-alpha", "draft": false, "prerelease": false, "published_at": "2026-10-09T10:00:00Z"}
]""")


def test_bases_newest_published_first_skipping_drafts_prereleases_and_renderer_tags() -> None:
    assert pick_bases(LISTED, 3, "v0.188.0-alpha") == ("v0.187.0-alpha", "v0.186.0-alpha", "v0.185.0-alpha")


def test_bases_exclude_the_current_tag() -> None:
    assert pick_bases(LISTED, 2, "v0.187.0-alpha") == ("v0.186.0-alpha", "v0.185.0-alpha")


def test_bases_with_fewer_releases_than_requested_or_none() -> None:
    assert len(pick_bases(LISTED, 10, "v0.188.0-alpha")) == 5
    assert pick_bases(LISTED, 0, "v0.188.0-alpha") == ()


def test_bases_follow_publish_time() -> None:
    assert pick_bases(OUT_OF_ORDER, 2, "v0.189.0-alpha") == ("v0.187.0-alpha", "v0.188.0-alpha")


def test_prune_keeps_the_newest_and_prunes_the_next_window() -> None:
    assert prune_targets(LISTED, 2, 2, None) == ("v0.185.0-alpha", "v0.184.0-alpha")
    assert prune_targets(LISTED, 4, 5, None) == ("v0.183.0-alpha",)
    assert prune_targets(LISTED, 10, 3, None) == ()


def test_prune_ranks_by_version_not_publish_time() -> None:
    assert prune_targets(OUT_OF_ORDER, 2, 5, None) == ("v0.187.0-alpha", "v0.186.10-alpha", "v0.186.9-alpha")


def test_prune_never_returns_the_live_release() -> None:
    assert prune_targets(OUT_OF_ORDER, 2, 5, "v0.187.0-alpha") == ("v0.186.10-alpha", "v0.186.9-alpha")


def test_a_malformed_v_tag_fails_instead_of_being_skipped() -> None:
    with pytest.raises(DeltaError, match="malformed"):
        _releases(
            '[{"tag_name": "vNext", "draft": false, "prerelease": false, "published_at": "2026-10-20T10:00:00Z"}]',
        )


def test_release_list_is_strict() -> None:
    with pytest.raises(ValueError, match="bool"):
        _releases('[{"tag_name": "v1.0.0", "draft": "false", "prerelease": false, "published_at": null}]')


@pytest.mark.parametrize(
    ("exit_code", "stdout", "stderr", "state"),
    [
        (1, "", "gh: Not Found (HTTP 404)", ReleaseState.MISSING),
        (0, '{"tag_name": "v1.0.0", "draft": true, "prerelease": false, "published_at": null}', "", ReleaseState.DRAFT),
        (
            0,
            '{"tag_name": "v1.0.0", "draft": false, "prerelease": false, "published_at": "2026-10-20T10:00:00Z"}',
            "",
            ReleaseState.PUBLISHED,
        ),
    ],
)
def test_release_state(exit_code: int, stdout: str, stderr: str, state: ReleaseState) -> None:
    assert release_state(exit_code, stdout, stderr) is state


def test_an_api_failure_is_never_unpublished() -> None:
    with pytest.raises(DeltaError, match="502"):
        release_state(1, "", "gh: Bad Gateway (HTTP 502)")
