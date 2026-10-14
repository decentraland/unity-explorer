import pytest
from pydantic import ValidationError
from returns.maybe import Nothing, Some
from returns.pipeline import is_successful
from returns.result import safe

from delta.domain import (
    ApplyDiff,
    ApplyWhole,
    BaseDelta,
    BaseResult,
    DeletePath,
    DeltaPlan,
    DiffJob,
    DiffPatch,
    MakeSymlink,
    Manifest,
    Mismatch,
    Platform,
    SetMode,
    SignatureCheck,
    WholeFile,
    WholeJob,
    ZipFolder,
    ZipRef,
    ZipRegularFile,
    ZipSymlink,
)
from delta.transform import (
    application_steps,
    assemble_manifest,
    base_delta,
    compare_trees,
    job_summary,
    needs_whole_trial,
    patch_path,
    plan_delta,
    prefer_whole,
    signature_differences,
    signed_artifacts,
    zip_layout_problem,
)
from tests.builders import link, problem_of, regular, tree, value_of

SHA = "b" * 64
OLD_BUNDLE = "Data/aa/ui_assets_all_0123456789abcdef0123456789abcdef.bundle"
NEW_BUNDLE = "Data/aa/ui_assets_all_fedcba9876543210fedcba9876543210.bundle"


def test_plan_patches_changed_files_and_skips_unchanged() -> None:
    plan = plan_delta(tree(regular("a", b"1"), regular("same")), tree(regular("a", b"2"), regular("same")))
    assert plan == DeltaPlan(deleted=(), jobs=(DiffJob(path="a", from_path="a"),))


def test_plan_ships_new_files_whole_and_lists_deleted_paths() -> None:
    plan = plan_delta(tree(regular("gone.txt"), link("old-link", "gone.txt")), tree(regular("added.txt")))
    assert plan == DeltaPlan(deleted=("gone.txt", "old-link"), jobs=(WholeJob(path="added.txt"),))


def test_plan_pairs_a_renamed_bundle_with_its_old_name() -> None:
    plan = plan_delta(tree(regular(OLD_BUNDLE, b"1")), tree(regular(NEW_BUNDLE, b"2")))
    assert plan == DeltaPlan(deleted=(OLD_BUNDLE,), jobs=(DiffJob(path=NEW_BUNDLE, from_path=OLD_BUNDLE),))


def test_plan_ships_a_bundle_whole_when_the_old_name_is_ambiguous() -> None:
    other = "Data/aa/ui_assets_all_11111111111111111111111111111111.bundle"
    plan = plan_delta(tree(regular(OLD_BUNDLE), regular(other)), tree(regular(NEW_BUNDLE)))
    assert plan.jobs == (WholeJob(path=NEW_BUNDLE),)


def test_plan_ships_a_case_only_rename_whole() -> None:
    plan = plan_delta(tree(regular("Plugin.dll", b"old")), tree(regular("plugin.dll", b"new")))
    assert plan == DeltaPlan(deleted=("Plugin.dll",), jobs=(WholeJob(path="plugin.dll"),))


def test_plan_ships_a_file_replacing_a_symlink_whole() -> None:
    plan = plan_delta(tree(regular("t"), link("Lib", "t")), tree(regular("t"), regular("Lib/lib")))
    assert plan == DeltaPlan(deleted=("Lib",), jobs=(WholeJob(path="Lib/lib"),))


def test_plan_ignores_mode_and_symlink_only_changes() -> None:
    plan = plan_delta(
        tree(regular("run.sh", mode="644"), regular("t"), link("l", "t")),
        tree(regular("run.sh", mode="755"), regular("t"), link("l", "run.sh")),
    )
    assert plan == DeltaPlan(deleted=(), jobs=())


@pytest.mark.parametrize(
    ("file_size", "diff_size", "trial"),
    [(100, 49, False), (100, 50, True), (100, 120, True), (0, 0, True)],
)
def test_whole_is_tried_from_half_the_file_size(file_size: int, diff_size: int, *, trial: bool) -> None:
    assert needs_whole_trial(file_size, diff_size) is trial


def test_whole_wins_only_when_strictly_smaller() -> None:
    assert prefer_whole(10, 9)
    assert not prefer_whole(10, 10)


def test_patch_path() -> None:
    assert patch_path("v1.0.0", "Data/space dir/a.bin") == "from/v1.0.0/Data/space dir/a.bin.hdiff"


@pytest.mark.parametrize(("base", "path"), [("not-a-tag", "a"), ("v1.0.0", "../a")])
def test_pure_functions_reject_arguments_outside_their_types(base: str, path: str) -> None:
    # A type violation is a programming error: pydantic's validate_call refuses the call.
    assert not is_successful(safe((ValidationError,))(patch_path)(base, path))


def _diff(path: str, from_path: str | None = None) -> DiffPatch:
    return DiffPatch(
        path=path,
        from_path=from_path or path,
        from_sha256=SHA,
        patch=f"from/v1.0.0/{path}.hdiff",
        patch_size=10,
        patch_sha256=SHA,
    )


def _whole(path: str) -> WholeFile:
    return WholeFile(path=path, patch=f"from/v1.0.0/{path}.hdiff", patch_size=20, patch_sha256=SHA)


def test_base_delta_splits_encoded_patches_and_checks_the_plan() -> None:
    plan = DeltaPlan(deleted=("gone",), jobs=(DiffJob(path="a", from_path="a"), WholeJob(path="b")))
    # A planned diff can be encoded whole.
    assert value_of(base_delta(plan, (_whole("a"), _whole("b")))) == BaseDelta(
        deleted=("gone",),
        patches=(),
        whole=(_whole("a"), _whole("b")),
    )
    assert "do not match the plan" in problem_of(base_delta(plan, (_diff("a"),)))


def _manifest() -> Manifest:
    files = tree(regular("a"), regular("b", mode="755"), link("c", "a"))
    result = BaseResult(base="v1.0.0", delta=BaseDelta(deleted=("gone",), patches=(_diff("a"),), whole=(_whole("b"),)))
    zip_ref = ZipRef(path="Decentraland_windows64.zip", size=1048576, sha256=SHA)
    return value_of(assemble_manifest("v1.1.0", Platform.WINDOWS64, zip_ref, files, (result,)))


def test_assemble_keys_bases_by_tag_and_rejects_duplicates() -> None:
    manifest = _manifest()
    assert list(manifest.bases) == ["v1.0.0"]
    result = BaseResult(base="v1.0.0", delta=manifest.bases["v1.0.0"])
    duplicated = assemble_manifest(manifest.version, manifest.platform, manifest.zip, manifest.files, (result, result))
    assert "duplicate" in problem_of(duplicated)


def test_steps_delete_first_then_patch_then_whole_then_links_and_modes() -> None:
    assert value_of(application_steps(_manifest(), "v1.0.0")) == (
        DeletePath(path="gone"),
        ApplyDiff(source="a", patch="from/v1.0.0/a.hdiff", patch_sha256=SHA, path="a"),
        ApplyWhole(patch="from/v1.0.0/b.hdiff", patch_sha256=SHA, path="b"),
        SetMode(path="a", mode="644"),
        SetMode(path="b", mode="755"),
        MakeSymlink(path="c", target="a"),
    )


def test_steps_fail_for_an_unknown_base() -> None:
    assert "no base" in problem_of(application_steps(_manifest(), "v0.9.0"))


def test_compare_trees_lists_every_mismatch() -> None:
    want = tree(regular("same"), regular("changed", b"1"), regular("missing"), regular("mode", mode="755"))
    got = tree(regular("same"), regular("changed", b"2"), regular("extra"), regular("mode", mode="644"))
    assert compare_trees(want, got, compare_modes=True) == (
        Mismatch(kind="missing", path="missing"),
        Mismatch(kind="unexpected", path="extra"),
        Mismatch(kind="different", path="changed"),
        Mismatch(kind="different", path="mode"),
    )
    assert Mismatch(kind="different", path="mode") not in compare_trees(want, got, compare_modes=False)


def test_signed_artifacts() -> None:
    build = tree(regular("Decentraland.exe"), regular("sub/Helper.exe"), regular("readme.txt"))
    assert signed_artifacts(Platform.WINDOWS64, build) == ("Decentraland.exe", "sub/Helper.exe")
    app = tree(regular("Decentraland.app/Contents/Info.plist"), regular("Other.app/x"), regular("x.app"))
    assert signed_artifacts(Platform.MACOS, app) == ("Decentraland.app", "Other.app")


def test_signature_differences() -> None:
    new = (SignatureCheck(artifact="a.exe", valid=True), SignatureCheck(artifact="b.exe", valid=True))
    assert signature_differences(new, new) == ()
    rebuilt = (SignatureCheck(artifact="a.exe", valid=False),)
    assert signature_differences(new, rebuilt) == (
        "a.exe: new build ok, rebuilt invalid",
        "b.exe: new build ok, rebuilt absent",
    )


def test_job_summary() -> None:
    summary = job_summary(_manifest(), ("v0.9.0",))
    assert "| v1.0.0 | 1 | 1 | 1 | 0.0 MB | 1.0 MB | 100% |" in summary
    assert "Left out after a failure: v0.9.0" in summary


def _folder(path: str) -> ZipFolder:
    return ZipFolder(path=path)


def test_zip_layout_accepts_a_normal_build() -> None:
    members = (_folder("Fw"), ZipRegularFile(path="Fw/A/lib", mode="644"), ZipSymlink(path="Fw/Current", target="A"))
    assert zip_layout_problem(members) == Nothing


def test_zip_layout_rejects_a_member_written_through_a_symlink() -> None:
    members = (ZipSymlink(path="lnk", target="sub"), ZipRegularFile(path="lnk/pwned", mode="644"))
    assert zip_layout_problem(members) == Some("zip entry lnk/pwned lies under the file or symlink lnk")


def test_zip_layout_rejects_a_symlink_leaving_the_build() -> None:
    members = (ZipSymlink(path="lnk", target="../escape"),)
    assert zip_layout_problem(members) == Some("zip symlink leaves the build: lnk -> ../escape")
