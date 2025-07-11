using Cysharp.Threading.Tasks;
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
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private CancellationTokenSource skyboxMenuCts = new ();

        public event IPanelInSharedSpace.ViewShowingCompleteDelegate? ViewShowingComplete;

        public SkyboxMenuController(ViewFactoryMethod viewFactory, SkyboxSettingsAsset skyboxSettings) : base(viewFactory)
        {
            this.skyboxSettings = skyboxSettings;
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

            skyboxSettings.TimeOfDayChanged += OnTimeOfDayChanged;
            skyboxSettings.DayCycleChanged += ToggleDayCycleEnabled;

            viewInstance!.CloseButton.onClick.AddListener(OnClose);

            viewInstance.TimeProgressionToggle.onValueChanged.AddListener(OnTimeProgressionToggleChanged);
            viewInstance.TimeSlider.onValueChanged.AddListener(OnTimeSliderValueChanged);

            ToggleDayCycleEnabled(skyboxSettings.IsDayCycleEnabled);
            OnTimeOfDayChanged(skyboxSettings.TimeOfDayNormalized);
        }

        private void ToggleDayCycleEnabled(bool isEnabled)
        {
            viewInstance!.TimeProgressionToggle.isOn = isEnabled;
            viewInstance.TopSliderGroup.enabled = isEnabled;
            viewInstance.TextSliderGroup.enabled = isEnabled;
        }

        private void OnTimeSliderValueChanged(float sliderValue)
        {
            skyboxSettings.TimeOfDayNormalized = sliderValue;
            viewInstance!.TimeText.text = GetFormatedTime(sliderValue);
        }

        private void OnTimeProgressionToggleChanged(bool isOn)
        {
            skyboxSettings.IsUIControlled = !isOn;

            ToggleDayCycleEnabled(isOn);
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

        public override void Dispose()
        {
            base.Dispose();
            skyboxMenuCts.SafeCancelAndDispose();

            skyboxSettings.TimeOfDayChanged -= OnTimeOfDayChanged;
            skyboxSettings.DayCycleChanged -= ToggleDayCycleEnabled;

            if (!viewInstance) return;
            viewInstance.CloseButton.onClick.RemoveAllListeners();
            viewInstance.TimeSlider.onValueChanged.RemoveAllListeners();
            viewInstance.TimeProgressionToggle.onValueChanged.RemoveAllListeners();
        }
    }
}
