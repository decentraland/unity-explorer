using DCL.Utilities;
using TMPro;
using UnityEngine;

namespace DCL.UI
{
    public class ProfileNameTooltipView : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private GameObject verifiedMark;

        public void Setup(string profileName, bool isVerified)
        {
            nameText.text = profileName;
            nameText.color = NameColorHelper.GetNameColor(profileName);
            verifiedMark.SetActive(isVerified);
        }
    }
}
