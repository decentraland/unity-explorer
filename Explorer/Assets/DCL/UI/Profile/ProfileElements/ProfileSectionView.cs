using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.ProfileElements
{
    public class ProfileSectionView : ViewBase, IView
    {
        [field: SerializeField] public ImageView FaceSnapshotImage { get; private set; } = null!;
        [field: SerializeField] public Image FaceFrame { get; private set; }
        [field: SerializeField] public UserNameElement UserNameElement { get; private set; }
        [field: SerializeField] public UserWalletAddressElement UserWalletAddressElement { get; private set; }
    }
}
