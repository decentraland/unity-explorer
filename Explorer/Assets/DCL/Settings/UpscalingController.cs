using DCL.Platforms;
using DCL.Prefs;
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using MVC;

namespace DCL.Utilities
{
    public class UpscalingController
    {
        private const float STP_VALUE_FOR_UI_OPEN = 1f;
        private const float STP_HIGH_RESOLUTION_WINDOWS = 0.5f;
        private const float STP_HIGH_RESOLUTION_MAC = 0.5f;
        private const float STP_MID_RESOLUTION_MAC = 0.6f;
        private const float STP_MID_RESOLUTION_WINDOWS = 1f;
        private const float INITIAL_UPSCALE_VALUE = 1f;

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

            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_UPSCALER))
            {
                UpdateUpscaling(DCLPlayerPrefs.GetFloat(DCLPrefKeys.SETTINGS_UPSCALER));
                ignoreFirstResolutionChange = true;
            }
            else
                UpdateUpscaling(INITIAL_UPSCALE_VALUE);
        }

        //Should always get in decimal form
        public void UpdateUpscaling(float newValue)
        {
            if (currentUIOpened > 0)
                savedUpscalingDuringUIOpen = newValue;
            else
            {
                SetUpscaling(newValue, UpscalingFilterSelection.FSR);
                DCLPlayerPrefs.SetFloat(DCLPrefKeys.SETTINGS_UPSCALER, newValue);
            }
        }

        public float GetCurrentUpscale() =>
            DCLPlayerPrefs.GetFloat(DCLPrefKeys.SETTINGS_UPSCALER);

        private void OnUIClosed(IController controller)
        {
            if (currentUIOpened > 0 && ShouldTriggerUpscalerChange(controller))
            {
                currentUIOpened--;
                if (currentUIOpened == 0)
                    SetUpscaling(savedUpscalingDuringUIOpen, UpscalingFilterSelection.FSR);
            }
        }

        private void OnUIOpened(IController controller)
        {
            // Only trigger upscaler change for certain types of controllers
            if (ShouldTriggerUpscalerChange(controller))
            {
                if (currentUIOpened == 0)
                {
                    savedUpscalingDuringUIOpen = ((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).renderScale;
                    SetUpscaling(STP_VALUE_FOR_UI_OPEN, UpscalingFilterSelection.Auto);
                }
                currentUIOpened++;
            }
        }

        private void SetUpscaling(float renderScale, UpscalingFilterSelection filterSelection)
        {
            foreach (RenderPipelineAsset allConfiguredRenderPipeline in GraphicsSettings.allConfiguredRenderPipelines)
                ((UniversalRenderPipelineAsset)allConfiguredRenderPipeline).renderScale = renderScale;

            foreach (RenderPipelineAsset allConfiguredRenderPipeline in GraphicsSettings.allConfiguredRenderPipelines)
                ((UniversalRenderPipelineAsset)allConfiguredRenderPipeline).upscalingFilter = filterSelection;
        }

        //This UIs should force an upscaling reset.
        private bool ShouldTriggerUpscalerChange(IController controller)
        {
             string controllerTypeName = controller.GetType().Name;
             return controllerTypeName.Contains("AuthenticationScreenController") ||
                    controllerTypeName.Contains("ExplorePanelController") ||
                    controllerTypeName.Contains("PassportController");
        }

        public void ResolutionChanged(Resolution resolution)
        {
            //TODO (Juani): Resolution setting is not correct. You can chose a higher resolution, even if your monitor is not on that resolution.
            //Therefore, automatically setting it is not currently reliable
            return;

            //Helper bool for the first stp value set. ResolutionChanged is invoked on application start, and if the value does exist in PlayerPrefs,
            //the first invoke should be ignored
            if (ignoreFirstResolutionChange)
            {
                ignoreFirstResolutionChange = false;
                return;
            }

            var newSTPScale = 1f;

            if (resolution.width > 3000 || resolution.height > 3000)
                newSTPScale = highResolutionPreset;
            else if (resolution.width > 2000 || resolution.height > 2000)
                newSTPScale = midResolutionPreset;

            UpdateUpscaling(newSTPScale);
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
