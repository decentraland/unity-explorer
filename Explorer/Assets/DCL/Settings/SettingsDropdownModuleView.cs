using TMPro;
using UnityEngine;

namespace DCL.Settings
{
    public class SettingsDropdownModuleView : SettingsModuleView
    {
        [field: SerializeField] public TMP_Dropdown Dropdown { get; private set; }
        // ...
        // rest of the params for dropdown module!
    }
}
