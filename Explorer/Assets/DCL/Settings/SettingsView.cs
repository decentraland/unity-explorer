using UnityEngine;
using UnityEngine.UI;

namespace DCL.Settings
{
    public class SettingsView : MonoBehaviour
    {
        [field: SerializeField] public SettingsMenuConfiguration Configuration { get; private set; }
        [field: SerializeField] public Transform GeneralSectionContainer { get; private set; }
        [field: SerializeField] public Transform GraphicsSectionContainer { get; private set; }
        [field: SerializeField] public Transform SoundSectionContainer { get; private set; }
        [field: SerializeField] public Transform ControlsSectionContainer { get; private set; }
        [field: SerializeField] public Button GeneralSectionButton { get; private set; }
        [field: SerializeField] public Button GraphicsSectionButton { get; private set; }
        [field: SerializeField] public Button SoundSectionButton { get; private set; }
        [field: SerializeField] public Button ControlsSectionButton { get; private set; }
        [field: SerializeField] public ScrollRect ContentScrollRect { get; private set; }
    }
}
