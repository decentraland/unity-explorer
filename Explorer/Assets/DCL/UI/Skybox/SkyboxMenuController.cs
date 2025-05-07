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

            skyboxSettings.NormalizedTimeChanged += OnNormalizedTimeChanged;
            skyboxSettings.UseDynamicTimeChanged += OnUseDynamicTimeChanged;

            if (viewInstance != null)
            {
                // Hook into ToggleView (handles its own graphics & audio)
                var tv = viewInstance.ToggleView;
                tv.Toggle.onValueChanged.AddListener(OnDynamicToggleValueChanged);
                
                viewInstance.TimeSlider.onValueChanged.AddListener(OnTimeSliderValueChanged);
                viewInstance.CloseButton.onClick.AddListener(OnClose);

                tv.Toggle.SetIsOnWithoutNotify(skyboxSettings.UseDynamicTime);
                if (tv.autoToggleImagesOnToggle)
                    tv.SetToggleGraphics(skyboxSettings.UseDynamicTime);
                
                viewInstance.TimeSlider.SetValueWithoutNotify(skyboxSettings.NormalizedTime);
                viewInstance.TimeText.text = GetFormatedTime(skyboxSettings.NormalizedTime);
            }

            OnUseDynamicTimeChanged(skyboxSettings.UseDynamicTime);
            OnNormalizedTimeChanged(skyboxSettings.NormalizedTime);
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();
            skyboxMenuCts = skyboxMenuCts.SafeRestart();
        }

        private void OnUseDynamicTimeChanged(bool dynamic)
        {
            SetTimeEnabled(!dynamic);
            
            if (viewInstance != null)
            {
                var tv = viewInstance.ToggleView;
                tv.Toggle.SetIsOnWithoutNotify(dynamic);
                if (tv.autoToggleImagesOnToggle)
                    tv.SetToggleGraphics(dynamic);
            }
        }

        private void OnDynamicToggleValueChanged(bool dynamic)
        {
            skyboxSettings.UseDynamicTime = dynamic;
        }

        private void OnNormalizedTimeChanged(float time)
        {
            if (viewInstance == null) return;
            
            viewInstance.TimeSlider.SetValueWithoutNotify(time);
            viewInstance.TimeText.text = GetFormatedTime(time);
        }

        private void OnTimeSliderValueChanged(float time)
        {
            skyboxSettings.UseDynamicTime = false;
            skyboxSettings.NormalizedTime = time;
        }

        private void SetTimeEnabled(bool enabled)
        {
            // viewInstance!.TopSliderGroup.enabled = !enabled;
            // viewInstance!.TextSliderGroup.enabled = !enabled;
            
            // enabled=true → slider & text are interactable (manual mode)
            viewInstance!.TopSliderGroup.enabled  = enabled;
            viewInstance!.TextSliderGroup.enabled = enabled;
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
            skyboxMenuCts.Cancel();
        }

        public override void Dispose()
        {
            base.Dispose();
            skyboxMenuCts.SafeCancelAndDispose();
            
            skyboxSettings.UseDynamicTimeChanged  -= OnUseDynamicTimeChanged;
            skyboxSettings.NormalizedTimeChanged  -= OnNormalizedTimeChanged;

            if (viewInstance != null)
            {
                var tv = viewInstance.ToggleView;
                tv.Toggle.onValueChanged.RemoveListener(OnDynamicToggleValueChanged);
                
                viewInstance.TimeSlider.onValueChanged.RemoveListener(OnTimeSliderValueChanged);
                viewInstance.CloseButton.onClick.RemoveListener(OnClose);
            }
        }
    }
}
