using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DCL.Settings.ModuleViews
{
    public class SettingsDropdownModuleView : SettingsModuleView<SettingsDropdownModuleView.Config>
    {
        [Serializable]
        public class Config : SettingsModuleViewConfiguration
        {
            public bool isMultiselect;
            public List<string> options;
        }

        [field: SerializeField] public TMP_Dropdown Dropdown { get; private set; }

        protected override void Configure(Config configuration)
        {
            Dropdown.interactable = configuration.IsEnabled;
            Dropdown.MultiSelect = configuration.isMultiselect;
            Dropdown.options.Clear();
            foreach (string option in configuration.options)
                Dropdown.options.Add(new TMP_Dropdown.OptionData { text = option });
        }
    }
}
