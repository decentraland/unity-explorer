using DCL.Quality.Runtime;
using DCL.Settings.ModuleViews;
using DCL.Settings.Utils;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DCL.Settings.ModuleControllers
{
    public class ResolutionSettingsController : SettingsFeatureController
    {
        private readonly SettingsDropdownModuleView view;
        private readonly List<Resolution> possibleResolutions = new ();
        private readonly IQualitySettingsController qualitySettingsController;

        public ResolutionSettingsController(SettingsDropdownModuleView view, IQualitySettingsController qualitySettingsController)
        {
            this.view = view;
            this.qualitySettingsController = qualitySettingsController;

            LoadResolutionOptions();

            int savedIndex = FindResolutionIndex(qualitySettingsController.ResolutionWidth, qualitySettingsController.ResolutionHeight, qualitySettingsController.ResolutionRefreshRate);

            if (savedIndex >= 0)
                view.DropdownView.Dropdown.value = savedIndex;
            else
            {
                for (int index = 0; index < possibleResolutions.Count; index++)
                {
                    if (!ResolutionUtils.IsDefaultResolution(possibleResolutions[index]))
                        continue;

                    view.DropdownView.Dropdown.value = index;
                    break;
                }
            }

            view.DropdownView.Dropdown.onValueChanged.AddListener(SetResolutionSettingsOnChange);
            SetResolutionSettings(view.DropdownView.Dropdown.value);
        }

        private void SetResolutionSettingsOnChange(int index)
        {
            SetResolutionSettings(index);
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
            Resolution targetResolution = index < 0 || index >= possibleResolutions.Count ? WindowModeUtils.GetDefaultResolution(possibleResolutions) : possibleResolutions[index];
            qualitySettingsController.SetResolution(targetResolution.width, targetResolution.height, targetResolution.refreshRateRatio);
        }

        private int FindResolutionIndex(int width, int height, RefreshRate refreshRate)
        {
            for (int i = 0; i < possibleResolutions.Count; i++)
            {
                Resolution r = possibleResolutions[i];

                if (r.width == width && r.height == height && r.refreshRateRatio.Equals(refreshRate))
                    return i;
            }

            return -1;
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(SetResolutionSettingsOnChange);
        }
    }
}