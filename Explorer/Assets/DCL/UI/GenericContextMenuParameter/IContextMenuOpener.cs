using System.Threading;

namespace DCL.UI
{
    public interface IContextMenuOpener
    {
        void OpenContextMenu(GenericContextMenuParameter contextMenuParameter, CancellationToken ct);
    }
}
