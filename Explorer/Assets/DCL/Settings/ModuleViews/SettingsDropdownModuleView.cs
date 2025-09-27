using DCL.UI;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace DCL.Settings.ModuleViews
{
    public class SettingsDropdownModuleView : SettingsModuleView<SettingsDropdownModuleView.Config>
    {
        public UnityEvent<bool> showStatusUpdated = new ();

        [Serializable]
        public class Config : SettingsModuleViewConfiguration
        {
            public bool isMultiselect;
            public List<string> options;
            public int defaultOptionIndex;
        }

        [field: SerializeField] public DropdownView DropdownView { get; private set; }

        public override void SetInteractable(bool interactable) =>
            DropdownView.Dropdown.interactable = interactable;

        public override void SetActive(bool isActive) =>
            gameObject.SetActive(isActive);

        protected override void Configure(Config configuration)
        {
            DropdownView.Dropdown.interactable = configuration.IsEnabled;
            DropdownView.Dropdown.MultiSelect = configuration.isMultiselect;
            DropdownView.Dropdown.options.Clear();

            foreach (string option in configuration.options)
                DropdownView.Dropdown.options.Add(new TMP_Dropdown.OptionData { text = option });

            if (DropdownView.Dropdown.options.Count > configuration.defaultOptionIndex && configuration.defaultOptionIndex >= 0)
                DropdownView.Dropdown.value = configuration.defaultOptionIndex;
        }

        private void OnEnable()
        {
            showStatusUpdated.Invoke(true);
        }

        private void OnDisable()
        {
            showStatusUpdated.Invoke(false);
        }
    }
}
