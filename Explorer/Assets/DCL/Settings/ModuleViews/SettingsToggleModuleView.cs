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
            // ...
        }

        [field: SerializeField] public ToggleView Control { get; private set; }

        protected override void Configure(Config configuration)
        {

        }
    }
}
