using System;
using TMPro;
using UnityEngine;

namespace DCL.Settings.ModuleViews
{
    public abstract class SettingsModuleView<TConfig> : MonoBehaviour where TConfig: SettingsModuleViewConfiguration
    {
        [field: SerializeField] public TMP_Text ModuleTitle { get; private set; }

        public void Configure(SettingsModuleViewConfiguration configuration)
        {
            ModuleTitle.text = configuration.ModuleName;
            Configure((TConfig) configuration);
        }

        protected abstract void Configure(TConfig configuration);
    }

    [Serializable]
    public class SettingsModuleViewConfiguration
    {
        [field: SerializeField]
        public string ModuleName { get; private set; }
    }
}
