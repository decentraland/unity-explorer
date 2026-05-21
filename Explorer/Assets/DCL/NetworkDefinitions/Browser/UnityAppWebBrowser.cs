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
            Application.OpenURL(escaped);

#if ALTTESTER
            // Test-affordance: write the URL we just opened to a known path so
            // AltTester-driven cross-stack tests (in
            // `explorer-automation/web/tests/auth/specs/client-to-web-handoff.spec.ts`)
            // can pick it up without scraping the system browser. Compile-time
            // gated to ALTTESTER builds, so production binaries have zero
            // information-leak surface (the `#if` strips the entire block).
            //
            // Same pattern as `data-testid` annotations in DCL's dapp tests: a
            // small, explicit test affordance owned by production code,
            // active only in instrumented builds.
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
