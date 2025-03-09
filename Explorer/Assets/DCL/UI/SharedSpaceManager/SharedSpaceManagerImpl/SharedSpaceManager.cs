using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.Chat;
using DCL.Diagnostics;
using DCL.EmotesWheel;
using DCL.ExplorePanel;
using DCL.Friends.UI.FriendPanel;
using DCL.InWorldCamera;
using DCL.UI.ProfileElements;
using DCL.UI.Skybox;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DCL.UI.SharedSpaceManager
{
    // PLEASE read the summary in ISharedSpaceManager
    public class SharedSpaceManager : ISharedSpaceManager, IDisposable
    {
        private readonly IMVCManager mvcManager;
        private readonly DCLInput dclInput;
        private readonly Dictionary<PanelsSharingSpace, IPanelInSharedSpace> controllers = new ();
        private readonly World ecsWorld;

        private CancellationTokenSource cts = new ();
        private bool isShowing; // true whenever a view is being shown, so other calls wait for them to finish
        private bool isHiding; // true whenever a view is being hidden, so other calls wait for them to finish
        private PanelsSharingSpace panelBeingShown = PanelsSharingSpace.Chat; // Showing a panel may make other panels show too internally, this is the panel that started the process

        private bool isExplorePanelVisible => controllers[PanelsSharingSpace.Explore].IsVisibleInSharedSpace;

        private readonly bool isFriendsFeatureEnabled;
        private readonly bool isCameraReelFeatureEnabled;

        public SharedSpaceManager(IMVCManager mvcManager, DCLInput dclInput, World world, bool isFriendsEnabled, bool isCameraReelEnabled)
        {
            this.mvcManager = mvcManager;
            this.dclInput = dclInput;
            this.isFriendsFeatureEnabled = isFriendsEnabled;
            this.isCameraReelFeatureEnabled = isCameraReelEnabled;
            this.ecsWorld = world;

            if(isFriendsEnabled)
                dclInput.Shortcuts.FriendPanel.performed += OnInputShortcutsFriendPanelPerformedAsync;

            dclInput.Shortcuts.EmoteWheel.performed += OnInputShortcutsEmoteWheelPerformedAsync;
            dclInput.Shortcuts.OpenChat.performed += OnInputShortcutsOpenChatPerformedAsync;
            dclInput.UI.Submit.performed += OnUISubmitPerformedAsync;

            dclInput.Shortcuts.MainMenu.performed += OnInputShortcutsMainMenuPerformedAsync;
            dclInput.Shortcuts.Map.performed += OnInputShortcutsMapPerformedAsync;
            dclInput.Shortcuts.Settings.performed += OnInputShortcutsSettingsPerformedAsync;
            dclInput.Shortcuts.Backpack.performed += OnInputShortcutsBackpackPerformedAsync;
            dclInput.InWorldCamera.ToggleInWorldCamera.performed += OnInputInWorldCameraToggledAsync;

            if(isCameraReelEnabled)
                dclInput.InWorldCamera.CameraReel.performed += OnInputShortcutsCameraReelPerformedAsync;
        }

        public async UniTask ShowAsync(PanelsSharingSpace panel, object parameters = null)
        {
            if (!IsRegistered(panel))
            {
                ReportHub.LogError(ReportCategory.UI, $"The panel {panel} is not registered in the shared space manager!");
                return;
            }

            if (isShowing || isHiding)
            {
                ReportHub.Log(ReportCategory.UI, $"The panel {panel} could not be shown, there is another panel still being shown or hidden.");
                return;
            }

            isShowing = true; // Set to false when the view is shown, see OnControllerViewShowingComplete
            panelBeingShown = panel;

            try
            {
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
                        {
                            ChatController.ShowParams chatParams = parameters == null ? default(ChatController.ShowParams) : (ChatController.ShowParams)parameters;
                            await mvcManager.ShowAsync(ChatController.IssueCommand(chatParams), cts.Token);
                        }
                        else if (!controllerInSharedSpace.IsVisibleInSharedSpace)
                            await controllerInSharedSpace.OnShownInSharedSpaceAsync(cts.Token, parameters);
                        else
                            isShowing = false;

                        break;
                    }
                    case PanelsSharingSpace.Friends:
                    {
                        if (!controllerInSharedSpace.IsVisibleInSharedSpace && isFriendsFeatureEnabled)
                        {
                            // The chat is hidden while the friends panel is present
                            (controllers[PanelsSharingSpace.Chat] as ChatController).SetViewVisibility(false);

                            FriendsPanelParameter friendsParams = parameters == null ? default(FriendsPanelParameter) : (FriendsPanelParameter)parameters;
                            await mvcManager.ShowAsync(FriendsPanelController.IssueCommand(friendsParams), cts.Token);

                            // Once the friends panel is hidden, chat must appear
                            bool isShowingChat = panelBeingShown == PanelsSharingSpace.Chat;
                            await controllers[PanelsSharingSpace.Chat].OnShownInSharedSpaceAsync(cts.Token, new ChatController.ShowParams(isShowingChat));
                        }
                        else
                            isShowing = false;

                        break;
                    }
                    case PanelsSharingSpace.Skybox:
                    {
                        if (!controllerInSharedSpace.IsVisibleInSharedSpace)
                            await mvcManager.ShowAsync(SkyboxMenuController.IssueCommand(), cts.Token);
                        else
                            isShowing = false;

                        break;
                    }
                    case PanelsSharingSpace.EmotesWheel:
                    {
                        if (!controllerInSharedSpace.IsVisibleInSharedSpace)
                            await mvcManager.ShowAsync(EmotesWheelController.IssueCommand(), cts.Token);
                        else
                            isShowing = false;

                        break;
                    }
                    case PanelsSharingSpace.Explore:
                    {
                        if (!controllerInSharedSpace.IsVisibleInSharedSpace)
                        {
                            ExplorePanelParameter exploreParams = parameters == null ? default(ExplorePanelParameter) : (ExplorePanelParameter)parameters;
                            await mvcManager.ShowAsync(ExplorePanelController.IssueCommand(exploreParams), cts.Token);
                        }
                        else
                            isShowing = false;

                        break;
                    }
                    case PanelsSharingSpace.SidebarProfile:
                    {
                        if (!controllerInSharedSpace.IsVisibleInSharedSpace)
                            await mvcManager.ShowAsync(ProfileMenuController.IssueCommand(), cts.Token);
                        else
                            isShowing = false;

                        break;
                    }
                    case PanelsSharingSpace.Notifications:
                    case PanelsSharingSpace.SidebarSettings:
                    {
                        if (!controllerInSharedSpace.IsVisibleInSharedSpace)
                            await controllerInSharedSpace.OnShownInSharedSpaceAsync(cts.Token);
                        else
                            isShowing = false;

                        break;
                    }
                }
            }
            catch (OperationCanceledException ex2)
            {
                isShowing = false;
                isHiding = false;
            }
            catch (Exception ex)

            {
                isShowing = false;
                isHiding = false;

                if (!(ex is OperationCanceledException))
                    throw;
            }

            // Arrives here once when the panel stops being shown (they leave WaitForCloseIntentAsync) for whatever reason.
            // If there is an exit animation or any other process that takes time, it waits for it
            await UniTask.WaitUntil(() => isHiding == false, PlayerLoopTiming.Update, cts.Token);

        }

        public async UniTask HideAsync(PanelsSharingSpace panel, object parameters = null)
        {
            Debug.Log("HIdING: " + panel);

            if (!IsRegistered(panel))
                return;

            isHiding = true;

            try
            {
                IPanelInSharedSpace controllerInSharedSpace = controllers[panel];

                switch (panel)
                {
                    case PanelsSharingSpace.Friends:
                    {
                        if (isFriendsFeatureEnabled)
                        {
                            await controllerInSharedSpace.OnHiddenInSharedSpaceAsync(cts.Token);

                            // When friends panel is not present, the chat panel must be
                            await controllers[PanelsSharingSpace.Chat].OnShownInSharedSpaceAsync(cts.Token, new ChatController.ShowParams(false));
                        }

                        break;
                    }
                    default:
                    {
                        await controllerInSharedSpace.OnHiddenInSharedSpaceAsync(cts.Token);
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
            }
        }

        public async UniTask ToggleVisibilityAsync(PanelsSharingSpace panel, object parameters = null)
        {
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
            if (IsRegistered(panel))
            {
                ReportHub.LogError(ReportCategory.UI, $"The panel {panel} was already registered in the shared space manager!");
                return;
            }

            controller.ViewShowingComplete += OnControllerViewShowingComplete;

            controllers.Add(panel, controller);
        }

        public void Dispose()
        {
            if(isFriendsFeatureEnabled)
                dclInput.Shortcuts.FriendPanel.performed -= OnInputShortcutsFriendPanelPerformedAsync;

            dclInput.Shortcuts.EmoteWheel.performed -= OnInputShortcutsEmoteWheelPerformedAsync;
            dclInput.Shortcuts.OpenChat.performed -= OnInputShortcutsOpenChatPerformedAsync;
            dclInput.UI.Submit.performed -= OnUISubmitPerformedAsync;

            dclInput.Shortcuts.MainMenu.performed -= OnInputShortcutsMainMenuPerformedAsync;
            dclInput.Shortcuts.Map.performed -= OnInputShortcutsMapPerformedAsync;
            dclInput.Shortcuts.Settings.performed -= OnInputShortcutsSettingsPerformedAsync;
            dclInput.Shortcuts.Backpack.performed -= OnInputShortcutsBackpackPerformedAsync;

            if(isCameraReelFeatureEnabled)
                dclInput.InWorldCamera.CameraReel.performed -= OnInputShortcutsCameraReelPerformedAsync;
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
            // Showing some panels may make others be visible too (like the chat), but we only consider the showing process as finished if the event was raised by the panel that started it
            if (controllers[panelBeingShown] == controller)
                isShowing = false;
        }

        private async void OnUISubmitPerformedAsync(InputAction.CallbackContext obj)
        {
            if (IsRegistered(PanelsSharingSpace.Chat) && !isExplorePanelVisible)
            {
                await ShowAsync(PanelsSharingSpace.Chat, new ChatController.ShowParams(true));
                (controllers[PanelsSharingSpace.Chat] as ChatController).FocusInputBox();
            }
        }

        #region Shortcut handlers

        private async void OnInputShortcutsCameraReelPerformedAsync(InputAction.CallbackContext obj)
        {
            if(!isExplorePanelVisible && isCameraReelFeatureEnabled)
                await ShowAsync(PanelsSharingSpace.Explore, new ExplorePanelParameter(ExploreSections.CameraReel));
        }

        private async void OnInputShortcutsBackpackPerformedAsync(InputAction.CallbackContext obj)
        {
            if(!isExplorePanelVisible)
                await ShowAsync(PanelsSharingSpace.Explore, new ExplorePanelParameter(ExploreSections.Backpack));
        }

        private async void OnInputShortcutsSettingsPerformedAsync(InputAction.CallbackContext obj)
        {
            if(!isExplorePanelVisible)
                await ShowAsync(PanelsSharingSpace.Explore, new ExplorePanelParameter(ExploreSections.Settings));
        }

        private async void OnInputShortcutsMapPerformedAsync(InputAction.CallbackContext obj)
        {
            if(!isExplorePanelVisible)
                await ShowAsync(PanelsSharingSpace.Explore, new ExplorePanelParameter(ExploreSections.Navmap));
        }

        private async void OnInputShortcutsMainMenuPerformedAsync(InputAction.CallbackContext obj)
        {
            if(!isExplorePanelVisible)
                await ShowAsync(PanelsSharingSpace.Explore); // No section provided, the panel will decide
        }

        private async void OnInputShortcutsEmoteWheelPerformedAsync(InputAction.CallbackContext obj)
        {
            if(!isExplorePanelVisible)
                await ToggleVisibilityAsync(PanelsSharingSpace.EmotesWheel);
        }

        private async void OnInputShortcutsFriendPanelPerformedAsync(InputAction.CallbackContext obj)
        {
            if(!isExplorePanelVisible && isFriendsFeatureEnabled)
                await ToggleVisibilityAsync(PanelsSharingSpace.Friends, new FriendsPanelParameter());
        }

        private async void OnInputShortcutsOpenChatPerformedAsync(InputAction.CallbackContext obj)
        {
            if(!isExplorePanelVisible)
                await ToggleVisibilityAsync(PanelsSharingSpace.Chat, new ChatController.ShowParams(true));
        }

        private async void OnInputInWorldCameraToggledAsync(InputAction.CallbackContext obj)
        {
            // TODO: When we have more time, the InWorldCameraController and EmitInWorldCameraInputSystem and other stuff should be refactored and adapted properly
            if(isShowing || isHiding)
                return;

            Entity camera = ecsWorld.CacheCamera();

            if(!ecsWorld.Has<InWorldCameraComponent>(camera))
                await HideAllAsync();

            const string SOURCE_SHORTCUT = "Shortcut";
            ecsWorld.Add(camera, new ToggleInWorldCameraRequest { IsEnable = !ecsWorld.Has<InWorldCameraComponent>(camera), Source = SOURCE_SHORTCUT });
        }

        #endregion
    }
}
