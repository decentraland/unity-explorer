﻿using DCL.Prefs;
using DCL.Settings.ModuleViews;
using DCL.Settings.Utils;
using DCL.Utilities;
using Global.AppArgs;
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
        private readonly IAppArgs appParameters;

        public ResolutionSettingsController(SettingsDropdownModuleView view, UpscalingController upscalingController, IAppArgs appParameters)
        {
            this.view = view;
            this.upscalingController = upscalingController;

            LoadResolutionOptions();

            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_RESOLUTION))
                view.DropdownView.Dropdown.value = DCLPlayerPrefs.GetInt(DCLPrefKeys.SETTINGS_RESOLUTION);
            else
            {
                //When running multiple instances from local scene, the last one opened will set the resolution to the lowest available
                bool isLocalScene = appParameters.HasFlag(AppArgsFlags.LOCAL_SCENE);
                int startIndex = isLocalScene ? possibleResolutions.Count - 1 : 0;
                int endIndex = isLocalScene ? -1 : possibleResolutions.Count;
                int step = isLocalScene ? -1 : 1;

                for (int index = startIndex; index != endIndex; index += step)
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
