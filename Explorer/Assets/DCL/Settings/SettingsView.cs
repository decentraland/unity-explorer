using DCL.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Settings
{
    public class SettingsView : MonoBehaviour
    {
        [field: Header("Containers")]
        [field: SerializeField] public Transform GeneralSectionContainer { get; private set; } = null!;
        [field: SerializeField] public Transform GraphicsSectionContainer { get; private set; } = null!;
        [field: SerializeField] public Transform SoundSectionContainer { get; private set; } = null!;
        [field: SerializeField] public Transform ControlsSectionContainer { get; private set; } = null!;
        [field: SerializeField] public Transform ChatSectionContainer { get; private set; } = null!;
        [field: SerializeField] public Transform LobbySectionContainer { get; private set; } = null!;

        [field: Header("Header Buttons")]
        [field: SerializeField] public ButtonWithSelectableStateView GeneralSectionButton { get; private set; } = null!;
        [field: SerializeField] public ButtonWithSelectableStateView GraphicsSectionButton { get; private set; } = null!;
        [field: SerializeField] public ButtonWithSelectableStateView SoundSectionButton { get; private set; } = null!;
        [field: SerializeField] public ButtonWithSelectableStateView ControlsSectionButton { get; private set; } = null!;
        [field: SerializeField] public ButtonWithSelectableStateView ChatSectionButton { get; private set; } = null!;
        [field: SerializeField] public ButtonWithSelectableStateView LobbySectionButton { get; private set; } = null!;

        [field: Header("Sections Backgrounds")]
        [field: SerializeField] public Image BackgroundImage { get; private set; } = null!;
        [field: SerializeField] public Sprite GeneralSectionBackground { get; private set; } = null!;
        [field: SerializeField] public Sprite GraphicsSectionBackground { get; private set; } = null!;
        [field: SerializeField] public Sprite SoundSectionBackground { get; private set; } = null!;
        [field: SerializeField] public Sprite ControlsSectionBackground { get; private set; } = null!;
        [field: SerializeField] public Sprite ChatSectionBackground { get; private set; } = null!;
        [field: SerializeField] public Sprite LobbySectionBackground { get; private set; } = null!;

        [field: Header("Others")]
        [field: SerializeField] public ScrollRect ContentScrollRect { get; private set; } = null!;
        [field: SerializeField] public Animator PanelAnimator { get; private set; } = null!;
        [field: SerializeField] public Animator HeaderAnimator { get; private set; } = null!;
    }
}
