using DCL.Profiles;
using TMPro;
using UnityEngine;

namespace DCL.UI.ProfileElements
{
    public class SimpleUserNameElement : MonoBehaviour
    {
        [field: SerializeField] private TMP_Text userNameText;
        [field: SerializeField] private TMP_Text userNameHashtagText;
        [field: SerializeField] private GameObject verifiedMark;

        public void Setup(Profile profile, Color usernameColor)
        {
            userNameText.text = profile.Name;
            userNameText.color = usernameColor;
            userNameHashtagText.gameObject.SetActive(!profile.HasClaimedName);

            if (!profile.HasClaimedName)
                userNameHashtagText.text = profile.WalletId;

            verifiedMark.SetActive(profile.HasClaimedName);
        }
    }
}
