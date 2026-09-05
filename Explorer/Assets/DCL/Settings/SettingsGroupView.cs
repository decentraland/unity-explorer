using TMPro;
using UnityEngine;

namespace DCL.Settings
{
    public class SettingsGroupView : MonoBehaviour
    {
        [field: SerializeField] public TMP_Text GroupTitle { get; private set; }
        [field: SerializeField] public Transform ModulesContainer { get; private set; }
    }
}
