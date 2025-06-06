using System;
using TMPro;
using UnityEngine;

namespace DCL.Settings.ModuleViews
{
    public class DELETE_ME_SettingsTextModuleView : SettingsModuleView<DELETE_ME_SettingsTextModuleView.Config>
    {
        [field: SerializeField] public TMP_InputField InputField { get; private set; }

        public override void SetInteractable(bool interactable) =>
            InputField.interactable = interactable;

        protected override void Configure(Config configuration)
        {
            InputField.interactable = configuration.IsEnabled;

            if (!string.IsNullOrEmpty(configuration.placeholder))
            {
                if (InputField.placeholder is TMP_Text placeholderText)
                    placeholderText.text = configuration.placeholder;
            }

            if (configuration.characterLimit > 0)
                InputField.characterLimit = configuration.characterLimit;

            if (!string.IsNullOrEmpty(configuration.defaultValue))
                InputField.text = configuration.defaultValue;
        }

        [Serializable]
        public class Config : SettingsModuleViewConfiguration
        {
            public string placeholder = "Enter text...";
            public int characterLimit; // 0 means no limit
            public string defaultValue = "";
        }
    }
}
