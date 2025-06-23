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
        private const string STP_DATA_STORE_KEY = "Settings_STP";

        private readonly SettingsDataStore settingsDataStore;
        private readonly float highResolutionPreset;
        private readonly float midResolutionPreset;

        private float savedUpscalingDuringCharacterPreview;
        private bool ignoreFirstResolutionChange;
        [CanBeNull] private SettingsSliderModuleView sliderView;
        private float stepMultiplier;

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
                SetSTPSetting(settingsDataStore.GetSliderValue(STP_DATA_STORE_KEY), false, false);
                ignoreFirstResolutionChange = true;
            }
        }

        private void CharacterViewClosed(CharacterPreviewControllerBase obj) =>
            SetSTPSetting(savedUpscalingDuringCharacterPreview, false, false);

        private void CharacterViewOpened(CharacterPreviewControllerBase obj)
        {
            savedUpscalingDuringCharacterPreview = ((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).renderScale;
            SetSTPSetting(STP_VALUE_FOR_CHARACTER_PREVIEW, false, false);
        }

        //Should always get in decimal form
        private void SetSTPSetting(float sliderValue, bool updateSlider, bool updateStoredValue)
        {
            foreach (RenderPipelineAsset allConfiguredRenderPipeline in GraphicsSettings.allConfiguredRenderPipelines)
                ((UniversalRenderPipelineAsset)allConfiguredRenderPipeline).renderScale = sliderValue;

            if (updateStoredValue)
                settingsDataStore.SetSliderValue(STP_DATA_STORE_KEY, sliderValue);

            if (updateSlider)
                sliderView?.SliderView.Slider.SetValueWithoutNotify(sliderValue * stepMultiplier);
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

            SetSTPSetting(newSTPScale, true, true);
        }

        public void SetSliderModuleView(SettingsSliderModuleView stpSettingsView)
        {
            sliderView = stpSettingsView;
            stepMultiplier = sliderView!.stepMultiplier;
            sliderView!.SliderView.Slider.onValueChanged.AddListener(newValue =>
            {
                //If there is a slider change, we want it to persist when the menu is closed
                savedUpscalingDuringCharacterPreview = newValue;
                SetSTPSetting(newValue / stepMultiplier, false, true);
            });

            sliderView.SliderView.Slider.SetValueWithoutNotify(settingsDataStore.GetSliderValue(STP_DATA_STORE_KEY) * stepMultiplier);
        }
    }
}
