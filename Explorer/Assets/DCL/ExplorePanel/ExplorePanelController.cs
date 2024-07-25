using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Backpack;
using DCL.CharacterMotion.Components;
using DCL.Navmap;
using DCL.Notification;
using DCL.Notification.NotificationsBus;
using DCL.Settings;
using DCL.UI;
using MVC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine.InputSystem;
using Utility;
using Utility.Arch;

namespace DCL.ExplorePanel
{
    public class ExplorePanelController : ControllerBase<ExplorePanelView, ExplorePanelParameter>
    {
        private readonly SettingsController settingsController;
        private readonly BackpackController backpackController;
        private readonly Entity playerEntity;
        private readonly World world;
        private readonly ProfileWidgetController profileWidgetController;
        private readonly SystemMenuController systemMenuController;
        private readonly DCLInput dclInput;
        private readonly INotificationsBusController notificationBusController;
        private readonly IMVCManager mvcManager;
        private Dictionary<ExploreSections, TabSelectorView> tabsBySections;
        private Dictionary<ExploreSections, ISection> exploreSections;

        private SectionSelectorController<ExploreSections> sectionSelectorController;
        private CancellationTokenSource animationCts;
        private CancellationTokenSource? profileWidgetCts;
        private CancellationTokenSource? systemMenuCts;
        private TabSelectorView previousSelector;
        private ExploreSections lastShownSection;

        private bool isControlClosing;

        public NavmapController NavmapController { get; }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Fullscreen;

        public ExplorePanelController(ViewFactoryMethod viewFactory,
            NavmapController navmapController,
            SettingsController settingsController,
            BackpackController backpackController,
            Entity playerEntity,
            World world,
            ProfileWidgetController profileWidgetController,
            SystemMenuController systemMenuController,
            DCLInput dclInput,
            INotificationsBusController notificationBusController,
            IMVCManager mvcManager)
            : base(viewFactory)
        {
            NavmapController = navmapController;
            this.settingsController = settingsController;
            this.backpackController = backpackController;
            this.playerEntity = playerEntity;
            this.world = world;
            this.profileWidgetController = profileWidgetController;
            this.systemMenuController = systemMenuController;
            this.dclInput = dclInput;
            this.notificationBusController = notificationBusController;
            this.mvcManager = mvcManager;
            this.notificationBusController.SubscribeToNotificationType(NotificationType.REWARD_ASSIGNMENT, OnRewardAssigned);
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
            systemMenuCts.SafeCancelAndDispose();
        }

        protected override void OnViewInstantiated()
        {
            exploreSections = new Dictionary<ExploreSections, ISection>
            {
                { ExploreSections.Navmap, NavmapController },
                { ExploreSections.Settings, settingsController },
                { ExploreSections.Backpack, backpackController },
            };

            NavmapController.OnSetDestination += SetDestination;

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

            viewInstance.ProfileWidget.OpenProfileButton.onClick.AddListener(ShowSystemMenu);
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

            if (systemMenuController.State is ControllerState.ViewFocused or ControllerState.ViewBlurred)
                systemMenuController.HideViewAsync(CancellationToken.None).Forget();

            BlockUnwantedActions();
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

            profileWidgetCts.SafeCancelAndDispose();
            systemMenuCts.SafeCancelAndDispose();

            UnblockUnwantedActions();
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

        private void BlockUnwantedActions()
        {
            world.AddOrGet<CameraBlockerComponent>(playerEntity);
            world.AddOrGet<MovementBlockerComponent>(playerEntity);
            dclInput.Camera.Disable();
        }

        private void UnblockUnwantedActions()
        {
            world.TryRemove<CameraBlockerComponent>(playerEntity);
            world.TryRemove<MovementBlockerComponent>(playerEntity);
            dclInput.Camera.Enable();
        }

        private void SetDestination()
        {
            viewInstance.CloseButton.onClick.Invoke();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            return UniTask.WhenAny(viewInstance.CloseButton.OnClickAsync(ct),
                UniTask.WaitUntil(() => isControlClosing, PlayerLoopTiming.Update, ct),
                viewInstance.SystemMenu.LogoutButton.OnClickAsync(ct));
        }

        private void ShowSystemMenu()
        {
            systemMenuCts = systemMenuCts.SafeRestart();

            async UniTaskVoid ShowSystemMenuAsync(CancellationToken ct)
            {
                await systemMenuController.LaunchViewLifeCycleAsync(new CanvasOrdering(CanvasOrdering.SortingLayer.Overlay, 0),
                    new ControllerNoData(), ct);

                await systemMenuController.HideViewAsync(ct);
            }

            if (systemMenuController.State is ControllerState.ViewFocused or ControllerState.ViewBlurred)
                systemMenuController.HideViewAsync(systemMenuCts.Token).Forget();
            else
                ShowSystemMenuAsync(systemMenuCts.Token).Forget();
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
