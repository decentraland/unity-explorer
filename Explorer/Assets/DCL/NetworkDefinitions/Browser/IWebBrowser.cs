using DCL.Multiplayer.Connections.DecentralandUrls;

// ReSharper disable once CheckNamespace
namespace DCL.Browser
{
    public interface IWebBrowser
    {
        void OpenUrl(string url);

        void OpenUrl(DecentralandUrl url);

        string GetUrl(DecentralandUrl url);
    }
}
