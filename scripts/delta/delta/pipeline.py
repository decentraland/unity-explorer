"""Steps that combine I/O with the pure transformations: one base's patch set, its replay,
and the manifest. Every step returns a `Result` and stops at the first Problem."""

import sys
from collections.abc import Mapping
from dataclasses import dataclass
from functools import partial
from pathlib import Path

from returns.result import Result, Success

from delta.domain import (
    BaseResult,
    DiffJob,
    DiffPatch,
    Job,
    Manifest,
    Platform,
    Problem,
    ReleaseTag,
    RelPath,
    Step,
    Tree,
    WholeFile,
    WholeJob,
    fail,
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


# --- one patch set ----------------------------------------------------------------------


@dataclass(frozen=True)
class _Encoder:
    """Writes the patch of one planned job and describes it."""

    base_root: Path
    new_root: Path
    base: ReleaseTag
    out: Path
    scratch: Path
    sizes: Mapping[RelPath, int]
    tools: HDiffPatch

    @property
    def empty(self) -> Path:
        return self.scratch / "empty"

    def encode(self, job: Job) -> Result[DiffPatch | WholeFile, Problem]:
        rel = patch_path(self.base, job.path)
        patch = self.out / rel
        new_file = self.new_root / job.path
        prepared = fs.make_parent(patch)
        match job:
            case WholeJob():
                return prepared.bind(lambda _: self.tools.diff_checked(self.empty, new_file, patch)).bind(
                    lambda _: self._whole(job.path, rel, patch),
                )
            case DiffJob(from_path=from_path):
                return (
                    prepared.bind(lambda _: self.tools.diff_checked(self.base_root / from_path, new_file, patch))
                    .bind(lambda _: fs.file_size(patch))
                    .bind(lambda diff_size: self._smaller(job, rel, patch, diff_size))
                )

    def _smaller(
        self, job: DiffJob, rel: RelPath, patch: Path, diff_size: int
    ) -> Result[DiffPatch | WholeFile, Problem]:
        """Keeps the diff unless encoding the file alone is strictly smaller."""
        if not needs_whole_trial(self.sizes[job.path], diff_size):
            return self._diff(job, rel, patch)
        whole = self.scratch / "whole.hdiff"
        return (
            self.tools.diff_checked(self.empty, self.new_root / job.path, whole)
            .bind(lambda _: fs.file_size(whole))
            .bind(
                lambda whole_size: fs.move(whole, patch).bind(lambda _: self._whole(job.path, rel, patch))
                if prefer_whole(diff_size, whole_size)
                else self._diff(job, rel, patch),
            )
        )

    def _whole(self, path: RelPath, rel: RelPath, patch: Path) -> Result[DiffPatch | WholeFile, Problem]:
        return Result.do(
            WholeFile(path=path, patch=rel, patch_size=size, patch_sha256=sha256)
            for size in fs.file_size(patch)
            for sha256 in fs.sha256_file(patch)
        )

    def _diff(self, job: DiffJob, rel: RelPath, patch: Path) -> Result[DiffPatch | WholeFile, Problem]:
        return Result.do(
            DiffPatch(
                path=job.path,
                from_path=job.from_path,
                from_sha256=from_sha256,
                patch=rel,
                patch_size=size,
                patch_sha256=sha256,
            )
            for from_sha256 in fs.sha256_file(self.base_root / job.from_path)
            for size in fs.file_size(patch)
            for sha256 in fs.sha256_file(patch)
        )

    def encode_all(self, jobs: tuple[Job, ...]) -> Result[tuple[DiffPatch | WholeFile, ...], Problem]:
        """Encodes the jobs in order; `bind` skips every job after the first Problem (each costs
        an hdiffz run)."""
        encoded: Result[tuple[DiffPatch | WholeFile, ...], Problem] = Success(())
        for job in jobs:
            encoded = encoded.bind(partial(self._append, job))
        return encoded

    def _append(
        self,
        job: Job,
        done: tuple[DiffPatch | WholeFile, ...],
    ) -> Result[tuple[DiffPatch | WholeFile, ...], Problem]:
        return self.encode(job).map(lambda item: (*done, item))


@dataclass(frozen=True)
class _Trees:
    base: Tree
    new: Tree


def _encode(
    roots: tuple[Path, Path],
    trees: _Trees,
    base: ReleaseTag,
    out: Path,
    tools: HDiffPatch,
    scratch: Path,
) -> Result[BaseResult, Problem]:
    plan = plan_delta(trees.base, trees.new)
    sizes = {f.path: f.size for f in trees.new.files()}
    encoder = _Encoder(roots[0], roots[1], base, out, scratch, sizes, tools)
    return Result.do(
        BaseResult(base=base, delta=delta)
        for _ in fs.write_text(encoder.empty, "")
        for encoded in encoder.encode_all(plan.jobs)
        for delta in base_delta(plan, encoded)
    )


def make_delta(
    base_root: Path,
    new_root: Path,
    base: ReleaseTag,
    out: Path,
    tools: HDiffPatch,
) -> Result[BaseResult, Problem]:
    """Writes `<out>/from/<base>/<path>.hdiff` for every planned job and
    `<out>/base-<base>.json`. Every patch is round-trip checked."""
    return Result.do(
        _logged(result, size_line(result, trees.new))
        for trees in Result.do(_Trees(b, n) for b in fs.scan_tree(base_root) for n in fs.scan_tree(new_root))
        for _ in fs.remove_paths(out / "from" / base)
        for result in fs.with_scratch(partial(_encode, (base_root, new_root), trees, base, out, tools))
        for _ in fs.write_text(base_result_file(out, base), result.model_dump_json(indent=2))
    )


def _logged(result: BaseResult, message: str) -> BaseResult:
    log(message)
    return result


def build_manifest(
    version: ReleaseTag,
    platform: Platform,
    new_root: Path,
    new_zip: Path,
    results: tuple[BaseResult, ...],
) -> Result[Manifest, Problem]:
    return Result.do(
        manifest
        for zip_ref in fs.zip_ref(new_zip)
        for files in fs.scan_tree(new_root)
        for manifest in assemble_manifest(version, platform, zip_ref, files, results)
    )


# --- replaying a patch set --------------------------------------------------------------


def _matches(manifest: Manifest, rebuilt: Tree, base: ReleaseTag) -> Result[None, Problem]:
    mismatches = compare_trees(manifest.files, rebuilt, compare_modes=not fs.IS_WINDOWS)
    if mismatches:
        details = "\n".join(f"  {m.kind}: {m.path}" for m in mismatches)
        return fail(f"rebuilt tree does not match the manifest for base '{base}':\n{details}")
    log(f"{base}: rebuilt tree matches the manifest")
    return Success(None)


def _replay(
    steps: tuple[Step, ...],
    base_root: Path,
    patches: Path,
    rebuild: Path,
    tools: HDiffPatch,
    scratch: Path,
) -> Result[None, Problem]:
    return fs.replay(steps, base_root, patches, rebuild, scratch, tools.patch)


def verify_delta(
    base_root: Path,
    patches: Path,
    manifest: Manifest,
    base: ReleaseTag,
    rebuild_dir: Path,
    tools: HDiffPatch,
) -> Result[Path, Problem]:
    """Replays the launcher onto a copy of the base in the empty `rebuild_dir` and proves the
    result matches `files`. Modes are not compared on Windows."""
    return Result.do(
        rebuild
        for steps in application_steps(manifest, base)
        for rebuild in fs.ensure_empty_dir(rebuild_dir)
        for _ in fs.with_scratch(partial(_replay, steps, base_root, patches, rebuild, tools))
        for rebuilt in fs.scan_tree(rebuild)
        for _ in _matches(manifest, rebuilt, base)
    )


def _same_signatures(
    platform: Platform, new_root: Path, rebuilt: Path, base: ReleaseTag, verify: Verifier
) -> Result[None, Problem]:
    return Result.do(
        signature_differences(new, again)
        for new in check_signatures(platform, new_root, verify)
        for again in check_signatures(platform, rebuilt, verify)
    ).bind(partial(_no_differences, base))


def _no_differences(base: ReleaseTag, differences: tuple[str, ...]) -> Result[None, Problem]:
    if differences:
        details = "\n".join(differences)
        return fail(f"{base}: signature verification of the rebuilt build differs from the new build:\n{details}")
    return Success(None)


@dataclass(frozen=True)
class BaseInputs:
    """Everything one base's patch set is made from."""

    version: ReleaseTag
    platform: Platform
    new_root: Path
    new_zip: Path
    base: ReleaseTag
    base_zip: Path
    out: Path


def _prove(inputs: BaseInputs, tools: HDiffPatch, verify: Verifier, work: Path) -> Result[BaseResult, Problem]:
    return Result.do(
        result
        for base_root in fs.extract_build(inputs.base_zip, work / "base")
        for result in make_delta(base_root, inputs.new_root, inputs.base, inputs.out, tools)
        for manifest in build_manifest(inputs.version, inputs.platform, inputs.new_root, inputs.new_zip, (result,))
        for rebuilt in verify_delta(base_root, inputs.out, manifest, inputs.base, work / "rebuild", tools)
        for _ in _same_signatures(inputs.platform, inputs.new_root, rebuilt, inputs.base, verify)
    )


def delta_for_base(inputs: BaseInputs, tools: HDiffPatch, verify: Verifier) -> Result[BaseResult, Problem]:
    """Extracts the base, makes its patch set, replays it and checks that the rebuilt build's
    signatures verify exactly like the new build's. On a Problem the base's output in `out`
    is removed."""
    return (
        fs.with_scratch(partial(_prove, inputs, tools, verify))
        .map(partial(_verified, inputs.base))
        .alt(partial(_discard, inputs))
    )


def _verified(base: ReleaseTag, result: BaseResult) -> BaseResult:
    log(f"{base}: patch set verified")
    return result


def _discard(inputs: BaseInputs, problem: Problem) -> Problem:
    """Removes the failed base's output; a failed removal is added to the Problem."""
    removed = fs.remove_paths(inputs.out / "from" / inputs.base, base_result_file(inputs.out, inputs.base))
    log(f"{inputs.base}: failed, its patch set was removed")
    unremoved = Problem(message=f"{problem.message}\n(its partial output could not be removed)")
    return removed.map(lambda _: problem).value_or(unremoved)
