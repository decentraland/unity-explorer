using DCL.Platforms;
using DCL.Prefs;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using MVC;

namespace DCL.Utilities
{
    public class UpscalingController
    {
        // Used when system doesn't meet minimum specs
        public const float MIN_SPECS_UPSCALER_VALUE = 0.8f;

        private const float STP_VALUE_FOR_UI_OPEN = 1f;
        private const float STP_HIGH_RESOLUTION_WINDOWS = 0.5f;
        private const float STP_HIGH_RESOLUTION_MAC = 0.5f;
        private const float STP_MID_RESOLUTION_MAC = 0.6f;
        private const float STP_MID_RESOLUTION_WINDOWS = 1f;

        private readonly float highResolutionPreset;
        private readonly float midResolutionPreset;
        private readonly IMVCManager mvcManager;

        private float savedUpscalingDuringUIOpen;
        private bool ignoreFirstResolutionChange;
        private int currentUIOpened;

        public UpscalingController(IMVCManager mvcManager)
        {
            this.mvcManager = mvcManager;

            if (IPlatform.DEFAULT.Is(IPlatform.Kind.Windows))
            {
                highResolutionPreset = STP_HIGH_RESOLUTION_WINDOWS;
                midResolutionPreset = STP_MID_RESOLUTION_WINDOWS;
            }
            else
            {
                highResolutionPreset = STP_HIGH_RESOLUTION_MAC;
                midResolutionPreset = STP_MID_RESOLUTION_MAC;
            }

            mvcManager.OnViewShowed += OnUIOpened;
            mvcManager.OnViewClosed += OnUIClosed;
        }

        //Should always get in decimal form
        public void UpdateUpscaling(float newValue)
        {
            if (currentUIOpened > 0)
                savedUpscalingDuringUIOpen = newValue;
            else { SetUpscaling(newValue, UpscalingFilterSelection.FSR); }
        }

        private void OnUIClosed(IController controller)
        {
            if (ShouldTriggerUpscalerChange(controller))
            {
                currentUIOpened--;

                if (currentUIOpened == 0)
                    UpdateUpscaling(savedUpscalingDuringUIOpen);
            }
        }

        private void OnUIOpened(IController controller)
        {
            // Only trigger upscaler change for certain types of controllers
            if (ShouldTriggerUpscalerChange(controller))
            {
                savedUpscalingDuringUIOpen = ((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).renderScale;
                SetUpscaling(STP_VALUE_FOR_UI_OPEN, UpscalingFilterSelection.Auto);
                currentUIOpened++;
            }
        }

        private void SetUpscaling(float renderScale, UpscalingFilterSelection filterSelection)
        {
            foreach (RenderPipelineAsset allConfiguredRenderPipeline in GraphicsSettings.allConfiguredRenderPipelines)
            {
                var renderPipeline = ((UniversalRenderPipelineAsset)allConfiguredRenderPipeline);
                renderPipeline.renderScale = renderScale;
                renderPipeline.upscalingFilter = filterSelection;
            }
        }

        //This UIs should force an upscaling reset.
        private bool ShouldTriggerUpscalerChange(IController controller)
        {
            string controllerTypeName = controller.GetType().Name;
            return controllerTypeName.Contains("AuthenticationScreenController") ||
                   controllerTypeName.Contains("ExplorePanelController") ||
                   controllerTypeName.Contains("PassportController");
        }

        public void Dispose()
        {
            if (mvcManager != null)
            {
                mvcManager.OnViewShowed -= OnUIOpened;
                mvcManager.OnViewClosed -= OnUIClosed;
            }
        }
    }
}
