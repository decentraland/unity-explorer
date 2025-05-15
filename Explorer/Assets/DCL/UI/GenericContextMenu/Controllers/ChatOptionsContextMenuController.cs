using Cysharp.Threading.Tasks;
using DCL.UI.GenericContextMenu.Controls.Configs;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.UI.GenericContextMenu.Controllers
{
    public class ChatOptionsContextMenuController
    {
        private const int CONTEXT_MENU_WIDTH = 280;
        private const int CONTEXT_MENU_ELEMENTS_SPACING = 8;
        private static readonly RectOffset CONTEXT_MENU_VERTICAL_LAYOUT_PADDING = new (15, 15, 14, 14);
        private static readonly RectOffset HORIZONTAL_LAYOUT_PADDING = new (0, 0, 0, 0);
        private static readonly int HORIZONTAL_LAYOUT_SPACING = 8;
        private static readonly Vector2 CONTEXT_MENU_OFFSET = new (0, -30);

        private readonly IMVCManager mvcManager;
        private readonly Controls.Configs.GenericContextMenu contextMenu;

        private CancellationTokenSource cancellationTokenSource;
        private UniTaskCompletionSource closeContextMenuTask;

        public ChatOptionsContextMenuController(IMVCManager mvcManager, Sprite deleteChatHistoryIcon, string deleteChatHistoryText, Action onDeleteChatHistoryClicked)
        {
            this.mvcManager = mvcManager;
            ButtonContextMenuControlSettings deleteChatHistoryButton = new ButtonContextMenuControlSettings(deleteChatHistoryText, deleteChatHistoryIcon, onDeleteChatHistoryClicked);

            contextMenu = new Controls.Configs.GenericContextMenu(CONTEXT_MENU_WIDTH, CONTEXT_MENU_OFFSET, CONTEXT_MENU_VERTICAL_LAYOUT_PADDING, CONTEXT_MENU_ELEMENTS_SPACING, anchorPoint: ContextMenuOpenDirection.TOP_LEFT)
               .AddControl(deleteChatHistoryButton);

            //Disabled until we got multiple channels working
            //.AddControl(new SeparatorContextMenuControlSettings())
            //.AddControl(new ToggleWithIconContextMenuControlSettings(pinChatToggleTextIcon, pinChatToggleText, OnPinChatToggle, HORIZONTAL_LAYOUT_PADDING, HORIZONTAL_LAYOUT_SPACING));
        }

        public async UniTask ShowContextMenuAsync(Vector2 position, UniTask closeMenuTask, Action onContextMenuHide = null)
        {
            closeContextMenuTask?.TrySetResult();
            closeContextMenuTask = new UniTaskCompletionSource();
            UniTask closeTask = UniTask.WhenAny(closeMenuTask, closeContextMenuTask.Task);
            cancellationTokenSource = cancellationTokenSource.SafeRestart();

            await mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(
                new GenericContextMenuParameter(contextMenu, position, actionOnHide: onContextMenuHide, closeTask: closeTask)), cancellationTokenSource.Token);
        }

        private void OnPinChatToggle(bool value)
        {
            //TODO: Pin Chat logic once we have several conversations
        }
    }
}
