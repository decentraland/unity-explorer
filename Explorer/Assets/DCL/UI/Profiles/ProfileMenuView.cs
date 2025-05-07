using DCL.UI.ProfileElements;
using DCL.UI.SystemMenu;
using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.Profiles
{
    public class ProfileMenuView : ViewBaseWithAnimationElement, IView
    {
        [field: SerializeField] public ProfileSectionView ProfileMenu { get; private set; } = null!;
        [field: SerializeField] public SystemMenuView SystemMenuView { get; private set; } = null!;
        [field: SerializeField] public Button CloseButton { get; private set; } = null!;
    }
}
