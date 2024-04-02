using UnityEngine;

namespace DCL.Settings
{
    public class SettingsView : MonoBehaviour
    {
        [field: SerializeField] public SettingsGroupView SettingsGroupPrefab { get; private set; }
        [field: SerializeField] public SettingsModulesMapping SettingsModulesMapping { get; private set; }
        [field: SerializeField] public SettingsSectionConfiguration GeneralSectionConfiguration { get; private set; }
        [field: SerializeField] public Transform GeneralSectionContainer { get; private set; }
        [field: SerializeField] public Transform GraphicsSectionContainer { get; private set; }
        [field: SerializeField] public Transform AudioSectionContainer { get; private set; }
    }
}
