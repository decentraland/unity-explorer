using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Friends.UI.FriendPanel;
using DCL.Friends.UI.Requests;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using Utility;

namespace DCL.UI.SharedSpaceManager
{
    public class SharedSpaceManager : ISharedSpaceManager, IDisposable
    {
        private FriendsPanelController friendsController;
        private ChatController chatController;
        private FriendRequestController friendRequestController;
        private readonly IMVCManager mvcManager;
        private readonly DCLInput dclInput;

        private CancellationTokenSource cts = new ();

        public SharedSpaceManager(IMVCManager mvcManager,
                                  DCLInput dclInput)
        {
            this.mvcManager = mvcManager;
            this.dclInput = dclInput;

            dclInput.Shortcuts.FriendPanel.performed += OnInputShortcutsFriendPanelPerformed;
            dclInput.Shortcuts.OpenChat.performed += OnInputShortcutsOpenChatPerformed;
        }

        public async UniTask ShowAsync(PanelsSharingSpace panel, object parameters)
        {
            Debug.Log("YEAH SHOW: " + panel);

            if (!IsRegistered(panel))
                return;

            cts = cts.SafeRestart();

            switch (panel)
            {
                case PanelsSharingSpace.Chat:
                {
                    if (chatController.State == ControllerState.ViewHidden)
                    {
                        await HideAllAsync();
                        await mvcManager.ShowAsync(ChatController.IssueCommand(), cts.Token);
                    }
                    else
                    {
                        chatController.IsUnfolded = true;
                    }
                    break;
                }
                case PanelsSharingSpace.Friends:
                {
                    if (friendsController.State == ControllerState.ViewHidden)
                    {
                        await HideAllAsync();
                        await mvcManager.ShowAsync(FriendsPanelController.IssueCommand((FriendsPanelParameter)parameters), cts.Token);
                    }
                    break;
                }
                case PanelsSharingSpace.FriendRequest:
                {
                    if (friendRequestController.State == ControllerState.ViewHidden)
                    {
                        await HideAllAsync();
                        await mvcManager.ShowAsync(FriendRequestController.IssueCommand((FriendRequestParams)parameters), cts.Token);
                    }
                    break;
                }
            }
        }

        public async UniTask HideAsync(PanelsSharingSpace panel, object parameters)
        {
            Debug.Log("YEAH HIDE: " + panel);

            if (!IsRegistered(panel))
                return;

            cts = cts.SafeRestart();

            switch (panel)
            {
                case PanelsSharingSpace.Chat:
                {
                    chatController.IsUnfolded = !chatController.IsUnfolded;
                    break;
                }
                case PanelsSharingSpace.Friends:
                {
                    await friendsController.HideViewAsync(cts.Token);
                    await mvcManager.ShowAsync(ChatController.IssueCommand());
                    break;
                }
                case PanelsSharingSpace.FriendRequest:
                {
                    // TODO
                    break;
                }
            }
        }

        public async UniTask ToggleVisibilityAsync(PanelsSharingSpace panel, object parameters)
        {
            Debug.Log("YEAH TOGGLE: " + panel);

            if (!IsRegistered(panel))
                return;

            bool show = false;

            switch (panel)
            {
                case PanelsSharingSpace.Chat:
                {
                    show = chatController.State == ControllerState.ViewHidden;
                    break;
                }
                case PanelsSharingSpace.Friends:
                {
                    show = friendsController.State == ControllerState.ViewHidden;
                    break;
                }
                case PanelsSharingSpace.FriendRequest:
                {
                    show = chatController.State == ControllerState.ViewHidden;
                    break;
                }
            }

            if(show)
                await ShowAsync(panel, parameters);
            else
                await HideAsync(panel, parameters);
        }

        public void RegisterPanelController(PanelsSharingSpace panel, IController controller)
        {
            Debug.Log("YEAH REGISTER: " + panel);

            if (IsRegistered(panel))
                Debug.LogError($"The panel {panel} was already registered in the shared space manager!");

            switch (panel)
            {
                case PanelsSharingSpace.Chat:
                {
                    chatController = controller as ChatController;
                    break;
                }
                case PanelsSharingSpace.Friends:
                {
                    friendsController = controller as FriendsPanelController;
                    break;
                }
                case PanelsSharingSpace.FriendRequest:
                {
                    friendRequestController = controller as FriendRequestController;
                    break;
                }
            }
        }

        public void Dispose()
        {
            dclInput.Shortcuts.FriendPanel.performed -= OnInputShortcutsFriendPanelPerformed;
            dclInput.Shortcuts.OpenChat.performed -= OnInputShortcutsOpenChatPerformed;
        }

        private async void OnInputShortcutsFriendPanelPerformed(InputAction.CallbackContext obj)
        {
            await ToggleVisibilityAsync(PanelsSharingSpace.Friends, new FriendsPanelParameter());
        }

        private async void OnInputShortcutsOpenChatPerformed(InputAction.CallbackContext obj)
        {
            await ToggleVisibilityAsync(PanelsSharingSpace.Chat, null);
        }

        private bool IsRegistered(PanelsSharingSpace panel)
        {
            switch (panel)
            {
                case PanelsSharingSpace.Chat:
                    return chatController != null;
                case PanelsSharingSpace.Friends:
                    return friendsController != null;
                case PanelsSharingSpace.FriendRequest:
                    return friendRequestController != null;
            }

            return false;
        }

        private async UniTask HideAllAsync()
        {
            if(friendsController.State != ControllerState.ViewHidden)
                await friendsController.HideViewAsync(cts.Token);

            if(friendRequestController.State != ControllerState.ViewHidden)
                await friendRequestController.HideViewAsync(cts.Token);

            if(chatController.State != ControllerState.ViewHidden)
                await chatController.HideViewAsync(cts.Token);

        }
    }
}
