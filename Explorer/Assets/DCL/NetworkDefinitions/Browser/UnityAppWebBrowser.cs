using DCL.Multiplayer.Connections.DecentralandUrls;
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

        public UnityAppWebBrowser(IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        public void OpenUrl(string url)
        {
            var escaped = Uri.EscapeUriString(url);

#if ALTTESTER
            // ALTTESTER builds write auth URL to disk and suppress the system browser so Playwright tests can drive their own browser to it.
            try
            {
                var path = Path.Combine(Application.persistentDataPath, "auth-url.txt");
                File.WriteAllText(path, escaped);
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.AUTHENTICATION);
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
