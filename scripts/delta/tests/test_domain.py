import pytest
from pydantic import TypeAdapter, ValidationError

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
    version_of,
)
from tests.builders import link, regular, tree

SHA = "a" * 64

REL_PATH: TypeAdapter[str] = TypeAdapter(RelPath)
TAG: TypeAdapter[str] = TypeAdapter(ReleaseTag)


@pytest.mark.parametrize("path", ["a", "Data/space dir/asset file.bin", "x.app/Contents/Info.plist", ".hidden"])
def test_rel_path_accepts(path: str) -> None:
    assert REL_PATH.validate_python(path) == path


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
    with pytest.raises(ValidationError):
        REL_PATH.validate_python(path)


@pytest.mark.parametrize("tag", ["v1.0.0", "v0.187.0-alpha", "v1.2.3-rc.1"])
def test_release_tag_accepts(tag: str) -> None:
    assert TAG.validate_python(tag) == tag


@pytest.mark.parametrize(
    "tag",
    ["", "avatar-preview-renderer/v3.4.0", "v1.0.0-../x", "v9.9.9?x", "vNext", "v1.0", "v1.0.0\n", "1.0.0"],
)
def test_release_tag_rejects(tag: str) -> None:
    with pytest.raises(ValidationError):
        TAG.validate_python(tag)


def test_version_orders_numerically() -> None:
    assert version_of("v0.186.10-alpha") > version_of("v0.186.9-alpha")
    assert version_of("v1.2.3") == Version(1, 2, 3)


def test_models_are_strict_frozen_and_closed() -> None:
    with pytest.raises(ValidationError):
        RegularFile.model_validate({"path": "a", "size": "1", "sha256": SHA, "mode": "644"})
    with pytest.raises(ValidationError):
        RegularFile.model_validate({"path": "a", "size": 1, "sha256": SHA, "mode": "644", "extra": 1})
    with pytest.raises(ValidationError):
        RegularFile(path="a", size=-1, sha256=SHA, mode="644")
    with pytest.raises(ValidationError):
        RegularFile(path="a", size=1, sha256="A" * 64, mode="644")
    with pytest.raises(ValidationError):
        RegularFile(path="a", size=1, sha256=SHA, mode="999")
    file = regular("a")
    with pytest.raises(ValidationError):
        file.size = 2


def test_entry_union_reads_both_cases_from_json() -> None:
    parsed = Tree.model_validate_json(
        f'[{{"path": "a", "size": 1, "sha256": "{SHA}", "mode": "644"}}, {{"path": "b", "symlink": "a"}}]',
    )
    assert isinstance(parsed.root[0], RegularFile)
    assert isinstance(parsed.root[1], Symlink)
    assert Tree.model_validate_json(parsed.model_dump_json()) == parsed


def test_entry_union_rejects_a_mix() -> None:
    with pytest.raises(ValidationError):
        Tree.model_validate_json(f'[{{"path": "a", "size": 1, "sha256": "{SHA}", "mode": "644", "symlink": "b"}}]')


def test_tree_must_be_sorted_and_unique() -> None:
    with pytest.raises(ValidationError):
        Tree((regular("b"), regular("a")))
    with pytest.raises(ValidationError):
        Tree((regular("a"), regular("a")))


def test_tree_rejects_entries_inside_a_file_or_symlink() -> None:
    with pytest.raises(ValidationError):
        tree(regular("a"), regular("a/b"))
    with pytest.raises(ValidationError):
        tree(link("Lib", "x"), regular("Lib/lib"))


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
def test_lexical_containment(link_path: str, target: str, escapes: bool) -> None:  # noqa: FBT001
    assert lexically_escapes(link_path, target) is escapes


def test_tree_rejects_symlinks_leaving_the_build() -> None:
    with pytest.raises(ValidationError, match="leaves the build"):
        tree(link("sub/link", "../../outside"))
    with pytest.raises(ValidationError):
        tree(link("sub/link", "/etc/passwd"))
    with pytest.raises(ValidationError):
        tree(link("sub/link", "x\nF\tevil"))
    with pytest.raises(ValidationError):
        tree(link("sub/link", "..\\x"))


def _manifest(files: Tree, delta: BaseDelta, version: str = "v1.1.0") -> Manifest:
    zip_ref = ZipRef(path="Decentraland_windows64.zip", size=1, sha256=SHA)
    return Manifest(version=version, platform=Platform.WINDOWS64, zip=zip_ref, files=files, bases={"v1.0.0": delta})


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
    manifest = _manifest(tree(regular("a"), regular("b"), link("c", "a")), delta)
    assert Manifest.model_validate_json(manifest.model_dump_json()) == manifest


def test_manifest_rejects_inconsistent_bases() -> None:
    whole = WholeFile(path="c", patch="from/v1.0.0/c.hdiff", patch_size=1, patch_sha256=SHA)
    with pytest.raises(ValidationError, match="not a regular file"):
        _manifest(tree(regular("a"), link("c", "a")), BaseDelta(deleted=(), patches=(), whole=(whole,)))
    with pytest.raises(ValidationError, match="deleted but listed"):
        _manifest(tree(regular("a")), BaseDelta(deleted=("a",), patches=(), whole=()))
    with pytest.raises(ValidationError, match="its own base"):
        _manifest(tree(regular("a")), BaseDelta(deleted=(), patches=(), whole=()), version="v1.0.0")


def test_base_delta_rejects_a_path_rebuilt_twice() -> None:
    whole = WholeFile(path="a", patch="from/v1.0.0/a.hdiff", patch_size=1, patch_sha256=SHA)
    with pytest.raises(ValidationError, match="more than once"):
        BaseDelta(deleted=(), patches=(), whole=(whole, whole))
