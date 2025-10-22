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

            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_RESOLUTION))
                view.DropdownView.Dropdown.value = DCLPlayerPrefs.GetInt(DCLPrefKeys.SETTINGS_RESOLUTION);
            else
            {
                for (var index = possibleResolutions.Count - 1; index >= 0; index--)
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

            possibleResolutions.AddRange(ResolutionUtils.GetAvailableResolutions());

            for (int i = 0; i < possibleResolutions.Count; i++)
            {
                Resolution resolution = possibleResolutions[i];
                view.DropdownView.Dropdown.options.Add(
                    new TMP_Dropdown.OptionData
                    {
                        text = ResolutionUtils.FormatResolutionDropdownOption(resolution)
                    }
                );
            }
        }

        private void SetResolutionSettings(int index)
        {
            Resolution selectedResolution = possibleResolutions[index];
            Screen.SetResolution(selectedResolution.width, selectedResolution.height, Screen.fullScreenMode, selectedResolution.refreshRateRatio);
            DCLPlayerPrefs.SetInt(DCLPrefKeys.SETTINGS_RESOLUTION, index, save: true);
            upscalingController.ResolutionChanged(selectedResolution);
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(SetResolutionSettings);
        }
    }
}
