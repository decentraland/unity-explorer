"""Unit tests for build.py's pure helpers and the link-info file writer.

Run from anywhere: python3 -m unittest scripts.cloudbuild.test_build_helpers
(or `python3 -m unittest discover -s scripts/cloudbuild`). build.py's build
flow is under a __main__ guard, so importing it here executes nothing.
"""
import os
import re
import sys
import tempfile
import unittest
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build  # noqa: E402

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
UCB_LINKS_ACTION = os.path.join(REPO_ROOT, '.github', 'actions', 'ucb-build-links', 'action.yml')


class EnvMixin:
    def set_env(self, **pairs):
        for key, value in pairs.items():
            old = os.environ.get(key)
            self.addCleanup(
                (lambda k, v: (os.environ.__setitem__(k, v) if v is not None else os.environ.pop(k, None)))
                , key, old)
            if value is None:
                os.environ.pop(key, None)
            else:
                os.environ[key] = value


class PlatformKeyTest(EnvMixin, unittest.TestCase):
    def check(self, target, expected):
        self.set_env(TARGET=target)
        self.assertEqual(build._platform_key(), expected)

    def test_template_targets(self):
        self.check('t_windows64', 'windows64')
        self.check('t_macos', 'macos')

    def test_branch_derived_targets(self):
        self.check('windows64-feat-unity-cloud-build-link', 'windows64')
        self.check('macos-release-epic', 'macos')

    def test_unknown(self):
        self.check('linux64-foo', 'linux64-foo')
        self.check('', 'unknown')


class DashboardUrlTest(EnvMixin, unittest.TestCase):
    ENV = dict(ORG_ID='4673197905245',
               PROJECT_ID='8c12744f-9e98-47b8-b40c-576d04cb8d5c',
               TARGET='windows64-some-branch')

    def test_shape(self):
        self.set_env(**self.ENV)
        self.assertEqual(
            build._dashboard_build_url(15),
            'https://cloud.unity.com/home/organizations/4673197905245'
            '/projects/8c12744f-9e98-47b8-b40c-576d04cb8d5c'
            '/buildtargets/windows64-some-branch/builds/15'.replace(
                '/buildtargets', '/cloud-build/buildtargets'))

    def test_missing_env_returns_none(self):
        for absent in ('ORG_ID', 'PROJECT_ID', 'TARGET'):
            env = dict(self.ENV)
            env[absent] = None
            self.set_env(**env)
            self.assertIsNone(build._dashboard_build_url(15), f'{absent} unset')

    def test_matches_consumer_url_re(self):
        """Drift guard: the consumer drops URLs failing its allowlist silently,
        so the producer's constructed URL must always pass it."""
        with open(UCB_LINKS_ACTION) as f:
            match = re.search(r"URL_RE='([^']+)'", f.read())
        self.assertIsNotNone(match, 'URL_RE not found in ucb-build-links/action.yml')
        url_re = re.compile(match.group(1))
        self.set_env(**self.ENV)
        url = build._dashboard_build_url(42)
        self.assertRegex(url, url_re)
        # The producer's API-href filter must be the same rule verbatim, or a
        # link it persists could still be dropped downstream.
        self.assertEqual(build._DASHBOARD_LINK_RE.pattern, match.group(1))
        self.assertRegex(url, build._DASHBOARD_LINK_RE)


class LinkInfoFileTest(EnvMixin, unittest.TestCase):
    def setUp(self):
        tmp = tempfile.TemporaryDirectory()
        self.addCleanup(tmp.cleanup)
        old_cwd = os.getcwd()
        self.addCleanup(os.chdir, old_cwd)
        os.chdir(tmp.name)
        # Silence build.py's prints: its ::notice:: line is a live workflow
        # command when the test job itself runs on the Actions runner.
        silencer = mock.patch('builtins.print')
        silencer.start()
        self.addCleanup(silencer.stop)
        # PR_NUMBER unset keeps maybe_update_live_comment inert.
        self.set_env(TARGET='windows64-x', ORG_ID='org1', PROJECT_ID='proj1', PR_NUMBER=None)
        build.dashboard_url = None
        build._build_link_info_written = False
        build._final_elapsed = None
        self.addCleanup(self._reset_module_state)

    @staticmethod
    def _reset_module_state():
        build.dashboard_url = None
        build._build_link_info_written = False
        build._final_elapsed = None

    @staticmethod
    def read_info():
        with open(build.BUILD_LINK_INFO_PATH) as f:
            return dict(line.strip().split('=', 1) for line in f if '=' in line)

    def test_first_write_uses_constructed_url(self):
        build.record_build_link_info(7, {})
        info = self.read_info()
        self.assertEqual(info['BUILD_ID'], '7')
        self.assertEqual(info['DASHBOARD_URL'], build._dashboard_build_url(7))
        self.assertNotIn('QUEUE_SECS', info)

    def test_api_href_replaces_constructed_and_survives_final_rewrite(self):
        build.record_build_link_info(7, {})
        href = 'https://cloud.unity.com/some/deep/builds/7/link'
        build.record_build_link_info(7, {'links': {'dashboard_summary': {'href': href}}})
        self.assertEqual(self.read_info()['DASHBOARD_URL'], href)

        build.record_final_elapsed(7, 63, 3725)
        info = self.read_info()
        self.assertEqual(info['DASHBOARD_URL'], href, 'final rewrite must keep the API deep link')
        self.assertEqual(info['QUEUE_SECS'], '63')
        self.assertEqual(info['BUILD_SECS'], '3725')

    def test_non_build_link_rejected(self):
        build.record_build_link_info(7, {'links': {'dashboard_url': {'href': 'https://cloud.unity.com/'}}})
        self.assertEqual(self.read_info()['DASHBOARD_URL'], build._dashboard_build_url(7))

    def test_link_failing_consumer_allowlist_rejected(self):
        for href in ('https://example.com/deep/builds/7',          # non-dashboard host
                     'https://cloud.unity.com/deep/builds/none',   # no numeric build id
                     'https://cloud.unity.com/deep/builds/7?x=<'):  # char outside the allowlist
            build.record_build_link_info(7, {'links': {'dashboard_summary': {'href': href}}})
            self.assertEqual(self.read_info()['DASHBOARD_URL'], build._dashboard_build_url(7), href)

    def test_final_elapsed_clamps_negative(self):
        build.record_final_elapsed(7, -5, -1)
        info = self.read_info()
        self.assertEqual(info['QUEUE_SECS'], '0')
        self.assertEqual(info['BUILD_SECS'], '0')


class LiveCommentReconcileTest(EnvMixin, unittest.TestCase):
    def setUp(self):
        tmp = tempfile.TemporaryDirectory()
        self.addCleanup(tmp.cleanup)
        old_cwd = os.getcwd()
        self.addCleanup(os.chdir, old_cwd)
        os.chdir(tmp.name)
        # CI_STATUS_SCRIPT is cwd-relative; it must exist for the gate to pass.
        os.makedirs(os.path.dirname(build.CI_STATUS_SCRIPT))
        open(build.CI_STATUS_SCRIPT, 'w').close()
        self.set_env(PR_NUMBER='1', GH_TOKEN='token')
        self._reset_counters()
        self.addCleanup(self._reset_counters)
        self.addCleanup(setattr, build, 'upsert_live_comment', build.upsert_live_comment)

    @staticmethod
    def _reset_counters():
        build._live_comment_asserts = 0
        build._live_comment_last_attempt = 0.0
        build._live_comment_confirms = 0

    def stub_upsert(self, result):
        calls = []

        def fake(build_id, only_if_missing=False):
            calls.append(only_if_missing)
            if isinstance(result, Exception):
                raise result
            return result
        build.upsert_live_comment = fake
        return calls

    def test_reconcile_retries_after_failed_first_write(self):
        self.stub_upsert(RuntimeError('transient'))
        build.maybe_update_live_comment(7)  # swallowed; no row asserted
        self.assertEqual(build._live_comment_asserts, 0)

        calls = self.stub_upsert(True)
        build.maybe_update_live_comment(7, reconcile=True)
        self.assertEqual(calls, [], 'the 240s spacing must still hold')

        build._live_comment_last_attempt -= 241
        build.maybe_update_live_comment(7, reconcile=True)
        self.assertEqual(calls, [True], 'reconcile must retry the failed first write')
        self.assertEqual(build._live_comment_asserts, 1)

    def test_reconcile_caps_still_hold(self):
        calls = self.stub_upsert(True)
        build._live_comment_asserts = 3
        build.maybe_update_live_comment(7, reconcile=True)
        build._live_comment_asserts = 1
        build._live_comment_confirms = 3
        build.maybe_update_live_comment(7, reconcile=True)
        self.assertEqual(calls, [])


class UpsertLiveCommentRaceTest(EnvMixin, unittest.TestCase):
    """Pins the race mikhail-dcl flagged in review: upsert-ci-status.sh's own
    retry loop only confirms that *this* write's body landed, so a sibling
    platform's row arriving between upsert_live_comment's read and that write
    is invisible to it. upsert_live_comment must notice its composed union did
    not survive and retry against a fresh read instead of reporting success on
    a stale one."""

    WIN_ROW = '| Windows | Unity Cloud build 7 <!-- ucb-live:windows64 --> |'
    MAC_ROW = '| Mac | Unity Cloud build 9 <!-- ucb-live:macos --> |'

    def setUp(self):
        self.set_env(TARGET='windows64-x', GITHUB_REPOSITORY='org/repo', GITHUB_RUN_ID='1',
                     GITHUB_SERVER_URL='https://github.com', ORG_ID=None, PROJECT_ID=None)
        build.dashboard_url = None
        self.addCleanup(setattr, build, 'dashboard_url', None)

    @staticmethod
    def fake_run_capturing(bodies):
        def fake_run(cmd, env, timeout, check):
            with open(env['SECTION_BODY_FILE']) as f:
                bodies.append(f.read())
            return mock.Mock(returncode=0)
        return fake_run

    def test_sibling_row_landing_mid_write_is_recovered_on_retry(self):
        # attempt 1 pre-write read: no rows yet.
        # attempt 1 post-write verify: Mac's row raced in underneath us — the
        # union this attempt wrote (Windows only) is now stale. The pre-fix
        # code had no post-write read at all and would have reported success
        # here, permanently dropping Mac's row from the next real write.
        # attempt 2 pre-write read: fresh, carries Mac's row along.
        # attempt 2 post-write verify: both rows confirmed present.
        reads = ['', self.MAC_ROW, self.MAC_ROW, f'{self.MAC_ROW}\n{self.WIN_ROW}']
        bodies = []
        with mock.patch.object(build, '_build_section_of_status_comment', side_effect=reads), \
             mock.patch.object(build, '_own_job_url', return_value=None), \
             mock.patch.object(build.subprocess, 'run', side_effect=self.fake_run_capturing(bodies)):
            result = build.upsert_live_comment(7)

        self.assertTrue(result)
        self.assertEqual(len(bodies), 2, 'a stale-union write must be retried, not accepted')
        self.assertIn(self.WIN_ROW, bodies[0])
        self.assertNotIn(self.MAC_ROW, bodies[0], "attempt 1's read had no sibling row yet")
        self.assertIn(self.WIN_ROW, bodies[1])
        self.assertIn(self.MAC_ROW, bodies[1], "retry's union must carry the sibling row along")

    def test_gives_up_after_exhausting_attempts_instead_of_spinning(self):
        # Pathological: the post-write read never reflects this attempt's own
        # write (as if every attempt kept losing the race). Must terminate
        # after LIVE_COMMENT_WRITE_ATTEMPTS, not retry forever.
        reads = [''] * (2 * build.LIVE_COMMENT_WRITE_ATTEMPTS)
        bodies = []
        with mock.patch.object(build, '_build_section_of_status_comment', side_effect=reads), \
             mock.patch.object(build, '_own_job_url', return_value=None), \
             mock.patch.object(build.subprocess, 'run', side_effect=self.fake_run_capturing(bodies)):
            result = build.upsert_live_comment(7)

        self.assertIsNone(result)
        self.assertEqual(len(bodies), build.LIVE_COMMENT_WRITE_ATTEMPTS)

    def test_only_if_missing_short_circuits_on_first_fresh_read(self):
        with mock.patch.object(build, '_build_section_of_status_comment', return_value=self.WIN_ROW), \
             mock.patch.object(build, '_own_job_url', return_value=None), \
             mock.patch.object(build.subprocess, 'run') as run:
            result = build.upsert_live_comment(7, only_if_missing=True)

        self.assertFalse(result)
        run.assert_not_called()


if __name__ == '__main__':
    unittest.main()
