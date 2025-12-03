using DCL.UI.ProfileElements;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Passport.Modules
{
    public class UserBasicInfo_PassportModuleView : MonoBehaviour
    {
        [field: SerializeField] public UserNameElement UserNameElement { get; private set; } = null!;
        [field: SerializeField] public UserWalletAddressElement UserWalletAddressElement { get; private set; } = null!;
        [field: SerializeField] public Button ClaimNameButton { get; private set; } = null!;
        [field: SerializeField] public Button EditNameButton { get; private set; } = null!;
    }
}
