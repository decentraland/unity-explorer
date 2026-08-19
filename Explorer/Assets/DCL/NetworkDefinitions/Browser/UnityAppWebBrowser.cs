using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using Global.AppArgs;
using System;
using UnityEngine;
#if ALTTESTER
using System.IO;
#endif

// ReSharper disable once CheckNamespace
namespace DCL.Browser
{
    public class UnityAppWebBrowser
    {
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly IAppArgs? appArgs;

        public UnityAppWebBrowser(IDecentralandUrlsSource decentralandUrlsSource, IAppArgs? appArgs = null)
        {
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.appArgs = appArgs;
        }

        /// <summary>
        /// Opens a validated web URL in the system browser. Must be called from the main thread.
        /// Escaping uses <see cref="Uri.AbsoluteUri"/> rather than <c>Uri.EscapeUriString</c>: the latter
        /// percent-encodes '%', double-encoding URLs that already contain escaped sequences (e.g. a Stripe
        /// checkout fragment's %2F becomes %252F, breaking Stripe's atob() decode), whereas AbsoluteUri
        /// escapes only unescaped characters and preserves existing escapes.
        /// </summary>
        public virtual void OpenUrlMainThreadOnly(string url)
        {
            if (!ExternalUrlPolicy.IsWebScheme(url))
            {
                ReportHub.LogWarning(ReportCategory.UI, "Refused to open non-web URL scheme");
                return;
            }

            var escaped = Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ? uri.AbsoluteUri : url;

#if ALTTESTER
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

        public virtual void OpenUrlMainThreadOnly(DecentralandUrl url)
        {
            OpenUrlMainThreadOnly(decentralandUrlsSource.Url(url));
        }

        public string GetUrl(DecentralandUrl url) =>
            decentralandUrlsSource.Url(url);
    }
}
