using DCL.UI.ProfileElements;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Passport.Modules
{
    public class UserBasicInfo_PassportModuleView : MonoBehaviour
    {
        [field: SerializeField] public UserNameElement UserNameElement { get; private set; }
        [field: SerializeField] public UserWalletAddressElement UserWalletAddressElement { get; private set; }
        [field: SerializeField] public Button EditionButton { get; private set; }

    }
}
