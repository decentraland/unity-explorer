using Cysharp.Threading.Tasks;
using DCL.UI;
using MVC;
using System;
using System.Threading;
using Utility;

namespace DCL.Chat.ChatServices.ChatContextService
{
    public class ChatContextMenuService : IDisposable
    {
        private readonly IMVCManagerMenusAccessFacade mvcFacade;
        private readonly ChatClickDetectionHandler chatClickDetectionHandler;

        private CancellationTokenSource activeMenuCts = new();
        private UniTaskCompletionSource activeMenuTcs = new();

        public ChatContextMenuService(IMVCManagerMenusAccessFacade mvcFacade,
            ChatClickDetectionHandler chatClickDetectionHandler)
        {
            this.mvcFacade = mvcFacade;
            this.chatClickDetectionHandler = chatClickDetectionHandler;
        }


        public async UniTask ShowCommunityContextMenuAsync(ShowContextMenuRequest request)
        {
            RestartLifecycleControls();
            chatClickDetectionHandler.Pause();

            try
            {
                var parameter = new GenericContextMenuParameter(
                    request.MenuConfiguration,
                    request.Position, // Cast the Vector3 to Vector2
                    actionOnHide: () =>
                    {
                        activeMenuTcs.TrySetResult();
                        request.OnHide?.Invoke();
                    },
                    actionOnShow: () => request.OnShow?.Invoke(),
                    closeTask: activeMenuTcs.Task
                );

                await mvcFacade.ShowGenericContextMenuAsync(parameter);
                await activeMenuTcs.Task;
            }
            finally
            {
                chatClickDetectionHandler.Resume();
            }
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

            chatClickDetectionHandler.Pause();
            try
            {
                await mvcFacade.ShowUserProfileContextMenuFromWalletIdAsync(
                    request.WalletAddress,
                    request.Position,
                    default,
                    activeMenuCts.Token,
                    activeMenuTcs.Task,
                    onHide: () =>
                    {
                        activeMenuTcs.TrySetResult();
                        request.OnHide?.Invoke();
                    },
                    request.AnchorPoint,
                    onShow: () => request.OnShow?.Invoke()
                );
            }
            finally
            {
                chatClickDetectionHandler.Resume();
            }
        }

        /// <summary>
        ///     Shows the generic context menu for channel options.
        ///     This uses the standard lifecycle management for context menus.
        /// </summary>
        public async UniTask ShowChannelContextMenuAsync(ShowChannelContextMenuRequest request)
        {
            RestartLifecycleControls();
            chatClickDetectionHandler.Pause();

            try
            {
                var parameter = new GenericContextMenuParameter(
                    request.MenuConfiguration,
                    request.Position,
                    actionOnHide: () => activeMenuTcs.TrySetResult(),
                    closeTask: activeMenuTcs.Task
                );

                ViewDependencies.ContextMenuOpener.OpenContextMenu(parameter, activeMenuCts.Token);

                await activeMenuTcs.Task;
            }
            finally
            {
                chatClickDetectionHandler.Resume();
            }
        }

        private void RestartLifecycleControls()
        {
            activeMenuTcs.TrySetResult();

            activeMenuCts = activeMenuCts.SafeRestart();
            activeMenuTcs = new UniTaskCompletionSource();
        }

        public void Dispose()
        {
            activeMenuTcs.TrySetResult();
            activeMenuCts.SafeCancelAndDispose();
        }
    }
}
