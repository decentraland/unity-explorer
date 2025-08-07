using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.Chat;
using DCL.Chat.ControllerShowParams;
using DCL.Communities;
using DCL.Diagnostics;
using DCL.ExplorePanel;
using DCL.Friends.UI.FriendPanel;
using DCL.InWorldCamera;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.InputSystem;
using Utility;

namespace DCL.UI.SharedSpaceManager
{
    /// <summary>
    ///     <inheritdoc cref="ISharedSpaceManager" />
    /// </summary>
    public class SharedSpaceManager : ISharedSpaceManager, IDisposable
    {
        private readonly Dictionary<PanelsSharingSpace, PanelRegistration> registrations = new ();

        private readonly IMVCManager mvcManager;
        private readonly DCLInput dclInput;
        private readonly World ecsWorld;

        private readonly bool isFriendsFeatureEnabled;
        private readonly bool isCameraReelFeatureEnabled;
        private bool isCommunitiesFeatureEnabled;

        private readonly CancellationTokenSource cts = new ();
        private readonly CancellationTokenSource configureShortcutsCts = new ();
        private bool isTransitioning; // true whenever a view is being shown or hidden, so other calls wait for them to finish
        private PanelsSharingSpace panelBeingShown = PanelsSharingSpace.Chat; // Showing a panel may make other panels show too internally, this is the panel that started the process

        private bool isExplorePanelVisible => registrations[PanelsSharingSpace.Explore].panel.IsVisibleInSharedSpace;

        public SharedSpaceManager(IMVCManager mvcManager, World world, bool isFriendsEnabled, bool isCameraReelEnabled)
        {
            this.mvcManager = mvcManager;
            dclInput = DCLInput.Instance;
            isFriendsFeatureEnabled = isFriendsEnabled;
            isCameraReelFeatureEnabled = isCameraReelEnabled;
            ecsWorld = world;

            configureShortcutsCts = configureShortcutsCts.SafeRestart();
            ConfigureShortcutsAsync(configureShortcutsCts.Token).Forget();
        }

        private async UniTaskVoid ConfigureShortcutsAsync(CancellationToken ct)
        {
            if (isFriendsFeatureEnabled)
                dclInput.Shortcuts.FriendPanel.performed += OnInputShortcutsFriendPanelPerformedAsync;

            dclInput.Shortcuts.EmoteWheel.performed += OnInputShortcutsEmoteWheelPerformedAsync;
            dclInput.Shortcuts.OpenChat.performed += OnInputShortcutsOpenChatPerformedAsync;
            dclInput.UI.Submit.performed += OnUISubmitPerformedAsync;

            dclInput.Shortcuts.MainMenu.performed += OnInputShortcutsMainMenuPerformedAsync;
            dclInput.Shortcuts.Map.performed += OnInputShortcutsMapPerformedAsync;
            dclInput.Shortcuts.Settings.performed += OnInputShortcutsSettingsPerformedAsync;
            dclInput.Shortcuts.Backpack.performed += OnInputShortcutsBackpackPerformedAsync;

            isCommunitiesFeatureEnabled = await CommunitiesFeatureAccess.Instance.IsUserAllowedToUseTheFeatureAsync(ct);
            if (isCommunitiesFeatureEnabled)
                dclInput.Shortcuts.Communities.performed += OnInputShortcutsCommunitiesPerformedAsync;

            if (isCameraReelFeatureEnabled)
            {
                dclInput.InWorldCamera.CameraReel.performed += OnInputShortcutsCameraReelPerformedAsync;
                dclInput.InWorldCamera.ToggleInWorldCamera.performed += OnInputInWorldCameraToggledAsync;
            }
        }

        public void Dispose()
        {
            if (isFriendsFeatureEnabled)
                dclInput.Shortcuts.FriendPanel.performed -= OnInputShortcutsFriendPanelPerformedAsync;

            dclInput.Shortcuts.EmoteWheel.performed -= OnInputShortcutsEmoteWheelPerformedAsync;
            dclInput.Shortcuts.OpenChat.performed -= OnInputShortcutsOpenChatPerformedAsync;
            dclInput.UI.Submit.performed -= OnUISubmitPerformedAsync;

            dclInput.Shortcuts.MainMenu.performed -= OnInputShortcutsMainMenuPerformedAsync;
            dclInput.Shortcuts.Map.performed -= OnInputShortcutsMapPerformedAsync;
            dclInput.Shortcuts.Settings.performed -= OnInputShortcutsSettingsPerformedAsync;
            dclInput.Shortcuts.Backpack.performed -= OnInputShortcutsBackpackPerformedAsync;

            if (isCameraReelFeatureEnabled)
                dclInput.Shortcuts.Communities.performed -= OnInputShortcutsCommunitiesPerformedAsync;

            if (isCameraReelFeatureEnabled)
                dclInput.InWorldCamera.CameraReel.performed -= OnInputShortcutsCameraReelPerformedAsync;

            cts.SafeCancelAndDispose();
            configureShortcutsCts.SafeCancelAndDispose();
        }

        public async UniTask ShowAsync<TParams>(PanelsSharingSpace panel, TParams parameters = default!)
        {
            if (!IsRegistered(panel))
            {
                ReportHub.LogError(ReportCategory.UI, $"The panel {panel} is not registered in the shared space manager!");
                return;
            }

            if (isTransitioning)
            {
                ReportHub.Log(ReportCategory.UI, $"The panel {panel} could not be shown, there is another panel still being shown or hidden.");
                return;
            }

            isTransitioning = true; // Set to false when the view is shown, see OnControllerViewShowingComplete
            panelBeingShown = panel;

            try
            {
                await HideAllAsync(panelToIgnore: PanelsSharingSpace.Chat);
                
                PanelRegistration<TParams> registration = registrations[panel].GetByParams<TParams>();
                IPanelInSharedSpace<TParams> panelInSharedSpace = registration.instance;

                // Each panel has a different situation and implementation, and has to be shown in a different way
                switch (panel)
                {
                    case PanelsSharingSpace.Chat:
                    {
                        IController controller = registration.GetPanel<IController>();
                        var chatParams = (ChatControllerShowParams)(object)parameters;
                        
                        if (controller.State == ControllerState.ViewHidden)
                            await registration.IssueShowCommandAsync(mvcManager, parameters, cts.Token);
                        else if (!panelInSharedSpace.IsVisibleInSharedSpace || chatParams.Focus)
                            await panelInSharedSpace.OnShownInSharedSpaceAsync(cts.Token, parameters);
                        else
                            isTransitioning = false;
                        break;
                    }
                    case PanelsSharingSpace.Friends:
                    {
                        if (!panelInSharedSpace.IsVisibleInSharedSpace && isFriendsFeatureEnabled)
                        {
                            ChatMainController chatController = registrations[PanelsSharingSpace.Chat].GetPanel<ChatMainController>();
                            chatController.SetVisibility(false);

                            await registration.IssueShowCommandAsync(mvcManager, parameters, cts.Token);

                            await UniTask.WaitUntil(() => !panelInSharedSpace.IsVisibleInSharedSpace, PlayerLoopTiming.Update, cts.Token);

                            // If it is transitioning, it's due to another panel was shown and that made Friends hide
                            // In order to not execute 2 code paths at the same time, it waits for new operation to finish and then
                            // continues, showing the chat
                            if(isTransitioning)
                                await UniTask.WaitWhile(() => isTransitioning);

                            // Once the friends panel is hidden, chat must appear (unless the Friends panel was hidden due to showing the chat panel)
                            if (panelBeingShown != PanelsSharingSpace.Chat)
                                await registrations[PanelsSharingSpace.Chat].GetPanel<ChatMainController>().OnShownInSharedSpaceAsync(cts.Token, new ChatControllerShowParams(false, false));
                        }
                        else
                            isTransitioning = false;

                        break;
                    }
                    case PanelsSharingSpace.Skybox:
                    case PanelsSharingSpace.EmotesWheel:
                    case PanelsSharingSpace.Explore:
                    case PanelsSharingSpace.SidebarProfile:
                    case PanelsSharingSpace.MarketplaceCredits:
                    {
                        if (!panelInSharedSpace.IsVisibleInSharedSpace)
                        {
                            await registration.IssueShowCommandAsync(mvcManager, parameters, cts.Token);

                            await UniTask.WaitUntil(() => !panelInSharedSpace.IsVisibleInSharedSpace, PlayerLoopTiming.Update, cts.Token);
                        }
                        else
                            isTransitioning = false;

                        break;
                    }
                    case PanelsSharingSpace.Notifications:
                    case PanelsSharingSpace.SidebarSettings:
                    {
                        if (!panelInSharedSpace.IsVisibleInSharedSpace)
                        {
                            await panelInSharedSpace.OnShownInSharedSpaceAsync(cts.Token);

                            await UniTask.WaitUntil(() => !panelInSharedSpace.IsVisibleInSharedSpace, PlayerLoopTiming.Update, cts.Token);
                        }
                        else
                            isTransitioning = false;

                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                isTransitioning = false;

                if (!(ex is OperationCanceledException))
                    throw;
            }
        }

        /// <summary>
        ///     Waits for the panel to finish its animation or cleaning process.
        /// </summary>
        /// <param name="panel">Which panel to hide.</param>
        /// <returns>The async task.</returns>
        private async UniTask HideAsync(PanelsSharingSpace panel)
        {
            if (!IsRegistered(panel))
                return;

            try
            {
                IPanelInSharedSpace controllerInSharedSpace = registrations[panel].panel;

                if (!(controllerInSharedSpace is IController) ||
                    ((controllerInSharedSpace as IController).State != ControllerState.ViewHiding && (controllerInSharedSpace as IController).State != ControllerState.ViewHidden))
                {
                    await controllerInSharedSpace.OnHiddenInSharedSpaceAsync(cts.Token);

                    await UniTask.WaitUntil(() => !controllerInSharedSpace.IsVisibleInSharedSpace);
                }
            }
            catch (OperationCanceledException)
            {
                // Stops propagation
            }
        }

        public async UniTask ToggleVisibilityAsync<TParams>(PanelsSharingSpace panel, TParams parameters = default!)
        {
            if (!IsRegistered(panel) || isTransitioning)
                return;

            bool show = !registrations[panel].panel.IsVisibleInSharedSpace;

            if (show)
                await ShowAsync(panel, parameters);
            else
                await HideAsync(panel);
        }

        private bool IsRegistered(PanelsSharingSpace panel) =>
            registrations.ContainsKey(panel);

        private async UniTask HideAllAsync(PanelsSharingSpace? panelToIgnore = null)
        {
            foreach (KeyValuePair<PanelsSharingSpace, PanelRegistration> controllerInSharedSpace in registrations)
                if ((!panelToIgnore.HasValue || controllerInSharedSpace.Key != panelToIgnore) && controllerInSharedSpace.Value.panel.IsVisibleInSharedSpace)
                    await HideAsync(controllerInSharedSpace.Key);
        }

        private void OnPanelViewShowingComplete(IPanelInSharedSpace panel)
        {
            // Showing some panels may make others be visible too (like the chat), but we only consider the showing process as finished if the event was raised by the panel that started it
            if (registrations[panelBeingShown].panel == panel)
                isTransitioning = false;
        }

        private async void OnUISubmitPerformedAsync(InputAction.CallbackContext obj)
        {
            if (IsRegistered(PanelsSharingSpace.Chat) && !isExplorePanelVisible)
                await ShowAsync(PanelsSharingSpace.Chat, new ChatControllerShowParams(true, true));
        }

#region Registration
        private abstract class PanelRegistration
        {
            /// <summary>
            /// Gets the registered panel without distinguishing among controller or non-controller.
            /// </summary>
            internal abstract IPanelInSharedSpace panel { get; }

            /// <summary>
            /// Gets a specific type version of the registration, according to the type of the parameters required by the panel.
            /// </summary>
            /// <typeparam name="T">The type of the parameters.</typeparam>
            /// <returns>The typed version of the registration.</returns>
            internal abstract PanelRegistration<T> GetByParams<T>();

            /// <summary>
            /// Gets a specific type version of the registered panel, according to the type of the parameters required by it.
            /// </summary>
            /// <typeparam name="T">The type of the parameters.</typeparam>
            /// <returns>The typed version of the panel.</returns>
            internal abstract T GetPanel<T>();
        }

        private class PanelRegistration<TParams> : PanelRegistration
        {
            internal readonly IPanelInSharedSpace<TParams> instance;
            private readonly Func<IMVCManager, TParams, CancellationToken, UniTask> showAsyncCommand;

            internal override IPanelInSharedSpace panel => instance;

            internal PanelRegistration(IPanelInSharedSpace<TParams> panel, Func<IMVCManager, TParams, CancellationToken, UniTask> showAsyncCommand = null)
            {
                instance = panel;
                this.showAsyncCommand = showAsyncCommand;
            }

            internal override T GetPanel<T>()
            {
                if (instance is not T castedPanel)
                    throw new ArgumentException($"{panel} is not assignable to {typeof(T)}");

                return castedPanel;
            }

            internal UniTask IssueShowCommandAsync(IMVCManager mvcManager, TParams parameters, CancellationToken ct)
            {
                if (showAsyncCommand == null)
                    throw new NotSupportedException($"{instance} is not a controller");

                return showAsyncCommand(mvcManager, parameters, ct);
            }

            internal override PanelRegistration<T> GetByParams<T>()
            {
                if (typeof(T) != typeof(TParams))
                    throw new ArgumentException($"The parameters type provided ({typeof(T).Name}) does not match the expected type ({typeof(TParams).Name})");

                return this as PanelRegistration<T>;
            }
        }

        public void RegisterPanel<TParams>(PanelsSharingSpace panel, IPanelInSharedSpace<TParams> panelImplementation)
        {
            if (IsRegistered(panel))
            {
                ReportHub.LogError(ReportCategory.UI, $"The panel {panel} was already registered in the shared space manager!");
                return;
            }

            panelImplementation.ViewShowingComplete += OnPanelViewShowingComplete;
            registrations.Add(panel, new PanelRegistration<TParams>(panelImplementation));
        }

        public void RegisterPanel<TView, TInputData>(PanelsSharingSpace panel, IControllerInSharedSpace<TView, TInputData> controller) where TView: IView
        {
            if (IsRegistered(panel))
            {
                ReportHub.LogError(ReportCategory.UI, $"The panel {panel} was already registered in the shared space manager!");
                return;
            }

            controller.ViewShowingComplete += OnPanelViewShowingComplete;

            registrations.Add(panel, new PanelRegistration<TInputData>(controller,
                (manager, data, ct) => manager.ShowAsync(new ShowCommand<TView, TInputData>(data), ct)));
        }
#endregion

#region Shortcut handlers
        private async void OnInputShortcutsCameraReelPerformedAsync(InputAction.CallbackContext obj)
        {
            if (!isExplorePanelVisible && isCameraReelFeatureEnabled)
                await ShowAsync(PanelsSharingSpace.Explore, new ExplorePanelParameter(ExploreSections.CameraReel));
        }

        private async void OnInputShortcutsBackpackPerformedAsync(InputAction.CallbackContext obj)
        {
            if (!isExplorePanelVisible)
                await ShowAsync(PanelsSharingSpace.Explore, new ExplorePanelParameter(ExploreSections.Backpack));
        }

        private async void OnInputShortcutsSettingsPerformedAsync(InputAction.CallbackContext obj)
        {
            if (!isExplorePanelVisible)
                await ShowAsync(PanelsSharingSpace.Explore, new ExplorePanelParameter(ExploreSections.Settings));
        }

        private async void OnInputShortcutsMapPerformedAsync(InputAction.CallbackContext obj)
        {
            if (!isExplorePanelVisible)
                await ShowAsync(PanelsSharingSpace.Explore, new ExplorePanelParameter(ExploreSections.Navmap));
        }

        private async void OnInputShortcutsMainMenuPerformedAsync(InputAction.CallbackContext obj)
        {
            if (!isExplorePanelVisible)
                await ShowAsync(PanelsSharingSpace.Explore, default(ExplorePanelParameter)); // No section provided, the panel will decide
        }

        private async void OnInputShortcutsEmoteWheelPerformedAsync(InputAction.CallbackContext obj)
        {
            if (!isExplorePanelVisible)
                await ToggleVisibilityAsync(PanelsSharingSpace.EmotesWheel, new ControllerNoData());
        }

        private async void OnInputShortcutsFriendPanelPerformedAsync(InputAction.CallbackContext obj)
        {
            if (!isExplorePanelVisible && isFriendsFeatureEnabled)
                await ToggleVisibilityAsync(PanelsSharingSpace.Friends, new FriendsPanelParameter());
        }

        private async void OnInputShortcutsOpenChatPerformedAsync(InputAction.CallbackContext obj)
        {
            if (!isExplorePanelVisible && !isTransitioning)
                await ToggleVisibilityAsync(PanelsSharingSpace.Chat, new ChatControllerShowParams(true, true));
        }

        private async void OnInputShortcutsCommunitiesPerformedAsync(InputAction.CallbackContext obj)
        {
            if (!isExplorePanelVisible && isCommunitiesFeatureEnabled)
                await ShowAsync(PanelsSharingSpace.Explore, new ExplorePanelParameter(ExploreSections.Communities));
        }

        private async void OnInputInWorldCameraToggledAsync(InputAction.CallbackContext obj)
        {
            // TODO: When we have more time, the InWorldCameraController and EmitInWorldCameraInputSystem and other stuff should be refactored and adapted properly
            if (isTransitioning)
                return;

            Entity camera = ecsWorld.CacheCamera();

            if (!ecsWorld.Has<InWorldCameraComponent>(camera))
                await HideAllAsync(PanelsSharingSpace.Chat);

            const string SOURCE_SHORTCUT = "Shortcut";
            ecsWorld.Add(camera, new ToggleInWorldCameraRequest { IsEnable = !ecsWorld.Has<InWorldCameraComponent>(camera), Source = SOURCE_SHORTCUT });
            // Clue: It is handled by ToggleInWorldCameraActivitySystem
        }
#endregion
    }
}
