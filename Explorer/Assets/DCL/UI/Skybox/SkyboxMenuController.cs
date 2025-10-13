using Cysharp.Threading.Tasks;
using DCL.SceneRestrictionBusController.SceneRestriction;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.SkyBox;
using DCL.UI.SharedSpaceManager;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.UI.Skybox
{
    public class SkyboxMenuController : ControllerBase<SkyboxMenuView>, IControllerInSharedSpace<SkyboxMenuView>
    {
        private readonly SkyboxSettingsAsset skyboxSettings;
        private readonly ISceneRestrictionBusController sceneRestrictionBusController;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private CancellationTokenSource skyboxMenuCts = new ();
        private bool? pendingInteractableState;

        public event IPanelInSharedSpace.ViewShowingCompleteDelegate? ViewShowingComplete;

        public SkyboxMenuController(ViewFactoryMethod viewFactory, SkyboxSettingsAsset skyboxSettings, ISceneRestrictionBusController sceneRestrictionBusController) : base(viewFactory)
        {
            this.skyboxSettings = skyboxSettings;
            this.sceneRestrictionBusController = sceneRestrictionBusController;
            this.sceneRestrictionBusController.SubscribeToSceneRestriction(OnSceneRestrictionChanged);
        }

        public override void Dispose()
        {
            base.Dispose();
            skyboxMenuCts.SafeCancelAndDispose();

            skyboxSettings.TimeOfDayChanged -= OnTimeOfDayChanged;
            skyboxSettings.DayCycleChanged -= OnDayCycleChanged;
            sceneRestrictionBusController.UnsubscribeToSceneRestriction(OnSceneRestrictionChanged);

            if (!viewInstance) return;
            viewInstance.CloseButton.onClick.RemoveAllListeners();
            viewInstance.TimeSlider.onValueChanged.RemoveAllListeners();
            viewInstance.TimeProgressionToggle.onValueChanged.RemoveAllListeners();
        }

        public async UniTask OnHiddenInSharedSpaceAsync(CancellationToken ct)
        {
            skyboxMenuCts.Cancel();

            await UniTask.WaitUntil(() => State == ControllerState.ViewHidden, PlayerLoopTiming.Update, ct);
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            ViewShowingComplete?.Invoke(this);
            await UniTask.WaitUntilCanceled(skyboxMenuCts.Token);
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            skyboxSettings.DayCycleChanged += OnDayCycleChanged;
            skyboxSettings.TimeOfDayChanged += OnTimeOfDayChanged;

            viewInstance!.CloseButton.onClick.AddListener(OnClose);

            viewInstance.TimeProgressionToggle.onValueChanged.AddListener(OnTimeProgressionToggleChanged);
            viewInstance.TimeSlider.onValueChanged.AddListener(OnTimeSliderValueChanged);

            if (pendingInteractableState.HasValue)
                SetInteractable(pendingInteractableState.Value);

            OnDayCycleChanged(skyboxSettings.IsDayCycleEnabled);
            OnTimeOfDayChanged(skyboxSettings.TimeOfDayNormalized);
        }

        private void OnDayCycleChanged(bool isEnabled)
        {
            viewInstance!.TimeProgressionToggle.isOn = isEnabled;
            viewInstance.TopSliderGroup.enabled = isEnabled;
            viewInstance.TextSliderGroup.enabled = isEnabled;
        }

        private void OnTimeSliderValueChanged(float sliderValue)
        {
            skyboxSettings.TimeOfDayNormalized = sliderValue;
            skyboxSettings.TargetTimeOfDayNormalized = sliderValue;
            skyboxSettings.UIOverrideTimeOfDayNormalized = sliderValue;
        }

        private void OnTimeProgressionToggleChanged(bool isOn)
        {
            skyboxSettings.IsUIControlled = !isOn;

            if (skyboxSettings.IsUIControlled)
                skyboxSettings.UIOverrideTimeOfDayNormalized = viewInstance!.TimeSlider.normalizedValue;

            viewInstance!.TopSliderGroup.enabled = isOn;
            viewInstance.TextSliderGroup.enabled = isOn;
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();
            skyboxMenuCts = skyboxMenuCts.SafeRestart();
        }

        private void OnTimeOfDayChanged(float time)
        {
            viewInstance!.TimeSlider.SetValueWithoutNotify(time);
            viewInstance!.TimeText.text = GetFormatedTime(time);
        }

        private static string GetFormatedTime(float time)
        {
            int totalMinutes = (int)Mathf.Round(time * SkyboxSettingsAsset.TOTAL_MINUTES_IN_DAY);

            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;

            return $"{hours:00}:{minutes:00}";
        }

        private void OnClose()
        {
            skyboxMenuCts.Cancel();
        }

        private void OnSceneRestrictionChanged(SceneRestriction restriction)
        {
            if (restriction.Type == SceneRestrictions.SKYBOX_TIME_UI_BLOCKED)
                SetInteractable(restriction.Action != SceneRestrictionsAction.APPLIED);
        }

        private void SetInteractable(bool isInteractable)
        {
            if (viewInstance == null)
            {
                pendingInteractableState = isInteractable;
                return;
            }

            viewInstance.TimeSlider.interactable = isInteractable;
            viewInstance.TimeProgressionToggle.interactable = isInteractable;

            // When enabled these groups display controls as "disabled"
            viewInstance.TimeProgressionGroup.enabled = !isInteractable;
            viewInstance.TopSliderGroup.enabled = !isInteractable;
            viewInstance.TextSliderGroup.enabled = !isInteractable;
        }
    }
}
