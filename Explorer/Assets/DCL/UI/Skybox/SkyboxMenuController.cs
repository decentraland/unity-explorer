using Cysharp.Threading.Tasks;
using DCL.StylizedSkybox.Scripts;
using DCL.UI.SharedSpaceManager;
using MVC;
using System.Threading;
using Utility;

namespace DCL.UI.Skybox
{
    public class SkyboxMenuController : ControllerBase<SkyboxMenuView>, IPanelInSharedSpace
    {
        private const int SECONDS_IN_DAY = 86400;

        private readonly StylizedSkyboxSettingsAsset skyboxSettings;
        private readonly ISharedSpaceManager sharedSpaceManager;
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private CancellationTokenSource skyboxMenuCts = new ();

        public SkyboxMenuController(ViewFactoryMethod viewFactory, StylizedSkyboxSettingsAsset skyboxSettings, ISharedSpaceManager sharedSpaceManager) : base(viewFactory)
        {
            this.skyboxSettings = skyboxSettings;
            this.sharedSpaceManager = sharedSpaceManager;
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            ViewShowingComplete?.Invoke(this);
            await UniTask.WaitUntilCanceled(ct);
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
            var totalSec = (int)(time * SECONDS_IN_DAY);

            int hours = totalSec / 3600;
            int minutes = totalSec % 3600 / 60;
            return $"{hours:00}:{minutes:00}";
        }

        private void OnClose()
        {
            sharedSpaceManager.HideAsync(PanelsSharingSpace.Skybox).Forget();
        }

        public override void Dispose()
        {
            base.Dispose();
            skyboxMenuCts.SafeCancelAndDispose();
            skyboxSettings.UseDynamicTimeChanged -= OnUseDynamicTimeChanged;
            skyboxSettings.NormalizedTimeChanged -= OnNormalizedTimeChanged;
        }

        public event IPanelInSharedSpace.ViewShowingCompleteDelegate? ViewShowingComplete;
        public bool IsVisibleInSharedSpace => State != ControllerState.ViewHidden;

        public async UniTask ShowInSharedSpaceAsync(CancellationToken ct, object parameters = null)
        {
            await LaunchViewLifeCycleAsync(new CanvasOrdering(CanvasOrdering.SortingLayer.Overlay, 0), new ControllerNoData(), ct);
        }

        public async UniTask HideInSharedSpaceAsync(CancellationToken ct)
        {
            await HideViewAsync(skyboxMenuCts.Token);
        }
    }
}
