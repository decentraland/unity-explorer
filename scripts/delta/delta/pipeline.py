"""Steps that combine I/O with the pure transformations: one base's patch set, its replay,
and the manifest."""

import sys
import tempfile
from pathlib import Path

from delta.domain import (
    BaseResult,
    DeltaError,
    DiffJob,
    DiffPatch,
    Manifest,
    Platform,
    ReleaseTag,
    WholeFile,
)
from delta.io import fs
from delta.io.hdiff import HDiffPatch
from delta.io.signatures import Verifier, check_signatures
from delta.transform import (
    application_steps,
    assemble_manifest,
    base_delta,
    compare_trees,
    needs_whole_trial,
    patch_path,
    plan_delta,
    prefer_whole,
    signature_differences,
    size_line,
)


def log(message: str) -> None:
    print(f"[delta] {message}", file=sys.stderr)  # noqa: T201


def base_result_file(out: Path, base: ReleaseTag) -> Path:
    return out / f"base-{base}.json"


def make_delta(base_root: Path, new_root: Path, base: ReleaseTag, out: Path, tools: HDiffPatch) -> BaseResult:
    """Writes `<out>/from/<base>/<path>.hdiff` for every planned job and
    `<out>/base-<base>.json`. Every patch is round-trip checked."""
    base_tree, new_tree = fs.scan_tree(base_root), fs.scan_tree(new_root)
    plan = plan_delta(base_tree, new_tree)
    fs.remove_paths(out / "from" / base)
    new_files = {f.path: f for f in new_tree.files()}
    encoded: list[DiffPatch | WholeFile] = []
    with tempfile.TemporaryDirectory() as scratch:
        empty = Path(scratch) / "empty"
        empty.touch()
        for job in plan.jobs:
            new_file = new_root / job.path
            rel = patch_path(base, job.path)
            patch = out / rel
            patch.parent.mkdir(parents=True, exist_ok=True)
            size = new_files[job.path].size
            if isinstance(job, DiffJob):
                tools.diff_checked(base_root / job.from_path, new_file, patch)
                diff_size = patch.stat().st_size
                if needs_whole_trial(size, diff_size):
                    whole = Path(scratch) / "whole.hdiff"
                    tools.diff_checked(empty, new_file, whole)
                    if prefer_whole(diff_size, whole.stat().st_size):
                        whole.replace(patch)
                        encoded.append(_whole(job.path, rel, patch))
                        continue
                encoded.append(
                    DiffPatch(
                        path=job.path,
                        from_path=job.from_path,
                        from_sha256=fs.sha256_file(base_root / job.from_path),
                        patch=rel,
                        patch_size=patch.stat().st_size,
                        patch_sha256=fs.sha256_file(patch),
                    ),
                )
            else:
                tools.diff_checked(empty, new_file, patch)
                encoded.append(_whole(job.path, rel, patch))
    result = BaseResult(base=base, delta=base_delta(plan, tuple(encoded)))
    fs.write_text(base_result_file(out, base), result.model_dump_json(indent=2))
    log(size_line(result, new_tree))
    return result


def _whole(path: str, rel: str, patch: Path) -> WholeFile:
    return WholeFile(path=path, patch=rel, patch_size=patch.stat().st_size, patch_sha256=fs.sha256_file(patch))


def build_manifest(
    version: ReleaseTag,
    platform: Platform,
    new_root: Path,
    new_zip: Path,
    results: tuple[BaseResult, ...],
) -> Manifest:
    return assemble_manifest(version, platform, fs.zip_ref(new_zip), fs.scan_tree(new_root), results)


def verify_delta(
    base_root: Path,
    patches: Path,
    manifest: Manifest,
    base: ReleaseTag,
    rebuild: Path,
    tools: HDiffPatch,
) -> Path:
    """Replays the launcher onto a copy of the base in the empty `rebuild` folder and proves
    the result matches `files`. Modes are not compared on Windows."""
    steps = application_steps(manifest, base)
    rebuild = fs.ensure_empty_dir(rebuild)
    fs.replay(steps, base_root, patches, rebuild, tools.patch)
    mismatches = compare_trees(manifest.files, fs.scan_tree(rebuild), compare_modes=not fs.IS_WINDOWS)
    if mismatches:
        details = "\n".join(f"  {m.kind}: {m.path}" for m in mismatches)
        msg = f"rebuilt tree does not match the manifest for base '{base}':\n{details}"
        raise DeltaError(msg)
    log(f"{base}: rebuilt tree matches the manifest")
    return rebuild


def _require_same_signatures(
    platform: Platform,
    new_root: Path,
    rebuilt: Path,
    base: ReleaseTag,
    verify: Verifier,
) -> None:
    differences = signature_differences(
        check_signatures(platform, new_root, verify),
        check_signatures(platform, rebuilt, verify),
    )
    if differences:
        details = "\n".join(differences)
        msg = f"{base}: signature verification of the rebuilt build differs from the new build:\n{details}"
        raise DeltaError(msg)


def delta_for_base(
    version: ReleaseTag,
    platform: Platform,
    new_root: Path,
    new_zip: Path,
    base: ReleaseTag,
    base_zip: Path,
    out: Path,
    tools: HDiffPatch,
    verify: Verifier,
) -> BaseResult:
    """Extracts the base, makes its patch set, replays it and checks that the rebuilt build's
    signatures verify exactly like the new build's. On any failure the base's output in
    `out` is removed."""
    try:
        with tempfile.TemporaryDirectory() as scratch:
            work = Path(scratch)
            base_root = fs.extract_build(base_zip, work / "base")
            result = make_delta(base_root, new_root, base, out, tools)
            manifest = build_manifest(version, platform, new_root, new_zip, (result,))
            rebuilt = verify_delta(base_root, out, manifest, base, work / "rebuild", tools)
            _require_same_signatures(platform, new_root, rebuilt, base, verify)
    except BaseException:
        fs.remove_paths(out / "from" / base, base_result_file(out, base))
        log(f"{base}: failed, its patch set was removed")
        raise
    log(f"{base}: patch set verified")
    return result
