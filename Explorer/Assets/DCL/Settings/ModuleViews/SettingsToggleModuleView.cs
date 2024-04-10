using DCL.UI;
using System;
using UnityEngine;

namespace DCL.Settings.ModuleViews
{
    public class SettingsToggleModuleView : SettingsModuleView<SettingsToggleModuleView.Config>
    {
        [Serializable]
        public class Config : SettingsModuleViewConfiguration
        {
            public bool defaultIsOn;
        }

        [field: SerializeField] public ToggleView ToggleView { get; private set; }

        private void Awake()
        {
            ToggleView.Toggle.onValueChanged.AddListener(isOn =>
            {
                ToggleView.OnImage.SetActive(isOn);
                ToggleView.OffImage.SetActive(!isOn);
                ToggleView.Toggle.targetGraphic = isOn ? ToggleView.OnBackgroundImage : ToggleView.OffBackgroundImage;
            });
        }

        protected override void Configure(Config configuration)
        {
            ToggleView.Toggle.interactable = configuration.IsEnabled;
        }
    }
}
