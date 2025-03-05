
using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;

namespace DCL.UI
{
    public interface IControllerInSharedArea
    {
        UniTask<bool> HidingRequestedAsync(object? parameters, CancellationToken token);

        ControllerState State { get; }
    }
}
