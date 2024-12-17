using Cysharp.Threading.Tasks;
using DCL.StylizedSkybox.Scripts;
using MVC;
using System.Threading;
using Utility;

namespace DCL.UI.Skybox
{
    public class SkyboxMenuController : ControllerBase<SkyboxMenuView>
    {
        private const int SECONDS_IN_DAY = 86400;

        private readonly StylizedSkyboxSettingsAsset skyboxSettings;
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private CancellationTokenSource skyboxMenuCts = new ();

        public SkyboxMenuController(ViewFactoryMethod viewFactory, StylizedSkyboxSettingsAsset skyboxSettings) : base(viewFactory)
        {
            this.skyboxSettings = skyboxSettings;
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);

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
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();
            skyboxMenuCts = skyboxMenuCts.SafeRestart();
        }

        private void OnUseDynamicTimeChanged(bool dynamic)
        {
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

        /// <summary>
        ///     Auxiliary function to returnt the normalized time in HH:MM:SS
        /// </summary>
        public string GetFormatedTime(float time)
        {
            var totalSec = (int)(time * SECONDS_IN_DAY);

            int hours = totalSec / 3600;
            int minutes = totalSec % 3600 / 60;
            return $"{hours:00}:{minutes:00}";
        }

        private void OnClose()
        {
            CloseAsync().Forget();
        }

        private async UniTaskVoid CloseAsync()
        {
            await HideViewAsync(skyboxMenuCts.Token);
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
