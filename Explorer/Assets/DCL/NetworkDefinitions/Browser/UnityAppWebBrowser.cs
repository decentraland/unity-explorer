using DCL.Multiplayer.Connections.DecentralandUrls;
using System;
using UnityEngine;

namespace DCL.Browser
{
    public class UnityAppWebBrowser
    {
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        public UnityAppWebBrowser(IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        public void OpenUrlMainThreadOnly(string url)
        {
            Application.OpenURL(Uri.EscapeUriString(url));
        }

        public void OpenUrlMainThreadOnly(DecentralandUrl url)
        {
            OpenUrlMainThreadOnly(decentralandUrlsSource.Url(url));
        }

        public string GetUrl(DecentralandUrl url) =>
            decentralandUrlsSource.Url(url);
    }
}
