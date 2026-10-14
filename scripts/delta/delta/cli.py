"""Command line of the delta tool: argument parsing and output only. Each command takes
explicit arguments, writes results to stdout or the given folder, logs to stderr and exits
non-zero on failure."""

import argparse
import sys
from collections.abc import Callable, Sequence
from pathlib import Path

from pydantic import TypeAdapter, ValidationError

from delta import pipeline
from delta.domain import BaseResult, DeltaError, Manifest, Platform, ReleaseState, ReleaseTag
from delta.io import fs, github
from delta.io.hdiff import HDiffPatch, fetch_tools
from delta.io.signatures import check_signatures, system_verifier
from delta.transform import job_summary, pick_bases, prune_targets, published_explorer_releases

_TAG: TypeAdapter[str] = TypeAdapter(ReleaseTag)
_TAGS: TypeAdapter[tuple[str, ...]] = TypeAdapter(tuple[ReleaseTag, ...])


def _tag(value: str) -> ReleaseTag:
    try:
        return _TAG.validate_python(value)
    except ValidationError as error:
        msg = f"not a release tag: {value!r}"
        raise argparse.ArgumentTypeError(msg) from error


def _count(value: str) -> int:
    if not value.isdigit():
        msg = f"not a count: {value!r}"
        raise argparse.ArgumentTypeError(msg)
    return int(value)


def _out(text: str) -> None:
    sys.stdout.write(text if text.endswith("\n") else text + "\n")


def _fetch_tools(args: argparse.Namespace) -> None:
    _out(str(fetch_tools(args.tools_dir)))


def _check_unpublished(args: argparse.Namespace) -> None:
    state = github.lookup_release(github.repository(), args.tag)
    if state is ReleaseState.PUBLISHED:
        msg = f"{args.tag} is published; its delta patches are immutable and are not (re)generated or uploaded."
        raise DeltaError(msg)
    pipeline.log(f"{args.tag}: {state.value}")


def _bases(args: argparse.Namespace) -> None:
    releases = published_explorer_releases(github.list_releases(github.repository()))
    _out(_TAGS.dump_json(pick_bases(releases, args.count, args.current)).decode())


def _prune(args: argparse.Namespace) -> None:
    releases = published_explorer_releases(github.list_releases(github.repository()))
    for tag in prune_targets(releases, args.keep, args.window, args.live):
        _out(tag)


def _extract(args: argparse.Namespace) -> None:
    _out(str(fs.extract_build(args.zip, args.out_dir)))


def _tree(args: argparse.Namespace) -> None:
    _out(fs.scan_tree(args.dir).model_dump_json(indent=2))


def _make_delta(args: argparse.Namespace) -> None:
    pipeline.make_delta(args.base_dir, args.new_dir, args.base_tag, args.out_dir, HDiffPatch.from_path())


def _manifest(args: argparse.Namespace) -> None:
    results = tuple(BaseResult.model_validate_json(path.read_text(encoding="utf-8")) for path in args.base_results)
    _out(
        pipeline.build_manifest(args.version, args.platform, args.new_dir, args.zip, results).model_dump_json(indent=2)
    )


def _verify(args: argparse.Namespace) -> None:
    manifest = Manifest.model_validate_json(args.manifest.read_text(encoding="utf-8"))
    tools = HDiffPatch.from_path()
    _out(str(pipeline.verify_delta(args.base_dir, args.delta_dir, manifest, args.base_tag, args.rebuild_dir, tools)))


def _signatures(args: argparse.Namespace) -> None:
    for check in check_signatures(args.platform, args.root, system_verifier(args.platform)):
        _out(f"{check.artifact} {'ok' if check.valid else 'invalid'}")


def _delta_for_base(args: argparse.Namespace) -> None:
    pipeline.delta_for_base(
        args.version,
        args.platform,
        args.new_root,
        args.new_zip,
        args.base_tag,
        args.base_zip,
        args.out_dir,
        HDiffPatch.from_path(),
        system_verifier(args.platform),
    )


def _summary(args: argparse.Namespace) -> None:
    manifest = Manifest.model_validate_json(args.manifest.read_text(encoding="utf-8"))
    _out(job_summary(manifest, tuple(args.failed)))


def _parser() -> argparse.ArgumentParser:  # noqa: PLR0915 (one block per command)
    parser = argparse.ArgumentParser(prog="run.py", description=__doc__)
    commands = parser.add_subparsers(required=True, metavar="command")

    def command(name: str, handler: Callable[[argparse.Namespace], None], description: str) -> argparse.ArgumentParser:
        sub = commands.add_parser(name, help=description, description=description)
        sub.set_defaults(handler=handler)
        return sub

    sub = command("fetch-tools", _fetch_tools, "install the pinned HDiffPatch; prints the folder to add to PATH")
    sub.add_argument("tools_dir", type=Path)

    sub = command("check-unpublished", _check_unpublished, "exit 0 only for a release tag with no published release")
    sub.add_argument("tag", type=_tag)

    sub = command("bases", _bases, "JSON array of the <count> most recently published releases other than <current>")
    sub.add_argument("count", type=_count)
    sub.add_argument("current", type=_tag)

    sub = command("prune", _prune, "releases whose delta/ folder is removed, one per line")
    sub.add_argument("keep", type=_count)
    sub.add_argument("window", type=_count)
    sub.add_argument("--live", type=_tag, help="never pruned")

    sub = command("extract", _extract, "extract a release zip; prints the build root")
    sub.add_argument("zip", type=Path)
    sub.add_argument("out_dir", type=Path)

    sub = command("tree", _tree, "the files[] JSON of a build folder")
    sub.add_argument("dir", type=Path)

    sub = command("make-delta", _make_delta, "write from/<base tag>/**.hdiff and base-<base tag>.json")
    sub.add_argument("base_dir", type=Path)
    sub.add_argument("new_dir", type=Path)
    sub.add_argument("base_tag", type=_tag)
    sub.add_argument("out_dir", type=Path)

    sub = command("manifest", _manifest, "the manifest JSON from base-*.json results")
    sub.add_argument("version", type=_tag)
    sub.add_argument("platform", type=Platform)
    sub.add_argument("new_dir", type=Path)
    sub.add_argument("zip", type=Path)
    sub.add_argument("base_results", type=Path, nargs="*")

    sub = command("verify", _verify, "replay a base's patches and compare with files[]; prints the rebuild dir")
    sub.add_argument("base_dir", type=Path)
    sub.add_argument("delta_dir", type=Path)
    sub.add_argument("manifest", type=Path)
    sub.add_argument("base_tag", type=_tag)
    sub.add_argument("rebuild_dir", type=Path)

    sub = command("signatures", _signatures, "'<artifact> ok|invalid' per signed artifact")
    sub.add_argument("platform", type=Platform)
    sub.add_argument("root", type=Path)

    sub = command("delta-for-base", _delta_for_base, "make, verify and signature-check one base's patch set")
    sub.add_argument("version", type=_tag)
    sub.add_argument("platform", type=Platform)
    sub.add_argument("new_root", type=Path)
    sub.add_argument("new_zip", type=Path)
    sub.add_argument("base_tag", type=_tag)
    sub.add_argument("base_zip", type=Path)
    sub.add_argument("out_dir", type=Path)

    sub = command("summary", _summary, "markdown job summary of a manifest")
    sub.add_argument("manifest", type=Path)
    sub.add_argument("failed", type=_tag, nargs="*", help="bases left out after a failure")

    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    try:
        args.handler(args)
    except (DeltaError, ValidationError, OSError) as error:
        print(f"::error::{error}", file=sys.stderr)  # noqa: T201
        return 1
    return 0
