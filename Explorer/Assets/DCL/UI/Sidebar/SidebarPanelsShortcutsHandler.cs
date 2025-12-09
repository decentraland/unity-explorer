using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.CharacterCamera;
using DCL.Communities;
using DCL.EmotesWheel;
using DCL.ExplorePanel;
using DCL.FeatureFlags;
using DCL.InWorldCamera;
using DCL.InWorldCamera.UI;
using DCL.UI.Controls;
using ECS.Abstract;
using MVC;
using System;
using System.Threading;
using UnityEngine.InputSystem;

namespace DCL.UI.Sidebar
{
    public class SidebarPanelsShortcutsHandler : IDisposable
    {
        private const float QUICK_EMOTE_LOCK_TIME = 0.5f;
        private const string SOURCE_SHORTCUT = "Shortcut";


        private readonly IMVCManager mvcManager;
        private readonly DCLInput dclInput;
        private readonly EmotesBus emotesBus;
        private readonly World world;
        private SingleInstanceEntity? camera => cameraInternal ??= world.CacheCamera();

        private float lastQuickEmoteTime;
        private bool isCommunitiesFeatureEnabled;
        private SingleInstanceEntity? cameraInternal;

        public SidebarPanelsShortcutsHandler(
            IMVCManager mvcManager,
            DCLInput dclInput,
            EmotesBus emotesBus,
            World world)
        {
            this.mvcManager = mvcManager;
            this.dclInput = dclInput;
            this.emotesBus = emotesBus;
            this.world = world;

            //TODO FRAN: Add proper CT here
            ConfigureShortcutsAsync(CancellationToken.None).Forget();
        }

        private async UniTaskVoid ConfigureShortcutsAsync(CancellationToken ct)
        {
            if (FeaturesRegistry.Instance.IsEnabled(FeatureId.FRIENDS))
                dclInput.Shortcuts.FriendPanel.performed += OnInputShortcutsFriendPanelPerformedAsync;

            //dclInput.Shortcuts.EmoteWheel.canceled += OnInputShortcutsEmoteWheelPerformedAsync;
            dclInput.Shortcuts.Controls.performed += OnInputShortcutsControlsPanelPerformedAsync;
            dclInput.UI.Submit.performed += OnUISubmitPerformedAsync;

            isCommunitiesFeatureEnabled = await CommunitiesFeatureAccess.Instance.IsUserAllowedToUseTheFeatureAsync(ct);

            if (isCommunitiesFeatureEnabled)
                dclInput.Shortcuts.Communities.performed += OnInputShortcutsCommunitiesPerformed;

            if (FeaturesRegistry.Instance.IsEnabled(FeatureId.CAMERA_REEL))
                dclInput.InWorldCamera.ToggleInWorldCamera.performed += OnInputInWorldCameraToggled;
        }

        private async void OnUISubmitPerformedAsync(InputAction.CallbackContext obj)
        {
            //if (IsRegistered(PanelsSharingSpace.Chat) && !isExplorePanelVisible && !isChatBlockerVisible)
            //  await ShowAsync(PanelsSharingSpace.Chat, new ChatMainSharedAreaControllerShowParams(true, true));
        }

        private async void OnInputShortcutsControlsPanelPerformedAsync(InputAction.CallbackContext obj)
        {
            //mvcManager.ToggleAsync(ControlsPanelController.IssueCommand()).Forget();
        }


        private async void OnInputShortcutsEmoteWheelPerformedAsync(InputAction.CallbackContext obj)
        {
            if (IsEmoteWheelLocked())
            {
                // Reset time, we only want to stop one action.
                lastQuickEmoteTime = 0;
                return;
            }

            //mvcManager.ToggleAsync(EmotesWheelController.IssueCommand()).Forget();
        }


        private async void OnInputShortcutsFriendPanelPerformedAsync(InputAction.CallbackContext obj)
        {
            /*if (!isExplorePanelVisible && isFriendsFeatureEnabled)
                await ToggleVisibilityAsync(PanelsSharingSpace.Friends, new FriendsPanelParameter());*/
        }

        private void OnInputShortcutsCommunitiesPerformed(InputAction.CallbackContext obj)
        {
            if (isCommunitiesFeatureEnabled)
                mvcManager.ShowAndForget(ExplorePanelController.IssueCommand(new ExplorePanelParameter(ExploreSections.Communities)));
        }


        private void OnInputInWorldCameraToggled(InputAction.CallbackContext obj)
        {
            // Note: The following comment was in the original code and I preserved it because I agree, opening a window should not require adding a component...
            // TODO: When we have more time, the InWorldCameraController and EmitInWorldCameraInputSystem and other stuff should be refactored and adapted properly
            if (world.Get<CameraComponent>(camera!.Value).CameraInputChangeEnabled && !world.Has<ToggleInWorldCameraRequest>(camera!.Value))
                world.Add(camera!.Value, new ToggleInWorldCameraRequest { IsEnable = !world.Has<InWorldCameraComponent>(camera!.Value), Source = SOURCE_SHORTCUT });
        }

        /// <summary>
        /// Emote wheel is locked when quick emote action was executed, but not when wheel is already visible, in that
        /// case we want to hide it.
        /// </summary>
        private bool IsEmoteWheelLocked()
        {
            /*bool isPanelVisible = registrations[PanelsSharingSpace.EmotesWheel].panel.IsVisibleInSharedSpace;
            return !isPanelVisible && lastQuickEmoteTime + QUICK_EMOTE_LOCK_TIME > UnityEngine.Time.time;*/
            return false;
        }


        public void Dispose()
        {
        }
    }
}
