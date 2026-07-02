using DCL.Multiplayer.Connections.DecentralandUrls;
using System;

namespace DCL.Browser
{
    public class SupportRequestService
    {
        private readonly UnityAppWebBrowser webBrowser;

        public event Action? SupportRequested;

        public SupportRequestService(UnityAppWebBrowser webBrowser)
        {
            this.webBrowser = webBrowser;
        }

        public void OpenSupport()
        {
            webBrowser.OpenUrlMainThreadOnly(DecentralandUrl.Help);
            SupportRequested?.Invoke();
        }
    }
}
