using DCL.Input;
using MVC;

namespace DCL.UI.HyperlinkHandler
{
    public struct HyperlinkHandlerDependencies
    {
        public readonly IMVCManager MvcManager;
        public readonly ICursor Cursor;

        public HyperlinkHandlerDependencies(IMVCManager mvcManager, ICursor cursor)
        {
            MvcManager = mvcManager;
            Cursor = cursor;
        }
    }
}
