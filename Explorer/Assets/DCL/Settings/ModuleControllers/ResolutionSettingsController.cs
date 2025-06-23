using DCL.Prefs;
using DCL.Settings.ModuleViews;
using DCL.Settings.Utils;
using DCL.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace DCL.Settings.ModuleControllers
{
    public class ResolutionSettingsController : SettingsFeatureController
    {
        private readonly SettingsDropdownModuleView view;
        private readonly List<Resolution> possibleResolutions = new ();
        private readonly UpscalingController upscalingController;

        public ResolutionSettingsController(SettingsDropdownModuleView view, UpscalingController upscalingController)
        {
            this.view = view;
            this.upscalingController = upscalingController;

            LoadResolutionOptions();

            if (settingsDataStore.HasKey(DCLPrefKeys.SETTINGS_RESOLUTION))
                view.DropdownView.Dropdown.value = settingsDataStore.GetDropdownValue(DCLPrefKeys.SETTINGS_RESOLUTION);
            else
            {
                for (var index = 0; index < possibleResolutions.Count; index++)
                {
                    Resolution resolution = possibleResolutions[index];
                    if (!ResolutionUtils.IsDefaultResolution(resolution))
                        continue;

                    view.DropdownView.Dropdown.value = index;
                    break;
                }
            }

            view.DropdownView.Dropdown.onValueChanged.AddListener(SetResolutionSettings);
            SetResolutionSettings(view.DropdownView.Dropdown.value);
        }

        private void LoadResolutionOptions()
        {
            view.DropdownView.Dropdown.options.Clear();

            // By design, we want the list to be inverted so the biggest resolutions stay on top
            for (int index = Screen.resolutions.Length - 1; index >= 0; index--)
            {
                Resolution resolution = Screen.resolutions[index];

                // Exclude all resolutions that are not 16:9 or 16:10
                if (!ResolutionUtils.IsResolutionCompatibleWithAspectRatio(resolution.width, resolution.height, 16, 9) &&
                    !ResolutionUtils.IsResolutionCompatibleWithAspectRatio(resolution.width, resolution.height, 16, 10) &&
                    //Check for vertical monitors as well
                    !ResolutionUtils.IsResolutionCompatibleWithAspectRatio(resolution.width, resolution.height, 9, 16) &&
                    !ResolutionUtils.IsResolutionCompatibleWithAspectRatio(resolution.width, resolution.height, 10, 16))
                    continue;

                // Exclude all resolutions width less than 1024 (same for height in case of vertical monitors)
                if (Mathf.Min(resolution.width, resolution.height) <= 1024)
                    continue;

                // Exclude possible duplicates
                // Equals is not defined in Resolution class. LINQ used only in constructor to mimic a custom Equals
                if (possibleResolutions.Any(res => res.height == resolution.height
                                                   && res.width == resolution.width
                                                   && ((int) Math.Round(res.refreshRateRatio.value)).Equals((int) Math.Round(resolution.refreshRateRatio.value))))
                    continue;

                AddResolution(resolution);
            }

            //Adds a fallback resolution if no other resolution is available
            if (possibleResolutions.Count == 0)
            {
                var resolution = new Resolution
                {
                    width = 1920,
                    height = 1080
                };
                AddResolution(resolution);
            }

            void AddResolution(Resolution resolution)
            {
                possibleResolutions.Add(resolution);
                view.DropdownView.Dropdown.options.Add(new TMP_Dropdown.OptionData
                    { text = ResolutionUtils.FormatResolutionDropdownOption(resolution) });
            }
        }

        private void SetResolutionSettings(int index)
        {
            Resolution selectedResolution = possibleResolutions[index];
            Screen.SetResolution(selectedResolution.width, selectedResolution.height, Screen.fullScreenMode, selectedResolution.refreshRateRatio);
            settingsDataStore.SetDropdownValue(DCLPrefKeys.SETTINGS_RESOLUTION, index, save: true);
            upscalingController.ResolutionChanged(selectedResolution);
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(SetResolutionSettings);
        }
    }
}
