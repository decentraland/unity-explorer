
using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.UI.SharedSpaceManager
{
    public interface IPanelInSharedSpace
    {
        bool IsVisibleInSharedSpace { get; }
        UniTask ShowInSharedSpaceAsync(CancellationToken ct, object parameters = null);
        UniTask HideInSharedSpaceAsync(CancellationToken ct);
    }
}
