using Cysharp.Threading.Tasks;
using DCL.SceneRestrictionBusController.SceneRestriction;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.SkyBox;
using MVC;
using System.Threading;
using UnityEngine;

namespace DCL.UI.Skybox
{
    public class SkyboxMenuController : ControllerBase<SkyboxMenuView>
    {
        private readonly SkyboxSettingsAsset skyboxSettings;
        private readonly ISceneRestrictionBusController sceneRestrictionBusController;

        private UniTaskCompletionSource? closeViewTask;
        private bool? pendingInteractableState;
        private bool isRestrictedByScene;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public SkyboxMenuController(ViewFactoryMethod viewFactory, SkyboxSettingsAsset skyboxSettings, ISceneRestrictionBusController sceneRestrictionBusController) : base(viewFactory)
        {
            this.skyboxSettings = skyboxSettings;
            this.sceneRestrictionBusController = sceneRestrictionBusController;
            this.sceneRestrictionBusController.SubscribeToSceneRestriction(OnSceneRestrictionChanged);
        }

        public override void Dispose()
        {
            base.Dispose();

            skyboxSettings.TimeOfDayChanged -= OnTimeOfDayChanged;
            skyboxSettings.DayCycleChanged -= OnDayCycleChanged;
            sceneRestrictionBusController.UnsubscribeToSceneRestriction(OnSceneRestrictionChanged);

            if (!viewInstance) return;
            viewInstance.TimeSlider.onValueChanged.RemoveAllListeners();
            viewInstance.TimeProgressionToggle.onValueChanged.RemoveAllListeners();
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            closeViewTask = new UniTaskCompletionSource();
            await closeViewTask.Task.AttachExternalCancellation(ct).SuppressCancellationThrow();
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            skyboxSettings.DayCycleChanged += OnDayCycleChanged;
            skyboxSettings.TimeOfDayChanged += OnTimeOfDayChanged;

            viewInstance!.TimeProgressionToggle.isOn = false;
            viewInstance.TimeProgressionToggle.onValueChanged.AddListener(OnTimeProgressionToggleChanged);
            viewInstance.TimeSlider.onValueChanged.AddListener(OnTimeSliderValueChanged);

            if (pendingInteractableState.HasValue)
                SetInteractable(pendingInteractableState.Value);

            OnDayCycleChanged(skyboxSettings.IsDayCycleEnabled);
            OnTimeOfDayChanged(skyboxSettings.TimeOfDayNormalized);
        }

        private void OnDayCycleChanged(bool isEnabled) =>
            viewInstance!.TimeProgressionToggle.isOn = isEnabled;

        private void OnTimeSliderValueChanged(float sliderValue)
        {
            skyboxSettings.TimeOfDayNormalized = sliderValue;
            skyboxSettings.TargetTimeOfDayNormalized = sliderValue;
            skyboxSettings.UIOverrideTimeOfDayNormalized = sliderValue;
        }

        private void OnTimeProgressionToggleChanged(bool isOn)
        {
            if (isRestrictedByScene) return;

            SetTimeSliderInteractable(!isOn);
            skyboxSettings.IsUIControlled = !isOn;

            if (skyboxSettings.IsUIControlled)
                skyboxSettings.UIOverrideTimeOfDayNormalized = viewInstance!.TimeSlider.normalizedValue;
        }

        protected override void OnViewClose()
        {
            closeViewTask?.TrySetCanceled();
        }

        private void OnTimeOfDayChanged(float time)
        {
            viewInstance!.TimeSlider.SetValueWithoutNotify(time);
            viewInstance!.TimeText.text = GetFormatedTime(time);
        }

        private static string GetFormatedTime(float time)
        {
            var totalMinutes = (int)Mathf.Round(time * SkyboxSettingsAsset.TOTAL_MINUTES_IN_DAY);

            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;

            return $"{hours:00}:{minutes:00}";
        }

        private void OnSceneRestrictionChanged(SceneRestriction restriction)
        {
            if (restriction.Type != SceneRestrictions.SKYBOX_TIME_UI_BLOCKED) return;

            isRestrictedByScene = restriction.Action == SceneRestrictionsAction.APPLIED;
            SetInteractable(!isRestrictedByScene);
        }

        private void SetInteractable(bool isInteractable)
        {
            if (!viewInstance)
            {
                pendingInteractableState = isInteractable;
                return;
            }

            SetTimeProgressionInteractable(isInteractable);
            SetTimeSliderInteractable(isInteractable);
        }

        private void SetTimeProgressionInteractable(bool isInteractable)
        {
            viewInstance!.TimeProgressionToggle.interactable = isInteractable;
            viewInstance.TimeProgressionGroup.enabled = !isInteractable;
        }

        private void SetTimeSliderInteractable(bool isInteractable)
        {
            viewInstance!.TimeSlider.interactable = isInteractable;
            viewInstance.TopSliderGroup.enabled = !isInteractable;
            viewInstance.TextSliderGroup.enabled = !isInteractable;
        }
    }
}
