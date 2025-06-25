using Cysharp.Threading.Tasks;
using DCL.StylizedSkybox.Scripts;
using DCL.UI.SharedSpaceManager;
using MVC;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.UI.Skybox
{
    public class SkyboxMenuController : ControllerBase<SkyboxMenuView>, IControllerInSharedSpace<SkyboxMenuView>
    {
        private readonly StylizedSkyboxSettingsAsset skyboxSettings;
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private CancellationTokenSource skyboxMenuCts = new ();

        public event IPanelInSharedSpace.ViewShowingCompleteDelegate? ViewShowingComplete;

        public SkyboxMenuController(ViewFactoryMethod viewFactory, StylizedSkyboxSettingsAsset skyboxSettings) : base(viewFactory)
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

            viewInstance!.CloseButton.onClick.AddListener(OnClose);

            viewInstance.TimeSlider.onValueChanged.AddListener(OnTimeOfDaySliderValueChanged);
            viewInstance.TimeProgressionToggle.onValueChanged.AddListener(OnTimeProgressionToggleValueChanged);

            skyboxSettings.TimeOfDayChanged += OnTimeOfDayChanged;
            skyboxSettings.DayCycleEnabledChanged += OnDayCycleEnabledChanged;

            viewInstance.TimeProgressionToggle.SetIsOnWithoutNotify(skyboxSettings.IsDayCycleEnabled);
            viewInstance!.TimeSlider.SetValueWithoutNotify(skyboxSettings.TimeOfDayNormalized);
            viewInstance.TimeText.text = GetFormatedTime(skyboxSettings.TimeOfDayNormalized);
        }

        private void OnDayCycleEnabledChanged(bool cycleEnabled)
        {
            viewInstance!.TimeProgressionToggle.isOn = cycleEnabled;
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();
            skyboxMenuCts = skyboxMenuCts.SafeRestart();
        }

        private void OnTimeProgressionToggleValueChanged(bool dayCycleEnabled)
        {
            skyboxSettings.DayCycleEnabledChanged -= OnDayCycleEnabledChanged;

            skyboxSettings.IsDayCycleEnabled = dayCycleEnabled;
            skyboxSettings.SkyboxTimeSource = dayCycleEnabled ? SkyboxTimeSource.GLOBAL : SkyboxTimeSource.PLAYER_FIXED;
            SetTimeSliderEnabled(!skyboxSettings.IsDayCycleEnabled);

            skyboxSettings.DayCycleEnabledChanged += OnDayCycleEnabledChanged;
        }

        private void OnTimeOfDayChanged(float time)
        {
            viewInstance!.TimeSlider.SetValueWithoutNotify(time);
            viewInstance!.TimeText.text = GetFormatedTime(time);
        }

        private void OnTimeOfDaySliderValueChanged(float time)
        {
            skyboxSettings.TimeOfDayNormalized = time;
        }

        private void SetTimeSliderEnabled(bool enabled)//rename to controls
        {
            viewInstance!.TopSliderGroup.enabled = !enabled;
            viewInstance!.TextSliderGroup.enabled = !enabled;
        }

        private string GetFormatedTime(float time)
        {
            int totalMinutes = (int)Mathf.Round(time * StylizedSkyboxSettingsAsset.TOTAL_MINUTES_IN_DAY);

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
            skyboxSettings.DayCycleEnabledChanged -= OnDayCycleEnabledChanged;

            if(!viewInstance) return;
            viewInstance.CloseButton.onClick.RemoveAllListeners();
            viewInstance.TimeSlider.onValueChanged.RemoveAllListeners();
            viewInstance.TimeProgressionToggle.onValueChanged.RemoveAllListeners();
        }
    }
}
