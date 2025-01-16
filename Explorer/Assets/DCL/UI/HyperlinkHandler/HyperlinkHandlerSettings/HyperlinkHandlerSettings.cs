using MVC;

namespace DCL.UI.HyperlinkHandler
{
    public struct HyperlinkHandlerSettings
    {
        public readonly IMVCManager MvcManager;

        public HyperlinkHandlerSettings(IMVCManager mvcManager)
        {
            this.MvcManager = mvcManager;
        }
    }
}
