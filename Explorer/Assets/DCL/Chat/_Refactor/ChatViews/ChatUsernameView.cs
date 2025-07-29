using TMPro;
using UnityEngine;

namespace DCL.Chat
{
    public class ChatUsernameView : MonoBehaviour
    {
        [SerializeField] private TMP_Text userNameText;
        [SerializeField] private TMP_Text userNameHashtagText;
        [SerializeField] private GameObject verifiedMark;

        public void Setup(string username,
            string? walletId,
            bool isVerified,
            Color nameColor)
        {
            userNameText.text = username;
            userNameText.color = nameColor;

            verifiedMark.SetActive(isVerified);

            bool showHashtag = !isVerified && !string.IsNullOrEmpty(walletId);
            userNameHashtagText.gameObject.SetActive(showHashtag);

            if (showHashtag)
                userNameHashtagText.text = $"#{walletId[^4..]}";
        }
    }
}