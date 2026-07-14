using DCL.Multiplayer.Connections.DecentralandUrls;
using System;

namespace DCL.Browser
{
    public class SupportRequestService
    {
        private readonly IWebBrowser webBrowser;

        public event Action? SupportRequested;

        public SupportRequestService(IWebBrowser webBrowser)
        {
            this.webBrowser = webBrowser;
        }

        public void OpenSupport()
        {
            webBrowser.OpenUrl(DecentralandUrl.Help);
            SupportRequested?.Invoke();
        }
    }
}
