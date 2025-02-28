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
        private bool isShowing; // true whenever a view is being shown, so other calls wait for them to finish
        private bool isHiding; // true whenever a view is being hidden, so other calls wait for them to finish
        private bool isShowingChat; // true whenever the chat view is being shown, so other calls wait for them to finish (exceptional case)
        private PanelsSharingSpace panelBeingShown = PanelsSharingSpace.Chat;

        public SharedSpaceManager(IMVCManager mvcManager,
                                  DCLInput dclInput)
        {
            this.mvcManager = mvcManager;
            this.dclInput = dclInput;

            dclInput.Shortcuts.FriendPanel.performed += OnInputShortcutsFriendPanelPerformed;
            dclInput.Shortcuts.EmoteWheel.performed += OnInputShortcutsEmoteWheelPerformed;
            dclInput.Shortcuts.OpenChat.performed += OnInputShortcutsOpenChatPerformed;
            dclInput.UI.Submit.performed += OnUISubmitPerformed;
        }

        public async UniTask ShowAsync(PanelsSharingSpace panel, object parameters = null)
        {
            Debug.Log("YEAH SHOW: " + panel);

            if (!IsRegistered(panel))
                return;

            if(isShowing || isHiding || isShowingChat)
                return;

            isShowing = true; // Set to false when the view is shown, see OnControllerViewShowingComplete
            panelBeingShown = panel;
            Debug.Log("<color=red>YEAH ---> showing TRUE " + panel + "</color>");

            try
            {
                cts = cts.SafeRestart();

                if (panel == PanelsSharingSpace.Chat)
                    await HideAllAsync(panelToIgnore: PanelsSharingSpace.Chat);
                else
                    await HideAllAsync();

                IPanelInSharedSpace controllerInSharedSpace = controllers[panel];

                // Each panel has a different situation and implementation, and has to be shown in a different way
                switch (panel)
                {
                    case PanelsSharingSpace.Chat:
                    {
                        if ((controllerInSharedSpace as IController).State == ControllerState.ViewHidden)
                            await mvcManager.ShowAsync(ChatController.IssueCommand(), cts.Token);
                        else if(!controllerInSharedSpace.IsVisibleInSharedSpace)
                            await controllerInSharedSpace.ShowInSharedSpaceAsync(cts.Token);

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
                            await controllerInSharedSpace.ShowInSharedSpaceAsync(cts.Token);

                        break;
                    }
                    case PanelsSharingSpace.Skybox:
                    {
                        if (!controllerInSharedSpace.IsVisibleInSharedSpace)
                            await controllerInSharedSpace.ShowInSharedSpaceAsync(cts.Token);

                        break;
                    }
                    case PanelsSharingSpace.EmotesWheel:
                    {
                        if (!controllerInSharedSpace.IsVisibleInSharedSpace)
                            await mvcManager.ShowAsync(EmotesWheelController.IssueCommand(), cts.Token);

                        break;
                    }
                    case PanelsSharingSpace.SidebarProfile:
                    {
                        if (!controllerInSharedSpace.IsVisibleInSharedSpace)
                            await controllerInSharedSpace.ShowInSharedSpaceAsync(cts.Token);

                        break;
                    }
                    case PanelsSharingSpace.SidebarSettings:
                    {
                        if (!controllerInSharedSpace.IsVisibleInSharedSpace)
                            await controllerInSharedSpace.ShowInSharedSpaceAsync(cts.Token);

                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                isShowing = false;
                isHiding = false;
                isShowingChat = false;
                //Debug.LogException(ex);
                Debug.Log("<color=red>YEAH ---> showing and hiding FALSE ERROR! " + panel + "</color>");

                if (!(ex is OperationCanceledException))
                    throw;
            }
        }

        public async UniTask HideAsync(PanelsSharingSpace panel, object parameters = null)
        {
            Debug.Log("YEAH HIDE: " + panel);

            if (!IsRegistered(panel))
                return;

            isHiding = true;
            Debug.Log("<color=yellow>YEAH ---> hiding TRUE " + panel + "</color>");

            try
            {
                cts = cts.SafeRestart();

                IPanelInSharedSpace controllerInSharedSpace = controllers[panel];

                switch (panel)
                {
                    case PanelsSharingSpace.Friends:
                    {
                        await controllerInSharedSpace.HideInSharedSpaceAsync(cts.Token);
                        isShowingChat = true;
                        Debug.Log("<color=green>YEAH ---> showing TRUE " + PanelsSharingSpace.Chat + "</color>");
                        mvcManager.ShowAsync(ChatController.IssueCommand()).Forget();
                        break;
                    }
                    default:
                    {
                        await controllerInSharedSpace.HideInSharedSpaceAsync(cts.Token);
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Stops propagation
            }
            finally
            {
                isHiding = false;
                Debug.Log("<color=yellow>YEAH ---> hiding FALSE " + panel + "</color>");
            }
        }

        public async UniTask ToggleVisibilityAsync(PanelsSharingSpace panel, object parameters = null)
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

        public void RegisterPanel(PanelsSharingSpace panel, IPanelInSharedSpace controller)
        {
            Debug.Log("YEAH REGISTER: " + panel);

            if (IsRegistered(panel))
                Debug.LogError($"The panel {panel} was already registered in the shared space manager!");

            controller.ViewShowingComplete += OnControllerViewShowingComplete;

            controllers.Add(panel, controller);
        }

        public void Dispose()
        {
            dclInput.Shortcuts.EmoteWheel.performed -= OnInputShortcutsEmoteWheelPerformed;
            dclInput.Shortcuts.FriendPanel.performed -= OnInputShortcutsFriendPanelPerformed;
            dclInput.Shortcuts.OpenChat.performed -= OnInputShortcutsOpenChatPerformed;
        }

        private async void OnInputShortcutsEmoteWheelPerformed(InputAction.CallbackContext obj)
        {
            await ToggleVisibilityAsync(PanelsSharingSpace.EmotesWheel);
        }

        private async void OnInputShortcutsFriendPanelPerformed(InputAction.CallbackContext obj)
        {
            await ToggleVisibilityAsync(PanelsSharingSpace.Friends, new FriendsPanelParameter());
        }

        private async void OnInputShortcutsOpenChatPerformed(InputAction.CallbackContext obj)
        {
            await ToggleVisibilityAsync(PanelsSharingSpace.Chat);
        }

        private async void OnUISubmitPerformed(InputAction.CallbackContext obj)
        {
            if (IsRegistered(PanelsSharingSpace.Chat))
                await ShowAsync(PanelsSharingSpace.Chat);
        }

        private bool IsRegistered(PanelsSharingSpace panel)
        {
            return controllers.ContainsKey(panel);
        }

        private async UniTask HideAllAsync(PanelsSharingSpace? panelToIgnore = null)
        {
            foreach (KeyValuePair<PanelsSharingSpace,IPanelInSharedSpace> controllerInSharedSpace in controllers)
                if((!panelToIgnore.HasValue || controllerInSharedSpace.Key != panelToIgnore) && controllerInSharedSpace.Value.IsVisibleInSharedSpace)
                    await HideAsync(controllerInSharedSpace.Key);
        }

        private void OnControllerViewShowingComplete(IPanelInSharedSpace controller)
        {
            if (controllers[panelBeingShown] == controller)
            {
                isShowing = false;
                Debug.Log("<color=red>YEAH ---> showing FALSE " + controller + "</color>");
            }

            if (controllers[PanelsSharingSpace.Chat] == controller)
            {
                isShowingChat = false;
                Debug.Log("<color=green>YEAH ---> showing FALSE " + controller + "</color>");
            }
        }
    }
}
