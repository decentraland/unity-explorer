using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using System;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace DCL.Browser
{
    public class UnityAppWebBrowser
    {
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        public UnityAppWebBrowser(IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        public virtual void OpenUrlMainThreadOnly(string url)
        {
            if (!ExternalUrlPolicy.IsWebScheme(url))
            {
                ReportHub.LogWarning(ReportCategory.UI, "Refused to open non-web URL scheme");
                return;
            }

            Application.OpenURL(Uri.EscapeUriString(url));
        }

        public virtual void OpenUrlMainThreadOnly(DecentralandUrl url)
        {
            OpenUrlMainThreadOnly(decentralandUrlsSource.Url(url));
        }

        public string GetUrl(DecentralandUrl url) =>
            decentralandUrlsSource.Url(url);
    }
}
