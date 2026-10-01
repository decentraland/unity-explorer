"""Regression coverage using sanitized excerpts of Unity 6000.5 build logs."""
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parent))
from build_report import context_from_env, parse_log, render_summary

FIXTURES = Path(__file__).parent / 'fixtures'


def parse(text):
    return parse_log(text.splitlines())


class ShaderReportTest(unittest.TestCase):
    def test_multiline_cold_shader_counts_compilation_not_stripping(self):
        report = parse((FIXTURES / 'shader-cache-cold.txt').read_text())
        shaders = report['shaders']
        self.assertEqual(shaders['status'], 'observed')
        self.assertEqual(shaders['shader_markers'], 1)
        self.assertEqual(shaders['compiled_variants'], 45)
        self.assertEqual(shaders['unparsed_summaries'], 0)
        self.assertEqual(shaders['passes'][0]['pass'], 'ForwardLit')
        self.assertEqual(shaders['passes'][0]['type'], 'vp, metal')
        self.assertEqual(report['cache']['restores'][0]['status'], 'miss')
        # The repeated postbuild warning is not another restore attempt.
        self.assertEqual(len(report['cache']['restores']), 1)
        self.assertIsNone(report['cache']['restores'][0]['restore_seconds'])

    def test_warm_shader_reuses_local_cache_without_remote_hits(self):
        report = parse((FIXTURES / 'shader-cache-warm.txt').read_text())
        self.assertEqual(report['shaders']['compiled_variants'], 0)
        self.assertEqual(report['shaders']['local_cache_hits'], 106)
        self.assertEqual(report['shaders']['remote_cache_hits'], 0)
        restore = report['cache']['restores'][0]
        self.assertEqual(restore['status'], 'hit')
        self.assertEqual(restore['restore_seconds'], 710.118)
        self.assertEqual(restore['fetch_to_extraction_seconds'], 687.67)
        self.assertEqual(restore['extraction_seconds'], 22.448)
        archive = report['cache']['archives'][0]
        self.assertEqual(archive['compression'], 'none')
        self.assertEqual(archive['archive_seconds'], 27.773)
        self.assertEqual(archive['archive_to_postbuild_end_seconds'], 674.44)

    def test_legacy_header_and_grouped_counts(self):
        report = parse('''[2026-09-30T10:00:00Z - Unity] Compiling shader "Example" pass "Forward" (vp)
After scriptable stripping: 10000
finished in 1.25 seconds. Local cache hits 1,000 (0s), remote cache hits 20 (0s), compiled 3 variants (0s).
''')
        shaders = report['shaders']
        self.assertEqual(shaders['compiled_variants'], 3)
        self.assertEqual(shaders['local_cache_hits'], 1000)
        self.assertEqual(shaders['remote_cache_hits'], 20)
        self.assertEqual(shaders['passes'][0]['pass'], 'Forward')
        self.assertEqual(shaders['summed_pass_seconds'], 1.25)

    def test_unnamed_pass_with_negative_zero_duration(self):
        report = parse('Pass  (vp, metal) finished in -0.00 seconds. Local cache hits 0 (0s), remote cache hits 0 (0s), compiled 0 variants (0s), skipped 1 variants.')
        self.assertEqual(report['shaders']['status'], 'observed')
        self.assertEqual(report['shaders']['passes'][0]['pass'], '')
        self.assertEqual(report['shaders']['summed_pass_seconds'], 0)

    def test_unavailable_is_not_zero(self):
        for text in ('', 'Build failed before shader compilation'):
            with self.subTest(text=text):
                report = parse(text)
                self.assertEqual(report['shaders']['status'], 'unavailable')
                self.assertIsNone(report['shaders']['compiled_variants'])
                self.assertIsNone(report['shaders']['local_cache_hits'])
                self.assertIn('unknown', render_summary(report))

    def test_changed_log_format_is_visible_instead_of_false_zero(self):
        report = parse('Compiling shader "Example"\nLocal cache hits: NEW FORMAT')
        self.assertEqual(report['shaders']['status'], 'partial')
        self.assertEqual(report['shaders']['unparsed_summaries'], 1)
        self.assertIsNone(report['shaders']['compiled_variants'])
        self.assertTrue(report['warnings'])

    def test_partial_counts_are_marked(self):
        text = (FIXTURES / 'shader-cache-warm.txt').read_text()
        report = parse(text + '\nPass NewFormat: Local cache hits unknown')
        self.assertEqual(report['shaders']['status'], 'partial')
        self.assertEqual(report['shaders']['compiled_variants'], 0)
        self.assertEqual(report['shaders']['unparsed_summaries'], 1)


class CacheReportTest(unittest.TestCase):
    def test_incomplete_restore_and_archive_do_not_invent_timings(self):
        report = parse('''[2026-09-30T10:00:00Z] INFO: Fetching Cached library_example
[2026-09-30T10:05:00Z] INFO: Zipping cache files from Library using compression level low''')
        self.assertEqual(report['cache']['restores'][0]['status'], 'unknown')
        self.assertIsNone(report['cache']['restores'][0]['restore_seconds'])
        self.assertIsNone(report['cache']['archives'][0]['archive_seconds'])
        self.assertIsNone(report['cache']['archives'][0]['archive_to_postbuild_end_seconds'])
        self.assertTrue(report['warnings'])

    def test_hit_without_start_keeps_missing_durations_unknown(self):
        report = parse('[2026-09-30T10:00:00Z] INFO: library_example successfully fetched and unpacked from remote cache')
        self.assertEqual(report['cache']['restores'][0]['status'], 'hit')
        self.assertIsNone(report['cache']['restores'][0]['restore_seconds'])

    def test_multiple_restores_are_not_combined(self):
        report = parse('''[2026-09-30T10:00:00Z] INFO: Fetching Cached library_first
No Library cache found - ALL assets will be re-imported.
[2026-09-30T11:00:00Z] INFO: Fetching Cached workspace_second
[2026-09-30T11:02:00Z] INFO: workspace_second successfully fetched and unpacked from remote cache''')
        restores = report['cache']['restores']
        self.assertEqual([r['status'] for r in restores], ['miss', 'hit'])
        self.assertIsNone(restores[0]['restore_seconds'])
        self.assertEqual(restores[1]['restore_seconds'], 120)

    def test_negative_interval_is_unknown(self):
        report = parse('''[2026-09-30T11:00:00Z] INFO: Fetching Cached library_example
[2026-09-30T10:00:00Z] INFO: library_example successfully fetched and unpacked from remote cache''')
        self.assertIsNone(report['cache']['restores'][0]['restore_seconds'])

    def test_repeated_hit_preserves_original_restore_and_timings(self):
        report = parse('''[2026-09-30T10:00:00Z] INFO: Fetching Cached library_example
[2026-09-30T10:01:00Z] INFO: Extracting cache files to /example
[2026-09-30T10:02:00Z] INFO: library_example successfully fetched and unpacked from remote cache
[2026-09-30T10:03:00Z] INFO: library_example successfully fetched and unpacked from remote cache''')
        restores = report['cache']['restores']
        self.assertEqual(len(restores), 1)
        self.assertEqual(restores[0]['status'], 'hit')
        self.assertEqual(restores[0]['restore_seconds'], 120)
        self.assertEqual(restores[0]['extraction_seconds'], 60)

    def test_new_fetch_of_same_key_is_a_separate_restore(self):
        report = parse('''[2026-09-30T10:00:00Z] INFO: Fetching Cached library_example
[2026-09-30T10:02:00Z] INFO: library_example successfully fetched and unpacked from remote cache
[2026-09-30T11:00:00Z] INFO: Fetching Cached library_example
[2026-09-30T11:03:00Z] INFO: library_example successfully fetched and unpacked from remote cache''')
        restores = report['cache']['restores']
        self.assertEqual(len(restores), 2)
        self.assertEqual([r['restore_seconds'] for r in restores], [120, 180])

    def test_cache_markers_are_case_insensitive(self):
        report = parse('''[2026-09-30T10:00:00Z] INFO: FETCHING CACHED LIBRARY_example
[2026-09-30T10:02:00Z] INFO: library_example SUCCESSFULLY FETCHED AND UNPACKED FROM REMOTE CACHE
[2026-09-30T10:03:00Z] INFO: LIBRARY_example successfully fetched and unpacked from remote cache
[2026-09-30T11:00:00Z] INFO: fetching cached workspace_other
NO WORKSPACE CACHE FOUND''')
        restores = report['cache']['restores']
        self.assertEqual([r['status'] for r in restores], ['hit', 'miss'])
        self.assertEqual(restores[0]['restore_seconds'], 120)


class ReportSummaryTest(unittest.TestCase):
    def test_short_and_negative_durations_use_nonnegative_hms(self):
        for seconds, expected in ((27.773, '0:00:28'), (-0.01, '0:00:00'), (-0.0, '0:00:00')):
            with self.subTest(seconds=seconds):
                report = parse(f'Pass Forward (vp) finished in {seconds:.3f} seconds. Local cache hits 0 (0s), remote cache hits 0 (0s), compiled 1 variants (0s).')
                self.assertIn(f'| Forward / vp | 1 | {expected} |', render_summary(report))

    def test_shader_and_pass_names_render_as_literal_markdown(self):
        report = parse('''Compiling shader "[x](https://example.com) <tag>"
Pass [pass]*_`| (vp) finished in 1 seconds. Local cache hits 0 (0s), remote cache hits 0 (0s), compiled 1 variants (0s).''')
        summary = render_summary(report)
        self.assertIn(r'\[x\](https://example.com) \<tag\>', summary)
        self.assertIn(r'\[pass\]\*\_\`\| / vp', summary)


class ReportCommandTest(unittest.TestCase):
    def test_context_only_reads_allowlisted_fields(self):
        with tempfile.TemporaryDirectory() as directory:
            info = Path(directory) / 'build.env'
            info.write_text('BUILD_TARGET=macos-dev\nBUILD_ID=42\nSECRET=do-not-copy\n')
            context = context_from_env({'REPORT_CLEAN_BUILD': 'false', 'SECRET': 'do-not-copy'}, info)
            self.assertEqual(context['build_target'], 'macos-dev')
            self.assertEqual(context['build_id'], '42')
            self.assertFalse(context['requested_clean_build'])
            self.assertNotIn('do-not-copy', json.dumps(context))
            self.assertIsNone(context_from_env({}, None)['requested_clean_build'])

    def test_cli_writes_artifacts_and_appends_summary(self):
        with tempfile.TemporaryDirectory() as directory:
            dest = Path(directory)
            summary = dest / 'summary.md'
            summary.write_text('Existing summary\n')
            subprocess.run([
                sys.executable, str(Path(__file__).with_name('build_report.py')),
                '--input-log', str(FIXTURES / 'shader-cache-warm.txt'),
                '--output-report', str(dest / 'report.log'),
                '--output-json', str(dest / 'metrics.json'),
                '--build-info', str(dest / 'missing.env'),
            ], env={**os.environ, 'GITHUB_STEP_SUMMARY': str(summary)},
                check=True, capture_output=True, text=True)
            data = json.loads((dest / 'metrics.json').read_text())
            self.assertEqual(data['schema_version'], 1)
            self.assertEqual(data['cache']['restores'][0]['status'], 'hit')
            self.assertTrue(summary.read_text().startswith('Existing summary\n'))
            self.assertIn('0:11:50', summary.read_text())
            self.assertIn('0:00:28', summary.read_text())
            self.assertIn((dest / 'report.log').read_text(), summary.read_text())


if __name__ == '__main__':
    unittest.main()
