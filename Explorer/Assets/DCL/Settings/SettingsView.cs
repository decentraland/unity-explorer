using DCL.Settings.Configuration;
using DCL.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Settings
{
    public class SettingsView : MonoBehaviour
    {
        [field: Header("Containers")]
        [field: SerializeField] public Transform GeneralSectionContainer { get; private set; }
        [field: SerializeField] public Transform GraphicsSectionContainer { get; private set; }
        [field: SerializeField] public Transform SoundSectionContainer { get; private set; }
        [field: SerializeField] public Transform ControlsSectionContainer { get; private set; }

        [field: Header("Header Buttons")]
        [field: SerializeField] public ButtonWithSelectableStateView GeneralSectionButton { get; private set; }
        [field: SerializeField] public ButtonWithSelectableStateView GraphicsSectionButton { get; private set; }
        [field: SerializeField] public ButtonWithSelectableStateView SoundSectionButton { get; private set; }
        [field: SerializeField] public ButtonWithSelectableStateView ControlsSectionButton { get; private set; }
        [field: Header("Sections Backgrounds")]
        [field: SerializeField] public Image BackgroundImage { get; private set; }
        [field: SerializeField] public Sprite GeneralSectionBackground { get; private set; }
        [field: SerializeField] public Sprite GraphicsSectionBackground { get; private set; }
        [field: SerializeField] public Sprite SoundSectionBackground { get; private set; }
        [field: SerializeField] public Sprite ControlsSectionBackground { get; private set; }
        [field: Header("Others")]
        [field: SerializeField] public ScrollRect ContentScrollRect { get; private set; }
        [field: SerializeField] public Animator PanelAnimator { get; private set; }
        [field: SerializeField] public Animator HeaderAnimator { get; private set; }
    }
}
