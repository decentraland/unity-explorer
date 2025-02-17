using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class ChatEntryUsernameElement : MonoBehaviour
    {
        [field: SerializeField] internal TMP_Text userName { get; private set; }
        [field: SerializeField] internal TMP_Text walletIdText { get; private set; }
        [field: SerializeField] internal Image? verifiedIcon { get; private set; }

        public void SetUsername(string username, string? walletId)
        {
            userName.text = username;
            walletIdText.text = walletId;

            bool hasWalletId = !string.IsNullOrEmpty(walletId);

            walletIdText.gameObject.SetActive(hasWalletId);
            verifiedIcon?.gameObject.SetActive(!hasWalletId);
        }

        public float GetUserNamePreferredWidth(float backgroundWidthOffset, float verifiedBadgeWidth) =>
            userName.preferredWidth + Math.Max(walletIdText.preferredWidth, verifiedBadgeWidth) + backgroundWidthOffset;
    }
}
