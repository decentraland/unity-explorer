using DCL.UI;
using MVC;

namespace DCL.Chat.Services
{
    using Cysharp.Threading.Tasks;
    using System.Threading;

    public interface IContextMenuRequest
    {
    }

    public interface IContextMenuService
    {
        // Shows a modal context menu and awaits its closure.
        UniTask ShowMenuAsync<TRequest>(TRequest request, CancellationToken ct) where TRequest : IContextMenuRequest;
    }

    public class ChatContextMenuService : IContextMenuService
    {
        private readonly IMVCManagerMenusAccessFacade mvcFacade;
        private readonly ChatInputBlockingService inputBlocker;
        private readonly ChatClickDetectionService clickDetectionService;


        public ChatContextMenuService(IMVCManagerMenusAccessFacade mvcFacade,
            ChatInputBlockingService inputBlocker,
            ChatClickDetectionService clickDetectionService)
        {
            this.mvcFacade = mvcFacade;
            this.inputBlocker = inputBlocker;
            this.clickDetectionService = clickDetectionService;
        }

        /// <summary>
        /// How to use this service:
        /// Gather Data: The view should have methods to provide its own data.
        /// Vector2 menuPosition = sourceEntryView.GetContextMenuAnchorPosition();
        /// string messageText = sourceEntryView.GetMessageText();
        /// 
        /// // Create Request
        /// var request = new ChatMessageMenuRequest
        /// {
        ///     Position = menuPosition,
        ///     MessageText = messageText,
        ///     AnchorPoint = MenuAnchorPoint.TOP_RIGHT // Or determined by the view
        /// };
        /// 
        /// // Call Service and Forget
        /// // The presenter's job is done. It has delegated the complex task of showing
        /// // a modal menu to the specialized service.
        /// contextMenuService.ShowMenuAsync(request, lifeCts.Token).Forget();
        /// </summary>
        /// <param name="request"></param>
        /// <param name="ct"></param>
        /// <typeparam name="TRequest"></typeparam>
        public async UniTask ShowMenuAsync<TRequest>(TRequest request, CancellationToken ct) where TRequest : IContextMenuRequest
        {
            // block input somehow

            var closeCompletionSource = new UniTaskCompletionSource();
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            try
            {
                // use appropriate facade method to show the menu
                switch (request)
                {
                    case UserProfileMenuRequest userReq:
                        await mvcFacade.ShowUserProfileContextMenuFromWalletIdAsync(
                            userReq.WalletAddress,
                            userReq.Position,
                            userReq.Offset,
                            linkedCts.Token,
                            closeCompletionSource.Task,
                            () => closeCompletionSource.TrySetResult(),
                            userReq.AnchorPoint
                        );
                        break;

                    case ChatMessageMenuRequest msgReq:
                        var popupData = new ChatEntryMenuPopupData(
                            msgReq.Position,
                            msgReq.MessageText,
                            () => closeCompletionSource.TrySetResult(),
                            closeCompletionSource.Task
                        );
                        await mvcFacade.ShowChatEntryMenuPopupAsync(popupData, linkedCts.Token);
                        break;

                    // Add cases for other menu types...
                }
            }
            finally
            {
                // exit modal
                linkedCts.Cancel();
                inputBlocker.Unblock();
            }
        }
    }
}