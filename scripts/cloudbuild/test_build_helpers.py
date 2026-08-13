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
        # And the producer's own qualifying filter for API-returned links.
        self.assertTrue(url.startswith('https://') and '/builds/' in url)


class LinkInfoFileTest(EnvMixin, unittest.TestCase):
    def setUp(self):
        tmp = tempfile.TemporaryDirectory()
        self.addCleanup(tmp.cleanup)
        old_cwd = os.getcwd()
        self.addCleanup(os.chdir, old_cwd)
        os.chdir(tmp.name)
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

    def test_final_elapsed_clamps_negative(self):
        build.record_final_elapsed(7, -5, -1)
        info = self.read_info()
        self.assertEqual(info['QUEUE_SECS'], '0')
        self.assertEqual(info['BUILD_SECS'], '0')


if __name__ == '__main__':
    unittest.main()
