using UnityEngine;
using UnityEngine.UI;

namespace DCL.Settings
{
    public class SettingsSliderModuleView : SettingsModuleView
    {
        [field: SerializeField] public Slider Slider { get; private set; }
        // ...
        // rest of the params for slider module!
    }
}
