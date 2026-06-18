using DCL.Multiplayer.Connections.DecentralandUrls;
using Global.AppArgs;
using System;
using UnityEngine;
#if ALTTESTER
using DCL.Diagnostics;
using System.IO;
#endif

namespace DCL.Browser
{
    public class UnityAppWebBrowser : IWebBrowser
    {
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly IAppArgs? appArgs;

        public UnityAppWebBrowser(IDecentralandUrlsSource decentralandUrlsSource, IAppArgs? appArgs = null)
        {
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.appArgs = appArgs;
        }

        public void OpenUrl(string url)
        {
            var escaped = Uri.EscapeUriString(url);

#if ALTTESTER
            // Only skip the system browser when an AltTester cross-stack test is actually driving
            // this session (client launched with --alttester). Those tests read auth-url.txt and
            // navigate their own browser. Without the flag — Editor and dev/QA standalone builds,
            // which all carry the ALTTESTER compile define — we must still open the browser or
            // wallet login can never complete. The block is stripped from release builds entirely.
            if (appArgs?.HasFlag(AppArgsFlags.ALTTESTER) == true)
            {
                try
                {
                    var path = Path.Combine(Application.persistentDataPath, "auth-url.txt");
                    File.WriteAllText(path, escaped);
                }
                catch (Exception e)
                {
                    ReportHub.LogException(e, ReportCategory.AUTHENTICATION);
                }

                return;
            }
#endif
            Application.OpenURL(escaped);
        }

        public void OpenUrl(DecentralandUrl url)
        {
            OpenUrl(decentralandUrlsSource.Url(url));
        }

        public string GetUrl(DecentralandUrl url) =>
            decentralandUrlsSource.Url(url);
    }
}
