using DCL.CharacterPreview;
using DCL.Platforms;
using DCL.Settings;
using DCL.Settings.ModuleControllers;
using DCL.Settings.ModuleViews;
using JetBrains.Annotations;
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DCL.Utilities
{
    public class UpscalingController
    {
        private const float STP_VALUE_FOR_CHARACTER_PREVIEW = 1f;
        private const float STP_HIGH_RESOLUTION_WINDOWS = 0.5f;
        private const float STP_HIGH_RESOLUTION_MAC = 0.5f;
        private const float STP_MID_RESOLUTION_MAC = 0.6f;
        private const float STP_MID_RESOLUTION_WINDOWS = 1f;
        private const string STP_DATA_STORE_KEY = "Settings_Upscaler";
        private const float INITIAL_UPSCALE_VALUE = 1f;

        private readonly SettingsDataStore settingsDataStore;
        private readonly float highResolutionPreset;
        private readonly float midResolutionPreset;

        private float savedUpscalingDuringCharacterPreview;
        private bool ignoreFirstResolutionChange;
        private bool isCharacterPreviewActive;

        public Action<float> OnUpscalingChanged;

        public UpscalingController(CharacterPreviewEventBus characterPreviewEventBus)
        {
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

            characterPreviewEventBus.OnAnyCharacterPreviewShowEvent += CharacterViewOpened;
            characterPreviewEventBus.OnAnyCharacterPreviewHideEvent += CharacterViewClosed;
            savedUpscalingDuringCharacterPreview = ((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).renderScale;

            settingsDataStore = new SettingsDataStore();

            if (settingsDataStore.HasKey(STP_DATA_STORE_KEY))
            {
                SetUpscalingValue(settingsDataStore.GetSliderValue(STP_DATA_STORE_KEY), false);
                ignoreFirstResolutionChange = true;
            }
            else
            {
                SetUpscalingValue(INITIAL_UPSCALE_VALUE, true);
            }
        }

        private void CharacterViewClosed(CharacterPreviewControllerBase obj)
        {
            if (isCharacterPreviewActive)
            {
                SetUpscalingValue(savedUpscalingDuringCharacterPreview, false);
                isCharacterPreviewActive = false;
            }
        }

        private void CharacterViewOpened(CharacterPreviewControllerBase obj)
        {
            isCharacterPreviewActive = true;
            savedUpscalingDuringCharacterPreview = ((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).renderScale;

            //Render scale for character view should have a fixed value
            SetUpscalingValue(STP_VALUE_FOR_CHARACTER_PREVIEW, false);
        }

        //Should always get in decimal form
        public void SetUpscalingValue(float sliderValue, bool updateStoredValue)
        {
            foreach (RenderPipelineAsset allConfiguredRenderPipeline in GraphicsSettings.allConfiguredRenderPipelines)
                ((UniversalRenderPipelineAsset)allConfiguredRenderPipeline).renderScale = sliderValue;

            if (updateStoredValue)
            {
                settingsDataStore.SetSliderValue(STP_DATA_STORE_KEY, sliderValue);
                OnUpscalingChanged?.Invoke(sliderValue);
            }
        }

        public void ResolutionChanged(Resolution resolution)
        {
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

            SetUpscalingValue(newSTPScale, true);
        }

        public float GetCurrentUpscale() =>
            settingsDataStore.GetSliderValue(STP_DATA_STORE_KEY);
    }
}
