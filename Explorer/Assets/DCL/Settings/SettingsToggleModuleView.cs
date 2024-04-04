using DCL.UI;
using UnityEngine;

namespace DCL.Settings
{
    public class SettingsToggleModuleView : SettingsModuleView
    {
        [field: SerializeField] public ToggleView Control { get; private set; }
        // ...
        // rest of the params for toggle module!

        private void Awake()
        {
            Control.Toggle.onValueChanged.AddListener(isOn =>
            {
                Control.OnImage.SetActive(isOn);
                Control.OffImage.SetActive(!isOn);
            });
        }
    }
}
