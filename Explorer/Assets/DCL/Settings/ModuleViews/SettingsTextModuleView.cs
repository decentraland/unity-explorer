using System;
using TMPro;
using UnityEngine;

namespace DCL.Settings.ModuleViews
{
    public class SettingsTextModuleView : SettingsModuleView<SettingsTextModuleView.Config>
    {
        [Serializable]
        public class Config : SettingsModuleViewConfiguration
        {
            public string placeholder = "Enter text...";
            public int characterLimit = 0; // 0 means no limit
            public string defaultValue = "";
        }

        [field: SerializeField] public TMP_InputField InputField { get; private set; }

        private void Awake()
        {
            // Initialize input field if needed
        }

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
    }
} 