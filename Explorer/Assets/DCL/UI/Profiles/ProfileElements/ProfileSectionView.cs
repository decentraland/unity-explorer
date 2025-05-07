using MVC;
using UnityEngine;

namespace DCL.UI.ProfileElements
{
    public class ProfileSectionView : ViewBase, IView
    {
        [field: SerializeField] public ProfilePictureView ProfilePictureView { get; private set; } = null!;
        [field: SerializeField] public UserNameElement UserNameElement { get; private set; }
        [field: SerializeField] public UserWalletAddressElement UserWalletAddressElement { get; private set; }
    }
}
