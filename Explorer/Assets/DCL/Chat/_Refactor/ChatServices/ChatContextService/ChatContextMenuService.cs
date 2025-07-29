using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.UI;
using MVC;

namespace DCL.Chat.Services
{
    public class ChatContextMenuService : IDisposable
    {
        private readonly IMVCManagerMenusAccessFacade mvcFacade;
        private readonly ChatClickDetectionService chatClickDetectionService;

        private CancellationTokenSource? activeMenuCts;
        private UniTaskCompletionSource? activeMenuTcs;

        public ChatContextMenuService(IMVCManagerMenusAccessFacade mvcFacade,
            ChatClickDetectionService chatClickDetectionService)
        {
            this.mvcFacade = mvcFacade;
            this.chatClickDetectionService = chatClickDetectionService;
        }

        /// <summary>
        ///     Show user profile context menu.
        ///     Pause and Resume click detection service to prevent
        ///     clicks from being registered while the menu is open.
        /// </summary>
        /// <param name="request"></param>
        public async UniTask ShowUserProfileMenuAsync(UserProfileMenuRequest request)
        {
            RestartLifecycleControls();

            chatClickDetectionService.Pause();

            try
            {
                await mvcFacade.ShowUserProfileContextMenuFromWalletIdAsync(
                    request.WalletAddress,
                    request.Position,
                    default,
                    activeMenuCts.Token,
                    activeMenuTcs.Task,
                    onHide: () => activeMenuTcs.TrySetResult(),
                    request.AnchorPoint
                );
            }
            finally
            {
                chatClickDetectionService.Resume();
            }
        }

        /// <summary>
        ///     Show channel options context menu.
        ///     Pause and Resume click detection service to prevent
        ///     clicks from being registered while the menu is open.
        /// </summary>
        /// <param name="request"></param>
        public async UniTask ShowChannelOptionsAsync(ChatContextMenuRequest request)
        {
            RestartLifecycleControls();

            chatClickDetectionService.Pause();

            try
            {
                mvcFacade.ShowChatContextMenuAsync(
                    request.Position,
                    request.contextMenuData,
                    request.OnDeleteHistory,
                    onContextMenuHide: () => activeMenuTcs!.TrySetResult(),
                    closeMenuTask: activeMenuTcs.Task
                );

                await activeMenuTcs.Task;
            }
            finally
            {
                chatClickDetectionService.Resume();
            }
        }

        public async UniTask ShowChatOptionsAsync(ChatEntryMenuPopupData request)
        {
            RestartLifecycleControls();

            chatClickDetectionService.Pause();

            try
            {
                await mvcFacade.ShowChatEntryMenuPopupAsync(request, activeMenuCts.Token);
            }
            finally
            {
                chatClickDetectionService.Resume();
            }
        }

        private void RestartLifecycleControls()
        {
            activeMenuCts?.Cancel();
            activeMenuTcs?.TrySetResult();
            activeMenuCts?.Dispose();

            activeMenuCts = new CancellationTokenSource();
            activeMenuTcs = new UniTaskCompletionSource();
        }

        public void Dispose()
        {
            activeMenuCts?.Cancel();
            activeMenuTcs?.TrySetResult();
            activeMenuCts?.Dispose();
        }
    }
}