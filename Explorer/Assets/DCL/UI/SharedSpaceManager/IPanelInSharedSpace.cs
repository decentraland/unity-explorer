
using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.UI.SharedSpaceManager
{
    public interface IPanelInSharedSpace
    {
        delegate void ViewShowingCompleteDelegate(IPanelInSharedSpace controller);

        /// <summary>
        /// Raised once the view has finished any preparation / animation and is ready, when the controller is shown.
        /// </summary>
        event ViewShowingCompleteDelegate ViewShowingComplete;

        bool IsVisibleInSharedSpace { get; }
        UniTask ShowInSharedSpaceAsync(CancellationToken ct, object parameters = null);
        UniTask HideInSharedSpaceAsync(CancellationToken ct);
    }
}
