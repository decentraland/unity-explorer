using TMPro;
using UnityEngine;

namespace DCL.Chat.ChatViews
{
    public class ChatUsernameView : MonoBehaviour
    {
        [SerializeField] private TMP_Text userNameText;
        [SerializeField] private TMP_Text userNameHashtagText;
        [SerializeField] private GameObject verifiedMark;
        [SerializeField] private GameObject officialTag;

        public void Setup(string username,
            string? walletId,
            bool isVerified,
            bool isOfficial,
            Color nameColor)
        {
            userNameText.text = username;
            userNameText.color = nameColor;

            verifiedMark.SetActive(isVerified);
            officialTag.SetActive(isOfficial);

            bool showHashtag = !isVerified && !string.IsNullOrEmpty(walletId);
            userNameHashtagText.gameObject.SetActive(showHashtag);

            if (showHashtag)
                userNameHashtagText.text = $"#{walletId[^4..]}";
        }
    }
}
