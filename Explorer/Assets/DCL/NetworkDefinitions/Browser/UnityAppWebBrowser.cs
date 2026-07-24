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
            // Uri.EscapeUriString percent-encodes '%', double-encoding URLs that already contain escaped
            // sequences (e.g. a Stripe checkout fragment's %2F becomes %252F), which breaks Stripe's atob()
            // decode. AbsoluteUri escapes only unescaped characters and preserves existing escapes.
            Application.OpenURL(Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ? uri.AbsoluteUri : url);
        }

        public virtual void OpenUrlMainThreadOnly(DecentralandUrl url)
        {
            OpenUrlMainThreadOnly(decentralandUrlsSource.Url(url));
        }

        public string GetUrl(DecentralandUrl url) =>
            decentralandUrlsSource.Url(url);
    }
}
