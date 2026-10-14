import pytest
from pydantic import TypeAdapter, ValidationError
from returns.pipeline import is_successful
from returns.result import safe

from delta.domain import (
    BaseDelta,
    DiffPatch,
    Manifest,
    Platform,
    RegularFile,
    ReleaseTag,
    RelPath,
    Symlink,
    Tree,
    Version,
    WholeFile,
    ZipRef,
    lexically_escapes,
    parse,
    parse_as,
    parse_json,
    version_of,
)
from tests.builders import link, problem_of, regular, tree, value_of

SHA = "a" * 64

REL_PATH: TypeAdapter[str] = TypeAdapter(RelPath)
TAG: TypeAdapter[str] = TypeAdapter(ReleaseTag)


@pytest.mark.parametrize("path", ["a", "Data/space dir/asset file.bin", "x.app/Contents/Info.plist", ".hidden"])
def test_rel_path_accepts(path: str) -> None:
    assert value_of(parse_as(REL_PATH, path)) == path


@pytest.mark.parametrize(
    "path",
    [
        "",
        "/etc/passwd",
        "a/../b",
        "..",
        "./a",
        "a//b",
        "a/",
        "..\\..\\x.dll",
        "C:x.dll",
        "\\\\host\\share\\x",
        "Decentraland.exe:ads",
        "a\x00b",
        "a\nb",
        "a\tb",
    ],
)
def test_rel_path_rejects(path: str) -> None:
    assert not is_successful(parse_as(REL_PATH, path))


@pytest.mark.parametrize("tag", ["v1.0.0", "v0.187.0-alpha", "v1.2.3-rc.1"])
def test_release_tag_accepts(tag: str) -> None:
    assert value_of(parse_as(TAG, tag)) == tag


@pytest.mark.parametrize(
    "tag",
    ["", "avatar-preview-renderer/v3.4.0", "v1.0.0-../x", "v9.9.9?x", "vNext", "v1.0", "v1.0.0\n", "1.0.0"],
)
def test_release_tag_rejects(tag: str) -> None:
    assert not is_successful(parse_as(TAG, tag))


def test_version_orders_numerically() -> None:
    assert version_of("v0.186.10-alpha") > version_of("v0.186.9-alpha")
    assert version_of("v1.2.3") == Version(1, 2, 3)


@pytest.mark.parametrize(
    "fields",
    [
        {"path": "a", "size": "1", "sha256": SHA, "mode": "644"},
        {"path": "a", "size": 1, "sha256": SHA, "mode": "644", "extra": 1},
        {"path": "a", "size": -1, "sha256": SHA, "mode": "644"},
        {"path": "a", "size": 1, "sha256": "A" * 64, "mode": "644"},
        {"path": "a", "size": 1, "sha256": SHA, "mode": "999"},
    ],
)
def test_models_are_strict_and_closed(fields: dict[str, object]) -> None:
    assert not is_successful(parse(RegularFile, fields))


def test_models_are_frozen() -> None:
    assert not is_successful(safe((ValidationError,))(setattr)(regular("a"), "size", 2))


def test_entry_union_reads_both_cases_from_json() -> None:
    text = f'[{{"path": "a", "size": 1, "sha256": "{SHA}", "mode": "644"}}, {{"path": "b", "symlink": "a"}}]'
    parsed = value_of(parse_json(Tree, text))
    assert isinstance(parsed.root[0], RegularFile)
    assert isinstance(parsed.root[1], Symlink)
    assert value_of(parse_json(Tree, parsed.model_dump_json())) == parsed


def test_entry_union_rejects_a_mix() -> None:
    mixed = f'[{{"path": "a", "size": 1, "sha256": "{SHA}", "mode": "644", "symlink": "b"}}]'
    assert not is_successful(parse_json(Tree, mixed))


def test_tree_must_be_sorted_and_unique() -> None:
    assert "sorted" in problem_of(parse(Tree, (regular("b"), regular("a"))))
    assert "unique" in problem_of(parse(Tree, (regular("a"), regular("a"))))


def test_tree_rejects_entries_inside_a_file_or_symlink() -> None:
    assert "inside a," in problem_of(parse(Tree, (regular("a"), regular("a/b"))))
    assert "inside Lib," in problem_of(parse(Tree, (link("Lib", "x"), regular("Lib/lib"))))


@pytest.mark.parametrize(
    ("link_path", "target", "escapes"),
    [
        ("sub/link", "../../outside", True),
        ("sub/link", "../sibling", False),
        ("sub/up", "..", False),
        ("link", "..", True),
        ("Fw/Versions/Current", "A", False),
        ("a/b", "./../../x", True),
        ("a/b", "c/../../..", True),
    ],
)
def test_lexical_containment(link_path: str, target: str, *, escapes: bool) -> None:
    assert lexically_escapes(link_path, target) is escapes


@pytest.mark.parametrize("target", ["../../outside", "/etc/passwd", "x\nF\tevil", "..\\x"])
def test_tree_rejects_symlinks_that_can_leave_the_build(target: str) -> None:
    assert not is_successful(parse(Tree, ({"path": "sub/link", "symlink": target},)))


def _manifest_fields(files: Tree, delta: BaseDelta, version: str = "v1.1.0") -> dict[str, object]:
    zip_ref = ZipRef(path="Decentraland_windows64.zip", size=1, sha256=SHA)
    return {
        "version": version,
        "platform": Platform.WINDOWS64,
        "zip": zip_ref,
        "files": files,
        "bases": {"v1.0.0": delta},
    }


def test_manifest_round_trips_through_json() -> None:
    delta = BaseDelta(
        deleted=("gone.txt",),
        patches=(
            DiffPatch(
                path="a",
                from_path="a",
                from_sha256=SHA,
                patch="from/v1.0.0/a.hdiff",
                patch_size=3,
                patch_sha256=SHA,
            ),
        ),
        whole=(WholeFile(path="b", patch="from/v1.0.0/b.hdiff", patch_size=3, patch_sha256=SHA),),
    )
    manifest = value_of(parse(Manifest, _manifest_fields(tree(regular("a"), regular("b"), link("c", "a")), delta)))
    assert value_of(parse_json(Manifest, manifest.model_dump_json())) == manifest


def test_manifest_rejects_inconsistent_bases() -> None:
    whole = WholeFile(path="c", patch="from/v1.0.0/c.hdiff", patch_size=1, patch_sha256=SHA)
    rebuilt_symlink = _manifest_fields(
        tree(regular("a"), link("c", "a")), BaseDelta(deleted=(), patches=(), whole=(whole,))
    )
    assert "not a regular file" in problem_of(parse(Manifest, rebuilt_symlink))
    deleted_but_listed = _manifest_fields(tree(regular("a")), BaseDelta(deleted=("a",), patches=(), whole=()))
    assert "deleted but listed" in problem_of(parse(Manifest, deleted_but_listed))
    own_base = _manifest_fields(tree(regular("a")), BaseDelta(deleted=(), patches=(), whole=()), version="v1.0.0")
    assert "its own base" in problem_of(parse(Manifest, own_base))


def test_base_delta_rejects_a_path_rebuilt_twice() -> None:
    whole = WholeFile(path="a", patch="from/v1.0.0/a.hdiff", patch_size=1, patch_sha256=SHA)
    assert "more than once" in problem_of(parse(BaseDelta, {"deleted": (), "patches": (), "whole": (whole, whole)}))
