using DCL.Multiplayer.Connections.DecentralandUrls;
using System;
using UnityEngine;
#if ALTTESTER
using System.IO;
#endif

namespace DCL.Browser
{
    public class UnityAppWebBrowser : IWebBrowser
    {
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        public UnityAppWebBrowser(IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        public void OpenUrl(string url)
        {
            var escaped = Uri.EscapeUriString(url);

#if ALTTESTER
            // Test-affordance: write the URL to a known path AND skip the
            // actual `Application.OpenURL` call. Cross-stack tests in
            // `explorer-automation/web/tests/auth/specs/client-to-web-handoff.spec.ts`
            // poll for the file, parse the URL, and drive a Playwright-
            // controlled browser to the same destination. Letting the system
            // browser open here would just pop a stale tab the test ignores
            // (and confuse the human watching the test run).
            //
            // Compile-time gated to ALTTESTER builds, so production binaries
            // retain the full `Application.OpenURL` behaviour with zero
            // information-leak surface — the entire block is stripped at
            // compile time when ALTTESTER isn't defined. Same pattern as
            // `data-testid` annotations in DCL's dapp tests: a small, explicit
            // test affordance owned by production code, active only in
            // instrumented builds.
            try
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Application Support", "DecentralandLauncherLight",
                    "auth-url.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, escaped);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"UnityAppWebBrowser: failed to write auth-url.txt for tests: {e}");
            }
#else
            Application.OpenURL(escaped);
#endif
        }

        public void OpenUrl(DecentralandUrl url)
        {
            OpenUrl(decentralandUrlsSource.Url(url));
        }

        public string GetUrl(DecentralandUrl url) =>
            decentralandUrlsSource.Url(url);
    }
}
