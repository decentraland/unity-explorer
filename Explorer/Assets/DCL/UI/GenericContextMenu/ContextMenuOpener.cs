using DCL.UI.GenericContextMenu.Opener;
using MVC;
using System.Threading;

namespace DCL.UI.GenericContextMenu
{
    public class ContextMenuOpener : IContextMenuOpener
    {
        private readonly IMVCManager mvcManager;

        public ContextMenuOpener(IMVCManager mvcManager)
        {
            this.mvcManager = mvcManager;
        }

        public void OpenContextMenu(GenericContextMenuParameter.GenericContextMenuParameter contextMenuParameter, CancellationToken ct) =>
            mvcManager.ShowAndForget(GenericContextMenuController.IssueCommand(contextMenuParameter), ct);
    }
}
