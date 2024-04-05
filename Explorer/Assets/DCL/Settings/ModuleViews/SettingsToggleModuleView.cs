using DCL.UI;
using System;
using UnityEngine;

namespace DCL.Settings.ModuleViews
{
    public class SettingsToggleModuleView : SettingsModuleView<SettingsToggleModuleView.Config>
    {
        [Serializable]
        public class Config : SettingsModuleViewConfiguration { }

        [field: SerializeField] public ToggleView ToggleView { get; private set; }

        private void Awake()
        {
            ToggleView.Toggle.onValueChanged.AddListener(isOn =>
            {
                ToggleView.OnImage.SetActive(isOn);
                ToggleView.OffImage.SetActive(!isOn);
            });
        }

        protected override void Configure(Config configuration)
        {
            ToggleView.Toggle.interactable = configuration.IsEnabled;
        }
    }
}
