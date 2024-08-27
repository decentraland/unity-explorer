using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Backpack;
using DCL.Input.Component;
using DCL.Input.UnityInputSystem.Blocks;
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
        private readonly INotificationsBusController notificationBusController;
        private readonly IMVCManager mvcManager;
        private readonly IInputBlock inputBlock;

        private Dictionary<ExploreSections, TabSelectorView> tabsBySections;
        private Dictionary<ExploreSections, ISection> exploreSections;

        private SectionSelectorController<ExploreSections> sectionSelectorController;
        private CancellationTokenSource animationCts;
        private CancellationTokenSource? profileWidgetCts;
        private CancellationTokenSource? profileMenuCts;
        private TabSelectorView previousSelector;
        private ExploreSections lastShownSection;

        private bool isControlClosing;

        public NavmapController NavmapController { get; }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Fullscreen;

        public ExplorePanelController(ViewFactoryMethod viewFactory,
            NavmapController navmapController,
            SettingsController settingsController,
            BackpackController backpackController,
            ProfileWidgetController profileWidgetController,
            ProfileMenuController profileMenuController,
            DCLInput dclInput,
            INotificationsBusController notificationBusController,
            IMVCManager mvcManager,
            IInputBlock inputBlock)
            : base(viewFactory)
        {
            NavmapController = navmapController;
            this.settingsController = settingsController;
            this.backpackController = backpackController;
            this.profileWidgetController = profileWidgetController;
            this.dclInput = dclInput;
            this.notificationBusController = notificationBusController;
            this.mvcManager = mvcManager;
            this.profileMenuController = profileMenuController;
            this.notificationBusController.SubscribeToNotificationTypeClick(NotificationType.REWARD_ASSIGNMENT, OnRewardAssigned);
            this.inputBlock = inputBlock;
        }

        private void OnRewardAssigned(object[] parameters)
        {
            mvcManager.ShowAsync(IssueCommand(new ExplorePanelParameter(ExploreSections.Backpack))).Forget();
            lastShownSection = ExploreSections.Backpack;
            OnBackpackHotkeyPressed(default(InputAction.CallbackContext));
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
            };

            sectionSelectorController = new SectionSelectorController<ExploreSections>(exploreSections, ExploreSections.Navmap);

            lastShownSection = ExploreSections.Navmap;

            foreach (KeyValuePair<ExploreSections, ISection> keyValuePair in exploreSections)
                keyValuePair.Value.Deactivate();

            tabsBySections = viewInstance.TabSelectorMappedViews.ToDictionary(map => map.Section, map => map.TabSelectorViews);

            foreach ((ExploreSections section, TabSelectorView? tabSelector) in tabsBySections)
            {
                tabSelector.TabSelectorToggle.onValueChanged.RemoveAllListeners();

                tabSelector.TabSelectorToggle.onValueChanged.AddListener(
                    isOn => { ToggleSection(isOn, tabSelector, section, true); }
                );
            }

            viewInstance.ProfileWidget.OpenProfileButton.onClick.AddListener(ShowProfileMenu);
        }

        protected override void OnViewShow()
        {
            isControlClosing = false;
            sectionSelectorController.ResetAnimators();

            foreach ((ExploreSections section, TabSelectorView? tab) in tabsBySections)
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
                sectionSelectorController.SetAnimationState(false, tabsBySections[lastShownSection]);

            animationCts.SafeCancelAndDispose();
            animationCts = new CancellationTokenSource();
            sectionSelectorController.OnTabSelectorToggleValueChangedAsync(isOn, tabSelectorView, shownSection, animationCts.Token, animate).Forget();

            if (isOn)
            {
                if (shownSection == lastShownSection)
                    exploreSections[lastShownSection].Activate();

                lastShownSection = shownSection;
            }
        }

        private void RegisterHotkeys()
        {
            dclInput.Shortcuts.MainMenu.performed += OnCloseMainMenu;
            dclInput.UI.Close.performed += OnCloseMainMenu;
            dclInput.Shortcuts.Map.performed += OnMapHotkeyPressed;
            dclInput.Shortcuts.Settings.performed += OnSettingsHotkeyPressed;
            dclInput.Shortcuts.Backpack.performed += OnBackpackHotkeyPressed;
        }

        private void OnCloseMainMenu(InputAction.CallbackContext obj)
        {
            profileMenuController.HideViewAsync(CancellationToken.None).Forget();
            isControlClosing = true;
        }

        private void OnMapHotkeyPressed(InputAction.CallbackContext obj)
        {
            if (lastShownSection != ExploreSections.Navmap)
            {
                sectionSelectorController.SetAnimationState(false, tabsBySections[lastShownSection]);
                ShowSection(ExploreSections.Navmap);
            }
            else
                isControlClosing = true;
        }

        private void OnSettingsHotkeyPressed(InputAction.CallbackContext obj)
        {
            if (lastShownSection != ExploreSections.Settings)
            {
                sectionSelectorController.SetAnimationState(false, tabsBySections[lastShownSection]);
                ShowSection(ExploreSections.Settings);
            }
            else
                isControlClosing = true;
        }

        private void OnBackpackHotkeyPressed(InputAction.CallbackContext obj)
        {
            if (lastShownSection != ExploreSections.Backpack)
            {
                sectionSelectorController.SetAnimationState(false, tabsBySections[lastShownSection]);
                ShowSection(ExploreSections.Backpack);
            }
            else
                isControlClosing = true;
        }

        private void ShowSection(ExploreSections section)
        {
            ToggleSection(true, tabsBySections[section], section, true);
        }

        protected override void OnViewClose()
        {
            foreach (ISection exploreSectionsValue in exploreSections.Values)
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
            dclInput.UI.Close.performed -= OnCloseMainMenu;
            dclInput.Shortcuts.Map.performed -= OnMapHotkeyPressed;
            dclInput.Shortcuts.Settings.performed -= OnSettingsHotkeyPressed;
            dclInput.Shortcuts.Backpack.performed -= OnBackpackHotkeyPressed;
        }

        private void BlockUnwantedInputs()
        {
            inputBlock.BlockInputs(InputMapComponent.Kind.Camera , InputMapComponent.Kind.Player);
        }

        private void UnblockUnwantedInputs()
        {
            inputBlock.UnblockInputs(InputMapComponent.Kind.Camera , InputMapComponent.Kind.Player);
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
