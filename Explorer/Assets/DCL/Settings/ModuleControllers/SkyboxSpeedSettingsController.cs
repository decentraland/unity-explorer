using DCL.Settings.ModuleViews;
using DCL.StylizedSkybox.Scripts.Plugin;
using System;
using System.Linq;
using TMPro;

namespace DCL.Settings.ModuleControllers
{
    public class SkyboxSpeedSettingsController : SettingsFeatureController
    {
        private readonly SettingsDropdownModuleView view;
        private readonly StylizedSkyboxSettingsAsset skyboxSettingsAsset;

        public SkyboxSpeedSettingsController(SettingsDropdownModuleView view, StylizedSkyboxSettingsAsset skyboxSettingsAsset)
        {
            this.view = view;
            this.skyboxSettingsAsset = skyboxSettingsAsset;

            SetupOptions();

            this.skyboxSettingsAsset.SpeedChanged += OnSpeedChanged;
            view.DropdownView.Dropdown.onValueChanged.AddListener(SetSpeed);
        }

        private void OnSpeedChanged(StylizedSkyboxSettingsAsset.TimeProgression speed)
        {
            view.DropdownView.Dropdown.value = (int)speed;
        }

        private void SetSpeed(int index)
        {
            skyboxSettingsAsset.Speed = (StylizedSkyboxSettingsAsset.TimeProgression)index;
        }

        private void SetupOptions()
        {
            view.DropdownView.Dropdown.options =
                Enum.GetValues(typeof(StylizedSkyboxSettingsAsset.TimeProgression))
                    .Cast<StylizedSkyboxSettingsAsset.TimeProgression>()
                    .Select(x => new TMP_Dropdown.OptionData(x.ToString()))
                    .ToList();

            view.DropdownView.Dropdown.value = (int)StylizedSkyboxSettingsAsset.TimeProgression.Default;
        }

        public override void Dispose()
        {
            this.skyboxSettingsAsset.SpeedChanged -= OnSpeedChanged;
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(SetSpeed);
        }
    }
}
