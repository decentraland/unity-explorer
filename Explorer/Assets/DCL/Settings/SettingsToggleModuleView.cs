using UnityEngine;
using UnityEngine.UI;

namespace DCL.Settings
{
    public class SettingsToggleModuleView : SettingsModuleView
    {
        [field: SerializeField] public Toggle Toggle { get; private set; }
        // ...
        // rest of the params for toggle module!
    }
}
