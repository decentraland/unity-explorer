using DCL.UI;
using DCL.UI.ProfileElements;
using UnityEngine;

namespace DCL.ExplorePanel
{
    public class ProfileSectionElement : MonoBehaviour
    {
        [field: SerializeField] public ImageView FaceSnapshotImage { get; private set; } = null!;
        [field: SerializeField] public UserNameElement UserNameElement { get; private set; }
        [field: SerializeField] public UserWalletAddressElement UserWalletAddressElement { get; private set; }
    }
}
