using DCL.Multiplayer.Connections.DecentralandUrls;
using System;

namespace DCL.Browser
{
    public interface IWebBrowser
    {
        void OpenUrl(Uri url);

        void OpenUrl(DecentralandUrl url);

        string GetUrl(DecentralandUrl url);
    }
}
