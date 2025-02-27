using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.EmotesWheel;
using DCL.Friends.UI.FriendPanel;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using Utility;

namespace DCL.UI.SharedSpaceManager
{
    public class SharedSpaceManager : ISharedSpaceManager, IDisposable
    {
        private readonly IMVCManager mvcManager;
        private readonly DCLInput dclInput;

        private CancellationTokenSource cts = new ();
        private Dictionary<PanelsSharingSpace, IPanelInSharedSpace> controllers = new ();

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
            await HideAllAsync();

            IPanelInSharedSpace controllerInSharedSpace = controllers[panel];

            // Each panel has a different situation and implementation, and has to be shown in a different way
            switch (panel)
            {
                case PanelsSharingSpace.Chat:
                {
                    if ((controllerInSharedSpace as IController).State == ControllerState.ViewHidden)
                    {
                        await mvcManager.ShowAsync(ChatController.IssueCommand(), cts.Token);
                    }

                    if (!controllerInSharedSpace.IsVisibleInSharedSpace)
                    {
                        await controllerInSharedSpace.ShowInSharedSpaceAsync(cts.Token);
                    }

                    break;
                }
                case PanelsSharingSpace.Friends:
                {
                    if (!controllerInSharedSpace.IsVisibleInSharedSpace)
                    {
                        await (controllers[PanelsSharingSpace.Chat] as IController).HideViewAsync(cts.Token);
                        await mvcManager.ShowAsync(FriendsPanelController.IssueCommand((FriendsPanelParameter)parameters), cts.Token);
                    }

                    break;
                }
                case PanelsSharingSpace.Notifications:
                {
                    if (!controllerInSharedSpace.IsVisibleInSharedSpace)
                    {
                        await controllerInSharedSpace.ShowInSharedSpaceAsync(cts.Token);
                    }

                    break;
                }
                case PanelsSharingSpace.Skybox:
                {
                    if (!controllerInSharedSpace.IsVisibleInSharedSpace)
                    {
                        await controllerInSharedSpace.ShowInSharedSpaceAsync(cts.Token);
                    }

                    break;
                }
                case PanelsSharingSpace.EmotesWheel:
                {
                    if (!controllerInSharedSpace.IsVisibleInSharedSpace)
                    {
                        await mvcManager.ShowAsync(EmotesWheelController.IssueCommand(), cts.Token);
                    }

                    break;
                }
                case PanelsSharingSpace.SidebarProfile:
                {
                    if (!controllerInSharedSpace.IsVisibleInSharedSpace)
                    {
                        await controllerInSharedSpace.ShowInSharedSpaceAsync(cts.Token);
                    }

                    break;
                }
                case PanelsSharingSpace.SidebarSettings:
                {
                    if (!controllerInSharedSpace.IsVisibleInSharedSpace)
                    {
                        await controllerInSharedSpace.ShowInSharedSpaceAsync(cts.Token);
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

            IPanelInSharedSpace controllerInSharedSpace = controllers[panel];

            switch (panel)
            {
                case PanelsSharingSpace.Friends:
                {
                    await controllerInSharedSpace.HideInSharedSpaceAsync(cts.Token);
                    await mvcManager.ShowAsync(ChatController.IssueCommand());
                    break;
                }
                default:
                {
                    await controllerInSharedSpace.HideInSharedSpaceAsync(cts.Token);
                    break;
                }
            }
        }

        public async UniTask ToggleVisibilityAsync(PanelsSharingSpace panel, object parameters)
        {
            Debug.Log("YEAH TOGGLE: " + panel);

            if (!IsRegistered(panel))
                return;

            bool show = !controllers[panel].IsVisibleInSharedSpace;

            if(show)
                await ShowAsync(panel, parameters);
            else
                await HideAsync(panel, parameters);
        }

        public void RegisterPanelController(PanelsSharingSpace panel, IPanelInSharedSpace controller)
        {
            Debug.Log("YEAH REGISTER: " + panel);

            if (IsRegistered(panel))
                Debug.LogError($"The panel {panel} was already registered in the shared space manager!");

            if (panel == PanelsSharingSpace.Chat)
            {
                ChatController chatController = controller as ChatController;
                chatController.FoldingChanged += OnChatControllerFoldingChanged;
            }

            controllers.Add(panel, controller);
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
            return controllers.ContainsKey(panel);
        }

        private async UniTask HideAllAsync(PanelsSharingSpace? panelToIgnore = null)
        {
            foreach (KeyValuePair<PanelsSharingSpace,IPanelInSharedSpace> controllerInSharedSpace in controllers)
                if((!panelToIgnore.HasValue || controllerInSharedSpace.Key != panelToIgnore) && controllerInSharedSpace.Value.IsVisibleInSharedSpace)
                    await controllerInSharedSpace.Value.HideInSharedSpaceAsync(cts.Token);
        }
/*
        private bool CheckAllPanelsAreHidden()
        {
            bool areHidden = true;

            foreach (KeyValuePair<PanelsSharingSpace,IControllerInSharedSpace> controllerInSharedSpace in controllers)
                if (controllerInSharedSpace.Value.IsVisibleInSharedSpace)
                {
                    areHidden = false;
                    break;
                }


            return areHidden;
        }
*/
        private void OnChatControllerFoldingChanged(bool isUnfolded)
        {
            if(controllers[PanelsSharingSpace.Chat].IsVisibleInSharedSpace)
                HideAllAsync(PanelsSharingSpace.Chat).Forget();
        }
    }
}
