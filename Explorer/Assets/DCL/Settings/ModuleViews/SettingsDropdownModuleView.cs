using DCL.UI;
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
            public int defaultOptionIndex;
        }

        [field: SerializeField] public DropdownView DropdownView { get; private set; }

        protected override void Configure(Config configuration)
        {
            DropdownView.Dropdown.interactable = configuration.IsEnabled;
            DropdownView.Dropdown.MultiSelect = configuration.isMultiselect;
            DropdownView.Dropdown.options.Clear();
            foreach (string option in configuration.options)
                DropdownView.Dropdown.options.Add(new TMP_Dropdown.OptionData { text = option });
            DropdownView.Dropdown.value = configuration.defaultOptionIndex;
        }
    }
}
