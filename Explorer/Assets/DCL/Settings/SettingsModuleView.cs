using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Settings
{
    public class SettingsModuleView : MonoBehaviour
    {
        [field: SerializeField] public TMP_Text ModuleTitle { get; private set; }
        [field: SerializeField] public Selectable Control { get; private set; }
    }
}
