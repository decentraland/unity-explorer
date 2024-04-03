using TMPro;
using UnityEngine;

namespace DCL.Settings
{
    public class SettingsModuleView : MonoBehaviour
    {
        [field: SerializeField] public TMP_Text ModuleTitle { get; private set; }
        // ...
        // rest of the common params for all modules!
    }
}
