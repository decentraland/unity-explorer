using DCL.Settings.ModuleViews;
using DCL.Settings.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace DCL.Settings.ModuleControllers
{
    public class ResolutionSettingsController : SettingsFeatureController
    {
        private const string RESOLUTION_DATA_STORE_KEY = "Settings_Resolution";

        private readonly SettingsDropdownModuleView view;
        private readonly List<Resolution> possibleResolutions = new ();

        public ResolutionSettingsController(SettingsDropdownModuleView view)
        {
            this.view = view;

            LoadResolutionOptions();

            if (settingsDataStore.HasKey(RESOLUTION_DATA_STORE_KEY))
                view.DropdownView.Dropdown.value = settingsDataStore.GetDropdownValue(RESOLUTION_DATA_STORE_KEY);
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
                    !ResolutionUtils.IsResolutionCompatibleWithAspectRatio(resolution.width, resolution.height, 16, 10))
                    continue;

                // Exclude all resolutions width less than 1024
                if (resolution.width <= 1024)
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
            settingsDataStore.SetDropdownValue(RESOLUTION_DATA_STORE_KEY, index, save: true);
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(SetResolutionSettings);
        }
    }
}
