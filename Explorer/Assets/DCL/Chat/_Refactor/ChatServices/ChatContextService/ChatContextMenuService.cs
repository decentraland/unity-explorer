﻿using Cysharp.Threading.Tasks;
using DCL.UI;
using MVC;
using System;
using System.Threading;

namespace DCL.Chat.ChatServices.ChatContextService
{
    public class ChatContextMenuService : IDisposable
    {
        private readonly IMVCManagerMenusAccessFacade mvcFacade;
        private readonly ChatClickDetectionService chatClickDetectionService;

        private CancellationTokenSource activeMenuCts = new();
        private UniTaskCompletionSource activeMenuTcs = new();

        public ChatContextMenuService(IMVCManagerMenusAccessFacade mvcFacade,
            ChatClickDetectionService chatClickDetectionService)
        {
            this.mvcFacade = mvcFacade;
            this.chatClickDetectionService = chatClickDetectionService;
        }


        public async UniTask ShowCommunityContextMenuAsync(ShowContextMenuRequest request)
        {
            RestartLifecycleControls();
            chatClickDetectionService.Pause();

            try
            {
                var parameter = new GenericContextMenuParameter(
                    request.MenuConfiguration,
                    request.Position, // Cast the Vector3 to Vector2
                    actionOnHide: () => activeMenuTcs.TrySetResult(),
                    closeTask: activeMenuTcs.Task
                );

                await mvcFacade.ShowGenericContextMenuAsync(parameter);
                await activeMenuTcs.Task;
            }
            finally
            {
                chatClickDetectionService.Resume();
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
        ///     Shows the generic context menu for channel options.
        ///     This uses the standard lifecycle management for context menus.
        /// </summary>
        public async UniTask ShowChannelContextMenuAsync(ShowChannelContextMenuRequest request)
        {
            RestartLifecycleControls();
            chatClickDetectionService.Pause();

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
                chatClickDetectionService.Resume();
            }
        }

        private void RestartLifecycleControls()
        {
            activeMenuCts.Cancel();
            activeMenuTcs.TrySetResult();
            activeMenuCts.Dispose();

            activeMenuCts = new CancellationTokenSource();
            activeMenuTcs = new UniTaskCompletionSource();
        }

        public void Dispose()
        {
            activeMenuCts.Cancel();
            activeMenuTcs.TrySetResult();
            activeMenuCts.Dispose();
        }
    }
}
