using DCL.Multiplayer.Connections.DecentralandUrls;
using System;
using UnityEngine;

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
            Application.OpenURL(Uri.EscapeUriString(url));
        }

        public void OpenUrl(DecentralandUrl url)
        {
            OpenUrl(decentralandUrlsSource.Url(url));
        }
    }
}
