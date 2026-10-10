using MVC;
using System.Threading;

namespace DCL.UI
{
    public class ContextMenuOpener : IContextMenuOpener
    {
        private readonly IMVCManager mvcManager;

        public ContextMenuOpener(IMVCManager mvcManager)
        {
            this.mvcManager = mvcManager;
        }

        public void OpenContextMenu(GenericContextMenuParameter contextMenuParameter, CancellationToken ct) =>
            mvcManager.ShowAndForget(GenericContextMenuController.IssueCommand(contextMenuParameter), ct);
    }
}
