using DCL.Multiplayer.Connections.DecentralandUrls;
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DCL.Browser
{
    public class UnityAppWebBrowser : IWebBrowser
    {
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void OpenUrlInNewTab(string url);
#endif

        public UnityAppWebBrowser(IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        public void OpenUrl(string url)
        {
            var escaped = Uri.EscapeUriString(url);
#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[WebBrowser] OpenUrl (WebGL new tab): {escaped}");
            OpenUrlInNewTab(escaped);
            Debug.Log("[WebBrowser] OpenUrlInNewTab called");
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
