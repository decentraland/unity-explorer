"""Report observed shader work and cache timings from a Unity Cloud Build log."""
import argparse
import datetime
import json
import os
from pathlib import Path
import re


TIMESTAMP = re.compile(r'^\[(\d{4}-\d\d-\d\dT[\d:.]+(?:Z|[+-]\d\d:\d\d))')
SHADER = re.compile(r'Compiling shader "([^"]+)"')
PASS = re.compile(r'\b[Pp]ass "?(.*?)"? \(([^()]+)\)(?= finished|\s*$)')
COMPILATION = re.compile(
    r'finished in (-?\d+(?:\.\d+)?) seconds\. Local cache hits (\d+(?:,\d{3})*)\b.*?'
    r'remote cache hits (\d+(?:,\d{3})*)\b.*?compiled (\d+(?:,\d{3})*) variants\b')
FETCH = re.compile(r'Fetching Cached ((library|workspace)[\w.-]*)', re.IGNORECASE)
RESTORED = re.compile(r'((library|workspace)[\w.-]*) successfully fetched and unpacked from remote cache', re.IGNORECASE)
MISSING_CACHE = re.compile(r'No (Library|Workspace) cache found', re.IGNORECASE)
ARCHIVE = re.compile(r'Zipping cache files from (\w+) using compression level (\w+)')
SLOWEST_PASSES_SHOWN = 5


def timestamp(line):
    match = TIMESTAMP.search(line)
    if not match:
        return None
    try:
        return datetime.datetime.fromisoformat(match[1].replace('Z', '+00:00')).isoformat()
    except ValueError:
        return None


def elapsed(start, end):
    if start is None or end is None:
        return None
    seconds = (datetime.datetime.fromisoformat(end) - datetime.datetime.fromisoformat(start)).total_seconds()
    return round(seconds, 3) if seconds >= 0 else None


def new_restore(kind, key=None, started_at=None):
    return {'kind': kind, 'key': key, 'status': 'unknown', 'started_at': started_at,
            'extraction_started_at': None, 'finished_at': None}


def parse_log(lines):
    passes, restores, archives = [], [], []
    shader = legacy_pass = None
    shader_markers = unparsed_summaries = 0
    latest_by_kind = {}
    archive = None
    for line in lines:
        at = timestamp(line)
        match = SHADER.search(line)
        if match:
            shader_markers += 1
            shader = match[1]
            legacy_pass = PASS.search(line[match.end():])
        match = COMPILATION.search(line)
        if 'Local cache hits' in line and not match:
            unparsed_summaries += 1
        if match:
            pass_match = PASS.search(line) or legacy_pass
            passes.append({
                'shader': shader,
                'pass': pass_match[1] if pass_match else None,
                'type': pass_match[2] if pass_match else None,
                'finished_at': at,
                'duration_seconds': float(match[1]),
                'local_cache_hits': int(match[2].replace(',', '')),
                'remote_cache_hits': int(match[3].replace(',', '')),
                'compiled_variants': int(match[4].replace(',', '')),
            })
        match = FETCH.search(line)
        if match:
            kind = match[2].casefold()
            restore = new_restore(kind, match[1], at)
            restores.append(restore)
            latest_by_kind[kind] = restore
        match = MISSING_CACHE.search(line)
        if match:
            # Repeated postbuild warnings belong to the latest attempt of that kind.
            kind = match[1].casefold()
            restore = latest_by_kind.get(kind)
            if restore is None:
                restore = new_restore(kind)
                restores.append(restore)
                latest_by_kind[kind] = restore
            if restore['status'] == 'unknown':
                restore['status'] = 'miss'
        if 'Extracting cache files to' in line:
            # Extraction lines have no cache key; only a single pending attempt is unambiguous.
            pending = [item for item in latest_by_kind.values() if item['status'] == 'unknown']
            if len(pending) == 1 and pending[0]['extraction_started_at'] is None:
                pending[0]['extraction_started_at'] = at
        match = RESTORED.search(line)
        if match:
            kind = match[2].casefold()
            restore = latest_by_kind.get(kind)
            same_key = restore is not None and (restore['key'] or '').casefold() == match[1].casefold()
            if not same_key or restore['status'] == 'miss':
                restore = new_restore(kind, match[1])
                restores.append(restore)
                latest_by_kind[kind] = restore
            if restore['status'] != 'hit':
                restore.update(status='hit', finished_at=at)
        match = ARCHIVE.search(line)
        if match:
            archive = {'source': match[1], 'compression': match[2], 'started_at': at,
                       'finished_at': None, 'postbuild_finished_at': None}
            archives.append(archive)
        if 'Created the archive file in' in line and archive and archive['finished_at'] is None:
            archive['finished_at'] = at
        if 'postbuildsteps finished successfully' in line:
            for item in archives:
                if item['postbuild_finished_at'] is None:
                    item['postbuild_finished_at'] = at

    for item in restores:
        item['restore_seconds'] = elapsed(item['started_at'], item['finished_at']) if item['status'] == 'hit' else None
        item['fetch_to_extraction_seconds'] = elapsed(item['started_at'], item['extraction_started_at'])
        item['extraction_seconds'] = elapsed(item['extraction_started_at'], item['finished_at'])
    for item in archives:
        item['archive_seconds'] = elapsed(item['started_at'], item['finished_at'])
        item['archive_to_postbuild_end_seconds'] = elapsed(item['finished_at'], item['postbuild_finished_at'])

    warnings = []
    if unparsed_summaries or (shader_markers and not passes):
        warnings.append('Shader compilation markers could not be fully parsed; totals cover recognized summaries only.')
    status = 'partial' if warnings else ('observed' if passes else 'unavailable')
    if not passes:
        warnings.append('No recognized shader compilation summaries; counts and durations are unknown, not zero.')
    if any(item['status'] == 'unknown' for item in restores):
        warnings.append('Cache restore started without a recognized hit or miss; restore outcome is unknown.')
    if any(item['archive_seconds'] is None or item['archive_to_postbuild_end_seconds'] is None for item in archives):
        warnings.append('Cache archive or postbuild completion was not observed; missing durations are unknown.')
    totals = {key: sum(item[key] for item in passes) if passes else None
              for key in ('compiled_variants', 'local_cache_hits', 'remote_cache_hits')}
    return {
        'schema_version': 1,
        'shaders': {
            'status': status,
            'shader_markers': shader_markers,
            'unparsed_summaries': unparsed_summaries,
            'summaries_parsed': len(passes),
            **totals,
            # Pass timings can overlap; this sum is not wall-clock build time.
            'summed_pass_seconds': round(sum(item['duration_seconds'] for item in passes), 3) if passes else None,
            'passes': passes,
        },
        'cache': {'restores': restores, 'archives': archives},
        'warnings': warnings,
    }


def context_from_env(env, build_info):
    context = {key: env.get(var) or None for key, var in (
        ('repository', 'GITHUB_REPOSITORY'), ('run_id', 'GITHUB_RUN_ID'),
        ('run_attempt', 'GITHUB_RUN_ATTEMPT'), ('commit_sha', 'REPORT_COMMIT_SHA'),
        ('platform', 'REPORT_PLATFORM'), ('install_source', 'REPORT_INSTALL_SOURCE'),
        ('requested_cache_strategy', 'REPORT_CACHE_STRATEGY'))}
    clean = env.get('REPORT_CLEAN_BUILD', '').lower()
    context['requested_clean_build'] = {'true': True, 'false': False}.get(clean)
    context.update(build_target=None, build_id=None)
    if build_info and build_info.is_file():
        for line in build_info.read_text(encoding='utf-8').splitlines():
            key, _, value = line.partition('=')
            if key in ('BUILD_TARGET', 'BUILD_ID'):
                context[key.lower()] = value
    return context


def markdown_cell(text):
    return re.sub(r'([\\`*_\[\]<>|])', r'\\\1', text)


def render_summary(report):
    def count(value):
        return f'{value:,}' if value is not None else 'unknown'

    def duration(value):
        if value is None:
            return 'unknown'
        hours, remainder = divmod(round(max(value, 0)), 3600)
        return f'{hours}:{remainder // 60:02}:{remainder % 60:02}'

    shaders = report['shaders']
    lines = ['### Unity shader and cache report', '',
             f"Shader summary coverage: {shaders['status']} ({shaders['summaries_parsed']} summaries).",
             '', '| Shader metric | Observed value |', '|---|---:|',
             f"| Variants actually compiled | {count(shaders['compiled_variants'])} |",
             f"| Local cache hits | {count(shaders['local_cache_hits'])} |",
             f"| Remote cache hits | {count(shaders['remote_cache_hits'])} |",
             f"| Sum of pass durations (can overlap) | {duration(shaders['summed_pass_seconds'])} |", '',
             'Local shader hits can come from a restored Library cache. Remote hits are a separate shader cache metric.', '',
             '| Cache restore | Result | Total | Before extraction | Extraction |', '|---|---|---:|---:|---:|']
    for item in report['cache']['restores']:
        lines.append(f"| {item['key'] or item['kind']} | {item['status']} | {duration(item['restore_seconds'])} | "
                     f"{duration(item['fetch_to_extraction_seconds'])} | {duration(item['extraction_seconds'])} |")
    if not report['cache']['restores']:
        lines.append('| No recognized restore markers | unknown | unknown | unknown | unknown |')
    lines += ['', '| Cache archive | Compression | Creation | Archive → postbuild end |', '|---|---|---:|---:|']
    for item in report['cache']['archives']:
        lines.append(f"| {item['source']} | {item['compression']} | {duration(item['archive_seconds'])} | "
                     f"{duration(item['archive_to_postbuild_end_seconds'])} |")
    if not report['cache']['archives']:
        lines.append('| No recognized archive markers | unknown | unknown | unknown |')
    lines += ['', 'Archive → postbuild end includes transfer and other finalization work; it is not a measured upload duration.',
              'Missing timings remain unknown. JSON stores seconds and individual shader-pass observations.', '']
    if shaders['passes']:
        lines += ['| Slowest observed shader passes | Pass / type | Compiled variants | Duration |',
                  '|---|---|---:|---:|']
        for item in sorted(shaders['passes'], key=lambda p: p['duration_seconds'], reverse=True)[:SLOWEST_PASSES_SHOWN]:
            name = markdown_cell(item['shader'] or 'Unattributed (no shader header)')
            pass_name = markdown_cell(f"{item['pass'] or '(unnamed)'} / {item['type'] or 'unknown'}")
            lines.append(f"| {name} | {pass_name} | {count(item['compiled_variants'])} | {duration(item['duration_seconds'])} |")
        lines.append('')
    lines.extend(f'- Warning: {warning}' for warning in report['warnings'])
    return '\n'.join(lines) + '\n'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input-log', type=Path, required=True)
    parser.add_argument('--output-report', type=Path, required=True)
    parser.add_argument('--output-json', type=Path, required=True)
    parser.add_argument('--build-info', type=Path)
    args = parser.parse_args()
    with args.input_log.open(encoding='utf-8-sig', errors='replace') as lines:
        report = parse_log(lines)
    report['context'] = context_from_env(os.environ, args.build_info)
    summary = render_summary(report)
    args.output_report.write_text(summary, encoding='utf-8')
    args.output_json.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(summary)
    for warning in report['warnings']:
        print(f'::warning::{warning}')
    summary_path = os.environ.get('GITHUB_STEP_SUMMARY')
    if summary_path:
        with open(summary_path, 'a', encoding='utf-8') as output:
            output.write(summary)


if __name__ == '__main__':
    main()
