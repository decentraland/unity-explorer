using System;
using TMPro;
using UnityEngine;

namespace DCL.Settings.ModuleViews
{
    public abstract class SettingsModuleView<TConfig> : MonoBehaviour where TConfig: SettingsModuleViewConfiguration
    {
        [field: SerializeField] public TMP_Text ModuleTitle { get; private set; }
        [field: SerializeField] public TMP_Text DescriptionText { get; private set; }

        public void Configure(SettingsModuleViewConfiguration configuration)
        {
            ModuleTitle.text = configuration.ModuleName;
            if (DescriptionText != null)
                DescriptionText.text = configuration.Description;

            Configure((TConfig) configuration);
        }

        protected abstract void Configure(TConfig configuration);
    }

    [Serializable]
    public class SettingsModuleViewConfiguration
    {
        [field: SerializeField] public string ModuleName { get; private set; }
        [field: SerializeField] public string Description { get; private set; }
        [field: SerializeField] public bool IsEnabled { get; private set; } = true;
    }
}
