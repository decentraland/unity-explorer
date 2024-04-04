using System;
using TMPro;
using UnityEngine;

namespace DCL.Settings.ModuleViews
{
    public class SettingsDropdownModuleView : SettingsModuleView<SettingsDropdownModuleView.Config>
    {
        [Serializable]
        public class Config : SettingsModuleViewConfiguration
        {
            // ...
        }

        [field: SerializeField] public TMP_Dropdown Dropdown { get; private set; }

        protected override void Configure(Config configuration)
        {

        }
    }
}
