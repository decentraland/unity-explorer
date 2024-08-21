using DCL.Multiplayer.Connections.DecentralandUrls;

namespace DCL.Browser
{
    public interface IWebBrowser
    {
        void OpenUrl(string url);

        void OpenUrl(DecentralandUrl url);
    }
}
