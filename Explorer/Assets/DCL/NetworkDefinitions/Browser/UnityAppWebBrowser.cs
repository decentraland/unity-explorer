using DCL.Multiplayer.Connections.DecentralandUrls;
using Global.AppArgs;
using System;
using UnityEngine;
#if ALTTESTER
using DCL.Diagnostics;
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

        public virtual void OpenUrlMainThreadOnly(string url)
        {
            var escaped = Uri.EscapeUriString(url);

#if ALTTESTER
            // Suppress system browser only when --alttester runtime flag is set; Editor/QA builds carry the ALTTESTER define but still need wallet login.
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
