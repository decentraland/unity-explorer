using Cysharp.Threading.Tasks;
using DCL.StylizedSkybox.Scripts;
using DCL.UI.SharedSpaceManager;
using MVC;
using System.Threading;
using Utility;

namespace DCL.UI.Skybox
{
    public class SkyboxMenuController : ControllerBase<SkyboxMenuView>, IControllerInSharedSpace<SkyboxMenuView>
    {
        private const int SECONDS_IN_DAY = 86400;

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

            viewInstance.TimeSlider.value = skyboxSettings.NormalizedTime;
            skyboxSettings.NormalizedTimeChanged += OnNormalizedTimeChanged;
            viewInstance.TimeSlider.onValueChanged.AddListener(OnTimeSliderValueChanged);

            viewInstance.DynamicToggle.isOn = skyboxSettings.UseDynamicTime;
            skyboxSettings.UseDynamicTimeChanged += OnUseDynamicTimeChanged;
            viewInstance.DynamicToggle.onValueChanged.AddListener(OnDynamicToggleValueChanged);

            SetTimeEnabled(!skyboxSettings.UseDynamicTime);
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();
            skyboxMenuCts = skyboxMenuCts.SafeRestart();
        }

        private void OnUseDynamicTimeChanged(bool dynamic)
        {
            SetTimeEnabled(!dynamic);
            viewInstance!.DynamicToggle.isOn = dynamic;
        }

        private void OnDynamicToggleValueChanged(bool dynamic)
        {
            skyboxSettings.UseDynamicTime = dynamic;
        }

        private void OnNormalizedTimeChanged(float time)
        {
            viewInstance!.TimeSlider.SetValueWithoutNotify(time);
            viewInstance.TimeText.text = GetFormatedTime(time);
        }

        private void OnTimeSliderValueChanged(float time)
        {
            skyboxSettings.UseDynamicTime = false;
            skyboxSettings.NormalizedTime = time;
        }

        private void SetTimeEnabled(bool enabled)
        {
            viewInstance!.TopSliderGroup.enabled = !enabled;
            viewInstance!.TextSliderGroup.enabled = !enabled;
        }

        private string GetFormatedTime(float time)
        {
            // We need to subtract 1 second to SECONDS_IN_DAY to make the slider range is between 00:00 and 23:59, instead of 00:00 and 24:00
            var totalSec = (int)(time * (SECONDS_IN_DAY - 1));

            int hours = totalSec / 3600;
            int minutes = totalSec % 3600 / 60;
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
            skyboxSettings.UseDynamicTimeChanged -= OnUseDynamicTimeChanged;
            skyboxSettings.NormalizedTimeChanged -= OnNormalizedTimeChanged;
        }
    }
}
