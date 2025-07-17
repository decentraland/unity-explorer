using System.Threading;
using Cysharp.Threading.Tasks;
using MVC;

namespace DCL.Chat.Services
{
    public interface IContextMenuRequest
    {
    }

    public interface IContextMenuService
    {
        UniTask ShowMenuAsync<TRequest>(TRequest request, CancellationToken ct) where TRequest : IContextMenuRequest;
    }

    public class ChatContextMenuService : IContextMenuService
    {
        private readonly IMVCManager mvcManager;

        public ChatContextMenuService(IMVCManager mvcManager)
        {
            this.mvcManager = mvcManager;
        }

        // The method signature stays the same, providing a clean interface.
        public async UniTask ShowMenuAsync<TRequest>(TRequest request, CancellationToken ct) where TRequest : IContextMenuRequest
        {
            // Create the input data for our proxy controller
            var proxyInput = new ContextMenuProxyInput
            {
                RealRequest = request
            };

            // Create the command to show our proxy controller
            var command = new ShowCommand<ContextMenuProxyView, ContextMenuProxyInput>(proxyInput);

            // Tell the MVCManager to show our proxy controller as a popup.
            // The MVCManager will now handle the blocker, input, and stacking for us!
            await mvcManager.ShowAsync(command, ct);
        }
    }
}