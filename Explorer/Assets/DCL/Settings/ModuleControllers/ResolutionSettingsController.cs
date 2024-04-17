using DCL.Settings.ModuleViews;
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

            view.DropdownView.Dropdown.value = settingsDataStore.HasKey(RESOLUTION_DATA_STORE_KEY) ?
                settingsDataStore.GetDropdownValue(RESOLUTION_DATA_STORE_KEY) :
                0;

            view.DropdownView.Dropdown.onValueChanged.AddListener(SetResolutionSettings);
            SetResolutionSettings(view.DropdownView.Dropdown.value);
        }

        private void LoadResolutionOptions()
        {
            view.DropdownView.Dropdown.options.Clear();
            possibleResolutions.AddRange(Screen.resolutions.SkipWhile(r => r.width <= 1024));

            int length = possibleResolutions.Count;
            var resolutionLabels = new string[length];
            for (var i = 0; i < length; i++)
            {
                var resolution = possibleResolutions[i];

                // by design, we want the list to be inverted so the biggest resolutions stay on top
                // our resolutionSizeIndex is based on this decision
                resolutionLabels[length - 1 - i] = $"{resolution.width}x{resolution.height} ({GetAspectRatio(resolution.width, resolution.height)}) {resolution.refreshRate} Hz";;
            }

            foreach (string label in resolutionLabels)
                view.DropdownView.Dropdown.options.Add(new TMP_Dropdown.OptionData { text = label });
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
