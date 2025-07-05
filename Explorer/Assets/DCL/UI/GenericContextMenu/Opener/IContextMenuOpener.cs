using System.Threading;

namespace DCL.UI.GenericContextMenu.Opener
{
    public interface IContextMenuOpener
    {
        void OpenContextMenu(GenericContextMenuParameter.GenericContextMenuParameter contextMenuParameter, CancellationToken ct);
    }
}
