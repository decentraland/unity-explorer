"""Command line of the delta tool: argument parsing and output only. Each command takes
explicit arguments, prints its result on stdout, logs to stderr and exits non-zero with the
Problem when it fails."""

import argparse
import sys
from collections.abc import Callable, Sequence
from pathlib import Path

from pydantic import TypeAdapter
from returns.iterables import Fold
from returns.maybe import Maybe
from returns.pipeline import is_successful
from returns.result import Result, Success

from delta import pipeline
from delta.domain import (
    BaseResult,
    Manifest,
    Platform,
    Problem,
    PublishedRelease,
    ReleaseState,
    ReleaseTag,
    fail,
    parse_as,
    parse_json,
)
from delta.io import fs, github
from delta.io.hdiff import HDiffPatch, fetch_tools
from delta.io.signatures import check_signatures, system_verifier
from delta.transform import job_summary, pick_bases, prune_targets, published_explorer_releases

_TAG: TypeAdapter[str] = TypeAdapter(ReleaseTag)
_TAGS: TypeAdapter[tuple[str, ...]] = TypeAdapter(tuple[ReleaseTag, ...])
_PLATFORM: TypeAdapter[Platform] = TypeAdapter(Platform)

Output = Result[str, Problem]
"""What a command prints on stdout."""


def _tag(value: str) -> Result[ReleaseTag, Problem]:
    return parse_as(_TAG, value).alt(lambda _: Problem(message=f"not a release tag: {value!r}"))


def _tags(values: list[str]) -> Result[tuple[ReleaseTag, ...], Problem]:
    empty: Result[tuple[ReleaseTag, ...], Problem] = Success(())
    return Fold.collect([_tag(value) for value in values], empty)


def _count(value: int) -> Result[int, Problem]:
    return Success(value) if value >= 0 else fail(f"not a count: {value}")


def _platform(value: str) -> Result[Platform, Problem]:
    return parse_as(_PLATFORM, value).alt(lambda _: Problem(message=f"unknown platform: {value!r}"))


def _manifest_file(path: Path) -> Result[Manifest, Problem]:
    return fs.read_text(path).bind(lambda text: parse_json(Manifest, text))


def _base_result_file(path: Path) -> Result[BaseResult, Problem]:
    return fs.read_text(path).bind(lambda text: parse_json(BaseResult, text))


def _lines(values: tuple[str, ...]) -> str:
    return "".join(f"{value}\n" for value in values)


def _fetch_tools(args: argparse.Namespace) -> Output:
    return fetch_tools(args.tools_dir).map(str)


def _unpublished(tag: ReleaseTag, state: ReleaseState) -> Output:
    if state is ReleaseState.PUBLISHED:
        return fail(f"{tag} is published; its delta patches are immutable and are not (re)generated or uploaded.")
    pipeline.log(f"{tag}: {state.value}")
    return Success("")


def _check_unpublished(args: argparse.Namespace) -> Output:
    return Result.do(
        output
        for tag in _tag(args.tag)
        for repo in github.repository()
        for state in github.lookup_release(repo, tag)
        for output in _unpublished(tag, state)
    )


def _published() -> Result[tuple[PublishedRelease, ...], Problem]:
    return github.repository().bind(github.list_releases).bind(published_explorer_releases)


def _bases(args: argparse.Namespace) -> Output:
    return Result.do(
        _TAGS.dump_json(pick_bases(releases, count, current)).decode() + "\n"
        for count in _count(args.count)
        for current in _tag(args.current)
        for releases in _published()
    )


def _live(value: str | None) -> Result[Maybe[ReleaseTag], Problem]:
    return Maybe.from_optional(value).map(lambda tag: _tag(tag).map(Maybe.from_value)).value_or(Success(Maybe.empty))


def _prune(args: argparse.Namespace) -> Output:
    return Result.do(
        _lines(prune_targets(releases, keep, window, live))
        for keep in _count(args.keep)
        for window in _count(args.window)
        for live in _live(args.live)
        for releases in _published()
    )


def _extract(args: argparse.Namespace) -> Output:
    return fs.extract_build(args.zip, args.out_dir).map(lambda root: f"{root}\n")


def _tree(args: argparse.Namespace) -> Output:
    return fs.scan_tree(args.dir).map(lambda tree: tree.model_dump_json(indent=2) + "\n")


def _make_delta(args: argparse.Namespace) -> Output:
    return Result.do(
        ""
        for base in _tag(args.base_tag)
        for tools in HDiffPatch.from_path()
        for _ in pipeline.make_delta(args.base_dir, args.new_dir, base, args.out_dir, tools)
    )


def _manifest(args: argparse.Namespace) -> Output:
    empty: Result[tuple[BaseResult, ...], Problem] = Success(())
    return Result.do(
        manifest.model_dump_json(indent=2) + "\n"
        for version in _tag(args.version)
        for platform in _platform(args.platform)
        for results in Fold.collect([_base_result_file(path) for path in args.base_results], empty)
        for manifest in pipeline.build_manifest(version, platform, args.new_dir, args.zip, results)
    )


def _verify(args: argparse.Namespace) -> Output:
    return Result.do(
        f"{rebuild}\n"
        for base in _tag(args.base_tag)
        for manifest in _manifest_file(args.manifest)
        for tools in HDiffPatch.from_path()
        for rebuild in pipeline.verify_delta(args.base_dir, args.delta_dir, manifest, base, args.rebuild_dir, tools)
    )


def _signatures(args: argparse.Namespace) -> Output:
    return Result.do(
        _lines(tuple(f"{check.artifact} {'ok' if check.valid else 'invalid'}" for check in checks))
        for platform in _platform(args.platform)
        for verify in system_verifier(platform)
        for checks in check_signatures(platform, args.root, verify)
    )


def _delta_for_base(args: argparse.Namespace) -> Output:
    return Result.do(
        ""
        for version in _tag(args.version)
        for platform in _platform(args.platform)
        for base in _tag(args.base_tag)
        for tools in HDiffPatch.from_path()
        for verify in system_verifier(platform)
        for _ in pipeline.delta_for_base(
            pipeline.BaseInputs(version, platform, args.new_root, args.new_zip, base, args.base_zip, args.out_dir),
            tools,
            verify,
        )
    )


def _summary(args: argparse.Namespace) -> Output:
    return Result.do(
        job_summary(manifest, failed) for manifest in _manifest_file(args.manifest) for failed in _tags(args.failed)
    )


Handler = Callable[[argparse.Namespace], Output]


def _parser() -> argparse.ArgumentParser:  # noqa: PLR0915 (one block per command)
    parser = argparse.ArgumentParser(prog="run.py", description=__doc__)
    commands = parser.add_subparsers(required=True, metavar="command")
    platforms = [p.value for p in Platform]

    def command(name: str, handler: Handler, description: str) -> argparse.ArgumentParser:
        sub = commands.add_parser(name, help=description, description=description)
        sub.set_defaults(handler=handler)
        return sub

    sub = command("fetch-tools", _fetch_tools, "install the pinned HDiffPatch; prints the folder to add to PATH")
    sub.add_argument("tools_dir", type=Path)

    sub = command("check-unpublished", _check_unpublished, "exit 0 only for a release tag with no published release")
    sub.add_argument("tag")

    sub = command("bases", _bases, "JSON array of the <count> most recently published releases other than <current>")
    sub.add_argument("count", type=int)
    sub.add_argument("current")

    sub = command("prune", _prune, "releases whose delta/ folder is removed, one per line")
    sub.add_argument("keep", type=int)
    sub.add_argument("window", type=int)
    sub.add_argument("--live", help="never pruned")

    sub = command("extract", _extract, "extract a release zip; prints the build root")
    sub.add_argument("zip", type=Path)
    sub.add_argument("out_dir", type=Path)

    sub = command("tree", _tree, "the files[] JSON of a build folder")
    sub.add_argument("dir", type=Path)

    sub = command("make-delta", _make_delta, "write from/<base tag>/**.hdiff and base-<base tag>.json")
    sub.add_argument("base_dir", type=Path)
    sub.add_argument("new_dir", type=Path)
    sub.add_argument("base_tag")
    sub.add_argument("out_dir", type=Path)

    sub = command("manifest", _manifest, "the manifest JSON from base-*.json results")
    sub.add_argument("version")
    sub.add_argument("platform", choices=platforms)
    sub.add_argument("new_dir", type=Path)
    sub.add_argument("zip", type=Path)
    sub.add_argument("base_results", type=Path, nargs="*")

    sub = command("verify", _verify, "replay a base's patches and compare with files[]; prints the rebuild dir")
    sub.add_argument("base_dir", type=Path)
    sub.add_argument("delta_dir", type=Path)
    sub.add_argument("manifest", type=Path)
    sub.add_argument("base_tag")
    sub.add_argument("rebuild_dir", type=Path)

    sub = command("signatures", _signatures, "'<artifact> ok|invalid' per signed artifact")
    sub.add_argument("platform", choices=platforms)
    sub.add_argument("root", type=Path)

    sub = command("delta-for-base", _delta_for_base, "make, verify and signature-check one base's patch set")
    sub.add_argument("version")
    sub.add_argument("platform", choices=platforms)
    sub.add_argument("new_root", type=Path)
    sub.add_argument("new_zip", type=Path)
    sub.add_argument("base_tag")
    sub.add_argument("base_zip", type=Path)
    sub.add_argument("out_dir", type=Path)

    sub = command("summary", _summary, "markdown job summary of a manifest")
    sub.add_argument("manifest", type=Path)
    sub.add_argument("failed", nargs="*", help="bases left out after a failure")

    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    handler: Handler = args.handler
    outcome = handler(args)
    outcome.map(sys.stdout.write).alt(_report)
    return 0 if is_successful(outcome) else 1


def _report(problem: Problem) -> None:
    print(f"::error::{problem.message}", file=sys.stderr)  # noqa: T201
