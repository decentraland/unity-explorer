using Cysharp.Threading.Tasks;
using DCL.Backpack;
using DCL.Chat;
using DCL.Chat.ChatLifecycleBus;
using DCL.ExplorePanel.Components;
using DCL.Input;
using DCL.Input.Component;
using DCL.InWorldCamera.CameraReelGallery;
using DCL.Navmap;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.NotificationsBusController.NotificationTypes;
using DCL.Settings;
using DCL.UI;
using DCL.UI.ProfileElements;
using MVC;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Utility;

namespace DCL.ExplorePanel
{
    public class ExplorePanelController : ControllerBase<ExplorePanelView, ExplorePanelParameter>
    {
        private readonly SettingsController settingsController;
        private readonly BackpackController backpackController;
        private readonly ProfileWidgetController profileWidgetController;
        private readonly ProfileMenuController profileMenuController;
        private readonly DCLInput dclInput;
        private readonly IExplorePanelEscapeAction explorePanelEscapeAction;
        private readonly IMVCManager mvcManager;
        private readonly IInputBlock inputBlock;
        private readonly bool includeCameraReel;
        private readonly IChatLifecycleBusController chatLifecycleBusController;

        private Dictionary<ExploreSections, TabSelectorView> tabsBySections;
        private Dictionary<ExploreSections, ISection> exploreSections;
        private SectionSelectorController<ExploreSections> sectionSelectorController;
        private CancellationTokenSource? animationCts;
        private CancellationTokenSource? profileWidgetCts;
        private CancellationTokenSource? profileMenuCts;
        private TabSelectorView? previousSelector;
        private ExploreSections lastShownSection;
        private bool isControlClosing;

        public NavmapController NavmapController { get; }
        public CameraReelController CameraReelController { get; }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Fullscreen;

        public ExplorePanelController(ViewFactoryMethod viewFactory,
            NavmapController navmapController,
            SettingsController settingsController,
            BackpackController backpackController,
            CameraReelController cameraReelController,
            ProfileWidgetController profileWidgetController,
            ProfileMenuController profileMenuController,
            DCLInput dclInput,
            IExplorePanelEscapeAction explorePanelEscapeAction,
            INotificationsBusController notificationBusController,
            IMVCManager mvcManager,
            IInputBlock inputBlock,
            bool includeCameraReel,
            IChatLifecycleBusController chatLifecycleBusController)
            : base(viewFactory)
        {
            NavmapController = navmapController;
            this.settingsController = settingsController;
            this.backpackController = backpackController;
            CameraReelController = cameraReelController;
            this.profileWidgetController = profileWidgetController;
            this.dclInput = dclInput;
            this.explorePanelEscapeAction = explorePanelEscapeAction;
            this.mvcManager = mvcManager;
            this.profileMenuController = profileMenuController;
            notificationBusController.SubscribeToNotificationTypeClick(NotificationType.REWARD_ASSIGNMENT, OnRewardAssigned);
            this.inputBlock = inputBlock;
            this.includeCameraReel = includeCameraReel;
            this.chatLifecycleBusController = chatLifecycleBusController;
        }

        private void OnRewardAssigned(object[] parameters)
        {
            mvcManager.ShowAsync(IssueCommand(new ExplorePanelParameter(ExploreSections.Backpack))).Forget();
            ShowSection(ExploreSections.Backpack);
        }

        public override void Dispose()
        {
            base.Dispose();

            profileWidgetCts.SafeCancelAndDispose();
            profileMenuCts.SafeCancelAndDispose();
        }

        protected override void OnViewInstantiated()
        {
            exploreSections = new Dictionary<ExploreSections, ISection>
            {
                { ExploreSections.Navmap, NavmapController },
                { ExploreSections.Settings, settingsController },
                { ExploreSections.Backpack, backpackController },
                { ExploreSections.CameraReel, CameraReelController },
            };

            sectionSelectorController = new SectionSelectorController<ExploreSections>(exploreSections, ExploreSections.Navmap);

            lastShownSection = ExploreSections.Navmap;

            foreach (KeyValuePair<ExploreSections, ISection> keyValuePair in exploreSections)
                keyValuePair.Value.Deactivate();

            tabsBySections = viewInstance!.TabSelectorMappedViews.ToDictionary(map => map.Section, map => map.TabSelectorViews);

            foreach ((ExploreSections section, TabSelectorView? tabSelector) in tabsBySections)
            {
                tabSelector.TabSelectorToggle.onValueChanged.RemoveAllListeners();

                if (section == ExploreSections.CameraReel && !includeCameraReel)
                {
                    tabSelector.gameObject.SetActive(false);
                    continue;
                }

                tabSelector.TabSelectorToggle.onValueChanged.AddListener(
                    isOn => { ToggleSection(isOn, tabSelector, section, true); }
                );
            }

            viewInstance.ProfileWidget.OpenProfileButton.onClick.AddListener(ShowProfileMenu);
        }

        protected override void OnViewShow()
        {
            isControlClosing = false;
            sectionSelectorController!.ResetAnimators();

            foreach ((ExploreSections section, TabSelectorView? tab) in tabsBySections!)
            {
                ToggleSection(section == inputData.Section, tab, section, true);
                sectionSelectorController.SetAnimationState(section == inputData.Section, tabsBySections[section]);
            }

            if (inputData.BackpackSection != null)
                backpackController.Toggle(inputData.BackpackSection.Value);

            profileWidgetCts = profileWidgetCts.SafeRestart();

            profileWidgetController.LaunchViewLifeCycleAsync(new CanvasOrdering(CanvasOrdering.SortingLayer.Persistent, 0),
                                        new ControllerNoData(), profileWidgetCts.Token)
                                   .Forget();

            if (profileMenuController.State is ControllerState.ViewFocused or ControllerState.ViewBlurred)
                profileMenuController.HideViewAsync(CancellationToken.None).Forget();

            BlockUnwantedInputs();
            RegisterHotkeys();
        }

        private void ToggleSection(bool isOn, TabSelectorView tabSelectorView, ExploreSections shownSection, bool animate)
        {
            if (isOn && animate && shownSection != lastShownSection)
                sectionSelectorController!.SetAnimationState(false, tabsBySections![lastShownSection]);

            animationCts.SafeCancelAndDispose();
            animationCts = new CancellationTokenSource();
            sectionSelectorController!.OnTabSelectorToggleValueChangedAsync(isOn, tabSelectorView, shownSection, animationCts.Token, animate).Forget();

            if (!isOn) return;

            if (shownSection == lastShownSection)
                exploreSections![lastShownSection].Activate();

            lastShownSection = shownSection;
        }

        private void RegisterHotkeys()
        {
            dclInput.Shortcuts.MainMenu.performed += OnCloseMainMenu;
            explorePanelEscapeAction.RegisterEscapeAction(OnCloseMainMenu);
            dclInput.Shortcuts.Map.performed += OnMapHotkeyPressed;
            dclInput.Shortcuts.Settings.performed += OnSettingsHotkeyPressed;
            dclInput.Shortcuts.Backpack.performed += OnBackpackHotkeyPressed;
            dclInput.Shortcuts.OpenChat.performed += OnOpenChatHotkeyPressed;
            dclInput.InWorldCamera.CameraReel.performed += OnCameraReelHotkeyPressed;
        }

        private void OnOpenChatHotkeyPressed(InputAction.CallbackContext obj)
        {
       //     chatLifecycleBusController.ToggleChat();
        }

        private void OnCameraReelHotkeyPressed(InputAction.CallbackContext ctx)
        {
            if (!includeCameraReel) return;

            if (lastShownSection != ExploreSections.CameraReel)
            {
                sectionSelectorController.SetAnimationState(false, tabsBySections[lastShownSection]);
                ShowSection(ExploreSections.CameraReel);
            }
            else
                isControlClosing = true;
        }

        private void OnCloseMainMenu(InputAction.CallbackContext obj)
        {
            // Search bar could be focused when closing the menu, so we need to remove the focus,
            // which will also re-enable shortcuts
            EventSystem.current.SetSelectedGameObject(null);

            profileMenuController.HideViewAsync(CancellationToken.None).Forget();
            isControlClosing = true;
        }

        private void OnMapHotkeyPressed(InputAction.CallbackContext obj)
        {
            if (lastShownSection != ExploreSections.Navmap)
            {
                sectionSelectorController!.SetAnimationState(false, tabsBySections![lastShownSection]);
                ShowSection(ExploreSections.Navmap);
            }
            else
                isControlClosing = true;
        }

        private void OnSettingsHotkeyPressed(InputAction.CallbackContext obj)
        {
            if (lastShownSection != ExploreSections.Settings)
            {
                sectionSelectorController!.SetAnimationState(false, tabsBySections![lastShownSection]);
                ShowSection(ExploreSections.Settings);
            }
            else
                isControlClosing = true;
        }

        private void OnBackpackHotkeyPressed(InputAction.CallbackContext obj)
        {
            if (lastShownSection != ExploreSections.Backpack)
            {
                sectionSelectorController!.SetAnimationState(false, tabsBySections![lastShownSection]);
                ShowSection(ExploreSections.Backpack);
            }
            else
                isControlClosing = true;
        }

        private void ShowSection(ExploreSections section)
        {
            ToggleSection(true, tabsBySections![section], section, true);
        }

        protected override void OnViewClose()
        {
            foreach (ISection exploreSectionsValue in exploreSections!.Values)
                exploreSectionsValue.Deactivate();

            if (profileMenuController.State is ControllerState.ViewFocused or ControllerState.ViewBlurred)
                profileMenuController.HideViewAsync(CancellationToken.None).Forget();

            profileWidgetCts.SafeCancelAndDispose();
            profileMenuCts.SafeCancelAndDispose();

            UnblockUnwantedInputs();
            UnRegisterHotkeys();
        }

        private void UnRegisterHotkeys()
        {
            dclInput.Shortcuts.MainMenu.performed -= OnCloseMainMenu;
            explorePanelEscapeAction.RemoveEscapeAction(OnCloseMainMenu);
            dclInput.Shortcuts.Map.performed -= OnMapHotkeyPressed;
            dclInput.Shortcuts.Settings.performed -= OnSettingsHotkeyPressed;
            dclInput.Shortcuts.Backpack.performed -= OnBackpackHotkeyPressed;
            dclInput.Shortcuts.OpenChat.performed -= OnOpenChatHotkeyPressed;
            dclInput.InWorldCamera.CameraReel.performed -= OnCameraReelHotkeyPressed;
        }

        private void BlockUnwantedInputs()
        {
            inputBlock.Disable(InputMapComponent.Kind.CAMERA, InputMapComponent.Kind.PLAYER);
        }

        private void UnblockUnwantedInputs()
        {
            inputBlock.Enable(InputMapComponent.Kind.CAMERA, InputMapComponent.Kind.PLAYER);
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            return UniTask.WhenAny(viewInstance!.CloseButton.OnClickAsync(ct),
                UniTask.WaitUntil(() => isControlClosing, PlayerLoopTiming.Update, ct),
                viewInstance.ProfileMenuView.SystemMenuView.LogoutButton.OnClickAsync(ct));
        }

        private void ShowProfileMenu()
        {
            profileMenuCts = profileMenuCts.SafeRestart();

            async UniTaskVoid ShowProfileMenuAsync(CancellationToken ct)
            {
                await profileMenuController.LaunchViewLifeCycleAsync(new CanvasOrdering(CanvasOrdering.SortingLayer.Overlay, 0),
                    new ControllerNoData(), ct);

                await profileMenuController.HideViewAsync(ct);
            }

            if (profileMenuController.State is ControllerState.ViewFocused or ControllerState.ViewBlurred)
                profileMenuController.HideViewAsync(profileMenuCts.Token).Forget();
            else
                ShowProfileMenuAsync(profileMenuCts.Token).Forget();
        }
    }

    public readonly struct ExplorePanelParameter
    {
        public readonly ExploreSections Section;
        public readonly BackpackSections? BackpackSection;

        public ExplorePanelParameter(ExploreSections section, BackpackSections? backpackSection = null)
        {
            Section = section;
            BackpackSection = backpackSection;
        }
    }
}
