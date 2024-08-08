using DCL.ExplorePanel;
using MVC;
using UnityEngine;

namespace DCL.UI.Sidebar
{
    public class ProfileMenuView : ViewBaseWithAnimationElement, IView
    {
        [field: SerializeField] public ProfileSectionElement ProfileMenu { get; private set; }
        [field: SerializeField] public SystemMenuView SystemMenuView { get; private set; }
    }
}
