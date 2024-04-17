using DCL.Settings.ModuleViews;
using System.Collections.Generic;
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
                    if (!IsDefaultResolution(resolution))
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
                float aspectRatio = (float)resolution.width / resolution.height;
                if (aspectRatio - (16.0f / 9.0f) != 0f && aspectRatio - (16.0f / 10.0f) != 0f)
                    continue;

                // Exclude all resolutions width less than 1024
                if (resolution.width <= 1024)
                    continue;

                // Exclude possible duplicates
                if (possibleResolutions.Contains(resolution))
                    continue;

                possibleResolutions.Add(resolution);
                view.DropdownView.Dropdown.options.Add(new TMP_Dropdown.OptionData { text = $"{resolution.width}x{resolution.height} ({GetAspectRatio(resolution.width, resolution.height)}) {resolution.refreshRate} Hz" });
            }
        }

        private static string GetAspectRatio(int width, int height)
        {
            int tempWidth = width;
            int tempHeight = height;

            while (height != 0)
            {
                int rest = width % height;
                width = height;
                height = rest;
            }

            return $"{tempWidth / width}:{tempHeight / width}";
        }

        // By design, the default resolution should be a 1080p resolution with a 16:9 aspect ratio
        private static bool IsDefaultResolution(Resolution resolution)
        {
            if (resolution.height != 1080)
                return false;

            float aspectRatio = (float)resolution.width / resolution.height;
            return Mathf.Abs(aspectRatio - (16.0f / 9.0f)) == 0f;
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
