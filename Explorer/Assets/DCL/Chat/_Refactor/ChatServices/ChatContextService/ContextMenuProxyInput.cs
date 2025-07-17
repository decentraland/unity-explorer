using System.Threading;
using Cysharp.Threading.Tasks;
using MVC;

namespace DCL.Chat.Services
{
    public struct ContextMenuProxyInput
    {
        public IContextMenuRequest RealRequest;
    }

    public class ContextMenuProxyView : ViewBase, IView
    {
    }

    public class ContextMenuProxyController : ControllerBase<ContextMenuProxyView, ContextMenuProxyInput>
    {
        private readonly IMVCManagerMenusAccessFacade mvcFacade;

        public ContextMenuProxyController(ViewFactoryMethod viewFactory, IMVCManagerMenusAccessFacade mvcFacade) : base(viewFactory)
        {
            this.mvcFacade = mvcFacade;
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            // This is the core logic. When our controller is shown, this method runs.
            var closeCompletionSource = new UniTaskCompletionSource();
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            try
            {
                switch (inputData.RealRequest)
                {
                    case UserProfileMenuRequest userReq:
                        await mvcFacade.ShowUserProfileContextMenuFromWalletIdAsync(
                            userReq.WalletAddress, userReq.Position, userReq.Offset,
                            linkedCts.Token, closeCompletionSource.Task,
                            onHide: () => closeCompletionSource.TrySetResult(),
                            userReq.AnchorPoint
                        );
                        break;

                    case ChatContextMenuRequest chatReq:

                        mvcFacade.ShowChatContextMenuAsync(
                            chatReq.Position,
                            chatReq.contextMenuData,
                            chatReq.OnDeleteHistory,
                            () => closeCompletionSource.TrySetResult(),
                            closeMenuTask: closeCompletionSource.Task);

                        break;
                }

                await closeCompletionSource.Task;
            }
            finally
            {
                // Ensure we clean up if the operation is cancelled from the outside.
                linkedCts.Cancel();
            }
        }
    }
}